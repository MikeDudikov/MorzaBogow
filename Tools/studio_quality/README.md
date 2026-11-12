# Walidatory dowodów odbioru

Python 3.10+, biblioteka standardowa. Wrapper PowerShell: `Sprawdz-OdbiorStudia.ps1` (7.5+). Normy: `docs/RYGOR_PRODUKCJI_I_ODBIORU.md`. Żaden z tych skryptów nie uruchamia Unity ani nie instaluje hooków.

## Regresje

`regression --baseline qa/studio/regression-baseline.json --report <względna ścieżka report.json>` sprawdza dwie rzeczywiste suite XML, logi, hashe, kody procesu, spójność liczników i pełną listę przyjętych nazw. Bez expected-snapshot wynik ma jawny zakres HISTORICAL_ARTIFACTS_ONLY. Wrapper domyślnie liczy aktualny snapshot identycznym algorytmem jak istniejąca bramka; `-Historical` jest osobnym trybem audytu historii.

Baseline pochodzi z poświadczonego P01 (404 EditMode, 153 PlayMode). Nowe testy można dodawać. Usunięcie/zmiana nazwy wymaga zatwierdzonego kontraktu, niezależnego review i jawnej aktualizacji baseline; nie usuwać nazw po nieudanym uruchomieniu tylko dla uzyskania PASS. Plany następnych zadań powinny obejmować jego nazwy w expectedTests, aby istniejąca bramka rzeczywiście je egzekwowała.

## Dwie bramki

`acceptance --contract <plik> --evidence <plik> --expected-snapshot <hash> --expected-session <faktyczna sesja>` daje osobne wyniki art/physics. Wrapper wymaga `-SessionId` albo QUALITY_GATE_SESSION_ID. Ścieżki wejść i artefaktów są względne wobec projektu; traversal, dyski/ADS i reparse points są odrzucane. Wzorce `qa/studio/acceptance-*.template.json` mają template=true i nigdy nie przechodzą odbioru. Najpierw uzgodnić rzeczywisty kontrakt; nie wypełniać dowodów fikcyjnym PASS.

`sourceFiles` to rekordy path/sha256 dla dokładnych plików kontraktu. `scopeReview` zawiera reviewerId, verdict, actualResultInspected, notes oraz reviewedSha256 obejmujące kontrakt i źródła. Reviewer ma inne kanoniczne ID od wszystkich autorów. Lokalne ID i deklaracje są świadectwami przypisanymi przez koordynatora, nie uwierzytelnieniem.

Art: references zawierają path/sha256; baseline i candidate mają identyczne captureSettings. Frames zawierają path/sha256/timeSeconds, zgodne z frameTimesSeconds kontraktu. Dla ruchu wymagane są różne klatki oraz MP4/WebM recording z hashami i deklaracją rzeczywistego odtworzenia przez reviewera. PNG wspierany przez ten walidator to RGB/RGBA8 bez przeplotu; sprawdzane są CRC, dekompresja i wiersze obrazu. Kontrola kontenera filmu nie jest dekodowaniem całego nagrania. Każde criteria ma score i notes; reviewer przypina wszystkie oglądane artefakty w reviewedSha256 i podaje criticalDefects.

Physics: sources mają url, author, locator, claim, limitations, access=CONTENT_READ, kind (RESEARCH/STANDARD/DATASET/CRITICAL_EDITION) i readInSession zgodne z aktywną sesją. Model ma name/version/equations/units/parameters/validity/assumptions. To kompletność deklaracji; interpretację modelu, źródeł i zakresu weryfikuje niezależny reviewer.

`comparisons` kontraktu to mapa nazw pomiarów. Każdy wpis: reference (path/sha256), referenceType (MEASUREMENT/ANALYTICAL/CALIBRATION), referenceProducerId, provenance, unit, minimumSamples, maxAbsError i maxRmse. Evidence comparisons przypina osobny aktualny plik CSV. Kolumny dokładnie `time_s,value,unit`, co najmniej 3 skończone próbki, ściśle rosnące i zgodne czasy, zgodne jednostki; tolerancje są zapisane przed pomiarem. Skrypt sam oblicza RMSE i maxAbsError, bez niejawnej interpolacji lub konwersji jednostek. Idealnie zgodne wartości są dopuszczalne, ale plik odniesienia i wynik są osobnymi artefaktami z niezależnym pochodzeniem.

Evidence nunit przypina XML z rzeczywiście wykonanymi numericalTests. Gdy Unity raportuje asserts=0, wymagane unityGateReport (path/sha256), zawierające ten XML, obie suite i aktualny snapshot/sesję. Istniejąca bramka sprawdza wtedy źródło asercji. Sam XML z zerem asercji bez takiego dowodu nie przechodzi.

MEASURED_VALIDATION wymaga danych pomiarowych. CALIBRATED i RECONSTRUCTION pozostają odrębnymi poziomami nawet przy PASS. W każdym przypadku PASS dotyczy uzgodnionego zakresu dowodu. Brak danych, nieporównywalne ujęcia, inny snapshot/sesja lub zmieniony kontrakt blokują wynik. Narzędzie nie ocenia piękna, nie weryfikuje otwarcia strony internetowej i nie dowodzi naukowej prawdziwości wszystkich parametrów.

## Testy

`python -B -m unittest discover -s Tools/studio_quality/tests -v` — izolowane fixture w ignorowanym work/. Testy tego narzędzia nie są testami gry. Kod wyjścia CLI: 0 oznacza przejście właściwej kontroli opisanej w JSON, 2 oznacza BLOCKED. Nie interpretować historycznego zakresu jako odbioru bieżącej gry.
