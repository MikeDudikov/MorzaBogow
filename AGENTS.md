# Morza Bogów — instrukcje dla agentów

Stosuj [doktrynę jakości i dwie bramki odbioru](docs/RYGOR_PRODUKCJI_I_ODBIORU.md) oraz [WM01–WM03](docs/KONTRAKTY_MORZA_I_WIATRU.md). Każdy przyrost ma sprawdzalny kontrakt i niezależny odbiór; brak danych, testu lub faktycznego przeglądu nie oznacza PASS. Nie obniżaj kryteriów po zobaczeniu wyniku.

**Jedyny nadrzędny plan produktu:** [docs/NOWY_PLAN_GRY.md](docs/NOWY_PLAN_GRY.md), w szczególności §0. Obowiązuje mitologiczna Grecja IV w. p.n.e. (400–301 p.n.e., z 400 r. włącznie). Dokumenty pomocnicze nie mogą ustanawiać alternatywnej epoki ani sprzecznych reguł. Przed każdą mutacją, po niej i przed odbiorem stosuj [kartę zgodności](docs/KARTA_ZGODNOSCI_Z_PLANEM.md). Niezgodnego działania nie wykonuj; oznacz zależny zakres BLOCKED_PLAN_CONFLICT. Wyraźne polecenia użytkownika zachowują pierwszeństwo.

Stosuj [protokół komunikacji studia](PROMPT_KOMUNIKACJA_STUDIA.md): jeden koordynator zapisuje trwały lokalny dziennik; subagenci potwierdzają przydziały, zgłaszają blokady i oddają bezpieczne przekazania. Komunikacja nie rozszerza zakresu ani uprawnień. Podczas bramki zapisuj meldunki tylko poza chronionym snapshotem. Po wznowieniu sprawdź rzeczywisty stan, zanim wznowisz edycję.

Przeczytaj [CLAUDE.md](CLAUDE.md), [plan mechanik](docs/NOWY_PLAN_GRY.md), [katalog](docs/KATALOG_MECHANIK.md), [parametry](docs/PARAMETRY_ROBOCZE.md), [mapy](docs/MAPY_I_SCENARIUSZE.md), [plan integracji](docs/PLAN_WDROZENIA.md), [analizę stanu](docs/ANALIZA_STANU.md) i [protokół źródłowy](docs/HISTORIA_I_ZRODLA.md). Gra jest wyłącznie jednoosobowa, w mitologicznej Grecji IV w. p.n.e.; cuda działają w fikcji, bez przypisywania im naukowej prawdziwości.

Przy każdym rozpoczęciu pracy z planem samodzielnie sprawdź historyczne założenia w wiarygodnych źródłach otwartych w tej sesji i zapisz raport według protokołu. Dokument repozytorium ani pamięć modelu nie są weryfikacją. Nie przedstawiaj abstrakcji balansu jako faktów naukowych.

Nie zmieniaj estetyki, nie nadpisuj cudzej pracy i nie uznawaj planowanej funkcji za wykonaną. Wykorzystuj istniejącą architekturę, konwoje, zapis i bramkę. Przy błędzie zbierz dowód przed poprawką; odbiór dopasuj do rzeczywistego zakresu.

Obowiązują także [kontrakty dowodzenia i grywalności](docs/GRYWALNOSC_I_ODBIOR.md), J01–J10. Zachowaj zapis w pauzie, trwałość zadań gospodarczych, uczciwą wiedzę i taryfy AI, rozstrzygające cele oraz jawne rozliczenie kampanii. Sprawdzaj je na właściwych etapach P01–P12.

W środowisku Claude Code stosuj [organizację studia](PROMPT_STUDIO_PREMIUM.md) i [matrycę modeli](PROMPT_STUDIO_FABLE.md): Opus koordynuje i ocenia architekturę, Fable pisze kod i testy, niezależny reviewer działa na Opus, a proste wyszukiwanie na Haiku. Faktyczny model wymaga potwierdzenia. Te przypisania dotyczą konfiguracji Claude Code i nie oznaczają, że inne narzędzie lub uruchomienie użyło tych modeli.
