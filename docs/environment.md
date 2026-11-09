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
| Unity 6.3 LTS | NOT_VERIFIED | wybrana wersja projektu; czeka na instalację użytkownika |
| Unity 6000.6.7f1 | 6000.6.7f1 | zainstalowany, nieużywany w tym projekcie |

Dokładny numer wersji 6.3 LTS, wersje HDRP, VFX Graph i Unity Test Framework oraz lockfile trzeba wpisać po instalacji Editora i utworzeniu projektu.

## Kontrole wykonane

| Polecenie | Wynik | Zakres |
|---|---|---|
| `node qa/scenario-design-check.cjs` | STRUCTURE_VALID, 14 misji, 613 kontroli | tylko struktura danych |
| `pwsh -NoProfile -File qa/tests/test-gate.ps1` | 27/27 PASS | tylko mechanizm bramki, syntetyczny runner |

## Nieuruchomione

- Integracja hooków w działającym Claude Code: NOT_RUN.
- Testy Unity EditMode i PlayMode: NOT_RUN, projekt Unity nie istnieje.
- Zgodność funkcji HDRP i ray tracingu na tym GPU: NOT_RUN.
- Build Windows: NOT_RUN.
