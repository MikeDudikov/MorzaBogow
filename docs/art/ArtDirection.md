# Dyrekcja artystyczna: Morza Bogów (profil wspólny)

Data: 2026-11-10. Obowiązuje dla gry i dla potoku assetów (`Tools/asset_pipeline`). Lista zmian po audycie: [ArtDirectionAegean.md](ArtDirectionAegean.md). Ramy epoki: [HistoricalConstraints.md](HistoricalConstraints.md).

## Wizja

> Dojrzały, historycznie osadzony realizm śródziemnomorski: słoneczne wyspy, głębokie morze, surowe materiały, przekonujące postacie i odczuwalne konsekwencje walki.

- Z punktu odniesienia (Diablo II: Resurrected) bierzemy materialność, szczegółowość i ciężar, bez jego treści i form.
- **Powaga** pochodzi z proporcji, konstrukcji, materiałów, śladów użycia, ciężaru ruchu i skutków walki. Nie z ciemności ani szarości.
- **Czytelność** z kamery strategicznej ma pierwszeństwo: strony, kierunek kadłuba, klasa, stan uszkodzeń i zagrożenie są rozpoznawalne w 1920×1080.

## Epoka

- **Kotwica:** Egeja XII w. p.n.e., z ramą XII–V w. p.n.e. dla elementów, których XII w. nie wyznacza.
- **Anachronizmy wymagane przez gre** (machiny miotające) są jawnie oznaczone i narysowane w materiałach epoki.
- **Wykluczone:** gotyk, średniowiecze, proch, corvus.
- **Mitologia** to osobna kategoria z własnym charakterem i kontrolowaną paletą.

## Barwy (naturalne, egejskie)

- **Morze:** głęboki błękit otwartej wody; turkus i zieleń tylko nad płyciznami jasnego dna. Barwa wynika z głębokości i dna, nie z losowych plam.
- **Ląd:** ciepły jasny wapień, ochry i brązy suchej ziemi, oliwkowe i przygaszone zielenie zarośli i drzew.
- **Wytwory człowieka:** płótno, drewno suche i mokre, smoła, ochra na okrętach, terakota, brąz.
- **Akcenty nasycone** tam, gdzie mają uzasadnienie: barwy stron na żaglach i tarczach, insygnia, polichromia budowli sakralnych, ogień, sygnały rozgrywki.
- **Bez** odbarwiania całego świata.

## Materiały PBR

Rozróżniamy:
- drewno suche, mokre, obrobione i zużyte;
- tkaniny i liny;
- kamień, tynk, ceramikę;
- brąz i inne metale właściwe obiektowi;
- powierzchnie nadpalone (tylko przy zdarzeniach ognia).

Ślady użycia rozmieszczamy według funkcji: chwyty, połączenia, krawędzie, linia wody, miejsca pracy i uderzeń. Stany obiektu to: nowy, zadbany w użyciu, zużyty, uszkodzony, ruina lub wrak. Nie postarzamy wszystkiego do rozpadu.

Tekstury powstają w neutralnym świetle, bez wypalonych cieni i refleksów. Mask map HDRP: R = Metallic, G = AO, B = Detail, A = Smoothness. Kolor jest w sRGB, dane w linear.

## Światło, woda, pogoda

- **Światło:** wyraźny kierunek, przekonujące cienie, światło odbite, szczegóły w jasnym kamieniu, żaglach i pianie, czytelne półtony.
- **Tonemapping** wybieramy porównaniem kadrów.
- **Ograniczamy:** bloom, flary, winietę, sztuczne świecenie, jednolite filtry barwne.
- **Woda:** głębia, ruch, naturalne odbicia, fale w różnych skalach, piana od warunków, przejścia przy brzegu.
- **Groźne morze** wynika z fal, wiatru, światła i dźwięku. Zależności wiatru, wody i okrętów zostają.
- **Mgła** odpowiada pogodzie i odległości, nie zasłania jednostek. Reguły widoczności i mgła wojny się nie zmieniają.

## Ruch, walka, uszkodzenia

- **Animacje:** bezwładność, przygotowanie, przeniesienie ciężaru, kontakt, reakcja, powrót do równowagi.
- **Wiosłowanie:** wspólny rytm wszystkich wioseł.
- **Budowa:** wysiłek i właściwe narzędzia.
- **Walka:** zdecydowana.
- **Czas:** trafień, wyników, postępu budowy, tempa produkcji, ruchu i właścicieli stanu nie zmieniamy.
- **Uszkodzenia:** lokalne i materialne: wyłomy, odłamki, wbite strzały, podarte żagle, złamane stanowiska. Ogień tylko przy zdarzeniach ognia. Efekty dobrane do materiału i kontaktu, raz na zdarzenie, bez kolorowych eksplozji i nadmiaru iskier.

## Interfejs

Kamień, tkanina, drewno, przygaszony metal. Muszą zostać:
- czytelny tekst;
- kontrast ikon;
- rozróżnienie stron;
- widoczne zaznaczenie;
- jasne ostrzeżenia.

## Generatory

Profil jest w `Tools/asset_pipeline/config/pipeline.json`: klucze `templates` (concept, model, texture, animation, effect) i `negativeTerms`.

**Wykluczenia** idą tylko tam, gdzie narzędzie je przyjmuje: w ComfyUI przez pole `{{negative}}`. W Meshy żaden zweryfikowany parametr ich nie przyjmuje, więc potok ich nie wysyła.

**Koncept:**
> Grounded Ancient Greek maritime realism, mature and serious art direction, authentic Aegean landscape, clear Mediterranean sunlight, deep blue sea, natural coastal turquoise, warm stone, olive vegetation, tactile materials, believable construction, realistic proportions, readable strategic-game silhouette.

**Model:** konstrukcja, proporcje, części ruchome, skala w metrach, wymagania techniczne.

**Tekstura:** fizyczne właściwości, neutralne oświetlenie, bez wypalonych cieni, mapy PBR.

## Wydajność

Cel: 1920×1080, 60 FPS na i5 z 16 GB RAM i RTX 3050 Ti.

Pod kontrolą trzymamy:
- cienie;
- mgłę wolumetryczną;
- odbicia;
- przezroczystość;
- światła efektów;
- liczbę materiałów;
- pamięć tekstur.

Każda zmiana ma pomiar w trybie dymnym (`-morzaSmokeReport`). Czas symulacji i reguły świata się nie zmieniają.
