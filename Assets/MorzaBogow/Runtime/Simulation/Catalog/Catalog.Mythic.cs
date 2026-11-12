using System;
using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation.Catalog
{
    /// <summary>The sanctuary a power is learned and used at: of the Sea (B09) or of War (B10).</summary>
    public enum SanctuaryKind { Sea, War }

    /// <summary>What a power's radius means: metres around the target point, the whole map, one object, a point of summoning, or the caster's own side.</summary>
    public enum ReachKind { Area, WholeMap, SingleObject, SummonPoint, OwnSide }

    /// <summary>How long a power lasts: its seconds, until the operation ends or its source is removed, or one strike.</summary>
    public enum DurationKind { Timed, Match, Strike }

    /// <summary>
    /// One of the twelve powers M01-M12 (docs/KATALOG_MECHANIK.md §6, numbers of docs/PARAMETRY_ROBOCZE.md §4). The
    /// effects are named by ID only: what an effect is and does belongs to the effect contract, not to this catalogue.
    /// </summary>
    public sealed class AbilityDefinition
    {
        public string Id, Name, Summary;
        public SanctuaryKind Sanctuary;

        /// <summary>The price of one use, in units.</summary>
        public int CostWood, CostSilver;

        /// <summary>The distance from the sanctuary to the middle of a legal target, in metres; not asked when the whole map is in reach.</summary>
        public bool WholeMapRange;
        public double RangeMeters;

        public ReachKind Reach;
        public double RadiusMeters;
        public DurationKind Duration;
        public double DurationSeconds, CooldownSeconds, WarningSeconds;
        public string[] EffectRefs = Array.Empty<string>();
        public Parameter[] Parameters = Array.Empty<Parameter>();

        /// <summary>The research that teaches it.</summary>
        public string LearnTechnologyId;
        public Availability Availability = new Availability();
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("Ability", Id).F("Name", Name).F("Summary", Summary).F("Sanctuary", Sanctuary.ToString()).F("CostWood", CostWood).F("CostSilver", CostSilver)
                .F("WholeMapRange", WholeMapRange).F("RangeMeters", RangeMeters).F("Reach", Reach.ToString()).F("RadiusMeters", RadiusMeters)
                .F("Duration", Duration.ToString()).F("DurationSeconds", DurationSeconds).F("CooldownSeconds", CooldownSeconds).F("WarningSeconds", WarningSeconds)
                .F("EffectRefs", EffectRefs).F("Parameters", Parameters).F("LearnTechnologyId", LearnTechnologyId);
            Availability.Write(writer, "Availability");
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>
    /// One of the nine hero specialisations H01-H09 (docs/KATALOG_MECHANIK.md §7): a unique figure aboard a warship,
    /// joined through its own recruitment sign. Never kept in a fort or on a working ship.
    /// </summary>
    public sealed class HeroDefinition
    {
        /// <summary>A warship carries one hero, whatever the number of its artefact places.</summary>
        public const int PerWarship = 1;

        public string Id, Name, Summary, RecruitSignId;
        public string[] EffectRefs = Array.Empty<string>();
        public Parameter[] Parameters = Array.Empty<Parameter>();
        public Availability Availability = new Availability();
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("Hero", Id).F("Name", Name).F("Summary", Summary).F("RecruitSignId", RecruitSignId).F("EffectRefs", EffectRefs).F("Parameters", Parameters);
            Availability.Write(writer, "Availability");
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    public enum ArtifactKind { Utility, RecruitSign }

    /// <summary>
    /// One of the eleven kinds of useful object T01-T11 or of the nine recruitment signs R01-R09 (docs/KATALOG_MECHANIK.md
    /// §8). A sign is the key to its hero, not a passive bonus.
    /// </summary>
    public sealed class ArtifactDefinition
    {
        /// <summary>Places for artefacts: an ordinary ship one, the command ship two, a fort one; a town's store has no such limit.</summary>
        public const int PerShip = 1, PerCommandShip = 2, PerFort = 1;

        public string Id, Name, Summary;
        public ArtifactKind Kind;

        /// <summary>The hero a recruitment sign belongs to; null for a useful object.</summary>
        public string HeroId;
        public string[] EffectRefs = Array.Empty<string>();
        public Parameter[] Parameters = Array.Empty<Parameter>();
        public Availability Availability = new Availability();
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("Artifact", Id).F("Name", Name).F("Summary", Summary).F("Kind", Kind.ToString()).F("HeroId", HeroId).F("EffectRefs", EffectRefs).F("Parameters", Parameters);
            Availability.Write(writer, "Availability");
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>A threat of the map, a shrine of power, a configuration of the free flotillas, or the neutral economic target (E05).</summary>
    public enum EncounterKind { Threat, Shrine, FlotillaConfiguration, EconomicTarget }

    /// <summary>
    /// What a map may hold beside the two main sides (docs/KATALOG_MECHANIK.md §9): the eleven threats Z01-Z11, the two
    /// shrines, the five configurations of the free flotillas and the neutral target of harvest. They belong to the
    /// independent slot, or to nobody.
    /// </summary>
    public sealed class EncounterDefinition
    {
        public string Id, Name, Summary;
        public EncounterKind Kind;

        /// <summary>Held by no owner slot at all (the neutral target); otherwise OwnerSlot says whose it is.</summary>
        public bool Unowned;
        public int OwnerSlot = Owners.Independent;

        /// <summary>It comes back while its seat or nest stands; destroying that source ends it.</summary>
        public bool RespawnsFromSource;

        /// <summary>The channel of its own a shrine is proof against; null for none.</summary>
        public string ImmuneChannel;

        /// <summary>A flotilla ship that loses a boarding is scuttled, never taken.</summary>
        public bool ScuttlesWhenBoardingLost;

        /// <summary>The fleet role a flotilla configuration is built on; null while that is not settled.</summary>
        public string VariantId;

        /// <summary>The neutral target: what it pays once, in units, and the seconds of work it asks.</summary>
        public int RewardWood, RewardSilver;
        public double WorkSeconds;

        public string[] EffectRefs = Array.Empty<string>();
        public Parameter[] Parameters = Array.Empty<Parameter>();

        /// <summary>The content is a placeholder to be settled with its stage, not given by the documents.</summary>
        public bool Provisional;
        public Availability Availability = new Availability();
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("Encounter", Id).F("Name", Name).F("Summary", Summary).F("Kind", Kind.ToString()).F("Unowned", Unowned).F("OwnerSlot", OwnerSlot)
                .F("RespawnsFromSource", RespawnsFromSource).F("ImmuneChannel", ImmuneChannel).F("ScuttlesWhenBoardingLost", ScuttlesWhenBoardingLost)
                .F("VariantId", VariantId).F("RewardWood", RewardWood).F("RewardSilver", RewardSilver).F("WorkSeconds", WorkSeconds)
                .F("EffectRefs", EffectRefs).F("Parameters", Parameters).F("Provisional", Provisional);
            Availability.Write(writer, "Availability");
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    static partial class CatalogData
    {
        const string Apollodoros = "Pseudo-Apollodoros, Biblioteka 2.4.2 (przekł. J. G. Frazer, 1921); odczyt: " + Research0504;
        const string Odyssey = "Homer, Odyseja XII, rada Kirke (przekł. S. Butler); odczyt: " + Research0504;

        /// <summary>A motif of the myths named by the project and not read in a source: the motif is undetermined, what it does in the game is a rule of the game.</summary>
        static SourceNote MythUnread(string locator) => new SourceNote
        {
            Status = SourceStatus.Undetermined, Layer = WorldLayer.Mythic,
            Locator = locator + ". Motyw bez odczytanej karty źródłowej; działanie w grze: REGUŁA_GRY.",
        };

        /// <summary>
        /// The kinds of effect of the definitions that have more than one. An effect's ID is its owner's own ID where the
        /// owner has one effect, and the owner's ID, a dot and the kind where it has several (EffectIds).
        /// This division is a working one until the effects are wired in (P07). The rule it is to end at: one kind of
        /// effect and one magnitude to a definition of an effect.
        /// </summary>
        static readonly Dictionary<string, string[]> EffectKinds = new Dictionary<string, string[]>
        {
            { "M01", new[] { "ZONE", "PULL" } }, { "M02", new[] { "BARRIER", "IMPACT" } }, { "M04", new[] { "DAMAGE", "COURSE" } },
            { "H04", new[] { "WIND", "WARD" } }, { "H06", new[] { "RENEW", "WARD" } },
            { "T01", new[] { "HULL", "MODULES", "CREW" } }, { "T07", new[] { "SPEED", "RAM" } }, { "T09", new[] { "FIRE", "GAZE" } },
            { "Z04", new[] { "PULL", "DAMAGE" } }, { "Z-WIND", new[] { "WIND", "SPOUT" } },
        };

        static string[] EffectsOf(string ownerId) =>
            EffectKinds.TryGetValue(ownerId, out var kinds) ? kinds.Select(kind => EffectIds.Of(ownerId, kind)).ToArray() : new[] { ownerId };

        static Availability NotYet(PortBuildingSet target = PortBuildingSet.None, int slots = Availability.MainSlots, params string[] technologies) =>
            new Availability { Implemented = false, TargetBuildings = target, TargetTechnologies = technologies, OwnerSlots = slots };

        // ---------- Powers ----------

        internal static List<AbilityDefinition> Abilities()
        {
            var catalog = CatalogDoc + " §6; " + ParametersDoc + " §4";
            AbilityDefinition Ability(string id, string name, string summary, int wood, int silver, double range, ReachKind reach, double radius,
                DurationKind duration, double seconds, double cooldown, double warning, SourceNote source, params Parameter[] parameters)
            {
                var number = int.Parse(id.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
                var sanctuary = number <= 6 ? SanctuaryKind.Sea : SanctuaryKind.War;
                return new AbilityDefinition
                {
                    Id = id, Name = name, Summary = summary, Sanctuary = sanctuary, CostWood = wood, CostSilver = silver,
                    WholeMapRange = range <= 0, RangeMeters = Math.Max(0, range), Reach = reach, RadiusMeters = radius,
                    Duration = duration, DurationSeconds = seconds, CooldownSeconds = cooldown, WarningSeconds = warning,
                    EffectRefs = EffectsOf(id), Parameters = parameters, LearnTechnologyId = TechnologyIds.Learn(id),
                    Availability = NotYet(sanctuary == SanctuaryKind.Sea ? PortBuildingSet.SeaSanctuary : PortBuildingSet.WarSanctuary, Availability.MainSlots, TechnologyIds.Learn(id)),
                    Source = source ?? MythUnread(catalog),
                };
            }
            const double map = -1;
            return new List<AbilityDefinition>
            {
                Ability("M01", "Wir Posejdona", "Tworzy niebezpieczny wir w wybranym akwenie.", 180, 300, 300, ReachKind.Area, 35, DurationKind.Timed, 60, 120, 3, null,
                    new Parameter("CoreRadiusMeters", 10), new Parameter("HullSharePerSecond", 0.03), new Parameter("MaxActiveOwn", 2)),
                Ability("M02", "Zęby Morza", "Tworzy rafę stałą albo okresowo zanurzaną.", 180, 300, 300, ReachKind.Area, 25, DurationKind.Match, 0, 120, 3, null,
                    new Parameter("HullShareOnEntry", 0.15), new Parameter("RecontactSeconds", 5), new Parameter("ExposedSeconds", 5), new Parameter("SubmergedSeconds", 5), new Parameter("MaxOwnPermanent", 3)),
                Ability("M03", "Oddech Boreasza", "Wskazuje kierunek wiatru albo ciszę na określony czas.", 20, 40, map, ReachKind.WholeMap, 0, DurationKind.Timed, 45, 60, 3, null),
                Ability("M04", "Kolumna Nereusa", "Tworzy przemieszczającą się trąbę wodną o ograniczonym życiu.", 80, 140, 375, ReachKind.Area, 25, DurationKind.Timed, 30, 90, 3, null,
                    new Parameter("HullSharePerSecond", 0.02)),
                Ability("M05", "Spojrzenie Ateny", "Ujawnia wybrany obszar na określony czas, także obiekt objęty Zasłoną.", 0, 30, map, ReachKind.Area, 150, DurationKind.Timed, 30, 45, 3, null),
                Ability("M06", "Strażnik Głębin", "Przywołuje stworzenie atakujące wskazane wrogie cele; bez nagrody za jego zabicie.", 180, 260, 300, ReachKind.SummonPoint, 0, DurationKind.Timed, 90, 120, 3, null),
                Ability("M07", "Żar Aresa", "Czasowo wzmacnia siłę własnego uzbrojenia.", 220, 350, map, ReachKind.OwnSide, 0, DurationKind.Timed, 30, 120, 3, null,
                    new Parameter("PowerFactor", 1.5)),
                Ability("M08", "Grom Zeusa", "Niszczy niechronione cele przeciwnika w strefie i może zmienić maskę terenu.", 300, 800, 300, ReachKind.Area, 60, DurationKind.Strike, 0, 180, 3, null),
                Ability("M09", "Deszcz Hefajstosa", "Utrzymuje strefę kolejnych ognistych uderzeń.", 100, 180, 300, ReachKind.Area, 60, DurationKind.Timed, 20, 90, 3, null,
                    new Parameter("HullPerSecond", 35), new Parameter("MenPerSecond", 5)),
                Ability("M10", "Zasłona Hekate", "Czyni wskazany obiekt niewidocznym dla zwykłej obserwacji.", 30, 80, 375, ReachKind.SingleObject, 0, DurationKind.Timed, 45, 90, 0.5,
                    Myth(Apollodoros + ". Motyw niewidzialności (czapka Hadesa); przypisanie Hekate i liczby: REGUŁA_GRY.")),
                Ability("M11", "Aigida", "Redukuje obrażenia zwykłej broni w określonym czasie.", 30, 80, 375, ReachKind.SingleObject, 0, DurationKind.Timed, 45, 60, 0.5, null,
                    new Parameter("OrdinaryWeaponReduction", 0.8)),
                Ability("M12", "Pieczęć Hermesa", "Chroni przed wskazanymi oddziaływaniami magicznymi według macierzy odporności.", 30, 80, 375, ReachKind.SingleObject, 0, DurationKind.Timed, 45, 60, 0.5, null,
                    new Parameter("SummonedFireWaterspoutReduction", 0.8)),
            };
        }

        // ---------- Heroes ----------

        internal static List<HeroDefinition> Heroes()
        {
            var catalog = CatalogDoc + " §7; " + ParametersDoc + " §5";
            HeroDefinition Hero(int number, string name, string summary, SourceNote source, params Parameter[] parameters) => new HeroDefinition
            {
                Id = "H0" + number, Name = name, Summary = summary, RecruitSignId = "R0" + number, EffectRefs = EffectsOf("H0" + number),
                Parameters = parameters, Availability = NotYet(), Source = source ?? MythUnread(catalog),
            };
            return new List<HeroDefinition>
            {
                Hero(1, "Pilot Przesmyków", "Okręt nie wpada w wir i nie otrzymuje uderzenia rafy.", null),
                Hero(2, "Strateg Ateny", "Wzmacnia siłę i zasięg uzbrojenia obsadzonego okrętu.", null, new Parameter("PowerFactor", 1.2), new Parameter("RangeFactor", 1.1)),
                Hero(3, "Pieśniarz Delfinów", "Stworzenia z określonej listy nie inicjują ataku na jego okręt.", null),
                Hero(4, "Posłaniec Boreasza", "Uniezależnia prędkość od niekorzystnego wiatru i chroni przed trąbą wodną.", null),
                Hero(5, "Heraklida", "Czyni okręt niewrażliwym na obrażenia; nie omija lądu, wabienia ani warunków misji.", null),
                Hero(6, "Cieśla Hefajstosa", "Odnawia stan kadłuba i wzmacnia odporność przeciw stworzeniom.", null,
                    new Parameter("HullSharePerSecond", 0.02), new Parameter("CreatureDamageReduction", 0.5)),
                Hero(7, "Łuczniczka Artemidy", "Poprawia celność i cykl strzału.", null, new Parameter("DispersionFactor", 0.75), new Parameter("CycleFactor", 0.8)),
                Hero(8, "Tłumacz Pieśni", "Chroni przed syrenami i harpiami według macierzy celów.",
                    Myth(Odyssey + ". Motyw ochrony przed śpiewem syren; harpie i liczby: REGUŁA_GRY.")),
                Hero(9, "Nosiciel Lustra", "Chroni przed spojrzeniem gorgony.",
                    Myth(Apollodoros + ". Motyw odbicia w tarczy; postać i skutek w grze: REGUŁA_GRY.")),
            };
        }

        // ---------- Objects ----------

        internal static List<ArtifactDefinition> Artifacts()
        {
            var catalog = CatalogDoc + " §8; " + ParametersDoc + " §5";
            ArtifactDefinition Item(string id, string name, string summary, SourceNote source, params Parameter[] parameters) => new ArtifactDefinition
            {
                Id = id, Name = name, Summary = summary, Kind = ArtifactKind.Utility, EffectRefs = EffectsOf(id), Parameters = parameters,
                Availability = NotYet(PortBuildingSet.None, Availability.AllSlots), Source = source ?? MythRule(catalog),
            };
            var list = new List<ArtifactDefinition>
            {
                Item("T01", "Znak Odnowy", "Odtwarza stan kadłuba, modułów i obsady do zapisanych limitów.", null,
                    new Parameter("HullSharePerSecond", 0.03), new Parameter("ModulePointsPerSecond", 4), new Parameter("SecondsPerWarrior", 3)),
                Item("T02", "Depozyt towarowy", "Jednorazowo dostarcza określony ładunek D/S po podjęciu i rozliczeniu.", null),
                Item("T03", "Skrzynia uzbrojenia", "Dostarcza określony moduł; nie losuje go ponownie po wczytaniu.", null),
                Item("T04", "Naczynie mocy", "Pozwala użyć wskazanej zdolności w granicach posiadanych ładunków.", null),
                Item("T05", "Pieczęć płomienia", "Ochrona przed kanałem ognia.", null),
                Item("T06", "Znak osłony", "Zwiększa osłonę w określonym kanale.", null),
                Item("T07", "Znak szlaku", "Poprawia prędkość i skuteczność taranu.", null),
                Item("T08", "Więzy pokładu", "Ochrona przed przejęciem w abordażu.", null),
                Item("T09", "Maska strażnicza", "Odporność na ogień i oddziaływanie gorgony.",
                    Myth(Apollodoros + ". Motyw ochrony przed gorgoną; przedmiot i skutek w grze: REGUŁA_GRY.")),
                Item("T10", "Czara życia", "Jednorazowo przywraca część stanu załogi bojowej.", null, new Parameter("FightingCrewShare", 0.25), new Parameter("Charges", 1)),
                Item("T11", "Dokument warsztatowy", "Odblokowuje technologię po fizycznym dowiezieniu do uprawnionej osady.", null),
            };
            foreach (var hero in Heroes())
            {
                list.Add(new ArtifactDefinition
                {
                    Id = hero.RecruitSignId, Name = "Znak rekrutacyjny: " + hero.Name, Summary = "Klucz do przyłączenia bohatera " + hero.Id + "; nie jest pasywną premią.",
                    Kind = ArtifactKind.RecruitSign, HeroId = hero.Id, Availability = NotYet(PortBuildingSet.None, Availability.AllSlots), Source = MythRule(catalog),
                });
            }
            return list;
        }

        // ---------- Threats, shrines, flotillas and the neutral target ----------

        internal static List<EncounterDefinition> Encounters()
        {
            var catalog = CatalogDoc + " §9";
            Availability Independent() => new Availability { Implemented = false, OwnerSlots = Availability.IndependentSlot };
            EncounterDefinition Threat(int number, string name, string summary, bool respawns, SourceNote source = null) => new EncounterDefinition
            {
                Id = "Z" + number.ToString("00", System.Globalization.CultureInfo.InvariantCulture), Name = name, Summary = summary, Kind = EncounterKind.Threat,
                RespawnsFromSource = respawns, EffectRefs = EffectsOf("Z" + number.ToString("00", System.Globalization.CultureInfo.InvariantCulture)),
                Availability = Independent(), Source = source ?? MythUnread(catalog),
            };
            var list = new List<EncounterDefinition>
            {
                Threat(1, "Amazonka-strażniczka", "Razi załogi łukiem; element mitologicznego spotkania.", false),
                Threat(2, "Cyklop", "Miota kamienie na zbliżające się cele; zniszczenie siedziby kończy odtwarzanie.", true),
                Threat(3, "Byczy eidolon", "Atak pociskami i bliski ostrzał; zniszczenie siedziby usuwa źródło zagrożenia.", true),
                Threat(4, "Syrena", "Przyciąga ku skałom i wirom; kontrą jest trasa, ochrona i przerwanie ekspozycji.", false,
                    Myth(Odyssey + ". Motyw wabienia śpiewem; skutek i liczby w grze: REGUŁA_GRY.")),
                Threat(5, "Gorgona", "Czasowe unieruchomienie; wspólny budżet spojrzenia rozdzielany między cele.", false,
                    Myth(Apollodoros + ". Motyw spojrzenia gorgony; unieruchomienie zamiast skamienienia i budżet: REGUŁA_GRY.")),
                Threat(6, "Harpia", "Atakuje ludzi i odtwarza się, dopóki istnieje gniazdo.", true),
                Threat(7, "Drakon", "Ruchome źródło przywołań na lądzie i wodzie; trwałość daje określona siedziba.", true),
                Threat(8, "Krokodyl mityczny", "Zagrożenie wodne; nie jest zwykłą fauną Attyki.", false),
                Threat(9, "Wielki miecznik", "Atak na kadłub jako zjawisko fantastyczne.", false),
                Threat(10, "Rekin-strażnik", "Obszarowy drapieżca z ograniczoną strefą pościgu.", false),
                Threat(11, "Wąż morski", "Ciężki strażnik akwenu, cel do ominięcia albo pokonania.", false),

                new EncounterDefinition { Id = "Z-WIND", Name = "Przybytek Wiatru", Summary = "Tworzy niekorzystny wiatr i trąby; kończy oddziaływanie po zniszczeniu.",
                    Kind = EncounterKind.Shrine, ImmuneChannel = EffectChannelNames.Wind, EffectRefs = EffectsOf("Z-WIND"), Availability = Independent(), Source = MythUnread(catalog) },
                new EncounterDefinition { Id = "Z-FIRE", Name = "Przybytek Ognia", Summary = "Tworzy strefy ognistych uderzeń; kończy oddziaływanie po zniszczeniu.",
                    Kind = EncounterKind.Shrine, ImmuneChannel = EffectChannelNames.FireZone, EffectRefs = EffectsOf("Z-FIRE"), Availability = Independent(), Source = MythUnread(catalog) },

                new EncounterDefinition { Id = "E05", Name = "Neutralne stworzenie morskie", Summary = "Rzadki, skończony cel pozyskania; rozlicza jednego zwycięzcę.",
                    Kind = EncounterKind.EconomicTarget, Unowned = true, RewardWood = 960, RewardSilver = 800, WorkSeconds = 60,
                    Availability = new Availability { Implemented = false, OwnerSlots = Availability.MainSlots }, Source = Rule(CatalogDoc + " §1 E05; " + ParametersDoc + " §6") },
            };
            // Five configurations of the free flotillas: the documents fix their number and their rule of scuttling, not
            // their make-up, which is settled with their stage (P09).
            for (var i = 1; i <= 5; i++)
            {
                list.Add(new EncounterDefinition
                {
                    Id = "FL" + i, Name = "Wolna flotylla, konfiguracja " + i, Summary = "Okręt flotylli rozbójniczej; po przegranym abordażu samozatopiony zamiast przejęty.",
                    Kind = EncounterKind.FlotillaConfiguration, ScuttlesWhenBoardingLost = true, Provisional = true, Availability = Independent(),
                    Source = Rule(catalog + " (wolne flotylle); " + ParametersDoc + " §3. Skład konfiguracji nieokreślony w dokumentach."),
                });
            }
            return list;
        }
    }
}
