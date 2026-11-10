using System.Collections.Generic;
using MorzaBogow.Simulation;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>One man aboard and what he is doing now.</summary>
    public sealed class CrewMan
    {
        public int Man;
        public CrewTrade Trade;
        public CrewRole Role;
        public int Station = -1, Slot = -1;
        public CrewLook Look;
        public CrewAction Action;
        public float Phase, Strength;
        public Vector3 Aim;
        public float JoltUntil, JoltStrength;
        /// <summary>Walking between stations, in the ship's model space.</summary>
        public Vector3 From, To;
        public float FromFacing, ToFacing, WalkStart, WalkTime;
        /// <summary>Struck down: his place and facing where he fell; becomes a wounded man or a body.</summary>
        public bool Fallen, Wounding;
        public float FellAt;
        public Vector3 FellPosition;
        public float FellFacing;
        /// <summary>A boarding: he stands at the rail station facing the other deck.</summary>
        public int RailStation = -1;
        public List<(int Bone, Mesh Mesh, float4x4 Local)> Parts;
        public int PartsLod = -1;
    }

    /// <summary>
    /// The crew of one ship as drawn: who is aboard (CrewRoster), where each works (CrewManning, CrewLayoutProfile) and
    /// what each does from the ship's state: the stroke of the oars, the helm, readiness for a fight, the reload and
    /// loosing of each post, a ram's charge and blow, shots striking, men falling, a boarding at the rail, surrender.
    /// </summary>
    public sealed class CrewAboard
    {
        public readonly int ShipId;
        public readonly ShipClass Class;
        public readonly ShipVisual View;
        public int Owner { get; private set; }
        public readonly CrewLayoutProfile Profile;
        public readonly CrewRoster Roster = new CrewRoster();
        public readonly List<CrewMan> Men = new List<CrewMan>();
        readonly Dictionary<int, CrewMan> byMan = new Dictionary<int, CrewMan>();
        readonly List<CrewMan> bodies = new List<CrewMan>();
        public int Version { get; private set; }
        public int DrawnFigures { get; private set; }
        public bool Ready { get; private set; }

        WeaponType[] mounts = System.Array.Empty<WeaponType>();
        bool[] manned = System.Array.Empty<bool>();
        float[] firedAt = System.Array.Empty<float>();
        Vector3[] firedToward = System.Array.Empty<Vector3>();
        bool[] hasTarget = System.Array.Empty<bool>();
        Vector3[] targetAt = System.Array.Empty<Vector3>();
        int marksTick = -1;
        float[] postYaw = System.Array.Empty<float>();
        Transform[] mountTransforms = System.Array.Empty<Transform>();
        float surrenderUntil = -1f;
        int readinessTick = -1000, checkedTick = int.MinValue;
        int lastRamTick;
        bool reman = true;
        int boardingSide;
        int boardingFront;
        Vector3 boardingToward;
        float lastYaw;
        float turnRate;

        public CrewAboard(Ship ship, ShipVisual view, World world)
        {
            ShipId = ship.Id;
            Class = ship.Class;
            View = view;
            Owner = ship.Owner;
            Profile = CrewLayoutProfile.For(ship.Class);
            Roster.Reset(ship);
            lastRamTick = ship.LastRamTick;
            var slots = ship.Mounts.Length;
            firedAt = new float[slots];
            for (var i = 0; i < slots; i++) firedAt[i] = -100f;
            firedToward = new Vector3[slots];
            hasTarget = new bool[slots];
            targetAt = new Vector3[slots];
            postYaw = new float[slots];
            mountTransforms = new Transform[slots];
            for (var i = 0; i < slots; i++) mountTransforms[i] = view.Model.Find($"Mount {i}");
        }

        public void Dirty()
        {
            foreach (var man in Men) man.PartsLod = -1;
            foreach (var body in bodies) body.PartsLod = -1;
            Version++;
        }

        // From the simulation's ticks ----------------------------------------------------------------------------------
        public void Fired(int slot, float clock, Vector3 toward, int tick)
        {
            if (slot < 0 || slot >= firedAt.Length) return;
            firedAt[slot] = clock;
            firedToward[slot] = toward;
        }

        /// <summary>A blow (a ram, a shot): men near it, or everyone, are thrown and catch themselves.</summary>
        public void Jolt(float clock, float strength, Vector3? at)
        {
            foreach (var man in Men)
            {
                if (man.Station < 0) continue;
                if (at.HasValue && Vector3.Distance(WorldPosition(man), at.Value) > 3.5f) continue;
                man.JoltUntil = clock + 0.8f;
                man.JoltStrength = strength;
            }
        }

        public void Surrender(float clock) => surrenderUntil = clock + 6f;

        float abandonedAt = -1f;

        /// <summary>
        /// The ship has gone down: each man goes over the side in turn within 0.6 s, before the hull breaks or slips
        /// under (BattleEffects.ShipFate); false once all are gone.
        /// </summary>
        public bool Abandon(float clock)
        {
            if (abandonedAt < 0f)
            {
                abandonedAt = clock;
                bodies.Clear();
                for (var i = 0; i < Men.Count; i++)
                {
                    var man = Men[i];
                    if (man.Station < 0) continue;
                    man.FellPosition = ModelPosition(man);
                    man.FellFacing = Facing(man);
                    man.FellAt = clock + (i % 6) * 0.1f;
                    man.Wounding = false;
                }
            }
            var t = clock - abandonedAt;
            foreach (var man in Men)
            {
                man.Action = CrewAction.Overboard;
                man.Phase = Mathf.Clamp01((clock - man.FellAt) / 0.6f);
            }
            return t < 1.2f;
        }

        /// <summary>
        /// The roster to the simulation's numbers. Men lost in a tick of a blow or a boarding round fall where they stood
        /// (the nearest to the blow first, or those at the rail); two of every five fallen are the wounded the game counts,
        /// who go to the wounded's place; any other change (recruits, the healed, a prize crew) only comes or goes.
        /// </summary>
        public void Count(Ship ship, float clock, bool fighting, Vector3? at)
        {
            if (ship.Owner != Owner)
            {
                // Taken: the prize crew aboard is another crew, in the new owner's colours, without false falls.
                Owner = ship.Owner;
                Roster.Reset(ship);
                foreach (var man in Men) man.PartsLod = -1;
                Men.Clear();
                byMan.Clear();
                reman = true;
                Version++;
                return;
            }
            var gone = new List<int>();
            var woundedBefore = Roster.Count(CrewTrade.Wounded);
            var changed = Roster.Match(ship, fighting ? (trade, list) => Nearest(list, at) : (System.Func<CrewTrade, IReadOnlyList<int>, int>)null, gone);
            if (!changed) return;
            var newlyWounded = Mathf.Max(0, Roster.Count(CrewTrade.Wounded) - woundedBefore);
            foreach (var id in gone)
            {
                if (!byMan.TryGetValue(id, out var man)) continue;
                Men.Remove(man);
                byMan.Remove(id);
                if (!fighting || man.Station < 0 || man.Trade == CrewTrade.Wounded) continue;
                // Where he stood, before he becomes one of the fallen.
                man.FellPosition = ModelPosition(man);
                man.FellFacing = Facing(man);
                man.Fallen = true;
                man.Wounding = newlyWounded-- > 0;
                man.FellAt = clock;
                bodies.Add(man);
                while (bodies.Count > MaxBodies) bodies.RemoveAt(0);
            }
            reman = true;
            Version++;
        }

        const int MaxBodies = 4;

        int Nearest(IReadOnlyList<int> men, Vector3? at)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            foreach (var id in men)
            {
                if (!byMan.TryGetValue(id, out var man) || man.Station < 0) continue;
                var distance = at.HasValue ? Vector3.Distance(WorldPosition(man), at.Value) : man.RailStation >= 0 ? 0f : 100f;
                if (distance < bestDistance) { bestDistance = distance; best = id; }
            }
            return best;
        }

        // Each frame ---------------------------------------------------------------------------------------------------
        /// <param name="shown">Drawn this frame: only then each man decides his doing and the posts turn to their marks.</param>
        public void Update(Ship ship, ShipVisual view, World world, float clock, float delta, bool shown = true)
        {
            // Once a tick: a weapon changed in port, or a post lost or gained its men, mans the posts anew; readiness every
            // half second, each ship on its own beat.
            if (world.Tick != checkedTick)
            {
                checkedTick = world.Tick;
                // World.Manned reads only the weapons and the three fighting trades: asked again only when one changes.
                if (ship.Archers != seenArchers || ship.Javelins != seenJavelins || ship.Swordsmen != seenSwordsmen || !SameWeapons(ship.Mounts))
                {
                    seenArchers = ship.Archers;
                    seenJavelins = ship.Javelins;
                    seenSwordsmen = ship.Swordsmen;
                    var nowManned = World.Manned(ship);
                    if (!SameMounts(ship.Mounts, nowManned))
                    {
                        mounts = (WeaponType[])ship.Mounts.Clone();
                        manned = nowManned;
                        reman = true;
                    }
                }
                if (world.Tick >= readinessTick || world.Tick < readinessTick - 30)
                {
                    readinessTick = world.Tick + 15;
                    var ready = Readiness(ship, world);
                    if (ready != Ready) { Ready = ready; reman = true; }
                }
            }
            UpdateBoarding(ship, view, world);
            if (reman) Reman(ship, clock);

            // The oars: one stroke whose pace follows the ship's way, a sprint in a ram's charge; a bench without its man draws its oar in.
            var definition = ship.Definition;
            var speed = (float)(ship.Speed / definition.Speed);
            var yaw = view.transform.eulerAngles.y;
            if (delta > 0f) turnRate = Mathf.Lerp(turnRate, Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / delta, Mathf.Clamp01(delta * 4f));
            lastYaw = yaw;
            var charging = ship.Order == OrderKind.Ram && (ship.RamState == RamPhase.Approach || ship.RamState == RamPhase.Telegraph);
            view.SetStroke(RowingStroke.RateFor(speed, turnRate > 9f, charging));
            // Men gone ashore with the shore party (BattleController's crews at work) are not drawn aboard twice: man i of
            // the party is the i-th sailor at the oars, the sail, the lines or the bow (CrewAshore draws him ashore with his
            // look), gone from the deck while that man of the party is ashore. Untold (-1), the job's whole party is away.
            ashore.Clear();
            ashoreMen.Clear();
            var away = PartyAshore < 0 ? Ashore(ship) : MostAshore;
            foreach (var man in Men)
            {
                if (ashoreMen.Count >= away) break;
                if (man.Role != CrewRole.Rower && man.Role != CrewRole.LineHand && man.Role != CrewRole.SailHand && man.Role != CrewRole.BowHand) continue;
                if (PartyAshore < 0 || (PartyAshore & 1 << ashoreMen.Count) != 0) ashore.Add(man.Man);
                ashoreMen.Add(man.Man);
            }
            foreach (var bench in Profile.Of(StationKind.Bench)) view.SetOarCrewed(bench.Oar, false);
            foreach (var man in Men)
                if ((man.Role == CrewRole.Rower || man.Role == CrewRole.WarriorRower) && man.Station >= 0 && !Walking(man, clock) && !ashore.Contains(man.Man))
                    view.SetOarCrewed(Profile.Stations[man.Station].Oar, true);

            if (ship.LastRamTick != lastRamTick) lastRamTick = ship.LastRamTick;
            if (shown)
            {
                AimPosts(ship, view, world, delta);
                foreach (var man in Men) Decide(man, ship, view, clock);
            }
            for (var i = bodies.Count - 1; i >= 0; i--)
                if (clock - bodies[i].FellAt > (bodies[i].Wounding ? 1.2f : 6f)) bodies.RemoveAt(i);
        }

        readonly HashSet<int> ashore = new HashSet<int>();
        readonly List<int> ashoreMen = new List<int>();

        /// <summary>The largest shore party, a merchantman's (BattleController: six from a merchantman, else four).</summary>
        const int MostAshore = 6;

        /// <summary>
        /// The ship's own men a shore party is made of, in the party's order: man i of the party is AshoreMen[i], the
        /// i-th sailor at the oars, the sail, the lines or the bow (CrewAshore draws him ashore with his look from here).
        /// </summary>
        public IReadOnlyList<int> AshoreMen => ashoreMen;

        /// <summary>
        /// Which men of the ship's shore party are ashore now (bit i for man i of the party, ShipCrew.AshoreMask), told by
        /// CrewDirector each frame before Update; each of them is drawn ashore, not aboard. -1 (never told): the whole
        /// party counts as ashore for as long as the job lasts (Ashore).
        /// </summary>
        public int PartyAshore { get; set; } = -1;

        /// <summary>
        /// Men ashore at work, as the battle draws its shore parties (BattleController.CrewJobOf): four from a boat at a
        /// work point or a ship at a quay, six from a merchantman, from landing until they are back aboard.
        /// </summary>
        public static int Ashore(Ship ship)
        {
            var size = ship.Class == ShipClass.Merchant ? 6 : 4;
            if (ship.Order == OrderKind.Harvest && (ship.Harvest == HarvestPhase.Working || ship.Harvest == HarvestPhase.Landing || ship.Harvest == HarvestPhase.Embarking)) return size;
            if ((ship.Order == OrderKind.Transfer || ship.Order == OrderKind.Expedition) && (ship.Transport == TransportPhase.Loading || ship.Transport == TransportPhase.Unloading)) return size;
            return 0;
        }

        int seenArchers = -1, seenJavelins = -1, seenSwordsmen = -1;

        bool SameWeapons(WeaponType[] current)
        {
            if (current.Length != mounts.Length) return false;
            for (var i = 0; i < current.Length; i++) if (current[i] != mounts[i]) return false;
            return true;
        }

        bool SameMounts(WeaponType[] current, bool[] nowManned)
        {
            if (current.Length != mounts.Length || nowManned.Length != manned.Length) return false;
            for (var i = 0; i < current.Length; i++) if (current[i] != mounts[i] || nowManned[i] != manned[i]) return false;
            return true;
        }

        /// <summary>
        /// Ready for a fight: ordered to attack, ram or board, grappled, or an enemy in sight within the reach of its
        /// weapons (at least 120 m) and 40 m more. Merchants and boats are never ready: they have nothing to fight with.
        /// </summary>
        bool Readiness(Ship ship, World world)
        {
            if (ship.Definition.IsEconomic) return false;
            if (ship.Order == OrderKind.AttackShip || ship.Order == OrderKind.AttackSettlement || ship.Order == OrderKind.Ram || ship.Order == OrderKind.Board) return true;
            var reach = 120.0;
            for (var slot = 0; slot < ship.Mounts.Length; slot++)
            {
                hasTarget[slot] = false;
                if (ship.Mounts[slot] == WeaponType.None) continue;
                reach = System.Math.Max(reach, WeaponCatalog.Get(ship.Mounts[slot]).MaxRange);
            }
            var near = false;
            foreach (var other in world.Ships)
            {
                if (other.Owner == ship.Owner) continue;
                var dx = other.X - ship.X;
                var dy = other.Y - ship.Y;
                // The distance first: the fog's test runs over every ship and port.
                if (dx * dx + dy * dy < (reach + 40) * (reach + 40) && world.CanSee(ship.Owner, other)) { near = true; break; }
            }
            foreach (var link in world.Boardings) if (link.AttackerId == ship.Id || link.TargetId == ship.Id) near = true;
            return near;
        }

        /// <summary>
        /// Each machine turns toward its mark within its arc, as fast as men can heave it round; a bank's men turn
        /// themselves. The mark: the ship it is ordered to shoot, else the nearest enemy it may shoot at.
        /// </summary>
        void AimPosts(Ship ship, ShipVisual view, World world, float delta)
        {
            // The marks move with the game's positions, so they are sought once a tick; the machines turn every frame.
            var seek = world.Tick != marksTick;
            marksTick = world.Tick;
            for (var slot = 0; slot < ship.Mounts.Length && slot < hasTarget.Length; slot++)
            {
                if (seek) hasTarget[slot] = false;
                if (ship.Mounts[slot] == WeaponType.None) continue;
                var weapon = WeaponCatalog.Get(ship.Mounts[slot]);
                if (seek)
                {
                    Ship best = null;
                    var bestDistance = double.MaxValue;
                    if (ship.Order == OrderKind.AttackShip)
                    {
                        var ordered = world.FindShip(ship.TargetId);
                        if (ordered != null && World.InReach(ship, weapon, ordered.X, ordered.Y)) best = ordered;
                    }
                    if (best == null && ship.Order != OrderKind.Ram)
                        foreach (var other in world.Ships)
                        {
                            // Reach and arc first: the fog's test runs over every ship and port, so only for a mark in reach.
                            if (other.Owner == ship.Owner || !World.InReach(ship, weapon, other.X, other.Y) || !world.CanSee(ship.Owner, other)) continue;
                            var dx = other.X - ship.X;
                            var dy = other.Y - ship.Y;
                            var d = dx * dx + dy * dy;
                            if (d < bestDistance) { bestDistance = d; best = other; }
                        }
                    if (best != null)
                    {
                        hasTarget[slot] = true;
                        targetAt[slot] = new Vector3((float)best.X, 1.5f, (float)best.Y);
                    }
                }
                if (!weapon.IsMachine || mountTransforms[slot] == null) continue;
                var aim = hasTarget[slot] ? targetAt[slot] : view.transform.position + view.transform.forward * 50f;
                var local = view.Model.InverseTransformDirection(aim - view.Model.TransformPoint(Profile.Parts.Mounts[slot]));
                var wanted = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -(float)weapon.ArcDegrees * 0.5f, (float)weapon.ArcDegrees * 0.5f);
                postYaw[slot] = Mathf.MoveTowardsAngle(postYaw[slot], wanted, delta * 40f);
                mountTransforms[slot].localRotation = Quaternion.Euler(0f, postYaw[slot], 0f);
            }
        }

        /// <summary>A grappled boarding: which side faces the other deck and how many men fight at its rail.</summary>
        void UpdateBoarding(Ship ship, ShipVisual view, World world)
        {
            var side = 0;
            var front = 0;
            foreach (var link in world.Boardings)
            {
                if (!link.Hooked || link.AttackerId != ship.Id && link.TargetId != ship.Id) continue;
                var other = world.FindShip(link.AttackerId == ship.Id ? link.TargetId : link.AttackerId);
                if (other == null) continue;
                boardingToward = new Vector3((float)other.X, 1.5f, (float)other.Y);
                var local = view.transform.InverseTransformPoint(boardingToward);
                side = local.x >= 0f ? 1 : -1;
                var attacking = link.AttackerId == ship.Id;
                front = CrewDirector.Front(attacking ? link.AttackerFront : link.TargetFront, ship, World.BoardingFront(attacking ? ship : other, attacking ? other : ship));
            }
            if (side != boardingSide || front != boardingFront)
            {
                boardingSide = side;
                boardingFront = front;
                reman = true;
            }
        }

        /// <summary>Everyone to his station by the simulation's numbers; a man whose place changes walks there from where he is.</summary>
        void Reman(Ship ship, float clock)
        {
            reman = false;
            wasAt.Clear();
            foreach (var man in Men)
                if (StationOf(man) != null && !man.Fallen) wasAt[man.Man] = (Placed(man), Now(man, clock), Walking(man, clock) ? man.ToFacing : Facing(man));
            var places = CrewManning.Assign(Profile, Roster, ship.Mounts, World.Manned(ship), Ready);
            var seen = new HashSet<int>();
            foreach (var place in places)
            {
                seen.Add(place.Man);
                if (!byMan.TryGetValue(place.Man, out var man))
                {
                    man = new CrewMan { Man = place.Man, Trade = place.Trade };
                    byMan[place.Man] = man;
                    Men.Add(man);
                    man.Role = place.Role;
                    man.Station = place.Station;
                    man.Slot = place.Slot;
                    man.Look = CrewLooks.For(ShipId, place.Man, place.Role, Owner);
                    continue;
                }
                if (man.Role != place.Role) { man.Look = CrewLooks.For(ShipId, place.Man, place.Role, Owner); man.PartsLod = -1; }
                man.Role = place.Role;
                man.Station = place.Station;
                man.Slot = place.Slot;
            }
            for (var i = Men.Count - 1; i >= 0; i--)
                if (!seen.Contains(Men[i].Man)) { byMan.Remove(Men[i].Man); Men.RemoveAt(i); }
            // At a grappled rail the front stands and fights, as many as the front holds, at the places nearest the other ship:
            // a warrior already standing ready at one turns where he stands; a place without its man takes the deck's other
            // warriors, then (grappled, the posts cannot shoot, so their men fight too: World.DeckStrength) the posts' men.
            foreach (var man in Men) man.RailStation = -1;
            if (boardingSide != 0 && boardingFront > 0)
            {
                var rails = new List<int>();
                for (var i = 0; i < Profile.Stations.Count; i++)
                    if (Profile.Stations[i].Kind == StationKind.Rail && Profile.Stations[i].Side == boardingSide) rails.Add(i);
                var towardZ = View.Model.InverseTransformPoint(boardingToward).z;
                rails.Sort((a, b) => Mathf.Abs(Profile.Stations[a].Position.z - towardZ).CompareTo(Mathf.Abs(Profile.Stations[b].Position.z - towardZ)));
                if (rails.Count > boardingFront) rails.RemoveRange(boardingFront, rails.Count - boardingFront);
                var open = new List<int>();
                foreach (var rail in rails)
                {
                    CrewMan here = null;
                    foreach (var man in Men)
                        if (man.Station >= 0 && man.RailStation < 0 && Deck(man) && (Profile.Stations[man.Station].Position - Profile.Stations[rail].Position).sqrMagnitude < 1e-4f) { here = man; break; }
                    if (here != null) here.RailStation = rail;
                    else open.Add(rail);
                }
                var next = 0;
                foreach (var pass in new[] { 0, 1 })
                    foreach (var man in Men)
                    {
                        if (next >= open.Count) break;
                        if (man.RailStation >= 0 || man.Station < 0 || (pass == 0 ? !Deck(man) : !Post(man))) continue;
                        man.RailStation = open[next++];
                    }
            }
            // Whoever's place changed, to or from the rail or to another station, walks there from where he stood.
            foreach (var man in Men)
            {
                if (!wasAt.TryGetValue(man.Man, out var was)) continue;
                var place = Placed(man);
                if (place >= 0 && was.place >= 0 && place != was.place) Walk(man, was.at, was.facing, place, clock);
            }
            Version++;
        }

        readonly Dictionary<int, (int place, Vector3 at, float facing)> wasAt = new Dictionary<int, (int, Vector3, float)>();

        static bool Deck(CrewMan man) => man.Role == CrewRole.Marine || man.Role == CrewRole.Commander;

        static bool Post(CrewMan man) => man.Role == CrewRole.BankArcher || man.Role == CrewRole.BankJavelin || man.Role == CrewRole.MachineCrew;

        /// <summary>The station he stands at now: a boarding's rail before his own; -1 without one.</summary>
        static int Placed(CrewMan man) => man.RailStation >= 0 ? man.RailStation : man.Station;

        /// <summary>Where he is now (model space): on his way between two places, or at his own.</summary>
        Vector3 Now(CrewMan man, float clock) =>
            Walking(man, clock) ? Vector3.Lerp(man.From, man.To, Mathf.Clamp01((clock - man.WalkStart) / man.WalkTime)) : ModelPosition(man);

        void Walk(CrewMan man, Vector3 from, float fromFacing, int station, float clock)
        {
            man.From = from;
            man.FromFacing = fromFacing;
            var target = Profile.Stations[station];
            man.To = target.Slot >= 0 ? Profile.PostPoint(target, PostYaw(target.Slot)) : target.Position;
            man.ToFacing = target.Facing;
            man.WalkStart = clock;
            man.WalkTime = Mathf.Clamp(Vector3.Distance(from, man.To) / 1.3f, 0f, 6f);
        }

        static bool Walking(CrewMan man, float clock) => man.WalkTime > 0f && clock - man.WalkStart < man.WalkTime;

        float PostYaw(int slot) => slot >= 0 && slot < postYaw.Length ? postYaw[slot] : 0f;

        /// <summary>The station he works at now (a boarding's rail before his own).</summary>
        CrewStation StationOf(CrewMan man) => man.RailStation >= 0 ? Profile.Stations[man.RailStation] : man.Station >= 0 ? Profile.Stations[man.Station] : null;

        Vector3 ModelPosition(CrewMan man)
        {
            if (man.Fallen) return man.FellPosition;
            var station = StationOf(man);
            if (station == null) return Vector3.zero;
            return station.Slot >= 0 ? Profile.PostPoint(station, PostYaw(station.Slot)) : station.Position;
        }

        float Facing(CrewMan man)
        {
            if (man.Fallen) return man.FellFacing;
            var station = StationOf(man);
            return station == null ? 0f : station.Facing + (station.Slot >= 0 ? PostYaw(station.Slot) : 0f);
        }

        public Vector3 WorldPosition(CrewMan man) => View.Model.TransformPoint(ModelPosition(man));

        /// <summary>
        /// What a man does now, from his role and his ship's state. Rowers pull with the oars' own stroke whatever else
        /// happens aboard; the helmsman works the tillers; a post's men load, draw or heave by the post's reload and loose on
        /// the tick the game fires; warriors sit at sea and stand ready, brace for a ram's blow, fight at a grappled rail.
        /// </summary>
        void Decide(CrewMan man, Ship ship, ShipVisual view, float clock)
        {
            man.Strength = 1f;
            man.Aim = Vector3.zero;
            var station = StationOf(man);
            if (station == null) return;
            if (Walking(man, clock)) { man.Action = CrewAction.Walk; man.Phase = Mathf.Repeat(clock * 1.7f + man.Man * 0.37f, 1f); return; }
            var fighter = man.Trade != CrewTrade.Sailor && man.Trade != CrewTrade.Wounded;
            if (clock < surrenderUntil && fighter) { man.Action = CrewAction.Kneel; return; }
            var rower = man.Role == CrewRole.Rower || man.Role == CrewRole.WarriorRower;
            if (clock < man.JoltUntil && !rower && man.Role != CrewRole.Helmsman)
            {
                man.Action = CrewAction.Jolt;
                man.Phase = 1f - (man.JoltUntil - clock) / 0.8f;
                man.Strength = man.JoltStrength;
                return;
            }
            var telegraph = ship.Order == OrderKind.Ram && ship.RamState == RamPhase.Telegraph;
            switch (man.Role)
            {
                case CrewRole.Rower:
                case CrewRole.WarriorRower:
                    var stowed = view.OarStowed(station.Oar) > 0.5f;
                    man.Action = stowed ? CrewAction.SitIdle : Profile.Parts.RowersStand ? CrewAction.RowStanding : CrewAction.Row;
                    man.Phase = stowed ? Mathf.Repeat(clock * 0.12f + man.Man * 0.29f, 1f) : view.StrokePhase;
                    man.Strength = Mathf.Clamp01(Mathf.Abs((float)(ship.Speed / ship.Definition.Speed)) + (telegraph ? 0.3f : 0f));
                    break;
                case CrewRole.Helmsman:
                    man.Action = Profile.Parts.HelmSeated ? CrewAction.SteerSeated : CrewAction.Steer;
                    man.Strength = view.SteeringAngle / 22f;
                    break;
                case CrewRole.BankArcher:
                    Archer(man, station.Slot, ship, view, clock);
                    break;
                case CrewRole.BankJavelin:
                    Javelin(man, station.Slot, ship, view, clock);
                    break;
                case CrewRole.MachineCrew:
                    Machine(man, station, ship, clock);
                    break;
                case CrewRole.SailHand:
                    man.Action = view.SailChanging ? CrewAction.Haul : telegraph ? CrewAction.Brace : CrewAction.StandIdle;
                    man.Phase = view.SailChanging ? Mathf.Repeat(clock * 0.9f + man.Man * 0.5f, 1f) : Mathf.Repeat(clock * 0.15f + man.Man * 0.31f, 1f);
                    man.Strength = 1f;
                    break;
                case CrewRole.LineHand:
                    var sinking = view.HullFraction < 0.35f;
                    man.Action = sinking ? CrewAction.Bail : telegraph ? CrewAction.Brace : CrewAction.StandIdle;
                    man.Phase = Mathf.Repeat(clock * (sinking ? 0.8f : 0.15f) + man.Man * 0.31f, 1f);
                    break;
                case CrewRole.Wounded:
                    man.Action = man.Man % 3 == 0 ? CrewAction.LieWounded : CrewAction.SitWounded;
                    man.Phase = Mathf.Repeat(clock * 0.1f + man.Man * 0.4f, 1f);
                    break;
                case CrewRole.Lookout:
                case CrewRole.BowHand:
                    man.Action = telegraph ? CrewAction.Brace : CrewAction.Watch;
                    man.Aim = Threat(ship, view, station);
                    break;
                default:
                    // Warriors and their leader: seated at sea, standing ready, braced for a blow, at the rail in a boarding.
                    if (man.RailStation >= 0)
                    {
                        man.Action = (man.Man + Mathf.FloorToInt(clock * 0.9f)) % 2 == 0 ? CrewAction.Fight : CrewAction.Guard;
                        man.Phase = Mathf.Repeat(clock * 0.9f + man.Man * 0.23f, 1f);
                        man.Aim = Toward(station, boardingToward, view);
                    }
                    else if (telegraph) { man.Action = CrewAction.Brace; man.Strength = 1f; }
                    else if (station.Posture == CrewPosture.Sit) { man.Action = CrewAction.SitIdle; man.Phase = Mathf.Repeat(clock * 0.12f + man.Man * 0.29f, 1f); }
                    else
                    {
                        man.Action = Ready ? CrewAction.Watch : CrewAction.StandIdle;
                        man.Phase = Mathf.Repeat(clock * 0.15f + man.Man * 0.31f, 1f);
                        man.Aim = Ready ? Threat(ship, view, station) : Vector3.zero;
                    }
                    if (view.HullFraction < 0.35f && !Ready && station.Posture == CrewPosture.Sit) { man.Action = CrewAction.Bail; man.Phase = Mathf.Repeat(clock * 0.8f + man.Man * 0.3f, 1f); }
                    break;
            }
        }

        /// <summary>
        /// A bank's archer: an arrow to the string while the bank reloads; drawn and aimed from 0.6 s before the game will
        /// loose (by the reload's progress and the crew's tempo) while it has a mark in reach; loosed on the tick it fires.
        /// </summary>
        void Archer(CrewMan man, int slot, Ship ship, ShipVisual view, float clock)
        {
            var station = StationOf(man);
            var since = clock - firedAt[slot];
            var remaining = Remaining(ship, slot);
            if (since >= 0f && since < 0.45f) { man.Action = CrewAction.BowLoose; man.Phase = since / 0.45f; man.Aim = Toward(station, firedToward[slot], view); return; }
            var locked = Locked(ship);
            if (hasTarget[slot] && !locked && ship.Order != OrderKind.Ram && remaining < 0.6f)
            {
                man.Action = CrewAction.BowDraw;
                man.Phase = 1f - remaining / 0.6f;
                man.Aim = Toward(station, targetAt[slot], view);
                return;
            }
            man.Action = ship.Order == OrderKind.Ram && ship.RamState == RamPhase.Telegraph ? CrewAction.Brace : CrewAction.BowReady;
            man.Aim = hasTarget[slot] ? Toward(station, targetAt[slot], view) : Vector3.zero;
        }

        void Javelin(CrewMan man, int slot, Ship ship, ShipVisual view, float clock)
        {
            var station = StationOf(man);
            var since = clock - firedAt[slot];
            var remaining = Remaining(ship, slot);
            if (since >= 0f && since < 0.45f) { man.Action = CrewAction.JavelinThrow; man.Phase = 0.6f + 0.4f * since / 0.45f; man.Aim = Toward(station, firedToward[slot], view); return; }
            if (hasTarget[slot] && !Locked(ship) && ship.Order != OrderKind.Ram && remaining < 0.5f)
            {
                man.Action = CrewAction.JavelinThrow;
                man.Phase = 0.6f * (1f - remaining / 0.5f);
                man.Aim = Toward(station, targetAt[slot], view);
                return;
            }
            man.Action = CrewAction.JavelinReady;
            man.Aim = hasTarget[slot] ? Toward(station, targetAt[slot], view) : Vector3.zero;
        }

        /// <summary>A machine's crew: the aimer sights, the winch men heave through the reload, the loader sets the shot, the bearers carry.</summary>
        void Machine(CrewMan man, CrewStation station, Ship ship, float clock)
        {
            var slot = station.Slot;
            var progress = slot < ship.Progress.Length ? (float)ship.Progress[slot] : 1f;
            var broken = slot < ship.ModuleHp.Length && ship.ModuleHp[slot] <= 0;
            var place = station.Order;
            if (broken) { man.Action = CrewAction.Load; man.Phase = Mathf.Repeat(clock * 0.5f + place * 0.3f, 1f); return; }
            switch (place)
            {
                case 0:
                    man.Action = hasTarget[slot] ? CrewAction.Aim : CrewAction.StandIdle;
                    man.Phase = Mathf.Repeat(clock * 0.15f, 1f);
                    break;
                case 1:
                    man.Action = progress > 0.55f && progress < 0.95f ? CrewAction.Load : CrewAction.StandIdle;
                    man.Phase = Mathf.Clamp01((progress - 0.55f) / 0.4f);
                    break;
                default:
                    // The last places of a post (Medium 5–6, Heavy 7–8) bring the bolts or stones.
                    var bearersFrom = ShipCatalog.Get(Class).Slots[slot] == SlotSize.Heavy ? 6 : 4;
                    if (place >= bearersFrom)
                    {
                        man.Action = CrewAction.Carry;
                        break;
                    }
                    man.Action = progress < 0.6f ? CrewAction.Crank : CrewAction.StandIdle;
                    man.Phase = Mathf.Repeat(progress * 6f + place * 0.25f, 1f);
                    break;
            }
        }

        bool Locked(Ship ship) => boardingSide != 0;

        /// <summary>Seconds of game time before the post's reload is full, at the crew's tempo (World.StepWeapons).</summary>
        float Remaining(Ship ship, int slot)
        {
            if (slot >= ship.Progress.Length || ship.Mounts[slot] == WeaponType.None) return float.MaxValue;
            var weapon = WeaponCatalog.Get(ship.Mounts[slot]);
            var tempo = 0.75 + 0.25 * ship.Morale / 100.0;
            return (float)((1.0 - ship.Progress[slot]) * weapon.Cycle / tempo);
        }

        /// <summary>A direction toward a point in the world, in a station's frame on this ship.</summary>
        Vector3 Toward(CrewStation station, Vector3 point, ShipVisual view)
        {
            var frame = Frame(station);
            var local = Quaternion.Inverse(frame.rotation) * (view.Model.InverseTransformPoint(point) - frame.position);
            local.y = Mathf.Clamp(local.y, -0.3f * local.magnitude, 0.3f * local.magnitude);
            return local.sqrMagnitude > 1e-4f ? local.normalized : Vector3.zero;
        }

        Vector3 Threat(Ship ship, ShipVisual view, CrewStation station)
        {
            for (var slot = 0; slot < hasTarget.Length; slot++) if (hasTarget[slot]) return Toward(station, targetAt[slot], view);
            return Vector3.zero;
        }

        (Vector3 position, Quaternion rotation) Frame(CrewStation station)
        {
            if (station.Slot >= 0) return (Profile.PostPoint(station, PostYaw(station.Slot)), Quaternion.Euler(0f, station.Facing + PostYaw(station.Slot), 0f));
            return (station.Position, Quaternion.Euler(0f, station.Facing, 0f));
        }

        // Drawing ----------------------------------------------------------------------------------------------------
        /// <summary>Every man with a place, and the fallen, to pose and draw this frame.</summary>
        /// <param name="modelSlot">His ship's model in the frame's list, counted from 1 (FigureInput.ModelSlot).</param>
        public void Emit(List<Figure> figures, int lod, float clock, int modelSlot)
        {
            DrawnFigures = 0;
            var model = (float4x4)View.Model.localToWorldMatrix;
            foreach (var man in Men)
            {
                var station = StationOf(man);
                if (station == null || ashore.Contains(man.Man)) continue;
                figures.Add(Build(man, station, modelSlot, lod, clock));
                DrawnFigures++;
            }
            foreach (var body in bodies)
            {
                figures.Add(Build(body, null, modelSlot, lod, clock));
                DrawnFigures++;
            }
        }

        Figure Build(CrewMan man, CrewStation station, int modelSlot, int lod, float clock)
        {
            Vector3 position;
            float facing;
            var seated = false;
            if (man.Fallen)
            {
                position = man.FellPosition;
                facing = man.FellFacing;
            }
            else
            {
                // The station's place and heading straight, without a rotation taken apart again every frame.
                var yaw = station.Slot >= 0 ? PostYaw(station.Slot) : 0f;
                var at = station.Slot >= 0 ? Profile.PostPoint(station, yaw) : station.Position;
                position = at;
                facing = station.Facing + yaw;
                seated = station.Posture == CrewPosture.Sit;
                if (man.WalkTime > 0f)
                {
                    var t = Mathf.Clamp01((clock - man.WalkStart) / man.WalkTime);
                    if (man.Action == CrewAction.Walk)
                    {
                        position = Vector3.Lerp(man.From, at, t);
                        position.y = Mathf.Lerp(man.From.y, at.y, t);
                        facing = Mathf.Atan2(at.x - man.From.x, at.z - man.From.z) * Mathf.Rad2Deg;
                        seated = false;
                    }
                }
            }
            if (man.Action == CrewAction.Overboard && !man.Fallen)
            {
                // Over the rail on his own side and down into the sea, the hull settling under him.
                var t = man.Phase;
                var outward = man.FellPosition.x >= 0f ? 1f : -1f;
                position = man.FellPosition + new Vector3(outward * 1.8f * t, -(man.FellPosition.y + 1.2f) * t * t, 0f);
                facing = outward * 90f;
                seated = false;
            }
            if (man.Action == CrewAction.Walk || man.Action == CrewAction.Kneel || man.Action == CrewAction.Jolt || man.Action == CrewAction.Fall || man.Action == CrewAction.LieWounded) seated = seated && man.Action == CrewAction.LieWounded;
            // His frame is composed on worker threads from the ship's model and this place (CrewFrameJob).
            var input = new FigureInput
            {
                ModelSlot = modelSlot,
                Position = position,
                Facing = facing,
                Height = man.Look.Height,
                Girth = man.Look.Girth,
                Action = (int)(man.Fallen ? CrewAction.Fall : man.Action),
                Phase = man.Fallen ? Mathf.Clamp01((clock - man.FellAt) / 1.1f) : man.Phase,
                Strength = man.Strength,
                Aim = man.Aim,
                Seated = seated,
            };
            // Hands that hold: a rower's on his oar's handle, the helmsman's on the tillers (reaching, CrewPose), marked in the
            // world; from afar (LOD2, a few pixels a man) his pose alone.
            if (!man.Fallen && station != null && lod < 2)
            {
                if ((man.Action == CrewAction.Row || man.Action == CrewAction.RowStanding) && station.Oar >= 0)
                {
                    var pivot = View.OarPivot(station.Oar).localToWorldMatrix;
                    var side = View.OarSide(station.Oar);
                    var inboard = Profile.Parts.OarInboard[0];
                    var end = pivot.MultiplyPoint3x4(new Vector3(-side * inboard, 0f, 0f));
                    var outer = pivot.MultiplyPoint3x4(new Vector3(-side * (inboard - 0.18f), 0f, 0f));
                    input.Hands = 3 | 4;
                    input.HandLeft = side > 0 ? outer : end;
                    input.HandRight = side > 0 ? end : outer;
                }
                else if (man.Action == CrewAction.Steer || man.Action == CrewAction.SteerSeated)
                {
                    var two = Profile.Parts.HelmStands.Length == 2;
                    var index = station.Side < 0 ? 0 : 1;
                    var l = two ? View.TillerGrip(index) + View.Model.TransformDirection(Vector3.left) * 0.08f : View.TillerGrip(0);
                    var r = two ? View.TillerGrip(index) + View.Model.TransformDirection(Vector3.right) * 0.08f : View.TillerGrip(1);
                    input.Hands = 3 | 4;
                    input.HandLeft = l;
                    input.HandRight = r;
                }
            }
            Equip(man, ref input, station);
            if (man.Parts == null || man.PartsLod != lod)
            {
                man.Parts = Pieces(man.Look, man.Role, man.Trade, lod);
                man.PartsLod = lod;
            }
            return new Figure { Ship = ShipId, Man = man.Man, Input = input, Parts = man.Parts };
        }

        /// <summary>What he holds and where his shield is, by what he does.</summary>
        static void Equip(CrewMan man, ref FigureInput input, CrewStation station)
        {
            input.WeaponMode = 2;
            input.ShieldMode = 0;
            switch (man.Action)
            {
                case CrewAction.Fight:
                case CrewAction.Guard:
                case CrewAction.Brace:
                case CrewAction.Watch when man.Role != CrewRole.BowHand:
                case CrewAction.JavelinReady:
                case CrewAction.JavelinThrow:
                case CrewAction.CutLine:
                    input.WeaponMode = 1;
                    input.ShieldMode = 1;
                    break;
                case CrewAction.BowReady:
                case CrewAction.BowDraw:
                case CrewAction.BowLoose:
                    input.WeaponMode = 3;
                    break;
                case CrewAction.Bail:
                case CrewAction.Haul:
                case CrewAction.HaulLine:
                    input.WeaponMode = man.Role == CrewRole.LineHand ? 1 : 2;
                    break;
            }
            if (man.Role == CrewRole.BowHand) input.WeaponMode = 1;
            if (man.Action == CrewAction.SitIdle && station != null && station.Kind == StationKind.Seat) input.WeaponMode = 2;
        }

        /// <summary>The meshes of a man at a level of detail, in his colours (CrewMeshes), each on its bone or socket.</summary>
        public static List<(int Bone, Mesh Mesh, float4x4 Local)> Pieces(CrewLook look, CrewRole role, CrewTrade trade, int lod)
        {
            var list = new List<(int, Mesh, float4x4)>();
            // From afar (3 to 15 pixels a man) shades that close up tell one man from another merge into a few, so the men
            // share fewer meshes and draws; the sides' colours, helmets, shields and builds stay.
            if (lod > 0)
            {
                look.Skin = Coarse(look.Skin);
                look.Hair = Coarse(look.Hair);
                look.Cloth = Coarse(look.Cloth);
                look.Face = 0;
                look.HairStyle = 0;
            }
            var h = look.Height / CrewBody.ReferenceHeight;
            var body = float4x4.Scale(new float3(h * look.Girth, h, h * look.Girth));
            var head = float4x4.Scale(new float3(h));
            var one = float4x4.identity;
            var skin = look.Skin;
            var cloth = look.Cloth;
            var torso = look.Dress == 0 ? skin : look.Dress == 2 ? CrewPalette.Cell.Bronze : cloth;
            var sleeve = look.Dress == 0 ? skin : cloth;
            var detail = Mathf.Min(lod, 2);
            void Add(Bone bone, CrewMeshes.Piece piece, int variant, int side, CrewPalette.Cell first, CrewPalette.Cell second, float4x4 local) =>
                list.Add(((int)bone, CrewMeshes.Get(piece, detail, variant, side, first, second), local));
            if (lod >= 2)
            {
                // Farther still (2 to 5 pixels a metre, a man a few pixels tall) eight pieces: the hips, the trunk, the head,
                // each arm and leg in one along its upper bone, and the shield; hair, helmet and gear are below a pixel.
                Add(Bone.Pelvis, CrewMeshes.Piece.Pelvis, 0, 0, cloth, cloth, body);
                Add(Bone.Spine, CrewMeshes.Piece.Trunk, 0, 0, torso, torso, body);
                Add(Bone.Head, CrewMeshes.Piece.Head, 0, 0, skin, skin, head);
                Add(Bone.UpperArmL, CrewMeshes.Piece.ArmLong, 0, 1, skin, skin, body);
                Add(Bone.UpperArmR, CrewMeshes.Piece.ArmLong, 0, 0, skin, skin, body);
                Add(Bone.ThighL, CrewMeshes.Piece.LegLong, 0, 1, skin, skin, body);
                Add(Bone.ThighR, CrewMeshes.Piece.LegLong, 0, 0, skin, skin, body);
                if (look.Shield > 0 && role != CrewRole.WarriorRower && role != CrewRole.BankArcher && role != CrewRole.MachineCrew)
                    list.Add((CrewRenderer.ShieldSocket, CrewMeshes.Get(CrewMeshes.Piece.Shield, detail, look.Shield, 0, CrewPalette.Cell.Wicker, look.ShieldPaint), one));
                return list;
            }
            Add(Bone.Pelvis, CrewMeshes.Piece.Pelvis, 0, 0, skin, look.Dress == 0 ? cloth : cloth, body);
            Add(Bone.Spine, CrewMeshes.Piece.Spine, 0, 0, look.Dress == 2 ? cloth : torso, torso, body);
            Add(Bone.Chest, CrewMeshes.Piece.Chest, look.Dress, 0, torso, torso, body);
            Add(Bone.Head, CrewMeshes.Piece.Head, look.Face, 0, skin, skin, head);
            Add(Bone.UpperArmL, CrewMeshes.Piece.UpperArm, 0, 1, skin, sleeve, body);
            Add(Bone.UpperArmR, CrewMeshes.Piece.UpperArm, 0, 0, skin, sleeve, body);
            Add(Bone.ForeArmL, CrewMeshes.Piece.ForeArm, 0, 1, skin, skin, body);
            Add(Bone.ForeArmR, CrewMeshes.Piece.ForeArm, 0, 0, skin, skin, body);
            Add(Bone.ThighL, CrewMeshes.Piece.Thigh, 0, 1, skin, cloth, body);
            Add(Bone.ThighR, CrewMeshes.Piece.Thigh, 0, 0, skin, cloth, body);
            Add(Bone.ShinL, CrewMeshes.Piece.Shin, look.Greaves ? 1 : 0, 1, skin, CrewPalette.Cell.Bronze, body);
            Add(Bone.ShinR, CrewMeshes.Piece.Shin, look.Greaves ? 1 : 0, 0, skin, CrewPalette.Cell.Bronze, body);
            if (lod == 0)
            {
                Add(Bone.Neck, CrewMeshes.Piece.Neck, 0, 0, skin, skin, body);
                Add(Bone.HandL, CrewMeshes.Piece.Hand, 0, 1, skin, skin, head);
                Add(Bone.HandR, CrewMeshes.Piece.Hand, 0, 0, skin, skin, head);
                Add(Bone.FootL, CrewMeshes.Piece.Foot, 0, 1, skin, skin, head);
                Add(Bone.FootR, CrewMeshes.Piece.Foot, 0, 0, skin, skin, head);
                if (look.Beard > 0) Add(Bone.Head, CrewMeshes.Piece.Beard, look.Beard, 0, look.Hair, look.Hair, head);
            }
            if (look.Helmet == 0 || lod == 0) Add(Bone.Head, CrewMeshes.Piece.Hair, look.HairStyle, 0, look.Hair, look.Hair, head);
            if (look.Helmet > 0)
            {
                var (first, second) = look.Helmet switch
                {
                    1 => (CrewPalette.Cell.Leather, CrewPalette.Cell.Bronze),
                    2 => (CrewPalette.Cell.Bronze, CrewPalette.Cell.Bleached),
                    3 => (CrewPalette.Cell.Leather, CrewPalette.Cell.Linen),
                    _ => (CrewPalette.Cell.Leather, CrewPalette.Cell.Leather),
                };
                Add(Bone.Head, CrewMeshes.Piece.Helmet, look.Helmet, 0, first, second, head);
            }
            // What he carries by his trade and role: never a weapon his trade does not have.
            var weapon = role switch
            {
                CrewRole.BankArcher => CrewMeshes.Piece.Bow,
                CrewRole.BankJavelin => CrewMeshes.Piece.Javelin,
                CrewRole.BowHand => CrewMeshes.Piece.Pole,
                CrewRole.LineHand => CrewMeshes.Piece.Rope,
                CrewRole.Marine or CrewRole.Commander or CrewRole.WarriorRower or CrewRole.Lookout when trade == CrewTrade.Swordsman => CrewMeshes.Piece.Spear,
                CrewRole.Marine or CrewRole.Commander or CrewRole.WarriorRower or CrewRole.Lookout when trade == CrewTrade.Javelin => CrewMeshes.Piece.Javelin,
                CrewRole.Marine or CrewRole.Commander or CrewRole.WarriorRower or CrewRole.Lookout when trade == CrewTrade.Archer => CrewMeshes.Piece.Bow,
                _ => (CrewMeshes.Piece)(-1),
            };
            // Shafts, bows and poles are a few centimetres thick: below a pixel, without anti-aliasing they would flicker,
            // so only up close; a coil of rope and shields read from afar.
            var thin = weapon == CrewMeshes.Piece.Spear || weapon == CrewMeshes.Piece.Javelin || weapon == CrewMeshes.Piece.Bow || weapon == CrewMeshes.Piece.Pole;
            if (thin && lod > 0) weapon = (CrewMeshes.Piece)(-1);
            if ((int)weapon >= 0 && role != CrewRole.WarriorRower)
                list.Add((CrewRenderer.Weapon, CrewMeshes.Get(weapon, detail, 0, 0, weapon == CrewMeshes.Piece.Rope ? CrewPalette.Cell.Rope : CrewPalette.Cell.WoodShaft, CrewPalette.Cell.Bronze), one));
            if (look.Shield > 0 && role != CrewRole.WarriorRower && role != CrewRole.BankArcher && role != CrewRole.MachineCrew)
                list.Add((CrewRenderer.ShieldSocket, CrewMeshes.Get(CrewMeshes.Piece.Shield, detail, look.Shield, 0, CrewPalette.Cell.Wicker, look.ShieldPaint), one));
            if (trade == CrewTrade.Archer && lod < 2 && role != CrewRole.WarriorRower)
                list.Add((CrewRenderer.Extra, CrewMeshes.Get(CrewMeshes.Piece.Quiver, detail, 0, 0, CrewPalette.Cell.Leather, CrewPalette.Cell.Leather), one));
            return list;
        }

        /// <summary>
        /// The shade a colour merges into from afar: two skins (lighter and darker), two hairs, three undyed cloths (linen,
        /// wool, brown), each side's dye without its worn shade.
        /// </summary>
        public static CrewPalette.Cell Coarse(CrewPalette.Cell cell) => cell switch
        {
            CrewPalette.Cell.Skin0 or CrewPalette.Cell.Skin1 => CrewPalette.Cell.Skin1,
            CrewPalette.Cell.Skin2 or CrewPalette.Cell.Skin3 => CrewPalette.Cell.Skin2,
            CrewPalette.Cell.Hair0 or CrewPalette.Cell.Hair1 => CrewPalette.Cell.Hair1,
            CrewPalette.Cell.Linen or CrewPalette.Cell.Bleached or CrewPalette.Cell.LinenWorn or CrewPalette.Cell.Bandage => CrewPalette.Cell.Linen,
            CrewPalette.Cell.Wool or CrewPalette.Cell.WoolWorn or CrewPalette.Cell.Ochre => CrewPalette.Cell.Wool,
            CrewPalette.Cell.NavyWorn => CrewPalette.Cell.Navy,
            CrewPalette.Cell.MadderWorn => CrewPalette.Cell.Madder,
            _ => cell,
        };

        /// <summary>A man of a role at rest (a rower seated) as one mesh in his colours, for previews in the editor.</summary>
        public static Mesh PreviewMesh(CrewRole role)
        {
            var trade = CrewRoles.Of(role).Trades[0];
            var look = CrewLooks.For(1, (int)trade * CrewRoster.TradeStride + (int)role, role, 0, trade != CrewTrade.Sailor && trade != CrewTrade.Wounded);
            var input = new FigureInput
            {
                Frame = float4x4.identity, Height = look.Height, Girth = look.Girth, Strength = 1f, Phase = 0.25f,
                Action = (int)(role == CrewRole.Rower ? CrewAction.SitIdle : CrewAction.StandIdle), Seated = role == CrewRole.Rower,
            };
            var bones = new NativeArray<float4x4>(CrewRenderer.Matrices, Allocator.Temp);
            CrewPose.Solve(input, bones.Slice(0, CrewRenderer.Matrices));
            var combine = new List<CombineInstance>();
            foreach (var (bone, mesh, local) in Pieces(look, role, trade, 0))
                combine.Add(new CombineInstance { mesh = mesh, transform = (Matrix4x4)math.mul(bones[bone], local) });
            bones.Dispose();
            var merged = new Mesh { name = $"Crew {role} preview", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            merged.CombineMeshes(combine.ToArray(), true, true);
            return merged;
        }

        /// <summary>Where shaft i of a post's shot leaves: the bow hand or throwing hand of its i-th man; a machine's muzzle.</summary>
        public Vector3? LaunchPoint(int slot, int shaft, NativeArray<float4x4> bones, Dictionary<long, int> figureOf)
        {
            if (slot < 0 || slot >= mounts.Length || mounts[slot] == WeaponType.None) return null;
            var weapon = WeaponCatalog.Get(mounts[slot]);
            if (weapon.IsMachine)
            {
                var mount = mountTransforms.Length > slot ? mountTransforms[slot] : null;
                if (mount == null) return null;
                var size = Profile.Parts.Mounts.Length > slot ? ShipCatalog.Get(Class).Slots[slot] : SlotSize.Medium;
                return mount.TransformPoint(size == SlotSize.Heavy ? new Vector3(0f, 1.9f, 1.2f) : new Vector3(0f, 1.15f, 1.25f));
            }
            var crew = new List<CrewMan>();
            foreach (var man in Men) if (man.Slot == slot && man.Station >= 0) crew.Add(man);
            if (crew.Count == 0 || !bones.IsCreated) return null;
            crew.Sort((a, b) => Profile.Stations[a.Station].Order.CompareTo(Profile.Stations[b.Station].Order));
            var who = crew[shaft % crew.Count];
            if (!figureOf.TryGetValue(CrewDirector.Key(ShipId, who.Man), out var figure) || (figure + 1) * CrewRenderer.Matrices > bones.Length) return null;
            var hand = weapon.CrewKind == WeaponCrew.Archers ? (int)Bone.HandL : (int)Bone.HandR;
            var m = bones[figure * CrewRenderer.Matrices + hand];
            return new Vector3(m.c3.x, m.c3.y, m.c3.z);
        }
    }
}
