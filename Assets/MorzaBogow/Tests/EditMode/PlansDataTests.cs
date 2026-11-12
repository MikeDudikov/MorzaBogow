using System;
using System.Linq;
using System.Reflection;
using MorzaBogow.Simulation;
using MorzaBogow.Simulation.Catalog;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// P02: the data of tasks, of the opponent's profiles and of reinforcements (Catalog.Plans.cs). Data and its rules of
    /// soundness only: nothing here is wired into the running opponent or the world.
    /// </summary>
    public sealed class PlansDataTests
    {
        // ---------- Tasks ----------

        [Test]
        public void Tasks_FiveKindsEachWithItsDefinitionAndNeeds()
        {
            var catalog = GameCatalog.Default;
            Assert.That(catalog.Tasks.Select(item => (item.Id, item.Kind)), Is.EqualTo(new[]
            {
                ("A01", TaskKind.EscortGroup), ("A02", TaskKind.ScoutAndReturn), ("A03", TaskKind.Patrol), ("A04", TaskKind.DefendArea), ("A05", TaskKind.AttackInArea),
            }));
            foreach (TaskKind kind in Enum.GetValues(typeof(TaskKind))) Assert.That(catalog.Task(kind), Is.Not.Null, kind.ToString());
            Assert.That(catalog.Tasks.Select(item => (item.NeedsEscorted, item.NeedsRoute, item.NeedsArea, item.NeedsReturn)), Is.EqualTo(new[]
            {
                (true, false, false, false), (false, true, false, true), (false, true, false, false), (false, false, true, false), (false, false, true, false),
            }));
            Assert.That(catalog.Task(TaskKind.Patrol).LeastRoutePoints, Is.EqualTo(2), "A patrol goes round two points or more.");
            Assert.That(TaskRecord.QueueLimit, Is.EqualTo(16));
            Assert.That((int)AutonomyFlags.All, Is.EqualTo(15), "Four permissions, each its own bit.");
            Assert.That(new[] { AutonomyFlags.FireDefence, AutonomyFlags.AllowRam, AutonomyFlags.AllowBoard, AutonomyFlags.ReturnFire }.Select(flag => (int)flag), Is.EqualTo(new[] { 1, 2, 4, 8 }));
        }

        [Test]
        public void Tasks_ARecordIsSoundOnlyWithWhatItsKindNeeds()
        {
            var catalog = GameCatalog.Default;
            TaskRecord Sound(TaskKind kind)
            {
                switch (kind)
                {
                    case TaskKind.EscortGroup: return new TaskRecord { Kind = kind, UnitId = 7, EscortedIds = new[] { 3, 4 } };
                    case TaskKind.ScoutAndReturn: return new TaskRecord { Kind = kind, UnitId = 7, RouteX = new[] { 100.0 }, RouteY = new[] { 50.0 }, HasReturn = true, ReturnX = -200, ReturnY = 0 };
                    case TaskKind.Patrol: return new TaskRecord { Kind = kind, UnitId = 7, RouteX = new[] { 0.0, 100, 100 }, RouteY = new[] { 0.0, 0, 80 }, RouteIndex = 2 };
                    default: return new TaskRecord { Kind = kind, UnitId = 7, HasArea = true, AreaMinX = -50, AreaMinY = -50, AreaMaxX = 50, AreaMaxY = 50, Flags = AutonomyFlags.FireDefence | AutonomyFlags.ReturnFire };
                }
            }
            foreach (TaskKind kind in Enum.GetValues(typeof(TaskKind)))
                Assert.That(TaskRules.Problems(Sound(kind), catalog.Task(kind)), Is.Empty, kind.ToString());

            void Refused(TaskKind kind, Action<TaskRecord> fault, string name)
            {
                var task = Sound(kind);
                fault(task);
                Assert.That(TaskRules.Problems(task, catalog.Task(kind)), Is.Not.Empty, name);
            }
            Refused(TaskKind.EscortGroup, task => task.EscortedIds = new int[0], "an escort of nobody");
            Refused(TaskKind.EscortGroup, task => task.EscortedIds = new[] { 3, 3 }, "the same ship twice");
            Refused(TaskKind.EscortGroup, task => task.EscortedIds = new[] { 7 }, "a ship escorting itself");
            Refused(TaskKind.EscortGroup, task => task.UnitId = -1, "no unit");
            Refused(TaskKind.ScoutAndReturn, task => task.HasReturn = false, "a scout with no way back");
            Refused(TaskKind.ScoutAndReturn, task => task.RouteX = new double[0], "a scout with points that are no pairs");
            Refused(TaskKind.ScoutAndReturn, task => task.ReturnX = double.NaN, "a return that is no number");
            Refused(TaskKind.Patrol, task => { task.RouteX = new[] { 0.0 }; task.RouteY = new[] { 0.0 }; task.RouteIndex = 0; }, "a patrol of one point");
            Refused(TaskKind.Patrol, task => task.RouteIndex = 3, "a point beyond the route");
            Refused(TaskKind.Patrol, task => task.RouteY[1] = double.PositiveInfinity, "a point that is no number");
            Refused(TaskKind.DefendArea, task => task.HasArea = false, "a defence of no area");
            Refused(TaskKind.DefendArea, task => task.AreaMaxX = task.AreaMinX, "an empty area");
            Refused(TaskKind.AttackInArea, task => task.Flags = (AutonomyFlags)32, "a permission that is not");
            Assert.That(TaskRules.Problems(Sound(TaskKind.Patrol), catalog.Task(TaskKind.EscortGroup)), Is.Not.Empty, "Another kind's definition.");
            Assert.That(TaskRules.Problems(Sound(TaskKind.Patrol), null), Is.Not.Empty);
        }

        // ---------- The opponent's profiles ----------

        [Test]
        public void AiProfiles_ThreeLevelsThatDifferInDecisionsOnly()
        {
            var catalog = GameCatalog.Default;
            Assert.That(catalog.AiProfiles.Select(item => (item.Id, item.Name, item.ReactionTicks)), Is.EqualTo(new[] { ("AI-NAUKA", "Nauka", 90), ("AI-STANDARD", "Standard", 30), ("AI-WETERAN", "Weteran", 15) }));
            foreach (var profile in catalog.AiProfiles) Assert.That(AiProfileRules.Problems(profile), Is.Empty, profile.Id);

            // Standard is the opponent as it plays today: its fleet is planned every second, its economy every three, a
            // ship regroups below half its fighters, boards at odds strictly above 1.2, docks below 0.3 of its hull and
            // takes an enemy within 250 m of home for a threat (Opponent.cs).
            var standard = catalog.Profile("AI-STANDARD");
            Assert.That(catalog.AiProfiles.Where(item => item.IsRunningBehaviour).Select(item => item.Id), Is.EqualTo(new[] { "AI-STANDARD" }));
            Assert.That((standard.ReactionTicks, standard.StrategyTicks, standard.AttackGroupOffset, standard.RegroupCrewShare, standard.BoardingOddsAbove, standard.HoledHullShare, standard.HomeThreatMeters),
                Is.EqualTo((Rules.TickRate, 3 * Rules.TickRate, 0, 0.5, 1.2, 0.3, 250.0)));

            // A profile holds decisions only. Its type has no field for a price, a statistic, a reward or what the
            // opponent knows: a new field must be a decision, and this list says which there are.
            var fields = typeof(AiProfile).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal);
            Assert.That(fields, Is.EqualTo(new[] { "AttackGroupOffset", "BoardingOddsAbove", "HoledHullShare", "HomeThreatMeters", "Id", "IsRunningBehaviour", "Name", "ReactionTicks", "RegroupCrewShare", "Source", "StrategyTicks" }));
            Assert.That(typeof(AiProfile).GetProperties(BindingFlags.Public | BindingFlags.Instance), Is.Empty);

            // The parameters document fixes the reaction alone; every other decision of the two other levels is Standard's.
            foreach (var profile in catalog.AiProfiles)
                Assert.That((profile.StrategyTicks, profile.AttackGroupOffset, profile.RegroupCrewShare, profile.BoardingOddsAbove, profile.HoledHullShare, profile.HomeThreatMeters),
                    Is.EqualTo((standard.StrategyTicks, standard.AttackGroupOffset, standard.RegroupCrewShare, standard.BoardingOddsAbove, standard.HoledHullShare, standard.HomeThreatMeters)), profile.Id);
            Assert.That(catalog.AiProfiles.Select(item => item.ReactionTicks / (double)Rules.TickRate), Is.EqualTo(new[] { 3.0, 1.0, 0.5 }), "Three seconds, one, half a second.");

            void Refused(Action<AiProfile> fault, string name)
            {
                var profile = GameCatalog.Create().Profile("AI-STANDARD");
                fault(profile);
                Assert.That(AiProfileRules.Problems(profile), Is.Not.Empty, name);
            }
            Refused(profile => profile.ReactionTicks = 0, "no wait between decisions");
            Refused(profile => profile.StrategyTicks = -30, "a negative wait");
            Refused(profile => profile.RegroupCrewShare = 1.5, "a share above the whole crew");
            Refused(profile => profile.RegroupCrewShare = -0.1, "a negative share");
            Refused(profile => profile.BoardingOddsAbove = 0, "no odds");
            Refused(profile => profile.HoledHullShare = 1.5, "a share above the whole hull");
            Refused(profile => profile.HomeThreatMeters = 0, "no threat radius");
            Refused(profile => profile.Id = "", "no ID");
        }

        // ---------- Reinforcements ----------

        static ReinforcementManifest Wave() => new ReinforcementManifest
        {
            Id = "K03-W1", ScenarioId = "K03", SourceId = "WejścieZatoki", OwnerSlot = Owners.MainAi,
            Units = new[] { new ManifestUnit { VariantId = "N03", Count = 2 }, new ManifestUnit { VariantId = "N04", Count = 1 } },
            Limit = 1, Trigger = ReinforcementTrigger.ObjectiveReached, TriggerArg = "Wymagane", AnnounceSeconds = 20, PaidWood = 660, PaidSilver = 290,
            Source = new SourceNote { Status = SourceStatus.GameRule, Layer = WorldLayer.GameRule, Locator = "docs/MAPY_I_SCENARIUSZE.md §4 K03 (fala próbna testu)" },
        };

        [Test]
        public void Reinforcements_AManifestIsFiniteSourcedAnnouncedAndNeverNegative()
        {
            var catalog = GameCatalog.Default;
            bool Exists(string id) => catalog.Variant(id) != null;
            Assert.That(ReinforcementRules.Problems(Wave(), Exists), Is.Empty);
            Assert.That(catalog.Reinforcements, Is.Empty, "The scenarios' own waves come with their data (P10).");

            void Refused(Action<ReinforcementManifest> fault, string name)
            {
                var wave = Wave();
                fault(wave);
                Assert.That(ReinforcementRules.Problems(wave, Exists), Is.Not.Empty, name);
            }
            Refused(wave => wave.SourceId = " ", "no source");
            Refused(wave => wave.Units = new ManifestUnit[0], "an empty wave");
            Refused(wave => wave.Units[0].Count = 0, "an entry of no ships");
            Refused(wave => wave.Units[0].Count = -3, "a negative number of ships");
            Refused(wave => wave.Units[0].Count = ReinforcementRules.LargestWave, "a wave beyond the largest");
            Refused(wave => wave.Units[1].VariantId = "N99", "a role that is not in the catalogue");
            Refused(wave => wave.Limit = 0, "a wave that never comes");
            Refused(wave => wave.Limit = ReinforcementRules.LargestLimit + 1, "a wave without end");
            Refused(wave => wave.Limit = int.MaxValue, "a wave without end");
            Refused(wave => wave.AnnounceSeconds = 0, "no announcement");
            Refused(wave => wave.AnnounceSeconds = double.PositiveInfinity, "an announcement that never ends");
            Refused(wave => wave.TriggerArg = null, "a trigger that names nothing");
            Refused(wave => wave.Trigger = (ReinforcementTrigger)99, "an unknown trigger");
            Refused(wave => { wave.Trigger = ReinforcementTrigger.AtTick; wave.TriggerArg = "soon"; }, "a time that is no tick");
            Refused(wave => { wave.Trigger = ReinforcementTrigger.AtTick; wave.TriggerArg = "-30"; }, "a negative tick");
            Refused(wave => wave.PaidSilver = -1, "a negative price");
            Refused(wave => wave.PaidWood = -1, "a negative price");
            Refused(wave => wave.OwnerSlot = 3, "a slot that is not");
            Refused(wave => wave.Id = null, "no ID");
            Refused(wave => wave.Units[1] = null, "an empty entry");
            Refused(wave => wave.Units = null, "no list of ships");
            Refused(wave => wave.Source = null, "no source note");
            Refused(wave => wave.Source.Locator = " ", "a source note without a locator");

            var timed = Wave();
            timed.Trigger = ReinforcementTrigger.AtTick;
            timed.TriggerArg = "9000";
            Assert.That(ReinforcementRules.Problems(timed, Exists), Is.Empty, "A wave at a stated tick.");
            var largest = Wave();
            largest.Units = new[] { new ManifestUnit { VariantId = "N03", Count = ReinforcementRules.LargestWave } };
            largest.Limit = ReinforcementRules.LargestLimit;
            Assert.That(ReinforcementRules.Problems(largest, Exists), Is.Empty, "The largest wave the most times is still finite.");

            // What may set a wave off is something the briefing can state. No trigger measures how well the player does.
            Assert.That(Enum.GetNames(typeof(ReinforcementTrigger)), Is.EqualTo(new[] { "AtTick", "ObjectiveReached", "ZoneEntered", "SourceCaptured" }));
            // A sound wave belongs in the catalogue only with its scenario.
            var with = GameCatalog.Create();
            with.Reinforcements.Add(Wave());
            Assert.That(with.Problems(), Is.Empty);
            with.Reinforcements[0].ScenarioId = "K99";
            Assert.That(with.Problems(), Is.Not.Empty);
        }
    }
}
