# Morza Bogów — interfejs rozgrywki (stan wdrożony)

Interfejs potyczki wdrożony według pakietu „Morza_Bogow_Interfejs” (INTERFEJS.md, UNITY_WDROZENIE.md, design-tokens.json, ui-contract.json). Raport odbioru: [QA_INTERFEJS.md](QA_INTERFEJS.md).

## Technika

- UI Toolkit (UIDocument, PanelSettings tworzone w czasie gry, USS, TextCore). Projekt nie używał uGUI ani TextMeshPro; jest jeden HUD i jeden odbiornik rozkazów.
- Kod: `Runtime/Presentation/Hud/BattleHud.cs` (widok), `HudMinimap.cs` (mapa), `HudControls.cs` (paski), `HudPalette.cs` (kolory z tokenów), `KeyBindings.cs` (skróty), `HudTypes.cs` (rozkazy celowane, herold).
- Styl: `UI/Resources/MorzaBogowHud.uss`. Zasoby pakietu: `UI/Resources/Hud/` (Fonts z licencjami OFL, Icons 64/128, Frames, Ornaments, Cursors 32 + hotspots.json, Art: portrety triery i handlowca ograniczone do 512 px). Import: `Editor/HudAssetImport.cs` (bez mipmap, sRGB, alfa, Clamp, Bilinear, bez kompresji; kursory jako Cursor).
- Siatka 1920×1080, `ScaleWithScreenSize`, dopasowanie do wysokości; skala gracza 90/100/110/125% (`PanelSettings.scale`). Powyżej 105% etykiety zasobów i rozkazów ustępują ikonom (klasa `compact`).
- Interfejs tylko czyta stan (`World`, `BattleController`) i wysyła komendy; nie zmienia salda, życia, cargo, kolejek ani widoczności. Teksty odświeża co 0,125 s i zmienia tylko przy zmianie wartości; minimapę co 0,25 s; paski kadłuba co klatkę. Medaliony, karty, pozycje kolejek i wiadomości pochodzą z pul o stałym limicie.
- Tick symulacji: 30 Hz (`Rules.TickRate`), odczytany z kodu.
- Wejście (od 2026-11-10): pakiet Input System. Klawiatura i mysz są czytane w jednym miejscu (`Hud/GameInput.cs`); przypisania zostają zapisane jako KeyCode po nazwie, więc dawne ustawienia gracza działają dalej. Kliknięcia w panelach UI Toolkit obsługuje dostawca Input System z wbudowanymi akcjami UI.

## Układ

- **Górny pasek 44 px:** znak „Morza Bogów”, drewno i srebro wybranego portu (dostępne / pojemność; klik rozwija magazyn), ładunek w drodze, flota / limit 100, liczba portów, czas, tempo (−, ×N, +), pauza, menu. Meander pod paskiem.
- **Nad mapą:** wybór portu z nazwą (Port macierzysty, Przyczółek N); grupy floty (numer, ikona klasy, licznik); podpowiedź trybu wskazania celu; herold po prawej (najwyżej 4 wiadomości).
- **Dolny pas 194 px:** mapa morska (222 px), zaznaczenie, budowa i wodowanie (dwie linie), rozkazy 4×3. Tab zwija pas; zostają pasek, herold i przycisk przywrócenia.

## Magazyn i porty

Rozwinięcie magazynu pokazuje osobno: dostępne do wydania, zarezerwowane na zamówienia, zapas fizyczny, w tym zapas chroniony (60 D / 20 S z `Rules.ReserveWood/Silver`), pojemność i ładunek w drodze. Zapas chroniony jest częścią dostępnych środków, nie drugą rezerwacją; ładunek w drodze nie liczy się do magazynu. Zmiana portu zmienia odczyt i adres zamówień, nie przenosi towaru. Zamówienie, po którym w porcie zostałoby mniej niż zapas chroniony, wymaga decyzji w oknie z rzeczywistą pozostałością; brak środków podaje brakującą kwotę.

## Zaznaczenie

- Brak: krótka instrukcja.
- Jeden statek: portret (ilustracja triery lub handlowca, ikona kutra), nazwa, kadłub z liczbą, marynarze, żołnierze, morale (ikona trzech stanów + %), ładownia, pełna faza rozkazu (np. „drewno, załoga schodzi na ląd”).
- Grupa: do 4 kart z kadłubem i kartą „+N” z pełną listą; liczniki klas, łączny kadłub i załoga. Klik: tylko ten; Shift+klik: usuń z zaznaczenia.
- Port własny: mury, oba magazyny, stanowiska (x/3) i czekający, stan stoczni. Port wroga: tylko mury; magazyny i kolejki nieznane.
- Wrak w zasięgu wzroku: ładunek i czas do zatonięcia z `Wreck.ExpiresTick`.
- Okręt wroga: tylko to, co widać teraz; bez ładunku i rozkazów. Znika z karty, mapy i podpowiedzi, gdy zniknie z pola widzenia.

## Produkcja

Linia wojenna i portowa są niezależne. Pozycja: ikona klasy, etap (ikona: stępka, wręgi, poszycie, pokład, maszt, wiosła i żagiel – te same progi co model na pochylni), pasek postępu i pozostały czas z zegara symulacji. PPM lub Delete na pozycji anuluje; powyżej 50% okno pokazuje zwolnioną rezerwację, zwrot i stratę wyliczone przez `World.QuoteCancel` (ta sama funkcja wykonuje anulowanie). Przycisk „+” otwiera szablony z ceną kompletną i czasem. Przyczółek bez stoczni nie ma linii wojennej; zamiast niej pokazuje budowę stoczni.

Menu linii wojennej ma przełącznik „Domyślne uzbrojenie: tak/nie”, zapamiętany w ustawieniach. Przy „nie” ceny klas spadają o wartość ich broni, a okręt schodzi na wodę z pustymi gniazdami ([UZBROJENIE.md](UZBROJENIE.md#budowa-bez-domyślnego-uzbrojenia-3c)). Bombowiec bez Warsztatu w porcie albo bez badania broni ciężkiej jest niedostępny, a podpowiedź podaje powód.

## Rozkazy i skróty

Stała kolejność: Ruch M, Stop S, Atak A, Taran R, Abordaż B, Ostrzał V, Zbiór H, Dostawa D, Buduj (przyczółek) F, Eskorta E, Remont P, Złomuj X; obok nich szyk grupy G. Niedostępne dla zaznaczenia znikają; podpowiedź mieszanej grupy mówi, ilu statków dotyczy. Rozkaz celowany włącza tryb wskazania (podpis nad mapą, kursor); LPM wskazuje cel, Shift zostawia tryb, PPM/Esc anuluje bez utraty zaznaczenia. Złomowanie wymaga potwierdzenia. PPM na mapie wybiera czynność według klasy, celu, własności i widoczności; kursor kontekstowy to tylko podgląd, a symulacja ponownie sprawdza cel.

Szyk grupy, G. Rozkaz ruchu dla co najmniej dwóch statków (PPM na morzu albo Ruch) daje każdemu własne miejsce w wybranym szyku:
- **linia:** statki stają burta przy burcie w poprzek kursu, w odstępie najszerszego kadłuba z wiosłami po obu stronach i 2 m wolnej wody; najwyżej 8 w szeregu, kolejne szeregi o długość kadłuba za nim;
- **klin:** prowadzący płynie w sam punkt, reszta w dwóch skrzydłach za nim.

Pozostałe zasady:
- **Kurs** biegnie od środka grupy do punktu. Przy kliknięciu tuż obok grupy szyk ustawia się według kursu prowadzącego.
- **Ląd:** punkt przy lądzie odsuwa się na wodę z zapasem, jakiego potrzebuje najgłębszy kadłub grupy. Miejsce na lądzie, na płyciźnie albo za blisko innego przechodzi na najbliższe wolne.
- **Kursy:** statki dostają miejsca tak, żeby łączna droga była możliwie krótka, więc ich kursy się nie krzyżują.
- **Pojedynczy statek** płynie w sam punkt.

Przełącznik „Szyk: linia/klin” w nagłówku rozkazów widać tylko przy grupie; wybór jest zapamiętany. Szyk to zwykłe rozkazy ruchu dla każdego statku, więc zapis i powtórka go odtwarzają (`Simulation/Formation.cs`). Gdy ten sam klawisz ma kilka czynności, herold mówi o tym na początku bitwy.

Pozostałe: Pause – pauza; Spacja – do ostatniego niepotwierdzonego alarmu; Tab – zwiń panele; Esc – anuluj / menu; F5/F8 – zapis/wczytanie; +/− – tempo; Delete – anuluj zamówienie z fokusem. Kamera: strzałki, kółko, przecinek/kropka (obrót), PgUp/PgDn (pochylenie), Home, środkowy przycisk. Wszystkie przypisania można zmienić w Ustawieniach → Sterowanie; konflikty są pokazywane, a podczas przechwytywania klawisza gra nie reaguje na klawisze.

## Znaczniki, mgła, minimapa, herold

- Pasek kadłuba nad statkiem tylko po zaznaczeniu, przy oglądanym wrogu i nad wrogiem atakowanym rozkazem gracza; pierścień zaznaczenia cienki i półprzezroczysty; znacznik celu ataku po rozkazie; potwierdzenie rozkazu jednym pierścieniem 0,6 s; brak stałych linii rozkazów i kilwateru.
- Minimapa: granatowa woda, ochrowy ląd z rzeczywistych linii brzegu, mgła w trzech stanach, porty, znane złoża, wolne brzegi, własne statki jako koła i wrogie jako romby (tylko widoczne), trapez widoku. Klik przesuwa kamerę, PPM wydaje ruch.
- Herold: najwyżej 4 wiadomości; krytyczne zostają do potwierdzenia (kliknięcia lub Spacji), zwykłe znikają po 8 s; zdarzenia tego samego rodzaju o tym samym porcie lub konwoju łączą się w jedną wiadomość z licznikiem w oknie 6 s; alarm o tym samym celu najwyżej raz na 15 s.

## Menu i ustawienia

Menu (Esc bez zaznaczenia lub przycisk): Wznów, Zapisz, Wczytaj, Ustawienia, Nowa bitwa; otwarte okno wstrzymuje grę i przejmuje wejście, po zamknięciu przywraca poprzedni stan pauzy. Ustawienia: Obraz (profil, rozdzielczość z 15 s na potwierdzenie), Interfejs (skala, wyższy kontrast, wzory oprócz kolorów – kreskowanie wroga i stanu krytycznego, ograniczenie animacji), Sterowanie (zmiana przypisań, konflikty, przywrócenie domyślnych), Dźwięk (gra nie ma jeszcze kanałów miksera). Podpowiedzi po 400 ms przy najechaniu i przy fokusie klawiatury.
