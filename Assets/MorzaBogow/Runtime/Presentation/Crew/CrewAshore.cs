using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// One man at work ashore as drawn: a man of a ship's shore party or of a port's dock gang (ShipCrew), or a shipwright
    /// at a slipway (Slipway). Only his state lives here: where he stands and which way he faces, what he does (an action
    /// of the crews' poses and its phase), what he holds and whether the player may see him. No scene object stands for
    /// him: CrewDirector draws him with the crews' body and renderer (CrewAshore). His owner moves him with the steps the
    /// old stand-in figures took, at the same speeds and thresholds, so the work keeps its rhythm in game time.
    /// </summary>
    public sealed class ShoreMan
    {
        /// <summary>Metres walked in one whole cycle of the walking pose, two steps.</summary>
        public const float StrideMetres = 1.8f;

        public ShoreMan(int index) => Index = index;

        /// <summary>His place in his party, gang or yard: with that place's id, his lasting identity (CrewAshore.Identify).</summary>
        public int Index { get; }
        public Vector3 Position { get; private set; }
        /// <summary>Which way he faces: degrees about the world's up axis, 0 toward +Z.</summary>
        public float Facing { get; private set; }
        public CrewAction Action { get; private set; } = CrewAction.StandIdle;
        /// <summary>Phase of his walking cycle or of his blow, 0 to 1.</summary>
        public float Phase { get; private set; }
        public CrewTool Tool { get; private set; }
        public CrewLoad Load { get; private set; }
        /// <summary>Ashore, at work, where the player can see him.</summary>
        public bool Shown { get; private set; }

        /// <summary>
        /// Who he is, set when he is drawn (CrewAshore.Identify): the number of a man of his ship's roster, or of his
        /// port's or yard's men; and the look that number gives him. -1 before he is first drawn.
        /// </summary>
        public int Number { get; internal set; } = -1;
        public CrewLook Look { get; internal set; }

        /// <summary>The ship, port or settlement he belongs to, as last identified.</summary>
        internal int PlaceId = int.MinValue;
        internal List<(int Bone, Mesh Mesh, float4x4 Local)> Parts;
        internal long PartsKey = long.MinValue;

        internal void Show(bool shown) => Shown = shown;

        internal void Place(Vector3 at) => Position = at;

        internal void SetTool(CrewTool tool) => Tool = tool;

        internal void SetLoad(CrewLoad load)
        {
            Load = load;
            if (Action == CrewAction.StandIdle || Action == CrewAction.Carry) Stand();
        }

        /// <summary>Walks toward a point at a speed; the walking pose runs with the ground covered. True on arrival.</summary>
        internal bool WalkTo(Vector3 target, float speed, float delta)
        {
            var offset = target - Position;
            var distance = offset.magnitude;
            var step = speed * delta;
            if (distance <= step || distance < 0.05f)
            {
                Position = target;
                Stand();
                return true;
            }
            var flat = new Vector3(offset.x, 0f, offset.z);
            if (flat.sqrMagnitude > 1e-4f) Facing = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            Position += offset / distance * step;
            Action = CrewAction.Walk;
            Phase = Mathf.Repeat(Phase + step / StrideMetres, 1f);
            return false;
        }

        internal void Face(Vector3 point)
        {
            var flat = point - Position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 1e-4f) Facing = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        /// <summary>Standing: at ease, or holding his load before him.</summary>
        internal void Stand() => Action = Load != CrewLoad.None ? CrewAction.Carry : CrewAction.StandIdle;

        /// <summary>A blow of his axe or pick: raised high, brought down, recovered (CutLine). Phase 0 to 1 per blow.</summary>
        internal void Strike(float phase)
        {
            Action = CrewAction.CutLine;
            Phase = phase;
        }
    }

    /// <summary>
    /// The men at work ashore, drawn as the crews aboard are (docs/art/ZALOGI.md): the same rig, palette, poses and
    /// instanced draws (CrewBody, CrewPose, CrewRenderer), gathered each frame by CrewDirector from the battle's shore
    /// parties, dock gangs and slipways. A ship's shore party are its own men: man i of the party is the man its crew
    /// aboard leaves out while he is ashore (CrewAboard.AshoreMen), with the look he has on deck. A port's gang and a
    /// yard's shipwrights are men of that place, numbered by it. Every look comes from these lasting numbers alone
    /// (CrewLooks), never from instance ids or the game's random numbers. What each man does is his owner's: walking,
    /// standing, carrying a load before him with both hands on it, blows of an axe or a pick.
    /// </summary>
    public static class CrewAshore
    {
        /// <summary>Whose men: a ship's shore party, a port's dock gang, a settlement's shipwrights.</summary>
        public enum Group { Party, Dock, Yard }

        /// <summary>
        /// The numbers each group's men are drawn under in CrewDirector's table (CrewDirector.Key: below 2^20), apart from
        /// every man aboard (under five trades of CrewRoster.TradeStride) and from each other. They are also the men's own
        /// numbers, except a party's men, who are men of their ship's roster when it has the sailors to be them.
        /// </summary>
        public const int PartyMen = 0x40000, DockMen = 0x50000, YardMen = 0x60000;

        static int Base(Group group) => group == Group.Party ? PartyMen : group == Group.Dock ? DockMen : YardMen;

        /// <summary>
        /// Who a man is. A ship's party: the man of the roster he is (CrewAboard.AshoreMen), with his look from the deck;
        /// without a sailor aboard to be him, a man of the party's own numbers. A port's or yard's man: one of that place's.
        /// Called each frame he is drawn; his look and pieces change only when his number does.
        /// </summary>
        public static void Identify(ShoreMan man, int place, Group group, int owner, CrewAboard aboard)
        {
            var number = Base(group) + man.Index;
            if (group == Group.Party && aboard != null && man.Index < aboard.AshoreMen.Count) number = aboard.AshoreMen[man.Index];
            if (number == man.Number && place == man.PlaceId) return;
            var deck = group == Group.Party && aboard != null && number < PartyMen ? aboard.Men.Find(m => m.Man == number) : null;
            man.Number = number;
            man.PlaceId = place;
            // Undyed linen and wool, as the sailors aboard (CrewLooks): workers ashore wear no side's dye.
            man.Look = deck != null ? deck.Look : CrewLooks.For(place, number, group == Group.Party ? CrewRole.SailHand : CrewRole.LineHand, owner, false);
            man.PartsKey = long.MinValue;
        }

        /// <summary>
        /// Where a load rides before a man of the reference height (his station frame: +Z ahead, +Y up) and where his
        /// hands hold it: at its sides, or under a log.
        /// </summary>
        static (Vector3 centre, float halfWidth, float grip) Hold(CrewLoad load) => load switch
        {
            CrewLoad.Log => (new Vector3(0f, 0.98f, 0.29f), 0.24f, -0.07f),
            CrewLoad.Chest => (new Vector3(0f, 1.0f, 0.3f), 0.235f, 0f),
            _ => (new Vector3(0f, 1.0f, 0.31f), 0.215f, 0f),
        };

        /// <summary>
        /// The figure to pose and draw for a man ashore (after Identify): his frame on the ground where he stands, his
        /// action and phase; his tool in his right fist (WeaponMode 1), or his load before him on the socket's resting
        /// place (WeaponMode 0) with both hands on it by reaching (CrewPose), walking or standing.
        /// </summary>
        public static Figure Build(ShoreMan man, int place, Group group, int lod)
        {
            var look = man.Look;
            var scale = look.Height / CrewBody.ReferenceHeight;
            var input = new FigureInput
            {
                Frame = float4x4.TRS(man.Position, quaternion.RotateY(math.radians(man.Facing)), new float3(1f)),
                Height = look.Height,
                Girth = look.Girth,
                Action = (int)man.Action,
                Phase = man.Phase,
                Strength = 1f,
                WeaponMode = man.Load != CrewLoad.None ? 0 : 1,
            };
            if (man.Load != CrewLoad.None)
            {
                var (centre, half, grip) = Hold(man.Load);
                input.Rest = (float3)centre * scale;
                input.Hands = 3;
                input.HandLeft = new float3(-half, centre.y + grip, centre.z) * scale;
                input.HandRight = new float3(half, centre.y + grip, centre.z) * scale;
            }
            var key = PartsKey(man, lod);
            if (man.Parts == null || man.PartsKey != key)
            {
                man.Parts = Pieces(man, lod);
                man.PartsKey = key;
            }
            return new Figure { Ship = place, Man = Base(group) + man.Index, Input = input, Parts = man.Parts };
        }

        /// <summary>What decides a man's pieces in the frame's list: who he is and what he holds (CrewDirector's signature).</summary>
        public static long Signature(ShoreMan man) => (long)man.Number * 64 + (int)man.Tool * 8 + (int)man.Load;

        static long PartsKey(ShoreMan man, int lod) => Signature(man) * 4 + lod;

        /// <summary>
        /// The tool turned in the fist so its haft runs on from the forearm, cocked 40° off it: overhead at the top of the
        /// swing (CutLine), level before him as the blow lands, hanging from his hand as he walks.
        /// </summary>
        static readonly float4x4 ToolInHand = float4x4.RotateX(math.radians(50f));

        /// <summary>His body as a sailor's (undyed cloth, no weapon, CrewAboard.Pieces), with his load or his tool.</summary>
        static List<(int Bone, Mesh Mesh, float4x4 Local)> Pieces(ShoreMan man, int lod)
        {
            var list = CrewAboard.Pieces(man.Look, CrewRole.SailHand, CrewTrade.Sailor, lod);
            var detail = Mathf.Min(lod, 2);
            if (man.Load != CrewLoad.None)
            {
                var (piece, first, second) = man.Load switch
                {
                    CrewLoad.Log => (CrewMeshes.Piece.Log, CrewPalette.Cell.Brown, CrewPalette.Cell.Brown),
                    CrewLoad.Chest => (CrewMeshes.Piece.OreChest, CrewPalette.Cell.Brown, CrewPalette.Cell.Bronze),
                    _ => (CrewMeshes.Piece.Crate, CrewPalette.Cell.Wicker, CrewPalette.Cell.Rope),
                };
                list.Add((CrewRenderer.Weapon, CrewMeshes.Get(piece, detail, 0, 0, first, second), float4x4.identity));
            }
            // A haft a few centimetres thick is below a pixel from afar and would flicker, as spears would: only up close.
            else if (man.Tool != CrewTool.None && lod == 0)
            {
                var piece = man.Tool == CrewTool.Axe ? CrewMeshes.Piece.Axe : CrewMeshes.Piece.Pick;
                list.Add((CrewRenderer.Weapon, CrewMeshes.Get(piece, 0, 0, 0, CrewPalette.Cell.WoodShaft, CrewPalette.Cell.Bronze), ToolInHand));
            }
            return list;
        }

        /// <summary>The box the shown men stand in, with room for their bodies, an axe overhead and a log; false when none is shown.</summary>
        public static bool BoxOf(IReadOnlyList<ShoreMan> men, out Bounds box)
        {
            box = default;
            var any = false;
            for (var i = 0; i < men.Count; i++)
            {
                var man = men[i];
                if (!man.Shown) continue;
                if (!any) box = new Bounds(man.Position, Vector3.zero);
                else box.Encapsulate(man.Position);
                any = true;
            }
            if (any) box.SetMinMax(box.min - new Vector3(1.5f, 0.5f, 1.5f), box.max + new Vector3(1.5f, 3f, 1.5f));
            return any;
        }
    }
}
