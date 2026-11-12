using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using MorzaBogow.Simulation;
using MorzaBogow.Simulation.Catalog;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// P02-D: the catalogue written out as text for the tools outside the game (CatalogExport.ToJson). The text is read
    /// back here by a small reader of its own, so every test asks the text what it says, not the code what it meant.
    /// Nothing here reads or writes a file.
    /// </summary>
    public sealed class CatalogExportTests
    {
        // ---------- A reader of JSON, enough for the export: objects keep the order of their keys ----------

        sealed class JsonObject
        {
            public readonly List<KeyValuePair<string, object>> Members = new List<KeyValuePair<string, object>>();
            public IEnumerable<string> Keys => Members.Select(member => member.Key);
            public bool Has(string key) => Members.Any(member => member.Key == key);

            public object this[string key]
            {
                get
                {
                    var found = Members.Where(member => member.Key == key).ToList();
                    if (found.Count != 1) throw new KeyNotFoundException($"The key {key} is there {found.Count} times.");
                    return found[0].Value;
                }
            }

            public JsonObject Obj(string key) => (JsonObject)this[key];
            public List<object> List(string key) => (List<object>)this[key];
            public string Text(string key) => (string)this[key];
            public long Whole(string key) => (long)this[key];
            public bool Flag(string key) => (bool)this[key];
            public double Number(string key) => this[key] is long whole ? whole : (double)this[key];
            public IEnumerable<string> Texts(string key) => List(key).Cast<string>();
            public IEnumerable<JsonObject> Objects(string key) => List(key).Cast<JsonObject>();
        }

        sealed class JsonReader
        {
            readonly string text;
            int at;

            JsonReader(string text) { this.text = text; }

            public static JsonObject Parse(string text)
            {
                var reader = new JsonReader(text);
                var value = reader.Value();
                reader.Space();
                if (reader.at != text.Length) throw new FormatException("Something after the end of the value at " + reader.at);
                return (JsonObject)value;
            }

            void Space() { while (at < text.Length && (text[at] == ' ' || text[at] == '\n')) at++; }

            void Expect(char character)
            {
                if (at >= text.Length || text[at] != character) throw new FormatException($"Expected {character} at {at}");
                at++;
            }

            object Value()
            {
                Space();
                var character = text[at];
                if (character == '{')
                {
                    at++;
                    var result = new JsonObject();
                    Space();
                    if (text[at] == '}') { at++; return result; }
                    while (true)
                    {
                        Space();
                        var key = Quoted();
                        Space();
                        Expect(':');
                        if (result.Has(key)) throw new FormatException("The key " + key + " twice in one object.");
                        result.Members.Add(new KeyValuePair<string, object>(key, Value()));
                        Space();
                        if (text[at] == ',') { at++; continue; }
                        Expect('}');
                        return result;
                    }
                }
                if (character == '[')
                {
                    at++;
                    var result = new List<object>();
                    Space();
                    if (text[at] == ']') { at++; return result; }
                    while (true)
                    {
                        result.Add(Value());
                        Space();
                        if (text[at] == ',') { at++; continue; }
                        Expect(']');
                        return result;
                    }
                }
                if (character == '"') return Quoted();
                if (Word("true")) return true;
                if (Word("false")) return false;
                if (Word("null")) return null;
                var start = at;
                while (at < text.Length && (char.IsDigit(text[at]) || text[at] == '-' || text[at] == '+' || text[at] == '.' || text[at] == 'e' || text[at] == 'E')) at++;
                var number = text.Substring(start, at - start);
                if (number.Length == 0) throw new FormatException("No value at " + start);
                if (number.All(digit => char.IsDigit(digit) || digit == '-')) return long.Parse(number, CultureInfo.InvariantCulture);
                return double.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            bool Word(string word)
            {
                if (string.CompareOrdinal(text, at, word, 0, word.Length) != 0) return false;
                at += word.Length;
                return true;
            }

            string Quoted()
            {
                Expect('"');
                var result = new StringBuilder();
                while (text[at] != '"')
                {
                    var character = text[at++];
                    if (character < ' ') throw new FormatException("A bare control character in a text at " + at);
                    if (character != '\\') { result.Append(character); continue; }
                    var escaped = text[at++];
                    switch (escaped)
                    {
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append((char)int.Parse(text.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); at += 4; break;
                        case '"': case '\\': case '/': result.Append(escaped); break;
                        default: throw new FormatException("An unknown escape at " + at);
                    }
                }
                at++;
                return result.ToString();
            }
        }

        static readonly string[] DefinitionLists =
        {
            "ships", "hullProfiles", "weaponFamilies", "specialWeapons", "upgradeLevels", "technologies", "forts", "abilities", "heroes", "artifacts",
            "encounters", "scenarios", "tasks", "aiProfiles", "reinforcements",
        };

        /// <summary>The lists whose definitions say when they may be used.</summary>
        static readonly string[] ListsWithAvailability = { "ships", "technologies", "forts", "abilities", "heroes", "artifacts", "encounters" };

        static JsonObject Export(GameCatalog catalog) => JsonReader.Parse(CatalogExport.ToJson(catalog));

        static IEnumerable<JsonObject> Definitions(JsonObject export) => DefinitionLists.SelectMany(export.Objects);

        static JsonObject ById(JsonObject export, string list, string id) => export.Objects(list).Single(item => item.Text("id") == id);

        static (long wood, long silver) Pair(JsonObject cost) => (cost.Whole("wood"), cost.Whole("silver"));

        // ---------- The form ----------

        [Test]
        public void Export_IsDeterministicAndCarriesTheCataloguesIdentity()
        {
            var first = CatalogExport.ToJson(GameCatalog.Create());
            Assert.That(CatalogExport.ToJson(GameCatalog.Create()), Is.EqualTo(first), "Two catalogues built anew give the same text.");
            Assert.That(CatalogExport.ToJson(GameCatalog.Default), Is.EqualTo(first), "And the game's own gives it too.");

            // One form everywhere: LF only, a new line at the end, no mark before the first brace, no tab.
            Assert.That(first[0], Is.EqualTo('{'));
            Assert.That(first, Does.EndWith("}\n").And.Not.EndWith("\n\n"));
            Assert.That(first.IndexOf('\r'), Is.EqualTo(-1));
            Assert.That(first.IndexOf('\t'), Is.EqualTo(-1));
            Assert.That(first.IndexOf('\uFEFF'), Is.EqualTo(-1));
            var bytes = new UTF8Encoding(false).GetBytes(first);
            Assert.That(new UTF8Encoding(false, true).GetString(bytes), Is.EqualTo(first), "It survives being saved as UTF-8 without a mark.");

            // A machine that writes numbers with a comma gives the same text.
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                comma.NumberFormat.NumberDecimalSeparator = ",";
                Thread.CurrentThread.CurrentCulture = comma;
                Assert.That((0.5).ToString(), Is.EqualTo("0,5"), "The trial culture really writes a comma.");
                Assert.That(CatalogExport.ToJson(GameCatalog.Create()), Is.EqualTo(first));
            }
            finally { Thread.CurrentThread.CurrentCulture = culture; }

            var export = JsonReader.Parse(first);
            Assert.That(export.Keys, Is.EqualTo(new[]
            {
                "schema", "catalogId", "catalogHash", "units", "fields", "sourceStatuses", "worldLayers", "rules", "ships", "hullProfiles", "weaponFamilies", "specialWeapons",
                "upgradeLevels", "technologies", "forts", "abilities", "heroes", "artifacts", "encounters", "scenarios", "tasks", "aiProfiles", "reinforcements", "effects",
            }), "The keys, in their one order.");
            Assert.That(export.Text("schema"), Is.EqualTo("morza-bogow/runtime-catalog@1"));
            Assert.That(export.Text("catalogId"), Is.EqualTo(GameCatalog.CatalogId));
            Assert.That(export.Text("catalogHash"), Is.EqualTo(GameCatalog.Default.Hash().ToString("X16", CultureInfo.InvariantCulture)));
            Assert.That(export.Text("catalogId") + "#" + export.Text("catalogHash"), Is.EqualTo(GameCatalog.Default.Identity()));
            Assert.That(export.Obj("units").Keys, Is.EqualTo(new[] { "wood", "silver", "rate", "time", "distance", "share" }));
            Assert.That(export.Obj("units").Text("wood"), Does.StartWith("centi-D"));
            Assert.That(export.Obj("units").Text("silver"), Does.StartWith("centi-S"));
            // The text says of itself which rates the match profile's yield scales (work at a point) and which it does
            // not (handling in a harbour), and how far "enforced" reaches: buildings and researches, not the other
            // conditions of founding a settlement or building a shipyard.
            Assert.That(export.Obj("units").Text("rate"), Does.Contain("runtime.workRate and rules.node.sharedRate are multiplied").And.Contain("rules.harbour.handlingRate is not"));
            Assert.That(export.Obj("fields").Keys, Is.EqualTo(new[] { "implemented", "enforced", "target", "runtime", "provisional" }));
            Assert.That(export.Obj("fields").Text("enforced"), Does.Contain("buildings of the port and researches of the side only").And.Contain("B01").And.Contain("B03").And.Contain("not expressed here"));
            Assert.That(export.Texts("sourceStatuses"),
                Is.EqualTo(new[] { "POTWIERDZONE_W_ZAKRESIE", "REKONSTRUKCJA", "TRADYCJA_MITOLOGICZNA", "REGUŁA_GRY", "NIEUSTALONE", "BLOCKED_RESEARCH" }));
            Assert.That(export.Texts("worldLayers"), Is.EqualTo(new[] { "Material", "Mythic", "GameRule" }));

            // The division of effects is a working one, and the text says so itself, not only a comment in the code.
            var effects = export.Obj("effects");
            Assert.That(effects.Text("divisionStatus"), Is.EqualTo("WORKING_UNTIL_P07"));
            Assert.That(effects.Objects("refs").Select(item => (item.Text("ownerId"), item.Text("effectId"))), Is.EqualTo(GameCatalog.Default.AllEffectRefs()));
            Assert.That(effects.Objects("refs").Select(item => item.Text("effectId")), Is.SupersetOf(new[] { "M01.ZONE", "M11", "Z-WIND.SPOUT" }));
        }

        [Test]
        public void Export_EveryDefinitionIsThereOnceWithItsSourceInTheProtocolsWords()
        {
            var catalog = GameCatalog.Default;
            var export = Export(catalog);
            var definitions = Definitions(export).ToList();

            Assert.That(definitions.Select(item => item.Text("id")), Is.EqualTo(catalog.AllIds()), "Every ID of the catalogue, once, in the catalogue's order.");
            Assert.That(definitions.Select(item => item.Text("id")).Distinct().Count(), Is.EqualTo(definitions.Count));
            Assert.That(definitions.Count, Is.EqualTo(170));
            Assert.That(DefinitionLists.Select(list => export.List(list).Count), Is.EqualTo(new[] { 12, 3, 6, 2, 6, 52, 3, 12, 9, 20, 19, 18, 5, 3, 0 }));

            // The status in the protocol's own word, the layer beside it and never inside it, the locator whole.
            var notes = catalog.AllSources().ToDictionary(item => item.id, item => item.note);
            foreach (var definition in definitions)
            {
                var id = definition.Text("id");
                var source = definition.Obj("source");
                Assert.That(source.Keys, Is.EqualTo(new[] { "status", "layer", "locator" }), id);
                Assert.That(source.Text("status"), Is.EqualTo(SourceNote.Code(notes[id].Status)), id);
                Assert.That(source.Text("layer"), Is.EqualTo(notes[id].Layer.ToString()), id);
                Assert.That(source.Text("locator"), Is.EqualTo(notes[id].Locator), id);
                Assert.That(definition.Text("name"), Is.Not.Null.And.Not.Empty, id);
            }
            Assert.That(ById(export, "ships", "N03").Obj("source").Text("status"), Is.EqualTo("REGUŁA_GRY"));
            Assert.That(ById(export, "weaponFamilies", "C03-4").Obj("source").Text("status"), Is.EqualTo("NIEUSTALONE"));
            Assert.That(ById(export, "abilities", "M10").Obj("source").Text("status"), Is.EqualTo("TRADYCJA_MITOLOGICZNA"));
            var threes = ById(export, "hullProfiles", "HULL-TRIERES");
            Assert.That((threes.Obj("source").Text("status"), threes.Obj("source").Text("layer"), threes["date"]), Is.EqualTo(("POTWIERDZONE_W_ZAKRESIE", "Material", (object)null)));
            Assert.That(threes.Obj("source").Text("locator"), Does.Contain("„The Piraeus").And.Contain("378/7–323/2"), "Text beyond ASCII comes through as it is.");
            Assert.That(ById(export, "hullProfiles", "HULL-TETRERES").Obj("date").Whole("earliestYearBc"), Is.EqualTo(330));

            // What is a first guess for trials says so: the five planned roles and the five flotilla configurations.
            var provisional = definitions.Where(item => item.Has("provisional") && item.Flag("provisional")).Select(item => item.Text("id"));
            Assert.That(provisional, Is.EqualTo(new[] { "N08", "N09", "N10", "N11", "N12", "FL1", "FL2", "FL3", "FL4", "FL5" }));
            Assert.That(export.Objects("ships").Concat(export.Objects("encounters")).All(item => item.Has("provisional")), Is.True);
        }

        // ---------- The numbers the running game really uses ----------

        [Test]
        public void Export_CostsOfTheSevenClassesAndTheFiveProjectsAreTheRunningGamesInHundredths()
        {
            var export = Export(GameCatalog.Default);

            // N01-N07: the block "runtime" is the class of the running game, in hundredths; the planned roles have none.
            var roles = new[] { ("N01", ShipClass.Cutter), ("N02", ShipClass.Merchant), ("N03", ShipClass.Ram), ("N04", ShipClass.CrewDestroyer),
                ("N05", ShipClass.ShipKiller), ("N06", ShipClass.Bomber), ("N07", ShipClass.Boarder) };
            Assert.That(roles.Select(role => role.Item2), Is.EquivalentTo((ShipClass[])Enum.GetValues(typeof(ShipClass))), "All the classes of the running game.");
            foreach (var (id, shipClass) in roles)
            {
                var definition = ShipCatalog.Get(shipClass);
                var ship = ById(export, "ships", id);
                var runtime = ship.Obj("runtime");
                Assert.That(runtime.Text("class"), Is.EqualTo(shipClass.ToString()), id);
                Assert.That(Pair(runtime.Obj("cost")), Is.EqualTo((definition.CostWood * 100L, definition.CostSilver * 100L)), id);
                Assert.That(Pair(ship.Obj("cost")), Is.EqualTo(Pair(runtime.Obj("cost"))), id + ": the catalogue's price is the running game's");
                Assert.That((runtime.Number("buildSeconds"), ship.Number("buildSeconds")), Is.EqualTo((definition.BuildSeconds, definition.BuildSeconds)), id);
                Assert.That((runtime.Whole("maxCrew"), runtime.Number("speed"), runtime.Number("hull")), Is.EqualTo(((long)definition.MaxCrew, definition.Speed, definition.Hull)), id);
                Assert.That(runtime.Whole("capacity"), Is.EqualTo(definition.Capacity * 100L), id);
                Assert.That(Pair(runtime.Obj("workRate")), Is.EqualTo((definition.WoodWork * 100L, definition.SilverWork * 100L)), id);

                // The parts of the price are those the running game charges, and together they are the whole price (E04).
                var parts = runtime.Obj("priceParts");
                Assert.That(parts.Whole("constructionWood"), Is.EqualTo(TemplatePricing.ConstructionWood(definition)), id);
                Assert.That(parts.Whole("constructionSilver"), Is.EqualTo(TemplatePricing.ConstructionSilver(definition)), id);
                Assert.That(parts.Whole("crewSilver"), Is.EqualTo(TemplatePricing.CrewSilver(definition)), id);
                Assert.That(parts.Whole("constructionWood") + parts.Whole("equipmentWood"), Is.EqualTo(runtime.Obj("cost").Whole("wood")), id);
                Assert.That(parts.Whole("constructionSilver") + parts.Whole("equipmentSilver") + parts.Whole("crewSilver"), Is.EqualTo(runtime.Obj("cost").Whole("silver")), id);
                Assert.That(parts.Members.Select(member => (long)member.Value), Is.All.GreaterThanOrEqualTo(0L), id + ": no part of a price is negative (Q03)");

                // The shares the text gives under rules.pricing are the ones those parts were really made with.
                var pricing = export.Obj("rules").Obj("pricing");
                Assert.That(runtime.Whole("sailors") + runtime.Whole("fighters"), Is.EqualTo(runtime.Whole("maxCrew")), id);
                Assert.That(parts.Whole("crewSilver"), Is.EqualTo(runtime.Whole("sailors") * pricing.Whole("sailorSilver") + runtime.Whole("fighters") * pricing.Whole("fighterSilver")), id);
                if (!definition.IsEconomic) Assert.That(runtime.Whole("sailors"), Is.EqualTo((long)Math.Ceiling(runtime.Whole("maxCrew") * pricing.Number("warshipSailorShare"))), id);
                Assert.That(parts.Whole("constructionWood"), Is.EqualTo((long)Math.Round(runtime.Obj("cost").Whole("wood") * pricing.Number("constructionWoodShare"))), id);
                Assert.That(parts.Whole("constructionSilver"),
                    Is.EqualTo((long)Math.Floor((runtime.Obj("cost").Whole("silver") - parts.Whole("crewSilver")) * pricing.Number("constructionSilverShare") + 1e-6)), id);
            }
            Assert.That(export.Objects("ships").Where(item => item["runtime"] != null).Select(item => item.Text("id")), Is.EqualTo(roles.Select(role => role.Item1)));
            Assert.That(ById(export, "ships", "N01").Obj("runtime").Obj("cost").Whole("wood"), Is.EqualTo(6000), "A cutter costs 60 units of wood: 6000 hundredths.");

            // The five projects: what World.ProjectCost charges (it answers in hundredths already) and how long it takes.
            var projects = new[] { ("B04", ProjectKind.Workshop), ("B06", ProjectKind.Town), ("B07", ProjectKind.Dock), ("B08", ProjectKind.SageSchool), ("TECH-HEAVY-ARMS", ProjectKind.HeavyArmsResearch) };
            Assert.That(projects.Select(project => project.Item2), Is.EquivalentTo(((ProjectKind[])Enum.GetValues(typeof(ProjectKind))).Where(kind => kind != ProjectKind.None)));
            foreach (var (id, kind) in projects)
            {
                var (wood, silver, seconds) = World.ProjectCost(kind);
                var technology = ById(export, "technologies", id);
                var runtime = technology.Obj("runtime");
                Assert.That(runtime.Text("project"), Is.EqualTo(kind.ToString()), id);
                Assert.That(Pair(runtime.Obj("cost")), Is.EqualTo((wood, silver)), id);
                Assert.That(runtime.Number("seconds"), Is.EqualTo(seconds), id);
                Assert.That(Pair(technology.Obj("cost")), Is.EqualTo(Pair(runtime.Obj("cost"))), id + ": the catalogue's price is the running game's");
                Assert.That(technology.Number("seconds"), Is.EqualTo(seconds), id);
            }
            Assert.That(export.Objects("technologies").Where(item => item["runtime"] != null).Select(item => item.Text("id")), Is.EquivalentTo(projects.Select(project => project.Item1)));
            Assert.That(Pair(ById(export, "technologies", "B08").Obj("runtime").Obj("cost")), Is.EqualTo((20000L, 50000L)), "The school: 200 wood and 500 silver, in hundredths.");

            // The rules the economy model reads, as the running game holds them.
            var rules = export.Obj("rules");
            Assert.That((rules.Whole("centi"), rules.Whole("tickRate"), rules.Whole("fleetLimit"), rules.Whole("convoySize")),
                Is.EqualTo((Rules.Centi, (long)Rules.TickRate, (long)Rules.FleetLimit, (long)Rules.ConvoySize)));
            Assert.That(Pair(rules.Obj("reserve")), Is.EqualTo((Rules.ReserveWood, Rules.ReserveSilver)));
            Assert.That(Pair(rules.Obj("warehouse")), Is.EqualTo((Rules.WarehouseWood, Rules.WarehouseSilver)));
            Assert.That((rules.Obj("harbour").Whole("berths"), rules.Obj("harbour").Whole("handlingRate")), Is.EqualTo(((long)Rules.PortBerths, (long)(Rules.HandlingPerSecond * 100))));
            Assert.That(rules.Obj("node").Whole("workBerths"), Is.EqualTo(Rules.WorkBerths));
            Assert.That(Pair(rules.Obj("node").Obj("sharedRate")), Is.EqualTo((Rules.WoodNodeCap * 100L, Rules.SilverNodeCap * 100L)));
            Assert.That(Pair(rules.Obj("outpost").Obj("cost")), Is.EqualTo((Rules.OutpostWood, Rules.OutpostSilver)));
            Assert.That(rules.Obj("outpost").Number("seconds"), Is.EqualTo(Rules.OutpostBuildSeconds));
            Assert.That(Pair(rules.Obj("shipyard").Obj("cost")), Is.EqualTo((Rules.ShipyardWood, Rules.ShipyardSilver)));
            Assert.That(rules.Obj("shipyard").Number("seconds"), Is.EqualTo(Rules.ShipyardBuildSeconds));
            // The settlement and the shipyard are no projects, yet the catalogue prices them as the running game does.
            Assert.That(Pair(ById(export, "technologies", "B01").Obj("cost")), Is.EqualTo(Pair(rules.Obj("outpost").Obj("cost"))));
            Assert.That(Pair(ById(export, "technologies", "B03").Obj("cost")), Is.EqualTo(Pair(rules.Obj("shipyard").Obj("cost"))));

            // The stock a side starts with is what a new match really puts into its port.
            foreach (var profile in rules.Objects("matchProfiles"))
            {
                var length = (MatchLength)Enum.Parse(typeof(MatchLength), profile.Text("length"));
                var world = MapFactory.CreateSkirmish(11, length);
                var home = world.Settlements.First(port => port.Owner == Owners.Human);
                Assert.That(Pair(profile.Obj("start")), Is.EqualTo((home.Wood, home.Silver)), profile.Text("length"));
                Assert.That((profile.Number("woodYield"), profile.Number("silverYield")), Is.EqualTo((MatchProfile.Of(length).WoodYield, MatchProfile.Of(length).SilverYield)));
            }
            Assert.That(rules.Objects("matchProfiles").Select(profile => profile.Text("length")), Is.EqualTo(Enum.GetNames(typeof(MatchLength))));

            // Every amount of wood and silver anywhere in the text is a whole number of hundredths, never negative,
            // and every price the catalogue keeps in whole units is a multiple of a hundred.
            var amounts = 0;
            void Walk(object value, string path)
            {
                if (value is JsonObject node)
                {
                    foreach (var member in node.Members)
                    {
                        if (member.Key == "wood" || member.Key == "silver")
                        {
                            if (path.EndsWith("units")) continue;
                            Assert.That(member.Value, Is.InstanceOf<long>(), path + "." + member.Key);
                            Assert.That((long)member.Value, Is.GreaterThanOrEqualTo(0L), path + "." + member.Key);
                            if (path.EndsWith("cost") || path.EndsWith("reward") || path.EndsWith("paid")) Assert.That((long)member.Value % 100, Is.EqualTo(0L), path + "." + member.Key);
                            amounts++;
                        }
                        else Walk(member.Value, path + "." + member.Key);
                    }
                }
                else if (value is List<object> list) foreach (var item in list) Walk(item, path);
            }
            Walk(export, "");
            Assert.That(amounts, Is.GreaterThan(200), "The walk really went through the prices.");
        }

        // ---------- Enforced and planned ----------

        [Test]
        public void Export_WhatIsEnforcedIsToldApartFromWhatIsPlanned()
        {
            var catalog = GameCatalog.Default;
            var export = Export(catalog);
            var withAvailability = ListsWithAvailability.SelectMany(export.Objects).ToList();
            Assert.That(withAvailability.Count, Is.EqualTo(12 + 52 + 3 + 12 + 9 + 20 + 19));

            // What the running game does not have asks for nothing today: no "enforced" at all, only a plan.
            foreach (var definition in withAvailability)
            {
                var id = definition.Text("id");
                Assert.That(definition["enforced"] != null, Is.EqualTo(definition.Flag("implemented")), id);
                Assert.That(definition.Obj("target").Keys, Is.EqualTo(new[] { "buildings", "technologies" }), id);
                if (definition.Flag("implemented")) Assert.That(definition.Obj("enforced").Keys, Is.EqualTo(new[] { "buildings", "technologies", "homePortOnly" }), id);
            }
            var implemented = withAvailability.Where(item => item.Flag("implemented")).Select(item => item.Text("id"));
            Assert.That(implemented, Is.EqualTo(new[] { "N01", "N02", "N03", "N04", "N05", "N06", "N07", "B01", "B03", "B04", "B06", "B07", "B08", "TECH-HEAVY-ARMS" }),
                "The seven classes, the settlement, the shipyard and the five projects: what the running game has.");

            // The siege ship: today a shipyard, a workshop and the heavy arms; by the plan a town and a school as well,
            // and the second armour. Buildings by name, in the order of the letters O P S W K M U.
            var siege = ById(export, "ships", "N06");
            Assert.That(siege.Obj("enforced").Texts("buildings"), Is.EqualTo(new[] { "Shipyard", "Workshop" }));
            Assert.That(siege.Obj("enforced").Texts("technologies"), Is.EqualTo(new[] { "TECH-HEAVY-ARMS" }));
            Assert.That(siege.Obj("target").Texts("buildings"), Is.EqualTo(new[] { "Shipyard", "Workshop", "Town", "Knowledge" }));
            Assert.That(siege.Obj("target").Texts("technologies"), Is.EqualTo(new[] { "TECH-HEAVY-ARMS", "TECH-ARMOR-2" }));

            // The merchant: today nothing is asked, the plan asks for a town (the difference P04 is to close).
            var merchant = ById(export, "ships", "N02");
            Assert.That(merchant.Obj("enforced").Texts("buildings"), Is.Empty);
            Assert.That(merchant.Obj("target").Texts("buildings"), Is.EqualTo(new[] { "Shipyard", "Town" }));

            // The town: today a shipyard and a workshop at the home port; by the plan a harbour and a forge too.
            var town = ById(export, "technologies", "B06");
            Assert.That(town.Obj("enforced").Texts("buildings"), Is.EqualTo(new[] { "Shipyard", "Workshop" }));
            Assert.That(town.Obj("enforced").Flag("homePortOnly"), Is.True);
            Assert.That(town.Obj("target").Texts("buildings"), Is.EqualTo(new[] { "Harbour", "Shipyard", "Workshop", "Forge" }));
            Assert.That(town.Texts("grants"), Is.EqualTo(new[] { "Town" }));

            // A planned role has a plan and a price of the parameters document, and no number of the running game.
            var command = ById(export, "ships", "N08");
            Assert.That((command.Flag("implemented"), command["enforced"], command["runtime"]), Is.EqualTo((false, (object)null, (object)null)));
            Assert.That(command.Obj("target").Texts("buildings"), Is.EqualTo(new[] { "Settlement", "Shipyard", "Workshop", "Forge" }));
            Assert.That(command.Obj("target").Texts("technologies"), Is.EqualTo(new[] { "TECH-COMMAND" }));
            Assert.That(Pair(command.Obj("cost")), Is.EqualTo((30000L, 20000L)));

            // What is enforced in the text is what the catalogue's rule of availability answers by: a port that has
            // exactly the enforced buildings and researches may build, and one that lacks any one of them may not.
            foreach (var variant in catalog.ShipVariants.Where(item => item.Availability.Implemented))
            {
                var enforced = ById(export, "ships", variant.Id).Obj("enforced");
                var buildings = enforced.Texts("buildings").Aggregate(PortBuildingSet.None, (set, name) => set | (PortBuildingSet)Enum.Parse(typeof(PortBuildingSet), name));
                var technologies = enforced.Texts("technologies").ToArray();
                AvailabilityContext With(PortBuildingSet has, string[] researched) => new AvailabilityContext { Buildings = has, Technologies = researched, OwnerSlot = Owners.Human };
                Assert.That(AvailabilityRules.Evaluate(variant.Availability, With(buildings, technologies)), Is.EqualTo(Unavailable.None), variant.Id);
                foreach (var missing in enforced.Texts("buildings"))
                {
                    var without = buildings & ~(PortBuildingSet)Enum.Parse(typeof(PortBuildingSet), missing);
                    Assert.That(AvailabilityRules.Evaluate(variant.Availability, With(without, technologies)), Is.EqualTo(Unavailable.MissingBuilding), variant.Id + " without " + missing);
                }
                if (technologies.Length > 0)
                    Assert.That(AvailabilityRules.Evaluate(variant.Availability, With(buildings, Array.Empty<string>())), Is.EqualTo(Unavailable.MissingTechnology), variant.Id);
            }

            // Who may have it, by slot and faction: the specialisations by faction, the threats in the independent slot.
            Assert.That(ById(export, "ships", "N09").Text("faction"), Is.EqualTo("A"));
            Assert.That(ById(export, "ships", "N12").Text("faction"), Is.EqualTo("B"));
            Assert.That(ById(export, "ships", "N03").List("ownerSlots"), Is.EqualTo(new object[] { 0L, 1L }));
            Assert.That(ById(export, "encounters", "Z04").List("ownerSlots"), Is.EqualTo(new object[] { 2L }));
            Assert.That(ById(export, "encounters", "E05")["ownerSlot"], Is.Null, "The neutral target belongs to nobody.");
            Assert.That(ById(export, "encounters", "Z04").Whole("ownerSlot"), Is.EqualTo(2));
        }

        // ---------- Sensitivity ----------

        [Test]
        public void Export_ChangingADefinitionChangesTheText()
        {
            var original = CatalogExport.ToJson(GameCatalog.Create());

            // Each change is made to a catalogue of its own; the text differs, and in the place that was changed.
            JsonObject Changed(Action<GameCatalog> change)
            {
                var catalog = GameCatalog.Create();
                change(catalog);
                var text = CatalogExport.ToJson(catalog);
                Assert.That(text, Is.Not.EqualTo(original));
                Assert.That(JsonReader.Parse(text).Text("catalogHash"), Is.Not.EqualTo(JsonReader.Parse(original).Text("catalogHash")), "The identity moves with the content.");
                return JsonReader.Parse(text);
            }

            // A price of the catalogue: its own "cost" moves, while "runtime" still says what the running game charges.
            var dearer = ById(Changed(c => c.Variant("N03").CostSilver = 999), "ships", "N03");
            Assert.That(dearer.Obj("cost").Whole("silver"), Is.EqualTo(99900));
            Assert.That(dearer.Obj("runtime").Obj("cost").Whole("silver"), Is.EqualTo(ShipCatalog.Get(ShipClass.Ram).CostSilver * 100L),
                "A catalogue that has drifted from the running game shows both numbers.");

            Assert.That(ById(Changed(c => c.Variant("N03").Availability.Buildings |= PortBuildingSet.Forge), "ships", "N03").Obj("enforced").Texts("buildings"),
                Is.EqualTo(new[] { "Shipyard", "Forge" }));
            Assert.That(ById(Changed(c => c.Variant("N03").Availability.TargetTechnologies = new[] { TechnologyIds.Ram2 }), "ships", "N03").Obj("target").Texts("technologies"),
                Is.EqualTo(new[] { "TECH-RAM-2" }));
            Assert.That(ById(Changed(c => c.Variant("N07").Availability.Implemented = false), "ships", "N07")["enforced"], Is.Null);
            Assert.That(ById(Changed(c => c.Variant("N08").Provisional = false), "ships", "N08").Flag("provisional"), Is.False);
            Assert.That(ById(Changed(c => c.Technology("B05").CostWood = 161), "technologies", "B05").Obj("cost").Whole("wood"), Is.EqualTo(16100));
            Assert.That(ById(Changed(c => c.Technology("B05").Seconds = 45.5), "technologies", "B05").Number("seconds"), Is.EqualTo(45.5));
            Assert.That(ById(Changed(c => c.Forts[1].PoolCost = 3), "forts", "F02").Whole("poolCost"), Is.EqualTo(3));
            Assert.That(ById(Changed(c => c.Ability("M08").CostSilver = 801), "abilities", "M08").Obj("cost").Whole("silver"), Is.EqualTo(80100));
            Assert.That(ById(Changed(c => c.Encounters.Single(item => item.Id == "E05").RewardWood = 961), "encounters", "E05").Obj("reward").Whole("wood"), Is.EqualTo(96100));

            // A source's status or layer: the protocol's word moves with it.
            var reread = ById(Changed(c => c.Ability("M01").Source.Status = SourceStatus.BlockedResearch), "abilities", "M01").Obj("source");
            Assert.That(reread.Text("status"), Is.EqualTo("BLOCKED_RESEARCH"));
            Assert.That(ById(Changed(c => c.Ability("M01").Source.Layer = WorldLayer.GameRule), "abilities", "M01").Obj("source").Text("layer"), Is.EqualTo("GameRule"));
            Assert.That(ById(Changed(c => c.Hero("H01").Source.Locator = "inna „karta”\n\tz nową linią \\ i ukośnikiem"), "heroes", "H01").Obj("source").Text("locator"),
                Is.EqualTo("inna „karta”\n\tz nową linią \\ i ukośnikiem"), "Marks of the form inside a text are escaped and come back whole.");

            // A definition added is a line added: a wave of reinforcements, of which the catalogue has none today.
            var wave = Changed(c => c.Reinforcements.Add(new ReinforcementManifest
            {
                Id = "W-TEST", ScenarioId = "K03", SourceId = "PortWroga", Units = new[] { new ManifestUnit { VariantId = "N03", Count = 2 } }, Trigger = ReinforcementTrigger.AtTick,
                TriggerArg = "5400", AnnounceSeconds = 30, PaidWood = 480, PaidSilver = 200,
                Source = new SourceNote { Status = SourceStatus.GameRule, Locator = "docs/MAPY_I_SCENARIUSZE.md §4 K03 (fala próbna testu)" },
            })).Objects("reinforcements").Single();
            Assert.That((wave.Text("id"), Pair(wave.Obj("paid")), wave.Objects("units").Single().Whole("count")), Is.EqualTo(("W-TEST", (48000L, 20000L), 2L)));
            Assert.That(wave.Obj("source").Text("layer"), Is.EqualTo("GameRule"), "A note whose layer was never set is a rule of the game, not a statement about the material world.");

            // And nothing of all that touched the game's own catalogue.
            Assert.That(CatalogExport.ToJson(GameCatalog.Default), Is.EqualTo(original));
            Assert.Throws<ArgumentNullException>(() => CatalogExport.ToJson(null));
        }

        /// <summary>
        /// The fields of the definitions that the export leaves out on purpose: prose for people, which the tools outside
        /// the game do not read. The catalogue's hash still covers every one of them (CatalogTests,
        /// Identity_ChangingAnyFieldOfAnyDefinitionChangesTheHash). A list of the catalogue, then the field.
        /// </summary>
        static readonly string[] LeftOutOnPurpose =
        {
            "ShipVariants.Summary", "Technologies.Summary", "Forts.Summary", "Abilities.Summary", "Heroes.Summary", "Artifacts.Summary", "Encounters.Summary",
            "Scenarios.Place", "Scenarios.Motif", "Scenarios.Goal", "Scenarios.NewRule", "Scenarios.FailureExample",
            "Tasks.EndCondition",
            // Not prose: branches hung on a leaf of a condition. A leaf has none; the notation of the scenario document
            // (Predicate.Text) has no place for them, and the catalogue's own check refuses such a condition.
            "Scenarios.Success.Children.Children",
        };

        /// <summary>The export without the catalogue's hash, which moves with any change at all and would hide a field left out.</summary>
        static string WithoutHash(string export)
        {
            var export16 = JsonReader.Parse(export).Text("catalogHash");
            var line = "\"catalogHash\": \"" + export16 + "\"";
            Assert.That(export, Does.Contain(line));
            return export.Replace(line, "\"catalogHash\": \"-\"");
        }

        /// <summary>
        /// A catalogue in which every field has a place to show in the text: a wave of reinforcements is there, the first
        /// and last definition of every list count as implemented (so that what they enforce is written), and the
        /// scenarios have a year.
        /// </summary>
        static GameCatalog WithEveryFieldShowing()
        {
            var catalog = CatalogTests.WithAWave();
            foreach (var (_, item) in CatalogTests.Roots(catalog))
            {
                if (item.GetType().GetField("Availability")?.GetValue(item) is Availability availability) availability.Implemented = true;
                if (item is ScenarioDefinition scenario) { scenario.HasYear = true; scenario.YearBc = 350; }
            }
            return catalog;
        }

        /// <summary>The list and the field of a change, without the place in the list: "ShipVariants[11].Slots+" is "ShipVariants.Slots".</summary>
        static string FieldOf(string path)
        {
            var field = new StringBuilder();
            var inIndex = false;
            foreach (var character in path)
            {
                if (character == '[') inIndex = true;
                else if (character == ']') inIndex = false;
                else if (!inIndex && character != '+') field.Append(character);
            }
            return field.ToString();
        }

        [Test]
        public void Export_EveryFieldOfEveryDefinitionShowsInTheTextOrIsLeftOutOnPurpose()
        {
            // Every public field of the first and the last definition of every list, however deep, is changed alone in
            // a catalogue of its own. A field shows when some such change moves the text (the hash set aside). A field
            // that never shows must be on the list above, and nothing on the list may show: a field added to a
            // definition and forgotten in the export fails here until it is exported or left out by name.
            var original = WithoutHash(CatalogExport.ToJson(WithEveryFieldShowing()));
            var paths = CatalogTests.Roots(WithEveryFieldShowing()).SelectMany(root => CatalogTests.Changes(root.item, root.name)).Select(change => change.path).ToList();
            Assert.That(paths.Count, Is.GreaterThan(500), "The fields of every kind of definition are walked.");

            var all = new SortedSet<string>(StringComparer.Ordinal);
            var showing = new SortedSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < paths.Count; i++)
            {
                var catalog = WithEveryFieldShowing();
                var change = CatalogTests.Roots(catalog).SelectMany(root => CatalogTests.Changes(root.item, root.name)).ElementAt(i);
                Assert.That(change.path, Is.EqualTo(paths[i]));
                change.change();
                var field = FieldOf(change.path);
                all.Add(field);
                if (WithoutHash(CatalogExport.ToJson(catalog)) != original) showing.Add(field);
            }

            var hidden = all.Where(field => !showing.Contains(field)).ToList();
            Assert.That(hidden, Is.EquivalentTo(LeftOutOnPurpose),
                "Not showing and not on the list: " + string.Join(", ", hidden.Except(LeftOutOnPurpose)) + ". On the list and showing: " + string.Join(", ", LeftOutOnPurpose.Where(showing.Contains)) + ".");
            Assert.That(LeftOutOnPurpose.Where(field => !all.Contains(field)), Is.Empty, "The list names only fields that exist.");

            // The walk reached every list of the catalogue and the parts they share.
            foreach (var list in new[] { "ShipVariants", "HullProfiles", "WeaponFamilies", "SpecialWeapons", "UpgradeLevels", "Technologies", "Forts", "Abilities", "Heroes",
                         "Artifacts", "Encounters", "Scenarios", "Tasks", "AiProfiles", "Reinforcements" })
                Assert.That(showing.Any(field => field.StartsWith(list + ".Id", StringComparison.Ordinal)), Is.True, list);
            Assert.That(showing, Is.SupersetOf(new[]
            {
                "ShipVariants.Availability.Buildings", "ShipVariants.Availability.TargetTechnologies", "ShipVariants.Source.Status", "ShipVariants.Source.Layer",
                "Forts.Availability.HomePortOnly", "Technologies.RuntimeProject", "Scenarios.YearBc", "Scenarios.Sides.Slot", "Scenarios.Carry.ExtraManifest",
                "Scenarios.Success.Kind", "Reinforcements.Units.Count", "HullProfiles.Date.EarliestYearBc", "Abilities.Parameters.Value",
            }));
        }

        [Test]
        public void Export_AFractionThatNeedsMoreThanFifteenDigitsIsRefusedAloud()
        {
            // Runtimes agree on fifteen digits and differ beyond them. A number that fifteen digits do not hold would be
            // one text in the editor and another in a build, so it is refused where it is written, not found later as a
            // difference of one byte.
            Assert.That(new[] { 0.1, 0.25, 1.15, 0.53, 0.03, 45.5, 1e-7, 123456789.125, -2.5 }.Select(CanonicalWriter.Number),
                Is.EqualTo(new[] { "0.1", "0.25", "1.15", "0.53", "0.03", "45.5", "1E-07", "123456789.125", "-2.5" }));
            foreach (var value in new[] { 1.0 / 3, 0.1 + 0.2, 2.0 / 3, Math.PI, 1e15 + 0.5, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                Assert.Throws<InvalidOperationException>(() => CanonicalWriter.Number(value), value.ToString("G17", CultureInfo.InvariantCulture));
            Assert.That(0.1 + 0.2, Is.Not.EqualTo(0.3), "The sum that does not come out even is really another number than 0.3.");

            // In a definition's own field and in a named parameter: neither the export nor the catalogue's identity is made.
            var third = GameCatalog.Create();
            third.Technology("B05").Seconds = 1.0 / 3;
            Assert.That(Assert.Throws<InvalidOperationException>(() => CatalogExport.ToJson(third)).Message, Does.Contain("fifteen digits"));
            Assert.Throws<InvalidOperationException>(() => third.CanonicalText());
            Assert.Throws<InvalidOperationException>(() => third.Identity());

            var parameter = GameCatalog.Create();
            parameter.Ability("M07").Parameters[0].Value = 1.0 / 3;
            Assert.Throws<InvalidOperationException>(() => CatalogExport.ToJson(parameter));
            Assert.Throws<InvalidOperationException>(() => parameter.Hash());

            // Every fraction the catalogue holds today is one that fifteen digits hold: the game's own is written.
            Assert.DoesNotThrow(() => CatalogExport.ToJson(GameCatalog.Default));
            Assert.DoesNotThrow(() => GameCatalog.Default.Identity());
        }

        [Test]
        public void Export_NegativeZeroIsWrittenAsZero()
        {
            // Runtimes differ in whether they print the sign of a negative zero; the catalogue writes one zero.
            var negativeZero = -1.0 * 0.0;
            Assert.That(1.0 / negativeZero, Is.EqualTo(double.NegativeInfinity), "The trial value really is the negative zero.");
            Assert.That(CanonicalWriter.Number(negativeZero), Is.EqualTo("0"));
            Assert.That(CanonicalWriter.Number(0.0), Is.EqualTo("0"));
            Assert.That(CanonicalWriter.Number(-0.5), Is.EqualTo("-0.5"), "A negative number keeps its sign.");

            // In a definition: the identity and the export are those of the same catalogue with a plain zero.
            var plain = GameCatalog.Create();
            var signed = GameCatalog.Create();
            Assert.That(plain.Technology("B02").Seconds, Is.EqualTo(0.0), "The harbour takes no time today: the field tried is a zero.");
            Assert.That(plain.UpgradeLevels[0].Value, Is.EqualTo(0.0));
            signed.Technology("B02").Seconds = negativeZero;
            signed.UpgradeLevels[0].Value = negativeZero;
            Assert.That(signed.CanonicalText(), Is.EqualTo(plain.CanonicalText()));
            Assert.That(signed.Identity(), Is.EqualTo(plain.Identity()));
            Assert.That(CatalogExport.ToJson(signed), Is.EqualTo(CatalogExport.ToJson(plain)));
        }

        // ---------- The files kept in qa ----------

        /// <summary>The root of the project: the nearest folder upwards from where the tests run that holds ProjectSettings and qa.</summary>
        static string ProjectRoot()
        {
            var start = System.IO.Directory.GetCurrentDirectory();
            for (var folder = new System.IO.DirectoryInfo(start); folder != null; folder = folder.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(folder.FullName, "ProjectSettings")) && System.IO.Directory.Exists(System.IO.Path.Combine(folder.FullName, "qa")))
                    return folder.FullName;
            Assert.Fail("No folder with ProjectSettings and qa at or above " + start + ": the tests must run inside the project.");
            return null;
        }

        static byte[] BytesOf(string repositoryPath)
        {
            var path = System.IO.Path.Combine(ProjectRoot(), repositoryPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Assert.That(System.IO.File.Exists(path), Is.True, repositoryPath + " is missing.");
            return System.IO.File.ReadAllBytes(path);
        }

        /// <summary>The bytes with every CR LF read as LF: a checkout may have turned the one into the other.</summary>
        static byte[] WithLf(byte[] bytes)
        {
            var result = new List<byte>(bytes.Length);
            for (var i = 0; i < bytes.Length; i++)
                if (!(bytes[i] == 13 && i + 1 < bytes.Length && bytes[i + 1] == 10)) result.Add(bytes[i]);
            return result.ToArray();
        }

        static string TextOf(string repositoryPath)
        {
            var bytes = WithLf(BytesOf(repositoryPath));
            Assert.That(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, Is.False, repositoryPath + " begins with a byte order mark.");
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        [Test]
        public void Export_TheFileInQaIsTheGamesCatalogueWrittenOut()
        {
            // The tools outside the game read the file, not the code: it must be the catalogue as it is now. The test
            // only reads. End of lines apart (a checkout may give CR LF), the file is the export byte for byte.
            const string refresh = "qa/runtime-catalog.json is not the game's catalogue written out. Write it again with MorzaBogow.Editor.CatalogExporter.Run "
                + "(menu Morza Bogów / Katalog / Zapisz eksport katalogu (qa), or Unity -batchmode -executeMethod MorzaBogow.Editor.CatalogExporter.Run) and commit the file.";
            Assert.That(CatalogExport.RepositoryPath, Is.EqualTo("qa/runtime-catalog.json"));
            var file = TextOf(CatalogExport.RepositoryPath);
            var export = CatalogExport.ToJson(GameCatalog.Default);

            Assert.That(file.IndexOf((char)13), Is.EqualTo(-1), "A bare CR in the file.");
            var saved = JsonReader.Parse(file);
            Assert.That(saved.Text("schema"), Is.EqualTo(CatalogExport.Schema), refresh);
            Assert.That(saved.Text("catalogId") + "#" + saved.Text("catalogHash"), Is.EqualTo(GameCatalog.Default.Identity()), refresh);
            if (file != export)
            {
                // Say where, so the difference need not be looked for in a hundred and fifty thousand characters.
                var shared = Math.Min(file.Length, export.Length);
                var at = 0;
                while (at < shared && file[at] == export[at]) at++;
                var line = file.Substring(0, at).Count(character => character == (char)10) + 1;
                Assert.Fail(refresh + " First difference in line " + line + " of the file.");
            }
        }

        [Test]
        public void Legacy_TheMapPinsTheTwoOldFilesAndNamesOnlyDefinitionsThatExist()
        {
            // qa/economy-baseline.json and qa/scenario-design.json were written before the catalogue. They are kept as
            // they are, marked LEGACY, and are not rules of the game. qa/legacy-map.json says which of their keys name a
            // definition of the catalogue. Whether the map covers every key was checked once, when it was written; here:
            // the two files are still the ones the map was written for, and the map names nothing that does not exist.
            const string baseline = "qa/economy-baseline.json", design = "qa/scenario-design.json";
            const string noId = "NO_CATALOG_ID", outOfFrame = "OUT_OF_PLAN_FRAME";
            var map = JsonReader.Parse(TextOf("qa/legacy-map.json"));
            Assert.That(map.Text("schema"), Is.EqualTo("morza-bogow/legacy-map@1"));
            Assert.That(map.Text("catalogId"), Is.EqualTo(GameCatalog.CatalogId));
            Assert.That(map.Texts("markers"), Is.EqualTo(new[] { noId, outOfFrame }));
            // The marker says what the rule below enforces: no implemented definition answers, not "no definition at all".
            Assert.That(map.Text("markerMeaning"), Does.Contain("NO_CATALOG_ID: no implemented definition of the catalogue answers to the key."));
            Assert.That(map.Text("fieldMeaning"), Does.Contain("candidateId (optional): a planned, not yet implemented definition").And.Contain("not a mapping"));

            var files = map.Objects("files").ToList();
            Assert.That(files.Select(file => (file.Text("file"), file.Text("status"))), Is.EqualTo(new[] { (baseline, "LEGACY"), (design, "LEGACY") }));
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                foreach (var file in files)
                {
                    Assert.That(file.Keys, Is.EqualTo(new[] { "file", "sha256", "status" }));
                    var actual = string.Concat(sha.ComputeHash(WithLf(BytesOf(file.Text("file")))).Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
                    Assert.That(actual, Is.EqualTo(file.Text("sha256")),
                        file.Text("file") + " has changed since qa/legacy-map.json was written. A LEGACY file is not to be edited; if it was changed on purpose, the map must be checked against its keys again.");
                }
            }

            var entries = map.Objects("entries").ToList();
            var ids = new HashSet<string>(GameCatalog.Default.AllIds());
            foreach (var entry in entries)
            {
                var name = entry.Text("file") + " " + entry.Text("key");
                Assert.That(entry.Keys.Take(3), Is.EqualTo(new[] { "file", "key", "catalogId" }), name);
                Assert.That(entry.Keys.Skip(3), Is.EqualTo(new[] { "candidateId", "note" }).Or.EqualTo(new[] { "note" }).Or.Empty, name);
                Assert.That(entry.Text("file"), Is.EqualTo(baseline).Or.EqualTo(design), name);
                Assert.That(entry.Text("key"), Is.Not.Null.And.Not.Empty, name);
                var catalogId = entry.Text("catalogId");
                Assert.That(catalogId == noId || catalogId == outOfFrame || ids.Contains(catalogId), Is.True, name + ": " + catalogId + " is neither a marker nor an ID of the catalogue.");
            }
            Assert.That(entries.Select(entry => entry.Text("file") + "|" + entry.Text("key")).Distinct().Count(), Is.EqualTo(entries.Count), "A key is mapped once.");
            Assert.That(entries.Count, Is.EqualTo(75));

            // What is mapped to a definition is something the running game has: its seven classes, the settlement, the shipyard.
            var mapped = entries.Select(entry => entry.Text("catalogId")).Where(id => id != noId && id != outOfFrame).Distinct().ToList();
            Assert.That(mapped, Is.EquivalentTo(new[] { "N01", "N02", "N03", "N04", "N05", "N06", "N07", "B01", "B03" }));
            var catalog = GameCatalog.Default;
            foreach (var id in mapped)
                Assert.That((catalog.Variant(id)?.Availability ?? catalog.Technology(id).Availability).Implemented, Is.True, id);
            string IdOf(string file, string key) => entries.Single(entry => entry.Text("file") == file && entry.Text("key") == key).Text("catalogId");
            Assert.That((IdOf(baseline, "ships.bomber"), IdOf(baseline, "ships.boarding"), IdOf(baseline, "ships.royal")), Is.EqualTo(("N06", "N07", noId)),
                "The old names are told apart: the bomber is the siege ship, boarding the assault ship; the royal ship has no implemented definition (candidate N08).");
            Assert.That((IdOf(design, "initialFleet.shipDestroyer"), IdOf(design, "initialFleet.crewDestroyer"), IdOf(design, "initialFleet.royal")), Is.EqualTo(("N05", "N04", noId)));

            // A planned counterpart is a candidate, not a mapping: the numbers of these files are not rules of the game.
            // A candidate stands only where nothing implemented answers, exists in the catalogue, is not implemented
            // yet, and comes with a note that says on what ground and how surely.
            bool Implemented(string id) =>
                catalog.Variant(id)?.Availability.Implemented ?? catalog.Technology(id)?.Availability.Implemented ?? catalog.Forts.Single(fort => fort.Id == id).Availability.Implemented;
            var withCandidate = entries.Where(entry => entry.Has("candidateId")).ToList();
            foreach (var entry in withCandidate)
            {
                var name = entry.Text("file") + " " + entry.Text("key");
                var candidate = entry.Text("candidateId");
                Assert.That(entry.Text("catalogId"), Is.EqualTo(noId), name + ": a candidate beside a mapping.");
                Assert.That(entry.Text("note"), Does.Contain("confidence high").Or.Contain("confidence medium").Or.Contain("confidence low"), name);
                if (candidate.Length == 0) continue;
                Assert.That(ids.Contains(candidate), Is.True, name + ": the candidate " + candidate + " is not in the catalogue.");
                Assert.That(Implemented(candidate), Is.False, name + ": " + candidate + " is implemented, so it is a mapping or nothing, not a candidate.");
                Assert.That(entry.Text("note"), Does.Contain("Candidate " + candidate), name);
            }
            // The candidates, written out: a change of any of them is a change made on purpose.
            Assert.That(withCandidate.Where(entry => entry.Text("candidateId").Length > 0).Select(entry => (entry.Text("key"), entry.Text("candidateId"))), Is.EqualTo(new[]
            {
                ("crewMax.royal", "N08"), ("crewMax.fire", "N09"), ("crewMax.smoke", "N10"), ("crewMax.immortals", "N12"), ("crewMax.strait_serpent", "N11"),
                ("buildings.shore_tower", "F01"),
                ("ships.royal", "N08"), ("ships.fire", "N09"), ("ships.smoke", "N10"), ("ships.immortals", "N12"), ("ships.strait_serpent", "N11"),
                ("initialFleet.royal", "N08"),
            }));
            // Where two definitions are possible, or the only one is already implemented and does not match, none is
            // chosen: the candidate is empty and the note names them.
            string NoteOf(string key) => entries.Single(entry => entry.Text("file") == baseline && entry.Text("key") == key).Text("note");
            Assert.That(withCandidate.Where(entry => entry.Text("candidateId").Length == 0).Select(entry => entry.Text("key")),
                Is.EqualTo(new[] { "buildings.arsenal", "buildings.temple", "buildings.naval_school" }));
            Assert.That(NoteOf("buildings.arsenal"), Does.Contain("B04").And.Contain("B05"));
            Assert.That(NoteOf("buildings.temple"), Does.Contain("B09").And.Contain("B10"));
            Assert.That(NoteOf("buildings.naval_school"), Does.Contain("B08").And.Contain("is implemented"));
            Assert.That((Implemented("B04"), Implemented("B05"), Implemented("B08"), Implemented("B09"), Implemented("B10")), Is.EqualTo((true, false, true, false, false)),
                "What the notes say of these buildings is what the catalogue says.");
            // The one candidate called sure is the one whose numbers are the catalogue's own: the price and the time of N09.
            var fire = catalog.Variant("N09");
            Assert.That((fire.CostWood, fire.CostSilver, fire.BuildSeconds), Is.EqualTo((260, 180, 45.0)));
            Assert.That(NoteOf("ships.fire"), Does.Contain("confidence high").And.Contain("260/180").And.Contain("45 s"));
            Assert.That(withCandidate.Count(entry => entry.Text("note").Contains("confidence high")), Is.EqualTo(2), "ships.fire and crewMax.fire only.");

            // A key that is a rule and not a definition says where the running game's own value stands in the export,
            // and that place is there.
            var export = Export(GameCatalog.Default);
            var rules = export.Obj("rules");
            var profile = rules.Objects("matchProfiles").First();
            foreach (var (key, path, there) in new[]
                     {
                         ("pricing", "rules.pricing", rules.Has("pricing")), ("reserve", "rules.reserve", rules.Has("reserve")), ("warehouse", "rules.warehouse", rules.Has("warehouse")),
                         ("node", "rules.node", rules.Has("node")), ("harbor", "rules.harbour", rules.Has("harbour")), ("convoy", "rules.convoySize", rules.Has("convoySize")),
                         ("start", "rules.matchProfiles[].start", profile.Has("start")), ("start", "rules.matchProfiles[].deposits", profile.Has("deposits")),
                     })
            {
                Assert.That(NoteOf(key), Does.Contain("qa/runtime-catalog.json").And.Contain(path), key);
                Assert.That(there, Is.True, path + " is not in the export.");
            }

            // The fourteen scenarios of the old design are outside the plan's frame (Attica and the Aegean in the fourth
            // century). Their IDs M01-M14 look like the catalogue's powers M01-M12 and are not: none is mapped to one.
            var scenarios = entries.Where(entry => entry.Text("key").StartsWith("scenarios.", StringComparison.Ordinal)).ToList();
            Assert.That(scenarios.Select(entry => entry.Text("key")), Is.EqualTo(Enumerable.Range(1, 14).Select(number => "scenarios.M" + number.ToString("00", CultureInfo.InvariantCulture))));
            Assert.That(scenarios.Select(entry => (entry.Text("file"), entry.Text("catalogId"))), Is.All.EqualTo((design, outOfFrame)));
            Assert.That(entries.Count(entry => entry.Text("catalogId") == outOfFrame), Is.EqualTo(14), "Nothing else is called out of frame.");
            Assert.That(entries.Select(entry => entry.Text("catalogId")).Where(id => id.StartsWith("M", StringComparison.Ordinal) || id.StartsWith("K", StringComparison.Ordinal)), Is.Empty,
                "No old key is passed off as a power or an operation of the catalogue.");
        }
    }
}
