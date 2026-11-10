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

        /// <summary>A bush of the maquis: the tree's entry with its own leaves, levels and tests.</summary>
        static ManifestEntry Shrub(TreeSpecies species, string id, string leaves, string variants)
        {
            var entry = Tree(species, id, variants, "Maquis.Plant z BattleController.PlantGrove: mniej krzewów na wysepkach portów i wolnych brzegów, podszyt sosen na wysepkach zaplecza", AllRegions);
            entry.Materials = new[]
            {
                "Environment/OliveBark (zamiennik kory: w gęstym krzewie prawie niewidoczna)",
                $"Environment/{leaves} (HDRP/Lit Translucent, profil Foliage, alpha clip, atlas z LeafAtlas: {(species.LeafShape == LeafKind.Pinnate ? "liść parzystopierzasty na oskrzydlonej osadce" : "liść owalny, falisty, z kolcem na każdym zębie")})",
            };
            entry.Animations = new[]
            {
                "kołysanie pnia: model wahadła drzew liściastych przeniesiony na krzew (spekulacja, brak pomiarów)",
                "konary i gałęzie: wsporniki k = 3EI/L, wymuszenie ruchem podstawy",
                $"trzepotanie pędów {species.FlutterFrequency:0} Hz od {species.FlutterOnset.ToString("0.##", CultureInfo.InvariantCulture)} m/s (sztywne, skórzaste liście)",
            };
            entry.Lods = new[] { "LOD0 ≤ 5000 trójkątów", "LOD1 ≤ 1500", "LOD2 ≤ 400, karty nie większe niż 0,6 promienia korony", "wspólne kości wszystkich poziomów" };
            entry.Tests = new[]
            {
                "ShrubTests.Shrubs_StayLowDenseToTheGroundAndWithinTheirBudgets", "ShrubTests.Shrubs_SwayFasterThanTheOlivesAsTheirModelSays",
                "ShrubTests.Shrubs_AtlasesDrawTheirOwnLeaves", "ShrubTests.Maquis_KeepsOffBeachesCliffRimsTreesAndBuildings",
                "LandscapeTests.Landscape_TheMaquisGrowsOnTheIsletsAndTheMatchWindBendsIt",
            };
            return entry;
        }

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

        static ManifestEntry Done(string id, string category, string name, Region[] regions, Era[] eras, string dimensions, string materials, string integration, string[] tests, Confidence confidence, params string[] sources)
        {
            var entry = Planned(id, category, name, regions, eras, dimensions, integration, confidence, sources);
            entry.Status = AssetStatus.Trial;
            entry.Materials = new[] { materials };
            entry.Physics = "bez colliderów; tylko prezentacja";
            entry.Lods = new[] { "jedna siatka (proste bryły), bez LOD" };
            entry.Tests = tests;
            return entry;
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
                Shrub(TreeSpecies.KermesOak, "veg.quercus.coccifera", "KermesLeaves", "kształt z hasza id: 3–6 pni, gęsta kopuła do ziemi, liście 1,5–3,5 cm z 4–8 kolcami"),
                Shrub(TreeSpecies.Lentisk, "veg.pistacia.lentiscus", "LentiskLeaves", "kształt z hasza id: 2–5 pni, kopuła do ziemi, 3–5 par listków na liściu"),
                Planned("veg.phrygana", "vegetation", "Poduszki frygany (Sarcopoterium spinosum, Thymbra capitata, Cistus)", all, eras, "poduszki 0,3–0,8 m", "suche wierzchowiny wysepek", Confidence.Established, "2.x: EUNIS F7.31"),
                new ManifestEntry
                {
                    Id = "veg.halophytes",
                    Category = "vegetation",
                    Name = "Halofity klifów (Crithmum maritimum, Limonium)",
                    Status = AssetStatus.Trial,
                    Regions = all,
                    Eras = eras,
                    Dimensions = "kępy: koper morski 0,25–0,55 m, zatrwian 0,15–0,35 m; na ścianie klifu 1,9–5 m nad morzem, na skalistym brzegu 0,65–1,6 m",
                    Materials = new[] { "Environment/CliffPlants (HDRP/Lit Translucent, profil Foliage, alpha clip, atlas z CliffPlantAtlas)" },
                    Animations = new[] { "bez ruchu na wietrze: małe, sztywne, mięsiste rośliny (uproszczenie)" },
                    Physics = "bez colliderów i cieni; tylko prezentacja",
                    Lods = new[] { "jedna siatka kart na wysepkę (3 karty na kępę), bez LOD" },
                    Integration = "CliffPlants przez CoastFeatures: klify wysepek portów, zaplecza i złóż",
                    Tests = new[] { "CoastTests.Coast_CliffPlantsGrowOnTheFacesAboveTheNotchAndOnRockyShoresOnly", "CoastTests.Coast_CliffPlantAtlasShowsGreySamphireAndVioletSeaLavender" },
                    Confidence = Confidence.Interpretation,
                    Sources = new[] { "2.17: AUTh B3.33 (stan obecny, Kreta)", "8.15–8.19: Flora Europaea vol. 2 s. 333; Renna 2018", "8.20–8.21: Crespo, Pena-Martín 2013; Brullo, Erben 2016" },
                },
                Done("coast.limestone.notch", "coast", "Klif wapienny z notchem pływowym i mokrym pasem", new[] { Region.SouthMainland, Region.Crete, Region.Cyclades }, eras, "warga 0,55–1,7 m nad morzem gry, wysunięta 0,5–1,1 m",
                    "skała brzegu sceny (Shore_Limestone, CC0 marble_cliff_03) i jej mokra kopia", "CoastFeatures na wysepkach portów, zaplecza i złóż",
                    new[] { "CoastTests.Coast_TheNotchCutsOnlyTheCliffsAtTheGamesSeaLevel", "CoastTests.Coast_TheWetBandHugsTheWaterline", "CoastTests.Coast_SameSeedSameShore" }, Confidence.Established, "3.x: Evelpidou i in. 2014"),
                Planned("coast.marble-schist", "coast", "Odsłonięcie marmuru i łupka", new[] { Region.Cyclades, Region.SouthMainland }, eras, "do ustalenia", "klify Cyklad i Laurionu", Confidence.Established, "3.x: Desruelles i in. 2009; 4.x"),
                Done("coast.beachrock", "coast", "Płyty beachrocku w strefie pływów", all, eras, "płyty 1,2–2,2 × 1,0–1,4 × 0,22–0,32 m, 1–3 rzędy, upad 5–9° ku morzu",
                    "kopia dna brzegu sceny (Shore_Seabed, CC0 coast_sand_rocks_02)", "CoastFeatures: łagodne brzegi wysepek",
                    new[] { "CoastTests.Coast_BeachrockLiesAwashOnTheGentleShoreAndDipsSeaward" }, Confidence.Established, "3.2: Desruelles i in. 2009"),
                Done("coast.pebble-beach", "coast", "Plaża żwirowa z pasem wilgoci", all, eras, "pas od 0,35 m pod wodą do 0,7 m nad nią, odcinkami na przemian z gołym brzegiem; otoczaki ok. 0,1–0,3 m (kafel 2 m, oszacowanie z obrazu)",
                    "Environment/ShorePebbles (HDRP/Lit, CC0 dry_river_pebbles wybielone ku otoczakom wapiennym, triplanar w świecie) i jego mokra kopia", "Shingle przez CoastFeatures: łagodne brzegi wysepek; pod żwirem mokry pas skały ustępuje",
                    new[] { "CoastTests.Coast_ShingleLiesOnTheGentleShoreFromUnderTheWaterToAboveTheSwash", "CoastTests.Coast_WhereShingleLiesItsOwnWetPartTakesTheSwash" }, Confidence.Speculation, "3.12: brak przeczytanego źródła, potrzebna referencja zdjęciowa"),
                Planned("coast.sand-cove", "coast", "Piasek w zatokach", all, eras, "do ustalenia", "tylko osłonięte zatoki", Confidence.Interpretation, "3.x"),
                Planned("coast.terra-rossa", "coast", "Sucha ziemia (terra rossa) z fryganą", new[] { Region.SouthMainland, Region.Crete }, eras, "do ustalenia", "wierzchowiny wysepek", Confidence.Interpretation, "3.x"),
                Done("ore.laurion.outcrop", "ore", "Odsłonięcie rudy na kontakcie marmuru i łupka (galenit, cerusyt, tlenki Fe)", new[] { Region.SouthMainland }, eras, "dwie ściany po 11,5 × 4,2 m po bokach wejścia do kopalni, kontakt ok. 1,2–2,1 m nad stopą",
                    "marmur (Shore_Limestone), kopie: łupek, limonit, galenit (metal), cerusyt", "ResourceSiteVisual.BuildMiningQuay: wysepka srebra",
                    new[] { "SiteTests.Ore_MarbleUnderSchistWithTheOreInTheContactBetween", "SiteTests.Ore_OnlyTheGalenaIsMetal" }, Confidence.Established, "4.1: Kepper 2004 s. 75"),
                Done("ore.face.E1", "ore", "Przodek E1: płytkie, nieregularne wyrobiska wzdłuż kontaktu, urobek pod nimi", new[] { Region.SouthMainland }, new[] { Era.PostPalatial, Era.EarlyIron }, "wyrobiska 0,8–1,5 m, jedno na ok. 5 m ściany",
                    "MineDark sceny, kopia limonitu", "OreOutcrop na wysepce srebra", new[] { "SiteTests.Ore_MarbleUnderSchistWithTheOreInTheContactBetween" }, Confidence.Interpretation, "4.13: Wood i in. 2021"),
                new ManifestEntry
                {
                    Id = "ore.dust",
                    Category = "ore",
                    Name = "Pył i odłamki spod kilofa przy przodku",
                    Status = AssetStatus.Trial,
                    Regions = new[] { Region.SouthMainland },
                    Eras = eras,
                    Dimensions = "na uderzenie 6 obłoków pyłu 0,25–0,6 m i 5 odłamków 4–8 cm",
                    Materials = new[] { "dwie kopie mgiełki sceny (Placeholder_Spray, HDRP/Unlit): jasny pył, szare odłamki" },
                    Animations = new[] { "obłok unoszony w górę i ku górnikowi przez 1,4–2,2 s, odłamki opadają w 0,5–0,8 s; w chwili, gdy kilof trafia w skałę (połowa cyklu pozy CutLine)" },
                    Physics = "cząstki Shuriken bez kolizji; z wartości stałego ciągu (bez losowania gry i Unity); tylko prezentacja",
                    Lods = new[] { "cząstki bez LOD, do 240 pyłu i 160 odłamków na złoże" },
                    Integration = "ResourceSiteVisual.Worked z CrewVisual.Work: tylko srebro, stan złoża bez zmian",
                    Tests = new[] { "SiteTests.Ore_EachBlowThrowsDustAndChipsFromTheFaceWithoutTheGamesRandom", "LandscapeTests.Landscape_DustFliesWhenThePickMeetsTheRockOfSilverOnly" },
                    Confidence = Confidence.Interpretation,
                    Sources = new[] { "4.7: ScienceDaily 2016 (tłuki kamienne, ślady narzędzi szpiczastych)" },
                },
                Planned("ore.face.E5", "ore", "Przodek E5: regularny chodnik, ślady szpicaka", new[] { Region.SouthMainland }, new[] { Era.Archaic, Era.Classical, Era.LateClassical }, "do ustalenia", "wysepka srebra w profilu E5", Confidence.Established, "4.x: Kepper 2004; Rehren 2002"),
                Done("ore.heap", "ore", "Hałda rudy sortowanej ręcznie", new[] { Region.SouthMainland }, eras, "stożek ok. 2,6 m średnicy, 60 brył; skala 0,35–1 ze stanu złoża", "kopia limonitu",
                    "ResourceSiteVisual.UpdateStock (tylko odczyt node.Stock)", new[] { "LandscapeTests.Landscape_TheSortedOreHeapFollowsTheStockAndNeverTouchesIt" }, Confidence.Interpretation, "4.x"),
                Planned("ore.washery.E5", "ore", "Płuczka rudy (E5 i E6)", new[] { Region.SouthMainland }, new[] { Era.Classical, Era.LateClassical }, "do ustalenia", "tylko profil E5", Confidence.Established, "4.9–4.10: Kepper 2004 s. 75–77; Rehren 2002 s. 27"),
                Done("yard.slipway.E1", "shipyard", "Wyposażenie stoczni plażowej E1: skład bali i desek, szałas z trzciny, kocioł smoły, kamienne podpórki", all, new[] { Era.PostPalatial, Era.EarlyIron, Era.Geometric },
                    "szałas 2,8 × 2 m, okap 2,0 m; bale 4,2 m", "Timber, Meadow (strzecha), Shore_Limestone, Pitch sceny", "ShipyardProps obok pochylni ShipyardVisual (rampa bez zmian)",
                    new[] { "SiteTests.Yard_GearStandsOnDryLandClearOfTheRampsAndItsToolsFollowTheEra" }, Confidence.Interpretation, "5.x: Uluburun; Kommos"),
                Done("yard.tools", "shipyard", "Narzędzia szkutnicze na stojaku: brąz (E1), żelazo (od E2)", all, eras, "topór, ciosło, dłuta na żerdzi 1,5 m", "kopia skały w barwie brązu lub żelaza (metal)",
                    "ShipyardProps: stojak z narzędziami", new[] { "SiteTests.Yard_GearStandsOnDryLandClearOfTheRampsAndItsToolsFollowTheEra" }, Confidence.Interpretation, "5.x: Homer Od. 5; Uluburun"),
                Planned("yard.shed.zea2", "shipyard", "Hangar Zea, faza 2 (profile E5 i E6)", new[] { Region.SouthMainland }, new[] { Era.Classical, Era.LateClassical }, "światło 5,81 m: nie mieści okrętów gry (do 10,9 m)", "tylko jako pozycja profilu", Confidence.Established, "5.10: Lovén 2011"),
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
