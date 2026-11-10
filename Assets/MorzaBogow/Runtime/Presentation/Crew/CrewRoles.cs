using MorzaBogow.Simulation;

namespace MorzaBogow.Presentation
{
    /// <summary>A man's trade in the simulation (Ship.Sailors, Swordsmen, Javelins, Archers), or one of its wounded.</summary>
    public enum CrewTrade { Sailor, Swordsman, Javelin, Archer, Wounded }

    /// <summary>
    /// What a man does aboard as drawn: the role of the presentation. It is not the game's trade (CrewTrade) and not a
    /// historical office: one man can fill more than one function (a warrior rows at sea and fights at his station; the
    /// helmsman of a boat also commands it), and no role adds a weapon, an attack or a fighting trade the data lacks.
    /// </summary>
    public enum CrewRole
    {
        Helmsman, Rower, WarriorRower, Lookout, Commander, Skipper, SailHand, LineHand, BowHand,
        BankArcher, BankJavelin, MachineCrew, Marine, Wounded,
    }

    public enum CrewPosture { Sit, Stand, Kneel, Lie }

    /// <summary>One role: who fills it, what it stands for in history and how sure that is (docs/art/CrewRoles.json).</summary>
    public sealed class CrewRoleInfo
    {
        public CrewRole Role;
        /// <summary>Polish name shown in the docs.</summary>
        public string Name;
        /// <summary>The trades that may fill it, in order of preference.</summary>
        public CrewTrade[] Trades;
        /// <summary>The historical function it draws, and the level of certainty (ustalenie, interpretacja, spekulacja, anachronizm gry).</summary>
        public string Function, Certainty;
        /// <summary>Reference ids of docs/art/ReferenceRegister.json behind it.</summary>
        public string[] Sources;
        /// <summary>What the man wears and carries.</summary>
        public string Kit;
    }

    /// <summary>
    /// The roles of the crews and their grounds for Greek ships of the 4th century BC, the project's anchor since 2026-11-10
    /// (docs/art/HistoricalConstraints.md): rowers on benches facing aft; a helmsman (kybernetes) at the steering oars;
    /// marines (epibatai) with the pilos, some in linen corselets, and the aspis; peltasts with the pelte and javelins after
    /// Iphicrates' reform; archers; an officer (trierarch) in a bronze cuirass; a bow officer watching ahead. Ship-borne
    /// engines are known from Tyre, 332 BC: an early interpretation, no longer an anachronism. The keleustes and the
    /// piper are looks of men already aboard, never extra men.
    /// </summary>
    public static class CrewRoles
    {
        static readonly CrewTrade[] Sailors = { CrewTrade.Sailor };
        static readonly CrewTrade[] Fighters = { CrewTrade.Swordsman, CrewTrade.Javelin, CrewTrade.Archer };

        public static readonly CrewRoleInfo[] All =
        {
            new CrewRoleInfo { Role = CrewRole.Helmsman, Name = "sternik (kybernetes)", Trades = Sailors,
                Function = "steruje dwoma wiosłami sterowymi za rumple na rufie", Certainty = "interpretacja (dwa wiosła sterowe okrętów klasycznych, rekonstrukcja Olympias; tytuł klasyczny, tekst do odczytu)",
                Sources = new[] { "R-OLYMPIAS" }, Kit = "exomis lub chiton z niebarwionego lnu, czasem filcowa czapka; bez pancerza" },
            new CrewRoleInfo { Role = CrewRole.Rower, Name = "wioślarz", Trades = Sailors,
                Function = "siedzi na ławie twarzą ku rufie i ciągnie jedno wiosło; rytm podają keleustes i flecista (wygląd ludzi z obsady, bez dodatkowych ludzi)", Certainty = "rekonstrukcja (triera Olympias, jeden rząd w grze); spekulacja (strój)",
                Sources = new[] { "R-OLYMPIAS" }, Kit = "przepaska lub exomis z niebarwionego lnu albo wełny, często nagi tors; bez broni" },
            new CrewRoleInfo { Role = CrewRole.WarriorRower, Name = "żołnierz przy wiośle", Trades = Fighters,
                Function = "żołnierz bez stanowiska siada w rejsie do wolnego wiosła i odchodzi na stanowisko w gotowości bojowej", Certainty = "interpretacja (dana gry; żołnierze przy wiosłach w potrzebie)",
                Sources = new[] { "R-THUCYDIDES" }, Kit = "jak epibates, broń i tarcza odłożone przy ławie" },
            new CrewRoleInfo { Role = CrewRole.Lookout, Name = "oficer dziobowy (prorates)", Trades = new[] { CrewTrade.Javelin, CrewTrade.Archer, CrewTrade.Swordsman, CrewTrade.Sailor },
                Function = "czuwa na dziobie i podaje znaki sternikowi", Certainty = "interpretacja (tytuł klasyczny, tekst do odczytu)",
                Sources = new string[0], Kit = "peltasta lub marynarz" },
            new CrewRoleInfo { Role = CrewRole.Commander, Name = "dowódca (trierarcha)", Trades = Fighters,
                Function = "dowodzi z rufy; jedyny w pancerzu z brązu", Certainty = "interpretacja (trierarchowie ateńskiej floty IV w.; strój z ikonografii do odczytu)",
                Sources = new[] { "R-NAVALLISTS" }, Kit = "pancerz mięśniowy z brązu, nagolenniki, hełm koryncki lub beocki z grzebieniem, aspis, włócznia i miecz" },
            new CrewRoleInfo { Role = CrewRole.Skipper, Name = "szyper (naukleros)", Trades = Sailors,
                Function = "prowadzi statek kupiecki przy sterniku", Certainty = "interpretacja (statek z Kyrenii; tytuł klasyczny)",
                Sources = new[] { "R-KYRENIA" }, Kit = "dłuższy chiton, płaszcz (himation); bez broni" },
            new CrewRoleInfo { Role = CrewRole.SailHand, Name = "marynarz przy żaglu", Trades = Sailors,
                Function = "ciągnie gejtawy przy zwijaniu i stawianiu żagla; przed walką żagiel zwinięty", Certainty = "ustalenie (żagiel z gejtawami: Kyrenia; duże żagle zostawiane przed walką: Hellenika 6.2.27); interpretacja (osobni ludzie przy żaglu)",
                Sources = new[] { "R-KYRENIA", "R-XENOPHON-HELLENICA" }, Kit = "jak wioślarz" },
            new CrewRoleInfo { Role = CrewRole.LineHand, Name = "marynarz przy linach i ładunku", Trades = Sailors,
                Function = "pilnuje lin i ładunku, wylewa wodę", Certainty = "interpretacja",
                Sources = new[] { "R-KYRENIA" }, Kit = "jak wioślarz, czasem czerpak lub lina" },
            new CrewRoleInfo { Role = CrewRole.BowHand, Name = "dziobowy łodzi", Trades = Sailors,
                Function = "na dziobie łodzi z bosakiem, wypatruje mielizn i podaje liny przy brzegu", Certainty = "interpretacja",
                Sources = new string[0], Kit = "jak wioślarz, z bosakiem" },
            new CrewRoleInfo { Role = CrewRole.BankArcher, Name = "łucznik stanowiska", Trades = new[] { CrewTrade.Archer },
                Function = "czterech łuczników na stanowisku za przedpiersiem strzela salwami w cyklu broni", Certainty = "interpretacja (łucznicy na okrętach klasycznych; łucznicy kreteńscy, źródło do odczytu); stanowisko to dana gry",
                Sources = new string[0], Kit = "krótki chiton, łuk, kołczan; bez hełmu albo w czapce" },
            new CrewRoleInfo { Role = CrewRole.BankJavelin, Name = "peltasta stanowiska", Trades = new[] { CrewTrade.Javelin },
                Function = "czterech peltastów na stanowisku rzuca oszczepami w cyklu broni", Certainty = "ustalenie (peltaści IV w.: Diodor 15.44, Ksenofont, Hellenika 4.5.13); stanowisko to dana gry",
                Sources = new[] { "R-IPHICRATES" }, Kit = "tunika, czapka lub pilos, tarcza pelte, pęk oszczepów" },
            new CrewRoleInfo { Role = CrewRole.MachineCrew, Name = "obsługa machiny", Trades = Fighters,
                Function = "napina, ładuje, celuje i wyzwala machinę stanowiska", Certainty = "wczesna interpretacja (katapulta od 399 r.: Diodor 14.42; machiny na okrętach pod Tyrem 332 r.: Arrian 2.21)",
                Sources = new[] { "R-DIODORUS-14", "R-ARRIAN-TYRE" }, Kit = "tunika, filcowa czapka lub pilos; tarcza i broń odłożone przy stanowisku" },
            new CrewRoleInfo { Role = CrewRole.Marine, Name = "epibates (żołnierz okrętowy)", Trades = Fighters,
                Function = "żołnierze na pokładzie i pomostach: walka wręcz przy abordażu; zapasowi łucznicy i peltaści wspierają zza frontu", Certainty = "interpretacja (epibatai okrętów klasycznych; pilos: R-PILOS, lniany pancerz: Nepos)",
                Sources = new[] { "R-PILOS", "R-IPHICRATES" }, Kit = "tunika w barwie strony (konwencja gry), hełm pilos (część koryncki lub beocki), część w lnianym pancerzu, tarcza aspis, włócznia, miecz" },
            new CrewRoleInfo { Role = CrewRole.Wounded, Name = "ranny", Trades = new[] { CrewTrade.Wounded },
                Function = "ranni z liczby Wounded siedzą lub leżą przy maszcie do leczenia w porcie", Certainty = "dana gry",
                Sources = new string[0], Kit = "bez broni, bandaż z lnu" },
        };

        public static CrewRoleInfo Of(CrewRole role) => All[(int)role];

        [System.Serializable] sealed class RoleFile { public string opis; public RoleEntry[] role; }

        [System.Serializable]
        sealed class RoleEntry
        {
            public string rola, nazwa, funkcjaHistoryczna, pewnosc, wyposazenie;
            public string[] zawodyWGrze, zrodla;
        }

        /// <summary>The manifest of roles as written to docs/art/CrewRoles.json (CrewExport); a test keeps the file equal.</summary>
        public static string Json()
        {
            var entries = new RoleEntry[All.Length];
            for (var i = 0; i < All.Length; i++)
            {
                var info = All[i];
                var trades = new string[info.Trades.Length];
                for (var t = 0; t < trades.Length; t++) trades[t] = info.Trades[t].ToString();
                entries[i] = new RoleEntry
                {
                    rola = info.Role.ToString(), nazwa = info.Name, funkcjaHistoryczna = info.Function, pewnosc = info.Certainty,
                    wyposazenie = info.Kit, zawodyWGrze = trades, zrodla = info.Sources,
                };
            }
            var file = new RoleFile
            {
                opis = "Manifest ról załóg: rola prezentacji, zawody gry, które ją pełnią, funkcja historyczna, pewność i źródła (docs/art/ReferenceRegister.json). Generowany z CrewRoles (CrewExport).",
                role = entries,
            };
            return UnityEngine.JsonUtility.ToJson(file, true).Replace("\r\n", "\n") + "\n";
        }

        /// <summary>The simulation's count of one trade.</summary>
        public static int Count(Ship ship, CrewTrade trade) => trade switch
        {
            CrewTrade.Sailor => ship.Sailors,
            CrewTrade.Swordsman => ship.Swordsmen,
            CrewTrade.Javelin => ship.Javelins,
            CrewTrade.Archer => ship.Archers,
            _ => ship.Wounded,
        };

        /// <summary>A role that fights at its station (a post's crew or a deck warrior), as opposed to the ship's working crew.</summary>
        public static bool Fights(CrewRole role) =>
            role == CrewRole.BankArcher || role == CrewRole.BankJavelin || role == CrewRole.MachineCrew || role == CrewRole.Marine || role == CrewRole.Commander;
    }
}
