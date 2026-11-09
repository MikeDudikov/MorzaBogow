# Środowisko projektu

Stan z 2026-11-09. Dokument zawiera wyłącznie wartości odczytane z maszyny.

## Sprzęt deweloperski

| Składnik | Wartość |
|---|---|
| System | Windows 11 Home, build 26200 |
| CPU | Intel Core i7-11390H, 3,40 GHz |
| RAM | 15,8 GB |
| GPU | NVIDIA GeForce RTX 3050 Ti Laptop, 4096 MiB VRAM, sterownik 621.01 |
| Drugi GPU | Intel Iris Xe Graphics |
| Bieżący tryb obrazu | 1920×1080 |

To sprzęt deweloperski, nie wymaganie gry. Gra nie ma wymagań sprzętowych (V10). Sprawdzenie fizycznego wyświetlania 2560×1440 wymaga zewnętrznego monitora (V13).

## Narzędzia

| Narzędzie | Wersja | Stan |
|---|---|---|
| PowerShell | 7.6.9 | zainstalowany |
| Node.js | 26.13.0 | zainstalowany |
| Git | 2.56.0.windows.4 | zainstalowany |
| Unity Hub | 3.22.5, instalacja ze Sklepu Microsoft | uruchomiony |
| Unity Editor projektu | 6000.3.28f1 (8f082d2fe476) | zainstalowany; Windows Build Support tylko Mono, bez IL2CPP |
| Unity 6000.6.7f1 | 6000.6.7f1 | zainstalowany, nieużywany w tym projekcie |
| HDRP, Shader Graph, VFX Graph | 17.3.0 | zablokowane w `Packages/packages-lock.json` |
| Unity Test Framework | 1.6.0 | zablokowany |
| Input System | 1.20.1 | zablokowany |

Grafika: Direct3D 12, feature level 12.2, Linear. Backend: Mono.

## Kontrole wykonane

| Polecenie | Wynik | Zakres |
|---|---|---|
| `node qa/scenario-design-check.cjs` | STRUCTURE_VALID, 14 misji, 613 kontroli | tylko struktura danych |
| `pwsh -NoProfile -File qa/tests/test-gate.ps1` | 27/27 PASS | tylko mechanizm bramki, syntetyczny runner |

| Unity EditMode, batch | 21/21 PASS, 0 skipped | logika rozdzielczości i profili |
| Unity PlayMode, batch | 8/8 PASS, 0 skipped | konfiguracja profili i przełączanie w Editorze |
| Build Windows64 Mono, batch | Success, 144985365 B, 0 błędów, 0 ostrzeżeń, 12,6 min | scena M0_Probe |

## Sonda w buildzie (informacyjna)

Scena M0_Probe: woda, niebo, kula testowa, bez statków. Okno 1920×1080, 180 klatek rozgrzewki, 600 klatek pomiaru. Czas klatki jest ograniczony synchronizacją do około 16,7 ms, więc P50 nie porównuje kosztu profili. Spadki P95 i P99 mogą pochodzić z rozgrzewania shaderów; ich przyczyny nie ustalono. Są to czasy całej klatki, nie GPU.

| Profil | RT aktywny | SSR | SSGI | Wolumetria | SSS | P50 ms | P95 ms | P99 ms |
|---|---|---|---|---|---|---:|---:|---:|
| Low | nie | nie | nie | nie | nie | 11,815 | 24,720 | 202,896 |
| Medium | nie | nie | nie | tak | nie | 15,177 | 87,369 | 182,785 |
| High | nie | tak | nie | tak | tak | 16,679 | 83,338 | 216,660 |
| Extra High | nie | tak | tak | tak | tak | 16,667 | 33,351 | 250,007 |
| Ultra | tak | tak | tak | tak | tak | 16,677 | 100,001 | 166,668 |

GPU zgłasza DXR. Tryby monitora obejmują w zakresie V13 wyłącznie 1920×1080.

## Etap M1

| Kontrola | Wynik | Zakres |
|---|---|---|
| Bramka uruchomiona ręcznie zdarzeniem TaskCompleted | PASS, EditMode 82/82, PlayMode 15/15 | gospodarka D/S, logika i integracja, raport w `qa/reports` |
| Testy bramki | 45/45 PASS | sam mechanizm, syntetyczny runner |
| Build Windows64 Mono, scena M1_Battle | Success, 261613684 B, 0 błędów, 0 ostrzeżeń | |
| Pojedynczy kuter, trasa 70 m | 108 D/min | model: do 124 D/min przy pełnym wykorzystaniu trasy |
| Mecz SI kontra SI, seed 9 | Rozstrzygnięty po 20 min, pierwsza walka po 148 s | jeden przebieg, nie dowód balansu |
| docs/economy-model/economy-model.py | Wykonany ponownie, CSV zgodne z pakietem | model nie obejmuje walki ani Unity |

Coast Rocks 02: 4 siatki, 2363290 trójkątów LOD0, Mesh LOD do 64 trójkątów na siatkę, FBX zaimportowany bez zmian, jeden materiał HDRP Lit. Na mapie wyspa ma około 102 × 7 × 110 m. Modele okrętów, osad i węzłów to oznaczone modele zastępcze.

Paski HP pojawiają się tylko po zaznaczeniu myszą, także dla wielu okrętów i widocznych wrogów. Dodane animacje i efekty to prezentacja na modelach zastępczych; nie obejmują mgły wojny dla odległych zdarzeń.

Wdrożone z gospodarki E00–E11: drewno i srebro, skończone punkty z dwoma stanowiskami i kolejką FIFO, magazyn z rezerwacjami, port na trzy statki, osobna linia wojenna i kolejka portu, zużycie kosztu wraz z postępem, rozdział ceny szablonu, remont, złomowanie, wrak z połową ładunku, księga świata i zapis w wersji 2.

Budżety kampanii M01–M14 i ładunek misji (E12) są tylko w dokumentach i danych. Sceny misji nie istnieją.

Uproszczenia względem specyfikacji, do wdrożenia w kolejnym etapie:

- Następny etap z E00: handlowiec, druga wyspa, eskorta, przyczółek, konwoje i lokalna produkcja.
- Nie ma inwestycji z E05, kolejnej stoczni, ostrzeżeń o nasyceniu ani rezerwy gracza; rezerwy 60 D/20 S przestrzega tylko SI.
- Nawigacja omija koła wysp; nie ma grafu akwenów, głębokości, zanurzenia, rezerwacji przejść ani formacji. Odległości mapy to linie proste, nie zmierzone trasy.
- Jedyna broń to łucznicy z rzutem seedowanym, bez lotu pocisku, sektorów i wyboru wyposażenia. Wyposażenie z szablonu nie jest osobnym przedmiotem, więc 30% szansy jego przetrwania we wraku nie jest wdrożone.
- Osada łączy port i stocznię w jednym obiekcie, więc zniszczenia samego portu nie ma; łup 30% ze zniszczonej osady nie jest wdrożony, bo jej zniszczenie kończy mecz.
- SI jest prosta i zna pozycję startową wroga oraz położenie punktów pracy. Balans: NOT_RUN.

## Znane ograniczenia

- Unity zapisuje `asserts="0"` w wynikach NUnit także dla testów z asercjami. Bramka wymaga wtedy asercji w kodzie właściwej metody testu, więc asercja wyłącznie w metodzie pomocniczej blokuje wynik. Zmiana przeszła niezależny przegląd, po którym usunięto wykryte obejścia; poprawek nie przeglądano ponownie. Analiza kodu nie dowodzi, że asercja faktycznie się wykonała.
- PreToolUse reaguje na narzędzie TaskUpdate, którego ta aplikacja nie używa. Automatyczne wywołanie hooków: NOT_RUN.
- Profile różnią się na razie funkcjami HDRP, kaskadami i ustawieniami objętości. Limity świateł, detal wody i gęstość VFX z V10 nie są wdrożone.
- Menu rozdzielczości (F10) i profili (F9) to robocze panele IMGUI.

## Nieuruchomione

- Dłuższa rozgrywka człowieka: użytkownik zagrał krótko, zaznaczanie i rozkazy działały, bez błędów. Paski HP, animacje i efekty dodane po tej próbie: NOT_RUN w ręcznej ocenie.
- Ocena obrazu, w tym wizualne potwierdzenie działania RTGI i odbić RT: NOT_RUN.
- Fizyczne wyświetlanie 2560×1440 i pozostałych trybów powyżej 1080p: BLOCKED, brak monitora.
- Pełne przełączanie rozdzielczości i profili w buildzie, 20 cykli bez wycieków: NOT_RUN.
- Pomiary GPU, VRAM oraz scen 100/200/400 statków: NOT_RUN.
- Integracja hooków w działającym Claude Code: NOT_RUN.
- Zewnętrzne CI: NOT_RUN.
