using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>Done in the trial set (built, integrated and tested) or still planned.</summary>
    public enum AssetStatus { Trial, Planned }

    /// <summary>One asset of the environment as docs/art/EnvironmentAssetManifest.json lists it.</summary>
    public sealed class ManifestEntry
    {
        public string Id, Category, Name, Dimensions, Variants, Physics, Integration;
        public AssetStatus Status;
        public Region[] Regions = System.Array.Empty<Region>();
        public Era[] Eras = System.Array.Empty<Era>();
        public string[] Materials = System.Array.Empty<string>(), Animations = System.Array.Empty<string>(), Lods = System.Array.Empty<string>();
        public string[] Tests = System.Array.Empty<string>();

        /// <summary>Sections of docs/art/SrodowiskoZrodla.md (or a register key) behind the asset, and how sure it is.</summary>
        public string[] Sources = System.Array.Empty<string>();
        public Confidence Confidence;
    }

    /// <summary>
    /// The manifest of the environment's assets (docs/art/EnvironmentAssetManifest.json), made from the code: the
    /// trial set from the species and generator themselves, the rest as planned entries tied to their sources. Only
    /// what the game needs (V-rules and the islets it draws), never a scenario or a rule of play.
    /// </summary>
    public static class EnvironmentManifest
    {
        static readonly Region[] AllRegions = (Region[])System.Enum.GetValues(typeof(Region));
        static readonly Era[] AllEras = (Era[])System.Enum.GetValues(typeof(Era));

        /// <summary>Species and plants that must not appear: later introductions to the Aegean (report 2.x).</summary>
        public static readonly (string name, string reason)[] Excluded =
        {
            ("Eucalyptus", "wprowadzony w XIX–XX w."),
            ("Opuntia ficus-indica", "z Ameryki, po 1492 r."),
            ("Agave americana", "z Ameryki, po 1492 r."),
            ("Robinia pseudoacacia", "z Ameryki Północnej, nowożytna"),
            ("Ailanthus altissima", "z Chin, XVIII w."),
        };

        public static IReadOnlyList<ManifestEntry> Entries { get; } = Build();

        static ManifestEntry Tree(TreeSpecies species, string id, string variants, string integration, Region[] regions)
        {
            return new ManifestEntry
            {
                Id = id,
                Category = "vegetation",
                Name = $"{species.Name} ({species.LatinName})",
                Status = AssetStatus.Trial,
                Regions = regions,
                Eras = AllEras,
                Dimensions = string.Format(CultureInfo.InvariantCulture, "wysokość {0:0.#}–{1:0.#} m, pień {2:0.##}–{3:0.##} m, korona ≈ {4:0.##}·H, pnie {5}–{6}",
                    species.HeightMin, species.HeightMax, species.StemDiameterMin, species.StemDiameterMax, species.CrownWidthRatio, species.StemsMin, species.StemsMax),
                Materials = new[] { "Environment/OliveBark (HDRP/Lit, kora CC0 bark_willow_02, UV w metrach)", "Environment/OliveLeaves (HDRP/Lit Translucent, profil Foliage, alpha clip, atlas z LeafAtlas)" },
                Variants = variants,
                Animations = new[]
                {
                    "kołysanie pnia: oscylator 2 stopni swobody, częstotliwość z modelu wahadła",
                    "konary i gałęzie: wsporniki k = 3EI/L, wymuszenie ruchem podstawy",
                    $"trzepotanie pędów {species.FlutterFrequency:0} Hz od {species.FlutterOnset.ToString("0.##", CultureInfo.InvariantCulture)} m/s, skręt pokazujący srebrny spód",
                },
                Physics = "bez Rigidbody i Jointów; wiatr z World.Marine.Wind (tylko odczyt), czas gry, pauza zamraża",
                Lods = new[] { "LOD0 ≤ 5000 trójkątów (≥ 12 px/m)", "LOD1 ≤ 1500 (4–12 px/m)", "LOD2 ≤ 400 (< 4 px/m)", "wspólne kości wszystkich poziomów" },
                Integration = integration,
                Tests = new[]
                {
                    "TreeTests.Trees_SameIdGivesTheSameTreeAndAnotherIdAnother", "TreeTests.Trees_StayWithinTheirSpecies", "TreeTests.Trees_LevelsShareOneSkeletonWithinTheirBudgets",
                    "TreeTests.Trees_TheFootStaysPutWhileTheCrownBends", "TreeTests.Trees_ChangingLevelKeepsTheBend", "TreeTests.TreeWind_SteadyBendFollowsUToTheTwoPlusV",
                    "TreeTests.TreeWind_AGustSwaysTheTreeWhileASteadyWindOnlyBendsIt", "TreeTests.TreeWind_AfterTheGustTheSwayDiesAway", "TreeTests.TreeWind_NearTreesSwayTogetherAndDownwindTreesLater",
                    "LandscapeTests.Landscape_OlivesOfTheBattleAreTreesTheMatchWindBends", "LandscapeTests.Landscape_TreesReadTheWindAndLeaveTheMatchAsItWas",
                },
                Sources = species.Sources.Select(note => $"{note.Reference} ({note.Place}): {note.Confidence}").ToArray(),
                Confidence = Confidence.Interpretation,
            };
        }

        static ManifestEntry Planned(string id, string category, string name, Region[] regions, Era[] eras, string dimensions, string integration, Confidence confidence, params string[] sources) =>
            new ManifestEntry
            {
                Id = id,
                Category = category,
                Name = name,
                Status = AssetStatus.Planned,
                Regions = regions,
                Eras = eras,
                Dimensions = dimensions,
                Integration = integration,
                Confidence = confidence,
                Sources = sources,
            };

        static List<ManifestEntry> Build()
        {
            var mainland = new[] { Region.SouthMainland, Region.Cyclades };
            var east = new[] { Region.Crete, Region.EastAegean };
            var all = AllRegions;
            var eras = AllEras;
            return new List<ManifestEntry>
            {
                Tree(TreeSpecies.OliveCultivated, "veg.olive.cultivated", "kształt z hasza id (wysokość, pień, 3–4 konary, skręt bruzd, odchylenie od etezji na eksponowanym brzegu)",
                    "BattleController.PlantGrove: wysepki portów i wolnych brzegów (udział uprawnych z profilu epoki)", all),
                Tree(TreeSpecies.OliveWild, "veg.olive.wild", "krzew wielopniowy 2–4 pni, kształt z hasza id",
                    "BattleController.PlantGrove (reszta oliwek) i ResourceSiteVisual: zbocza wysepki srebra", all),
                Planned("veg.pine.halepensis", "vegetation", "Sosna alepska (Pinus halepensis)", mainland, eras, "drzewo 10–20 m (do potwierdzenia)", "skład drewna, lasy wysp zaplecza", Confidence.Established, "2.x: JRC Atlas s. 122"),
                Planned("veg.pine.brutia", "vegetation", "Sosna kalabryjska (Pinus brutia)", east, eras, "do ustalenia", "skład drewna w profilu Krety i wschodniej Egei", Confidence.Established, "2.x: JRC Atlas s. 122"),
                Planned("veg.cypress.wild", "vegetation", "Cyprys rozłożysty (Cupressus sempervirens f. horizontalis)", east, eras, "do ustalenia", "zbocza Krety i Dodekanezu", Confidence.Established, "2.x: JRC Atlas s. 88–89"),
                Planned("veg.cypress.columnar", "vegetation", "Cyprys kolumnowy (uprawny)", all, eras, "do ustalenia", "tylko sanktuaria i ogrody", Confidence.Interpretation, "2.x: JRC Atlas s. 88–89"),
                Planned("veg.quercus.coccifera", "vegetation", "Dąb kermesowy (Quercus coccifera)", all, eras, "krzew 1–3 m (do potwierdzenia)", "makia na zboczach", Confidence.Established, "2.x: EUNIS F7.31 / makia"),
                Planned("veg.pistacia.lentiscus", "vegetation", "Lentyszek (Pistacia lentiscus)", all, eras, "krzew 1–3 m (do potwierdzenia)", "makia przy brzegu", Confidence.Established, "2.x: EUNIS F7.31"),
                Planned("veg.phrygana", "vegetation", "Poduszki frygany (Sarcopoterium spinosum, Thymbra capitata, Cistus)", all, eras, "poduszki 0,3–0,8 m", "suche wierzchowiny wysepek", Confidence.Established, "2.x: EUNIS F7.31"),
                Planned("veg.halophytes", "vegetation", "Halofity klifów (Crithmum maritimum, Limonium)", all, eras, "kępy 0,2–0,5 m", "szczeliny klifów nad wodą", Confidence.Interpretation, "2.x"),
                Planned("coast.limestone.notch", "coast", "Klif wapienny z notchem pływowym", new[] { Region.SouthMainland, Region.Crete, Region.Cyclades }, eras, "notch na poziomie morza gry", "klify wysepek (ShoreShape)", Confidence.Established, "3.x: Evelpidou i in. 2014"),
                Planned("coast.marble-schist", "coast", "Odsłonięcie marmuru i łupka", new[] { Region.Cyclades, Region.SouthMainland }, eras, "do ustalenia", "klify Cyklad i Laurionu", Confidence.Established, "3.x: Desruelles i in. 2009; 4.x"),
                Planned("coast.beachrock", "coast", "Płyty beachrocku w strefie pływów", all, eras, "płyty 1,5–3 m, upad ku morzu", "łagodne brzegi", Confidence.Established, "3.2: Desruelles i in. 2009"),
                Planned("coast.pebble-beach", "coast", "Plaża żwirowa z pasem wilgoci", all, eras, "pas 3–5 m", "łagodne brzegi wysepek", Confidence.Interpretation, "3.x"),
                Planned("coast.sand-cove", "coast", "Piasek w zatokach", all, eras, "do ustalenia", "tylko osłonięte zatoki", Confidence.Interpretation, "3.x"),
                Planned("coast.terra-rossa", "coast", "Sucha ziemia (terra rossa) z fryganą", new[] { Region.SouthMainland, Region.Crete }, eras, "do ustalenia", "wierzchowiny wysepek", Confidence.Interpretation, "3.x"),
                Planned("ore.laurion.outcrop", "ore", "Odsłonięcie rudy na kontakcie marmuru i łupka (galenit, cerusyt, tlenki Fe)", new[] { Region.SouthMainland }, eras, "do ustalenia", "ResourceSiteVisual: wysepka srebra", Confidence.Established, "4.1: Kepper 2004 s. 75"),
                Planned("ore.face.E1", "ore", "Przodek E1: drobna, nieregularna praca, ślady tłuków", new[] { Region.SouthMainland }, new[] { Era.PostPalatial, Era.EarlyIron }, "do ustalenia", "wysepka srebra", Confidence.Interpretation, "4.13: Wood i in. 2021"),
                Planned("ore.face.E5", "ore", "Przodek E5: regularny chodnik, ślady szpicaka", new[] { Region.SouthMainland }, new[] { Era.Archaic, Era.Classical }, "do ustalenia", "wysepka srebra w profilu E5", Confidence.Established, "4.x: Kepper 2004; Rehren 2002"),
                Planned("ore.heap", "ore", "Hałda rudy sortowanej ręcznie", new[] { Region.SouthMainland }, eras, "do ustalenia", "stan z node.Stock (bez zmiany zasobu)", Confidence.Interpretation, "4.x"),
                Planned("ore.washery.E5", "ore", "Płuczka rudy (tylko E5)", new[] { Region.SouthMainland }, new[] { Era.Classical }, "do ustalenia", "tylko profil E5", Confidence.Established, "4.9–4.10: Kepper 2004 s. 75–77; Rehren 2002 s. 27"),
                Planned("yard.slipway.E1", "shipyard", "Pochylnia plażowa E1: płozy poprzeczne, podpory, kamienne podpórki", all, new[] { Era.PostPalatial, Era.EarlyIron, Era.Geometric }, "do ustalenia", "ShipyardVisual", Confidence.Interpretation, "5.x: Uluburun; Kommos"),
                Planned("yard.tools", "shipyard", "Narzędzia szkutnicze: brąz (E1), żelazo (od E2)", all, eras, "topór, ciosło, dłuto, piła, świder łukowy", "stojak w stoczni", Confidence.Interpretation, "5.x: Homer Od. 5; Uluburun"),
                Planned("yard.shed.zea2", "shipyard", "Hangar Zea, faza 2 (tylko profil E5)", new[] { Region.SouthMainland }, new[] { Era.Classical }, "światło 5,81 m: nie mieści okrętów gry (do 10,9 m)", "tylko jako pozycja profilu", Confidence.Established, "5.10: Lovén 2011"),
            };
        }

        /// <summary>The manifest as written to docs/art/EnvironmentAssetManifest.json (stable order and formatting).</summary>
        public static string Json()
        {
            var text = new StringBuilder();
            text.Append("{\n  \"schema\": \"morza-bogow/environment-manifest/1\",\n");
            text.Append("  \"profile\": ").Append(Quote($"{EnvironmentProfile.Default.Era}/{EnvironmentProfile.Default.Region}")).Append(",\n");
            text.Append("  \"sources\": \"docs/art/SrodowiskoZrodla.md\",\n");
            text.Append("  \"excluded\": [\n");
            for (var i = 0; i < Excluded.Length; i++)
                text.Append("    { \"name\": ").Append(Quote(Excluded[i].name)).Append(", \"reason\": ").Append(Quote(Excluded[i].reason)).Append(" }").Append(i + 1 < Excluded.Length ? ",\n" : "\n");
            text.Append("  ],\n  \"assets\": [\n");
            for (var i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                text.Append("    {\n");
                Field(text, "id", e.Id);
                Field(text, "category", e.Category);
                Field(text, "name", e.Name);
                Field(text, "status", e.Status.ToString());
                List(text, "regions", e.Regions.Select(r => r.ToString()));
                List(text, "eras", e.Eras.Select(r => r.ToString()));
                Field(text, "dimensions", e.Dimensions);
                List(text, "materials", e.Materials);
                Field(text, "variants", e.Variants);
                List(text, "animations", e.Animations);
                Field(text, "physics", e.Physics);
                List(text, "lod", e.Lods);
                Field(text, "integration", e.Integration);
                List(text, "tests", e.Tests);
                List(text, "sources", e.Sources);
                text.Append("      \"confidence\": ").Append(Quote(e.Confidence.ToString())).Append("\n");
                text.Append(i + 1 < Entries.Count ? "    },\n" : "    }\n");
            }
            text.Append("  ]\n}\n");
            return text.ToString();
        }

        static void Field(StringBuilder text, string name, string value) =>
            text.Append("      ").Append(Quote(name)).Append(": ").Append(value == null ? "null" : Quote(value)).Append(",\n");

        static void List(StringBuilder text, string name, IEnumerable<string> values)
        {
            var items = values.ToArray();
            text.Append("      ").Append(Quote(name)).Append(": [");
            for (var i = 0; i < items.Length; i++) text.Append(i == 0 ? "" : ", ").Append(Quote(items[i]));
            text.Append("],\n");
        }

        static string Quote(string value)
        {
            var text = new StringBuilder("\"");
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    default: text.Append(c); break;
                }
            }
            return text.Append('"').ToString();
        }
    }
}
