"""Command line: python3 Tools/asset_pipeline/ap.py <command> (Windows: Tools/asset_pipeline/ap.ps1 <command>)."""
import argparse
import json
import sys

from ..core import brief as briefs
from ..core import service
from ..core.brief import BriefError
from ..core.config import CATEGORIES, Config
from ..core.stages import LABEL, MANUAL
from ..core.worker import Worker
from ..providers import capabilities


def _print_json(data):
    print(json.dumps(data, indent=2, ensure_ascii=False))


def cmd_capabilities(args, config):
    rows = capabilities(config)
    if args.json:
        return _print_json(rows)
    for row in rows:
        env = ", ".join(f"{k}={'ustawiona' if v else 'brak'}" for k, v in row["env"].items())
        print(f"{row['service']:<30} {row['status']:<20} {', '.join(row['stages'])}")
        print(f"    weryfikacja: {row['verified']}" + (f"; zmienne: {env}" if env else ""))
        print(f"    {row['detail']}")


def cmd_template(args, config):
    _print_json(briefs.template(args.category, args.ship_class))


def cmd_create(args, config, store):
    raw, base = service.load_brief_file(args.brief)
    try:
        jobs = service.create_jobs(config, store, raw, base, args.approve_credits)
    except BriefError as error:
        print("Brief niepoprawny:", file=sys.stderr)
        for problem in error.problems:
            print(f"  - {problem}", file=sys.stderr)
        return 2
    for job in jobs:
        print(f"{job['id']}  szacunek {job['estimated_credits']} kr., zatwierdzone {job['approved_credits']} kr.")
        if job["approval_needed"]:
            print(f"    Płatne kroki zatrzymają się do zatwierdzenia: ap approve {job['id']} --credits {job['estimated_credits']}")
    if args.run:
        Worker(config, store, out=print).run(until_idle=True, max_seconds=args.max_seconds)
    return 0


def cmd_list(args, config, store):
    for job in store.jobs([args.state] if args.state else None):
        print(f"{job['id']:<48} {job['state']:<20} {job['detail'] or ''}"[:160])


def cmd_status(args, config, store):
    job = service.find_job(store, args.job)
    data = service.view(store, job, args.events)
    if args.json:
        return _print_json(data)
    print(f"{data['id']}  [{data['state']}] {data['stateLabel']}")
    print(f"  {data['detail']}")
    if data["blockedStage"]:
        print(f"  zatrzymane na etapie: {LABEL.get(data['blockedStage'], data['blockedStage'])}")
    if data["needs"]:
        print(f"  brakuje: {', '.join(data['needs'])}")
    print(f"  kredyty: szacunek {data['credits']['estimated']}, zatwierdzone {data['credits']['approved']}, użyte {data['credits']['used']}")
    for task in data["tasks"]:
        print(f"  zadanie {task['provider']}/{task['kind']}: {task['status']} {task['remote_id'] or ''} {task['progress'] or ''}")
    unity = data["artifacts"].get("unity") or {}
    if unity.get("prefab"):
        print(f"  prefab: {unity['prefab']}  raport: {unity.get('report')}  podgląd: {unity.get('preview')}")
    for event in data["events"]:
        print(f"  {event['at']} {event['state']:<19} {event['level']:<5} {event['message']}"[:220])


def cmd_approve(args, config, store):
    job = service.approve(store, service.find_job(store, args.job), args.credits)
    print(f"{job['id']}: zatwierdzono {job['approved_credits']} kr. Wznowienie: ap resume {job['id']}")


def cmd_stage(args, config, store):
    job = service.start_stage(store, service.find_job(store, args.job), args.command)
    print(f"{job['id']}: {LABEL[job['state']]}")
    if not args.no_run:
        Worker(config, store, out=print).run(job_id=job["id"], until_idle=True, max_seconds=args.max_seconds)
        print(f"Stan: {store.get(job['id'])['state']}")


def cmd_resume(args, config, store):
    job = service.resume(store, service.find_job(store, args.job), args.resubmit)
    print(f"{job['id']}: wznowiono od {LABEL.get(job['state'], job['state'])}")
    if not args.no_run:
        Worker(config, store, out=print).run(job_id=job["id"], until_idle=True, max_seconds=args.max_seconds)
        print(f"Stan: {store.get(job['id'])['state']}")


def cmd_cancel(args, config, store):
    job = service.cancel(store, service.find_job(store, args.job))
    print(f"{job['id']}: {job['state']} (anulowanie)")


def cmd_worker(args, config, store):
    steps = Worker(config, store, out=print).run(job_id=args.job, until_idle=args.until_idle, max_seconds=args.max_seconds)
    print(f"Kroki: {steps}")


def cmd_mcp(args, config):
    from ..mcp.server import serve
    serve(config)


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="ap", description="Potok assetów Morza Bogów")
    p.add_argument("--config", help="plik konfiguracji (domyślnie Tools/asset_pipeline/config/pipeline.json)")
    sub = p.add_subparsers(dest="command", required=True)
    s = sub.add_parser("capabilities", help="macierz możliwości: usługi, etapy, konfiguracja")
    s.add_argument("--json", action="store_true")
    s = sub.add_parser("template", help="szablon briefu")
    s.add_argument("category", choices=CATEGORIES)
    s.add_argument("--ship-class")
    s = sub.add_parser("create", help="nowe zadanie z pliku briefu")
    s.add_argument("brief")
    s.add_argument("--approve-credits", type=int, default=0, help="budżet kredytów zatwierdzony przez człowieka")
    s.add_argument("--run", action="store_true", help="od razu uruchom workera")
    s.add_argument("--max-seconds", type=int, default=3600)
    s = sub.add_parser("list", help="lista zadań")
    s.add_argument("--state")
    s = sub.add_parser("status", help="stan zadania")
    s.add_argument("job")
    s.add_argument("--json", action="store_true")
    s.add_argument("--events", type=int, default=12)
    s = sub.add_parser("approve", help="zatwierdź budżet kredytów zadania (decyzja człowieka)")
    s.add_argument("job")
    s.add_argument("--credits", type=int, required=True)
    for command in MANUAL:
        s = sub.add_parser(command, help=f"uruchom etap: {LABEL[MANUAL[command]]}")
        s.add_argument("job")
        s.add_argument("--no-run", action="store_true")
        s.add_argument("--max-seconds", type=int, default=3600)
    s = sub.add_parser("resume", help="wznów zatrzymane zadanie od jego etapu")
    s.add_argument("job")
    s.add_argument("--resubmit", action="store_true", help="porzuć nieznane lub nieudane zadania zdalne i wyślij nowe (koszt)")
    s.add_argument("--no-run", action="store_true")
    s.add_argument("--max-seconds", type=int, default=3600)
    s = sub.add_parser("cancel", help="anuluj zadanie (zadania zdalne zostają u dostawcy)")
    s.add_argument("job")
    s = sub.add_parser("worker", help="przetwarzaj kolejkę")
    s.add_argument("--job")
    s.add_argument("--until-idle", action="store_true", help="czekaj na trwające zadania zdalne, aż kolejka się zatrzyma")
    s.add_argument("--max-seconds", type=int)
    sub.add_parser("mcp", help="serwer MCP (stdio)")
    return p


def main(argv=None) -> int:
    args = parser().parse_args(argv)
    config = Config.load(args.config)
    if args.command == "capabilities":
        return cmd_capabilities(args, config) or 0
    if args.command == "template":
        return cmd_template(args, config) or 0
    if args.command == "mcp":
        return cmd_mcp(args, config) or 0
    store = service.open_store(config)
    try:
        handler = {"create": cmd_create, "list": cmd_list, "status": cmd_status, "approve": cmd_approve, "resume": cmd_resume,
                   "cancel": cmd_cancel, "worker": cmd_worker}.get(args.command)
        if handler is None and args.command in MANUAL:
            handler = cmd_stage
        return handler(args, config, store) or 0
    except (KeyError, ValueError) as error:
        print(str(error).strip("'\""), file=sys.stderr)
        return 2
    finally:
        store.close()
