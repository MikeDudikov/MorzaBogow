# Dyrekcja artystyczna: realizm egejski (obowiązująca)

Data: 2026-11-10. Zastępuje mroczniejszy wariant z tego samego dnia. Z punktu odniesienia (Diablo II: Resurrected) bierzemy materialność, szczegółowość i ciężar. Treści, form, nazw i elementów tamtej gry nie bierzemy.

> Dojrzały, historycznie osadzony realizm śródziemnomorski: słoneczne wyspy, głębokie morze, surowe materiały, przekonujące postacie i odczuwalne konsekwencje walki.

Gra ma być poważna, piękna i wiarygodna. Naturalna jasność i barwy Egei zostają. Powagę dają proporcje, materiały, ślady użycia, ciężar ruchu i skutki walki, a nie przyciemnienie ani odbarwienie obrazu.

## Konkretne zmiany po audycie ([AUDYT_WYGLADU.md](AUDYT_WYGLADU.md))

| # | Zmiana | Dlaczego | Gdzie | Stan |
|---|---|---|---|---|
| 1 | Nasycenie z +35 do około +10–15, nie do zera | +35 daje cukierkowość; odbarwienie zabiłoby urodę Egei | profil Volume sceny bitwy | do zrobienia (scena techniczna, porównanie kadrów) |
| 2 | Tonemapping: ACES i Neutral porównane na jasnym kamieniu, żaglu i pianie; zostaje ten, który nie przepala bieli i zachowuje półtony | ekspozycja na słońcu 100 000 lx | Volume | do zrobienia |
| 3 | Barwy stron na żaglach: granat lnu gracza i ciemna czerwień wroga, różne jasności (rozróżnialne także w odcieniach szarości) | dzisiejsze (0.12, 0.22, 0.60) i (0.55, 0.08, 0.08) to najjaskrawsze plamy w kadrze | `BattleSceneBuilder` (materiały żagli i tunik) | do zrobienia z testem czytelności |
| 4 | Znaczniki zaznaczenia i celu: kolory z palety świata o wysokim kontraście jasności zamiast neonowej zieleni | czytelność bez neonu | `BattleSceneBuilder`, USS | do zrobienia z testem czytelności |
| 5 | Kule błysku przy taranie, zdobyciu i zatopieniu: mniejsze i krótsze; resztę robią drzazgi, bryzgi i szczątki | „kolorowe eksplozje” przy zwykłych zdarzeniach | `BattleEffects.Flash`, `BattleController` | taran zmniejszony z 22 do 12 m; reszta do zrobienia |
| 6 | Okręty: greckie typy z XII w. p.n.e., smoła, ochra, drewno, brąz | prymitywy wyglądały zabawkowo | `GreekShips` | **zrobione** (7 klas, testy) |
| 7 | Tekstury PBR okrętów: słoje, smoła, zużyte krawędzie i chwyty, wilgoć i sól przy linii wody; nowy, zadbany i zużyty wariant | gładkie, jednakowo czyste powierzchnie | potok (ComfyUI, Meshy po zgodzie na koszt), `AIAssetImporter` | do zrobienia |
| 8 | Postaci w proporcjach dorosłego, cięższe pozy, przygotowanie i powrót po ciosie | kapsuły i szybkie machanie | `CrewVisual` | częściowo: wojownicy z epoki, pchnięcie z zasłoną, upadki; proporcje i ciężar do zrobienia |
| 9 | Budowle osady w zestawach modułowych z kamienia, cegły suszonej, drewna i tynku | prymitywy z oznaczeniem `[PLACEHOLDER]` | nowy generator budowli | do zrobienia (jedna budowla: stocznia) |
| 10 | Woda: barwa z głębokości i dna (turkus tylko nad płyciznami, głęboki błękit dalej), piana od wiatru, bez plam niezależnych od akwenu | uroda Egei i czytelność głębi | woda HDRP, `Seabed`, `WeatherController` | do sprawdzenia w scenie technicznej |
| 11 | Ruda srebra matowa, połysk tylko na świeżych odłupaniach | plastikowy połysk | materiał `SilverOre` | do zrobienia |
| 12 | Interfejs: koral (241, 132, 108) na ciemną czerwień, złoto przygaszone do brązu, kontrast tekstu zachowany | zbyt pogodne akcenty | `MorzaBogowHud.uss` | do zrobienia (po teście 1920×1080) |
| 13 | Dźwięk: wiatr, kadłub, liny, wiosła, pękanie drewna, port | brak warstwy audio | nowy moduł | do zaplanowania (brak plików audio) |

## Czego unikamy w obie strony

**Słodycz:**
- komiczne proporcje;
- zaokrąglenia;
- plastikowy połysk;
- jednakowa czystość;
- przesadne nasycenie;
- lekkość ruchu;
- ozdobne efekty.

**Ponurość:**
- utrata szczegółów w czerni;
- odbarwienie;
- permanentna szarość;
- ciężka mgła;
- noc i burza bez związku z pogodą;
- jednolity filtr niebieski lub brązowy;
- nieczytelne jednostki.

## Kolejność wdrożenia

1. Audyt (zrobiony).
2. Profil artystyczny (ten dokument i [ArtDirection.md](ArtDirection.md)).
3. Jeden statek (triakontoros: geometria gotowa, materiały do zrobienia) i jedna budowla (stocznia).
4. Materiały i oświetlenie.
5. Woda i pogoda.
6. Animacje oraz efekty.
7. Kontrola czytelności.
8. Profilowanie.
9. Rozszerzenie na bibliotekę.

Porównania (dzień słoneczny, zachmurzenie, zmierzch, spokój, sztorm, zdrowe i uszkodzone jednostki, pojedynczy obiekt, gęsta scena) wymagają okna gry 1920×1080 w trybie zrzutów. Zrobię je w osobnym kroku, z uprzedzeniem o oknie gry. Testów wizualnych i sprzętowych, których nie wykonano, nie deklaruję.
