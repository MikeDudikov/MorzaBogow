# Audyt wyglądu i plan zmian (surowy realizm antycznej Grecji)

Data: 2026-11-10. Podstawa: kod i dokumenty projektu, podglądy batch okrętów (`AIAssetBatch.PreviewShips`). Zrzutów z gry w tym audycie nie robiono: ocena obrazu w 1920×1080 z kamery strategicznej jest krokiem 3 planu, w scenie technicznej.

Kierunek zadany przez użytkownika: dojrzały realizm śródziemnomorski z materialnością i ciężarem punktu odniesienia (Diablo II: Resurrected), ale z naturalną jasnością i barwami Egei. Z gry odniesienia nie bierzemy treści, form ani elementów. Obowiązujące zmiany: [ArtDirectionAegean.md](ArtDirectionAegean.md). Audyt sprawdza oba kierunki błędu:
- **słodycz:** nasycenie, połysk, zabawkowe kształty;
- **ponurość:** czerń bez szczegółów, odbarwienie, ciężka mgła, nieczytelne jednostki.

Dziś zagrożeń z drugiej grupy nie ma: obraz jest jasny, mgła w meczu spokojna, a jednostki czytelne dzięki żaglom.

## Co dziś daje zbyt łagodny wygląd

| Obszar | Stan (źródło) | Skutek | Zmiana |
|---|---|---|---|
| Postprodukcja | nasycenie +35, tonemapping Neutral, stała ekspozycja EV 12,25 (`docs/environment.md`) | barwy podbite, cukierkowe | nasycenie około +10–15 (bez odbarwiania); porównanie ACES z Neutral na tym samym kadrze; ekspozycja z półtonami, bez przepaleń piany |
| Słońce | 100 000 lx, kąt 45–60° (`BattleSceneBuilder`, `environment.md`) | jednakowe południe | warianty: zachmurzenie, zmierzch, sztorm; spójne z `WeatherController` |
| Żagle i tuniki stron | gracz (0.12, 0.22, 0.60), wróg (0.55, 0.08, 0.08) (`BattleSceneBuilder`) | najbardziej „cukierkowe” plamy w kadrze | przygaszony granat lnu i ciemna czerwień (#5E1B1A) o różnej jasności, z testem rozróżnialności |
| Znaczniki | zaznaczenie jaskrawozielone (0.45, 1, 0.5); cel (1, 0.15, 0.1) | neon nad wodą | stonowane kolory sygnałów z zachowanym kontrastem (decyzja w scenie technicznej, bo dotyczy czytelności) |
| Błyski trafień | kula materiału `Flash` (1, 0.6, 0.2) o średnicy 8–22 m przy taranie, zdobyciu i zatopieniu | „kolorowe eksplozje” przy zwykłych zdarzeniach | błysk tylko krótki i mały przy kontakcie; główną robotę robią drzazgi, bryzgi i kurz (już są) |
| Okręty | grecka geometria epoki brązu, kolory płaskie, bez tekstur (`GreekShips`) | nieskazitelne powierzchnie | tekstury PBR: słoje, smoła, zużyte krawędzie, wilgoć przy linii wody, sól na burtach |
| Budowle | prymitywy z oznaczeniem `[PLACEHOLDER]` (`BattleController`) | zabawkowe bryły | modułowe zestawy według `HistoricalConstraints.md` (krok 5) |
| Postaci | kapsuły i sześciany; wojownicy z hełmem, tarczą i włócznią (`CrewVisual`) | zabawkowe proporcje | proporcje dorosłego człowieka, cięższe pozy, wolniejsze przygotowanie ciosu i dłuższy powrót do równowagi |
| Ruda srebra | metallic 1, smoothness 0.85, jasna (0.85, 0.87, 0.92) | plastikowy połysk | ruda w skale: matowa skała z żyłami; połysk tylko świeżych odłupań |
| Woda | woda HDRP z pianą i dekalami; sztorm tylko w pokazach (`WeatherController`) | dobra podstawa | głębsza barwa (odniesienie #0B232B), piana zależna od wiatru, różne akweny |
| Dźwięk | brak warstwy audio (żadnego `AudioSource` ani pliku audio) | brak masy i napięcia | osobne zadanie: wiatr, kadłub, liny, wiosła, pękanie drewna, port |
| Interfejs | złoto (228, 185, 105), kremy, koral (241, 132, 108) na ciemnym morskim tle (`MorzaBogowHud.uss`) | spójny, ciepły; koral zbyt pogodny | koral zamienić na ciemną czerwień; złoto przygasić do brązu |

## Co już działa w tym kierunku

- Okręty mają greckie sylwetki z XII w. p.n.e., smołę, ochrę i drewno zamiast jaskrawych prymitywów.
- Trafienia zostawiają lokalne wyłomy i wbite strzały. Okręt z małą ilością kadłuba ma przechył, niższą burtę i podarty żagiel. Naprawa zdejmuje ślady.
- Tonięcie trwa około 10 s: osiadanie, przechył, upadek masztu, szczątki. Rozpad na trzy części następuje po taranie lub ciężkim pocisku.
- Abordaż: wojownicy w zbrojach z epoki, polegli na pokładzie i w morzu.

Te elementy zostają jako podstawa. Plan zmienia ich kolorystykę i materiały, nie zasady.

## Plan (kolejność z polecenia)

1. **Audyt** (ten dokument).
2. **Wspólny profil:** [ArtDirection.md](ArtDirection.md), paleta odniesień, szablony i wykluczenia dla generatorów w `Tools/asset_pipeline/config/pipeline.json`.
3. **Scena techniczna** z istniejącymi zasobami (tryb `-morzaShowcase`): ten sam kadr przy różnych ustawieniach postprodukcji, zdrowe i uszkodzone okręty, spokój i sztorm, 1920×1080, z pomiarem czasu klatki. Wymaga okna gry: przed uruchomieniem uprzedzę.
4. **Jeden okręt:** triakontoros (klasa Ram) z teksturami PBR i zużyciem.
5. **Jedna budowla:** stocznia z pochylnią.
6. **Materiały, światło i woda** dla całej sceny.
7. **Animacje i efekty:** ciężar ruchu, mniejsze błyski.
8. **Ocena czytelności i wydajności** z pomiarem.
9. **Rozszerzenie** na resztę biblioteki według `AssetManifest.json`.

Mechanika, balans, widoczność i mgła wojny zostają bez zmian. Światło nie zmienia reguł odkrywania mapy.
