---
name: komunikacja-studia
description: Aktywuje tylko protokol komunikacji i trwaly dziennik istniejacego zespolu.
disable-model-invocation: true
---

Zastosuj wyłącznie protokół z `C:\Users\noirp\MorzaBogow\PROMPT_KOMUNIKACJA_STUDIA.md` oraz narzędzie `C:\Users\noirp\MorzaBogow\Komunikacja-Studio.ps1` w projekcie `C:\Users\noirp\MorzaBogow`. Odpowiadaj po polsku.

To polecenie aktywuje komunikację i ciągłość zapisu. Zachowaj istniejące role, modele, przydziały oraz postępy. Nie wdrażaj ponownie całej organizacji studia, nie rekonfiguruj modeli, hooków ani uprawnień i nie rozpoczynaj nowego zadania programistycznego w wyniku tej aktywacji. Bieżące prace doprowadź do bezpiecznego punktu bez przymusowego przerywania operacji.

1. Przeczytaj cały wskazany prompt komunikacji. Sprawdź, kto obecnie koordynuje, jakie zadania i pliki są zajęte oraz czy trwa bramka. Nie przejmuj koordynacji od aktywnej sesji. Jeśli wdrożenie wymagałoby edycji zamrożonego snapshotu, odłóż tę edycję.
2. W PowerShell wykonaj:

   ```powershell
   pwsh -NoProfile -File 'C:\Users\noirp\MorzaBogow\Komunikacja-Studio.ps1' -ProjectRoot 'C:\Users\noirp\MorzaBogow' -Action Init
   pwsh -NoProfile -File 'C:\Users\noirp\MorzaBogow\Komunikacja-Studio.ps1' -ProjectRoot 'C:\Users\noirp\MorzaBogow' -Action Read -Last 40
   ```

   Po każdym poleceniu sprawdź kod zakończenia. Przy błędzie zatrzymaj zależną aktywację i zgłoś przyczynę; nie kasuj dziennika ani blokad. Odtwórz ostatni CHECKPOINT i WSZYSTKIE późniejsze zdarzenia przez Read -AfterRevision N bez -Last; bez checkpointu odczytaj cały dziennik. Porównaj zapis z rzeczywistym stanem projektu.
3. Użyj istniejącego kanału do głównego agenta i subagentów. Sprawdź SendMessage w faktycznej sesji; nie zakładaj, że zmiana definicji nadała je już działającym instancjom. Sprawdź komunikat i rzeczywisty ACK na jednym dostępnym wykonawcy, wyłącznie do odczytu. Zapisz wynik przez koordynatora. Bez dostępnego kanału zastosuj RESULT_ONLY i jawny punkt przekazania; transport bez testu oznacz NOT_RUN. Nie uruchamiaj całego zespołu do tego sprawdzenia i nie zmieniaj przypisanych modeli.
4. Przyjmij zastane zadania do rejestru. Utrwal prawdziwe przydziały, potwierdzenia, rezerwacje plików, blokady, decyzje i oczekiwane odpowiedzi. Tylko koordynator zapisuje dziennik przez Append z aktualnym ExpectedRevision; role do odczytu zwracają komunikaty bez edycji plików. Nie wymyślaj ACK ani autorstwa.
5. Zachowaj jednego autora na plik i jednego właściciela uruchomień Unity. Stosuj FREEZE/freezeId i potwierdzenia WRITES_STOPPED przed przyszłą bramką; sama aktywacja nie wymaga uruchomienia Unity. Dziennik pozostaje w work/ poza obecnym snapshotem.
6. Zapisz CHECKPOINT z aktualnymi zadaniami, sesją, agentami/ID, dowodami modeli lub UNKNOWN, rezerwacjami, decyzjami, nierozstrzygniętymi ACK, stanem freeze, runnerem i następnym krokiem. Ponownie sprawdź spójność rejestru z rzeczywistą pracą i zakresem tego polecenia. Zachowaj wpisy między sesjami, odtwarzaj je po wznowieniu i nie traktuj dziennika jako działającego w tle transportu.

Zakończ krótkim raportem aktywacji: ścieżka i rewizja dziennika, rzeczywiście sprawdzony kanał, wynik ACK, przyjęte zadania, ograniczenia i punkt kontynuacji. Sama obecność plików nie dowodzi aktywnej komunikacji. Nie rozpoczynaj kolejnego przyrostu gry w ramach tego polecenia.
