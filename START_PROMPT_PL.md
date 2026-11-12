# Rozpoczęcie pracy

Najpierw przyjmij §0 [jedynego nadrzędnego planu gry](docs/NOWY_PLAN_GRY.md): mitologiczna Grecja IV w. p.n.e., 400–301 p.n.e., z 400 r. włącznie. Każde działanie zmieniające projekt podlega [karcie zgodności](docs/KARTA_ZGODNOSCI_Z_PLANEM.md); sprzeczny zakres ma status BLOCKED_PLAN_CONFLICT i nie jest wykonywany.

Włącz [protokół komunikacji studia](PROMPT_KOMUNIKACJA_STUDIA.md). Przyjmij zastane prace do trwałego dziennika, uzyskaj ACK przydziałów i zachowaj jednego autora na plik oraz jednego runnera Unity. Wznowienie wymaga porównania checkpointu z bieżącym stanem, bez automatycznego przejmowania cudzej pracy.

Pracuj po polsku nad „Morzami Bogów”, jednoosobową grą w mitologicznej Grecji IV w. p.n.e. Odczytaj CLAUDE.md oraz docs/NOWY_PLAN_GRY.md, docs/KATALOG_MECHANIK.md, docs/PARAMETRY_ROBOCZE.md, docs/MAPY_I_SCENARIUSZE.md, docs/PLAN_WDROZENIA.md i docs/HISTORIA_I_ZRODLA.md.

Odczytaj też [docs/GRYWALNOSC_I_ODBIOR.md](docs/GRYWALNOSC_I_ODBIOR.md): kontrakty J01–J10 są częścią odbioru etapów. Pełne polecenie kontynuacji zawiera [PROMPT_WDROZENIOWY.md](PROMPT_WDROZENIOWY.md).

W Claude Code zastosuj [PROMPT_STUDIO_PREMIUM.md](PROMPT_STUDIO_PREMIUM.md) i [matrycę modeli](PROMPT_STUDIO_FABLE.md): Opus koordynuje, Fable implementuje i pisze testy, Opus wykonuje niezależne review, Haiku realizuje proste wyszukiwanie. Uruchom `Sprawdz-StudioPremium.ps1` i potwierdź rzeczywiste modele w kliencie przed użyciem danej roli.

Po każdym rozpoczęciu pracy z planem samodzielnie otwórz wiarygodne źródła historyczne i odpowiednie teksty mitologiczne. Odróżnij tezy materialne, tradycje literackie i fikcyjne reguły gry. Zapisz raport źródeł z autorem, wydaniem, lokalizatorem, faktycznym dostępem i ograniczeniami. Nie zastępuj odczytu pamięcią modelu. Cuda nie wymagają udawania dowodu naukowego.

Sprawdź Git, aktualny zapis, kod i najnowszy raport. Zacznij od najwcześniejszego nieukończonego zadania P z wykonanymi zależnościami. Zachowaj działający World, D/S, transport, konwoje, klasy, broń i zapis. Nie zmieniaj estetyki. Nie wdrażaj funkcji sieciowych.

Dla zadania ustal konkretny kontrakt, właściwe testy i decyzję o zapisie. Odtwórz błąd przed poprawką. Wykonaj małą zmianę, niezależny review i odpowiednią bramkę. Nie traktuj planu, walidatora danych ani wyniku innego snapshotu jako dowodu ukończenia. Raportuj wykonane funkcje, dowody, ograniczenia i następny krok.
