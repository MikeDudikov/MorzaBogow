# Morza Bogów — balans gospodarki

## Zakres dowodów

Wykonano docs/economy-model/economy-model.py w Pythonie, krok bazowy 1/30 s. Wyniki są w docs/economy-model: model-results.json i plikach CSV. Model obejmuje jedną bazę, dwa skończone punkty pracy, stanowiska pracy, fizyczne cargo, kolejki portu, finansowanie produkcji, budowę kutrów i stoczni oraz hipotetyczne zakłócenia. Nie symuluje obrażeń, zasięgów, walki, rozpoznania, nawigacji, prądów, pogody ani zachowania przeciwnika.

Model zapisuje zaangażowany koszt w chwili rozpoczęcia zlecenia i nie odwzorowuje postępu zużycia oraz zwrotów przy anulowaniu. Korzysta z liczb zmiennoprzecinkowych; implementacja gry ma używać określonej dokładności kwot i transakcji. Kontrola bilansu modelu toleruje poniżej 0,001 jednostki różnicy w całym przebiegu. Nie jest to test kodu gry.

Status: **obliczenia wykonane; balans walki i playtesty NOT_RUN**.

## 1. Pierwsza flota

Wszystkie próby produkują ten sam kompletny taranowiec 240 D/100 S w 35 s, aby porównać finansowanie, bez dopisywania różnym klasom umownej siły bojowej. Startowe cztery kutry i budynki są gotowe. Najbliższe zasoby w tym benchmarku są oddalone o 70 m. Kolejne kutry powstają w osobnej kolejce. Chroniona rezerwa wynosi 60 D/20 S.

| Otwarcie | Pierwszy okręt | Drugi | Czwarty | Gotowe do 10:00 |
|---|---:|---:|---:|---:|
| Dwa okręty przed rozbudową | 0:35 | 1:12 | 3:08 | 11 |
| Okręt i rozbudowa do 6 kutrów | 0:35 | 1:31 | 2:45 | 11 |
| Najpierw 8 kutrów | 1:47 | 2:22 | 3:32 | 9 |

Liczba w 10:00 oznacza łączną produkcję bez strat bojowych. Nie jest stanem armii po bitwie. Nie obejmuje ekspansji ani dostępu do kolejnych złóż. Wyczerpanie jednej bazy ogranicza wynik otwarcia z ośmioma kutrami. Tego wyniku nie wolno przedstawiać jako porażki strategii ekspansji: jej najważniejszy element nie jest w tej próbie symulowany.

Wniosek do prototypu: wczesna druga jednostka daje okno presji, a otwarcie z jednym okrętem i rozbudową szybciej finansuje następną grupę. Gromadzenie zbieraczy bez kolejnego miejsca pracy opóźnia flotę. Wartość okna presji i możliwość ukarania rozbudowy zależą od realnej walki.

## 2. Odległość i czas kontaktu

Zbadano 200 powtarzalnych kombinacji odległości, seed 7319: zasoby 70–110 m, droga do przeciwnika 650–850 m. To próbki dwóch odległości, a nie 200 wygenerowanych map ani rozegranych meczów.

Pierwszy taranowiec był gotowy w 0:35. Przy ruchu po przyjętej trasie z prędkością 8,2 m/s najwcześniejsze dotarcie pod bazę przeciwnika wyniosło **1:55–2:19**. To czas możliwego dotarcia, nie obserwacja faktycznego kontaktu. Rozpędzanie, kolizje i manewry mogą go wydłużyć. Drugi okręt otwarcia „okręt i gospodarka” był gotowy po 1:31–1:47.

Docelowe okno kontaktu 1:45–3:00 jest zgodne z takim oszacowaniem dojścia, ale nadal wymaga testu nawigacji i rozpoznania. Gracz ma czas na reakcję; nie ma bezpiecznego okresu narzuconego przez zegar.

## 3. Dochód po pełnym rejsie

Dla jednego kutra bez kolejki: T = C/q + 2d/v + 4 + C/u; przychód/min = 60C/T. Stałe: C=60, v=10, u=60. q=6 dla D, q=3 dla S. Wynik jest średnią długookresową; wypłata następuje przy rozładunku.

| Jedna droga | D/min | S/min |
|---|---:|---:|
| 70 m | 124,14 | 92,31 |
| 140 m | 83,72 | 67,92 |
| 280 m | 50,70 | 44,44 |
| 450 m | 34,29 | 31,30 |

Przy 280 m pojedynczy kuter dostarcza znacznie mniej niż przy 70 m. Położenie portu jest zatem wyraźną decyzją. Wysoka chwilowa szybkość pracy nie oznacza takiego samego dochodu frakcji. Sprawdzono zgodność wzoru z osobnym przebiegiem jednego kutra; różnica pierwszych dziesięciu minut uwzględnia rozpoczęcie pracy i nieukończony ostatni rejs, poniżej 7%.

## 4. Nasycenie i kolejki

saturation.csv zawiera 48 prób: trzy odległości × osiem wielkości grupy × dwa zasoby, każda po 180 s. Zasoby są skończone. Raportowany dochód obejmuje start od portu i jest średnią pierwszych trzech minut, nie nieprzerwanym dochodem po rozgrzaniu.

Na trasie 70 m drewno: 4 kutry dostarczyły średnio 440 D/min, 6 kutrów 640 D/min, a 8, 12 i 16 nadal 640 D/min. Srebro: 3 kutry dały 180 S/min, 4 dały 200 S/min, a 6 i więcej nadal 200 S/min. Fizyczne stanowiska oraz wspólny limit chronią przed nieograniczonym wzrostem w jednym miejscu.

Punkty mają dwa stanowiska i kolejkę. Zbyt duża grupa nie rozdziela pracy między kilkanaście niedokończonych ładowni. Koszt kolejki jest widoczny w UI; automat nie powinien budować kolejnych kutrów do pełnego punktu.

## 5. Ekspansja i rzeczywisty koszt dostaw

Osobny rachunek porównuje te same cztery kutry D i dwa S przy odległości 280 m oraz 70 m od miejsca wydawania zasobów. Uwzględnia stanowiska i kolejki. Dochód mierzono między 5:00 a 15:00 z zapasami punktów wystarczającymi na całą próbę. To warunek porównania tempa, nie odnawialne złoża gry.

Przyrost dostarczonego dochodu wyniósł około **300,00 D/min oraz 72,00 S/min**. Dla dodatnich korzyści obu zasobów czas odtworzenia kosztu określa wolniejszy składnik: max(koszt D/przyrost D, koszt S/przyrost S). Nie zamieniano srebra na drewno arbitralnym kursem.

- Port z nowym handlowcem, 440 D/160 S: około 2,22 min dodatkowej pracy.
- Port, handlowiec i lokalna stocznia, 640 D/260 S: około 3,61 min dodatkowej pracy.
- Zakres z czterema nowymi kutrami, 880 D/340 S: około 4,72 min dodatkowej pracy.

To optymistyczne rachunki odzyskania wydatku przy tej samej porównywanej grupie sześciu pracowników. Ostatni wariant uwzględnia zakup czterech kutrów, ale nie dodaje im osobno dochodu. Nie zastępuje symulacji uruchamiania nowej gospodarki. Nie obejmuje straconej pracy relokowanych kutrów, eskorty, strat, sekwencji budynków, wyczerpania punktów i kosztu nowych stanowisk wojennych w czasie. Sam rejs handlowca 280 m i budowa portu zajmują co najmniej 1:15, przed rozpoczęciem lokalnych dostaw. Rozpędzanie i dokowanie zwiększą ten czas.

Korzyść zakłada wydawanie zasobów lokalnie. Jeśli trzeba je wozić do macierzystej stoczni, handlowiec o ładowni 480 ma w tym wariancie maksymalnie około **288,00 jednostek D+S/min**: 80 s rejsu, 16 s załadunku i rozładunku, 4 s manewrów. Jednego limitu nie wolno przyznać osobno drewnu i srebru. Trzeba doliczyć kolejki i wybrać skład ładunku.

Wniosek: przyczółek daje silną korzyść przy lokalnej produkcji i dostępie do nowych zapasów. Nie tworzy natychmiastowego globalnego dochodu. Jego opłacalność wobec dodatkowej eskorty i kolejnych galer wymaga pełnej próby dwóch wysp.

## 6. Rajd i przerwanie dostaw

W otwarciu „okręt i gospodarka” w 3:00 usunięto jeden kuter D i jeden S. Przy wyborze ofiary model bierze jednostkę z największym ładunkiem danej grupy, rozdziela jej cargo na 50% straty oraz 50% wraku. Wrak nie jest zbierany w tej próbie. Odtworzenie liczby sześciu kutrów zajęło **36,00 s**. Nie oznacza to odtworzenia dostarczanego dochodu: nowe kutry muszą jeszcze dopłynąć, zebrać i rozładować.

Drugi wariant to 40 s postoju zbieraczy od 3:00, bez strat. Jest to kontrolowany przestój, nie symulacja bitwy lub blokady portu. Produkcja z wcześniej zgromadzonego zapasu działa dalej.

| Próba | Okręty do 4:00 | Okręty do 5:00 | D dostarczone do 5:00 | S dostarczone do 5:00 |
|---|---:|---:|---:|---:|
| Bez zakłócenia | 6 | 7 | 2340,00 | 840,00 |
| Utrata dwóch kutrów w 3:00 | 5 | 7 | 2174,00 | 780,00 |
| 40 s postoju od 3:00 | 5 | 6 | 1980,00 | 720,00 |

Zakłócenie tworzy okno przewagi, ale nie odbiera istniejącym okrętom zdolności walki. Liczba produkowanych jednostek może częściowo nadrobić stratę po dziesięciu minutach, dlatego oceniaj też chwilę gotowości i utracone możliwości, nie tylko sumę końcową. Próba nie wycenia czasu atakującego ani ryzyka utraty jego floty.

## 7. Sprawdzone ograniczenia

- Zachowanie sum D/S między złożami, magazynem, cargo, wydatkami, zatopieniem i wrakiem we wszystkich wykonanych przebiegach.
- Brak ujemnego magazynu, przekroczenia trzech stanowisk portu i dwóch stanowisk punktu pracy.
- Częściowy końcowy ładunek i nieujemny stan wyczerpanego złoża w próbie 30 minut.
- Zatrzymanie rozładunku przy pełnym magazynie; pozostałość pozostaje na statkach.
- Kroki 1/60, 1/30 i 1/15 s: drugi okręt różni się o mniej niż sekundę, a dostawy w 5:00 o mniej niż 1%. To kontrola zbieżności tych prób, nie dowód niezależności każdego możliwego stanu od kroku.
- Rozdział ceny wszystkich 12 kompletnych szablonów na konstrukcję, przedmioty i ludzi bez ujemnych składników lub drugiego naliczenia; template-costs.csv.
- Remont i złom: dla kosztu konstrukcji C oraz stanu h, remont do pełna i złom daje 0,15C−0,25C(1−h), co nie przewyższa 0,15Ch za złom od razu. Test punktów h=0;0,25;0,5;0,75;1 oraz nierówność algebraiczna.

## 8. Ryzyka wymagające prototypu

1. Wczesne tarany mogą zbyt łatwo niszczyć kutry. Sprawdź faktyczne dojście, przyspieszenie, zwrotność, drogę ucieczki, osłonę portu i koszt nieudanego ataku.
2. Utrata całej grupy obsługującej srebro może opóźnić rekrutację mocniej niż utrata drewna. Sprawdź rezerwę, kierowanie nowych kutrów i napływ pierwszego ładunku po odbudowie.
3. Port i lokalna stocznia mogą zbyt szybko się zwracać na krótkich trasach. Sprawdź pełny rozruch, utraconą pracę i cenę ochrony, zanim ustalisz koszt na stałe.
4. Zestaw z ośmioma kutrami bez ekspansji nie daje szybkiej floty w modelu. UI musi ostrzegać o nasyceniu; gracz rozwijający gospodarkę powinien mieć osiągalne nowe punkty.
5. Cena okrętów z dużą załogą oraz wartość zdobytego wyposażenia wymagają prób abordażu. Same ceny nie pokazują siły przejmowania jednostek.
6. Darmowe bieżące utrzymanie floty ogranicza obowiązki, ale może sprzyjać obronie. Oceń wieże, cele mapy i możliwość ostrzału oblężniczego; nie dopisuj ukrytych dochodów atakującego.

## 9. Odbiór rozgrywki

Wymagany etap: działający prototyp dwóch wysp z konwojem, lokalnymi stoczniami, taranem, ostrzałem i abordażem. Następnie macierz meczów rajd / ekspansja z eskortą / obrona z kontratakiem, zamiana stron i frakcji. Zmierz czas kontaktu, główne starcia, długość partii, odbudowę, wydatki na flotę i uwagę gracza. Wyniki SI uzupełnij playtestami ludzi.

Nie przypisano procentów zwycięstw ani statusu zatwierdzonego balansu. Model uzasadnia testowanie tych parametrów i ujawnia ograniczenia; nie dowodzi równowagi strategii.

## 10. Kampania

Model nie obejmuje misji kampanii. Budżety M01–M14 z E12 nie mają potwierdzenia obliczeniowego ani playtestów: NOT_RUN.
