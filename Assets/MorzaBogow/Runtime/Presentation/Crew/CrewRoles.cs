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
    /// The roles of the crews and their grounds, after the evidence for Aegean galleys of the 12th century BC
    /// (docs/art/HistoricalConstraints.md): rowers levied from settlements (the Pylos rower tablets), seated low along the
    /// sides facing aft; a steersman at the steering oars in the stern; warriors on deck with helmets, shields, spears and
    /// javelins, full armour for a leader only; a man watching at the bow. No master of the rowers: the keleustes belongs to
    /// the classical trireme. Machines are the game's anachronism; the men who work them wear the age's kit.
    /// </summary>
    public static class CrewRoles
    {
        static readonly CrewTrade[] Sailors = { CrewTrade.Sailor };
        static readonly CrewTrade[] Fighters = { CrewTrade.Swordsman, CrewTrade.Javelin, CrewTrade.Archer };

        public static readonly CrewRoleInfo[] All =
        {
            new CrewRoleInfo { Role = CrewRole.Helmsman, Name = "sternik", Trades = Sailors,
                Function = "steruje dwoma wiosłami sterowymi za rumple; podaje rytm wioślarzom (bez osobnego keleustesa)", Certainty = "ustalenie (sternik przy wiośle sterowym, Kynos A91); interpretacja (dwa wiosła, podawanie rytmu)",
                Sources = new[] { "R-KYNOS", "R-MEDINET" }, Kit = "krótka tunika lub przepaska, czasem hełm; bez pancerza" },
            new CrewRoleInfo { Role = CrewRole.Rower, Name = "wioślarz", Trades = Sailors,
                Function = "e-re-ta z tabliczek z Pylos: siedzi nisko przy burcie twarzą ku rufie i ciągnie jedno wiosło", Certainty = "ustalenie (wioślarze w tabliczkach i na wazach); spekulacja (strój)",
                Sources = new[] { "R-PYLOS-AN", "R-BADEMGEDIGI", "R-MEDINET" }, Kit = "przepaska lub krótka tunika z niebarwionego lnu albo wełny, nagi tors; bez broni" },
            new CrewRoleInfo { Role = CrewRole.WarriorRower, Name = "wojownik przy wiośle", Trades = Fighters,
                Function = "wojownik bez stanowiska siada w rejsie do wolnego wiosła i odchodzi na stanowisko w gotowości bojowej", Certainty = "interpretacja (Homer, Il. 2.718–720; Tukidydes 1.10.4)",
                Sources = new[] { "R-HOMER", "R-THUCYDIDES" }, Kit = "jak wojownik, broń i tarcza odłożone przy ławie" },
            new CrewRoleInfo { Role = CrewRole.Lookout, Name = "obserwator", Trades = new[] { CrewTrade.Javelin, CrewTrade.Archer, CrewTrade.Swordsman, CrewTrade.Sailor },
                Function = "ktoś czuwa na dziobie: funkcja tak, osobny tytuł nie", Certainty = "interpretacja (Kynos A91–A92; Od. 12.228–229); tytuł prōireus jest późniejszy",
                Sources = new[] { "R-KYNOS", "R-HOMER" }, Kit = "wojownik z oszczepami na galerze; marynarz na statku i łodzi" },
            new CrewRoleInfo { Role = CrewRole.Commander, Name = "dowódca", Trades = Fighters,
                Function = "przywódca na pomoście rufowym; jedyny w pełnym rynsztunku", Certainty = "interpretacja (Waza Wojowników; groby wojowników LH IIIC w Achai; tytuł na okręcie nieznany)",
                Sources = new[] { "R-WARRIORVASE", "R-ACHAEA" }, Kit = "napierśnik, nagolenniki, hełm, tarcza, włócznia i miecz typu Naue II" },
            new CrewRoleInfo { Role = CrewRole.Skipper, Name = "szyper", Trades = Sailors,
                Function = "właściciel lub prowadzący statek kupiecki przy sterniku", Certainty = "spekulacja (funkcja konieczna na statku handlowym, bez źródła dla XII w.)",
                Sources = new[] { "R-ULUBURUN" }, Kit = "dłuższa tunika, płaszcz; bez broni" },
            new CrewRoleInfo { Role = CrewRole.SailHand, Name = "marynarz przy żaglu", Trades = Sailors,
                Function = "ciągnie gejtawy przy zwijaniu i stawianiu żagla; cała załoga stawia maszt i żagiel", Certainty = "ustalenie (żagiel z gejtawami, Medinet Habu, Kynos); interpretacja (osobni ludzie przy żaglu)",
                Sources = new[] { "R-MEDINET", "R-KYNOS", "R-HOMER" }, Kit = "jak wioślarz" },
            new CrewRoleInfo { Role = CrewRole.LineHand, Name = "marynarz przy linach i ładunku", Trades = Sailors,
                Function = "pilnuje lin i ładunku, wylewa wodę", Certainty = "interpretacja",
                Sources = new[] { "R-ULUBURUN" }, Kit = "jak wioślarz, czasem czerpak lub lina" },
            new CrewRoleInfo { Role = CrewRole.BowHand, Name = "dziobowy łodzi", Trades = Sailors,
                Function = "na dziobie łodzi z bosakiem, wypatruje mielizn i podaje liny przy brzegu", Certainty = "interpretacja",
                Sources = new string[0], Kit = "jak wioślarz, z bosakiem" },
            new CrewRoleInfo { Role = CrewRole.BankArcher, Name = "łucznik stanowiska", Trades = new[] { CrewTrade.Archer },
                Function = "czterech łuczników na stanowisku za plecionym przedpiersiem strzela salwami w cyklu broni", Certainty = "spekulacja/interpretacja (łucznicy na okrętach egejskich rzadcy: Kynos A92; wioślarze-łucznicy u Homera); stanowisko to dana gry",
                Sources = new[] { "R-KYNOS", "R-HOMER", "R-MEDINET" }, Kit = "krótka tunika, łuk, kołczan, czasem hełm" },
            new CrewRoleInfo { Role = CrewRole.BankJavelin, Name = "oszczepnik stanowiska", Trades = new[] { CrewTrade.Javelin },
                Function = "czterech oszczepników na stanowisku rzuca w cyklu broni", Certainty = "ustalenie (wojownicy z oszczepami na okrętach, Kynos); stanowisko to dana gry",
                Sources = new[] { "R-KYNOS", "R-BADEMGEDIGI" }, Kit = "tunika, hełm, mała tarcza, pęk oszczepów" },
            new CrewRoleInfo { Role = CrewRole.MachineCrew, Name = "obsługa machiny", Trades = Fighters,
                Function = "napina, ładuje, celuje i wyzwala machinę stanowiska", Certainty = "anachronizm gry (machiny miotające nie istniały w XII w. p.n.e.)",
                Sources = new string[0], Kit = "tunika i hełm; tarcza i broń odłożone przy stanowisku" },
            new CrewRoleInfo { Role = CrewRole.Marine, Name = "wojownik pokładowy", Trades = Fighters,
                Function = "wojownicy na pokładzie i pomostach: walka wręcz przy abordażu; zapasowi łucznicy i oszczepnicy wspierają zza frontu", Certainty = "ustalenie (wojownicy na pokładach, Kynos, Bademgediği, Medinet Habu)",
                Sources = new[] { "R-KYNOS", "R-BADEMGEDIGI", "R-MEDINET", "R-WARRIORVASE" }, Kit = "tunika w barwie strony (konwencja gry), hełm jeżowy, pierzasty lub rogaty, tarcza okrągła lub klepsydrowa, włócznia lub oszczepy albo łuk" },
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
