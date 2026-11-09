using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    public enum CrewTool { None, Axe, Pick }
    public enum CrewLoad { None, Log, Chest, Crate }

    /// <summary>A small placeholder figure that walks, swings a tool and carries goods. Presentation only.</summary>
    public sealed class CrewFigure
    {
        public readonly Transform Root;
        readonly Transform legLeft, legRight, arm, axe, pick;
        readonly GameObject log, chest, crate;
        float stride;

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
            SetTool(CrewTool.None);
            SetLoad(CrewLoad.None);
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
            if (flat.sqrMagnitude > 1e-4f) Root.rotation = Quaternion.LookRotation(flat);
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
            if (flat.sqrMagnitude > 1e-4f) Root.rotation = Quaternion.LookRotation(flat);
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
    /// the ship casts off they hurry back aboard. It reads the simulation only to choose the job.
    /// </summary>
    public sealed class ShipCrew
    {
        public const float WalkSpeed = 3.2f, CarrySpeed = 2.6f, HurrySpeed = 5f, StrikeSeconds = 0.9f, StrikesPerTree = 4f;

        enum Point { Deck, Jetty, Landing, Pile, Work }
        enum Arrival { None, Pick, Drop, StartWork, Aboard }

        sealed class Member
        {
            public CrewFigure Figure;
            public bool Hauler;
            public readonly List<Point> Route = new List<Point>();
            public Arrival AtEnd;
            public bool Working, Aboard = true;
            public float Wait, Timer;
            public int Spot;
            public Point LastReached = Point.Deck;
        }

        readonly List<Member> members = new List<Member>();
        readonly Transform plank;
        CrewJob job;
        Workplace place;

        public CrewJob Job => job;
        public int Ashore { get; private set; }
        public int Carrying { get; private set; }
        public int Working { get; private set; }
        public int Delivered { get; private set; }
        public int Felled { get; private set; }

        public ShipCrew(Transform parent, PlaceholderMaterials materials, int owner, int size, int seed)
        {
            for (var i = 0; i < size; i++)
            {
                var figure = new CrewFigure(parent, materials, owner);
                figure.Show(false);
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
            var loaded = figure.Carrying != CrewLoad.None;
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
                if (member.Figure.Carrying != CrewLoad.None) Carrying++;
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
                var speed = member.AtEnd == Arrival.Aboard && job == CrewJob.None ? HurrySpeed : figure.Carrying != CrewLoad.None ? CarrySpeed : WalkSpeed;
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

        public void Destroy()
        {
            foreach (var member in members) member.Figure.Destroy();
            if (plank != null) Object.Destroy(plank.gameObject);
        }
    }
}
