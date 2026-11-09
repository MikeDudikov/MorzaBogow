using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// A slipway on a settlement's land: a timber ramp running down the bank into the sea, and on it the hull now
    /// being built, shown at the stage its build order has reached. Shipwrights work beside it while it grows.
    /// Presentation only: it reads the order's class and progress and never changes the game.
    /// <list type="bullet">
    /// <item>0–10%: the keel laid on its cradle, scaffolding raised.</item>
    /// <item>10–35%: frames (ribs) set up from stern to bow.</item>
    /// <item>35–70%: planking rises from the keel to the gunwale.</item>
    /// <item>70–85%: the deck laid from stern to bow.</item>
    /// <item>85–95%: mast stepped; a ram's bronze beak fitted.</item>
    /// <item>95–100%: oars and furled sail; the scaffolding comes down.</item>
    /// </list>
    /// </summary>
    public sealed class Slipway
    {
        // Slope (below): the drop of the ramp for each metre run toward the sea.
        // The ramp stands on posts above the bank, so it reads as one run from the land down into the sea.
        const float RampStart = 1f, RampEnd = 27f, RampTop = 2.7f;
        public const float Slope = 0.12f;

        readonly Transform root, cradle;
        readonly PlaceholderMaterials materials;
        readonly int owner;
        readonly float side;
        Transform hullParts;
        Transform keel, planking, deck, mast, sail, beak;
        Transform[] ribs = new Transform[0];
        Transform[] scaffold = new Transform[0];
        Transform[] oars = new Transform[0];
        readonly CrewFigure[] shipwrights;
        ShipClass? building;
        float length, beam, height, workTime;

        public ShipClass? Building => building;
        public float Progress { get; private set; }

        /// <summary>What a watcher sees now, for tools and tests: frames standing, planking height, deck, rig.</summary>
        public int FramesShown { get; private set; }
        public float PlankedFraction { get; private set; }
        public bool Decked { get; private set; }
        public bool Rigged { get; private set; }
        public bool Scaffolded { get; private set; }
        public int ShipwrightsAtWork { get; private set; }

        /// <summary>Rest position of a hull's centre on the cradle and its orientation, bow down the ramp to the sea.</summary>
        public Vector3 RestPosition => cradle.position;
        public Quaternion RestRotation => cradle.rotation;

        /// <summary>Where the ramp meets the sea, in world space.</summary>
        public Vector3 SeaEnd => root.TransformPoint(new Vector3(0f, -0.6f, side * RampEnd));

        /// <param name="parent">The settlement's root, at the land's centre.</param>
        /// <param name="toward">+1 or -1: the side of the land facing its port.</param>
        /// <param name="side">-1 for the war line, +1 for the port line; the ramp runs out to that side.</param>
        public Slipway(Transform parent, PlaceholderMaterials materials, int owner, float toward, float side)
        {
            this.materials = materials;
            this.owner = owner;
            this.side = side;
            root = new GameObject(side < 0 ? "Slipway (war line)" : "Slipway (port line)").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(toward * (float)Rules.SlipAlong, 0f, 0f);

            // The ramp: two runners and cross sleepers, from the bank down below the waterline.
            var run = RampEnd - RampStart;
            var middle = (RampStart + RampEnd) * 0.5f;
            var pitch = Mathf.Atan(Slope) * Mathf.Rad2Deg;
            foreach (var x in new[] { -1.4f, 1.4f })
            {
                CrewFigure.Part(root, PrimitiveType.Cube, materials?.Timber, new Vector3(x, RampHeight(middle) - 0.15f, side * middle),
                    new Vector3(0.45f, 0.3f, run), new Vector3(side * pitch, 0f, 0f));
            }
            for (var z = RampStart + 1f; z < RampEnd; z += 2.2f)
            {
                CrewFigure.Part(root, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, RampHeight(z) - 0.35f, side * z),
                    new Vector3(4.2f, 0.22f, 0.5f), new Vector3(side * pitch, 0f, 0f));
            }
            // Posts carry the ramp down to the ground and into the seabed.
            for (var z = RampStart + 2f; z < RampEnd; z += 5f)
            {
                var top = RampHeight(z) - 0.4f;
                var foot = Mathf.Min(top - 0.3f, -0.8f + 2.6f * Mathf.Clamp01(1f - z / 20f));
                foreach (var x in new[] { -1.4f, 1.4f })
                {
                    CrewFigure.Part(root, PrimitiveType.Cylinder, materials?.Timber, new Vector3(x, (top + foot) * 0.5f, side * z), new Vector3(0.3f, (top - foot) * 0.5f, 0.3f));
                }
            }

            cradle = new GameObject("Cradle").transform;
            cradle.SetParent(root, false);
            cradle.localPosition = new Vector3(0f, RampHeight((float)Rules.SlipCentre) + 0.35f, side * (float)Rules.SlipCentre);
            cradle.localRotation = Quaternion.LookRotation(new Vector3(0f, -Slope, side));

            shipwrights = new CrewFigure[3];
            for (var i = 0; i < shipwrights.Length; i++)
            {
                shipwrights[i] = new CrewFigure(root, materials, owner);
                shipwrights[i].SetTool(CrewTool.Axe);
                shipwrights[i].Show(false);
            }
        }

        /// <summary>Height of the ramp's surface at a distance along it from the land's centre line.</summary>
        static float RampHeight(float along) => RampTop - Slope * (along - RampStart);

        /// <summary>Shows the hull of this class at this stage; nothing when the line is idle.</summary>
        public void Show(ShipClass? shipClass, float progress, float delta)
        {
            if (shipClass != building) Rebuild(shipClass);
            Progress = Mathf.Clamp01(progress);
            FramesShown = 0;
            PlankedFraction = 0f;
            Decked = Rigged = Scaffolded = false;
            ShipwrightsAtWork = 0;
            if (building == null) { foreach (var wright in shipwrights) wright.Show(false); return; }

            var p = Progress;
            keel.gameObject.SetActive(true);

            // Frames go up one after another from stern to bow.
            var framed = Mathf.Clamp01((p - 0.1f) / 0.25f);
            for (var i = 0; i < ribs.Length; i++)
            {
                var up = framed * ribs.Length > i + 0.01f;
                ribs[i].gameObject.SetActive(up);
                if (up) FramesShown++;
            }

            // Planking rises from the keel: the hull's own shape grows taller.
            var planked = Mathf.Clamp01((p - 0.35f) / 0.35f);
            PlankedFraction = planked;
            planking.gameObject.SetActive(planked > 0f);
            if (planked > 0f)
            {
                var tall = height * Mathf.Lerp(0.15f, 1f, planked);
                planking.localScale = new Vector3(beam, length * 0.5f, tall);
                planking.localPosition = new Vector3(0f, tall * 0.5f, 0f);
            }

            var decked = Mathf.Clamp01((p - 0.7f) / 0.15f);
            Decked = decked > 0f;
            deck.gameObject.SetActive(decked > 0f);
            if (decked > 0f)
            {
                var run = length * 0.8f * decked;
                deck.localScale = new Vector3(beam * 0.72f, 0.15f, run);
                deck.localPosition = new Vector3(0f, height * 0.92f, -length * 0.4f + run * 0.5f);
            }

            var stepped = Mathf.Clamp01((p - 0.85f) / 0.08f);
            mast.gameObject.SetActive(stepped > 0f);
            if (stepped > 0f)
            {
                var tall = length * 0.28f * stepped;
                mast.localScale = new Vector3(0.5f, tall, 0.5f);
                mast.localPosition = new Vector3(0f, height + tall, 0f);
            }
            if (beak != null) beak.gameObject.SetActive(p >= 0.9f);

            var rigged = p >= 0.95f;
            Rigged = rigged;
            Scaffolded = p < 0.97f;
            ShipwrightsAtWork = Scaffolded ? shipwrights.Length : 0;
            sail.gameObject.SetActive(rigged);
            foreach (var oar in oars) oar.gameObject.SetActive(rigged);
            foreach (var post in scaffold) post.gameObject.SetActive(Scaffolded);

            // Shipwrights stand on the ramp beside the hull and hew at it, each at their own pace.
            workTime += delta;
            for (var i = 0; i < shipwrights.Length; i++)
            {
                var wright = shipwrights[i];
                var flank = i % 2 == 0 ? -1f : 1f;
                var along = (i - 1) * length * 0.3f;
                var at = cradle.TransformPoint(new Vector3(flank * (beam * 0.5f + 1.1f), 0f, along));
                at.y = root.TransformPoint(new Vector3(0f, RampHeight(Mathf.Abs(root.InverseTransformPoint(at).z)), 0f)).y;
                wright.Place(at);
                wright.Face(cradle.TransformPoint(new Vector3(0f, height * 0.5f, along)));
                wright.Strike((workTime / ShipCrew.StrikeSeconds + i * 0.37f) % 1f);
                wright.Show(p < 0.97f);
            }
        }

        void Rebuild(ShipClass? shipClass)
        {
            if (hullParts != null) Object.Destroy(hullParts.gameObject);
            hullParts = null;
            building = shipClass;
            workTime = 0f;
            if (shipClass == null) return;

            var definition = ShipCatalog.Get(shipClass.Value);
            length = (float)definition.Radius * 2f;
            beam = (float)definition.Beam;
            height = beam * 0.6f;
            hullParts = new GameObject($"Hull under construction ({shipClass})").transform;
            hullParts.SetParent(cradle, false);

            keel = CrewFigure.Part(hullParts, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, 0.18f, 0f), new Vector3(0.4f, 0.36f, length * 0.94f));

            // Frames: a floor timber and two side timbers, narrowing toward stem and stern.
            var count = Mathf.Max(5, Mathf.RoundToInt(length / 1.7f));
            ribs = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                var z = Mathf.Lerp(-0.42f, 0.42f, i / (count - 1f)) * length;
                var taper = Mathf.Sqrt(Mathf.Max(0.08f, 1f - Mathf.Pow(2f * z / length, 2f)));
                var half = beam * 0.5f * taper;
                var rib = new GameObject("Frame").transform;
                rib.SetParent(hullParts, false);
                rib.localPosition = new Vector3(0f, 0f, z);
                CrewFigure.Part(rib, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, 0.35f, 0f), new Vector3(half * 1.6f, 0.22f, 0.25f));
                foreach (var flank in new[] { -1f, 1f })
                {
                    CrewFigure.Part(rib, PrimitiveType.Cube, materials?.Timber, new Vector3(flank * half * 0.85f, height * 0.6f, 0f),
                        new Vector3(0.22f, height * 1.15f, 0.25f), new Vector3(0f, 0f, -flank * 14f));
                }
                ribs[i] = rib;
            }

            planking = CrewFigure.Part(hullParts, PrimitiveType.Capsule, materials?.Wood, Vector3.zero, Vector3.one, new Vector3(90f, 0f, 0f));
            deck = CrewFigure.Part(hullParts, PrimitiveType.Cube, materials?.Timber, Vector3.zero, Vector3.one);
            mast = CrewFigure.Part(hullParts, PrimitiveType.Cylinder, materials?.Wood, Vector3.zero, Vector3.one);
            sail = CrewFigure.Part(hullParts, PrimitiveType.Cube, owner == 0 ? materials?.PlayerSail : materials?.EnemySail,
                new Vector3(0f, height + length * 0.5f, 0f), new Vector3(length * 0.5f, 0.5f, 0.5f));
            beak = shipClass == ShipClass.Ram
                ? CrewFigure.Part(hullParts, PrimitiveType.Cube, materials?.Bronze, new Vector3(0f, 0.5f, length * 0.58f), new Vector3(0.7f, 0.7f, length * 0.25f))
                : null;

            // Oars lie shipped along the sides until the hull is afloat.
            var oarCount = shipClass == ShipClass.Cutter ? 3 : 6;
            oars = new Transform[oarCount * 2];
            for (var i = 0; i < oars.Length; i++)
            {
                var flank = i < oarCount ? -1f : 1f;
                var along = Mathf.Lerp(-0.3f, 0.3f, (i % oarCount) / Mathf.Max(1f, oarCount - 1f)) * length;
                oars[i] = CrewFigure.Part(hullParts, PrimitiveType.Cube, materials?.Wood, new Vector3(flank * (beam * 0.5f + 0.1f), height * 0.85f, along),
                    new Vector3(0.14f, 0.14f, (float)Rules.OarReach(definition)));
            }

            // Scaffolding posts on both flanks.
            scaffold = new Transform[6];
            for (var i = 0; i < scaffold.Length; i++)
            {
                var flank = i < 3 ? -1f : 1f;
                var along = ((i % 3) - 1) * length * 0.38f;
                var tall = height + 2f;
                scaffold[i] = CrewFigure.Part(hullParts, PrimitiveType.Cylinder, materials?.Timber, new Vector3(flank * (beam * 0.5f + 0.7f), tall * 0.5f - 0.3f, along),
                    new Vector3(0.25f, tall * 0.5f, 0.25f));
            }

            foreach (var part in new[] { planking, deck, mast, sail }) part.gameObject.SetActive(false);
            if (beak != null) beak.gameObject.SetActive(false);
            foreach (var oar in oars) oar.gameObject.SetActive(false);
            foreach (var rib in ribs) rib.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// A settlement's slipways: the war line's on one side of the jetty, the port line's on the other. A bridgehead
    /// has only its port line until its own shipyard rises; the ramp is then built up as that work progresses.
    /// </summary>
    public sealed class ShipyardVisual
    {
        public readonly Slipway War, Port;
        readonly Transform warRoot;

        public ShipyardVisual(Transform settlementRoot, PlaceholderMaterials materials, Settlement settlement, float toward)
        {
            Port = new Slipway(settlementRoot, materials, settlement.Owner, toward, 1f);
            War = new Slipway(settlementRoot, materials, settlement.Owner, toward, -1f);
            warRoot = settlementRoot.Find("Slipway (war line)");
        }

        public Slipway Line(bool war) => war ? War : Port;

        public void Update(Settlement settlement, float delta)
        {
            var hasYard = !settlement.IsOutpost || settlement.HasShipyard;
            var rising = settlement.IsOutpost && !settlement.HasShipyard && settlement.ShipyardBuild != null;
            if (warRoot != null)
            {
                var shown = hasYard || rising;
                if (warRoot.gameObject.activeSelf != shown) warRoot.gameObject.SetActive(shown);
                // While the bridgehead's shipyard is being built, its ramp rises out of the bank.
                var grown = rising ? Mathf.Lerp(0.15f, 1f, Fraction(settlement.ShipyardBuild)) : 1f;
                warRoot.localScale = new Vector3(1f, grown, 1f);
            }

            var war = hasYard && settlement.WarQueue.Count > 0 ? settlement.WarQueue[0] : null;
            War.Show(war?.Class, Fraction(war), delta);
            var port = settlement.PortQueue.Count > 0 ? settlement.PortQueue[0] : null;
            Port.Show(port?.Class, Fraction(port), delta);
        }

        static float Fraction(BuildOrder order) => order == null || order.TotalTicks <= 0 ? 0f : (float)order.DoneTicks / order.TotalTicks;
    }
}
