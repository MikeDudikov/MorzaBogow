using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MorzaBogow.Simulation.Catalog
{
    /// <summary>
    /// The catalogue of the game's definitions as data (P02): the fleet roles, the buildings and research, the forts, the
    /// weapon families, the powers, the heroes, the objects, the encounters, the scenarios, the tasks and the opponent's
    /// profiles. It describes; it runs nothing. What the running game already has is marked Implemented and, for the
    /// seven classes and the five projects, equals the running game's own numbers. Its ID and hash name one exact
    /// content, so a save or a report can later say which catalogue it was made with.
    /// </summary>
    public sealed class GameCatalog
    {
        /// <summary>The catalogue's name and the number of its form; the hash tells the content apart.</summary>
        public const string CatalogId = "morza-bogow/catalog@1";

        public readonly List<ShipVariantDefinition> ShipVariants;
        public readonly List<HullProfileReference> HullProfiles;
        public readonly List<WeaponFamilyDefinition> WeaponFamilies;
        public readonly List<SpecialWeaponDefinition> SpecialWeapons;
        public readonly List<UpgradeLevelDefinition> UpgradeLevels;
        public readonly List<TechnologyDefinition> Technologies;
        public readonly List<FortDefinition> Forts;
        public readonly List<AbilityDefinition> Abilities;
        public readonly List<HeroDefinition> Heroes;
        public readonly List<ArtifactDefinition> Artifacts;
        public readonly List<EncounterDefinition> Encounters;
        public readonly List<ScenarioDefinition> Scenarios;
        public readonly List<TaskDefinition> Tasks;
        public readonly List<AiProfile> AiProfiles;

        /// <summary>The waves of reinforcements of the scenarios; none until the scenarios' own data is written (P10).</summary>
        public readonly List<ReinforcementManifest> Reinforcements = new List<ReinforcementManifest>();

        GameCatalog()
        {
            ShipVariants = CatalogData.ShipVariants();
            HullProfiles = CatalogData.HullProfiles();
            WeaponFamilies = CatalogData.WeaponFamilies();
            SpecialWeapons = CatalogData.SpecialWeapons();
            UpgradeLevels = CatalogData.UpgradeLevels();
            Technologies = CatalogData.Technologies();
            Forts = CatalogData.Forts();
            Abilities = CatalogData.Abilities();
            Heroes = CatalogData.Heroes();
            Artifacts = CatalogData.Artifacts();
            Encounters = CatalogData.Encounters();
            Scenarios = CatalogData.Scenarios();
            Tasks = CatalogData.Tasks();
            AiProfiles = CatalogData.AiProfiles();
        }

        /// <summary>A catalogue of its own, built anew: tools and tests may change it without touching the game's.</summary>
        public static GameCatalog Create() => new GameCatalog();

        static GameCatalog standard;

        /// <summary>The game's catalogue. Read only: nothing may change what it holds.</summary>
        public static GameCatalog Default => standard ?? (standard = new GameCatalog());

        // ---------- Lookup ----------

        public ShipVariantDefinition Variant(string id) => ShipVariants.FirstOrDefault(item => item.Id == id);
        public TechnologyDefinition Technology(string id) => Technologies.FirstOrDefault(item => item.Id == id);
        public AbilityDefinition Ability(string id) => Abilities.FirstOrDefault(item => item.Id == id);
        public HeroDefinition Hero(string id) => Heroes.FirstOrDefault(item => item.Id == id);
        public ArtifactDefinition Artifact(string id) => Artifacts.FirstOrDefault(item => item.Id == id);
        public ScenarioDefinition Scenario(string id) => Scenarios.FirstOrDefault(item => item.Id == id);
        public AiProfile Profile(string id) => AiProfiles.FirstOrDefault(item => item.Id == id);
        public TaskDefinition Task(TaskKind kind) => Tasks.FirstOrDefault(item => item.Kind == kind);

        /// <summary>The fleet role that is a class of the running game.</summary>
        public ShipVariantDefinition VariantOf(ShipClass shipClass) => ShipVariants.FirstOrDefault(item => item.IsRuntimeClass && item.HullClass == shipClass);

        /// <summary>The roles a faction may have: the eight common to both and its own two specialisations.</summary>
        public IEnumerable<ShipVariantDefinition> VariantsOf(FactionId faction) =>
            ShipVariants.Where(item => item.Availability.Faction == FactionId.Any || item.Availability.Faction == faction);

        /// <summary>The technology that is a project of the running game.</summary>
        public TechnologyDefinition TechnologyOf(ProjectKind project) => project == ProjectKind.None ? null : Technologies.FirstOrDefault(item => item.RuntimeProject == project);

        /// <summary>The family a machine type of the running game belongs to; null for the banks of archers and javelin men, which are no machines.</summary>
        public WeaponFamilyDefinition FamilyOf(WeaponType type) => WeaponFamilies.FirstOrDefault(item => item.RuntimeTypes.Contains(type));

        /// <summary>Every ID of the catalogue, in the catalogue's order.</summary>
        public IEnumerable<string> AllIds() =>
            ShipVariants.Select(item => item.Id).Concat(HullProfiles.Select(item => item.Id)).Concat(WeaponFamilies.Select(item => item.Id))
                .Concat(SpecialWeapons.Select(item => item.Id)).Concat(UpgradeLevels.Select(item => item.Id)).Concat(Technologies.Select(item => item.Id))
                .Concat(Forts.Select(item => item.Id)).Concat(Abilities.Select(item => item.Id)).Concat(Heroes.Select(item => item.Id))
                .Concat(Artifacts.Select(item => item.Id)).Concat(Encounters.Select(item => item.Id)).Concat(Scenarios.Select(item => item.Id))
                .Concat(Tasks.Select(item => item.Id)).Concat(AiProfiles.Select(item => item.Id)).Concat(Reinforcements.Select(item => item.Id));

        /// <summary>Every source note of the catalogue with the ID it belongs to.</summary>
        public IEnumerable<(string id, SourceNote note)> AllSources() =>
            ShipVariants.Select(item => (item.Id, item.Source)).Concat(HullProfiles.Select(item => (item.Id, item.Source)))
                .Concat(WeaponFamilies.Select(item => (item.Id, item.Source))).Concat(SpecialWeapons.Select(item => (item.Id, item.Source)))
                .Concat(UpgradeLevels.Select(item => (item.Id, item.Source))).Concat(Technologies.Select(item => (item.Id, item.Source)))
                .Concat(Forts.Select(item => (item.Id, item.Source))).Concat(Abilities.Select(item => (item.Id, item.Source)))
                .Concat(Heroes.Select(item => (item.Id, item.Source))).Concat(Artifacts.Select(item => (item.Id, item.Source)))
                .Concat(Encounters.Select(item => (item.Id, item.Source))).Concat(Scenarios.Select(item => (item.Id, item.Source)))
                .Concat(Tasks.Select(item => (item.Id, item.Source))).Concat(AiProfiles.Select(item => (item.Id, item.Source)))
                .Concat(Reinforcements.Select(item => (item.Id, item.Source)));

        /// <summary>Every effect the catalogue names, with the ID of the definition that names it.</summary>
        public IEnumerable<(string ownerId, string effectId)> AllEffectRefs() =>
            SpecialWeapons.SelectMany(item => (item.EffectRefs ?? Array.Empty<string>()).Select(effect => (item.Id, effect)))
                .Concat(Abilities.SelectMany(item => (item.EffectRefs ?? Array.Empty<string>()).Select(effect => (item.Id, effect))))
                .Concat(Heroes.SelectMany(item => (item.EffectRefs ?? Array.Empty<string>()).Select(effect => (item.Id, effect))))
                .Concat(Artifacts.SelectMany(item => (item.EffectRefs ?? Array.Empty<string>()).Select(effect => (item.Id, effect))))
                .Concat(Encounters.SelectMany(item => (item.EffectRefs ?? Array.Empty<string>()).Select(effect => (item.Id, effect))));

        // ---------- Soundness ----------

        /// <summary>What is wrong with the catalogue's data; empty when it is sound.</summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            var ids = AllIds().ToList();
            foreach (var id in ids.Where(string.IsNullOrWhiteSpace)) problems.Add("A definition without an ID.");
            foreach (var repeated in ids.GroupBy(id => id).Where(group => group.Count() > 1)) problems.Add($"{repeated.Key}: the ID is used {repeated.Count()} times.");
            foreach (var (id, note) in AllSources())
            {
                if (note == null || string.IsNullOrWhiteSpace(note.Locator)) problems.Add($"{id}: no source locator.");
                else if (!Enum.IsDefined(typeof(SourceStatus), note.Status) || !Enum.IsDefined(typeof(WorldLayer), note.Layer)) problems.Add($"{id}: an unknown source status or layer.");
            }

            // Effects are named by the owner's ID, or by it and a kind; no two definitions name the same effect.
            var effects = AllEffectRefs().ToList();
            foreach (var (ownerId, effectId) in effects)
                if (!EffectIds.IsValidFor(ownerId, effectId)) problems.Add($"{ownerId}: the effect ID {effectId ?? "(none)"} is not its own ID, nor its ID with a kind, or is too long.");
            foreach (var repeated in effects.Where(item => item.effectId != null).GroupBy(item => item.effectId).Where(group => group.Count() > 1))
                problems.Add($"{repeated.Key}: the effect is named {repeated.Count()} times.");

            bool TechnologyExists(string id) => Technologies.Any(item => item.Id == id);
            void Check(string id, Availability availability)
            {
                foreach (var technology in availability.Technologies.Concat(availability.TargetTechnologies))
                    if (!TechnologyExists(technology)) problems.Add($"{id}: asks for a technology that is not in the catalogue, {technology}.");
                foreach (var ruleset in availability.Rulesets) if (Array.IndexOf(Rulesets.All, ruleset) < 0) problems.Add($"{id}: an unknown ruleset, {ruleset}.");
                if (availability.OwnerSlots == 0 || (availability.OwnerSlots & ~Availability.AllSlots) != 0) problems.Add($"{id}: the owner slots are none or not slots.");
                if (availability.Date != null && availability.Date.LatestYearBc > availability.Date.EarliestYearBc) problems.Add($"{id}: the date window ends before it begins.");
            }

            foreach (var variant in ShipVariants)
            {
                Check(variant.Id, variant.Availability);
                if (variant.CostWood < 0 || variant.CostSilver < 0 || !(variant.BuildSeconds > 0)) problems.Add($"{variant.Id}: a negative price or no building time.");
                if (variant.MaxCrew <= 0) problems.Add($"{variant.Id}: no crew.");
                if (variant.DefaultLoadout.Length > variant.Slots.Length) problems.Add($"{variant.Id}: more weapons than posts.");
                for (var i = 0; i < variant.DefaultLoadout.Length; i++)
                    if (!WeaponCatalog.Fits(variant.DefaultLoadout[i], variant.Slots[i])) problems.Add($"{variant.Id}: the weapon of post {i} does not fit it.");
                if (variant.BuiltInWeaponId != null && SpecialWeapons.All(item => item.Id != variant.BuiltInWeaponId)) problems.Add($"{variant.Id}: a built-in weapon that is not in the catalogue.");
                if (variant.ArtifactSlots < 0 || variant.HeroSlots < 0) problems.Add($"{variant.Id}: a negative number of places.");
                // The crew is paid out of the silver of the price (half a unit a sailor, a unit a fighter): a variant whose
                // crew costs more than the whole ship would have a negative price for its hull (Q03).
                var profile = ShipCatalog.Get(variant.HullClass);
                var economic = profile.IsEconomic;
                var sailors = economic ? variant.MaxCrew : (int)Math.Ceiling(variant.MaxCrew * 0.25);
                var crewSilverCenti = sailors * Rules.Centi / 2 + (variant.MaxCrew - sailors) * Rules.Centi;
                if (crewSilverCenti > variant.CostSilver * Rules.Centi) problems.Add($"{variant.Id}: the crew costs more silver than the ship.");
            }
            foreach (var weapon in SpecialWeapons)
            {
                var carrier = Variant(weapon.CarrierVariantId);
                if (carrier == null || carrier.BuiltInWeaponId != weapon.Id) problems.Add($"{weapon.Id}: its carrier does not carry it.");
            }
            foreach (var type in (WeaponType[])Enum.GetValues(typeof(WeaponType)))
            {
                var families = WeaponFamilies.Count(family => family.RuntimeTypes.Contains(type));
                var machine = type != WeaponType.None && WeaponCatalog.Get(type).IsMachine;
                if (machine && families != 1) problems.Add($"{type}: a machine belongs to exactly one family, not {families}.");
                if (!machine && families != 0) problems.Add($"{type}: not a machine, yet in a family.");
            }
            foreach (var level in UpgradeLevels)
            {
                if (level.Level < 1 || level.Level > 3) problems.Add($"{level.Id}: a grade outside one to three.");
                if ((level.Level == 1) != (level.TechnologyId == null)) problems.Add($"{level.Id}: grade I needs no research, the others do.");
                if (level.TechnologyId != null && !TechnologyExists(level.TechnologyId)) problems.Add($"{level.Id}: its research is not in the catalogue.");
            }
            foreach (var technology in Technologies)
            {
                Check(technology.Id, technology.Availability);
                if (technology.CostWood < 0 || technology.CostSilver < 0 || technology.Seconds < 0) problems.Add($"{technology.Id}: a negative price or time.");
                if (technology.Scope == TechnologyScope.PortBuilding && technology.Grants == PortBuildingSet.None) problems.Add($"{technology.Id}: a building that adds nothing to its port.");
                if (technology.Scope == TechnologyScope.SideResearch && technology.Grants != PortBuildingSet.None) problems.Add($"{technology.Id}: a research that is a building.");
            }
            foreach (var project in Technologies.Where(item => item.RuntimeProject != ProjectKind.None).GroupBy(item => item.RuntimeProject).Where(group => group.Count() > 1))
                problems.Add($"{project.Key}: the project is claimed by {project.Count()} technologies.");
            if (HasTechnologyCycle(out var inCycle)) problems.Add($"{inCycle}: the research graph has a cycle.");
            foreach (var fort in Forts)
            {
                Check(fort.Id, fort.Availability);
                if (fort.CostWood < 0 || fort.CostSilver < 0 || !(fort.BuildSeconds > 0) || !(fort.Hull > 0)) problems.Add($"{fort.Id}: a negative price, no building time or no hull.");
                if (fort.PoolCost < 1 || fort.PoolCost > FortDefinition.SettlementPool) problems.Add($"{fort.Id}: a pool cost outside the settlement's pool.");
            }
            foreach (var ability in Abilities)
            {
                Check(ability.Id, ability.Availability);
                if (ability.CostWood < 0 || ability.CostSilver < 0 || ability.RangeMeters < 0 || ability.RadiusMeters < 0 || ability.DurationSeconds < 0 || ability.CooldownSeconds < 0 || ability.WarningSeconds < 0)
                    problems.Add($"{ability.Id}: a negative price, range, radius or time.");
                if (!ability.WholeMapRange && !(ability.RangeMeters > 0)) problems.Add($"{ability.Id}: a local power without a range.");
                if (ability.Duration == DurationKind.Timed && !(ability.DurationSeconds > 0)) problems.Add($"{ability.Id}: a timed power without a time.");
                if (ability.EffectRefs.Length == 0 || ability.EffectRefs.Any(string.IsNullOrWhiteSpace)) problems.Add($"{ability.Id}: no effect named.");
                if (!TechnologyExists(ability.LearnTechnologyId)) problems.Add($"{ability.Id}: its learning is not in the catalogue.");
            }
            foreach (var hero in Heroes)
            {
                Check(hero.Id, hero.Availability);
                var sign = Artifact(hero.RecruitSignId);
                if (sign == null || sign.Kind != ArtifactKind.RecruitSign || sign.HeroId != hero.Id) problems.Add($"{hero.Id}: its recruitment sign is missing or belongs to another.");
                if (hero.EffectRefs.Length == 0 || hero.EffectRefs.Any(string.IsNullOrWhiteSpace)) problems.Add($"{hero.Id}: no effect named.");
            }
            foreach (var artifact in Artifacts)
            {
                Check(artifact.Id, artifact.Availability);
                if (artifact.Kind == ArtifactKind.RecruitSign)
                {
                    var hero = Hero(artifact.HeroId);
                    if (hero == null || hero.RecruitSignId != artifact.Id) problems.Add($"{artifact.Id}: a sign of no hero.");
                }
                else
                {
                    if (artifact.HeroId != null) problems.Add($"{artifact.Id}: a useful object bound to a hero.");
                    if (artifact.EffectRefs.Length == 0 || artifact.EffectRefs.Any(string.IsNullOrWhiteSpace)) problems.Add($"{artifact.Id}: no effect named.");
                }
            }
            foreach (var encounter in Encounters)
            {
                Check(encounter.Id, encounter.Availability);
                if (!encounter.Unowned && encounter.OwnerSlot != Owners.Independent) problems.Add($"{encounter.Id}: an encounter belongs to the independent slot or to nobody.");
                if (encounter.ImmuneChannel != null && Array.IndexOf(EffectChannelNames.All, encounter.ImmuneChannel) < 0) problems.Add($"{encounter.Id}: proof against a channel that is not, {encounter.ImmuneChannel}.");
                if (encounter.VariantId != null && Variant(encounter.VariantId) == null) problems.Add($"{encounter.Id}: built on a role that is not in the catalogue.");
                if (encounter.RewardWood < 0 || encounter.RewardSilver < 0 || encounter.WorkSeconds < 0) problems.Add($"{encounter.Id}: a negative reward or time.");
                if (encounter.Kind == EncounterKind.EconomicTarget && !(encounter.WorkSeconds > 0 && encounter.RewardWood + encounter.RewardSilver > 0)) problems.Add($"{encounter.Id}: a target of harvest without work or reward.");
            }
            foreach (var scenario in Scenarios)
                foreach (var problem in ScenarioRules.Problems(scenario)) problems.Add($"{scenario.Id}: {problem}");
            foreach (var task in Tasks.GroupBy(item => item.Kind).Where(group => group.Count() > 1)) problems.Add($"{task.Key}: the kind of task is defined {task.Count()} times.");
            foreach (var profile in AiProfiles)
                foreach (var problem in AiProfileRules.Problems(profile)) problems.Add($"{profile.Id}: {problem}");
            if (AiProfiles.Count(item => item.IsRunningBehaviour) != 1) problems.Add("Exactly one profile is the opponent as it plays today.");
            foreach (var manifest in Reinforcements)
            {
                foreach (var problem in ReinforcementRules.Problems(manifest, id => Variant(id) != null)) problems.Add($"{manifest.Id}: {problem}");
                if (Scenario(manifest.ScenarioId) == null) problems.Add($"{manifest.Id}: a wave of no scenario.");
            }
            return problems;
        }

        /// <summary>
        /// The technologies a technology asks for first, by the planned requirements: the researches it names and the
        /// buildings it needs in its port.
        /// </summary>
        public IEnumerable<string> Prerequisites(TechnologyDefinition technology)
        {
            foreach (var id in technology.Availability.TargetTechnologies) yield return id;
            foreach (var building in Technologies.Where(item => item.Scope == TechnologyScope.PortBuilding))
                if (building.Id != technology.Id && (technology.Availability.TargetBuildings & building.Grants) != 0) yield return building.Id;
        }

        /// <summary>A research must never need what only it opens.</summary>
        bool HasTechnologyCycle(out string inCycle)
        {
            var state = new Dictionary<string, int>();
            bool Visit(string id)
            {
                var technology = Technology(id);
                if (technology == null) return false;
                state.TryGetValue(id, out var seen);
                if (seen == 1) return true;
                if (seen == 2) return false;
                state[id] = 1;
                foreach (var prerequisite in Prerequisites(technology)) if (Visit(prerequisite)) return true;
                state[id] = 2;
                return false;
            }
            foreach (var technology in Technologies)
            {
                if (!Visit(technology.Id)) continue;
                inCycle = technology.Id;
                return true;
            }
            inCycle = null;
            return false;
        }

        // ---------- Identity ----------

        /// <summary>The whole catalogue as text in its one fixed form (CanonicalWriter): the same content gives the same text on every machine.</summary>
        public string CanonicalText()
        {
            var writer = new CanonicalWriter();
            writer.Begin("Catalog", CatalogId).End();
            foreach (var item in ShipVariants) item.Write(writer);
            foreach (var item in HullProfiles) item.Write(writer);
            foreach (var item in WeaponFamilies) item.Write(writer);
            foreach (var item in SpecialWeapons) item.Write(writer);
            foreach (var item in UpgradeLevels) item.Write(writer);
            foreach (var item in Technologies) item.Write(writer);
            foreach (var item in Forts) item.Write(writer);
            foreach (var item in Abilities) item.Write(writer);
            foreach (var item in Heroes) item.Write(writer);
            foreach (var item in Artifacts) item.Write(writer);
            foreach (var item in Encounters) item.Write(writer);
            foreach (var item in Scenarios) item.Write(writer);
            foreach (var item in Tasks) item.Write(writer);
            foreach (var item in AiProfiles) item.Write(writer);
            foreach (var item in Reinforcements) item.Write(writer);
            return writer.ToString();
        }

        /// <summary>FNV-1a over the canonical text: the catalogue's content as one number.</summary>
        public ulong Hash() => CanonicalWriter.Hash(CanonicalText());

        /// <summary>The ID and the hash as one line, e.g. morza-bogow/catalog@1#0123456789ABCDEF.</summary>
        public string Identity() => CatalogId + "#" + Hash().ToString("X16", CultureInfo.InvariantCulture);
    }
}
