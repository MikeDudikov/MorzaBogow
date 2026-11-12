# Morza Bogów

Komunikacja głównego agenta i subagentów: [gotowy protokół](PROMPT_KOMUNIKACJA_STUDIA.md) oraz [lokalny dziennik](Komunikacja-Studio.ps1). Koordynator utrwala przydziały, potwierdzenia, decyzje i przekazania w `work/studio-communication/`; zapis pozostaje lokalny i nie uruchamia modeli w tle.

Jednoosobowy naval RTS w mitologicznej Grecji IV w. p.n.e. Plan obejmuje flotę, modułowe uzbrojenie, trzy sposoby walki, porty, badania, moce, postacie i artefakty we wspólnej symulacji. Stan implementacji należy sprawdzać w kodzie i aktualnych raportach.

Czytaj w kolejności:

1. [CLAUDE.md](CLAUDE.md) — zasady pracy i źródła w każdej sesji.
2. [Plan gry](docs/NOWY_PLAN_GRY.md).
3. [Katalog mechanik](docs/KATALOG_MECHANIK.md).
4. [Parametry robocze](docs/PARAMETRY_ROBOCZE.md).
5. [Mapy i scenariusze](docs/MAPY_I_SCENARIUSZE.md).
6. [Plan integracji](docs/PLAN_WDROZENIA.md).
7. [Rejestr badań](docs/HISTORIA_I_ZRODLA.md).
8. [Dowodzenie i odbiór grywalności](docs/GRYWALNOSC_I_ODBIOR.md).

Uruchomienie pracy w Claude Code: [gotowy prompt wdrożeniowy](PROMPT_WDROZENIOWY.md). Otwórz projekt w tym katalogu i poleć wykonanie całego pliku promptu.

Organizacja zespołu: [studio premium](PROMPT_STUDIO_PREMIUM.md) i [podział modeli Opus/Fable/Haiku](PROMPT_STUDIO_FABLE.md). Projektowy model główny to Opus; pięć definicji w `.claude/agents/` określa architekta, programistę, autora testów, reviewera i wyszukiwanie. Pełna kontrola konfiguracji: [Sprawdz-StudioPremium.ps1](Sprawdz-StudioPremium.ps1). Kontrola lokalna nie potwierdza dostępności ani rzeczywistego modelu w kliencie.

Punkt bazowy analizy: commit 81ec638, zapis 18, odczytana bramka PASS 364/147. To dowód wskazanego snapshotu, nie całego docelowego katalogu ani aktualnego stanu każdej późniejszej pracy. Etap kontynuacji wybierz po sprawdzeniu bieżącego kodu i najnowszych dowodów. Kod i estetyka nie zostały przebudowane w ramach przygotowania planu.
