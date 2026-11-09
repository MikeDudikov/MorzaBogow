# Prompt wdrożeniowy dla Claude Code

Pracuj nad grą „Morza Bogów” jako koordynator implementacji Unity. Rozpocznij od sprawdzenia tego katalogu i wykonuj kolejne grywalne etapy. W istniejącym projekcie scalaj ustawienia oraz pliki; zachowuj jego działającą strukturę.

## Dokumenty i dostępne pliki

Przeczytaj GAME_VISION.md, GAME_DESIGN.md, ECONOMY_DESIGN.md, ECONOMY_BALANCE.md, VISUAL_TECH_SPEC.md, MYTHIC_SCENARIOS.md, CLAUDE.md i README_PL.md. Wczytaj ASSET_STATUS.json oraz dane i macierze qa/. Stosuj priorytety rozgrywki morskiej oraz wymagania bramki testów.

ASSET_STATUS.json określa faktyczną dostępność modeli. Coast Rocks 02 jest pobrany: FBX oraz cztery tekstury EXR 2K znajdują się w Assets/Art/External/Coast_Rocks_02. Cztery paczki mają status NOT_PURCHASED; foldery AssetPackages zawierają wyłącznie oznaczenie braku plików. Nie traktuj ich jako zaimportowanych modeli.

## Przygotowanie projektu

1. Wykryj katalog projektu Unity, edytor, HDRP i dostępne narzędzia. Gdy projekt nie istnieje, przygotuj Unity 6.3 LTS z kompatybilnym HDRP. Zapisz dokładne ustawienia środowiska i sprawdź kompilację. Korzystaj z dostępnych uprawnień do rutynowych operacji, zależności i uruchomień.
2. Scal .claude/settings.json, skonfiguruj ścieżkę Unity.exe w .claude/qa/policy.json i sprawdź hooki zgodnie z CLAUDE.md. Ustaw rzeczywiste testy EditMode oraz PlayMode. Wyniku testów bramki nie przedstawiaj jako odbioru gry.
3. Zasoby, materiały i prefaby przygotowane dla gry umieszczaj poza External. Geometrię i tekstury pobrane do External zachowaj jako wejście do importu. Jednostka świata to metr; potwierdź skalę, orientację i położenie modelu.

## Wdrożenie wybrzeża

4. Zaimportuj FBX i jawnie przypisz mapy z ASSET_STATUS.json. Napraw odwołania do tekstur, jeśli FBX wskazuje JPG lub inne lokalizacje. Sprawdź kodowanie koloru EXR; nie włączaj sRGB automatycznie dla danych liniowych. Mapę normalną zaimportuj zgodnie z konwencją używanego shadera. Roughness przelicz na smoothness dokładnie raz. AO przypisz do odpowiedniego kanału materiału HDRP.
5. Sprawdź rzeczywistą liczbę trójkątów, submeshy i materiałów. Model wybrzeża ma bardzo szczegółową geometrię. Przygotuj uproszczone warianty, LODGroup i proste collidery. Nie stosuj pełnej geometrii jako dynamicznego MeshCollider. Obrys przeszkód nawigacyjnych ma pozostawać niezależny od wizualnego LOD.
6. Utwórz prefab wybrzeża i scenę Art_Integration_Coast. Sprawdź widoczność skał i płycizn z kamery strategicznej, odbicia, cienie, styk z wodą oraz zachowanie po zmianie jakości. Potwierdź brak różnicy w zasadach nawigacji między profilami.

## Flota i brakujące paczki

7. Dopóki plików paczek nie ma, buduj czytelne modele zastępcze okrętów z oddzielnym kadłubem, żaglem, wiosłami, taranem i stanowiskami uzbrojenia. Zachowaj odrębne root obiektu symulacji i model wizualny. Przygotuj stałe punkty mocowania, aby wymiana grafiki nie zmieniała kosztów, obrażeń ani sterowania. Dla budynków i stworzenia przygotuj oznaczone modele zastępcze i kontynuuj mechaniki.
8. Gdy właściwe pliki pojawią się w AssetPackages, sprawdź ich format, komplet tekstur, szkielet, animacje oraz zgodność z HDRP. Paczki .unitypackage importuj przez Unity. Dla zbiorczych paczek budynków zweryfikuj dostępność właściwych modeli, nie uznawaj samego małego pliku pakietu za kompletną bibliotekę geometrii. Triera z Fab jest alternatywą wizualną do podstawowej floty; jej dodanie nie tworzy automatycznie nowej klasy jednostki.
9. Potwora morskiego dopasuj do konkretnego scenariusza dopiero po udostępnieniu plików. Sterowanie, punkty trafień, telegraph i moment naliczania obrażeń mają wynikać z symulacji, a animacja ma je prezentować.

## Grywalny etap i odbiór

10. Wykonaj małą mapę z wybrzeżem, portem, kutrem i okrętami bojowymi. Zrealizuj zbiór B/R, lokalny rozładunek, ruch, ostrzał, taran, abordaż, przeciwnika, zwycięstwo i porażkę oraz zapis/wczytanie. Gospodarka pracuje równolegle do działań floty. Nie uzależniaj grywalności od zakupienia całej oprawy.
11. Sprawdź prefaby w buildzie i wykonaj odpowiednie testy zachowania gry. Profiluj sceny ze 100, 200 i 400 statkami. Podawaj realny sprzęt, ustawienia, liczby obiektów i p95 czasu CPU/GPU. Modele zastępcze nie potwierdzają wydajności docelowych modeli.
12. Raportuj po polsku: co jest rzeczywiście zaimportowane, co działa jako model zastępczy, jakie testy wykonano i które pliki nadal są potrzebne. NOT_RUN oraz BLOCKED nie oznaczają PASS. Opisy mechanik i wizji zachowaj w formie wymagań; dostępność paczek prowadź w ASSET_STATUS.json.

Rozpocznij od odczytania plików i sprawdzenia środowiska. Następnie zaimportuj pobrany model wybrzeża, przygotuj scenę próbną i rozwijaj pierwszy grywalny etap.
