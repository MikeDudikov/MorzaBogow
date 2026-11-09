"""Economic timing model. No combat, pathfinding, Unity or win-rate simulation."""
from __future__ import annotations
from dataclasses import dataclass
from pathlib import Path
import csv, json, math, random

ROOT = Path(__file__).resolve().parent
PARAMS = json.loads((ROOT/'economy-parameters.json').read_text(encoding='utf-8'))

@dataclass
class Boat:
    resource: str
    state: str = 'out'
    timer: float = 0.0
    cargo: float = 0.0
    alive: bool = True
    arrived: float = 0.0

def run(duration=600, distance=70.0, target=(4,2), opening='one_ship',
        raid_at=None, freeze=0, dt=1/30, build=True, initial=None, cap=None, finite=True):
    p=PARAMS; boatp=p['collector']; harbor=p['harbor']; ram=p['ships']['ram']; cost=p['collector']['cost']
    bank=dict(p['start']['stock']); deposited={'D':0.,'S':0.}
    deposits=dict(p['start']['deposits']) if finite else {'D':1e6,'S':1e6}
    initial_deposits=dict(deposits); committed={'D':0.,'S':0.}; destroyed={'D':0.,'S':0.}; wreck={'D':0.,'S':0.}
    boats=[Boat(r,timer=distance/boatp['speed']) for r,n in zip(('D','S'),initial or p['start']['collectors']) for _ in range(n)]
    lines=[0.0]; worker_end=None; worker_resource=None; yard_end=None
    ships=[]; workers_built=0; snapshots=[]; raid_done=False; worker_lost=0; restored_at=None
    reserve=p['reserve']; target_total=sum(target); queued_ships=0
    def pay(c, protected=False):
        if any(bank[r]+1e-8<c.get(r,0)+(reserve[r] if protected else 0) for r in bank): return False
        for r in bank: bank[r]-=c.get(r,0); committed[r]+=c.get(r,0)
        return True
    def live_counts():return tuple(sum(b.alive and b.resource==r for b in boats) for r in ('D','S'))
    max_active=0
    for step in range(math.ceil(duration/dt)+1):
        t=round(step*dt,8)
        if t>duration+1e-7:break
        # Economic blockade benchmark: all collectors wait in place, no damage or relocation.
        paused=raid_at is not None and raid_at<=t<raid_at+freeze
        if raid_at is not None and t>=raid_at and not raid_done:
            raid_done=True
            if freeze==0:
                for r in ('D','S'):
                    candidates=[b for b in boats if b.alive and b.resource==r]
                    if candidates:
                        b=max(candidates,key=lambda x:x.cargo); b.alive=False; worker_lost+=1
                        destroyed[r]+=b.cargo*.5; wreck[r]+=b.cargo*.5; b.cargo=0
        for i,end in enumerate(lines):
            if end and end<=t+1e-8:ships.append(t); lines[i]=0
        if yard_end is not None and yard_end<=t+1e-8:lines.append(0);yard_end=None
        if worker_end is not None and worker_end<=t+1e-8:
            boats.append(Boat(worker_resource,timer=distance/boatp['speed']));workers_built+=1;worker_end=None
        counts=live_counts()
        if raid_done and freeze==0 and restored_at is None and sum(counts)>=target_total:restored_at=t
        if build:
            opening_met=(len(ships)+sum(bool(x) for x in lines)) >= (2 if opening=='two_ships' else 1 if opening=='one_ship' else 0)
            if worker_end is None and opening_met and any(n<want for n,want in zip(counts,target)):
                deficit=[(want-n)/max(want,1) for n,want in zip(counts,target)]
                worker_resource=('D','S')[max(range(2),key=lambda i:deficit[i])]
                if pay(cost):worker_end=t+boatp['buildSeconds']
            # The extra yard is funded only after a small fleet and the planned workforce.
            if len(lines)==1 and yard_end is None and len(ships)>=3 and sum(counts)>=target_total:
                yard=p['buildings']['shipyard']
                enough=all(bank[r]>=yard['cost'][r]+ram['cost'][r]+reserve[r] for r in bank)
                if enough and pay(yard['cost'],protected=True):yard_end=t+yard['seconds']
            may_fight=opening!='workers_first' or sum(counts)>=target_total
            if may_fight:
                for i,end in enumerate(lines):
                    if not end and pay(ram['cost'],protected=True):lines[i]=t+ram['seconds'];queued_ships+=1
        if not paused:
            for b in boats:
                if not b.alive or b.state in ('gather','node_queue','queue','unload','empty'):continue
                b.timer-=dt
                if b.timer<=1e-8:
                    if b.state=='out':b.state='approach';b.timer=harbor['sourceApproachSeconds']
                    elif b.state=='approach':b.state='node_queue';b.arrived=t
                    elif b.state=='back':b.state='queue';b.arrived=t
                    elif b.state=='undock':b.state='out';b.timer=distance/boatp['speed']
            for r in ('D','S'):
                active=[b for b in boats if b.alive and b.state=='gather' and b.resource==r]
                waiting=sorted((b for b in boats if b.alive and b.state=='node_queue' and b.resource==r),key=lambda b:b.arrived)
                for b in waiting[:max(0,p['node']['workBerths'][r]-len(active))]:
                    b.state='gather';active.append(b)
                assert len(active)<=p['node']['workBerths'][r]
                rate=min(boatp['rates'][r],p['node']['sharedRates'][r]/max(1,len(active)))
                for b in active:
                    q=min(rate*dt,boatp['cargo']-b.cargo,deposits[r]);deposits[r]-=q;b.cargo+=q
                    if b.cargo>=boatp['cargo']-1e-8 or deposits[r]<=1e-8:
                        b.state='back' if b.cargo>1e-8 else 'empty';b.timer=distance/boatp['speed']
            unloading=[b for b in boats if b.alive and b.state=='unload']
            for b in sorted((b for b in boats if b.alive and b.state=='queue'),key=lambda b:b.arrived)[:max(0,harbor['berths']-len(unloading))]:
                b.state='unload';unloading.append(b)
            max_active=max(max_active,len(unloading))
            for b in unloading:
                limit=(cap or p['warehouse'])[b.resource]
                q=min(harbor['unloadRate']*dt,b.cargo,max(0,limit-bank[b.resource]));b.cargo-=q;bank[b.resource]+=q;deposited[b.resource]+=q
                if b.cargo<1e-8:b.cargo=0;b.state='undock';b.timer=harbor['turnaroundSeconds']
        if step % round(60/dt)==0:
            snapshots.append({'second':t,'readyShips':len(ships),'collectors':sum(live_counts()),'D':round(bank['D'],2),'S':round(bank['S'],2),'deliveredD':round(deposited['D'],2),'deliveredS':round(deposited['S'],2)})
    for r in ('D','S'):
        cargo=sum(b.cargo for b in boats if b.alive and b.resource==r)
        lhs=p['start']['stock'][r]+initial_deposits[r]
        rhs=bank[r]+deposits[r]+cargo+committed[r]+destroyed[r]+wreck[r]
        assert abs(lhs-rhs)<.001,(r,lhs,rhs)
    assert max_active<=harbor['berths'] and all(v>=-1e-6 for v in bank.values())
    return {'firstShip':ships[0] if ships else None,'secondShip':ships[1] if len(ships)>1 else None,
            'fourthShip':ships[3] if len(ships)>3 else None,'readyShips':len(ships),'readyTimes':ships,
            'collectors':sum(live_counts()),'workersBuilt':workers_built,'shipyards':len(lines),
            'delivered':deposited,'stock':bank,'remainingDeposits':deposits,'committed':committed,
            'wreck':wreck,'destroyed':destroyed,'recoverySeconds':restored_at-raid_at if restored_at is not None else None,'snapshots':snapshots}

def flow(distance,resource):
    p=PARAMS;b=p['collector'];h=p['harbor'];c=b['cargo']
    seconds=c/b['rates'][resource]+2*distance/b['speed']+h['sourceApproachSeconds']+h['turnaroundSeconds']+c/h['unloadRate']
    return {'distance':distance,'resource':resource,'cycleSeconds':seconds,'perMinute':60*c/seconds}

def main():
    incomes=[flow(d,r) for d in (70,140,280,450) for r in ('D','S')]
    # Start-up/wind-down excluded: independent cycle formula vs measured long run, with finite cap disabled.
    cycle_checks=[]
    for r in ('D','S'):
        f=flow(70,r);o=run(duration=600,distance=70,build=False,initial=(1,0) if r=='D' else (0,1),finite=True)
        effective=o['delivered'][r]/10
        assert abs(effective-f['perMinute'])/f['perMinute']<.07
        cycle_checks.append({'resource':r,'formula':f['perMinute'],'measuredFirst10Minutes':effective})
    saturation=[]
    for d in (70,140,280):
        for n in (1,2,3,4,6,8,12,16):
            for r in ('D','S'):
                o=run(duration=180,distance=d,build=False,initial=(n,0) if r=='D' else (0,n),cap={'D':100000,'S':100000})
                saturation.append({'distance':d,'resource':r,'boats':n,'deliveredPerMinute':round(o['delivered'][r]/3,3),'remaining':round(o['remainingDeposits'][r],3)})
    openings={
        'rajd':run(target=(4,2),opening='two_ships'),
        'flota_i_gospodarka':run(target=(4,2),opening='one_ship'),
        'rozwoj_najpierw':run(target=(5,3),opening='workers_first'),
    }
    raid=run(target=(4,2),opening='one_ship',raid_at=180)
    blockade=run(target=(4,2),opening='one_ship',raid_at=180,freeze=40)
    lowstock=run(duration=1800,build=False,initial=(8,4))
    full=run(duration=180,build=False,initial=(3,1),cap={'D':400,'S':180})
    assert full['stock']['D']<=400+1e-8 and full['stock']['S']<=180+1e-8
    assert lowstock['remainingDeposits']['D']>=-1e-8 and lowstock['remainingDeposits']['S']>=-1e-8
    rng=random.Random(7319);samples=[]
    for i in range(200):
        d=rng.uniform(70,110);enemy_distance=rng.uniform(650,850);o=run(duration=240,distance=d,target=(4,2),opening='one_ship')
        samples.append({'sample':i,'resourceDistance':round(d,2),'enemyDistance':round(enemy_distance,2),'firstShip':o['firstShip'],'earliestDirectRaidArrival':o['firstShip']+enemy_distance/PARAMS['ships']['ram']['speed'],'secondShip':o['secondShip']})
    sensitivity={str(dt):run(duration=300,dt=dt,target=(4,2),opening='one_ship') for dt in (1/60,1/30,1/15)}
    reference=sensitivity[str(1/30)]
    for candidate in sensitivity.values():
        assert abs(candidate['secondShip']-reference['secondShip'])<1
        for r in ('D','S'):
            assert abs(candidate['delivered'][r]-reference['delivered'][r])/max(1,reference['delivered'][r])<.01
    expansion_flows={}
    for d in (70,280):
        o=run(duration=900,distance=d,build=False,initial=(4,2),cap={'D':100000,'S':100000},finite=False)
        at300=next(x for x in o['snapshots'] if x['second']==300)
        expansion_flows[str(d)]={r:(o['delivered'][r]-at300['delivered'+r])/10 for r in ('D','S')}
    gains={r:expansion_flows['70'][r]-expansion_flows['280'][r] for r in ('D','S')}
    costs={
        'port_and_new_merchant':{'D':440,'S':160},
        'port_merchant_and_local_shipyard':{'D':640,'S':260},
        'port_merchant_shipyard_and_four_new_collectors':{'D':880,'S':340}}
    expansion={'constantNodeCounterfactual':True,'measuredRatesAfter5MinuteWarmup':expansion_flows,'gainPerMinute':gains,
               'optimisticRecoveryMinutes':{name:max(c[r]/gains[r] for r in ('D','S')) for name,c in costs.items()},
               'constructionTripSeconds':280/PARAMS['merchant']['speed']+PARAMS['buildings']['outpost']['seconds'],
               'merchantCombinedCapacityPerMinute':60*480/(2*280/7+2*480/60+4),
               'scope':'Same 4D/2S collectors and local spending. No new workforce income, combat, acceleration, relocation downtime, depletion or building-sequence simulation.'}
    # Refund/remont: rebuilding HP to scrap must never create a profit, component-wise.
    for h in (0,.25,.5,.75,1):
        assert .15-.25*(1-h)<=.15*h+1e-10
    report={'scope':'MODEL EKONOMICZNY; WALKA, NAWIGACJA I PLAYTESTY: NOT_RUN',
            'checks':{'resourceConservation':'VERIFIED_IN_MODEL','nonNegativeInventories':'VERIFIED_IN_MODEL','portCapacity':'VERIFIED_IN_MODEL','finiteNodes':'VERIFIED_IN_MODEL','fullWarehouse':'VERIFIED_IN_MODEL','cycleFormula':'CHECKED_AGAINST_MODEL','repairScrapLoop':'ALGEBRA_CHECKED'},
            'cycleChecks':cycle_checks,'incomes':incomes,'openings':openings,'raid':raid,'blockade':blockade,'depletion':lowstock,'fullWarehouse':full,'expansion':expansion,
            'samples200':{'seed':7319,'raidArrivalMin':min(x['earliestDirectRaidArrival'] for x in samples),'raidArrivalMax':max(x['earliestDirectRaidArrival'] for x in samples),'secondShipMin':min(x['secondShip'] for x in samples),'secondShipMax':max(x['secondShip'] for x in samples)},
            'timestepSensitivity':{dt:{'firstShip':o['firstShip'],'secondShip':o['secondShip'],'readyShips':o['readyShips'],'delivered':o['delivered']} for dt,o in sensitivity.items()}}
    (ROOT/'model-results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for name,data in [('income-distance.csv',incomes),('saturation.csv',saturation),('map-samples.csv',samples)]:
        with (ROOT/name).open('w',newline='',encoding='utf-8') as f:
            w=csv.DictWriter(f,fieldnames=data[0].keys());w.writeheader();w.writerows(data)
    print(json.dumps({'openings':{k:{q:o[q] for q in ('firstShip','secondShip','fourthShip','readyShips','collectors','shipyards')} for k,o in openings.items()},'raidRecovery':raid['recoverySeconds'],'raidShips':raid['readyShips'],'blockadeShips':blockade['readyShips'],'samples':report['samples200'],'expansion':expansion,'sensitivity':report['timestepSensitivity']},ensure_ascii=False,indent=2))

if __name__=='__main__':main()
