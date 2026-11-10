using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>The kinds of named places aboard (CrewLayoutProfile).</summary>
    public enum StationKind { Bench, Helm, Lookout, Command, Sail, Line, Post, Seat, Stand, Rail, Wounded, Bow }

    /// <summary>
    /// A named place aboard where a man works: the roles allowed there, where he is and which way he faces, where his
    /// hands and feet rest, where his weapon or tool is, how far he moves, the group he keeps time with, the room he needs
    /// and from which level of detail he is drawn there. Positions are in the ship's model space (metres, +Z bow, waterline
    /// Y = 0): his hips when he sits, the floor between his feet when he stands. A post's crew stands in the post's own
    /// space (Slot ≥ 0), which turns with the weapon as it is aimed.
    /// </summary>
    public sealed class CrewStation
    {
        public string Id;
        public StationKind Kind;
        public CrewRole[] Roles;
        public Vector3 Position;
        /// <summary>Degrees about Y: 0 faces the bow (+Z), 180 the stern, 90 starboard.</summary>
        public float Facing;
        public CrewPosture Posture;
        public Vector3 HandLeft, HandRight, FootLeft, FootRight;
        /// <summary>Where his weapon or tool rests when he does not hold it.</summary>
        public Vector3 Tool;
        /// <summary>How far from the station his body moves while he works (metres).</summary>
        public float Reach;
        /// <summary>Men who keep one time: 1 the rowers, 2 the helm, 10 + slot a post's crew, 0 none.</summary>
        public int SyncGroup;
        /// <summary>Half the room he needs: across (X), his height (Y) and along the ship (Z).</summary>
        public Vector3 Room;
        /// <summary>Drawn from this level of detail on (0 always): every station of this game is drawn at every level.</summary>
        public int Lod;
        public int Side, Oar = -1, Slot = -1, Bank;
        /// <summary>Filled in this order, lowest first.</summary>
        public int Order;
        /// <summary>On a platform or a castle rather than the deck.</summary>
        public bool Raised;

        public bool Holds(CrewRole role) => System.Array.IndexOf(Roles, role) >= 0;
    }

    /// <summary>
    /// The stations of one class's ship (docs/art/ZALOGI.md), built from its Aegean form (GreekShips): a bench at every oar
    /// where its rower sits facing aft, the helmsman at the tillers, the lookout at the bow, the leader aft, two men at the
    /// mast for the brails, the posts' crews around their weapons, seats along the middle line where the warriors sit at
    /// sea, places on the platforms and the middle line where they stand ready, places at the rail where they fight a
    /// boarding, and the place of the wounded. Where a weapon post leaves no room, a station is left out rather than
    /// squeezed in; every man is a man of 1.6 to 1.72 m, never scaled to fit. Cached per class.
    /// </summary>
    public sealed class CrewLayoutProfile
    {
        /// <summary>A man's half width at the shoulders and half the depth of his body; heights seated and standing.</summary>
        public const float BodyHalfWidth = 0.22f, BodyHalfDepth = 0.16f, SeatedHeight = 0.92f, StandingHeight = 1.72f;
        /// <summary>Spacing of men standing in a line, and of men sitting with their knees drawn up.</summary>
        public const float StandSpacing = 0.7f, SeatSpacing = 0.85f;

        public ShipClass Class;
        public GreekShipParts Parts;
        public readonly List<CrewStation> Stations = new List<CrewStation>();

        static readonly Dictionary<ShipClass, CrewLayoutProfile> cache = new Dictionary<ShipClass, CrewLayoutProfile>();

        public IEnumerable<CrewStation> Of(StationKind kind)
        {
            foreach (var station in Stations) if (station.Kind == kind) yield return station;
        }

        public int Count(StationKind kind)
        {
            var count = 0;
            foreach (var station in Stations) if (station.Kind == kind) count++;
            return count;
        }

        public int IndexOf(string id) => Stations.FindIndex(station => station.Id == id);

        public static CrewLayoutProfile For(ShipClass shipClass)
        {
            if (cache.TryGetValue(shipClass, out var found) && found.Parts.Hull.Mesh != null) return found;
            found = Build(shipClass);
            cache[shipClass] = found;
            return found;
        }

        // Floors ----------------------------------------------------------------------------------------------------
        static float StationOf(GreekShipForm f, float z) => Mathf.InverseLerp(GreekShips.Z(f, 0f), GreekShips.Z(f, 1f), z);

        /// <summary>The top of the stern or bow platform (castle), or -1 if the ship has none.</summary>
        public static float PlatformTop(GreekShipForm f, bool stern) =>
            f.Platforms <= 0f ? -1f : GreekShips.Sheer(f, stern ? 0.115f : 0.885f) + f.Platforms * (stern && !f.Castles ? 0.85f : 1f);

        /// <summary>Half the width of a platform, as the generator builds it.</summary>
        public static float PlatformHalfWidth(GreekShipForm f, bool stern) => GreekShips.HalfWidth(f, stern ? 0.115f : 0.885f) * 0.9f;

        /// <summary>
        /// The floor under a point (GreekShips.FloorAt): a platform's top, the walkway's over the rowers, else the deck (a
        /// boat's bottom boards); raised when it is a platform or the walkway.
        /// </summary>
        public static float FloorAt(GreekShipForm f, Vector3 point, out bool raised)
        {
            var s = StationOf(f, point.z);
            var floor = GreekShips.FloorAt(f, s, point.x);
            raised = floor > GreekShips.DeckAt(f, s) + 0.01f;
            return floor;
        }

        /// <summary>
        /// Whether a man fits on the floor at a point with this much room: on a platform or the walkway within its edges,
        /// on the deck within the hull and clear of the walkway above it.
        /// </summary>
        public static bool OnFloor(GreekShipForm f, Vector3 point, float room)
        {
            var s = StationOf(f, point.z);
            if (s < 0.04f || s > 0.96f) return false;
            FloorAt(f, point, out var raised);
            var onPlatform = f.Platforms > 0f && (s >= 0.05f && s <= 0.18f || s >= 0.82f && s <= 0.95f) && Mathf.Abs(point.x) <= PlatformHalfWidth(f, s < 0.5f);
            if (raised && onPlatform)
            {
                var stern = s < 0.5f;
                var (s0, s1) = stern ? (0.05f, 0.18f) : (0.82f, 0.95f);
                var half = (GreekShips.Z(f, s1) - GreekShips.Z(f, s0)) * 0.5f;
                var middle = (GreekShips.Z(f, s0) + GreekShips.Z(f, s1)) * 0.5f;
                // Castles have screens round their edge, platforms an open rail: both keep a man in by his room.
                return Mathf.Abs(point.x) + room <= PlatformHalfWidth(f, stern) && Mathf.Abs(point.z - middle) + room <= half;
            }
            if (raised)
            {
                var (from, to) = GreekShips.WalkwaySpan(f);
                var span = GreekShips.Z(f, 1f) - GreekShips.Z(f, 0f);
                return Mathf.Abs(point.x) + room <= GreekShips.WalkwayHalfWidth(f, s) && s - room / span >= from && s + room / span <= to;
            }
            if (GreekShips.HasWalkway(f))
            {
                var (from, to) = GreekShips.WalkwaySpan(f);
                if (s >= from && s <= to && Mathf.Abs(point.x) - room < GreekShips.WalkwayHalfWidth(f, s)) return false;
            }
            return Mathf.Abs(point.x) + room <= GreekShips.HalfWidth(f, s) * 0.9f;
        }

        static Vector3 Floored(GreekShipForm f, Vector3 point)
        {
            point.y = FloorAt(f, point, out _);
            return point;
        }

        // Building ---------------------------------------------------------------------------------------------------
        static CrewLayoutProfile Build(ShipClass shipClass)
        {
            var parts = GreekShips.Parts(shipClass);
            var profile = new CrewLayoutProfile { Class = shipClass, Parts = parts };
            AddBenches(profile);
            AddHelm(profile);
            AddPosts(profile);
            AddCommand(profile);
            AddLookout(profile);
            AddMast(profile);
            AddWounded(profile);
            var waist = AddWarriors(profile);
            AddRails(profile, waist);
            return profile;
        }

        /// <summary>One rower at every oar, port oars first then starboard, as the view numbers its oars.</summary>
        static void AddBenches(CrewLayoutProfile profile)
        {
            var parts = profile.Parts;
            var f = parts.Form;
            var pins = parts.OarPins;
            var tilt = parts.OarTilt[0] * Mathf.Deg2Rad;
            var inboard = parts.OarInboard[0];
            var oar = 0;
            for (var sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                var side = sideIndex == 0 ? -1 : 1;
                for (var i = 0; i < pins.Length; i++, oar++)
                {
                    var pin = new Vector3(side * pins[i].x, pins[i].y, pins[i].z);
                    var seat = parts.RowerSeats[i];
                    seat.x *= side;
                    var along = new Vector3(-side * Mathf.Cos(tilt), Mathf.Sin(tilt), 0f);
                    var handle = pin + along * inboard;
                    var feetZ = seat.z - (parts.RowersStand ? 0.15f : 0.6f);
                    var feetY = GreekShips.DeckAt(f, StationOf(f, feetZ));
                    var inner = handle + along * 0.06f;
                    var outer = handle - along * 0.12f;
                    profile.Stations.Add(new CrewStation
                    {
                        Id = $"lawa.{(side < 0 ? "L" : "P")}{i + 1}", Kind = StationKind.Bench, Roles = new[] { CrewRole.Rower, CrewRole.WarriorRower },
                        Position = seat, Facing = 180f, Posture = parts.RowersStand ? CrewPosture.Stand : CrewPosture.Sit,
                        // Facing aft, his left hand is on the starboard side: on a starboard oar it takes the outer grip.
                        HandLeft = side > 0 ? outer : inner, HandRight = side > 0 ? inner : outer,
                        FootLeft = new Vector3(seat.x + 0.12f, feetY, feetZ), FootRight = new Vector3(seat.x - 0.12f, feetY, feetZ),
                        Tool = handle, Reach = 0.6f, SyncGroup = 1,
                        Room = new Vector3(0.26f, parts.RowersStand ? StandingHeight : SeatedHeight, 0.55f),
                        Side = side, Oar = oar,
                        // Rowers fill from the stern; in the boat from the bow, which leaves the helmsman room aft.
                        Order = profile.Class == ShipClass.Cutter ? pins.Length - 1 - i : i,
                    });
                }
            }
        }

        /// <summary>One man between both tillers, or one at each on the broad heavy galley.</summary>
        static void AddHelm(CrewLayoutProfile profile)
        {
            var parts = profile.Parts;
            var two = parts.HelmStands.Length == 2;
            for (var h = 0; h < parts.HelmStands.Length; h++)
            {
                var stand = parts.HelmStands[h];
                FloorAt(parts.Form, stand, out var raised);
                profile.Stations.Add(new CrewStation
                {
                    Id = two ? $"ster.{(h == 0 ? "L" : "P")}" : "ster", Kind = StationKind.Helm, Roles = new[] { CrewRole.Helmsman },
                    Position = stand, Facing = 0f, Posture = parts.HelmSeated ? CrewPosture.Sit : CrewPosture.Stand,
                    HandLeft = two ? parts.TillerGrips[h] + new Vector3(-0.08f, 0f, 0f) : parts.TillerGrips[0],
                    HandRight = two ? parts.TillerGrips[h] + new Vector3(0.08f, 0f, 0f) : parts.TillerGrips[1],
                    FootLeft = stand + new Vector3(-0.14f, 0f, 0f), FootRight = stand + new Vector3(0.14f, 0f, 0f),
                    Tool = parts.TillerGrips[two ? h : 1], Reach = 0.3f, SyncGroup = 2,
                    Room = new Vector3(BodyHalfWidth, parts.HelmSeated ? SeatedHeight : StandingHeight, 0.3f),
                    Side = two ? (h == 0 ? -1 : 1) : 0, Order = h, Raised = raised && !parts.HelmSeated,
                });
            }
        }

        static CrewStation Standing(string id, StationKind kind, CrewRole[] roles, Vector3 at, float facing, int order, bool raised, float reach = 0.4f)
        {
            var turn = Quaternion.Euler(0f, facing, 0f);
            return new CrewStation
            {
                Id = id, Kind = kind, Roles = roles, Position = at, Facing = facing, Posture = CrewPosture.Stand,
                HandLeft = at + turn * new Vector3(-0.2f, 1.0f, 0.15f), HandRight = at + turn * new Vector3(0.2f, 1.0f, 0.15f),
                FootLeft = at + turn * new Vector3(-0.13f, 0f, 0f), FootRight = at + turn * new Vector3(0.13f, 0f, 0f),
                Tool = at + turn * new Vector3(0.3f, 0f, 0.2f), Reach = reach, Room = new Vector3(BodyHalfWidth, StandingHeight, BodyHalfDepth),
                Side = at.x < -0.05f ? -1 : at.x > 0.05f ? 1 : 0, Order = order, Raised = raised,
            };
        }

        /// <summary>The first point, in order of preference, where a standing man has room; null if there is none.</summary>
        static Vector3? FirstClear(CrewLayoutProfile profile, IEnumerable<Vector3> candidates, float room)
        {
            foreach (var candidate in candidates)
            {
                var at = Floored(profile.Parts.Form, candidate);
                if (OnFloor(profile.Parts.Form, at, room) && Clear(profile, at, room)) return at;
            }
            return null;
        }

        /// <summary>Points on a platform (stern or bow) in a grid, the nearest to a preferred point first.</summary>
        static IEnumerable<Vector3> PlatformGrid(GreekShipForm f, bool stern, Vector3 preferred)
        {
            if (f.Platforms <= 0f) yield break;
            var (s0, s1) = stern ? (0.05f, 0.18f) : (0.82f, 0.95f);
            var half = PlatformHalfWidth(f, stern);
            var points = new List<Vector3>();
            for (var z = GreekShips.Z(f, s0) + 0.3f; z <= GreekShips.Z(f, s1) - 0.3f; z += 0.25f)
            for (var x = -half + 0.3f; x <= half - 0.3f + 1e-4f; x += 0.25f)
                points.Add(new Vector3(x, 0f, z));
            points.Sort((a, b) => (a - preferred).sqrMagnitude.CompareTo((b - preferred).sqrMagnitude));
            foreach (var point in points) yield return point;
        }

        /// <summary>Points around a preferred one on deck, in rings, the nearest first.</summary>
        static IEnumerable<Vector3> Around(Vector3 preferred, float radius)
        {
            yield return preferred;
            for (var r = 0.3f; r <= radius + 1e-4f; r += 0.3f)
            for (var k = 0; k < 12; k++)
            {
                var angle = k * Mathf.PI / 6f;
                yield return preferred + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            }
        }

        /// <summary>The leader on the stern platform behind the helmsman, where the Kynos paintings put him; on deck by the helm if a machine fills it.</summary>
        static void AddCommand(CrewLayoutProfile profile)
        {
            var f = profile.Parts.Form;
            if (profile.Class == ShipClass.Cutter) return; // The helmsman commands the boat.
            var helm = profile.Parts.HelmStands.Length == 2 ? (profile.Parts.HelmStands[0] + profile.Parts.HelmStands[1]) * 0.5f : profile.Parts.HelmStands[0];
            if (profile.Class == ShipClass.Merchant)
            {
                var skipper = FirstClear(profile, Around(helm + new Vector3(0.9f, 0f, 0.2f), 1.5f), 0.3f);
                if (skipper.HasValue) profile.Stations.Add(Standing("rufa.szyper", StationKind.Command, new[] { CrewRole.Skipper }, skipper.Value, 0f, 0, false));
                return;
            }
            var preferred = new Vector3(0f, 0f, GreekShips.Z(f, 0.105f));
            var candidates = new List<Vector3>(PlatformGrid(f, true, preferred));
            candidates.AddRange(Around(helm + new Vector3(0.8f, 0f, -0.2f), 1.5f));
            var at = FirstClear(profile, candidates, 0.3f);
            if (!at.HasValue) return;
            FloorAt(f, at.Value, out var raised);
            profile.Stations.Add(Standing("rufa.dowodca", StationKind.Command, new[] { CrewRole.Commander }, at.Value, 0f, 0, raised));
        }

        /// <summary>A man watching ahead from the bow platform, or the deck in the bows; the boat's bow man with his pole.</summary>
        static void AddLookout(CrewLayoutProfile profile)
        {
            var f = profile.Parts.Form;
            if (profile.Class == ShipClass.Cutter)
            {
                var bow = FirstClear(profile, Around(new Vector3(0f, 0f, GreekShips.Z(f, 0.86f)), 0.9f), 0.25f);
                if (bow.HasValue) profile.Stations.Add(Standing("dziob.bosak", StationKind.Bow, new[] { CrewRole.BowHand, CrewRole.Lookout }, bow.Value, 0f, 0, false));
                return;
            }
            var preferred = new Vector3(0f, 0f, GreekShips.Z(f, 0.915f));
            var candidates = new List<Vector3>(PlatformGrid(f, false, preferred));
            candidates.AddRange(Around(new Vector3(0f, 0f, GreekShips.Z(f, 0.86f)), 1.2f));
            var at = FirstClear(profile, candidates, 0.3f);
            if (!at.HasValue) return;
            FloorAt(f, at.Value, out var raised);
            profile.Stations.Add(Standing("dziob.obserwator", StationKind.Lookout, new[] { CrewRole.Lookout }, at.Value, 0f, 0, raised));
        }

        /// <summary>Two men aft of the mast hauling the brails, which run down to the deck behind it; men at the lines and cargo.</summary>
        static void AddMast(CrewLayoutProfile profile)
        {
            var f = profile.Parts.Form;
            if (profile.Class == ShipClass.Cutter) return;
            var foot = profile.Parts.MastFoot;
            for (var i = 0; i < 2; i++)
            {
                var x = i == 0 ? -0.55f : 0.55f;
                var at = FirstClear(profile, Around(new Vector3(x, 0f, foot.z - 0.5f), 0.9f), 0.25f);
                if (at.HasValue) profile.Stations.Add(Standing($"maszt.gejtawy.{(i == 0 ? "L" : "P")}", StationKind.Sail, new[] { CrewRole.SailHand, CrewRole.LineHand }, at.Value, 0f, i, false));
            }
            var lines = profile.Class == ShipClass.Merchant
                ? new[] { new Vector3(-1.4f, 0f, GreekShips.Z(f, 0.36f)), new Vector3(1.4f, 0f, GreekShips.Z(f, 0.36f)), new Vector3(-1.1f, 0f, GreekShips.Z(f, 0.24f)) }
                : new[] { new Vector3(0f, 0f, foot.z - 1.3f) };
            for (var i = 0; i < lines.Length; i++)
            {
                var at = FirstClear(profile, Around(lines[i], 1.2f), 0.25f);
                if (at.HasValue) profile.Stations.Add(Standing($"liny.{i + 1}", StationKind.Line, new[] { CrewRole.LineHand, CrewRole.SailHand, CrewRole.Lookout }, at.Value, at.Value.x < -0.1f ? 90f : at.Value.x > 0.1f ? -90f : 0f, i, false));
            }
        }

        // Posts ------------------------------------------------------------------------------------------------------
        /// <summary>
        /// Where the men of a weapon post stand in the post's own space (the weapon faces +Z), by the post's size and the
        /// weapon on it: four archers or javelin men on a light post's stand behind its wicker screen; at a machine the man
        /// who aims and looses behind it, the loader in front, the men at the winch on either side and the men who bring
        /// bolts or stones. A weapon with fewer men takes the first places.
        /// </summary>
        public static Vector3[] PostPlaces(SlotSize size, WeaponType weapon)
        {
            switch (size)
            {
                case SlotSize.Light:
                    return new[] { new Vector3(-0.38f, 0.2f, 0.22f), new Vector3(0.38f, 0.2f, 0.22f), new Vector3(-0.38f, 0.2f, -0.32f), new Vector3(0.38f, 0.2f, -0.32f) };
                case SlotSize.Medium:
                    // A great bow on a stand: stock along +Z from -0.8 to 1.2, the bow across it at Z 1.05. Close round it,
                    // as on a galley's platform there is no more room: the aimer at the stock's end, the others beside it.
                    if (weapon == WeaponType.Gastraphetes) return new[] { new Vector3(0f, 0f, -0.95f), new Vector3(0.55f, 0f, 0.3f) };
                    return new[] { new Vector3(0f, 0f, -0.95f), new Vector3(0.55f, 0f, 0.3f), new Vector3(-0.5f, 0f, -0.45f), new Vector3(0.5f, 0f, -0.45f), new Vector3(-0.85f, 0f, -0.9f), new Vector3(0.85f, 0f, -0.9f) };
                default:
                    // A throwing frame on a base 2 x 2.4 m: the arm along +Z, its sling forward when wound down.
                    return new[]
                    {
                        new Vector3(0f, 0f, -1.5f), new Vector3(0.65f, 0f, 1.5f), new Vector3(-1.3f, 0f, -0.45f), new Vector3(1.3f, 0f, -0.45f),
                        new Vector3(-1.3f, 0f, 0.35f), new Vector3(1.3f, 0f, 0.35f), new Vector3(-0.85f, 0f, -1.45f), new Vector3(0.85f, 0f, -1.45f),
                    };
            }
        }

        /// <summary>Which way a post place faces in the post's space: the aimer along the shot, the others toward the weapon.</summary>
        public static float PostFacing(SlotSize size, Vector3 place)
        {
            if (size == SlotSize.Light || Mathf.Abs(place.x) < 0.1f && place.z < 0f) return 0f;
            return Mathf.Atan2(-place.x, -place.z * 0.4f) * Mathf.Rad2Deg;
        }

        static void AddPosts(CrewLayoutProfile profile)
        {
            var definition = ShipCatalog.Get(profile.Class);
            var f = profile.Parts.Form;
            for (var slot = 0; slot < definition.Slots.Length; slot++)
            {
                var size = definition.Slots[slot];
                FloorAt(f, profile.Parts.Mounts[slot], out var raised);
                // Places for the most men any weapon of this size needs; the weapon on the post takes the first ones.
                var places = PostPlaces(size, WeaponType.None);
                for (var i = 0; i < places.Length; i++)
                {
                    var p = places[i];
                    var facing = PostFacing(size, p);
                    var turn = Quaternion.Euler(0f, facing, 0f);
                    profile.Stations.Add(new CrewStation
                    {
                        Id = $"bron.{slot}.{i + 1}", Kind = StationKind.Post, Roles = new[] { CrewRole.BankArcher, CrewRole.BankJavelin, CrewRole.MachineCrew },
                        Position = p, Facing = facing, Posture = CrewPosture.Stand,
                        HandLeft = p + turn * new Vector3(-0.2f, 1.1f, 0.3f), HandRight = p + turn * new Vector3(0.2f, 1.1f, 0.3f),
                        FootLeft = p + turn * new Vector3(-0.13f, 0f, 0f), FootRight = p + turn * new Vector3(0.13f, 0f, 0f),
                        Tool = p + turn * new Vector3(0.25f, 0f, 0.3f), Reach = 0.5f, SyncGroup = 10 + slot,
                        Room = new Vector3(BodyHalfWidth, StandingHeight, BodyHalfDepth), Slot = slot, Order = i, Raised = raised,
                    });
                }
            }
        }

        /// <summary>
        /// A post place in the ship's model space, the weapon turned by yaw degrees from the bow. A man of a machine on a
        /// small platform who would stand on its very edge steps fully onto it, or down onto the deck beside it, so he is
        /// never drawn half over the edge.
        /// </summary>
        public Vector3 PostPoint(CrewStation station, float yaw)
        {
            var mount = Parts.Mounts[station.Slot];
            var at = mount + Quaternion.Euler(0f, yaw, 0f) * station.Position;
            if (station.Position.y > 0f) return at;
            var f = Parts.Form;
            at.y = FloorAt(f, at, out var raised);
            if (!raised || OnFloor(f, at, 0.2f)) return at;
            var s = StationOf(f, at.z);
            var stern = s < 0.5f;
            var (s0, s1) = stern ? (0.05f, 0.18f) : (0.82f, 0.95f);
            var middle = (GreekShips.Z(f, s0) + GreekShips.Z(f, s1)) * 0.5f;
            var half = (GreekShips.Z(f, s1) - GreekShips.Z(f, s0)) * 0.5f;
            var inward = at;
            inward.z = middle + Mathf.Clamp(at.z - middle, -(half - 0.2f), half - 0.2f);
            var widest = PlatformHalfWidth(f, stern) - 0.2f;
            inward.x = Mathf.Clamp(at.x, -widest, widest);
            if ((inward - at).sqrMagnitude < 0.3f * 0.3f) return inward;
            // Down onto the deck at the platform's inner end, toward the waist (its outer end is the hull's end).
            var off = at;
            off.z = stern ? middle + half + 0.25f : middle - half - 0.25f;
            off.y = FloorAt(f, off, out _);
            return off;
        }

        // Warriors ---------------------------------------------------------------------------------------------------
        /// <summary>
        /// Seats along the middle line where warriors sit at sea, out of the rowers' reach, the mast, the posts and their
        /// crews; places on the platforms and the middle line where they stand ready for a fight. Returns the standing places
        /// of the waist, row by row.
        /// </summary>
        static List<CrewStation> AddWarriors(CrewLayoutProfile profile)
        {
            var f = profile.Parts.Form;
            var waist = new List<CrewStation>();
            if (profile.Class == ShipClass.Cutter) return waist;
            var from = GreekShips.Z(f, f.Platforms > 0f ? 0.19f : 0.14f);
            var to = GreekShips.Z(f, f.Platforms > 0f ? 0.81f : 0.86f);
            // How far in the rowers' bodies and looms reach from each side: the warriors keep inboard of it.
            var benchInner = GreekShips.HalfWidth(f, 0.5f);
            foreach (var bench in profile.Of(StationKind.Bench))
                benchInner = Mathf.Min(benchInner, Mathf.Abs(bench.Position.x) - BodyHalfWidth, Mathf.Abs(bench.HandLeft.x), Mathf.Abs(bench.HandRight.x));
            // On a galley the lanes run along the walkway over the rowers; elsewhere across the deck inboard of them.
            var laneEdge = GreekShips.HasWalkway(f) ? GreekShips.WalkwayHalfWidth(f, 0.5f) : benchInner;
            var lanes = new List<float> { 0f };
            for (var x = 0.6f; x + 0.3f <= laneEdge + 1e-3f; x += 0.6f) { lanes.Add(x); lanes.Add(-x); }
            var seat = 0;
            for (var z = from; z <= to; z += SeatSpacing)
            {
                foreach (var x in lanes)
                {
                    var at = Floored(f, new Vector3(x, 0f, z));
                    if (!OnFloor(f, at, 0.3f) || !Clear(profile, at, 0.3f)) continue;
                    FloorAt(f, at, out var raised);
                    var facing = x < -0.01f ? -90f : x > 0.01f ? 90f : seat % 2 == 0 ? 90f : -90f;
                    var turn = Quaternion.Euler(0f, facing, 0f);
                    profile.Stations.Add(new CrewStation
                    {
                        Id = $"siedzisko.{++seat}", Kind = StationKind.Seat, Roles = new[] { CrewRole.Marine, CrewRole.Lookout, CrewRole.Commander, CrewRole.LineHand },
                        Position = at + Vector3.up * 0.06f, Facing = facing, Posture = CrewPosture.Sit,
                        HandLeft = at + turn * new Vector3(-0.15f, 0.42f, 0.25f), HandRight = at + turn * new Vector3(0.15f, 0.42f, 0.25f),
                        FootLeft = at + turn * new Vector3(-0.12f, 0f, 0.4f), FootRight = at + turn * new Vector3(0.12f, 0f, 0.4f),
                        Tool = at + turn * new Vector3(0f, 0f, -0.25f), Reach = 0.2f, Room = new Vector3(0.3f, SeatedHeight, 0.3f), Order = seat, Raised = raised,
                    });
                }
            }
            // Standing ready: the platforms' free room, then the middle line.
            var stand = 0;
            foreach (var stern in new[] { true, false })
            {
                if (f.Platforms <= 0f) break;
                var (s0, s1) = stern ? (0.05f, 0.18f) : (0.82f, 0.95f);
                var half = PlatformHalfWidth(f, stern);
                for (var z = GreekShips.Z(f, s0) + 0.35f; z <= GreekShips.Z(f, s1) - 0.35f; z += StandSpacing)
                for (var x = -half + 0.35f; x <= half - 0.35f + 1e-4f; x += StandSpacing)
                {
                    var at = Floored(f, new Vector3(x, 0f, z));
                    if (!OnFloor(f, at, 0.3f) || !Clear(profile, at, 0.3f, StationKind.Seat)) continue;
                    profile.Stations.Add(Standing($"pomost.{(stern ? "rufa" : "dziob")}.{++stand}", StationKind.Stand, new[] { CrewRole.Marine }, at,
                        x < -0.2f ? -60f : x > 0.2f ? 60f : stern ? 180f : 0f, stand, true));
                }
            }
            for (var z = from; z <= to; z += StandSpacing)
            {
                foreach (var x in lanes)
                {
                    var at = Floored(f, new Vector3(x, 0f, z));
                    if (!OnFloor(f, at, 0.3f) || !Clear(profile, at, 0.3f, StationKind.Seat)) continue;
                    FloorAt(f, at, out var raised);
                    var station = Standing($"gotowosc.{++stand}", StationKind.Stand, new[] { CrewRole.Marine }, at, x < -0.01f ? -90f : 90f, 100 + stand, raised);
                    profile.Stations.Add(station);
                    waist.Add(station);
                }
            }
            return waist;
        }

        /// <summary>
        /// Where the front of a boarding fights on each side: row by row along the waist, the standing place nearest that
        /// side (where the walkway narrows, its middle), where a man standing ready turns to the other ship and fights. On a
        /// galley that is the walkway over the rowers, as the warriors of the Kynos krater stand on the deck above them
        /// (R-KYNOS); the rowers keep their benches in the gallery below, so no fighter stands where a rower sits.
        /// </summary>
        static void AddRails(CrewLayoutProfile profile, List<CrewStation> waist)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var rows = new SortedDictionary<int, CrewStation>();
                foreach (var station in waist)
                {
                    if (station.Position.x * side < -0.05f) continue;
                    var row = Mathf.RoundToInt(station.Position.z * 100f);
                    if (!rows.TryGetValue(row, out var outer) || station.Position.x * side > outer.Position.x * side) rows[row] = station;
                }
                var place = 0;
                foreach (var outer in rows.Values)
                {
                    var rail = Standing($"reling.{(side < 0 ? "L" : "P")}{++place}", StationKind.Rail, new[] { CrewRole.Marine, CrewRole.Commander },
                        outer.Position, side * 90f, place, outer.Raised, 0.6f);
                    rail.Side = side; // the side it fights toward, also from the walkway's middle
                    profile.Stations.Add(rail);
                }
            }
        }

        /// <summary>The wounded sit or lie by the mast, out of the way.</summary>
        static void AddWounded(CrewLayoutProfile profile)
        {
            var f = profile.Parts.Form;
            var foot = profile.Parts.MastFoot;
            var places = profile.Class == ShipClass.Cutter ? 1 : profile.Class == ShipClass.Merchant ? 3 : 6;
            var added = 0;
            var candidates = new List<Vector3>();
            for (var k = 0; k < 12; k++)
            {
                var z = foot.z + (k % 2 == 0 ? -1f : 1f) * (0.9f + 0.85f * (k / 2 % 3));
                candidates.Add(new Vector3(k / 6 == 0 ? 0.3f : -0.3f, 0f, z));
            }
            foreach (var candidate in candidates)
            {
                if (added >= places) break;
                var at = Floored(f, candidate);
                if (!OnFloor(f, at, 0.35f) || !Clear(profile, at, 0.35f)) continue;
                added++;
                var outward = at.x < 0f ? -1f : 1f;
                profile.Stations.Add(new CrewStation
                {
                    Id = $"ranni.{added}", Kind = StationKind.Wounded, Roles = new[] { CrewRole.Wounded }, Position = at + Vector3.up * 0.06f,
                    Facing = outward * 90f, Posture = CrewPosture.Sit,
                    HandLeft = at + new Vector3(0f, 0.4f, 0.1f), HandRight = at + new Vector3(0f, 0.4f, -0.05f),
                    FootLeft = at + new Vector3(outward * 0.45f, 0f, 0.1f), FootRight = at + new Vector3(outward * 0.45f, 0f, -0.1f),
                    Room = new Vector3(0.35f, SeatedHeight, 0.35f), Order = added,
                });
            }
        }

        // Room -------------------------------------------------------------------------------------------------------
        /// <summary>
        /// Whether a man can be put at a point (model space) with this much room around him: out of the rowers' strokes
        /// and the swing of their looms, the mast, every weapon post with the room its crew works in turned any way it may
        /// aim, the tillers and every station already placed (except a kind named to share the room, as standing places
        /// share it with the seats the same men leave).
        /// </summary>
        public static bool Clear(CrewLayoutProfile profile, Vector3 at, float room, StationKind shares = StationKind.Rail)
        {
            var parts = profile.Parts;
            var f = parts.Form;
            var flat = new Vector2(at.x, at.z);
            if (Vector2.Distance(flat, new Vector2(parts.MastFoot.x, parts.MastFoot.z)) < room + 0.012f * f.Length + 0.08f + 0.1f) return false;
            var definition = ShipCatalog.Get(profile.Class);
            for (var slot = 0; slot < parts.Mounts.Length; slot++)
            {
                var reach = definition.Slots[slot] == SlotSize.Light ? 1.0f : definition.Slots[slot] == SlotSize.Medium ? 1.6f : 2.0f;
                if (Vector2.Distance(flat, new Vector2(parts.Mounts[slot].x, parts.Mounts[slot].z)) < reach + room) return false;
            }
            foreach (var grip in parts.TillerGrips)
                if (Vector2.Distance(flat, new Vector2(grip.x, grip.z)) < room + 0.3f) return false;
            foreach (var station in profile.Stations)
            {
                if (station.Kind == shares || station.Slot >= 0) continue;
                if (station.Kind == StationKind.Bench)
                {
                    // A rower's whole stroke: from his back to his feet and the handle at the catch, and out to the side.
                    var inner = Mathf.Min(Mathf.Abs(station.Position.x) - station.Room.x, Mathf.Abs(station.HandLeft.x), Mathf.Abs(station.HandRight.x));
                    if (Mathf.Abs(at.x) + room > inner - 0.05f && (at.x >= 0f) == (station.Position.x >= 0f)
                        && at.z + room > station.Position.z - 1.05f && at.z - room < station.Position.z + 0.35f) return false;
                    continue;
                }
                var gap = Vector2.Distance(flat, new Vector2(station.Position.x, station.Position.z));
                if (gap < room + Mathf.Max(station.Room.x, station.Room.z) && Mathf.Abs(at.y - station.Position.y) < 1.2f) return false;
            }
            return true;
        }
    }
}
