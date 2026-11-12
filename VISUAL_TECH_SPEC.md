# Morza Bogów — techniczna specyfikacja obrazu

## V00. Kontrakt wdrożenia i priorytety

Obraz realizuje GAME_VISION.md: z kamery strategicznej czytelne są kierunek kadłuba, rola jednostki, stan uszkodzeń, trasa i zapowiedź zagrożenia. Woda, światło i efekty trafień mają wspierać ocenę manewru. Szczegółowość ujęć z bliska nie ma pierwszeństwa przed czytelną bitwą.

Dokument określa wymagania wykonawcze obrazu dla Mórz Bogów. Odbiór każdej funkcji wymaga działającej implementacji i pomiarów w docelowym środowisku.

Hierarchia: mechanika w GAME_DESIGN.md zachowuje pierwszeństwo; ten dokument precyzuje prezentację. Nie zmienia kosztów, zasięgów, obrażeń, widoczności SI ani reguł pogodowych. Renderowanie może interpolować i redukować jakość, ale nie może zmieniać wyniku symulacji. Nie buduj od zera renderera DirectX. Rozpocznij od funkcji HDRP i dopiero na podstawie pomiarów rozszerzaj Shader Graph, VFX Graph lub Custom Pass.

P0: biblioteka PBR, ekspozycja, światło, czytelność, brak przecieków mgły wojny, stabilne kadłuby i woda. P1: warstwowe uszkodzenia, fale i piana, pełne profile środowiskowe i wydajność floty. P2: szczegółowa skóra/włosy w zbliżeniu, OIT i dodatkowe odbicia. P0–P2 oznaczają kolejność prac, nie cięcie zakresu: wszystkie należą do docelowego profilu Ultra. Zbliżenia nie mogą opóźniać pierwszego grywalnego RTS.

Przed kodowaniem utwórz docs/rendering/compatibility.md: dokładna wersja Unity, HDRP, VFX Graph, API grafiki, GPU/sterownik, obsługiwane funkcje oraz dostępność każdego użytego API. Sprawdź build Windows, nie tylko Editor. Dla każdej funkcji wpisz AVAILABLE/UNAVAILABLE/NOT_VERIFIED i wynik minimalnej sceny. Nie zgaduj nazw klas/serialized fields z innej wersji. Brak funkcji → dokumentowany zamiennik i aktualizacja ADR, bez cichego pominięcia wymogu wizualnego.

## V01. Budowa klatki i odpowiedzialności

Logiczny przepływ, który należy odwzorować na faktyczny Frame Debugger zablokowanego HDRP (to nie lista jego niezmiennych wewnętrznych passów):

1. Odczyt niezmiennego snapshotu prezentacji: pozycje, stan morza, uszkodzenia, aktywne efekty, aktualna widoczność gracza.
2. Culling, dobór LOD, aktualizacja ograniczonych pul VFX i świateł, ustalenie niejawnych obiektów bez ujawniania ich odbiciami/cieniami.
3. Geometria opaque/alpha-clipped, bufory głębi i wymagane motion vectors; cienie i oświetlenie zgodnie z HDRP.
4. Woda, odbicia, refrakcja, mgła i przezroczystości w kolejności rzeczywiście wspieranej przez pakiet. Zweryfikować punkty wstrzyknięcia, potwierdzić obsługę każdego etapu przez API.
5. Ekspozycja, tonemapping, grading i AA zgodnie z HDRP; HUD kompozytowany tak, by nie dziedziczył gradingu świata i nie dostarczał sygnału do automatycznej ekspozycji.

Zacznij od Deferred dla opaque Lit oraz standardowej ścieżki Forward transparent. Przeprowadź A/B z Forward na tej samej scenie: otwarte morze z jednym słońcem oraz nocny port z wieloma światłami. Zapisz CPU/GPU i artefakty; wybierz wariant po pomiarze. Nie zakładaj, że ustawienie Deferred obejmuje wodę, włosy, tkaniny i wszystkie przezroczystości. Tryb Both tylko jeśli konkretny przypadek go wymaga; dodatkowe warianty zwiększają koszt builda.

Moduły prezentacji: EnvironmentPresentation (profile i pogoda), ShipVisualState (moknięcie/uszkodzenia), OceanPresentation (powierzchnia/kilwater), PresentationEventRouter (zdarzenia), EffectBudgetManager (priorytety i pule), VisualVisibilityPolicy (co wolno pokazać lokalnemu graczowi), VisualCaptureRunner (powtarzalne sekwencje). To proponowane odpowiedzialności, nie wymaganie dokładnie tych nazw klas. Zapis gry przechowuje stan logiczny; po wczytaniu efekty odtwarza się ze stanu, nie serializuje żywych uchwytów GPU.

## V02. PBR — geometria, tekstury i materiały

Modeluj sylwetkę kadłuba, dziób, taran i stanowiska broni geometrią, nie samą normal mapą. Z dalszej kamery ma być rozpoznawalna klasa, kierunek i frakcja. Detal desek, spoin i drobnych zarysowań może być wypalony z high-poly do normal/AO. Nie wypalaj kierunkowego słońca, cienia żagla ani białych refleksów w Base Color: po zmianie pory dnia pozostałyby w błędnym miejscu.

Użyj liniowego workflow. Base Color jako tekstura koloru z poprawnym sRGB; mapy danych bez sRGB, normalne z właściwym importerem i konwencją kanału Y. Weryfikuj shader i import testową sferą oraz płaszczyzną. W typowym HDRP Lit Mask Map kanały służą metallic/AO/detail/smoothness; przed generowaniem assetów potwierdź dokładne pakowanie w użytym shaderze. Nie podłączaj roughness bezpośrednio do smoothness: roboczo smoothness=1−roughness, z uwzględnieniem ewentualnych remapów. Nie odwracaj jej drugi raz w importerze.

Przygotuj wzorce: suche drewno, mokre drewno, brąz polerowany, brąz z patyną, lina, płótno, skała wapienna, mokry kamień, popiół, skóra, piana. Dielektryki mają metallic≈0; odsłonięty metal ≈1; patyna, brud i sól maskują metal, zamiast podnosić metallic całej powierzchni. Energia odbicia i rozproszenia musi pozostać spójna — unikać ręcznego dodawania nieograniczonego blasku do Lit.

Startowe przedziały roughness do strojenia, nie wartości pomiarowe: drewno suche 0,55–0,8, mokre 0,25–0,5, brąz 0,2–0,45, płótno 0,65–0,9. Każdą próbkę oceń w słońcu, zachmurzeniu i nocnym świetle. Podobne albedo przy innej roughness nie może zmieniać przedmiotu w plastik. Normal strength i częstotliwość detalu dobieraj do odległości; migotanie desek z daleka wymaga filtracji/mipów i uproszczenia, nie mocniejszego sharpeningu.

Wyjściowe tekstury: główne okręty do 2K na zestaw, królewski/bohater zbliżeniowy opcjonalnie 4K, małe rekwizyty 512–1K; rozmiar potwierdzany pokryciem ekranu. Stosuj atlasy/trim sheets z zachowaniem mip padding, współdzielone materiały i streaming mipów. Nie rezerwuj 4K osobno dla każdej deski. BC7/BC5 lub inny format wybierz po sprawdzeniu platformy i porównaniu normalnych; rejestruj pamięć zamiast liczyć ją wyłącznie z wielkości PNG.

## V03. Moknięcie, sól i zniszczenia

Stan wizualny statku: wetness [0,1], soot [0,1], burn [0,1], damage [0,1], waterlineHeight oraz deterministyczny variationSeed. Są to wejścia shaderów/presetów, nie dodatkowe punkty życia. Kadłub ma maskę linii wodnej i częstsze moknięcie przy dziobie. Deszcz zwiększa wetness; wysychanie stopniowe, roboczo stała czasowa 30–90 s. Wraz z wetness zmniejsz roughness i delikatnie przyciemnij albedo; nie zeruj roughness i nie dodawaj metallic.

Pożar zmienia emisję, sadzę i zwęglenie lokalnie. Jasne płomienie nie oznaczają automatycznie jasnej czarnej sadzy. Rozbij kadłub wizualnie na sekcje lub użyj masek uszkodzeń; moduluj rzeczywiste naprawione/zniszczone moduły. Dziura w teksturze nie wystarcza, jeśli gracz widzi przez nią nieistniejące wnętrze — stosuj kontrolowany zamiennik siatki albo zakrycie przekroju. Odłamki są pooled i mają limit życia, nie stają się trwałymi pełnymi Rigidbody wszystkich statków.

Przekazuj dane per instancja bez tworzenia materiału co klatkę. Dobór MaterialPropertyBlock/instancing/SRP Batcher sprawdź na rzeczywistym shaderze; nie gwarantuj równoczesnej korzyści ze wszystkich mechanizmów. Zrób pomiar draw calls po włączeniu różnic wetness i damage na 400 jednostkach.

## V04. Światło, cień i pośrednie doświetlenie

Światło ma tworzyć czytelną bryłę: główne słońce/księżyc, kontrolowany ambient nieba, lokalne ciepłe ogniska w porcie i chłodniejsze efekty morskie. Startowa wysokość słońca 25–45° do testów; potem sprawdź pełny cykl. Parametry fizyczne wpisuj w jednostkach obsługiwanych przez komponent (lux/lumen/kandela), nie mieszaj nieudokumentowanego intensity między typami światła. Temperatura barwowa startowo słońce ok. 5500–6500 K, ogień 1800–2400 K, ale kolor płomienia zależy też od emisji i tonemappingu.

Osobne limity świateł widocznych, świateł z cieniem i świateł wpływających na wolumetrię. Limity świateł każdego profilu określa V10. Każdy błysk nie tworzy nowego obiektu Light; używaj puli i priorytetu: bliskość, obszar na ekranie, znaczenie rozgrywkowe, intensywność. Kilka pożarów w skupisku może współdzielić światło z wygaszaniem, bez nagłego migotania przy zmianie lidera. Sama emisja materiału nie gwarantuje oświetlenia otoczenia w rasterze.

Cienie kaskadowe dla światła kierunkowego: liczba kaskad według V10, odległość dobrana do kadru, a nie całych 5 km mapy. Ustaw bias testem liny, burty, masztu i styku portu. Zbyt duży bias daje pływanie cienia, za mały acne. Contact shadows dla najbliższych istotnych obiektów; SSAO wzmacnia styk, nie zastępuje cienia kierunkowego i nie powinno czernić całej burty.

Pośrednie światło: niebo + sondy/reflection probes oraz opcjonalne dane pieczone dla statycznych portów w wariantach oświetlenia. Ruchomy statek nie może odziedziczyć wypalonego na stałe cienia w miejscu startu. Zmianę pory dnia i sond uzgodnij; nie pozostawiaj jaskrawej dziennej sondy w środku nocy. RTGI nie jest obecnie używane: pod ortogonalną kamerą gry pozostawia plamki na powierzchniach, więc Ultra stosuje SSGI, a promienie śledzi w odbiciach. Powrót do RTGI wymaga potwierdzenia w V00 bez tych artefaktów. Brak tych technik na niższym profilu nie może ujawniać przeciwników ani zmieniać czytelności zagrożeń.

Nie wygaszaj fizycznie całego ambientu poza światłem statku jako zamiennika mgły wojny. Ukrywanie wroga musi nastąpić przed renderowaniem jego geometrii, odbicia, cienia, światła i VFX. Wiele kamer i wspólne mapy cieni wymagają osobnej kontroli per obserwator — globalne wyłączenie renderera może zepsuć widok drugiego gracza/obserwatora.

## V05. HDR, ekspozycja i kolor

Rendering HDR oznacza pracę na wartościach ponad zakresem ekranu SDR. Monitor HDR jest osobnym trybem wyjścia. Najpierw dopracuj SDR sRGB/Rec.709; wyjście HDR10 wymaga osobnej kalibracji i nie jest warunkiem pierwszego etapu.

W scenie walidacyjnej zablokuj ekspozycję. W gameplay użyj ekspozycji stałej zależnej od pory dnia albo mocno ograniczonej adaptacji; startowo zakres adaptacji maks. 2 EV i przejście co najmniej 1 s. Błyskawica nie może przyciemnić całej sceny na kilka sekund. Jeśli metering mask jest dostępne w danej wersji, pomiń dominującą poświatę i HUD; przy braku rozwiązania wróć do stałej ekspozycji. Nie koryguj błędnego albedo przez globalne rozjaśnienie wszystkiego.

Porównaj tonemapping Neutral i ACES na tym samym kadrze. Wybierz po ocenie białej piany, nasyconej czerwieni ognia, granatowych żagli i brązu. Nie łącz dwóch krzywych tonemappingu przez przypadkowy import LUT z już wypalonym kontrastem. LUT/profile mają opis przestrzeni i zakresu, dokumentowany white balance i wersję. Zmiana biomu blendowana przez 3–5 s; żadnych ostrych progów koloru na granicy.

Trzy poziomy jasności sceny: czytelny obszar walki, spokojniejsze tło, krótkie akcenty zdarzeń. Bloom tylko dla mocnej emisji; piana i cała tafla morza nie mogą stale świecić. Chromatic aberration, film grain, głęboka winieta, DOF i motion blur domyślnie wyłączone w kamerze strategicznej. DOF dopuszczalne w osobnej kamerze portretowej/cinematic, nigdy do rozmywania interfejsu i jednostek w walce.

## V06. Ocean — optyka, ruch i interakcje

Bazą jest HDRP Water po potwierdzeniu kompatybilności. Zapisz w ADR: reprezentacja akwenu, sposób próbkowania wysokości/normalnej, obsługiwane decals/deformers/foam, głębokość i granice. To wymagania funkcjonalne, nie gwarancja dostępności konkretnych klas. Gdy API brakuje, przygotuj ograniczony adapter; nie uruchamiaj drugiego niezależnego oceanu tylko dla kilwateru bez uzasadnienia.

Trzy skale: długie falowanie ustala kołysanie, średnie fale nadają kształt światła, małe normalne tworzą detal refleksu. Ich amplitudy/częstotliwości i kierunek wynikają z jednego profilu pogody. Kuter nie może kołysać się na innej fazie fali niż widoczna tafla. Limity przechyłu i niezależność pozycji/kolizji podlegają [WM02](docs/KONTRAKTY_MORZA_I_WIATRU.md).

Absorpcję opisuj zależnością T(d)=exp(−σa·d), liczoną na kanałach; to model docelowy do strojenia, nie żądanie zastąpienia wbudowanego shaderu. Głębokość optyczna to droga światła, nie sama głębokość mapy nawigacyjnej. Refrakcję ograniczaj przy brzegu i krawędziach obiektów, aby nie ujawniać dna nad linią wody. Fresnel zwiększa udział odbicia pod kątem ślizgowym; nie dodawaj stałego białego refleksu niezależnego od widoku. Kaustykę ogranicz do płytkiej oświetlonej strefy, bez projekcji na dalekie statki i nocne dno bez źródła światła.

Odbicia: niebo/probes jako ciągła baza, SSR jako detal zależny od ekranu. Wyjście statku poza kadr nie powinno powodować ostrego zniknięcia całego blasku tafli. Planar reflection tylko dla wybranej spokojnej zatoki i po pomiarze kosztu dodatkowego renderu; nie twórz sondy planar dla każdego statku. Odbicia RT są częścią profilu Ultra, po potwierdzeniu w V00. Płomień transparentny może nie wystąpić w danej ścieżce odbić; dopuszczalny dyskretny, autorski efekt odbitego blasku związany ze światłem, ale nie duplikat płomienia renderowany przez wodę.

Kilwater: emisja zależy od prędkości względem wody i szerokości kadłuba, maleje płynnie po zatrzymaniu; zakręt zmienia ślad. Względna prędkość uwzględnia prąd — sama translacja statku z nurtem nie uzasadnia pełnej piany. Startowa długość życia piany 6–12 s; przy pauzie ślad zatrzymany razem z symulacją. Źródła przypisane do dziobu/rufy/wioseł, nie do środka obiektu bez uwzględnienia obrotu. Rozbryzgi przy taranie jednorazowo po zdarzeniu z identyfikatorem.

Piana to maska ograniczona i filtrowana, nie pełnoekranowy additive. Koszt i rozdzielczość pól ograniczane do obszaru obserwacji; na dalekim LOD zastąp ślad prostszym efektem. Okręt za mgłą wojny nie zdradza położenia kilwaterem, rozbryzgiem ani zmianą pola piany. Wyzerowanie rendererów jednostki bez usunięcia jej wkładu do pola jest błędem P1.

## V07. Przezroczystości, mgła i OIT

Zwykłe alpha blending jest zależne od kolejności; sortowanie całych obiektów nie rozwiązuje przecinających się cząsteczek. Additive ukrywa część problemu, ale nie nadaje się do ciemnego gęstego dymu. Depth prepass/postpass poprawia wybrane relacje głębi, lecz nie jest uniwersalnym OIT.

Etap bazowy: osobne systemy dymu, emisji, iskier i rozbryzgów; lit dym tam, gdzie ma reagować na światło; additive tylko dla energii/iskier; soft particles dla styku z geometrią; zasady sortowania i passów przed/po refrakcji zgodne z wersją HDRP. Żagle i liny jako opaque/alpha clip, gdzie to wystarcza, aby nie generować zbędnego overdraw. Zmierz quad overdraw z bliska, z daleka i podczas nakładania 20 pożarów.

Jeżeli błędy sortowania nadal blokują odbiór, porównaj prototyp weighted blended OIT z poprawnie posortowanym obrazem kontrolnym. W przybliżeniu przechowuj sumę C·α·w i α·w oraz iloczyn (1−α), a następnie resolve koloru i transmittance; użyj bezpiecznego mianownika ε. To przybliżenie nie odtwarza dowolnej refrakcji ani precyzyjnej kolejności kolorowych warstw. Dobór wag, pre-exposure, formatów RT i punktu wstrzyknięcia wymaga implementacji i testów.

Nie włączaj własnego OIT dla całego oceanu. Najpierw ogranicz go do konkretnej grupy cząsteczek z jasnym złożeniem z głębią, wodą i mgłą HDRP. Odrzuć rozwiązanie, jeśli podwójnie nakłada fog, tonemapping lub refrakcję. Własny pass musi mieć test zmiany rozdzielczości, stereo tylko jeśli wspierane, wielu kamer, pause/resume i odtworzenia zasobów. Nie przechodź na per-pixel linked lists/depth peeling bez dowodu potrzeby i budżetu pamięci.

Mgła środowiskowa jest wizualna i może korzystać z wolumetrii. Logiczna widoczność podlega planowi §8/§12 i [WM02](docs/KONTRAKTY_MORZA_I_WIATRU.md). Włączenie/wyłączenie wolumetrii nie daje przewagi informacyjnej. Efekt mgły magicznej posiada czytelną strefę zagrożenia na każdym profilu. Ostrzeżenia taktycznego nie wolno usuwać.

## V08. Skóra, włosy i tkaniny

SSS rozprasza światło w materiale; nie jest globalną poświatą ani zwykłą przezroczystością. Stosuj HDRP Lit z odpowiednim profilem dyfuzji/grubością, jeśli potwierdzono wsparcie. Jednostki w dalekim kadrze używają tańszego Lit z tym samym kolorem. Włączanie kosztownej skóry dla kilkuset nierozpoznawalnych twarzy nie jest wymaganiem.

Fizyczne cieniowanie włókien daje kierunkowy refleks wzdłuż włosów; wymaga poprawnych tangentów. Dla bohatera w portrecie dopuszczalne karty włosów i HDRP Hair po ocenie temporal aliasing. W dalekim LOD uproszczone kształty/alpha clip. Futro mitologicznego stworzenia tylko gdy rzeczywiście widać sylwetkę i światło. Dobierz technikę na podstawie widocznego efektu i kosztu. Zachowaj model energetyczny shaderu, nie składaj kilku pełnych lobów spekularnych bez normalizacji.

Żagiel wymaga kierunkowej struktury i kontrolowanego doświetlenia od tyłu, nie parametrów ludzkiej skóry ani włosów. Użyj Fabric albo prostszego Lit z maską transmisji, zależnie od pakietu i kosztu. Dwie strony materiału nie mogą dawać odwróconych normalnych lub podwójnej jasności. Test wschodzącego słońca za żaglem oraz nocy bez światła za nim. Łopoczący żagiel ma poprawny obrys cienia i bounding box.

## V09. Animacja, zdarzenia i stabilność w czasie

Rozkazy i trafienia w 30 Hz są interpolowane do renderowania. Wiosła synchronizuj z napędem, nie tylko prędkością po ziemi: statek płynący z prądem bez pracy obsady nie wiosłuje jak przy szarży. Kierunek wiatru deformuje żagiel, ale deformacja nie daje drugiego niezależnego przyspieszenia. Taran ma jedną sekwencję efektów i jedno rozliczenie obrażeń. Abordaż prezentuje haki, połączenie i walkę zgodnie ze stanem symulacji, nie uruchamia przejęcia eventem z animacji.

Zdarzenie efektu zawiera eventId, entityId, tick, pozycję/kierunek, typ, intensywność i politykę widoczności. Router nie wykonuje zdarzenia drugi raz po ponownym otrzymaniu tego samego ID. Po wczytaniu nie odtwarzaj wszystkich historycznych eksplozji; odbuduj trwały ogień i moknięcie. Pause zatrzymuje czas VFX związanych z rozgrywką. UI i kamera mogą nadal działać w czasie rzeczywistym.

Włącz TAA dopiero po poprawnych wektorach ruchu dla kadłuba i dostępnych deformacji. Kamera, kołysanie, żagle, wiosła i proceduralne przesunięcia wymagają aktualnej i poprzedniej pozycji odpowiedniej dla shaderu. Nie zakładaj, że sam komponent rigidbody dostarcza poprawne wektory dla deformacji wierzchołków. Przy braku wsparcia zmniejsz deformację dalekiego LOD lub zastosuj prostszy AA na danym profilu.

Testuj 300 klatek ruchu kamery: ghosting masztów, smugi za płomieniem, migotanie lin, refleksów i białej piany. Pojedynczy screenshot nie dowodzi stabilności. Sharpening nie jest naprawą błędnych motion vectors. Domyślnie gra renderuje natywnie, bez dynamicznej rozdzielczości i upscalera. Są to wyłącznie opcje dla gracza, po ustaleniu obrazu bazowego; porównanie natywnego 1080p z rekonstrukcją musi mieć identyczny seed, kadr i stan pogody.

## V10. Pięć profili obrazu bez wymagań sprzętowych

Gra nie ma wymagań sprzętowych, minimalnego GPU ani budżetu klatki ograniczającego jakość. Gracz wybiera profil Low, Medium, High, Extra High albo Ultra; zmiana działa bez restartu i jest zapisywana. Domyślnie wybrany jest Ultra. Ultra zawiera najwyższą jakość bez kompromisów. Gra nie obniża jakości automatycznie. Urządzenie bez wymaganej funkcji, na przykład DXR dla Ultra, otrzymuje jasny komunikat z propozycją Extra High, bez cichej zamiany.

Wartości są startowe i podlegają ocenie obrazu:

| Funkcja | Low | Medium | High | Extra High | Ultra |
|---|---|---|---|---|---|
| Światła lokalne / z cieniem | 8 / 2 | 16 / 4 | 32 / 8 | 48 / 12 | 64 / 16 |
| Cienie kierunkowe | 2 kaskady | 2 kaskady | 3 kaskady | 4 kaskady | 4 kaskady, cienie kontaktowe |
| Woda | czytelna, prosty detal | ograniczony detal i piana | pełny kilwater, średni detal | gęsty detal i piana | najgęstszy detal, pełne próbkowanie |
| Odbicia | niebo i sondy | niebo i sondy | SSR i sondy | SSR, sondy, wybrane planar | ray tracing, SSR i sondy jako fallback, wybrane planar |
| Oświetlenie pośrednie | sondy | sondy | sondy i SSAO | SSGI | SSGI (RTGI wyłączone, zob. oświetlenie pośrednie) |
| Mgła i VFX | prosta mgła, mniej cząsteczek, markery zachowane | niska wolumetria, markery zachowane | średnia wolumetria, markery zachowane | wysoka wolumetria, markery zachowane | pełna wolumetria, najwyższa rozdzielczość, markery zachowane |
| Bohaterowie | Lit | Lit | SSS w zbliżeniach | SSS i Hair w zbliżeniach | SSS i Hair w zbliżeniach, najwyższa jakość |
| Rendering | natywny | natywny | natywny | natywny | natywny |

Upscaler jest na każdym profilu wyłącznie opcją gracza. Pomiary mają charakter informacyjny i nie są warunkiem odbioru jakości obrazu. Przy 100/200/400 statkach zapisuj dla każdego profilu draw calls, triangles, particles, lokalne światła, czas CPU/GPU, peak VRAM oraz GC allocations, z podaniem sprzętu. Optymalizacja usuwa marnotrawstwo, a Ultra nie traci przez nią jakości.

Niezależnie od profilu i sprzętu nigdy nie zmieniaj logicznej mgły wojny, czasów zaklęć, hitboxów, sektora broni i wykrywania przeszkód. Różnice między profilami nie mogą dawać przewagi informacyjnej ani ukrywać ostrzeżeń taktycznych. Czytelność z V00, deterministyczna symulacja, brak przecieków mgły wojny w V11 i brak wycieków zasobów obowiązują na każdym profilu. Przełączanie profili nie może tworzyć materiałów i RT bez zwalniania; sprawdź 20 cykli zmiany przez wszystkie profile.

## V11. Walidacja obrazu i dowody

Utwórz sceny Visual_MaterialLab, Visual_Port_DayNight, Visual_Ocean_Coast, Visual_Battle_400 i Visual_TransparencyStress. Mają działać w buildzie Windows, bez zależności od Editor. Zrzuty i pomiary są artefaktami, a sceny testowe nie zastępują misji gameplay.

Kontrolowane wejścia: seed, czas symulacji, pogoda, pozycja/orientacja/projekcja kamery, rozdzielczość, profil, ekspozycja, wersje pakietów i shaderów, GPU/sterownik, commit/snapshot. Ustabilizuj TAA przez 64 klatki po ustawieniu sceny; zapisuj też sekwencję ruchu, nie tylko ostatnią klatkę. Przy zamrożonej logice pozwól wymaganym temporalnym buforom osiągnąć stabilny stan.

Dowody minimalne dla zmiany renderingu: PNG bezstratny, sekwencja ruchu 10 s lub zestaw klatek, Frame Debugger z opisem istotnych passów, GPU capture/profiler, raport liczb i review czytelności. Jeśli używasz RenderDoc, najpierw sprawdź zgodność API i narzędzia, nie deklaruj capture bez pliku. Ciężkie nagrania trzymaj poza repo, ze wskazaniem artefaktu i sumą kontrolną.

Automatyczna różnica obrazu: najpierw porównaj dwukrotne wykonanie tej samej wersji, by poznać niedeterminizm. Maskuj HUD/czas rzeczywisty tylko według jawnej maski. Progi różnicy ustal po zebraniu baz; nie wymyślaj uniwersalnego SSIM=0,99 dla wszystkich GPU. Wynik poza tolerancją → FAIL lub INCONCLUSIVE z dowodami, nie automatyczne nadpisanie baseline. Nowy baseline wymaga review innej osoby niż autor zmiany shaderu.

Test krytyczny informacji: dwie sceny identyczne, poza ukrytym wrogiem. W kontrolowanej sekwencji z ustalonym RNG jego obecność nie może zmieniać widocznych odbić, cieni, piany, świateł ani magicznych znaczników. Uwzględnij sondy aktualizowane dynamicznie i VFX GPU. Pojawienie się odkrytego wroga ma działać w obu profilach bez opóźnionej starej mgły. Materiał czarny na niewidocznym wrogu nie wystarczy, bo może nadal pisać głębię/cień.

Wymagane testy: poprawne przeliczenie roughness/smoothness; brak klonowania materiałów; jednorazowe VFX dla eventId; odbudowa ognia po save/load; pause/resume; maskowanie jednostek i emisji; brak wycieku RT przy zmianie jakości; start sceny bez missing shader/pink materials. Testy GPU wymagają rzeczywistego urządzenia i kontekstu graficznego. Gdy host ich nie zapewnia, zapisuj NOT_RUN/BLOCKED, nigdy PASS z samego EditMode.

Istniejący hook nadal wymaga EditMode/PlayMode i nie potrafi sam ocenić filmów ani pomiarów GPU. Dla zadania wizualnego plan ma zawierać konkretne expectedTests obu zestawów, a niezależny reviewer dodatkowo sprawdza materiały powyżej. Wydanie wymaga zewnętrznego CI i ręcznego odbioru obrazu. Bez adaptera automatyzującego uruchomienie builda nie deklaruj, że hook egzekwuje pełny visual QA.

## V12. Kolejność prac Claude Code

1. Sprawdź zgodność i utwórz ADR render path. Zbuduj pustą scenę HDRP na Windows i zapisz wynik. Brak środowiska → praca nad asset pipeline/dokumentacją, bez pozorowanych pomiarów.
2. Uruchom MaterialLab i porównaj wzorce suche/mokre w trzech ustawieniach światła. Ustal ekspozycję i neutralny punkt odniesienia.
3. Dodaj jeden kompletny statek, port i ocean: linia wodna, odbicia, cień, zanurzenie, kilwater. Dopiero potem masowe powielanie.
4. Połącz ogień, taran i magię z routerem zdarzeń, pulami oraz polityką widoczności. Testuj nakładające się efekty.
5. Dodaj profile biomów/pogody i sekwencje dzień–noc. Sprawdź szare skale, rozpoznawalność frakcji bez koloru i czytelność przy najmniejszym zoomie.
6. Profiluj 100/200/400 statków. Zapisz koszty, popraw najdroższy pass, porównaj klip przed/po. Po spełnieniu P0 wdróż SSS/Hair, planar i OIT z profilu Ultra.
7. Każdy etap odbierają QA i reviewer na dowodach; aktualizuj macierz V-ID → implementacja → test → artefakt → status. Wykonaj testy hooka po zmianie jego zakresu snapshotu.
8. Wprowadź przełączanie rozdzielczości V13 już przy pierwszej scenie HDRP, aby interfejs, bufory i pomiary od początku działały w całym zakresie.

## V13. Zakres rozdzielczości 1080p–1440p

Gra oferuje pełną gamę rozdzielczości od 1920×1080 do 2560×1440. Menu ustawień zawiera co najmniej tryby 16:9 1920×1080, 2048×1152, 2304×1296 i 2560×1440, gdy monitor je obsługuje, oraz wszystkie pozostałe tryby zgłaszane przez monitor o szerokości 1920–2560 i wysokości 1080–1440. Tryby poza tym zakresem nie są obowiązkowe. Lista nie pokazuje trybu, którego wybrane urządzenie nie obsługuje, a tryb inny niż 16:9 nie może rozciągać obrazu.

Dostępne są co najmniej pełny ekran i okno. Zmiana działa bez restartu gry; po zmianie gra pokazuje potwierdzenie i po 15 s bez reakcji wraca do poprzedniego trybu. Wybór jest zapisywany. Gdy zapisany tryb nie jest już dostępny, na przykład po odłączeniu monitora, gra przechodzi na najbliższy obsługiwany tryb i informuje o tym gracza. Pierwsze uruchomienie wybiera najwyższy obsługiwany tryb z zakresu.

Rozdzielczość jest wyłącznie ustawieniem prezentacji. Nie zmienia symulacji, zasięgu widzenia, mgły wojny, sektorów broni, hitboxów ani wyniku zapisu. Wyższa rozdzielczość lub szerszy obraz nie może odsłaniać jednostek ani dawać przewagi informacyjnej ponad jawnie określone pole kamery. Zaznaczanie ramką, rozkazy, przewijanie krawędzią ekranu i minimapa używają bieżących współrzędnych ekranu i działają jednakowo w każdym trybie.

HUD skaluje się względem bazowego 1920×1080 i łączy się z ustawieniem interfejsu 80–150% z C10. W skrajnych kombinacjach żaden element nie wychodzi poza ekran, a tekst i ikony pozostają czytelne. Czytelność klas, kierunku kadłuba i stanu uszkodzeń z V00 odbiera się przy obu krańcach zakresu i najmniejszym zoomie. Bufory, pola piany, mgła i własne passy są odtwarzane przy zmianie rozdzielczości bez wycieków; 20 cykli przełączania wchodzi w test z V10.

Docelowa dostępność rozdzielczości i profili z V10 wymaga prób na wskazanej konfiguracji. Wynik FPS/czasu klatki obowiązuje dla konkretnego pomiaru; brak pomiaru oznacza NOT_RUN. Zachować uzgodnioną jakość i brak cichego obniżania profilu. Skalowanie obrazu jest opcją gracza; wynik natywny i rekonstruowany raportuje się osobno, z identycznym seedem, kadrem i pogodą zgodnie z V09. Nie ekstrapoluj wyniku z 1080p na 1440p.

Informacyjne pomiary 100/200/400 statków z V10 wykonuje się w 1920×1080 i 2560×1440, z zapisem szczytowego VRAM, GPU, sterownika i faktycznego trybu wyświetlania. Działanie trybu 1440p potwierdza się na rzeczywistym wyjściu obrazu w tej rozdzielczości albo przez jawnie opisane renderowanie ponad rozdzielczość monitora; drugi wariant nie potwierdza wyświetlania na fizycznym panelu 1440p. Brak takiego sprzętu oznacza BLOCKED lub NOT_RUN, nigdy PASS.
