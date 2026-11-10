using System;
using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The matrix of ship classes and their crews (docs/art/ShipCrewCoverage.json), made from the game's data alone:
    /// propulsion, the weapons each post may carry, the roles and stations, how many men are drawn at sea and ready to
    /// fight, and what they do cruising, manoeuvring, attacking, boarding and when struck, with the levels of detail. A
    /// test keeps the file equal to this, so the matrix never claims a weapon, attack or role the data does not have.
    /// </summary>
    public static class CrewCoverage
    {
        [Serializable] public sealed class File { public string opis; public string datowanie; public string zrodlo; public ClassEntry[] klasy; }

        [Serializable]
        public sealed class ClassEntry
        {
            public string klasa, typ;
            public Propulsion naped;
            public Weapon[] uzbrojenie;
            public bool taran, abordazAtak, abordazObrona, zbiorNaLadzie, transport;
            public Crew zaloga;
            public Count[] role, roleWGotowosci, stanowiska;
            public int postacieRejs, postacieGotowosc, bezMiejsca;
            public string[] rejs, manewr, atak, abordaz, uszkodzenia, lod;
        }

        [Serializable] public sealed class Propulsion { public int wiosla, wioslaNaBurte, rzedy; public bool wioslarzeStoja; public int marynarzeDoPelnejPredkosci; public string zagiel, ster; }
        [Serializable] public sealed class Weapon { public int stanowisko; public string rozmiar, domyslna; public string[] mozliwe; }
        [Serializable] public sealed class Crew { public string preset; public int marynarze, szermierze, oszczepnicy, lucznicy; }
        [Serializable] public sealed class Count { public string nazwa; public int liczba; }

        static readonly ShipClass[] Classes = (ShipClass[])Enum.GetValues(typeof(ShipClass));

        /// <summary>The weapons a post can carry: a light post keeps its bank, a medium or heavy one any machine of its size.</summary>
        public static WeaponType[] Allowed(ShipDefinition definition, int slot)
        {
            var size = definition.Slots[slot];
            if (size == SlotSize.Light)
                return definition.DefaultLoadout.Length > slot && definition.DefaultLoadout[slot] != WeaponType.None ? new[] { definition.DefaultLoadout[slot] } : Array.Empty<WeaponType>();
            return ((WeaponType[])Enum.GetValues(typeof(WeaponType))).Where(type => type != WeaponType.None && WeaponCatalog.Get(type).IsMachine && WeaponCatalog.Fits(type, size)).ToArray();
        }

        /// <summary>A ship of the class with its default crew and weapons, as it leaves the yard.</summary>
        public static Ship Default(ShipClass shipClass)
        {
            var world = new World(1);
            return world.SpawnShip(0, shipClass, 0, 0);
        }

        public static File Build()
        {
            var entries = new List<ClassEntry>();
            foreach (var shipClass in Classes) entries.Add(Entry(shipClass));
            return new File
            {
                opis = "Macierz klas okrętów i ich załóg, z danych gry (ShipCatalog, WeaponCatalog, World.Manned, GreekShips). Generowana przez CrewCoverage; test pilnuje zgodności.",
                datowanie = "Kotwica XII w. p.n.e. (LH IIIC), rama XII–V w. tylko tam, gdzie XII w. nic nie wyznacza (docs/art/HistoricalConstraints.md).",
                zrodlo = "Assets/MorzaBogow/Runtime/Presentation/Crew/CrewCoverage.cs",
                klasy = entries.ToArray(),
            };
        }

        public static string Json() => JsonUtility.ToJson(Build(), true).Replace("\r\n", "\n") + "\n";

        static Count[] Counted(IEnumerable<string> names) =>
            names.GroupBy(name => name).Select(group => new Count { nazwa = group.Key, liczba = group.Count() }).OrderBy(c => c.nazwa, StringComparer.Ordinal).ToArray();

        static ClassEntry Entry(ShipClass shipClass)
        {
            var definition = ShipCatalog.Get(shipClass);
            var ship = Default(shipClass);
            var profile = CrewLayoutProfile.For(shipClass);
            var parts = profile.Parts;
            var roster = new CrewRoster();
            roster.Reset(ship);
            var manned = World.Manned(ship);
            var cruise = CrewManning.Assign(profile, roster, ship.Mounts, manned, false);
            var ready = CrewManning.Assign(profile, roster, ship.Mounts, manned, true);
            var weapons = new List<Weapon>();
            for (var slot = 0; slot < definition.Slots.Length; slot++)
                weapons.Add(new Weapon
                {
                    stanowisko = slot, rozmiar = definition.Slots[slot].ToString(),
                    domyslna = (definition.DefaultLoadout.Length > slot ? definition.DefaultLoadout[slot] : WeaponType.None).ToString(),
                    mozliwe = Allowed(definition, slot).Select(type => type.ToString()).ToArray(),
                });
            return new ClassEntry
            {
                klasa = shipClass.ToString(), typ = parts.Form.TypeName,
                naped = new Propulsion
                {
                    wiosla = parts.OarPins.Length * 2, wioslaNaBurte = parts.OarPins.Length, rzedy = parts.Form.Banks, wioslarzeStoja = parts.RowersStand,
                    marynarzeDoPelnejPredkosci = definition.BaseSailors,
                    zagiel = "żagiel czworokątny na rei, zwijany gejtawami; siła żagla według sesji wiatru (World.Marine), obraz bez wpływu na ruch",
                    ster = parts.HelmStands.Length == 2 ? "dwa wiosła sterowe, sternik przy każdym" : "dwa wiosła sterowe z rumplami, jeden sternik",
                },
                uzbrojenie = weapons.ToArray(),
                taran = definition.CanRam,
                abordazAtak = shipClass != ShipClass.Cutter && !definition.IsEconomic,
                abordazObrona = true,
                zbiorNaLadzie = definition.CanHarvest,
                transport = shipClass == ShipClass.Merchant,
                zaloga = new Crew { preset = definition.DefaultPreset.ToString(), marynarze = ship.Sailors, szermierze = ship.Swordsmen, oszczepnicy = ship.Javelins, lucznicy = ship.Archers },
                role = Counted(cruise.Where(place => place.Station >= 0).Select(place => place.Role.ToString())),
                roleWGotowosci = Counted(ready.Where(place => place.Station >= 0).Select(place => place.Role.ToString())),
                stanowiska = Counted(profile.Stations.Select(station => station.Kind.ToString())),
                postacieRejs = cruise.Count(place => place.Station >= 0),
                postacieGotowosc = ready.Count(place => place.Station >= 0),
                bezMiejsca = cruise.Count(place => place.Station < 0),
                rejs = Cruise(shipClass, parts),
                manewr = Manoeuvre(shipClass),
                atak = Attack(shipClass, definition),
                abordaz = Boarding(shipClass, definition),
                uszkodzenia = Damage(shipClass, definition),
                lod = Lod(),
            };
        }

        static string[] Cruise(ShipClass shipClass, GreekShipParts parts)
        {
            var list = new List<string>
            {
                parts.RowersStand ? "wioślarze na stojąco przy dwóch parach wioseł dziobowej połowy, jeden rytm" : "wioślarze siedzą twarzą ku rufie, jeden rytm wszystkich wioseł (zaczep, napęd, wyjście, powrót), tempo z prędkości",
                parts.HelmSeated ? "sternik siedzi na tylnej ławie z rumplami w rękach" : "sternik stoi przy rumplach; ster wychyla się ze skrętem",
            };
            if (shipClass == ShipClass.Cutter) list.Add("dziobowy z bosakiem wypatruje mielizn; partia schodzi na brzeg przy zbiorze (ekipy brzegowe)");
            else if (shipClass == ShipClass.Merchant) list.Add("obserwator na dziobie, dwóch przy gejtawach, szyper przy sterniku, ludzie przy linach i ładunku");
            else
            {
                list.Add("dowódca na pomoście rufowym, obserwator na dziobie, dwóch marynarzy przy gejtawach (jeśli są wolni)");
                list.Add("wojownicy bez stanowiska siedzą przy wolnych wiosłach i w osi pokładu; obsługa stanowisk przy broni");
            }
            list.Add("postój: wiosła wciągnięte, wioślarze siedzą; bezczynność bez przypadkowych gestów");
            return list.ToArray();
        }

        static string[] Manoeuvre(ShipClass shipClass) => new[]
        {
            "ostry skręt: tempo spada, sternik przenosi ciężar ciała, stojący chwytają reling i poszerzają rozstaw stóp",
            "przechył i kiwanie przenosi kadłub (przestrzeń modelu), bez dodatkowego kołysania postaci",
            "burta z innym kadłubem w zasięgu wioseł wciąga wiosła, jej wioślarze trzymają rękojeść",
        };

        static string[] Attack(ShipClass shipClass, ShipDefinition definition)
        {
            var list = new List<string>();
            var weapons = definition.Slots.Select((_, slot) => Allowed(definition, slot)).SelectMany(types => types).Distinct().ToList();
            if (definition.CanRam) list.Add($"taran (mnożnik {definition.RamMultiplier:0.0}): podejście ze sprintem wioseł, zapowiedź (zaparcie się wojowników), uderzenie w ticku ram_hit, wiosłowanie wstecz przy odbiciu");
            if (weapons.Contains(WeaponType.ArcherBank)) list.Add("łucznicy stanowiska: sięgnięcie do kołczanu, nałożenie, naciąg na 0,6 s przed strzałem, zwolnienie w ticku weapon_fired, strzały z ich rąk");
            if (weapons.Contains(WeaponType.JavelinBank)) list.Add("oszczepnicy stanowiska: oszczep ze stojaka, zamach, rzut w ticku weapon_fired, odzyskanie równowagi");
            if (weapons.Any(type => type != WeaponType.ArcherBank && type != WeaponType.JavelinBank))
                list.Add("machiny (z danych stanowiska, także po wymianie w porcie): napinanie lub ściąganie ramienia w czasie przeładowania, ładowanie, obrót w łuku broni, wyzwolenie w ticku weapon_fired, pocisk z wylotu");
            if (list.Count == 0)
            {
                list.Add("brak ataku w danych: obserwator wskazuje zagrożenie, załoga kuli się przy trafieniu, bez fikcyjnej broni");
                return list.ToArray();
            }
            list.Add("wioślarze wiosłują dalej niezależnie od salwy");
            if (definition.CanRam && weapons.Count > 0) list.Add("przy rozkazie taranu okręt nie strzela (symulacja), więc obsługa stanowisk się zapiera");
            return list.ToArray();
        }

        static string[] Boarding(ShipClass shipClass, ShipDefinition definition)
        {
            if (shipClass == ShipClass.Cutter) return new[] { "łódź nie może abordażować (dane); zaatakowana nie ma wojowników, więc broni się tylko w danych poddania" };
            var list = new List<string>();
            if (!definition.IsEconomic) list.Add("atak: rzut haków (hooks_thrown), przyciąganie kadłuba atakującego (tylko obraz), przejście przez relingi z kontaktem dłoni i stóp, walka frontu (AttackerFront), wsparcie oszczepników i łuczników zza frontu");
            list.Add(definition.IsEconomic ? "obrona: marynarze nie walczą na pokładzie (dane); załoga pryzowa obsadza statek po zdobyciu" : "obrona: szermierze tną liny (CutProgress), front obrońców (TargetFront) przy relingu, poddanie (ship_surrendered) i zdobycie (ship_captured)");
            list.Add("bez corvusa: tylko haki, liny i przejście przez relingi");
            return list.ToArray();
        }

        static string[] Damage(ShipClass shipClass, ShipDefinition definition) => new[]
        {
            "trafienie (projectile_hit): ludzie blisko punktu kulą się",
            "upadek tylko przy spadku liczebności w ticku trafienia lub rundy abordażu; ranni (Wounded) w miejscu rannych",
            definition.Slots.Length > 0 ? "złamane stanowisko (ModuleHp ≤ 0): obsługa przy naprawie, bo symulacja ją tam trzyma" : "bez stanowisk broni",
            "ciężkie uszkodzenie (kadłub < 35%): wolni ludzie wylewają wodę",
            "zatonięcie (ship_sunk): ludzie skaczą za burtę przy tonącym kadłubie",
            "naprawa w doku: załoga odpoczywa; zmiana właściciela: nowa obsada i barwy bez fałszywych upadków",
        };

        static string[] Lod() => new[]
        {
            "LOD0 (≥ 15 px/m): 17 kości, twarz, fryzura, zarost, broń w detalu, cienie",
            "LOD1 (5–15 px/m, domyślna kamera 6,9 px/m): 11 kości, broń i tarcza, bez cieni",
            "LOD2 (< 5 px/m): jedna wypieczona bryła na postać, sylwetka, barwa strony i rytm wioseł; obsada nie znika",
        };
    }
}
