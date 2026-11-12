using System;
using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation.Catalog
{
    public enum ScenarioKind { Campaign, Skirmish }

    /// <summary>What a map stands for (docs/MAPY_I_SCENARIUSZE.md §3): a cut of real water, a recognisable but compressed one, or a water of the myths.</summary>
    public enum SectorKind { Geographic, Schematic, Mythic }

    /// <summary>Who plays an owner slot in a scenario: the human, the main opponent, or the executor of encounters for the independent slot.</summary>
    public enum SideController { Human, MainAi, Encounters }

    public sealed class ScenarioSide
    {
        public int Slot;
        public FactionId Faction;
        public SideController Controller;
    }

    /// <summary>
    /// The conditions an operation's result is decided by (docs/MAPY_I_SCENARIUSZE.md §4). And and Or join whole branches;
    /// the others are leaves whose arguments name what the scenario's data must bind before the mission may start.
    /// </summary>
    public enum PredicateKind
    {
        And, Or, Delivered, Exited, Resolved, InfrastructureReady, LoadoutsReady, ProtectedObjectsIntact, SourcesDisabled,
        OutpostOperational, RouteObserved, Destroyed, FlagshipAlive, BeforeDeadline, MainSideDefeated,
    }

    /// <summary>A condition as data: a kind, its named arguments, and for And and Or the branches.</summary>
    public sealed class Predicate
    {
        public PredicateKind Kind;
        public string[] Args = Array.Empty<string>();
        public Predicate[] Children = Array.Empty<Predicate>();

        public static Predicate Leaf(PredicateKind kind, params string[] args) => new Predicate { Kind = kind, Args = args };
        public static Predicate All(params Predicate[] branches) => new Predicate { Kind = PredicateKind.And, Children = branches };
        public static Predicate Any(params Predicate[] branches) => new Predicate { Kind = PredicateKind.Or, Children = branches };

        /// <summary>How many arguments a leaf takes: exactly Least when Most is the same, at least Least when Most is -1.</summary>
        public static (int least, int most) Arity(PredicateKind kind)
        {
            switch (kind)
            {
                case PredicateKind.And:
                case PredicateKind.Or:
                case PredicateKind.FlagshipAlive: return (0, 0);
                case PredicateKind.Delivered: return (3, 3);
                case PredicateKind.Exited: return (2, 2);
                case PredicateKind.SourcesDisabled: return (1, -1);
                default: return (1, 1);
            }
        }

        /// <summary>The condition in the notation of the scenario document, e.g. Delivered(a, b, c) AND Exited(d, e).</summary>
        public string Text()
        {
            if (Kind == PredicateKind.And || Kind == PredicateKind.Or)
                return "(" + string.Join(Kind == PredicateKind.And ? " AND " : " OR ", (Children ?? Array.Empty<Predicate>()).Select(child => child == null ? "?" : child.Text())) + ")";
            return Kind + "(" + string.Join(", ", Args ?? Array.Empty<string>()) + ")";
        }

        /// <summary>
        /// The condition in full for the catalogue's hash: the kind, every argument with its length before it, and every
        /// branch, whatever the kind; null apart from empty.
        /// </summary>
        internal string Canonical()
        {
            var args = Args == null ? "~" : string.Join(",", Args.Select(arg => arg == null ? "~" : arg.Length + ":" + arg));
            var children = Children == null ? "~" : string.Join(",", Children.Select(child => child == null ? "~" : child.Canonical()));
            return Kind + "[" + args + "]{" + children + "}";
        }

        /// <summary>What is wrong with the condition's shape; empty when it is sound.</summary>
        public void Problems(List<string> problems)
        {
            var (least, most) = Arity(Kind);
            var args = Args ?? Array.Empty<string>();
            var children = Children ?? Array.Empty<Predicate>();
            if (Kind == PredicateKind.And || Kind == PredicateKind.Or)
            {
                if (children.Length < 2) problems.Add($"{Kind} needs two branches or more.");
                if (args.Length != 0) problems.Add($"{Kind} takes no arguments.");
                foreach (var child in children) { if (child == null) problems.Add($"{Kind} has an empty branch."); else child.Problems(problems); }
                return;
            }
            if (children.Length != 0) problems.Add($"{Kind} is a leaf and has branches.");
            if (args.Length < least || (most >= 0 && args.Length > most)) problems.Add($"{Kind} takes {(most < 0 ? least + " or more" : least.ToString())} arguments, not {args.Length}.");
            if (args.Any(string.IsNullOrWhiteSpace)) problems.Add($"{Kind} has an empty argument.");
        }
    }

    /// <summary>
    /// What goes on from an operation to the next (docs/NOWY_PLAN_GRY.md §9): by default the entitled flagship, its heroes
    /// and the objects in its places, and the evacuated store of a settlement; further hulls and crews only when the
    /// exit manifest names them.
    /// </summary>
    public sealed class CarryPolicy
    {
        public bool Flagship, FlagshipArtifacts, Heroes, EvacuatedStore;
        public string[] ExtraManifest = Array.Empty<string>();

        public static CarryPolicy Default() => new CarryPolicy { Flagship = true, FlagshipArtifacts = true, Heroes = true, EvacuatedStore = true };
        public static CarryPolicy Nothing() => new CarryPolicy();
    }

    /// <summary>
    /// One operation of the campaign (K01-K14) or one skirmish (S00-S03): where and when, who takes part and how they
    /// stand to one another, what decides the result and what is carried on. The conditions of K01-K14 are the
    /// document's contracts; their quantities, zones and objects are bound by the scenario's own data later (P10), and an
    /// operation whose parameters are not bound may not start.
    /// </summary>
    public sealed class ScenarioDefinition
    {
        /// <summary>The side of a sector may not be larger than the fog and navigation domain, 104 cells of 25 m.</summary>
        public const double LargestSectorMeters = 2600;

        public string Id, Name, Place, Motif, Goal, NewRule, FailureExample;
        public ScenarioKind Kind;
        public string Ruleset;

        /// <summary>The year of the fictional action, BC; only where the operation needs one.</summary>
        public bool HasYear;
        public int YearBc;

        public SectorKind Sector;
        public double SectorMeters;
        public ScenarioSide[] Sides = Array.Empty<ScenarioSide>();

        /// <summary>The hostile pairs of owner slots, as SideRelations keeps them; every pair at war unless the scenario says otherwise.</summary>
        public int HostilePairs = SideRelations.AllHostile.HostilePairs;

        public Predicate Success;
        public CarryPolicy Carry = CarryPolicy.Nothing();

        /// <summary>The running game can play it.</summary>
        public bool Implemented;

        /// <summary>Every quantity, zone, route and object the conditions name has a value in the scenario's data.</summary>
        public bool ParametersBound;
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("Scenario", Id).F("Name", Name).F("Place", Place).F("Motif", Motif).F("Goal", Goal).F("NewRule", NewRule).F("FailureExample", FailureExample)
                .F("Kind", Kind.ToString()).F("Ruleset", Ruleset).F("HasYear", HasYear).F("YearBc", YearBc).F("Sector", Sector.ToString()).F("SectorMeters", SectorMeters)
                .F("Sides", Sides?.Select(side => side == null ? null : side.Slot + ":" + side.Faction + ":" + side.Controller)).F("HostilePairs", HostilePairs)
                .F("Success", Success?.Canonical()).F("Carry", Carry != null)
                .F("Carry.Flagship", Carry != null && Carry.Flagship).F("Carry.FlagshipArtifacts", Carry != null && Carry.FlagshipArtifacts).F("Carry.Heroes", Carry != null && Carry.Heroes)
                .F("Carry.EvacuatedStore", Carry != null && Carry.EvacuatedStore).F("Carry.ExtraManifest", Carry?.ExtraManifest)
                .F("Implemented", Implemented).F("ParametersBound", ParametersBound);
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>Whether a scenario's data is sound, and whether it may start.</summary>
    public static class ScenarioRules
    {
        public static List<string> Problems(ScenarioDefinition scenario)
        {
            var problems = new List<string>();
            if (Array.IndexOf(Rulesets.All, scenario.Ruleset) < 0) problems.Add("Unknown ruleset.");
            if (scenario.Kind == ScenarioKind.Skirmish && (scenario.Ruleset != Rulesets.Skirmish || scenario.HasYear)) problems.Add("A skirmish is played under the skirmish ruleset and has no date.");
            if (scenario.Kind == ScenarioKind.Campaign && scenario.Ruleset == Rulesets.Skirmish) problems.Add("A campaign operation is not played under the skirmish ruleset.");
            if (scenario.HasYear && (scenario.YearBc < GameEpoch.LatestYearBc || scenario.YearBc > GameEpoch.EarliestYearBc)) problems.Add("The year lies outside the game's epoch.");
            if (!(scenario.SectorMeters > 0) || scenario.SectorMeters > ScenarioDefinition.LargestSectorMeters) problems.Add("The sector is larger than the domain of the map, or has no size.");
            var sides = (scenario.Sides ?? Array.Empty<ScenarioSide>()).Where(side => side != null).ToList();
            if (scenario.Sides == null || sides.Count != scenario.Sides.Length) problems.Add("The list of sides is missing or has an empty entry.");
            var slots = sides.Select(side => side.Slot).ToList();
            if (slots.Any(slot => !Owners.IsValid(slot))) problems.Add("A side is given a slot that is not.");
            if (slots.Distinct().Count() != slots.Count) problems.Add("A slot is given twice.");
            if (!slots.Contains(Owners.Human) || !slots.Contains(Owners.MainAi)) problems.Add("Both main sides must take part.");
            foreach (var side in sides)
            {
                var expected = side.Slot == Owners.Human ? SideController.Human : side.Slot == Owners.MainAi ? SideController.MainAi : SideController.Encounters;
                if (Owners.IsValid(side.Slot) && side.Controller != expected) problems.Add($"Slot {side.Slot} is played by {expected}, not {side.Controller}.");
            }
            if (scenario.HostilePairs != new SideRelations(scenario.HostilePairs).HostilePairs) problems.Add("The relations name a pair that is not.");
            if (scenario.Success == null) problems.Add("No condition of success.");
            else scenario.Success.Problems(problems);
            if (scenario.Carry == null) problems.Add("No carry policy.");
            return problems;
        }

        /// <summary>A scenario starts only when the game can play it, its data is sound and every parameter of its conditions is bound: missing data blocks the start, it never grants the goal.</summary>
        public static bool CanStart(ScenarioDefinition scenario) => scenario.Implemented && scenario.ParametersBound && Problems(scenario).Count == 0;
    }

    static partial class CatalogData
    {
        const string ScenarioDoc = "docs/MAPY_I_SCENARIUSZE.md";

        static ScenarioSide[] SidesOf(bool independent)
        {
            var sides = new List<ScenarioSide>
            {
                new ScenarioSide { Slot = Owners.Human, Controller = SideController.Human },
                new ScenarioSide { Slot = Owners.MainAi, Controller = SideController.MainAi },
            };
            if (independent) sides.Add(new ScenarioSide { Slot = Owners.Independent, Controller = SideController.Encounters });
            return sides.ToArray();
        }

        internal static List<ScenarioDefinition> Scenarios()
        {
            Predicate Delivered(string delivery, string manifest, string recipient) => Predicate.Leaf(PredicateKind.Delivered, delivery, manifest, recipient);
            Predicate Exited(string manifest, string zone) => Predicate.Leaf(PredicateKind.Exited, manifest, zone);
            Predicate Destroyed(string target) => Predicate.Leaf(PredicateKind.Destroyed, target);

            // A place of the real world: the operation's story is fiction, and what the place had in the fourth century
            // needs its own source card before the mission's data is written (P10).
            SourceNote PlaceCard(int number) => new SourceNote
            {
                Status = SourceStatus.Undetermined, Layer = WorldLayer.Material,
                Locator = ScenarioDoc + " §4–§5, K" + number.ToString("00") + ". Miejsce rzeczywiste, przebieg fikcyjny; lokalna karta źródłowa do odczytania przed danymi misji (P10).",
            };
            ScenarioDefinition Operation(int number, string name, string place, string motif, string goal, string newRule, SectorKind sector, double meters, string ruleset,
                bool independent, Predicate success, string failure, SourceNote source) => new ScenarioDefinition
            {
                Id = "K" + number.ToString("00"), Name = name, Place = place, Motif = motif, Goal = goal, NewRule = newRule, FailureExample = failure,
                Kind = ScenarioKind.Campaign, Ruleset = ruleset, Sector = sector, SectorMeters = meters, Sides = SidesOf(independent),
                Success = success, Carry = CarryPolicy.Default(), Implemented = false, ParametersBound = false, Source = source ?? PlaceCard(number),
            };
            const string material = Rulesets.CampaignMaterial, mythic = Rulesets.CampaignMythic;
            const SectorKind geo = SectorKind.Geographic;

            var list = new List<ScenarioDefinition>
            {
                Operation(1, "Pireus", "Podejście do Pireusu", "Ochrona portu i przydział materiałów", "Doprowadzić dwa transporty i rozładować wymagany towar.",
                    "Ruch, ostrzał, przeładunek i eskorta", geo, 1600, material, false,
                    Predicate.All(Delivered("DostawaT1", "ManifestT1", "PortOdbioru"), Delivered("DostawaT2", "ManifestT2", "PortOdbioru")),
                    "Niezastępowalny transport i jego ładunek utracone bez legalnego odzysku.", null),
                Operation(2, "Laurion", "Wybrzeże Thorikos–Laurion", "Praca zaplecza i prawo do partii srebra", "Utrzymać odbiór skończonych partii i wyprowadzić budżet.",
                    "Zbiór strefowy, rezerwacje i wyprawowa baza", geo, 2200, material, false,
                    Predicate.All(Delivered("DostawaPartii", "WymaganyProdukt", "PortPrzeładunku"), Exited("BudżetWyjścia", "Strefa")),
                    "Pozostałe partie i odzysk nie wystarczają do wymaganej ilości.", null),
                Operation(3, "Zea", "Wybrane wejście zatoki Zea", "Rzemiosło i patronat Ateny", "Ukończyć wyposażenie, odeprzeć atak i nie zablokować wodowania.",
                    "Warsztat, kuźnia, zestaw domyślny i dok", geo, 1600, material, false,
                    Predicate.All(Predicate.Leaf(PredicateKind.InfrastructureReady, "Wymagane"), Predicate.Leaf(PredicateKind.LoadoutsReady, "Eskadra"), Predicate.Leaf(PredicateKind.Resolved, "W1")),
                    "Utrata niezastępowalnego punktu rozwoju albo brak legalnej możliwości wyposażenia eskadry.", null),
                Operation(4, "Salamina", "Przejście koło Salaminy", "Ajas i pamięć bohatera", "Zdobyć dokument z wrogiego okrętu i dowieźć go do osady.",
                    "Abordaż, załoga pryzowa i przedmiot kluczowy", geo, 2200, material, false,
                    Delivered("DostawaDokumentu", "DokumentId", "OsadaOdbiorcza"), "Dokument bezpowrotnie utracony, bez zastępczego celu.", null),
                Operation(5, "Eleusis", "Zatoka Eleusis", "Demeter i obieg świętego zboża", "Ochronić konwój przed strażnikami i dostarczyć dar.",
                    "Artefakty, społeczny kontekst sanktuarium i pierwszy znak rekrutacyjny", geo, 2200, mythic, true,
                    Delivered("DostawaKonwoju", "DarId", "PortSanktuarium"), "Utrata wymaganego daru bez możliwości odzysku.", null),
                Operation(6, "Sunion", "Przylądek Sunion", "Posejdon i ryzyko opłynięcia przylądka", "Przepłynąć przez zmieniające się zagrożenia, ocalić flagowiec i minimum floty.",
                    "Sanktuarium Morza, wir, rafa i pilot", geo, 2600, mythic, true,
                    Predicate.All(Exited("Flagowiec", "Strefa"), Exited("MinimumFloty", "Strefa")), "Flagowiec utracony albo ocalały manifest nie może spełnić minimum.", null),
                Operation(7, "Brauron", "Podejście do Brauron", "Artemis i ochrona świętego miejsca", "Odbić dar bez zniszczenia wskazanych obiektów sanktuarium.",
                    "Zwiad, selekcja celów i postać łucznicza", geo, 1600, mythic, true,
                    Predicate.All(Delivered("DostawaDaru", "DarId", "PortId"), Predicate.Leaf(PredicateKind.ProtectedObjectsIntact, "ZbiórId")),
                    "Zniszczenie jawnie chronionego, niezastępowalnego obiektu.", null),
                Operation(8, "Egina", "Wycinek wybrzeża Eginy", "Ajakos i straż wyspy", "Unieszkodliwić dwa ośrodki odtwarzania i zdobyć przyczółek.",
                    "Niezależne flotylle, trzy klasy fortów i niszczenie źródła", geo, 2200, mythic, true,
                    Predicate.All(Predicate.Leaf(PredicateKind.SourcesDisabled, "S1", "S2"), Predicate.Leaf(PredicateKind.OutpostOperational, "P1")),
                    "Brak możliwości odtworzenia wymaganego przyczółka przy pozostałym budżecie i dostępie.", null),
                Operation(9, "Delos", "Schematyczny sektor okolic Delos", "Apollo i morska droga do sanktuarium", "Rozpoznać prawdziwy szlak, zabezpieczyć eskortę poselstwa.",
                    "Ujawnienie, niewidzialność, patrol i ochrona obszaru", SectorKind.Schematic, 2200, mythic, true,
                    Predicate.All(Predicate.Leaf(PredicateKind.RouteObserved, "R1"), Delivered("DostawaPoselstwa", "PoselstwoId", "PortId")), "Utrata niezastępowalnego poselstwa.", null),
                Operation(10, "Zatoka Powracających", "Sektor mityczny", "Syreny i próba rozpoznania zwodniczej obietnicy", "Odzyskać wskazany przedmiot, nie tracąc wszystkich statków do wabienia.",
                    "Śpiew, osłony i bohater odporny na wpływ", SectorKind.Mythic, 2200, mythic, true,
                    Delivered("DostawaPrzedmiotu", "PrzedmiotId", "PortWyjścia"), "Utrata ostatniego legalnego nośnika lub przedmiotu bez drogi odzysku.",
                    Myth(Odyssey + ". Akwen fikcyjny; " + ScenarioDoc + " §4 K10.")),
                Operation(11, "Lustrzana Skała", "Sektor mityczny", "Gorgona i spojrzenie pośrednie", "Zabrać znak z trzech punktów albo przełamać straż lustrem.",
                    "Budżet spojrzenia, rozdział celów i H09", SectorKind.Mythic, 1600, mythic, true,
                    Delivered("DostawaZnaku", "ZnakR09", "PortWyjścia"), "Brak znaku i brak możliwości wykonania którejkolwiek gałęzi.",
                    Myth(Apollodoros + ". Akwen fikcyjny; " + ScenarioDoc + " §4 K11.")),
                Operation(12, "Wyspy Kowala", "Sektor mityczny", "Hefajstos, ogień i praca nad materią", "Zniszczyć Przybytek Ognia i utrzymać transport wyposażenia.",
                    "Pełna artyleria, ogień, mgła obezwładniająca i odnowa", SectorKind.Mythic, 2600, mythic, true,
                    Predicate.All(Destroyed("PrzybytekOgniaId"), Delivered("DostawaWyposażenia", "Wyposażenie", "PortOdbioru")), "Utrata niezastępowalnego wyposażenia bez dozwolonego zastępstwa.",
                    MythUnread(ScenarioDoc + " §4 K12. Akwen fikcyjny")),
                Operation(13, "Przejście Boreasza", "Sektor mityczny", "Wiatr, trąby i powrót z wyprawy", "Wyprowadzić ruchomy cel i artefakty przed końcem okna.",
                    "Wiatr logiczny, odporności i pozycja powrotu", SectorKind.Mythic, 2200, mythic, true,
                    Predicate.All(Exited("CelRuchomyId", "Strefa"), Exited("Artefakty", "Strefa"), Predicate.Leaf(PredicateKind.BeforeDeadline, "Deadline")),
                    "Upływ terminu albo utrata krytycznego celu bez zastępstwa.", MythUnread(ScenarioDoc + " §4 K13. Akwen fikcyjny")),
                Operation(14, "Podejście do Attyki", "Odrębny sektor", "Konflikt stronnictw i próba utrzymania przysięgi", "Zrealizować złożony cel żeglugi albo przełamać główny ośrodek przeciwnika.",
                    "Cały katalog, niezależna trzecia siła i stan kampanii", SectorKind.Schematic, 2600, mythic, true,
                    Predicate.All(Predicate.Leaf(PredicateKind.FlagshipAlive),
                        Predicate.Any(Predicate.All(Delivered("DostawaKonwojów", "Manifest", "PortOdbioru"), Exited("Ewakuowani", "Strefa")), Destroyed("GłównyPortCelu"))),
                    "Utrata flagowca albo konkretnie wykazana niewykonalność obu gałęzi.", Rule(ScenarioDoc + " §4 K14. Nie jest rekonstrukcją nazwanej bitwy.")),
            };

            ScenarioDefinition Skirmish(int number, string name, string goal, double meters, bool independent, bool implemented) => new ScenarioDefinition
            {
                Id = "S0" + number, Name = name, Place = "Akwen potyczki", Motif = "Lokalna gra jednego człowieka przeciw AI", Goal = goal, NewRule = "",
                FailureExample = "Upadek własnego portu macierzystego albo utrata floty bez możliwości odbudowy.",
                Kind = ScenarioKind.Skirmish, Ruleset = Rulesets.Skirmish, Sector = SectorKind.Schematic, SectorMeters = meters, Sides = SidesOf(independent),
                Success = Predicate.Leaf(PredicateKind.MainSideDefeated, "1"), Carry = CarryPolicy.Nothing(), Implemented = implemented, ParametersBound = implemented,
                Source = Rule(ScenarioDoc + " §6" + (implemented ? "; MapFactory.CreateSkirmish" : "")),
            };
            list.Add(Skirmish(0, "Potyczka: dwa porty i cztery pary złóż", "Pokonać drugą główną stronę.", ScenarioDefinition.LargestSectorMeters, false, true));
            list.Add(Skirmish(1, "Zatoka i dwa wyjścia", "Eskorta lub przełamanie; jedna baza na stronę, boczne szlaki.", 1600, false, false));
            list.Add(Skirmish(2, "Archipelag trzech szlaków", "Dwie drogi osłonowe i otwarty środek, przyczółki, płycizny, jeden neutralny cel ekonomiczny.", 2200, false, false));
            list.Add(Skirmish(3, "Pierścień strażników", "Przewaga operacyjna bez obowiązku zabijania każdego stworzenia; strefa niezależnej flotylli.", 2600, true, false));
            return list;
        }
    }
}
