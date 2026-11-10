# Morza Bogów — uzgodnienie paczki specyfikacji

Data: 2026-11-10. Gałąź: `balans-skala`.

Źródło: paczka użytkownika `Naval_Strategy_Game_Design.zip` z `C:\Users\noirp\Documents\Codex\2026-11-09\…\outputs\`. Zawiera:
- `GAME_SPECIFICATION.md`, 30 rozdziałów;
- `GAME_SPECIFICATION.pdf` (ten sam tekst);
- `CLAUDE_CODE_PROMPT.md`.

Paczkę przeczytano bez rozpakowywania; jej treści nie kopiowano do repozytorium.

## Zasady uzgodnienia

- **Pierwszeństwo.** Obowiązują polecenia użytkownika i przyjęte decyzje projektu. O stanie wdrożenia decyduje kod i wyniki testów. Paczka jest propozycją: jej liczby (ceny, nagrody, czasy, limity) są danymi wejściowymi do dopasowania, nie stałymi do przepisania.
- **Granica treści.** Specyfikacja w dużej części odtwarza projekt istniejącej gry z 1998 r. (fabułę, frakcje, tabele cen, listy bohaterów i skarbów, interfejs, skróty, sieć i sprzęt tamtych lat). Reguła użytkownika dla Mórz Bogów: własne nazwy, liczby i grafika; mechaniki gatunku tak, kopia istniejącej gry nie. Z paczki bierzemy więc mechaniki. Nazwy, liczby, fabułę, misje, teksty, wygląd i interfejs tworzymy własne. Mitologia grecka jest wspólna, ale kampania i bohaterowie dostaną własne ujęcie.
- **Jedna aktualna wersja.** Każda przyjęta reguła trafia do jednego dokumentu projektu (ten plik, [BALANS_I_SKALA.md](BALANS_I_SKALA.md), [ZALOGA_I_ABORDAZ.md](ZALOGA_I_ABORDAZ.md)) i do kodu z testem. Bez równoległych tabel balansu.

## Decyzje użytkownika z uzgodnienia (2026-11-10)

| Pytanie | Decyzja |
|---|---|
| Nowe obszary do mapy drogowej | Magia i świątynie, skarby i artefakty, druga cywilizacja i barbarzyńcy, potwory i czarownicy. Wszystko z własnymi nazwami i liczbami. |
| Naprawa kadłuba | Jak w paczce: tylko w doku (ulepszenie stoczni po Mieście) i tylko za srebro. |
| Nieudany abordaż | Jak w paczce: atakujący traci całą załogę bojową; okręt bez wojowników jest bezbronny do uzupełnienia załogi. |

## Rozdziały paczki a stan gry

Statusy:
- **zrobione:** działa i ma test;
- **etap N:** z [mapy drogowej](BALANS_I_SKALA.md#mapa-drogowa-po-specyfikacji-użytkownika-z-2026-11-10);
- **konflikt:** z przyjętą regułą i jej uzasadnieniem;
- **nie dotyczy:** poza projektem.

| Rozdz. | Temat | Status w Morzach Bogów |
|---|---|---|
| 1 | Koncepcja: eksploracja, surowce, osady, uzbrajanie okrętów, ostrzał/taran/abordaż | Zrobione w zakresie etapów 1–3b (ostrzał, taran z zapowiedzią, abordaż na burtach, wymiana broni w porcie). Magia i kampania: etapy 8–9. |
| 2 | Świat i fabuła | Nie przenosimy fabuły z paczki. Fabuła i misje własne w etapie 8. |
| 3.1–3.2 | Kampania, misje własne | Etap 8. Gra ma dziś potyczkę 1v1 w trzech profilach długości. |
| 3.3 | Gra wieloosobowa (do 8 graczy, modem, IPX, Wireplay) | Nie dotyczy: transport z lat 90. Gra sieciowa nie była wybrana do mapy drogowej. |
| 3.4 | Poziomy trudności AI | Etap 7 (AI z falami i odbudową). |
| 3.5 | Pokaz automatyczny | Nie dotyczy. Showcase `-morzaShowcase` służy zrzutom. |
| 4.1 | Odnawianie złóż „kilka razy, potem koniec” | **Konflikt.** Obowiązuje decyzja użytkownika: złoża w pełni odnawialne i nie decydują o końcu gry (`World.StepRegrowth`, test `Deposits_Renew…`). |
| 4.2–4.3 | Srebro i drewno; naprawa bez drewna | Zrobione: D/S, lokalne magazyny, ładunek. Naprawa: decyzja użytkownika jak w paczce (tylko srebro, tylko dok). |
| 4.4 | Lewiatany: polowanie kutrem lub handlowcem, nagroda | Etap 5, z nagrodą w D/S i własnymi liczbami. Kwota z paczki przekracza zasoby całej mapy. |
| 4.5 | Wraki: 5% ceny okrętu w srebrze | **Konflikt.** Zostaje obecna reguła: wrak zachowuje 50% przewożonego ładunku przez 300 s, odzysk z 20 m (`Rules.WreckCargoFraction`). Wraki po sztormach w etapie 5. |
| 4.6 | Kuter i handlowiec, obszar pracy zaznaczany ramką | Zrobione: kuter i handlowiec, zbiór, transport, przyczółki. Obszar ramką: etap 7 (automatyzacja). |
| 5.1 | Budowle i ceny | Zrobione z własnymi kosztami D/S: stocznia, Warsztat, Miasto, Szkoła Mędrców, badanie (3b), dok (uzgodnienie paczki). Kuźnia: etap 6. Świątynie: etap 9. |
| 5.2 | Miasto | Zrobione: port macierzysty ze stocznią i Warsztatem (3b). Kuźnia jako warunek dojdzie w etapie 6. |
| 5.2 | Handlowiec wymaga Miasta | Do decyzji w etapie 6. Dziś handlowiec jest potrzebny do przyczółków od początku gry. |
| 5.3 | Naprawa tylko w doku; wolny plac przed stocznią | Dok: decyzja użytkownika. Wolne miejsce wodowania: zrobione (stanowiska i pochylnie). |
| 5.4 | Ulepszenia broni (szybkostrzelność, zasięg, siła); pancerze i tarany w trzech stopniach | Badanie broni ciężkiej zrobione (3b). Ulepszenia i Kuźnia: etap 6. |
| 6 | Forty małe/średnie/duże, uzbrajanie, limit siły osady | Etap 6, z własnymi cenami i limitem. Martwa strefa machin zrobiona (3a) i obejmie forty. |
| 7.1 | Dwie cywilizacje, wiatr, zanurzenie | Druga cywilizacja: etapy 4 i 7. Wiatr: etap 8. Zanurzenie i płycizny zrobione (`Shelf`, `Draft`). |
| 7.2 | Okręty gospodarcze, nie atakują abordażem; ich wojownicy bronią | Zrobione. Bezbronność bez wojowników: uzgodnienie paczki. |
| 7.3–7.4 | Klasy okrętów i ich role | Taranowiec, łowca załóg i abordażowiec zrobione. Niszczyciel okrętów i bombowiec: 3c. Okręt królewski, podpalacz i okręt z wapnem: etap 4. Ceny własne (szablony z uzbrojeniem). |
| 7.5–7.6 | Okręty unikalne cywilizacji | Etap 4, z własnymi nazwami. Okręt odporny na abordaż i wrażliwy na taran jako pomysł na rolę. |
| 8 | Tarany i pancerze w trzech stopniach | Etap 6 (Kuźnia). Pancerz przeciwpociskowy i mnożniki taranu klas zrobione (3a). |
| 9 | Łucznicy, oszczepnicy, szermierze; doświadczenie; nieudany abordaż | Trzy rodzaje wojowników i walka na burtach zrobione (etap 2, 3a). Nieudany abordaż: decyzja użytkownika. Doświadczenie (weterani): etapy 7–8. Abordaż okrętu barbarzyńców go topi: etap 7. |
| 10 | Bronie średnie, ciężkie, specjalne; martwa strefa; domyślne uzbrojenie | Bronie średnie i ciężkie, martwa strefa i plan uzbrojenia zrobione (3a, 3b). Ogień grecki i wapno: etap 4. Budowa bez domyślnego uzbrojenia: 3c. |
| 11–13 | Świątynie, zaklęcia morza i wojny | Etap 9, z własnym doborem zaklęć, kosztami w D/S i zasięgiem od świątyni. |
| 14, 19 | Bohaterowie i skarby, które ich przyciągają | Etap 7. Bohaterowie z mitologii, zdolności i liczby własne; tabela z paczki jako materiał, nie lista. |
| 15–16 | Potwory, stwory morskie, czarownicy | Etap 5 (zagrożenia, siedziby, chaty czarowników). |
| 17 | Barbarzyńcy | Etapy 4 (okręty) i 7 (strona AI, forty jako stocznie). |
| 18, 20 | Skarby i artefakty, ich zachowanie między misjami | Etap 7 (skarby), etap 8 (zachowanie w kampanii). |
| 21 | Zachowania okrętów i automatyzacja | Eskorta (konwój) zrobiona. Straż, taran, abordaż, ogień na napastnika, zwiad, patrol, strefy: etap 7. |
| 22 | Pasek sterowania, przeładunek, sprzedaż, mapa, prędkość, klawisze | Własny interfejs zrobiony ([INTERFEJS.md](INTERFEJS.md)): minimapa, prędkość gry, przeładunek surowców, złomowanie okrętu. Przeładunek broni i sprzedaż obiektów: etap 6. |
| 23 | Skróty klawiszowe | Własne przypisania w `KeyBindings` (grupy Ctrl+1–9, alarm, pauza, zapis). Nie przenosimy skrótów z paczki. |
| 24–27 | Porady taktyczne | Materiał do testów ról w pojedynkach (simprof `diag`) i AI w etapie 7. |
| 28 | Sprzęt i system z 1998 r. | Nie dotyczy. Cel projektu: Windows 11, 1920×1080, RTX 3050 Ti. |
| 29 | Pola nieokreślone | Wartości ustalane w etapach na podstawie obecnej gry, z pomiarem, nie zgadywane. |
| 30 | Ciągłe uzgadnianie | Stosowane: ten dokument i odnośniki z mapy drogowej. |

## Wdrożone przy uzgodnieniu

- **Nieudany abordaż** ([ZALOGA_I_ABORDAZ.md](ZALOGA_I_ABORDAZ.md)):
  - atakujący, któremu na wrogim pokładzie skończyli się wojownicy, traci także rannych z tego abordażu; żeglarze zostają;
  - okręt bez wojowników nie broni się na pokładzie i zostaje przejęty po utrzymaniu pokładu, bez strat atakującego;
  - przecięcie lin przed walką nie jest porażką na pokładzie: nikt nie przeszedł, więc nikt nie przepada.
- **Dok i naprawa za srebro** ([BALANS_I_SKALA.md](BALANS_I_SKALA.md)): kadłub i moduły naprawia tylko port z dokiem, za srebro. Przegrupowanie załogi i wymiana broni działają w każdym porcie.

## Druga paczka: system scenariuszy (`MorzaBogow.zip`)

Decyzja użytkownika: tylko wskazówki. Mapy, obrazy, paleta, profil skali kafelkowej i kod z paczki nie trafiają do gry. Przyjęte zasady i ich etapy: [WSKAZOWKI_SCENARIUSZY.md](WSKAZOWKI_SCENARIUSZY.md).
