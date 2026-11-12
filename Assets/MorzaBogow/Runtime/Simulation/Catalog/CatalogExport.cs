using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MorzaBogow.Simulation.Catalog
{
    /// <summary>
    /// The catalogue written out for the tools outside the game (the economy model, QA): the prices, times and
    /// requirements the running game really uses beside those the documents plan. A pure function of the catalogue and of
    /// the running game's own numbers; it reads and writes no file, knows no time and no machine.
    ///
    /// The form is fixed so that the same content is the same text everywhere: keys in the order written here, numbers
    /// in the invariant culture, two spaces of indent, LF, a new line at the end (to be saved as UTF-8 without a mark).
    /// Every amount of wood or silver is in hundredths, as the simulation keeps it. What the running game asks for
    /// today ("enforced") stands apart from what the parameters document plans ("target"); a definition the game does not
    /// have yet has no "enforced" at all. "runtime" holds numbers taken straight from the simulation (ShipCatalog,
    /// World.ProjectCost), not from the catalogue's copy of them.
    /// </summary>
    public static class CatalogExport
    {
        /// <summary>
        /// The name of the form and its number. @1 is the form of the first file kept in the repository
        /// (qa/runtime-catalog.json). From that file on, any change of the keys, of their order or of their meaning raises
        /// the number; a change of the catalogue's content alone does not, it only moves catalogHash.
        /// </summary>
        public const string Schema = "morza-bogow/runtime-catalog@1";

        /// <summary>Where the export is kept in the repository, from its root. Written by the editor (CatalogExporter.Run), compared by a test.</summary>
        public const string RepositoryPath = "qa/runtime-catalog.json";

        /// <summary>The division of effects among the definitions is a working one until the effects are wired in (P07).</summary>
        public const string EffectDivisionStatus = "WORKING_UNTIL_P07";

        // The shares by which TemplatePricing splits a template's price (Definitions.cs keeps them as bare numbers).
        // CatalogExportTests holds them to what TemplatePricing really answers for every class.
        const long SailorSilverCenti = Rules.Centi / 2, FighterSilverCenti = Rules.Centi;
        const double WarshipSailorShare = 0.25, ConstructionWoodShare = 0.8, ConstructionSilverShare = 0.4;

        public static string ToJson(GameCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var root = Obj()
                .Add("schema", Schema)
                .Add("catalogId", GameCatalog.CatalogId)
                .Add("catalogHash", catalog.Hash().ToString("X16", CultureInfo.InvariantCulture))
                .Add("units", Obj()
                    .Add("wood", "centi-D (hundredths of a unit of wood)")
                    .Add("silver", "centi-S (hundredths of a unit of silver)")
                    .Add("rate", "hundredths of a unit per second. A ship's runtime.workRate and rules.node.sharedRate are multiplied by the match profile's woodYield or silverYield before use; rules.harbour.handlingRate is not")
                    .Add("time", "seconds")
                    .Add("distance", "metres")
                    .Add("share", "a fraction of one"))
                .Add("fields", Obj()
                    .Add("implemented", "the running game has the thing today")
                    .Add("enforced", "what the running game asks for today, in buildings of the port and researches of the side only; null where the thing is not implemented. Other conditions of an order are not expressed here: a settlement (B01) is founded from a ship carrying its price, a shipyard (B03) is built at a port the side holds")
                    .Add("target", "what docs/PARAMETRY_ROBOCZE.md plans to ask for; data only until its stage brings it in")
                    .Add("runtime", "numbers read straight from the simulation (ShipCatalog, TemplatePricing, World.ProjectCost), not from the catalogue's own copy; null where the running game has no such thing")
                    .Add("provisional", "a first guess for trials, not given by the documents"))
                .Add("sourceStatuses", Texts(((SourceStatus[])Enum.GetValues(typeof(SourceStatus))).Select(SourceNote.Code)))
                .Add("worldLayers", Texts(Enum.GetNames(typeof(WorldLayer))))
                .Add("rules", RulesBlock())
                .Add("ships", Arr(catalog.ShipVariants.Select(Ship)))
                .Add("hullProfiles", Arr(catalog.HullProfiles.Select(item => Obj()
                    .Add("id", item.Id).Add("name", item.Name).Add("date", Window(item.Date)).Add("source", Source(item.Source)))))
                .Add("weaponFamilies", Arr(catalog.WeaponFamilies.Select(item => Obj()
                    .Add("id", item.Id).Add("name", item.Name).Add("runtimeTypes", Texts(item.RuntimeTypes.Select(type => type.ToString())))
                    .Add("source", Source(item.Source)))))
                .Add("specialWeapons", Arr(catalog.SpecialWeapons.Select(item => Obj()
                    .Add("id", item.Id).Add("name", item.Name).Add("carrierVariantId", item.CarrierVariantId).Add("effectRefs", Texts(item.EffectRefs))
                    .Add("source", Source(item.Source)))))
                .Add("upgradeLevels", Arr(catalog.UpgradeLevels.Select(item => Obj()
                    .Add("id", item.Id).Add("name", item.Name).Add("kind", item.Kind.ToString()).Add("level", item.Level).Add("value", item.Value)
                    .Add("technologyId", item.TechnologyId).Add("source", Source(item.Source)))))
                .Add("technologies", Arr(catalog.Technologies.Select(Technology)))
                .Add("forts", Arr(catalog.Forts.Select(Fort)))
                .Add("abilities", Arr(catalog.Abilities.Select(Ability)))
                .Add("heroes", Arr(catalog.Heroes.Select(item => Availability(Obj()
                        .Add("id", item.Id).Add("name", item.Name).Add("recruitSignId", item.RecruitSignId)
                        .Add("effectRefs", Texts(item.EffectRefs)).Add("parameters", Parameters(item.Parameters)), item.Availability)
                    .Add("source", Source(item.Source)))))
                .Add("artifacts", Arr(catalog.Artifacts.Select(item => Availability(Obj()
                        .Add("id", item.Id).Add("name", item.Name).Add("kind", item.Kind.ToString()).Add("heroId", item.HeroId)
                        .Add("effectRefs", Texts(item.EffectRefs)).Add("parameters", Parameters(item.Parameters)), item.Availability)
                    .Add("source", Source(item.Source)))))
                .Add("encounters", Arr(catalog.Encounters.Select(Encounter)))
                .Add("scenarios", Arr(catalog.Scenarios.Select(Scenario)))
                .Add("tasks", Arr(catalog.Tasks.Select(item => Obj()
                    .Add("id", item.Id).Add("name", item.Name).Add("kind", item.Kind.ToString())
                    .Add("needsEscorted", item.NeedsEscorted).Add("needsRoute", item.NeedsRoute).Add("needsArea", item.NeedsArea).Add("needsReturn", item.NeedsReturn)
                    .Add("leastRoutePoints", item.LeastRoutePoints).Add("source", Source(item.Source)))))
                .Add("aiProfiles", Arr(catalog.AiProfiles.Select(item => Obj()
                    .Add("id", item.Id).Add("name", item.Name).Add("isRunningBehaviour", item.IsRunningBehaviour)
                    .Add("reactionTicks", item.ReactionTicks).Add("strategyTicks", item.StrategyTicks).Add("attackGroupOffset", item.AttackGroupOffset)
                    .Add("regroupCrewShare", item.RegroupCrewShare).Add("boardingOddsAbove", item.BoardingOddsAbove)
                    .Add("holedHullShare", item.HoledHullShare).Add("homeThreatMeters", item.HomeThreatMeters).Add("source", Source(item.Source)))))
                .Add("reinforcements", Arr(catalog.Reinforcements.Select(item => Obj()
                    .Add("id", item.Id).Add("scenarioId", item.ScenarioId).Add("sourceId", item.SourceId).Add("ownerSlot", item.OwnerSlot)
                    .Add("units", Arr((item.Units ?? Array.Empty<ManifestUnit>()).Where(unit => unit != null).Select(unit => Obj().Add("variantId", unit.VariantId).Add("count", unit.Count))))
                    .Add("limit", item.Limit).Add("trigger", item.Trigger.ToString()).Add("triggerArg", item.TriggerArg).Add("announceSeconds", item.AnnounceSeconds)
                    .Add("paid", Cost(item.PaidWood, item.PaidSilver)).Add("source", Source(item.Source)))))
                .Add("effects", Obj()
                    .Add("divisionStatus", EffectDivisionStatus)
                    .Add("refs", Arr(catalog.AllEffectRefs().Select(item => Obj().Add("ownerId", item.ownerId).Add("effectId", item.effectId)))));

            var text = new StringBuilder();
            root.Write(text, 0);
            text.Append('\n');
            return text.ToString();
        }

        // ---------- The running game's own rules ----------

        static Node RulesBlock() => Obj()
            .Add("tickRate", Rules.TickRate)
            .Add("centi", Rules.Centi)
            .Add("fleetLimit", Rules.FleetLimit)
            .Add("reserve", CentiPair(Rules.ReserveWood, Rules.ReserveSilver))
            .Add("warehouse", CentiPair(Rules.WarehouseWood, Rules.WarehouseSilver))
            .Add("harbour", Obj().Add("berths", Rules.PortBerths).Add("handlingRate", Centi(Rules.HandlingPerSecond)))
            .Add("node", Obj()
                .Add("workBerths", Rules.WorkBerths)
                .Add("sharedRate", Obj().Add("wood", Rules.WoodNodeCap * Rules.Centi).Add("silver", Rules.SilverNodeCap * Rules.Centi))
                .Add("landCrewSeconds", Rules.LandCrewSeconds).Add("embarkSeconds", Rules.EmbarkSeconds))
            .Add("convoySize", Rules.ConvoySize)
            .Add("outpost", Obj().Add("cost", CentiPair(Rules.OutpostWood, Rules.OutpostSilver)).Add("seconds", Rules.OutpostBuildSeconds).Add("hull", Rules.OutpostHull))
            .Add("shipyard", Obj().Add("cost", CentiPair(Rules.ShipyardWood, Rules.ShipyardSilver)).Add("seconds", Rules.ShipyardBuildSeconds))
            .Add("pricing", Obj()
                .Add("sailorSilver", SailorSilverCenti).Add("fighterSilver", FighterSilverCenti).Add("warshipSailorShare", WarshipSailorShare)
                .Add("constructionWoodShare", ConstructionWoodShare).Add("constructionSilverShare", ConstructionSilverShare))
            .Add("matchProfiles", Arr(((MatchLength[])Enum.GetValues(typeof(MatchLength))).Select(length =>
            {
                var profile = MatchProfile.Of(length);
                return Obj()
                    .Add("length", length.ToString()).Add("targetMinutes", profile.TargetMinutes).Add("targetFleet", profile.TargetFleet)
                    .Add("start", Cost(profile.StartWood, profile.StartSilver))
                    .Add("woodYield", profile.WoodYield).Add("silverYield", profile.SilverYield)
                    .Add("deposits", Obj()
                        .Add("home", Cost(profile.HomeWood, profile.HomeSilver)).Add("pair", Cost(profile.PairWood, profile.PairSilver))
                        .Add("centre", Cost(profile.CentreWood, profile.CentreSilver)));
            })));

        // ---------- Definitions ----------

        static Node Ship(ShipVariantDefinition item)
        {
            var node = Obj()
                .Add("id", item.Id).Add("name", item.Name).Add("role", item.Role.ToString()).Add("hullClass", item.HullClass.ToString())
                .Add("isRuntimeClass", item.IsRuntimeClass).Add("provisional", item.Provisional)
                .Add("cost", Cost(item.CostWood, item.CostSilver)).Add("buildSeconds", item.BuildSeconds).Add("maxCrew", item.MaxCrew)
                .Add("slots", Texts(item.Slots.Select(slot => slot.ToString()))).Add("defaultLoadout", Texts(item.DefaultLoadout.Select(weapon => weapon.ToString())))
                .Add("builtInWeaponId", item.BuiltInWeaponId).Add("artifactSlots", item.ArtifactSlots).Add("heroSlots", item.HeroSlots)
                .Add("canBeFlagship", item.CanBeFlagship).Add("capturable", item.Capturable);
            if (item.IsRuntimeClass)
            {
                // Straight from the running game, not from the catalogue's fields above.
                var definition = ShipCatalog.Get(item.HullClass);
                node.Add("runtime", Obj()
                    .Add("class", definition.Class.ToString())
                    .Add("cost", Cost(definition.CostWood, definition.CostSilver)).Add("buildSeconds", definition.BuildSeconds)
                    .Add("priceParts", Obj()
                        .Add("constructionWood", TemplatePricing.ConstructionWood(definition)).Add("constructionSilver", TemplatePricing.ConstructionSilver(definition))
                        .Add("equipmentWood", TemplatePricing.EquipmentWood(definition)).Add("equipmentSilver", TemplatePricing.EquipmentSilver(definition))
                        .Add("crewSilver", TemplatePricing.CrewSilver(definition)))
                    .Add("maxCrew", definition.MaxCrew).Add("sailors", definition.StandardSailors).Add("fighters", definition.StandardSoldiers)
                    .Add("hull", definition.Hull).Add("speed", definition.Speed)
                    .Add("capacity", definition.Capacity * Rules.Centi)
                    .Add("workRate", Obj().Add("wood", definition.WoodWork * Rules.Centi).Add("silver", definition.SilverWork * Rules.Centi))
                    .Add("projectileArmor", definition.ProjectileArmor).Add("ramMultiplier", definition.RamMultiplier));
            }
            else node.Add("runtime", Null());
            return Availability(node, item.Availability).Add("source", Source(item.Source));
        }

        static Node Technology(TechnologyDefinition item)
        {
            var node = Obj()
                .Add("id", item.Id).Add("name", item.Name).Add("scope", item.Scope.ToString()).Add("grants", Buildings(item.Grants))
                .Add("cost", Cost(item.CostWood, item.CostSilver)).Add("seconds", item.Seconds).Add("parameters", Parameters(item.Parameters));
            if (item.RuntimeProject != ProjectKind.None)
            {
                // Straight from the running game: what the project is charged and how long it takes.
                var (wood, silver, seconds) = World.ProjectCost(item.RuntimeProject);
                node.Add("runtime", Obj().Add("project", item.RuntimeProject.ToString()).Add("cost", CentiPair(wood, silver)).Add("seconds", seconds));
            }
            else node.Add("runtime", Null());
            return Availability(node, item.Availability).Add("source", Source(item.Source));
        }

        static Node Fort(FortDefinition item) => Availability(Obj()
                .Add("id", item.Id).Add("name", item.Name).Add("poolCost", item.PoolCost).Add("settlementPool", FortDefinition.SettlementPool)
                .Add("cost", Cost(item.CostWood, item.CostSilver)).Add("buildSeconds", item.BuildSeconds).Add("hull", item.Hull)
                .Add("slots", Texts(item.Slots.Select(slot => slot.ToString()))).Add("artifactSlots", item.ArtifactSlots).Add("heroSlots", item.HeroSlots)
                .Add("canBeRammed", item.CanBeRammed).Add("canBeBoarded", item.CanBeBoarded).Add("expeditionFortSellable", item.ExpeditionFortSellable), item.Availability)
            .Add("source", Source(item.Source));

        static Node Ability(AbilityDefinition item) => Availability(Obj()
                .Add("id", item.Id).Add("name", item.Name).Add("sanctuary", item.Sanctuary.ToString()).Add("cost", Cost(item.CostWood, item.CostSilver))
                .Add("wholeMapRange", item.WholeMapRange).Add("rangeMeters", item.RangeMeters).Add("reach", item.Reach.ToString()).Add("radiusMeters", item.RadiusMeters)
                .Add("duration", item.Duration.ToString()).Add("durationSeconds", item.DurationSeconds).Add("cooldownSeconds", item.CooldownSeconds)
                .Add("warningSeconds", item.WarningSeconds).Add("effectRefs", Texts(item.EffectRefs)).Add("parameters", Parameters(item.Parameters))
                .Add("learnTechnologyId", item.LearnTechnologyId), item.Availability)
            .Add("source", Source(item.Source));

        static Node Encounter(EncounterDefinition item) => Availability(Obj()
                .Add("id", item.Id).Add("name", item.Name).Add("kind", item.Kind.ToString()).Add("provisional", item.Provisional)
                .Add("unowned", item.Unowned).Add("ownerSlot", item.Unowned ? Null() : Num(item.OwnerSlot))
                .Add("respawnsFromSource", item.RespawnsFromSource).Add("immuneChannel", item.ImmuneChannel)
                .Add("scuttlesWhenBoardingLost", item.ScuttlesWhenBoardingLost).Add("variantId", item.VariantId)
                .Add("reward", Cost(item.RewardWood, item.RewardSilver)).Add("workSeconds", item.WorkSeconds)
                .Add("effectRefs", Texts(item.EffectRefs)).Add("parameters", Parameters(item.Parameters)), item.Availability)
            .Add("source", Source(item.Source));

        static Node Scenario(ScenarioDefinition item)
        {
            var carry = item.Carry;
            return Obj()
                .Add("id", item.Id).Add("name", item.Name).Add("kind", item.Kind.ToString()).Add("ruleset", item.Ruleset)
                .Add("implemented", item.Implemented).Add("parametersBound", item.ParametersBound)
                .Add("yearBc", item.HasYear ? Num(item.YearBc) : Null())
                .Add("sector", item.Sector.ToString()).Add("sectorMeters", item.SectorMeters)
                .Add("sides", Arr((item.Sides ?? Array.Empty<ScenarioSide>()).Where(side => side != null).Select(side => Obj()
                    .Add("slot", side.Slot).Add("faction", side.Faction.ToString()).Add("controller", side.Controller.ToString()))))
                .Add("hostilePairs", HostilePairs(item.HostilePairs))
                .Add("success", item.Success?.Text())
                .Add("carry", carry == null ? Null() : Obj()
                    .Add("flagship", carry.Flagship).Add("flagshipArtifacts", carry.FlagshipArtifacts).Add("heroes", carry.Heroes)
                    .Add("evacuatedStore", carry.EvacuatedStore).Add("extraManifest", Texts(carry.ExtraManifest)))
                .Add("source", Source(item.Source));
        }

        // ---------- Shared parts ----------

        /// <summary>
        /// Whether the running game has the thing, what it asks for today and what the documents plan. "enforced" is null
        /// for a definition that is not implemented: nothing is asked of what cannot be had.
        /// </summary>
        static Node Availability(Node node, Availability availability)
        {
            node.Add("implemented", availability.Implemented);
            node.Add("enforced", availability.Implemented
                ? Obj().Add("buildings", Buildings(availability.Buildings)).Add("technologies", Texts(availability.Technologies)).Add("homePortOnly", availability.HomePortOnly)
                : Null());
            node.Add("target", Obj().Add("buildings", Buildings(availability.TargetBuildings)).Add("technologies", Texts(availability.TargetTechnologies)));
            node.Add("faction", availability.Faction.ToString());
            node.Add("ownerSlots", Arr(Enumerable.Range(0, Owners.SlotCount).Where(slot => (availability.OwnerSlots & (1 << slot)) != 0).Select(slot => Num(slot))));
            node.Add("date", Window(availability.Date));
            node.Add("rulesets", Texts(availability.Rulesets));
            return node;
        }

        static Node Source(SourceNote note) => note == null
            ? Null()
            : Obj().Add("status", SourceNote.Code(note.Status)).Add("layer", note.Layer.ToString()).Add("locator", note.Locator);

        static Node Window(DateWindow window) => window == null ? Null() : Obj().Add("earliestYearBc", window.EarliestYearBc).Add("latestYearBc", window.LatestYearBc);

        /// <summary>A price the catalogue keeps in whole units, written in hundredths.</summary>
        static Node Cost(long woodUnits, long silverUnits) => CentiPair(checked(woodUnits * Rules.Centi), checked(silverUnits * Rules.Centi));

        static Node CentiPair(long woodCenti, long silverCenti) => Obj().Add("wood", woodCenti).Add("silver", silverCenti);

        /// <summary>A whole number of units per second as hundredths per second.</summary>
        static long Centi(double units)
        {
            var centi = units * Rules.Centi;
            if (centi != Math.Floor(centi)) throw new InvalidOperationException("Not a whole number of hundredths.");
            return (long)centi;
        }

        /// <summary>The buildings of a set by name, in the order of their bits.</summary>
        static Node Buildings(PortBuildingSet set) =>
            Texts(((PortBuildingSet[])Enum.GetValues(typeof(PortBuildingSet))).Where(building => building != PortBuildingSet.None && (set & building) == building).Select(building => building.ToString()));

        /// <summary>The pairs of owner slots at war, each as its two slots, in the order of SideRelations' bits.</summary>
        static Node HostilePairs(int pairs)
        {
            var relations = new SideRelations(pairs);
            var list = new List<Node>();
            for (var a = 0; a < Owners.SlotCount; a++)
                for (var b = a + 1; b < Owners.SlotCount; b++)
                    if (relations.IsHostile(a, b)) list.Add(Arr(new[] { Num(a), Num(b) }));
            return Arr(list);
        }

        static Node Parameters(IEnumerable<Parameter> parameters) =>
            Arr((parameters ?? Array.Empty<Parameter>()).Where(parameter => parameter != null).Select(parameter => Obj().Add("name", parameter.Name).Add("value", parameter.Value)));

        // ---------- The JSON text ----------

        static Node Obj() => new Node { Members = new List<KeyValuePair<string, Node>>() };
        static Node Arr(IEnumerable<Node> items) => new Node { Items = items.ToList() };
        static Node Texts(IEnumerable<string> values) => Arr((values ?? Array.Empty<string>()).Select(Text));
        static Node Null() => new Node { Literal = "null" };
        static Node Num(long value) => new Node { Literal = value.ToString(CultureInfo.InvariantCulture) };

        static Node Num(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidOperationException("Not a number JSON can hold.");
            // A whole number is written as one, so the text does not depend on how a runtime prints 60 against 60.0.
            if (value == Math.Floor(value) && Math.Abs(value) < 1e15) return Num((long)value);
            return new Node { Literal = CanonicalWriter.Number(value) };
        }

        static Node Text(string value)
        {
            if (value == null) return Null();
            var text = new StringBuilder(value.Length + 2).Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (character < ' ') text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else text.Append(character);
                        break;
                }
            }
            return new Node { Literal = text.Append('"').ToString() };
        }

        /// <summary>A value of the text: an object with its keys in the order they were added, a list, or a literal.</summary>
        sealed class Node
        {
            public string Literal;
            public List<KeyValuePair<string, Node>> Members;
            public List<Node> Items;

            public Node Add(string key, Node value)
            {
                foreach (var member in Members)
                    if (member.Key == key) throw new InvalidOperationException("The key " + key + " twice in one object.");
                Members.Add(new KeyValuePair<string, Node>(key, value ?? Null()));
                return this;
            }

            public Node Add(string key, string value) => Add(key, Text(value));
            public Node Add(string key, bool value) => Add(key, new Node { Literal = value ? "true" : "false" });
            public Node Add(string key, long value) => Add(key, Num(value));
            public Node Add(string key, double value) => Add(key, Num(value));

            bool IsScalar => Literal != null;

            public void Write(StringBuilder text, int depth)
            {
                if (Literal != null) { text.Append(Literal); return; }
                if (Members != null)
                {
                    if (Members.Count == 0) { text.Append("{}"); return; }
                    text.Append("{\n");
                    for (var i = 0; i < Members.Count; i++)
                    {
                        Indent(text, depth + 1);
                        text.Append(Text(Members[i].Key).Literal).Append(": ");
                        Members[i].Value.Write(text, depth + 1);
                        text.Append(i + 1 < Members.Count ? ",\n" : "\n");
                    }
                    Indent(text, depth);
                    text.Append('}');
                    return;
                }
                if (Items.Count == 0) { text.Append("[]"); return; }
                // A list of plain values stays on one line; a list of objects or lists takes a line an item.
                if (Items.All(item => item.IsScalar))
                {
                    text.Append('[');
                    for (var i = 0; i < Items.Count; i++) { if (i > 0) text.Append(", "); Items[i].Write(text, depth); }
                    text.Append(']');
                    return;
                }
                text.Append("[\n");
                for (var i = 0; i < Items.Count; i++)
                {
                    Indent(text, depth + 1);
                    Items[i].Write(text, depth + 1);
                    text.Append(i + 1 < Items.Count ? ",\n" : "\n");
                }
                Indent(text, depth);
                text.Append(']');
            }

            static void Indent(StringBuilder text, int depth) => text.Append(' ', depth * 2);
        }
    }
}
