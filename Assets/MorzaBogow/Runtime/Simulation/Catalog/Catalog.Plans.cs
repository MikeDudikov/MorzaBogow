using System;
using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation.Catalog
{
    /// <summary>The five spatial tasks a unit may be given (docs/KATALOG_MECHANIK.md §5, A01-A05).</summary>
    public enum TaskKind { EscortGroup, ScoutAndReturn, Patrol, DefendArea, AttackInArea }

    /// <summary>
    /// The four permissions of a unit acting on its own: to answer with fire in defence, to ram, to board, and to fire
    /// back at whoever attacks it. Turning fire defence off stops the automatic answer, not a manual, legal order to shoot.
    /// </summary>
    [Flags]
    public enum AutonomyFlags { None = 0, FireDefence = 1, AllowRam = 2, AllowBoard = 4, ReturnFire = 8, All = 15 }

    /// <summary>What a task of a kind is and where it ends.</summary>
    public sealed class TaskDefinition
    {
        public string Id, Name, EndCondition;
        public TaskKind Kind;

        /// <summary>The task needs ships to guard, a route of points, an area, a place to come back to.</summary>
        public bool NeedsEscorted, NeedsRoute, NeedsArea, NeedsReturn;

        /// <summary>The least number of points of the route, where one is needed.</summary>
        public int LeastRoutePoints;
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("Task", Id).F("Name", Name).F("EndCondition", EndCondition).F("Kind", Kind.ToString()).F("NeedsEscorted", NeedsEscorted)
                .F("NeedsRoute", NeedsRoute).F("NeedsArea", NeedsArea).F("NeedsReturn", NeedsReturn).F("LeastRoutePoints", LeastRoutePoints);
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>
    /// One task of one unit as it will be kept and saved with its settings, route and place of return (P03). Data only:
    /// nothing in the running game reads it yet.
    /// </summary>
    public sealed class TaskRecord
    {
        /// <summary>A unit's queue holds at most this many tasks; one more is refused before any payment (docs/PARAMETRY_ROBOCZE.md §7).</summary>
        public const int QueueLimit = 16;

        public TaskKind Kind;
        public int UnitId = -1;
        public int[] EscortedIds = Array.Empty<int>();

        /// <summary>The route as x, y pairs in order, and the point the unit is making for.</summary>
        public double[] RouteX = Array.Empty<double>(), RouteY = Array.Empty<double>();
        public int RouteIndex;

        /// <summary>The area as a rectangle.</summary>
        public bool HasArea;
        public double AreaMinX, AreaMinY, AreaMaxX, AreaMaxY;

        public bool HasReturn;
        public double ReturnX, ReturnY;
        public AutonomyFlags Flags = AutonomyFlags.All;
    }

    public static class TaskRules
    {
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>What is wrong with a task's data; empty when it may be queued.</summary>
        public static List<string> Problems(TaskRecord task, TaskDefinition definition)
        {
            var problems = new List<string>();
            if (definition == null || definition.Kind != task.Kind) { problems.Add("No definition of this kind of task."); return problems; }
            if (task.UnitId < 0) problems.Add("No unit.");
            if ((task.Flags & ~AutonomyFlags.All) != 0) problems.Add("A permission that is not.");
            var escorted = task.EscortedIds ?? Array.Empty<int>();
            if (definition.NeedsEscorted && escorted.Length == 0) problems.Add("Nobody to guard.");
            if (escorted.Any(id => id < 0) || escorted.Distinct().Count() != escorted.Length || escorted.Contains(task.UnitId)) problems.Add("The guarded ships are not distinct real ships other than the unit.");
            var routeX = task.RouteX ?? Array.Empty<double>();
            var routeY = task.RouteY ?? Array.Empty<double>();
            if (routeX.Length != routeY.Length) problems.Add("The route's points are not pairs.");
            else
            {
                if (definition.NeedsRoute && routeX.Length < definition.LeastRoutePoints) problems.Add("The route is too short.");
                if (routeX.Any(value => !Finite(value)) || routeY.Any(value => !Finite(value))) problems.Add("A point of the route is no number.");
                if (routeX.Length > 0 && (task.RouteIndex < 0 || task.RouteIndex >= routeX.Length)) problems.Add("The point made for is not on the route.");
                if (routeX.Length == 0 && task.RouteIndex != 0) problems.Add("The point made for is not on the route.");
            }
            if (definition.NeedsArea && !task.HasArea) problems.Add("No area.");
            if (task.HasArea && !(Finite(task.AreaMinX) && Finite(task.AreaMinY) && Finite(task.AreaMaxX) && Finite(task.AreaMaxY) && task.AreaMinX < task.AreaMaxX && task.AreaMinY < task.AreaMaxY))
                problems.Add("The area is empty or no rectangle.");
            if (definition.NeedsReturn && !task.HasReturn) problems.Add("No place to come back to.");
            if (task.HasReturn && !(Finite(task.ReturnX) && Finite(task.ReturnY))) problems.Add("The place to come back to is no number.");
            return problems;
        }
    }

    /// <summary>
    /// How the opponent decides at one level of difficulty (docs/NOWY_PLAN_GRY.md §10, J03). A profile holds decisions
    /// only: how often it plans and reacts, how large a group it waits for, how much risk it takes. It has no field for a
    /// price, a statistic, a reward or what the opponent knows; those are the same for every profile and for the human.
    /// </summary>
    public sealed class AiProfile
    {
        public string Id, Name;

        /// <summary>The least ticks between two decisions of the fleet's planner, and between two of the economy's.</summary>
        public int ReactionTicks, StrategyTicks;

        /// <summary>Added to the attack group the scenario names before the opponent sails; 0 keeps it.</summary>
        public int AttackGroupOffset;

        /// <summary>The share of a crew's fighters below which a ship is called back to regroup.</summary>
        public double RegroupCrewShare;

        /// <summary>It boards only at odds strictly above this; at exactly this it does not.</summary>
        public double BoardingOddsAbove;

        /// <summary>The share of its hull below which a ship is sent to the dock, and how near the home port an enemy seen counts as a threat to it, in metres.</summary>
        public double HoledHullShare, HomeThreatMeters;

        /// <summary>The profile is how the opponent plays today.</summary>
        public bool IsRunningBehaviour;
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("AiProfile", Id).F("Name", Name).F("ReactionTicks", ReactionTicks).F("StrategyTicks", StrategyTicks).F("AttackGroupOffset", AttackGroupOffset)
                .F("RegroupCrewShare", RegroupCrewShare).F("BoardingOddsAbove", BoardingOddsAbove).F("HoledHullShare", HoledHullShare).F("HomeThreatMeters", HomeThreatMeters).F("IsRunningBehaviour", IsRunningBehaviour);
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    public static class AiProfileRules
    {
        public static List<string> Problems(AiProfile profile)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(profile.Id)) problems.Add("No ID.");
            if (profile.ReactionTicks <= 0 || profile.StrategyTicks <= 0) problems.Add("The opponent must wait at least a tick between decisions.");
            if (profile.RegroupCrewShare < 0 || profile.RegroupCrewShare > 1 || double.IsNaN(profile.RegroupCrewShare)) problems.Add("The regroup share is a share of the crew.");
            if (!(profile.BoardingOddsAbove > 0) || double.IsInfinity(profile.BoardingOddsAbove)) problems.Add("The boarding odds asked must be a positive number.");
            if (profile.HoledHullShare < 0 || profile.HoledHullShare > 1 || double.IsNaN(profile.HoledHullShare)) problems.Add("The holed-hull share is a share of the hull.");
            if (!(profile.HomeThreatMeters > 0) || double.IsInfinity(profile.HomeThreatMeters)) problems.Add("The home threat radius must be a positive distance.");
            return problems;
        }
    }

    /// <summary>What sets a wave of reinforcements off. Every kind is something the briefing can state; none measures how well the player is doing.</summary>
    public enum ReinforcementTrigger { AtTick, ObjectiveReached, ZoneEntered, SourceCaptured }

    public sealed class ManifestUnit
    {
        public string VariantId;
        public int Count;
    }

    /// <summary>
    /// A finite wave of reinforcements (docs/NOWY_PLAN_GRY.md §10, J03): where it comes from, what it holds, how many
    /// times at most, what sets it off and how long before it is announced. Paid for in the scenario's own accounts, and
    /// never set off by a hidden measure of the player's success.
    /// </summary>
    public sealed class ReinforcementManifest
    {
        public string Id, ScenarioId;

        /// <summary>The port, fort or edge of the map the wave comes from.</summary>
        public string SourceId;
        public int OwnerSlot = Owners.MainAi;
        public ManifestUnit[] Units = Array.Empty<ManifestUnit>();

        /// <summary>The wave comes at most this many times.</summary>
        public int Limit = 1;
        public ReinforcementTrigger Trigger;

        /// <summary>The tick, the objective or the zone the trigger names.</summary>
        public string TriggerArg;
        public double AnnounceSeconds;

        /// <summary>What the wave cost, in units, in the accounts of the scenario.</summary>
        public int PaidWood, PaidSilver;
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("Reinforcement", Id).F("ScenarioId", ScenarioId).F("SourceId", SourceId).F("OwnerSlot", OwnerSlot)
                .F("Units", Units?.Select(unit => unit == null ? null : unit.VariantId + "x" + unit.Count)).F("Limit", Limit).F("Trigger", Trigger.ToString()).F("TriggerArg", TriggerArg)
                .F("AnnounceSeconds", AnnounceSeconds).F("PaidWood", PaidWood).F("PaidSilver", PaidSilver);
            (Source ?? new SourceNote()).Write(writer, "Source");
            writer.End();
        }
    }

    public static class ReinforcementRules
    {
        /// <summary>The most ships one wave may hold, and the most times it may come: a manifest is always finite.</summary>
        public const int LargestWave = 64, LargestLimit = 16;

        /// <summary>What is wrong with a manifest; empty when it is finite, sourced, announced and names real roles.</summary>
        public static List<string> Problems(ReinforcementManifest manifest, Func<string, bool> variantExists)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(manifest.Id)) problems.Add("No ID.");
            if (string.IsNullOrWhiteSpace(manifest.SourceId)) problems.Add("No source.");
            if (!Owners.IsValid(manifest.OwnerSlot)) problems.Add("An owner slot that is not.");
            var units = manifest.Units ?? Array.Empty<ManifestUnit>();
            if (units.Length == 0) problems.Add("An empty wave.");
            if (units.Any(unit => unit == null || unit.Count <= 0)) problems.Add("Every entry holds one ship or more.");
            else if (units.Sum(unit => (long)unit.Count) > LargestWave) problems.Add("The wave is larger than a wave may be.");
            if (units.Any(unit => unit != null && (string.IsNullOrWhiteSpace(unit.VariantId) || !variantExists(unit.VariantId)))) problems.Add("An entry names a role that is not in the catalogue.");
            if (manifest.Limit < 1 || manifest.Limit > LargestLimit) problems.Add("The wave comes a finite number of times, at least once.");
            if (!Enum.IsDefined(typeof(ReinforcementTrigger), manifest.Trigger)) problems.Add("An unknown trigger.");
            if (string.IsNullOrWhiteSpace(manifest.TriggerArg)) problems.Add("The trigger names nothing.");
            if (manifest.Trigger == ReinforcementTrigger.AtTick && !(long.TryParse(manifest.TriggerArg, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var tick) && tick >= 0))
                problems.Add("A wave set off by time names a tick.");
            if (!(manifest.AnnounceSeconds > 0) || double.IsInfinity(manifest.AnnounceSeconds)) problems.Add("Every wave is announced beforehand.");
            if (manifest.PaidWood < 0 || manifest.PaidSilver < 0) problems.Add("A price is never negative.");
            if (manifest.Source == null || string.IsNullOrWhiteSpace(manifest.Source.Locator)) problems.Add("No source note with a locator.");
            return problems;
        }
    }

    static partial class CatalogData
    {
        internal static List<TaskDefinition> Tasks()
        {
            var source = Rule(CatalogDoc + " §5");
            TaskDefinition Task(string id, string name, TaskKind kind, string end, bool escorted = false, bool route = false, int points = 0, bool area = false, bool back = false) =>
                new TaskDefinition { Id = id, Name = name, Kind = kind, EndCondition = end, NeedsEscorted = escorted, NeedsRoute = route, LeastRoutePoints = points, NeedsArea = area, NeedsReturn = back,
                    Source = new SourceNote { Status = source.Status, Layer = source.Layer, Locator = source.Locator } };
            return new List<TaskDefinition>
            {
                Task("A01", "Eskorta grupy", TaskKind.EscortGroup, "Osłania wskazane jednostki; nie ściga bez końca poza strefą ochrony.", escorted: true),
                Task("A02", "Zwiad z powrotem", TaskKind.ScoutAndReturn, "Dopływa do wskazanego obszaru i wraca do pozycji wyjściowej; zaatakowany może przerwać i wrócić.", route: true, points: 1, back: true),
                Task("A03", "Patrol", TaskKind.Patrol, "Cykliczna lista punktów z jawną kolejnością i polityką reakcji.", route: true, points: 2),
                Task("A04", "Obrona obszaru", TaskKind.DefendArea, "Pozostaje w zadanym obszarze, reaguje na wykryte cele.", area: true),
                Task("A05", "Atak w obszarze", TaskKind.AttackInArea, "Wybiera legalne cele w strefie według wyposażenia i doktryny.", area: true),
            };
        }

        internal static List<AiProfile> AiProfiles()
        {
            // Standard is the opponent as it plays today: it plans its fleet every 30 ticks and its economy every 90, sends
            // a ship to regroup below half its fighters, boards at odds strictly above 1.2, docks a hull below 0.3 and counts an
            // enemy within 250 m of home as a threat (Opponent.cs). The parameters document
            // fixes the reaction of the other two; their other decisions are kept at Standard until trials say otherwise.
            AiProfile Profile(string id, string name, int reaction, bool running) => new AiProfile
            {
                Id = id, Name = name, ReactionTicks = reaction, StrategyTicks = 3 * Rules.TickRate, AttackGroupOffset = 0, RegroupCrewShare = 0.5, BoardingOddsAbove = 1.2, HoledHullShare = 0.3, HomeThreatMeters = 250,
                IsRunningBehaviour = running, Source = Rule(ParametersDoc + " §7; docs/GRYWALNOSC_I_ODBIOR.md J03"),
            };
            return new List<AiProfile> { Profile("AI-NAUKA", "Nauka", 90, false), Profile("AI-STANDARD", "Standard", 30, true), Profile("AI-WETERAN", "Weteran", 15, false) };
        }
    }
}
