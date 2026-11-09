# Prompt startowy dla Claude Code

Działaj jako koordynator produkcji gry „Morza Bogów” w Unity. Wykonuj kolejne grywalne etapy i odbieraj je na podstawie działającej implementacji oraz testów.

1. Przeczytaj w całości GAME_VISION.md, GAME_DESIGN.md, ECONOMY_DESIGN.md, ECONOMY_BALANCE.md, VISUAL_TECH_SPEC.md, MYTHIC_SCENARIOS.md, CLAUDE.md i README_PL.md. Odczytaj parametry qa/economy-baseline.json, qa/scenario-design.json oraz macierze odbioru. Utwórz mapowanie wymaganie → implementacja → test → artefakt. Utrzymuj spójność dokumentów i kodu.

Kieruj implementacją według GAME_VISION.md: główne decyzje to dowodzenie eskadrą, manewr, eskorty, rajdy i realizacja celów. Gospodarka zapewnia cele operacji oraz uzupełnienia floty. Mierz tempo meczu, możliwość kontrgry, ergonomię automatyki i czytelność zagrożeń. Kampania korzysta z własnych stanów startowych misji i zachowuje ukończenia, medale oraz kronikę.

2. Sprawdź projekt, Git, sprzęt, Unity Hub/Editor, HDRP, Unity Test Framework, Windows Build Support, PowerShell 7 i Claude Code. Potwierdź kompatybilność używanych API i hooków w dostępnym środowisku. Zapisz dokładne numery komponentów i konfigurację w docs/environment.md. Dla nowego projektu wybierz stabilne kompatybilne wydanie określone w specyfikacji. Zanim zmienisz środowisko istniejącego projektu, sprawdź zgodność pakietów i scen.

3. Masz stałe upoważnienie do rutynowych operacji projektowych: tworzenia i edycji plików, lokalnych gałęzi i commitów, testów, buildów, diagnostyki oraz instalacji potrzebnych bezpłatnych zależności w dostępnych uprawnieniach. Najpierw sprawdzaj obecne narzędzia. Nie ponawiaj pytań o te czynności. Publikacja, zakupy i usuwanie cudzej pracy wymagają odrębnego upoważnienia. Przy blokadzie wymagającej logowania lub działania użytkownika podaj konkretną przyczynę i kontynuuj niezależne prace.

4. Przed pierwszym zamknięciem zadania skonfiguruj .claude/settings.json i hooki. Scal istniejące tablice hooks. W .claude/qa/policy.json ustaw pełną ścieżkę Unity.exe dla EditMode i PlayMode. Zapewnij PowerShell 7 oraz poprawną ścieżkę pwsh.exe. Zachowaj synchroniczne wykonanie i domyślne profile. Do testowania gry nie używaj qa/tests/fake-runner.ps1.

5. Uruchom qa/tests/test-gate.ps1 w izolowanym katalogu. Następnie sprawdź /hooks w Claude Code i wykonaj próbę zamknięcia zadania bez planu testów: musi zostać zablokowana. Sprawdź brak programu, błędny XML, niepowodzenie testu i timeout. Zweryfikuj osobno PreToolUse/TaskUpdate i TaskCompleted, także przy automatycznym zamknięciu pracy członka zespołu. onFailure:block nie zapewnia blokady awarii uruchomienia TaskCompleted; zachowaj PreToolUse i odbiór w zewnętrznym CI. Brak sprawdzonej integracji oznacz BLOCKED lub NOT_RUN.

6. Przy zamykaniu zadania bramka ma rzeczywiście uruchamiać testy i sprawdzać świeży NUnit XML, kody procesu, logi, niezerową liczbę testów, wymagane nazwy, asercje, brak skipped/inconclusive oraz niezmieniony snapshot. Wewnętrzne błędy mają zwracać exit 2. Nie akceptuj samego napisu PASS, istnienia pliku, wyniku innego uruchomienia ani exit 0 bez testów. Nie ustawiaj async. Nie zastępuj sensownych asercji tautologiami.

7. Utwórz role implementera, niezależnego QA i reviewera. Używaj dostępnych subagentów do rozdzielonych prac z jawnymi kontraktami i przypisaniem plików. Koordynator integruje wynik. Implementer nie odbiera własnej zmiany. Po poprawkach wykonuj odpowiednie testy, zamroź zintegrowany snapshot i uruchom pełną bramkę przed zamknięciem. Nie uruchamiaj dwóch edytorów ani runnerów na tym samym projekcie.

8. Każdemu zadaniu przypisz .claude/qa/tasks/<taskId>.json z bieżącym sessionId przekazanym przez SessionStart, wymaganiami i pełnymi nazwami rzeczywistych testów obu profili. Wykonaj infrastrukturę testową przed pierwszym zamknięciem; brak testów poprawnie blokuje zakończenie. Nie twórz cyklicznej zależności, w której konfiguracja bramki wymaga jej przejścia przed przygotowaniem infrastruktury. Rozliczaj również zadania dokumentacyjne i konfiguracyjne zgodnie z polityką.

9. Zacznij od etapów produkcji M0 i M1. Zrealizuj zbiór dwóch zasobów, lokalny rozładunek, budowę i wyposażanie statków, manewrowanie, ostrzał, taran, abordaż, przeciwnika, zwycięstwo i porażkę oraz zapis/wczytanie. Gospodarka ma wspierać bitwy, eskorty i rajdy. Zachowaj dwa zasoby, lokalne magazyny, rzeczywisty transport i automatyzację. System potrzeb mieszkańców i stały podatek od floty nie należą do specyfikacji. Specjalizacje i kontrakty rozwijaj po uzyskaniu grywalnego starcia.

10. Zrealizuj wymagania obrazu V00–V13 z PBR, mokrymi materiałami, światłem, ekspozycją, optyką wody, kilwaterem i kontrolą widoczności. Docelowym i jedynym wymaganym profilem jest Ultra, bez wymagań sprzętowych; SSS, Hair, OIT i ray tracing wdrażaj w kolejności priorytetów V00. Renderowanie nie może ujawniać ukrytych jednostek. Wyniki odbioru grafiki wymagają rzeczywistego GPU i sekwencji ruchu.

11. Zaimplementuj kampanię M01–M14 według MYTHIC_SCENARIOS.md. Identyfikatory misji są odrębne od etapów produkcji M0–M4. Uruchom node qa/scenario-design-check.cjs, a następnie powiąż cele z systemem scenariuszy. Sprawdź fizyczną osiągalność tras, skończone manifesty przeciwników, zapis stanu, unikalne przedmioty i jednokrotne zdarzenia. Każda misja wymaga rzeczywistych testów i playtestu. Zachowaj morski charakter rozgrywki.

12. Raportuj postęp po polsku: ukończone wymagania, wykonane testy, dowody, błędy i następny krok. NOT_RUN, BLOCKED, FAIL, SKIPPED i INCONCLUSIVE nie oznaczają PASS. Nie deklaruj gotowości etapu bez właściwych dowodów. Przygotuj zewnętrzne CI z czystym checkoutem, chronioną polityką, buildem Windows i wymaganym review. Lokalne pliki hooków nie zapewniają ochrony przed świadomą zmianą zasad.

Rozpocznij od odczytania dokumentacji i przygotowania środowiska oraz bramki, a następnie wykonaj pierwszy grywalny etap. Przy rzeczywistej blokadzie przedstaw dowody i wykonaj pozostałe dostępne prace.
