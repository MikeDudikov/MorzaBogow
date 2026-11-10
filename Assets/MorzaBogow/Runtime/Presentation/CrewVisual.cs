using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Men fighting across the rails while two decks are locked in a boarding, armed as on the Warrior Vase of Mycenae:
    /// as many as the front holds, up to eight a side, each in its side's colours. They fight in pairs across the gap,
    /// one thrusting over his shield while the other covers, then the other way; an archer behind each line draws and
    /// looses. When the simulation's front loses men, a man falls on his deck or goes over the side into the sea.
    /// It only reads the boarding's state; the fight itself is the simulation's.
    /// </summary>
    public sealed class DeckFight
    {
        public const int MaxPerSide = 8, MaxFallen = 6;
        readonly CrewFigure[] attackers = new CrewFigure[MaxPerSide], defenders = new CrewFigure[MaxPerSide];
        readonly CrewFigure[] archers = new CrewFigure[2];
        readonly List<(CrewFigure figure, float age, bool overboard, Vector3 from, Vector3 outward)> fallen = new List<(CrewFigure, float, bool, Vector3, Vector3)>();
        readonly Transform parent;
        readonly PlaceholderMaterials materials;
        readonly int attackerOwner, defenderOwner;
        int lastOurs = -1, lastTheirs = -1, falls;
        float time;

        public int Shown { get; private set; }
        public int Fallen => fallen.Count;
        public int Overboard { get; private set; }

        /// <summary>Called where a man falls into the sea, for a splash.</summary>
        public System.Action<Vector3> Splash;

        public DeckFight(Transform parent, PlaceholderMaterials materials, int attackerOwner, int defenderOwner)
        {
            this.parent = parent;
            this.materials = materials;
            this.attackerOwner = attackerOwner;
            this.defenderOwner = defenderOwner;
            for (var i = 0; i < MaxPerSide; i++)
            {
                attackers[i] = new CrewFigure(parent, materials, attackerOwner);
                defenders[i] = new CrewFigure(parent, materials, defenderOwner);
                attackers[i].SetKit(CrewKit.Warrior);
                defenders[i].SetKit(CrewKit.Warrior);
                attackers[i].Show(false);
                defenders[i].Show(false);
            }
            for (var i = 0; i < archers.Length; i++)
            {
                archers[i] = new CrewFigure(parent, materials, i == 0 ? attackerOwner : defenderOwner);
                archers[i].SetKit(CrewKit.Archer);
                archers[i].Show(false);
            }
        }

        /// <summary>One figure for every two men at the front, so a full rail reads as a crowd without a crowd of meshes.</summary>
        public static int FiguresFor(int front) => Mathf.Clamp(Mathf.CeilToInt(front * 0.5f), 0, MaxPerSide);

        /// <summary>
        /// Each side's men line their own rail where the hulls lie together, facing the other deck, spread along the
        /// attacker's length so a full front reads as a line of fighters rather than a knot at one point.
        /// </summary>
        public void Update(ShipVisual attacker, ShipVisual target, int attackerFront, int targetFront, float delta, bool visible)
        {
            time += delta;
            var ours = visible ? FiguresFor(attackerFront) : 0;
            var theirs = visible ? FiguresFor(targetFront) : 0;
            // Men lost at the front fall where they stood; the first step only learns the starting numbers.
            if (visible && lastOurs >= 0) for (var i = ours; i < lastOurs; i++) Fall(attackers[i], attacker);
            if (visible && lastTheirs >= 0) for (var i = theirs; i < lastTheirs; i++) Fall(defenders[i], target);
            lastOurs = ours;
            lastTheirs = theirs;
            var axis = Flat(attacker.transform.forward);
            var spacing = Mathf.Clamp(Mathf.Min(attacker.Length, target.Length) * 0.7f / Mathf.Max(1, Mathf.Max(ours, theirs)), 0.8f, 1.4f);
            Shown = 0;
            Shown += Place(attackers, ours, attacker, target, axis, spacing, 0f);
            Shown += Place(defenders, theirs, target, attacker, axis, spacing, 0.5f);
            PlaceArcher(archers[0], ours > 0, attacker, target, 0f);
            PlaceArcher(archers[1], theirs > 0, target, attacker, 0.4f);
            for (var i = fallen.Count - 1; i >= 0; i--)
            {
                var (figure, age, overboard, from, outward) = fallen[i];
                age += delta;
                fallen[i] = (figure, age, overboard, from, outward);
                if (!visible) { figure.Show(false); continue; }
                figure.Show(true);
                if (!overboard) { figure.Fall(Mathf.Clamp01(age / 0.6f)); continue; }
                // Over the side: a step out past the rail, then down into the sea.
                var t = Mathf.Clamp01(age / 1.2f);
                figure.Place(from + outward * (1.6f * t) + Vector3.down * (from.y + 1.2f) * t * t);
                figure.Fall(Mathf.Clamp01(age / 0.5f) * 0.6f);
                if (age >= 1.2f && age - delta < 1.2f) Splash?.Invoke(from + outward * 1.6f);
                if (age >= 1.6f) figure.Show(false);
            }
        }

        /// <summary>A man of the front struck down: a copy of him lies on the deck, or every third one goes overboard.</summary>
        void Fall(CrewFigure standing, ShipVisual own)
        {
            if (!standing.Root.gameObject.activeSelf) return;
            var body = new CrewFigure(parent, materials, standing == null ? attackerOwner : OwnerOf(standing));
            body.SetKit(CrewKit.Warrior);
            body.Place(standing.Position);
            body.Face(standing.Position + standing.Root.forward);
            var overboard = ++falls % 3 == 0;
            if (overboard) Overboard++;
            fallen.Add((body, 0f, overboard, standing.Position, Flat(standing.Position - own.transform.position)));
            while (fallen.Count > MaxFallen)
            {
                fallen[0].figure.Destroy();
                fallen.RemoveAt(0);
            }
        }

        int OwnerOf(CrewFigure figure) => System.Array.IndexOf(attackers, figure) >= 0 ? attackerOwner : defenderOwner;

        int Place(CrewFigure[] figures, int count, ShipVisual own, ShipVisual other, Vector3 axis, float spacing, float offset)
        {
            var across = other.transform.position;
            for (var i = 0; i < figures.Length; i++)
            {
                var shown = i < count;
                figures[i].Show(shown);
                if (!shown) continue;
                var rail = own.RailNear(across + axis * ((i - (count - 1) * 0.5f) * spacing));
                // One step in from the gunwale, toward the own deck's middle; a step forward while thrusting.
                var inward = Flat(own.transform.position - rail);
                var phase = Mathf.Repeat(time * 0.9f + offset + i * 0.23f, 1f);
                var attacking = phase < 0.5f;
                var local = attacking ? phase / 0.5f : (phase - 0.5f) / 0.5f;
                var step = attacking ? Mathf.Sin(local * Mathf.PI) * 0.3f : 0f;
                var at = rail + inward * (0.5f - step);
                figures[i].Place(at);
                figures[i].Face(at - inward);
                if (attacking) figures[i].Thrust(local);
                else figures[i].Block(Mathf.Sin(local * Mathf.PI));
            }
            return count;
        }

        void PlaceArcher(CrewFigure archer, bool shown, ShipVisual own, ShipVisual other, float offset)
        {
            archer.Show(shown);
            if (!shown) return;
            var rail = own.RailNear(other.transform.position + Flat(own.transform.forward) * 2.5f);
            var inward = Flat(own.transform.position - rail);
            var at = rail + inward * 2.2f;
            archer.Place(at);
            archer.Face(other.transform.position);
            archer.Draw(Mathf.Repeat(time * 0.6f + offset, 1f));
        }

        internal static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 1e-6f ? vector.normalized : Vector3.forward;
        }

        public void Destroy()
        {
            foreach (var figure in attackers) figure.Destroy();
            foreach (var figure in defenders) figure.Destroy();
            foreach (var figure in archers) figure.Destroy();
            foreach (var (figure, _, _, _, _) in fallen) figure.Destroy();
            fallen.Clear();
        }
    }

    /// <summary>
    /// The grapple lines of one boarding, from the throw until the link ends: three ropes from the attacker's rail
    /// to hooks bitten into the other gunwale. While the hooks are fought over the lines hang slack and jerk, and
    /// fewer stand as the defenders cut them; once they hold the lines are taut. Presentation only.
    /// </summary>
    public sealed class GrappleLines
    {
        public const int Lines = 3;
        const int Segments = 3;
        const float Width = 0.18f, RailHeight = 0.8f;
        readonly Transform root;
        readonly Transform[] pieces = new Transform[Lines * Segments];
        readonly Transform[] hooks = new Transform[Lines];
        float time;

        public int Shown { get; private set; }

        public GrappleLines(Transform parent, PlaceholderMaterials materials)
        {
            root = new GameObject("Grapple lines").transform;
            root.SetParent(parent, false);
            for (var i = 0; i < pieces.Length; i++) pieces[i] = CrewFigure.Part(root, PrimitiveType.Cube, materials?.Rope, Vector3.zero, Vector3.one);
            for (var i = 0; i < Lines; i++) hooks[i] = CrewFigure.Part(root, PrimitiveType.Cube, materials?.Bronze, Vector3.zero, new Vector3(0.4f, 0.3f, 0.4f));
        }

        /// <summary>Lines still standing: all three once the hooks hold, else one fewer per third of the cutting done.</summary>
        public static int Standing(bool hooked, double cutProgress) =>
            hooked ? Lines : Mathf.Clamp(Lines - Mathf.FloorToInt((float)cutProgress * Lines), 1, Lines);

        public void Update(ShipVisual attacker, ShipVisual target, int standing, bool hooked, float delta, bool visible)
        {
            time += delta;
            Shown = visible ? Mathf.Clamp(standing, 0, Lines) : 0;
            var axis = DeckFight.Flat(attacker.transform.forward);
            var spacing = Mathf.Min(attacker.Length, target.Length) * 0.2f;
            for (var i = 0; i < Lines; i++)
            {
                var shown = i < Shown;
                for (var s = 0; s < Segments; s++) Show(pieces[i * Segments + s], shown);
                Show(hooks[i], shown);
                if (!shown) continue;
                var from = attacker.RailNear(target.transform.position + axis * ((i - 1) * spacing)) + Vector3.up * RailHeight;
                var to = target.RailNear(from) + Vector3.up * RailHeight;
                var sag = hooked ? 0.2f : 0.7f + 0.3f * Mathf.Sin(time * 7f + i * 1.9f);
                var previous = from;
                for (var s = 1; s <= Segments; s++)
                {
                    var t = (float)s / Segments;
                    var point = s == Segments ? to : Vector3.Lerp(from, to, t) - Vector3.up * (sag * 4f * t * (1f - t));
                    BattleEffects.Stretch(pieces[i * Segments + s - 1], previous, point, Width);
                    previous = point;
                }
                hooks[i].position = to;
                hooks[i].rotation = Quaternion.LookRotation(DeckFight.Flat(to - from));
            }
        }

        static void Show(Transform item, bool shown)
        {
            if (item.gameObject.activeSelf != shown) item.gameObject.SetActive(shown);
        }

        /// <summary>Safe also when the scene is already being torn down.</summary>
        public void Destroy()
        {
            if (root != null) Object.Destroy(root.gameObject);
        }
    }

    public enum CrewTool { None, Axe, Pick }
    public enum CrewLoad { None, Log, Chest, Crate }

    /// <summary>
    /// What a figure wears and carries: a worker or sailor in a plain tunic; a warrior as on the Warrior Vase of Mycenae
    /// (c. 1200 BC) with a horned bronze helmet, a round shield in the side's colour, greaves and a spear; an archer
    /// with a bow and a quiver.
    /// </summary>
    public enum CrewKit { Worker, Warrior, Archer }

    /// <summary>A small figure that walks, swings a tool, carries goods and fights. Presentation only.</summary>
    public sealed class CrewFigure
    {
        public readonly Transform Root;
        readonly Transform legLeft, legRight, arm, axe, pick;
        readonly GameObject log, chest, crate;
        readonly Transform shieldArm, spear, bow;
        readonly GameObject[] armour;
        Quaternion facing = Quaternion.identity;
        float stride;

        public CrewKit Kit { get; private set; }

        public Vector3 Position => Root.position;
        public CrewLoad Carrying { get; private set; }

        public CrewFigure(Transform parent, PlaceholderMaterials materials, int owner)
        {
            Root = new GameObject("Crew").transform;
            Root.SetParent(parent, false);
            var tunic = owner == 0 ? materials?.PlayerSail : materials?.EnemySail;
            var skin = materials?.Rope;

            legLeft = Pivot(Root, new Vector3(-0.12f, 0.85f, 0f));
            legRight = Pivot(Root, new Vector3(0.12f, 0.85f, 0f));
            Part(legLeft, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, -0.42f, 0f), new Vector3(0.17f, 0.82f, 0.2f));
            Part(legRight, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, -0.42f, 0f), new Vector3(0.17f, 0.82f, 0.2f));
            Part(Root, PrimitiveType.Capsule, tunic, new Vector3(0f, 1.27f, 0f), new Vector3(0.5f, 0.45f, 0.32f));
            Part(Root, PrimitiveType.Sphere, skin, new Vector3(0f, 1.86f, 0f), Vector3.one * 0.32f);

            arm = Pivot(Root, new Vector3(0.3f, 1.55f, 0f));
            Part(arm, PrimitiveType.Cube, skin, new Vector3(0f, -0.3f, 0f), new Vector3(0.13f, 0.6f, 0.13f));

            axe = Pivot(arm, new Vector3(0f, -0.6f, 0f));
            Part(axe, PrimitiveType.Cylinder, materials?.Timber, new Vector3(0f, 0f, 0.35f), new Vector3(0.06f, 0.38f, 0.06f), new Vector3(90f, 0f, 0f));
            Part(axe, PrimitiveType.Cube, materials?.Bronze, new Vector3(0f, 0f, 0.72f), new Vector3(0.06f, 0.26f, 0.22f));

            pick = Pivot(arm, new Vector3(0f, -0.6f, 0f));
            Part(pick, PrimitiveType.Cylinder, materials?.Timber, new Vector3(0f, 0f, 0.35f), new Vector3(0.06f, 0.38f, 0.06f), new Vector3(90f, 0f, 0f));
            Part(pick, PrimitiveType.Cube, materials?.SilverOre, new Vector3(0f, 0f, 0.72f), new Vector3(0.07f, 0.55f, 0.08f));

            log = Part(Root, PrimitiveType.Cylinder, materials?.Timber, new Vector3(0f, 2.05f, 0f), new Vector3(0.3f, 0.75f, 0.3f), new Vector3(0f, 0f, 90f)).gameObject;
            chest = Part(Root, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, 2.15f, 0f), new Vector3(0.7f, 0.42f, 0.5f)).gameObject;
            Part(chest.transform, PrimitiveType.Cube, materials?.SilverOre, Vector3.zero, new Vector3(0.18f, 1.05f, 1.05f));
            crate = Part(Root, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, 2.1f, 0f), new Vector3(0.55f, 0.45f, 0.55f)).gameObject;

            // The warrior's kit: horned helmet with a crest, greaves, a round shield on the left arm, a spear in the right
            // hand; an archer's bow and quiver. Bronze and the side's colour, as painted on the Warrior Vase.
            var bronze = materials?.Bronze;
            var paint = materials?.Paint ?? materials?.Rope;

            var parts = new List<GameObject>();
            var helmet = Part(Root, PrimitiveType.Sphere, bronze, new Vector3(0f, 1.95f, 0f), new Vector3(0.37f, 0.27f, 0.39f));
            parts.Add(helmet.gameObject);
            foreach (var side in new[] { -1f, 1f })
                parts.Add(Part(Root, PrimitiveType.Cylinder, paint, new Vector3(side * 0.2f, 2.08f, 0.04f), new Vector3(0.05f, 0.13f, 0.05f), new Vector3(0f, 0f, side * -38f)).gameObject);
            parts.Add(Part(Root, PrimitiveType.Cube, materials?.Ochre ?? tunic, new Vector3(0f, 2.12f, -0.02f), new Vector3(0.06f, 0.14f, 0.36f)).gameObject);
            foreach (var leg in new[] { legLeft, legRight })
                parts.Add(Part(leg, PrimitiveType.Cube, bronze, new Vector3(0f, -0.62f, 0.02f), new Vector3(0.2f, 0.36f, 0.22f)).gameObject);
            shieldArm = Pivot(Root, new Vector3(-0.3f, 1.55f, 0f));
            Part(shieldArm, PrimitiveType.Cube, skin, new Vector3(0f, -0.28f, 0f), new Vector3(0.12f, 0.56f, 0.12f));
            var shield = Part(shieldArm, PrimitiveType.Cylinder, tunic, new Vector3(-0.05f, -0.35f, 0.22f), new Vector3(0.82f, 0.025f, 0.82f), new Vector3(90f, 0f, 0f));
            Part(shield, PrimitiveType.Cylinder, paint, new Vector3(0f, -1.2f, 0f), new Vector3(0.36f, 0.6f, 0.36f));
            // The notch cut in the shield's lower rim.
            Part(shield, PrimitiveType.Cube, materials?.Pitch ?? materials?.Timber, new Vector3(0f, -1.1f, -0.42f), new Vector3(0.22f, 0.5f, 0.16f));
            parts.Add(shieldArm.gameObject);
            spear = Pivot(arm, new Vector3(0f, -0.58f, 0f));
            Part(spear, PrimitiveType.Cylinder, materials?.Timber, new Vector3(0f, 0f, 0.4f), new Vector3(0.05f, 1.25f, 0.05f), new Vector3(90f, 0f, 0f));
            Part(spear, PrimitiveType.Cube, bronze, new Vector3(0f, 0f, 1.72f), new Vector3(0.07f, 0.04f, 0.26f), new Vector3(0f, 0f, 45f));
            parts.Add(spear.gameObject);
            bow = Pivot(Root, new Vector3(-0.32f, 1.5f, 0.25f));
            Part(bow, PrimitiveType.Cube, materials?.Ochre ?? materials?.Timber, new Vector3(0f, 0.32f, -0.08f), new Vector3(0.05f, 0.55f, 0.05f), new Vector3(-18f, 0f, 0f));
            Part(bow, PrimitiveType.Cube, materials?.Ochre ?? materials?.Timber, new Vector3(0f, -0.32f, -0.08f), new Vector3(0.05f, 0.55f, 0.05f), new Vector3(18f, 0f, 0f));
            Part(Root, PrimitiveType.Cylinder, materials?.Ochre ?? materials?.Timber, new Vector3(0.15f, 1.4f, -0.22f), new Vector3(0.14f, 0.3f, 0.14f), new Vector3(15f, 0f, 0f)).gameObject.transform.SetParent(bow, true);
            parts.Add(bow.gameObject);
            armour = parts.ToArray();
            SetTool(CrewTool.None);
            SetLoad(CrewLoad.None);
            SetKit(CrewKit.Worker);
        }

        public void SetKit(CrewKit kit)
        {
            Kit = kit;
            var armed = kit != CrewKit.Worker;
            foreach (var part in armour) if (part.activeSelf != armed) part.SetActive(armed);
            spear.gameObject.SetActive(kit == CrewKit.Warrior);
            shieldArm.gameObject.SetActive(kit == CrewKit.Warrior);
            bow.gameObject.SetActive(kit == CrewKit.Archer);
            if (armed) SetTool(CrewTool.None);
        }

        /// <summary>A spear thrust over the shield: draw back, lunge, recover. Phase 0 to 1 per thrust.</summary>
        public void Thrust(float phase)
        {
            var lunge = phase < 0.45f ? Mathf.Lerp(0f, -0.4f, phase / 0.45f) : phase < 0.65f ? Mathf.Lerp(-0.4f, 1f, (phase - 0.45f) / 0.2f) : Mathf.Lerp(1f, 0f, (phase - 0.65f) / 0.35f);
            arm.localRotation = Quaternion.Euler(-80f - lunge * 12f, 0f, 0f);
            spear.localRotation = Quaternion.Euler(80f - lunge * 6f, 0f, 0f);
            legLeft.localRotation = Quaternion.Euler(-14f - lunge * 12f, 0f, 0f);
            legRight.localRotation = Quaternion.Euler(10f + lunge * 10f, 0f, 0f);
            Root.rotation = facing * Quaternion.Euler(lunge * 8f, 0f, 0f);
            shieldArm.localRotation = Quaternion.Euler(-40f, 0f, 10f);
        }

        /// <summary>The shield raised against a blow, 0 lowered to 1 high.</summary>
        public void Block(float amount)
        {
            shieldArm.localRotation = Quaternion.Euler(Mathf.Lerp(-40f, -95f, amount), 0f, Mathf.Lerp(10f, 25f, amount));
            arm.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            spear.localRotation = Quaternion.Euler(70f, 0f, 0f);
            Root.rotation = facing * Quaternion.Euler(-amount * 5f, 0f, 0f);
        }

        /// <summary>An archer draws and looses: bow arm out, string hand back to the cheek. Phase 0 to 1 per arrow.</summary>
        public void Draw(float phase)
        {
            var pull = phase < 0.7f ? phase / 0.7f : 1f - (phase - 0.7f) / 0.3f;
            bow.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            arm.localRotation = Quaternion.Euler(-80f, 0f, Mathf.Lerp(0f, 40f, pull));
            legLeft.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            legRight.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Root.rotation = facing;
        }

        /// <summary>Struck down: falls back onto the deck (0 standing, 1 lying).</summary>
        public void Fall(float amount)
        {
            Root.rotation = facing * Quaternion.Euler(-88f * Mathf.SmoothStep(0f, 1f, amount), 0f, 0f);
            arm.localRotation = Quaternion.Euler(-150f * amount, 0f, 20f);
            legLeft.localRotation = Quaternion.Euler(10f * amount, 0f, 0f);
            legRight.localRotation = Quaternion.Euler(-10f * amount, 0f, 0f);
        }

        public CrewTool Tool { get; private set; }

        public void SetTool(CrewTool tool)
        {
            Tool = tool;
            axe.gameObject.SetActive(tool == CrewTool.Axe);
            pick.gameObject.SetActive(tool == CrewTool.Pick);
        }

        public void SetLoad(CrewLoad load)
        {
            Carrying = load;
            log.SetActive(load == CrewLoad.Log);
            chest.SetActive(load == CrewLoad.Chest);
            crate.SetActive(load == CrewLoad.Crate);
        }

        public void Place(Vector3 at) => Root.position = at;

        /// <summary>Walks toward a point at a speed; legs swing with the distance covered. True on arrival.</summary>
        public bool WalkTo(Vector3 target, float speed, float delta)
        {
            var offset = target - Root.position;
            var distance = offset.magnitude;
            var step = speed * delta;
            if (distance <= step || distance < 0.05f)
            {
                Root.position = target;
                Stand();
                return true;
            }
            var flat = new Vector3(offset.x, 0f, offset.z);
            if (flat.sqrMagnitude > 1e-4f) Root.rotation = facing = Quaternion.LookRotation(flat);
            Root.position += offset / distance * step;

            stride += step * 2.6f;
            var swing = Mathf.Sin(stride) * 32f;
            legLeft.localRotation = Quaternion.Euler(swing, 0f, 0f);
            legRight.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            arm.localRotation = Carrying == CrewLoad.None ? Quaternion.Euler(-swing * 0.6f, 0f, 0f) : Quaternion.Euler(-160f, 0f, 0f);
            return false;
        }

        public void Face(Vector3 point)
        {
            var flat = point - Root.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 1e-4f) Root.rotation = facing = Quaternion.LookRotation(flat);
        }

        public void Stand()
        {
            legLeft.localRotation = Quaternion.identity;
            legRight.localRotation = Quaternion.identity;
            arm.localRotation = Carrying == CrewLoad.None ? Quaternion.identity : Quaternion.Euler(-160f, 0f, 0f);
        }

        /// <summary>A two-handed overhead blow: wind up slowly, strike fast. Phase runs 0 to 1 per blow.</summary>
        public void Strike(float phase)
        {
            legLeft.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            legRight.localRotation = Quaternion.Euler(8f, 0f, 0f);
            var angle = phase < 0.7f ? Mathf.Lerp(-20f, -170f, phase / 0.7f) : Mathf.Lerp(-170f, 30f, (phase - 0.7f) / 0.3f);
            arm.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        public void Show(bool shown)
        {
            if (Root.gameObject.activeSelf != shown) Root.gameObject.SetActive(shown);
        }

        /// <summary>Safe also when the scene is already being torn down and the figure is gone.</summary>
        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
        }

        static Transform Pivot(Transform parent, Vector3 position)
        {
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            return pivot;
        }

        internal static Transform Part(Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Vector3 rotation = default)
        {
            var part = GameObject.CreatePrimitive(type);
            var collider = part.GetComponent<Collider>();
            if (collider != null) { if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider); }
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(rotation);
            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material != null) renderer.sharedMaterial = material;
            return part.transform;
        }
    }

    /// <summary>Where a crew works ashore: the jetty end at the hull, the foot of the jetty, a pile and work spots.</summary>
    public sealed class Workplace
    {
        public Vector3 JettyEnd, Landing, Pile;
        public ResourceSiteVisual Site;
        public CrewLoad Goods;
    }

    public enum CrewJob { None, Wood, Silver, Unload, Load }

    /// <summary>
    /// A ship's shore party. When a cutter takes a berth, its crew walks the plank onto the jetty: woodcutters fell
    /// trees while others haul logs aboard, or miners break ore at the mine while others carry sealed chests. In
    /// port they carry the cargo from the hold along the jetty into the store, or from the store into the hold. When
    /// the ship casts off they hurry back aboard. It reads the simulation only to choose the job. The men are state
    /// only (ShoreMan), drawn with the crews' bodies by CrewDirector (CrewAshore); the plank is the party's one object.
    /// </summary>
    public sealed class ShipCrew
    {
        public const float WalkSpeed = 3.2f, CarrySpeed = 2.6f, HurrySpeed = 5f, StrikeSeconds = 0.9f, StrikesPerTree = 4f;

        enum Point { Deck, Jetty, Landing, Pile, Work }
        enum Arrival { None, Pick, Drop, StartWork, Aboard }

        sealed class Member
        {
            public ShoreMan Figure;
            public bool Hauler;
            public readonly List<Point> Route = new List<Point>();
            public Arrival AtEnd;
            public bool Working, Aboard = true;
            public float Wait, Timer;
            public int Spot;
            public Point LastReached = Point.Deck;
        }

        readonly List<Member> members = new List<Member>();
        readonly List<ShoreMan> men = new List<ShoreMan>();
        readonly Transform plank;
        CrewJob job;
        Workplace place;

        public CrewJob Job => job;
        public int Ashore { get; private set; }
        public int Carrying { get; private set; }
        public int Working { get; private set; }
        public int Delivered { get; private set; }
        public int Felled { get; private set; }

        /// <summary>The party's men in order, as drawn (CrewAshore); man i is the party's i-th.</summary>
        public IReadOnlyList<ShoreMan> Men => men;

        /// <summary>The side the party belongs to.</summary>
        public int Owner { get; }

        /// <summary>The men off the ship now, bit i for man i (CrewAboard.PartyAshore leaves them out on deck).</summary>
        public int AshoreMask { get; private set; }

        public ShipCrew(Transform parent, PlaceholderMaterials materials, int owner, int size, int seed)
        {
            Owner = owner;
            for (var i = 0; i < size; i++)
            {
                var figure = new ShoreMan(i);
                men.Add(figure);
                members.Add(new Member { Figure = figure, Hauler = i % 2 == 1, Spot = seed + i * 3 });
            }
            plank = CrewFigure.Part(parent, PrimitiveType.Cube, materials?.Timber, Vector3.zero, Vector3.one);
            plank.name = "Gangplank";
            plank.gameObject.SetActive(false);
        }

        public bool AllAboard
        {
            get
            {
                foreach (var member in members) if (!member.Aboard) return false;
                return true;
            }
        }

        /// <summary>Changes what the crew does. Members ashore walk back aboard when the job ends.</summary>
        public void Assign(CrewJob next, Workplace workplace)
        {
            if (next == job && (workplace == null || workplace == place)) return;
            job = next;
            if (workplace != null) place = workplace;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                member.Working = false;
                if (job == CrewJob.None)
                {
                    if (!member.Aboard) GoAboard(member);
                    continue;
                }
                if (member.Aboard) member.Wait = i * 0.35f;
                Plan(member);
            }
        }

        /// <summary>The next leg of a member's work, from wherever they now are.</summary>
        void Plan(Member member)
        {
            var figure = member.Figure;
            member.Route.Clear();
            var loaded = figure.Load != CrewLoad.None;
            switch (job)
            {
                case CrewJob.Wood:
                case CrewJob.Silver:
                    figure.SetTool(member.Hauler ? CrewTool.None : job == CrewJob.Wood ? CrewTool.Axe : CrewTool.Pick);
                    if (!member.Hauler) { Walk(member, Point.Work); member.AtEnd = Arrival.StartWork; }
                    else if (loaded) { Walk(member, Point.Deck); member.AtEnd = Arrival.Drop; }
                    else { Walk(member, Point.Pile); member.AtEnd = Arrival.Pick; }
                    break;
                case CrewJob.Unload:
                    figure.SetTool(CrewTool.None);

                    // The first load comes straight out of the hold as they step off.
                    if (!loaded && member.Aboard) { figure.SetLoad(place.Goods); loaded = true; }
                    if (loaded) { Walk(member, Point.Pile); member.AtEnd = Arrival.Drop; }
                    else { Walk(member, Point.Deck); member.AtEnd = Arrival.Pick; }
                    break;
                case CrewJob.Load:
                    figure.SetTool(CrewTool.None);
                    if (loaded) { Walk(member, Point.Deck); member.AtEnd = Arrival.Drop; }
                    else { Walk(member, Point.Pile); member.AtEnd = Arrival.Pick; }
                    break;
            }
        }

        void GoAboard(Member member)
        {
            member.Working = false;
            member.Figure.SetTool(CrewTool.None);
            member.Route.Clear();
            Walk(member, Point.Deck);
            member.AtEnd = Arrival.Aboard;
        }

        /// <summary>Routes always follow the plank, the jetty and the foot of the jetty: nobody walks on water.</summary>
        static void Walk(Member member, Point goal)
        {
            var chain = new[] { Point.Deck, Point.Jetty, Point.Landing };
            var from = member.Aboard ? 0 : member.LastReached == Point.Pile || member.LastReached == Point.Work ? 3 : System.Array.IndexOf(chain, member.LastReached);
            var to = goal == Point.Deck ? 0 : 3;
            if (from < to) for (var i = from + 1; i <= 2; i++) member.Route.Add(chain[i]);
            else if (from > to) for (var i = System.Math.Min(from, 3) - 1; i >= 0; i--) member.Route.Add(chain[i]);
            if (goal != Point.Deck) member.Route.Add(goal);
        }

        /// <summary>
        /// Seconds of game time the slowest member ashore needs to hurry back aboard along the jetty: those beyond
        /// its foot walk to it first. Zero when everyone is aboard.
        /// </summary>
        public float SecondsToBoard(Vector3 deck)
        {
            if (place == null) return 0f;
            var longest = 0f;
            foreach (var member in members)
            {
                if (member.Aboard) continue;
                var at = member.Figure.Position;
                var inland = member.LastReached == Point.Pile || member.LastReached == Point.Work || member.LastReached == Point.Landing;
                var path = inland
                    ? Vector3.Distance(at, place.Landing) + Vector3.Distance(place.Landing, place.JettyEnd)
                    : Vector3.Distance(at, place.JettyEnd);
                path += Vector3.Distance(place.JettyEnd, deck);
                longest = Mathf.Max(longest, path / HurrySpeed);
            }
            return longest;
        }

        /// <summary>Trips left for a dock gang; each carried load counts one. Negative means no limit.</summary>
        public int TripsLeft { get; set; } = -1;

        public Vector3 JettyEnd => place != null ? place.JettyEnd : Vector3.zero;

        /// <summary>
        /// Advances the shore party; delta is game time, so pause stops them. The deck is where they board: a hull's
        /// gunwale, or for a dock gang the jetty end where goods are set down. Shown only where the player can see.
        /// </summary>
        public void Update(Vector3 deck, float delta, bool visible)
        {
            Ashore = Carrying = Working = 0;
            AshoreMask = 0;
            if (place == null) { HideAll(); return; }

            foreach (var member in members)
            {
                if (member.Aboard)
                {
                    if (job == CrewJob.None || member.Route.Count == 0) { member.Figure.Show(false); continue; }
                    if (member.Wait > 0f) { member.Wait -= delta; member.Figure.Show(false); continue; }
                    member.Aboard = false;
                    member.LastReached = Point.Deck;
                    member.Figure.Place(deck);
                }

                Advance(member, deck, delta);
                if (member.Aboard) { member.Figure.Show(false); continue; }
                Ashore++;
                AshoreMask |= 1 << member.Figure.Index;
                if (member.Figure.Load != CrewLoad.None) Carrying++;
                if (member.Working) Working++;
                member.Figure.Show(visible);
            }

            // The plank lies from the gunwale to the jetty while anyone is ashore.
            var span = place.JettyEnd - deck;
            var shown = Ashore > 0 && visible && span.magnitude < 16f;
            if (plank.gameObject.activeSelf != shown) plank.gameObject.SetActive(shown);
            if (!shown) return;
            plank.position = (deck + place.JettyEnd) * 0.5f;
            plank.rotation = Quaternion.LookRotation(span);
            plank.localScale = new Vector3(0.7f, 0.08f, span.magnitude);
        }

        Vector3 Where(Point point, Member member, Vector3 deck)
        {
            switch (point)
            {
                case Point.Deck: return deck;
                case Point.Jetty: return place.JettyEnd;
                case Point.Landing: return place.Landing;
                case Point.Work: return place.Site != null && place.Site.WorkSpotCount > 0 ? place.Site.WorkSpot(member.Spot) : place.Pile;
                default: return place.Pile + new Vector3((member.Spot % 3 - 1) * 1.2f, 0f, 0f);
            }
        }

        void Advance(Member member, Vector3 deck, float delta)
        {
            var figure = member.Figure;

            // A hull pulling away leaves no one standing on the quay: those left behind count as having jumped aboard.
            if (job == CrewJob.None && Vector3.Distance(figure.Position, deck) > 25f && Vector3.Distance(deck, place.JettyEnd) > 20f)
            {
                member.Aboard = true;
                figure.SetLoad(CrewLoad.None);
                return;
            }
            if (member.Wait > 0f) { member.Wait -= delta; figure.Stand(); return; }
            if (member.Working) { Work(member, delta); return; }

            if (member.Route.Count > 0)
            {
                var speed = member.AtEnd == Arrival.Aboard && job == CrewJob.None ? HurrySpeed : figure.Load != CrewLoad.None ? CarrySpeed : WalkSpeed;
                if (!figure.WalkTo(Where(member.Route[0], member, deck), speed, delta)) return;
                member.LastReached = member.Route[0];
                member.Route.RemoveAt(0);
                if (member.Route.Count > 0) return;
            }

            switch (member.AtEnd)
            {
                case Arrival.Pick:
                    figure.SetLoad(place.Goods);
                    member.Wait = 0.5f;
                    break;
                case Arrival.Drop:
                    figure.SetLoad(CrewLoad.None);
                    Delivered++;
                    member.Wait = 0.4f;
                    if (TripsLeft > 0 && --TripsLeft == 0) { Assign(CrewJob.None, null); return; }
                    break;
                case Arrival.StartWork:
                    member.Working = true;
                    member.Timer = 0f;
                    return;
                case Arrival.Aboard:
                    member.Aboard = true;
                    figure.SetLoad(CrewLoad.None);
                    return;
            }
            if (job == CrewJob.None) GoAboard(member);
            else Plan(member);
        }

        /// <summary>Felling: blows against the trunk until the tree leans and falls, then on to the next. Mining: steady blows at the face.</summary>
        void Work(Member member, float delta)
        {
            var figure = member.Figure;
            var site = place.Site;
            if (site == null) { member.Working = false; Plan(member); return; }

            var spot = site.WorkSpot(member.Spot);
            figure.Face(spot + (site.Kind == ResourceType.Wood ? new Vector3(0f, 0f, 1.6f) : new Vector3(0f, 1f, 1.2f)));
            member.Timer += delta;
            figure.Strike(member.Timer / StrikeSeconds % 1f);
            site.Worked(member.Spot, (member.Timer - delta) / StrikeSeconds, member.Timer / StrikeSeconds);
            if (site.Kind != ResourceType.Wood) return;

            site.Fell(member.Spot, member.Timer / (StrikeSeconds * StrikesPerTree) * 0.8f);
            if (member.Timer < StrikeSeconds * StrikesPerTree) return;

            // The last blow brings it down; the woodcutter steps back and moves on to another standing tree.
            site.Fell(member.Spot, 1f);
            Felled++;
            for (var tries = 0; tries < site.WorkSpotCount && site.IsFelled(member.Spot); tries++) member.Spot += 2;
            member.Working = false;
            member.LastReached = Point.Work;
            figure.Stand();
            Plan(member);
        }

        void HideAll()
        {
            foreach (var member in members) member.Figure.Show(false);
            if (plank.gameObject.activeSelf) plank.gameObject.SetActive(false);
        }

        /// <summary>The men leave nothing in the scene; only the plank goes. Safe also when the scene is being torn down.</summary>
        public void Destroy()
        {
            foreach (var member in members) member.Figure.Show(false);
            if (plank != null) Object.Destroy(plank.gameObject);
        }
    }
}
