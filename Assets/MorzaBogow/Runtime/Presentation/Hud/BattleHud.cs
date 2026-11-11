using System;
using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation.Hud;
using MorzaBogow.Settings;
using MorzaBogow.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The battle interface after the interface package (Dokumentacja/INTERFEJS.md, ui-contract.json): a 44 px top
    /// bar, a 194 px bottom band with chart, selection, two production lines and a 4×3 order grid, the herald on the
    /// right and the port selector over the sea. UI Toolkit on a 1920×1080 reference, scaled evenly and by the
    /// player's 90–125%. It reads the battle controller and issues orders through it; it never changes the game.
    /// Text is formatted only when a shown value changes; lists come from fixed pools.
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        const float TextRefresh = 0.125f, ChartRefresh = 0.25f, CursorRefresh = 0.1f;
        const float TooltipDelay = 0.4f, HeraldInformational = 8f, HeraldMerge = 6f, FlashSeconds = 0.4f;
        const int MaxHeralds = 4, MaxCards = 5, MaxQueueShown = 4;

        BattleController battle;
        UIDocument document;
        PanelSettings panelSettings;
        VisualElement root;
        float textTimer, chartTimer, cursorTimer;

        // Options (saved)
        float userScale = 1f;
        bool highContrast, patterns, reducedMotion, collapsed;

        // ---------- Public surface for the controller and tests ----------

        public VisualElement Root => root;
        public int VisibleHealthBars { get; private set; }
        public string WoodText => woodValue?.text ?? string.Empty;
        public int VisibleCommands => commands.Count(command => command.Button.style.display != DisplayStyle.None);
        public int VisibleHeralds => heralds.Count;
        /// <summary>Title and subtitle of every herald entry shown, newest first (tests).</summary>
        public IReadOnlyList<string> HeraldLines => heralds.Select(item => $"{item.Title?.text}|{item.Sub?.text}").ToList();
        public bool Collapsed => collapsed;
        public bool ModalOpen => modal != null;
        public bool PopoverOpen => popover != null;

        /// <summary>True while a dialog or a key capture holds the input; the world and camera then ignore it.</summary>
        public bool HoldsInput => modal != null || KeyBindings.Capturing;

        double updateMs;
        int updates;
        public double AverageUpdateMs => updates > 0 ? updateMs / updates : 0;

        // ---------- Elements ----------

        Label woodValue, woodCap, silverValue, silverCap, transitValue, fleetValue, fleetLimit, portsValue, timeValue, speedValue, portName;
        VisualElement woodButton, silverButton, pauseGlyph, topBar, bottomBand, portSelector, groupsBar, restoreButton, heraldColumn;
        VisualElement commandHint, commandHintGlyph;
        Label commandHintTitle, commandHintText;
        HudMinimap minimap;
        Label selectionTitle, selectionNote;
        VisualElement clearSelectionButton;
        VisualElement emptyView, shipView, groupView, portView, wreckView;
        VisualElement shipPortrait, shipBadge, shipCargoRow, moraleGlyph, shipBoardingRow;
        Label shipName, shipTag, shipHull, shipSailors, shipSoldiers, shipJavelins, shipArchers, shipWounded, shipMorale, shipCargo, shipPhase, shipBoarding;
        MeterBar shipHullMeter, shipCargoMeter, shipBoardingOurs, shipBoardingTheirs;
        Button shipCrewButton, shipConvoyButton;
        int shipShownId = -1;
        readonly List<(Button card, VisualElement portrait, Label name, MeterBar meter)> unitCards = new List<(Button, VisualElement, Label, MeterBar)>();
        Button moreCard;
        Label moreLabel;
        readonly List<(VisualElement row, VisualElement glyph, Label text)> classCounts = new List<(VisualElement, VisualElement, Label)>();
        Label groupHull, groupCrew;
        VisualElement portPortrait;
        Label portWoodValue, portSilverValue, portBerths, portYard, portHullValue;
        MeterBar portHullMeter, portWoodMeter, portSilverMeter;
        VisualElement portStores;
        Label wreckCargo, wreckTime;
        QueueRow warRow, portRow;
        readonly List<CommandButton> commands = new List<CommandButton>();
        Label commandsEmpty;
        Button formationButton;
        VisualElement worldLayer, selectBox, popoverLayer, modalLayer, tooltip;
        Label tipTitle, tipBody, tipCost, tipKey;
        readonly List<(VisualElement root, MeterBar hull, MeterBar crew)> bars = new List<(VisualElement, MeterBar, MeterBar)>();

        // ---------- State ----------

        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        readonly Dictionary<Label, float> flashing = new Dictionary<Label, float>();
        long lastWood = long.MinValue, lastSilver = long.MinValue;
        VisualElement tooltipOwner;
        Func<TipInfo> tooltipContent;
        IVisualElementScheduledItem tooltipTimer;
        VisualElement popover, modal;
        Action modalConfirm;
        bool pausedBeforeMenu, menuPaused;
        string lastCursor;
        GraphicsProfileController profiles;
        DisplaySettingsController display;
        string lastProfileNotice = string.Empty, lastDisplayNotice = string.Empty;
        GameAction? capturing;
        string settingsTab = "Obraz";

        sealed class Herald
        {
            public VisualElement Element;
            public Label Title, Sub, Key;
            public HeraldMessage Message;
            public float Until, Born;
            public int Count = 1;
            public bool Acknowledged;
            /// <summary>What a merged alarm is about: the texts and the ships or ports it gathers.</summary>
            public readonly HashSet<string> Texts = new HashSet<string>();
            public readonly HashSet<int> Entities = new HashSet<int>();
        }

        readonly List<Herald> heralds = new List<Herald>();

        struct TipInfo
        {
            public string Title, Body, Cost, Key;
        }

        sealed class QueueRow
        {
            public bool War;
            public VisualElement Root, Items, Glyph;
            public Label Empty;
            public Button Add;
            public readonly List<(Button item, VisualElement glyph, VisualElement stage, Label time, MeterBar bar)> Slots = new List<(Button, VisualElement, VisualElement, Label, MeterBar)>();
        }

        sealed class CommandButton
        {
            public GameAction Action;
            public Button Button;
            public Label Hotkey;
            public HudOrder Targeted;
            public Action<BattleHud> Instant;
            public Func<Ship, bool> Able;
            public string Icon, Label, Full, Body;
        }

        // ---------- Creation ----------

        public static BattleHud Create(BattleController controller)
        {
            var host = new GameObject("Battle interface");
            host.transform.SetParent(controller.transform, false);
            host.SetActive(false);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "Battle interface panel";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f;
            settings.sortingOrder = 10;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MorzaBogowTheme");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var hud = host.AddComponent<BattleHud>();
            hud.battle = controller;
            hud.document = document;
            hud.panelSettings = settings;
            host.SetActive(true);
            hud.Build();
            return hud;
        }

        void Build()
        {
            LoadOptions();
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            var sheet = Resources.Load<StyleSheet>("MorzaBogowHud");
            if (sheet != null) root.styleSheets.Add(sheet);

            // Fonts come from the style sheet (Source Sans 3 for data, Cinzel SemiBold for short headings), so a
            // heading's own font is never overridden by one set on the root.
            var hud = Element("hud-root", root);
            hud.pickingMode = PickingMode.Ignore;

            worldLayer = Element("world-layer", hud);
            worldLayer.pickingMode = PickingMode.Ignore;
            selectBox = Element("select-box", worldLayer);
            selectBox.pickingMode = PickingMode.Ignore;
            Show(selectBox, false);

            BuildTopBar(hud);
            BuildOverMap(hud);
            BuildBottomBand(hud);

            popoverLayer = Element("popover-layer", hud);
            popoverLayer.pickingMode = PickingMode.Ignore;
            modalLayer = Element("modal-layer", hud);
            modalLayer.pickingMode = PickingMode.Ignore;
            BuildTooltip(hud);

            // A click outside an open popover closes it, and does not reach the sea.
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (popover == null) return;
                if (evt.target is VisualElement target && (popover.Contains(target) || target == popover)) return;
                ClosePopover();
            }, TrickleDown.TrickleDown);

            profiles = FindAnyObjectByType<GraphicsProfileController>();
            display = FindAnyObjectByType<DisplaySettingsController>();
            if (profiles != null) profiles.DrawImguiPanel = false;
            if (display != null) display.DrawImguiPanel = false;

            ApplyOptions();
            battle.Herald += OnHerald;
            OnMatchStarted();

            // Measurement switch: the same scene with the interface hidden, to price the interface on the GPU.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-morzaNoHud") >= 0) root.style.display = DisplayStyle.None;
        }

        void OnDestroy()
        {
            if (battle != null) battle.Herald -= OnHerald;
            UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        public void OnMatchStarted()
        {
            if (minimap == null) return;
            minimap.Bind(battle);
            foreach (var herald in heralds) herald.Element.RemoveFromHierarchy();
            heralds.Clear();
            CloseModal();
            ClosePopover();
            lastWood = lastSilver = long.MinValue;
            // A key shared by two actions fires both; the player hears of it at the start, not only in the settings
            // (a key bound earlier may now share G with the group formation).
            var clashes = KeyBindings.AllConflicts();
            if (clashes.Count > 0)
                OnHerald(new HeraldMessage("Jeden klawisz ma kilka czynności: " + string.Join("; ", clashes.Select(pair => $"{KeyBindings.Label(KeyBindings.Key(pair.Item1))} – {KeyBindings.Name(pair.Item1)} i {KeyBindings.Name(pair.Item2)}"))
                    + ". Zmień w Ustawieniach → Sterowanie.", HeraldLevel.Important, null, HeraldTopic.General));
            Refresh();
        }

        // ---------- Layout ----------

        void BuildTopBar(VisualElement parent)
        {
            topBar = Element("topbar", parent);
            Element("topbar-ornament", topBar).pickingMode = PickingMode.Ignore;

            var mark = Element("wordmark", topBar);
            mark.pickingMode = PickingMode.Ignore;
            Glyph(mark, "trireme", "glyph");
            Text("Morza Bogów", "title wordmark-label", mark);

            woodButton = Resource(topBar, "wood", "Drewno", out woodValue, out woodCap, () => OpenStore(ResourceType.Wood, woodButton));
            silverButton = Resource(topBar, "silver", "Srebro", out silverValue, out silverCap, () => OpenStore(ResourceType.Silver, silverButton));
            var transit = new Button { focusable = true };
            transit.AddToClassList("res");
            topBar.Add(transit);
            Glyph(transit, "cargo", "glyph");
            transitValue = Text("W drodze", "res-label", transit);
            Tip(transit, () => new TipInfo { Title = "Ładunek w drodze", Body = "Drewno i srebro na własnych statkach: w ładowniach kutrów i handlowców. Nie liczy się jednocześnie do magazynu." });

            Element("spacer", topBar).pickingMode = PickingMode.Ignore;

            var fleet = Stat(topBar, "fleet", out fleetValue, out fleetLimit);
            Tip(fleet, () => new TipInfo { Title = "Flota", Body = $"Własne statki i limit floty z zasad gry ({Rules.FleetLimit})." });
            var ports = Stat(topBar, "port", out portsValue, out _);
            Tip(ports, () => new TipInfo { Title = "Porty", Body = "Port macierzysty i przyczółki. Każdy ma własny magazyn i kolejki." });
            var time = Stat(topBar, "time", out timeValue, out _);
            Tip(time, () => new TipInfo { Title = "Czas potyczki", Body = "Czas gry od początku bitwy." });

            Divider(topBar);
            IconButton(topBar, "minus", () => battle.SpeedDown(), () => new TipInfo { Title = "Wolniej", Body = "Zmniejsza tempo gry.", Key = Keys(GameAction.Slower) });
            speedValue = Text("×1", "speed-label", topBar);
            IconButton(topBar, "plus", () => battle.SpeedUp(), () => new TipInfo { Title = "Szybciej", Body = "Zwiększa tempo gry.", Key = Keys(GameAction.Faster) });
            var pause = IconButton(topBar, "pause", () => battle.TogglePause(), () => new TipInfo { Title = battle.Paused ? "Wznów" : "Pauza", Body = "Wstrzymuje potyczkę; rozkazy można wydawać także w pauzie.", Key = Keys(GameAction.Pause) });
            pauseGlyph = pause.Q(className: "top-button-glyph");
            IconButton(topBar, "menu", OpenMenu, () => new TipInfo { Title = "Menu", Body = "Pauza, zapis, wczytanie i ustawienia.", Key = "Esc" });
        }

        VisualElement Resource(VisualElement bar, string icon, string label, out Label value, out Label cap, Action click)
        {
            var button = new Button(click) { focusable = true };
            button.AddToClassList("res");
            bar.Add(button);
            Glyph(button, icon, "glyph");
            Text(label, "res-label", button);
            value = Text("—", "res-value", button);
            cap = Text(string.Empty, "res-cap", button);
            var type = icon == "wood" ? ResourceType.Wood : ResourceType.Silver;
            Tip(button, () => StoreTip(type));
            return button;
        }

        VisualElement Stat(VisualElement bar, string icon, out Label value, out Label small)
        {
            var stat = Element("top-stat", bar);
            Glyph(stat, icon, "glyph");
            value = Text(string.Empty, "top-stat-value", stat);
            small = Text(string.Empty, "top-stat-small", stat);
            return stat;
        }

        Button IconButton(VisualElement bar, string icon, Action click, Func<TipInfo> tip)
        {
            var button = new Button(click) { focusable = true };
            button.AddToClassList("top-button");
            bar.Add(button);
            var glyph = Element("top-button-glyph", button);
            glyph.pickingMode = PickingMode.Ignore;
            glyph.style.backgroundImage = Icon(icon);
            Tip(button, tip);
            return button;
        }

        void BuildOverMap(VisualElement parent)
        {
            portSelector = new Button(OpenPortMenu) { focusable = true };
            portSelector.AddToClassList("port-selector");
            parent.Add(portSelector);
            Glyph(portSelector, "port", "glyph-small");
            portName = Text("Port macierzysty", "port-name", portSelector);
            Glyph(portSelector, "down", "glyph-small");
            Tip(portSelector, () => new TipInfo { Title = "Wybrany port", Body = "Z jego magazynu płacisz za zamówienia i jego kolejki widzisz w stoczni. Zmiana portu nie przenosi towaru." });

            groupsBar = Element("groups", parent);
            groupsBar.pickingMode = PickingMode.Ignore;

            commandHint = Element("command-hint", parent);
            commandHint.pickingMode = PickingMode.Ignore;
            commandHintGlyph = Glyph(commandHint, "move", "glyph");
            commandHintTitle = Text(string.Empty, "command-hint-title", commandHint);
            commandHintText = Text(string.Empty, "command-hint-text", commandHint);
            Show(commandHint, false);

            heraldColumn = Element("herald", parent);
            heraldColumn.pickingMode = PickingMode.Ignore;

            restoreButton = new Button(ToggleCollapse) { focusable = true };
            restoreButton.AddToClassList("restore-button");
            parent.Add(restoreButton);
            Glyph(restoreButton, "down", "glyph-small").style.rotate = new StyleRotate(new Rotate(Angle.Degrees(180f)));
            Text("Pokaż panele", "restore-label", restoreButton);
            Tip(restoreButton, () => new TipInfo { Title = "Pokaż panele", Body = "Przywraca dolny pas dowodzenia.", Key = Keys(GameAction.CollapseHud) });
            Show(restoreButton, false);
        }

        void BuildBottomBand(VisualElement parent)
        {
            bottomBand = Element("bottom-band", parent);
            Element("band-ornament", bottomBand).pickingMode = PickingMode.Ignore;

            // Chart
            var map = Element("section minimap-section", bottomBand);
            var mapHeading = Element("section-heading", map);
            Text("Mapa morska", "section-title", mapHeading);
            Element("spacer", mapHeading).pickingMode = PickingMode.Ignore;
            SmallButton(mapHeading, "target", FocusFleet, () => new TipInfo { Title = "Do floty", Body = "Kamera na zaznaczone statki, a bez zaznaczenia na wybrany port." });
            minimap = new HudMinimap();
            map.Add(minimap);
            Tip(minimap, () => new TipInfo { Title = "Mapa morska", Body = "Ląd, mgła, porty, złoża i widoczne statki. Ramka pokazuje widok kamery.", Key = "LPM: przesuń widok · PPM: ruch zaznaczonych" });

            // Selection
            var selection = Element("section selection-section", bottomBand);
            var heading = Element("section-heading", selection);
            selectionTitle = Text("Dowodzenie flotą", "section-title", heading);
            selectionNote = Text(string.Empty, "section-note", heading);
            Element("spacer", heading).pickingMode = PickingMode.Ignore;
            clearSelectionButton = SmallButton(heading, "close", () => { battle.ClearSelectionKeepPort(); Refresh(); }, () => new TipInfo { Title = "Odznacz", Body = "Czyści zaznaczenie.", Key = "Esc" });
            var content = Element("selection-content", selection);
            BuildEmpty(content);
            BuildShip(content);
            BuildGroup(content);
            BuildPort(content);
            BuildWreck(content);

            // Production
            var production = Element("section production-section", bottomBand);
            var prodHeading = Element("section-heading", production);
            Text("Budowa i wodowanie", "section-title", prodHeading);
            Element("spacer", prodHeading).pickingMode = PickingMode.Ignore;
            SmallButton(prodHeading, "plus", () => OpenProduction(null, prodHeading), () => new TipInfo { Title = "Zamów statek", Body = "Szablony okrętów z ceną kompletną i czasem budowy, płatne z magazynu wybranego portu." });
            warRow = QueueRowOf(production, true);
            portRow = QueueRowOf(production, false);

            // Orders
            var orders = Element("section commands-section", bottomBand);
            var ordersHeading = Element("section-heading", orders);
            Text("Rozkazy", "section-title", ordersHeading);
            Element("spacer", ordersHeading).pickingMode = PickingMode.Ignore;
            // The formation switch shows only for a group: a single ship always goes to the point itself.
            formationButton = new Button(() => { battle.ToggleGroupFormation(); Refresh(); }) { focusable = true };
            formationButton.AddToClassList("card-button");
            ordersHeading.Add(formationButton);
            Tip(formationButton, FormationTip);
            SmallButton(ordersHeading, "help", null, HelpTip);
            var grid = Element("command-grid", orders);
            foreach (var definition in CommandDefinitions()) commands.Add(MakeCommand(grid, definition));
            commandsEmpty = Text("Zaznacz okręt, aby wydać rozkaz. PPM na mapie wybiera czynność według celu.", "commands-empty", orders);
        }

        VisualElement SmallButton(VisualElement parent, string icon, Action click, Func<TipInfo> tip)
        {
            var button = new Button(() => click?.Invoke()) { focusable = true };
            button.AddToClassList("small-button");
            parent.Add(button);
            var glyph = Element("small-button-glyph", button);
            glyph.pickingMode = PickingMode.Ignore;
            glyph.style.backgroundImage = Icon(icon);
            Tip(button, tip);
            return button;
        }

        void BuildEmpty(VisualElement content)
        {
            emptyView = Element("card", content);
            Text("Zaznacz okręt albo grupę floty.", "body", emptyView);
            Text("LPM wybiera, ramka zaznacza grupę, Shift dodaje. PPM wydaje rozkaz według celu: wróg – atak, złoże – zbiór, wolny brzeg z handlowcem – przyczółek, własny port – dostawa.", "help-line", emptyView);
            Text("Ctrl+1–9 zapamiętuje grupę, 1–9 ją wybiera. Kamera: strzałki, kółko, przecinek i kropka.", "help-line", emptyView);
        }

        void BuildShip(VisualElement content)
        {
            shipView = Element("selection-content", content);
            shipPortrait = Element("portrait", shipView);
            shipBadge = Element("portrait-badge", shipPortrait);
            var card = Element("card", shipView);
            var head = Element("card-row", card);
            head.style.marginTop = 0;
            shipName = Text(string.Empty, "card-name", head);
            shipTag = Text(string.Empty, "card-tag", head);

            var hull = Element("card-row", card);
            Glyph(hull, "shield", "glyph-small");
            Text("Kadłub", "small", hull).style.marginLeft = 6;
            shipHullMeter = new MeterBar(HudPalette.Healthy, 10);
            shipHullMeter.AddToClassList("card-meter");
            hull.Add(shipHullMeter);
            shipHull = Text(string.Empty, "card-value", hull);
            Tip(hull, () => new TipInfo { Title = "Kadłub", Body = "Wytrzymałość kadłuba. Przy zerze statek tonie i zostawia wrak. Kadłub naprawia tylko dok (ulepszona stocznia Miasta), za srebro.", Key = Keys(GameAction.Repair) + ": remont" });

            // The crew by trade, each against the plan its port trains it to (docs/ZALOGA_I_ABORDAZ.md).
            var crew = Element("card-row", card);
            shipSailors = CardStat(crew, "sailor", "żeglarze", () => new TipInfo { Title = "Żeglarze", Body = "Sterują, wiosłują i zbierają surowce. Mniej niż czwarta część załogi spowalnia okręt; nadmiar wioseł (skład Żegluga) dodaje 8% prędkości. Bronią się wręcz tylko, gdy na pokładzie nie zostało wojsko." });
            shipSoldiers = CardStat(crew, "soldier", "szermierze", () => new TipInfo { Title = "Szermierze", Body = "Walczą wręcz: niosą abordaż na wrogi pokład, odpierają go na własnym i przecinają liny abordażowe przeciwnika." });
            shipJavelins = CardStat(crew, "attack", "oszczepnicy", () => new TipInfo { Title = "Oszczepnicy", Body = "Rzucają oszczepy na 5–45 m z banków po czterech i dobrze walczą w zwarciu – między szermierzem a łucznikiem." });
            shipArchers = CardStat(crew, "volley", "łucznicy", () => new TipInfo { Title = "Łucznicy", Body = "Ostrzeliwują załogi okrętów w zasięgu 15–100 m – każdy łucznik strzela. W abordażu wspierają z dystansu, a własnego pokładu bronią lepiej niż cudzego." });
            var crewMore = Element("card-row", card);
            shipWounded = CardStat(crewMore, "warning", "ranni", () => new TipInfo { Title = "Ranni", Body = "Dwóch na pięciu poległych to ranni: nie walczą ani nie wiosłują. Port leczy ich za 0,25 S i przywraca do służby." });
            var morale = Element("card-stat", crewMore);
            moraleGlyph = Glyph(morale, "morale-high", "glyph-small");
            shipMorale = Text(string.Empty, "card-stat-value", morale);
            Text("morale", "card-stat-label", morale);
            Tip(morale, () => new TipInfo { Title = "Morale", Body = "Duch załogi; niskie osłabia w walce wręcz. Poniżej 20% załoga poddaje okręt abordującemu. Ikona i liczba – nie tylko kolor." });
            shipCrewButton = new Button(OpenCrewPlan) { text = "Skład", focusable = true };
            shipCrewButton.AddToClassList("card-button");
            crewMore.Add(shipCrewButton);
            shipWeaponsButton = new Button(OpenWeapons) { text = "Broń", focusable = true };
            shipWeaponsButton.AddToClassList("card-button");
            shipWeaponsButton.style.marginLeft = 6;
            crewMore.Add(shipWeaponsButton);
            Tip(shipWeaponsButton, () => WeaponsSummaryTip());
            // E03: a merchant goes on convoy duty, or comes off it, with one click (World.Convoy).
            shipConvoyButton = new Button(() => { battle.ToggleConvoySelected(); Refresh(); }) { text = "Konwój", focusable = true };
            shipConvoyButton.AddToClassList("card-button");
            shipConvoyButton.style.marginLeft = 6;
            crewMore.Add(shipConvoyButton);
            Tip(shipConvoyButton, () => new TipInfo { Title = "Konwój", Body = "Handlowiec sam wozi nadwyżki innych portów (ponad ich minimum) do portu priorytetowego, aż ten ma cenę najdroższego okrętu, jaki może zbudować; potem czeka. Do trzech handlowców ładuje się w jednym porcie i płynie razem. Port priorytetowy i minimum ustawia się na karcie portu (Konwoje). Ponowne kliknięcie zdejmuje z konwoju." });
            Tip(shipCrewButton, () => new TipInfo { Title = "Skład załogi", Body = "Wybierz, do jakiego składu port wyszkoli załogę. Zmiana następuje przy przegrupowaniu w porcie ze stocznią: okręt bez rozkazu w pobliżu portu sam wchodzi na stanowisko, leczy rannych i uzupełnia ludzi; kadłub i moduły broni naprawia tylko dok, za srebro.", Key = Keys(GameAction.Repair) + ": do portu teraz" });

            shipBoardingRow = Element("card-row", card);
            Glyph(shipBoardingRow, "board", "glyph-small");
            shipBoarding = Text(string.Empty, "small", shipBoardingRow);
            shipBoarding.style.marginLeft = 6;
            shipBoardingOurs = new MeterBar(HudPalette.Ally, 6);
            shipBoardingOurs.AddToClassList("card-meter");
            shipBoardingRow.Add(shipBoardingOurs);
            shipBoardingTheirs = new MeterBar(HudPalette.Enemy, 6);
            shipBoardingTheirs.AddToClassList("card-meter");
            shipBoardingRow.Add(shipBoardingTheirs);
            Tip(shipBoardingRow, () => new TipInfo { Title = "Abordaż", Body = "Najpierw haki (3 s) – szermierze obrońcy mogą przeciąć liny. Potem walka na burtach: walczy tylko tylu ludzi, ilu zmieści się przy relingu, a łucznicy wspierają z dystansu. Paski: siła twojej strony i przeciwnika w ostatniej rundzie." });

            shipCargoRow = Element("card-row", card);
            Glyph(shipCargoRow, "cargo", "glyph-small");
            Text("Ładownia", "small", shipCargoRow).style.marginLeft = 6;
            shipCargoMeter = new MeterBar(HudPalette.Timber, 6);
            shipCargoMeter.AddToClassList("card-meter");
            shipCargoRow.Add(shipCargoMeter);
            shipCargo = Text(string.Empty, "card-value", shipCargoRow);

            shipPhase = Text(string.Empty, "card-phase", card);
        }

        Button shipWeaponsButton;
        int weaponOptions;

        static string SlotName(SlotSize size) => size == SlotSize.Light ? "lekkie" : size == SlotSize.Medium ? "średnie" : "ciężkie";

        /// <summary>The ship's posts: what each carries now, what is planned, and its module and load.</summary>
        TipInfo WeaponsSummaryTip()
        {
            var ship = battle.World.FindShip(shipShownId);
            if (ship == null) return default;
            var lines = new List<string>();
            var manned = World.Manned(ship);
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                var now = WeaponCatalog.Get(ship.Mounts[i]).Name;
                var plan = ship.Loadout.Length > i && ship.Loadout[i] != ship.Mounts[i] ? $" → {WeaponCatalog.Get(ship.Loadout[i]).Name}" : string.Empty;
                var state = ship.Mounts[i] == WeaponType.None ? string.Empty
                    : ship.ModuleHp[i] <= 0 ? " (zniszczone)" : !manned[i] ? " (bez obsługi)" : ship.Progress[i] >= 1 ? " (gotowe)" : $" (ładuje {Mathf.FloorToInt((float)ship.Progress[i] * 100f)}%)";
                lines.Add($"{i + 1}. {SlotName(ship.Definition.Slots[i])}: {now}{state}{plan}");
            }
            return new TipInfo { Title = "Uzbrojenie", Body = lines.Count > 0 ? string.Join("\n", lines) : "Brak gniazd broni.", Key = "Klik: zmień plan; wymiana przy przegrupowaniu w porcie" };
        }

        /// <summary>
        /// For each post the weapons that fit it, with the figures to compare and how many lie in the active port's
        /// arsenal; the click sets the plan, the port refits the ship (and orders what it lacks) when it regroups.
        /// </summary>
        void OpenWeapons()
        {
            weaponOptions = 0;
            var ship = battle.World.FindShip(shipShownId);
            if (ship == null || ship.Owner != BattleController.PlayerFaction || ship.Mounts.Length == 0) return;
            var panel = OpenPopover(shipWeaponsButton, $"Uzbrojenie – {BattleController.ClassName(ship.Class)}");
            var port = battle.ActivePort;
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                var slot = i;
                var size = ship.Definition.Slots[i];
                // The mounted weapon is named only when it differs from the plan (the plan carries the tick).
                var mounted = ship.Mounts[i] != ship.Loadout[i] ? $" · na pokładzie: {WeaponCatalog.Get(ship.Mounts[i]).Name}" : string.Empty;
                Text($"Gniazdo {i + 1} · {SlotName(size)}{mounted}", "popover-section", panel);
                foreach (WeaponType weapon in Enum.GetValues(typeof(WeaponType)))
                {
                    if (!WeaponCatalog.Fits(weapon, size)) continue;
                    var definition = WeaponCatalog.Get(weapon);
                    if (weapon != WeaponType.None && definition.Slot != size) continue;
                    var chosen = weapon;
                    var locked = definition.Slot == SlotSize.Heavy && weapon != WeaponType.None && !battle.World.HasHeavyArms(ship.Owner);
                    var stock = !definition.IsMachine || port == null ? string.Empty
                        : locked ? " · badanie"
                        : port.HasWorkshop || port.Arsenal[(int)weapon] > 0 ? $" · w arsenale {port.Arsenal[(int)weapon]}"
                        : " · brak Warsztatu";
                    var value = weapon == WeaponType.None ? "puste" : $"{definition.MinRange:0}–{definition.MaxRange:0} m · {definition.Cycle:0.#} s · {definition.Hull:0.#}/{definition.Men:0.#}{stock}";
                    var item = MenuItem(panel, ship.Loadout[i] == weapon ? "check" : "attack", definition.Name, value, () => { battle.SetWeapon(ship.Id, slot, chosen); ClosePopover(); });
                    item.AddToClassList("compact");
                    item.EnableInClassList("disabled", locked);
                    Tip(item, () => WeaponTip(chosen, locked ? "Broń ciężka wymaga badania w Szkole Mędrców." : null));
                    weaponOptions++;
                }
            }
            Text("Plan zmienia się od razu; broń wymienia port przy przegrupowaniu (zdjęcie 3 s, montaż 5 s / ciężka 10 s). Brakującą port zamawia w Warsztacie.", "popover-note", panel);
        }

        readonly List<Action> convoyActions = new List<Action>();

        /// <summary>Opens the active port's convoy menu and returns how many items it lists (showcase, tests).</summary>
        public int OpenConvoysForTest()
        {
            OpenConvoys();
            return convoyActions.Count;
        }

        /// <summary>Chooses an item of the open convoy menu as a click would (tests).</summary>
        public void PickConvoyForTest(int index) => convoyActions[index]();

        /// <summary>The convoy switch on the shown ship's card, or null when the card offers none (tests).</summary>
        public string ConvoySwitchForTest => shipConvoyButton != null && shipConvoyButton.resolvedStyle.display != DisplayStyle.None && shipView.resolvedStyle.display != DisplayStyle.None ? shipConvoyButton.text : null;

        /// <summary>Opens the active port's building menu and returns how many projects it lists (showcase, tests).</summary>
        public int OpenBuildForTest()
        {
            OpenBuild();
            return buildOptions;
        }

        int buildOptions;

        /// <summary>The names of the projects the open building menu lists, in order (tests).</summary>
        public IReadOnlyList<string> PopoverItemsForTest =>
            popover == null ? (IReadOnlyList<string>)Array.Empty<string>() : popover.Query<Label>(className: "menu-item-label").ToList().Select(label => label.text).ToList();

        public void ClosePopoverForTest() => ClosePopover();
        /// <summary>Where the open popover lies on screen, or null (showcase diagnostics).</summary>
        public Rect? PopoverBoundsForTest => popover == null ? (Rect?)null : popover.worldBound;
        /// <summary>The top edge of the bottom band in panel space (tests).</summary>
        public float BottomBandTopForTest => bottomBand?.worldBound.yMin ?? 0f;

        /// <summary>Opens the weapon choice for the shown ship and returns how many options it lists (tests).</summary>
        public int OpenWeaponsForTest()
        {
            OpenWeapons();
            return weaponOptions;
        }

        readonly List<Action> crewPlanActions = new List<Action>();

        /// <summary>The crew counts shown on the ship card: sailors, swordsmen, archers, wounded.</summary>
        public string CrewText => $"{shipSailors?.text}|{shipSoldiers?.text}|{shipJavelins?.text}|{shipArchers?.text}|{shipWounded?.text}";

        public bool CrewPlanOffered => shipCrewButton != null && shipCrewButton.resolvedStyle.display != DisplayStyle.None && shipView.resolvedStyle.display != DisplayStyle.None;

        /// <summary>Opens the crew plan choice for the shown ship and returns how many crews it offers (tests).</summary>
        public int OpenCrewPlanForTest()
        {
            OpenCrewPlan();
            return crewPlanActions.Count;
        }

        /// <summary>Chooses one of the offered crews as a click would (tests).</summary>
        public void PickCrewPlanForTest(int index) => crewPlanActions[index]();

        /// <summary>The four ready crews for the shown ship: their numbers and what the port charges to train to them.</summary>
        void OpenCrewPlan()
        {
            crewPlanActions.Clear();
            var ship = battle.World.FindShip(shipShownId);
            if (ship == null || ship.Owner != BattleController.PlayerFaction || ship.Definition.IsEconomic) return;
            var panel = OpenPopover(shipCrewButton, $"Skład załogi – {BattleController.ClassName(ship.Class)}");
            Text("żeglarze · szermierze · oszczepnicy · łucznicy; koszt w porcie", "popover-row-label", panel);
            foreach (var preset in new[] { CrewPreset.Balanced, CrewPreset.Boarding, CrewPreset.Volley, CrewPreset.Seamen })
            {
                var plan = ship.Definition.PlanFor(preset);
                var probe = new Ship { Class = ship.Class, Sailors = ship.Sailors, Swordsmen = ship.Swordsmen, Javelins = ship.Javelins, Archers = ship.Archers, Wounded = ship.Wounded, Plan = plan };
                var cost = World.MusterCost(probe);
                var chosen = preset;
                var current = ship.Plan.Preset == preset;
                Action pick = () => { battle.SetCrewPlan(ship.Id, chosen); ClosePopover(); };
                crewPlanActions.Add(pick);
                var item = MenuItem(panel, current ? "check" : preset == CrewPreset.Boarding ? "board" : preset == CrewPreset.Volley ? "volley" : preset == CrewPreset.Seamen ? "speed" : "soldier",
                    BattleController.PresetName(preset), $"{plan.Sailors} · {plan.Swordsmen} · {plan.Javelins} · {plan.Archers} · {cost / (float)Rules.Centi:0.##} S", pick);
                Tip(item, () => new TipInfo
                {
                    Title = BattleController.PresetName(chosen),
                    Body = chosen == CrewPreset.Boarding ? "Przewaga szermierzy: do zdobywania okrętów."
                        : chosen == CrewPreset.Volley ? "Przewaga łuczników: wybija załogi z dystansu i dobrze broni własnego pokładu."
                        : chosen == CrewPreset.Seamen ? "40% żeglarzy: +8% prędkości, mniej wojska."
                        : "Szermierze i łucznicy po równo.",
                    Cost = $"Przegrupowanie w porcie: {cost / (float)Rules.Centi:0.##} S (leczenie 0,25 S, zaciąg 0,5 S żeglarz / 1 S wojownik, przeszkolenie 0,5 S).",
                });
            }
        }

        Label CardStat(VisualElement row, string icon, string label, Func<TipInfo> tip)
        {
            var stat = Element("card-stat", row);
            Glyph(stat, icon, "glyph-small");
            var value = Text(string.Empty, "card-stat-value", stat);
            Text(label, "card-stat-label", stat);
            Tip(stat, tip);
            return value;
        }

        void BuildGroup(VisualElement content)
        {
            groupView = Element("selection-content", content);
            var cards = Element("group-cards", groupView);
            for (var i = 0; i < MaxCards; i++)
            {
                var index = i;
                var card = new Button { focusable = true };
                card.AddToClassList("unit-card");
                cards.Add(card);
                var portrait = Element("unit-portrait", card);
                portrait.pickingMode = PickingMode.Ignore;
                var name = Text(string.Empty, "unit-name", card);
                var meter = new MeterBar(HudPalette.Healthy);
                meter.AddToClassList("unit-meter");
                card.Add(meter);
                card.RegisterCallback<ClickEvent>(evt => ClickUnit(card, evt.shiftKey));
                Tip(card, () => UnitTip(card));
                unitCards.Add((card, portrait, name, meter));
            }
            moreCard = new Button(OpenFullSelection) { focusable = true };
            moreCard.AddToClassList("more-card");
            cards.Add(moreCard);
            moreLabel = Text("+0", "more-label", moreCard);
            Tip(moreCard, () => new TipInfo { Title = "Pozostałe statki", Body = "Pełna lista zaznaczenia; nic nie zostaje odznaczone." });

            var summary = Element("group-summary", groupView);
            for (var i = 0; i < 3; i++)
            {
                var row = Element("class-count", summary);
                var glyph = Glyph(row, "trireme", "glyph-small");
                var text = Text(string.Empty, "small", row);
                text.style.marginLeft = 6;
                classCounts.Add((row, glyph, text));
            }
            var totals = Element("card-row", summary);
            Glyph(totals, "shield", "glyph-small");
            groupHull = Text(string.Empty, "card-stat-value", totals);
            Glyph(totals, "sailor", "glyph-small").style.marginLeft = 14;
            groupCrew = Text(string.Empty, "card-stat-value", totals);
        }

        void BuildPort(VisualElement content)
        {
            portView = Element("selection-content", content);
            portPortrait = Element("portrait", portView);
            var card = Element("card", portView);
            // Three rows fit the 194 px band; the port's name is the section title above the card.
            var hull = Element("card-row", card);
            Glyph(hull, "settlement", "glyph-small");
            Text("Mury", "small", hull).style.marginLeft = 6;
            portHullMeter = new MeterBar(HudPalette.Healthy, 10);
            portHullMeter.AddToClassList("card-meter");
            hull.Add(portHullMeter);
            portHullValue = Text(string.Empty, "card-value", hull);
            // Both stores on one row, so the card fits the band with its berths and shipyard line.
            portStores = Element("card-row", card);
            Glyph(portStores, "wood", "glyph-small");
            portWoodMeter = new MeterBar(HudPalette.Timber, 10);
            portWoodMeter.AddToClassList("card-meter");
            portStores.Add(portWoodMeter);
            portWoodValue = Text(string.Empty, "card-value", portStores);
            Glyph(portStores, "silver", "glyph-small").style.marginLeft = 12;
            portSilverMeter = new MeterBar(HudPalette.Silver, 10);
            portSilverMeter.AddToClassList("card-meter");
            portStores.Add(portSilverMeter);
            portSilverValue = Text(string.Empty, "card-value", portStores);
            var berths = Element("card-row", card);
            Glyph(berths, "port", "glyph-small");
            portBerths = Text(string.Empty, "small", berths);
            portBerths.style.marginLeft = 6;
            portYard = Text(string.Empty, "small", berths);
            portYard.style.marginLeft = 18;
            // One line only: a long works summary is cut with an ellipsis, never wrapped out of the band.
            berths.style.overflow = Overflow.Hidden;
            portYard.style.whiteSpace = WhiteSpace.NoWrap;
            portYard.style.overflow = Overflow.Hidden;
            portYard.style.textOverflow = TextOverflow.Ellipsis;
            portYard.style.flexShrink = 1;
            portYard.style.minWidth = 0;
            Tip(portYard, () => new TipInfo { Title = "Stocznia i rozbudowa", Body = portYard.text });

            // Buildings, the workshop line and the arsenal (docs/UZBROJENIE.md): on the berths row, whose summary text
            // gives way to the buttons; the card has no height for a fourth row in the 194 px band.
            portWorksRow = berths;
            portWorks = portYard;
            portBuildButton = new Button(OpenBuild) { text = "Rozbudowa", focusable = true };
            portBuildButton.AddToClassList("card-button");
            portWorksRow.Add(portBuildButton);
            Tip(portBuildButton, () => new TipInfo { Title = "Rozbudowa portu", Body = "Warsztat (arsenał) przy stoczni; port macierzysty z Warsztatem awansuje na Miasto; Miasto wznosi Szkołę Mędrców; Szkoła z Warsztatem bada broń ciężką. Jedna budowa naraz, płatna z magazynu tego portu." });
            portWorkshopButton = new Button(OpenWorkshop) { text = "Warsztat", focusable = true };
            portWorkshopButton.AddToClassList("card-button");
            portWorkshopButton.style.marginLeft = 6;
            portWorksRow.Add(portWorkshopButton);
            Tip(portWorkshopButton, () => new TipInfo { Title = "Warsztat", Body = "Wytwarza machiny do arsenału portu. Okręty dostają je przy przegrupowaniu w porcie według planu uzbrojenia; brakujące port zamawia sam." });
            portConvoyButton = new Button(OpenConvoys) { text = "Konwoje", focusable = true };
            portConvoyButton.AddToClassList("card-button");
            portConvoyButton.style.marginLeft = 6;
            portWorksRow.Add(portConvoyButton);
            Tip(portConvoyButton, () => new TipInfo { Title = "Konwoje", Body = "Port priorytetowy, do którego handlowce w konwoju wiozą nadwyżki; minimum, poniżej którego automat nie bierze z tego portu; wolne handlowce do konwoju." });
        }

        VisualElement portWorksRow;
        Label portWorks;
        Button portBuildButton, portWorkshopButton, portConvoyButton;

        /// <summary>
        /// E03 convoys at this port (World.Convoy): make it the side's priority port, choose what it keeps against
        /// automatic loads, put the idle merchants on convoy duty. Each click is an ordinary command.
        /// </summary>
        void OpenConvoys()
        {
            var port = battle.ActivePort;
            if (port == null) return;
            var world = battle.World;
            var player = BattleController.PlayerFaction;
            var panel = OpenPopover(portConvoyButton, $"Konwoje – {battle.PortName(port)}");
            convoyActions.Clear();
            Button Item(string icon, string label, string value, Action click)
            {
                convoyActions.Add(click);
                return MenuItem(panel, icon, label, value, click);
            }
            var priority = world.PriorityPort(player);
            var (goalWood, goalSilver) = world.ConvoyGoal(port);
            var isPriority = priority == port;
            var named = Item(isPriority ? "check" : "port", "Port priorytetowy",
                isPriority ? "ten port" : $"teraz: {(priority != null ? battle.PortName(priority) : "brak")}", () => { battle.SetPriorityPort(port); ClosePopover(); });
            named.EnableInClassList("disabled", isPriority);
            Tip(named, () => new TipInfo { Title = "Port priorytetowy", Body = $"Handlowce w konwoju wiozą tu nadwyżki innych portów, aż wolny zapas osiągnie cenę najdroższego okrętu, jaki ten port może zbudować: teraz {goalWood / Rules.Centi} D · {goalSilver / Rules.Centi} S. Bez wskazania – port macierzysty." });
            foreach (var (wood, silver) in BattleController.PortMinimums)
            {
                var chosenWood = wood;
                var chosenSilver = silver;
                var current = port.KeepWood == wood * Rules.Centi && port.KeepSilver == silver * Rules.Centi;
                var row = Item(current ? "check" : "cargo", "Minimum w tym porcie", $"{wood} D · {silver} S", () => { battle.SetPortMinimum(port, chosenWood, chosenSilver); ClosePopover(); });
                row.EnableInClassList("disabled", current);
            }
            var idle = world.Ships.Count(ship => ship.Owner == player && ship.Class == ShipClass.Merchant && ship.Order == OrderKind.Idle);
            var onDuty = world.Ships.Count(ship => ship.Owner == player && ship.Order == OrderKind.Convoy);
            var send = Item("deliver", "Wolne handlowce do konwoju", $"{idle} wolne · {onDuty} w konwojach", () => { battle.ConvoyIdleMerchants(); ClosePopover(); });
            send.EnableInClassList("disabled", idle == 0);
            Text("Automat nie bierze ze źródła poniżej jego minimum (rozkaz z ilościami może). Do trzech handlowców ładuje się w jednym porcie i płynie razem. Stop zdejmuje handlowca z konwoju.", "popover-note", panel);
        }

        /// <summary>The projects with their price, state or what they still need; one click starts one.</summary>
        void OpenBuild()
        {
            var port = battle.ActivePort;
            if (port == null) return;
            var panel = OpenPopover(portBuildButton, $"Rozbudowa – {battle.PortName(port)}");
            buildOptions = 0;
            foreach (var kind in new[] { ProjectKind.Workshop, ProjectKind.Town, ProjectKind.Dock, ProjectKind.SageSchool, ProjectKind.HeavyArmsResearch })
            {
                var chosen = kind;
                var (wood, silver, seconds) = World.ProjectCost(kind);
                var done = kind == ProjectKind.Workshop ? port.HasWorkshop : kind == ProjectKind.Town ? port.IsTown
                    : kind == ProjectKind.Dock ? port.HasDock
                    : kind == ProjectKind.SageSchool ? port.HasSageSchool : battle.World.HasHeavyArms(port.Owner);
                var building = port.ProjectKind == kind && port.Project != null;
                var value = done ? "gotowe" : building ? $"w budowie {Mathf.FloorToInt(100f * port.Project.DoneTicks / Mathf.Max(1, port.Project.TotalTicks))}%"
                    : $"{wood / Rules.Centi} D · {silver / Rules.Centi} S · {seconds:0} s";
                var problem = done || building ? null : battle.World.ProjectProblem(port, kind);
                var item = MenuItem(panel, done ? "check" : kind == ProjectKind.HeavyArmsResearch ? "help" : kind == ProjectKind.Workshop || kind == ProjectKind.Dock ? "shipyard" : "settlement",
                    BattleController.ProjectName(kind), value, () => { battle.BuildProject(chosen); ClosePopover(); });
                item.EnableInClassList("disabled", done || building || problem != null);
                buildOptions++;
                Tip(item, () => new TipInfo
                {
                    Title = BattleController.ProjectName(chosen),
                    Body = (chosen == ProjectKind.Workshop ? "Linia Warsztatu wytwarza machiny do arsenału portu; odblokowuje Bombowiec."
                        : chosen == ProjectKind.Town ? "Osada staje się Miastem: mury mocniejsze o 25%, dostęp do Szkoły Mędrców i doku."
                        : chosen == ProjectKind.Dock ? "Stocznia Miasta staje się dokiem: jedynym miejscem naprawy kadłubów i modułów broni, tylko za srebro. Inne porty tylko przegrupowują załogę i wymieniają broń."
                        : chosen == ProjectKind.SageSchool ? "Badania floty."
                        : "Balista i katapulta z pociskami ołowianymi oraz onager w Warsztacie każdego portu.") + (problem != null ? "\n" + problem : string.Empty),
                    Cost = $"{wood / Rules.Centi} D · {silver / Rules.Centi} S · {seconds:0} s",
                });
            }
            if (port.Project != null)
            {
                var cancel = MenuItem(panel, "close", $"Anuluj: {BattleController.ProjectName(port.ProjectKind)}", null, () => { battle.CancelProject(); ClosePopover(); });
                Tip(cancel, () => new TipInfo { Title = "Anulowanie budowy", Body = "Niezużyta część rezerwacji wraca; po rozpoczęciu 80% z niej, reszta przepada – jak przy zamówieniach okrętów." });
            }
        }

        /// <summary>Weapons to order with their figures and price, the workshop line (click cancels) and the arsenal.</summary>
        void OpenWorkshop()
        {
            var port = battle.ActivePort;
            if (port == null) return;
            var panel = OpenPopover(portWorkshopButton, $"Warsztat – {battle.PortName(port)}");
            foreach (WeaponType weapon in Enum.GetValues(typeof(WeaponType)))
            {
                var definition = WeaponCatalog.Get(weapon);
                if (!definition.IsMachine || definition.CostWood + definition.CostSilver == 0) continue;
                var chosen = weapon;
                var problem = battle.World.WeaponProblem(port, weapon);
                var item = MenuItem(panel, definition.Slot == SlotSize.Heavy ? "target" : "attack", definition.Name,
                    $"{definition.CostWood} D · {definition.CostSilver} S · {definition.WorkshopSeconds:0} s · ma {port.Arsenal[(int)weapon]}", () => battle.QueueWeapon(chosen));
                item.EnableInClassList("disabled", problem != null);
                Tip(item, () => WeaponTip(chosen, problem));
            }
            for (var i = 0; i < port.WorkshopQueue.Count; i++)
            {
                var index = i;
                var order = port.WorkshopQueue[i];
                var progress = i == 0 ? $"{Mathf.FloorToInt(100f * order.DoneTicks / Mathf.Max(1, order.TotalTicks))}%" : "czeka";
                MenuItem(panel, "close", $"Anuluj: {WeaponCatalog.Get(order.Weapon).Name}", progress, () => { battle.CancelWeapon(index); ClosePopover(); });
            }
            Text("Machiny trafiają do arsenału portu; okręty dostają je przy przegrupowaniu według planu uzbrojenia.", "popover-note", panel);
        }

        /// <summary>A weapon's figures for comparison: range with dead zone, arc, cycle, the packet, the crew it needs.</summary>
        static TipInfo WeaponTip(WeaponType weapon, string problem)
        {
            var definition = WeaponCatalog.Get(weapon);
            return new TipInfo
            {
                Title = definition.Name,
                Body = $"Zasięg {definition.MinRange:0}–{definition.MaxRange:0} m, sektor {definition.ArcDegrees:0}°, cykl {definition.Cycle:0.#} s. "
                    + $"Trafienie: kadłub {definition.Hull:0.#}, ludzie {definition.Men:0.#} HP (10 HP = 1 człowiek), moduł {definition.Module:0.#}"
                    + (definition.Splash > 0 ? $", obszar {definition.Splash:0} m" : string.Empty) + (definition.BuildingMultiplier != 1 ? $", budowle ×{definition.BuildingMultiplier:0.##}" : string.Empty)
                    + $". Obsługa: {definition.Crew}." + (definition.Lobbed ? " Strzela łukiem ponad lądem." : string.Empty) + (problem != null ? "\n" + problem : string.Empty),
                Cost = definition.CostWood + definition.CostSilver > 0 ? $"Warsztat: {definition.CostWood} D · {definition.CostSilver} S · {definition.WorkshopSeconds:0} s" : "Stanowisko obsadzane przez załogę.",
            };
        }

        void BuildWreck(VisualElement content)
        {
            wreckView = Element("selection-content", content);
            var portrait = Element("portrait", wreckView);
            portrait.style.backgroundImage = Icon("wreck", 128);
            var card = Element("card", wreckView);
            Text("Wrak", "card-name", card);
            var cargo = Element("card-row", card);
            Glyph(cargo, "cargo", "glyph-small");
            wreckCargo = Text(string.Empty, "card-stat-value", cargo);
            var time = Element("card-row", card);
            Glyph(time, "time", "glyph-small");
            wreckTime = Text(string.Empty, "small", time);
            wreckTime.style.marginLeft = 6;
            Text("Kuter z rozkazem zbioru zabiera ładunek wraku i ocalałe machiny (do arsenału portu).", "help-line", card);
        }

        QueueRow QueueRowOf(VisualElement section, bool war)
        {
            var row = new QueueRow { War = war, Root = Element("queue-row", section) };
            var label = Element("queue-label", row.Root);
            row.Glyph = Glyph(label, war ? "shipyard" : "port", "glyph-small");
            Text(war ? "Wojenna" : "Portowa", "queue-label-text", label);
            Tip(label, () => new TipInfo { Title = war ? "Linia wojenna" : "Linia portowa", Body = war ? "Taranowce, niszczyciele załóg i abordażowce. Wymaga stoczni w tym porcie." : "Kutry i handlowce. Ma ją każdy port." });
            row.Items = Element("queue-items", row.Root);
            for (var i = 0; i < MaxQueueShown; i++)
            {
                var index = i;
                var item = new Button { focusable = true };
                item.AddToClassList("queue-item");
                row.Items.Add(item);
                var glyph = Element("queue-glyph", item);
                glyph.pickingMode = PickingMode.Ignore;
                var stage = Element("queue-stage", item);
                stage.pickingMode = PickingMode.Ignore;
                var time = Text(string.Empty, "queue-time", item);
                var bar = new MeterBar(HudPalette.Warning);
                bar.AddToClassList("queue-bar");
                item.Add(bar);
                item.userData = (war, index);
                item.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 1) { RequestCancel(war, index); evt.StopPropagation(); } });
                Tip(item, () => QueueTip(war, index));
                row.Slots.Add((item, glyph, stage, time, bar));
            }
            row.Empty = Text(string.Empty, "queue-empty", row.Items);
            row.Add = new Button { focusable = true };
            row.Add.AddToClassList("queue-add");
            var addGlyph = Element("small-button-glyph", row.Add);
            addGlyph.pickingMode = PickingMode.Ignore;
            addGlyph.style.backgroundImage = Icon("plus");
            row.Add.clicked += () => OpenProduction(war, row.Add);
            row.Items.Add(row.Add);
            Tip(row.Add, () => new TipInfo { Title = war ? "Zamów okręt wojenny" : "Zamów kuter lub handlowca", Body = "Lista szablonów tej linii z ceną i czasem." });
            return row;
        }

        CommandButton MakeCommand(VisualElement grid, CommandButton definition)
        {
            var button = new Button { focusable = true };
            button.AddToClassList("command");
            grid.Add(button);
            var glyph = Element("command-glyph", button);
            glyph.pickingMode = PickingMode.Ignore;
            glyph.style.backgroundImage = Icon(definition.Icon);
            Text(definition.Label, "command-label", button);
            definition.Hotkey = Text(string.Empty, "hotkey", button);
            definition.Button = button;
            button.clicked += () => Run(definition);
            Tip(button, () => CommandTip(definition));
            return definition;
        }

        void BuildTooltip(VisualElement parent)
        {
            tooltip = Element("tooltip", parent);
            tooltip.pickingMode = PickingMode.Ignore;
            tipTitle = Text(string.Empty, "tooltip-title", tooltip);
            tipBody = Text(string.Empty, "tooltip-body", tooltip);
            tipCost = Text(string.Empty, "tooltip-cost", tooltip);
            tipKey = Text(string.Empty, "tooltip-key", tooltip);
            Show(tooltip, false);
        }

        // ---------- Commands ----------

        static IEnumerable<CommandButton> CommandDefinitions()
        {
            yield return new CommandButton { Action = GameAction.Move, Icon = "move", Label = "Ruch", Full = "Płyń do punktu", Targeted = HudOrder.Move, Able = _ => true, Body = "Wskaż miejsce na morzu; statki omijają ląd i płycizny." };
            yield return new CommandButton { Action = GameAction.Stop, Icon = "stop", Label = "Stop", Full = "Zatrzymaj", Instant = hud => hud.battle.StopSelected(), Able = _ => true, Body = "Przerywa bieżący rozkaz." };
            yield return new CommandButton { Action = GameAction.Attack, Icon = "attack", Label = "Atak", Full = "Ostrzał", Targeted = HudOrder.Attack, Able = ship => World.HasWeapons(ship), Body = "Stanowiska łuczników i oszczepników oraz machiny strzelają do wskazanego okrętu lub osady wroga, gdy cel jest w ich zasięgu i sektorze." };
            yield return new CommandButton { Action = GameAction.Ram, Icon = "ram", Label = "Taran", Full = "Szarża taranem", Targeted = HudOrder.Ram, Able = ship => ship.Definition.CanRam, Body = "Każdy okręt wojenny ma taran; taranowiec uderza najmocniej (×1,5). Szarża jest zapowiedziana co najmniej 1,2 s przed uderzeniem; po niej odskok, 8 s odnowienia i odejście na długość kadłuba." };
            yield return new CommandButton { Action = GameAction.Board, Icon = "board", Label = "Abordaż", Full = "Abordaż", Targeted = HudOrder.Board, Able = ship => ship.Class == ShipClass.Boarder, Body = "Haki i walka wręcz o zdobycie okrętu." };
            yield return new CommandButton { Action = GameAction.Volley, Icon = "volley", Label = "Ostrzał", Full = "Ostrzał z abordażowca", Targeted = HudOrder.Volley, Able = ship => ship.Class == ShipClass.Boarder, Body = "Abordażowiec strzela zamiast szukać zwarcia. Także Alt+PPM." };
            yield return new CommandButton { Action = GameAction.Harvest, Icon = "gather", Label = "Zbiór", Full = "Zbiór surowca", Targeted = HudOrder.Harvest, Able = ship => ship.Definition.CanHarvest, Body = "Kutry na złoże drewna lub srebra i z urobkiem do portu; także zbiór wraków." };
            yield return new CommandButton { Action = GameAction.Deliver, Icon = "deliver", Label = "Dostawa", Full = "Dostawa do portu", Targeted = HudOrder.Deliver, Able = ship => ship.Class == ShipClass.Merchant, Body = "Handlowiec wiezie ładunek z wybranego portu do wskazanego własnego portu." };
            yield return new CommandButton { Action = GameAction.Outpost, Icon = "outpost", Label = "Buduj", Full = "Zbuduj przyczółek", Targeted = HudOrder.Outpost, Able = ship => ship.Class == ShipClass.Merchant, Body = $"Handlowiec zakłada przyczółek na wskazanym wolnym brzegu ({Rules.OutpostWood / Rules.Centi} D / {Rules.OutpostSilver / Rules.Centi} S z ładowni)." };
            yield return new CommandButton { Action = GameAction.Escort, Icon = "escort", Label = "Eskorta", Full = "Eskortuj handlowca", Targeted = HudOrder.Escort, Able = ship => !ship.Definition.IsEconomic, Body = "Okręty płyną przy burcie wskazanego handlowca i bronią go w jego strefie." };
            yield return new CommandButton { Action = GameAction.Repair, Icon = "repair", Label = "Remont", Full = "Remont w doku", Instant = hud => hud.battle.RepairSelected(), Able = _ => true, Body = "Uszkodzone statki płyną do najbliższego doku: kadłub i moduły broni naprawia tylko dok (ulepszona stocznia Miasta), za srebro. Bez doku statki płyną do najbliższej stoczni tylko po przegrupowanie załogi i wymianę broni." };
            yield return new CommandButton { Action = GameAction.Scrap, Icon = "scrap", Label = "Złomuj", Full = "Złomowanie", Instant = hud => hud.ConfirmScrap(), Able = _ => true, Body = "Rozbiera statek w porcie i odzyskuje część materiałów. Nie da się cofnąć." };
        }

        void Run(CommandButton command)
        {
            var ships = OwnSelected();
            if (!ships.Any(command.Able)) return;
            if (command.Instant != null) { command.Instant(this); Refresh(); return; }
            battle.PendingOrder = battle.PendingOrder == command.Targeted ? HudOrder.None : command.Targeted;
            Refresh();
        }

        void ConfirmScrap()
        {
            var ships = OwnSelected();
            if (ships.Count == 0) return;
            var names = string.Join(", ", ships.GroupBy(ship => ship.Class).Select(group => $"{group.Count()} × {BattleController.ClassName(group.Key)}"));
            OpenConfirm("Złomowanie", $"Rozebrać w porcie: {names}? Statki znikną z floty; część materiałów wróci do magazynu. Tej operacji nie da się cofnąć.",
                null, "Złomuj", () => battle.ScrapSelected(), true);
        }

        List<Ship> OwnSelected() => battle.Selected.Select(battle.World.FindShip).Where(ship => ship != null && ship.Owner == BattleController.PlayerFaction).ToList();

        // ---------- Frame ----------

        void Update()
        {
            if (battle == null || root == null || battle.World == null) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            UpdateFrame();
            updateMs += watch.Elapsed.TotalMilliseconds;
            updates++;
        }

        void UpdateFrame()
        {
            textTimer -= Time.unscaledDeltaTime;
            if (textTimer <= 0f)
            {
                textTimer = TextRefresh;
                Refresh();
            }
            if (!collapsed)
            {
                chartTimer -= Time.unscaledDeltaTime;
                if (chartTimer <= 0f)
                {
                    chartTimer = ChartRefresh;
                    minimap.Refresh();
                }
                minimap.RefreshView();
            }
            cursorTimer -= Time.unscaledDeltaTime;
            if (cursorTimer <= 0f)
            {
                cursorTimer = CursorRefresh;
                UpdateCursor();
            }
            UpdateHealthBars();
            UpdateSelectionBox();
            UpdateHeralds();
            UpdateFlashes();
        }

        /// <summary>
        /// The interface's keys, called once a frame from the battle controller's input step: key capture for
        /// rebinding, dialogs and popovers, folding the panels, the alarm, cancelling a focused order, order keys.
        /// </summary>
        public void HandleKeys()
        {
            ConsumedCancel = false;
            if (capturing.HasValue) { CaptureKey(); return; }
            if (modal != null)
            {
                if (GameInput.GetKeyDown(KeyCode.Escape)) { CloseModal(); ConsumedCancel = true; }
                else if (GameInput.GetKeyDown(KeyCode.Return) || GameInput.GetKeyDown(KeyCode.KeypadEnter)) ConfirmModal();
                return;
            }
            if (popover != null && KeyBindings.Down(GameAction.Cancel)) { ClosePopover(); ConsumedCancel = true; return; }
            if (KeyBindings.Down(GameAction.CollapseHud)) ToggleCollapse();
            if (KeyBindings.Down(GameAction.FocusAlert)) FocusAlarm();
            if (KeyBindings.Down(GameAction.CancelOrder) && FocusedQueueItem(out var war, out var index)) RequestCancel(war, index);
            if (KeyBindings.Down(GameAction.Cancel) && battle.PendingOrder == HudOrder.None && battle.Selected.Count == 0 && battle.InspectedSettlementId < 0) { OpenMenu(); ConsumedCancel = true; }
            var own = OwnSelected().Count;
            if (own == 0) return;
            if (own > 1 && KeyBindings.Down(GameAction.Formation)) { battle.ToggleGroupFormation(); Refresh(); }
            foreach (var command in commands)
            {
                if (KeyBindings.Down(command.Action)) Run(command);
            }
        }

        /// <summary>True when this frame's Escape already closed something in the interface.</summary>
        public bool ConsumedCancel { get; private set; }

        /// <summary>True while the pointer is over a visible control, so a click there never reaches the sea.</summary>
        public bool IsPointerOverInterface(Vector2 screenFromBottomLeft)
        {
            if (root?.panel == null) return false;
            var position = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenFromBottomLeft.x, Screen.height - screenFromBottomLeft.y));
            return root.panel.Pick(position) != null;
        }

        void Refresh()
        {
            if (battle == null || battle.World == null || minimap == null) return;
            RefreshTopBar();
            RefreshOverMap();
            if (!collapsed)
            {
                RefreshSelection();
                RefreshQueues();
                RefreshCommands();
            }
            RefreshOverlays();
            if (tooltipOwner != null && tooltipContent != null && tooltip.style.display == DisplayStyle.Flex) FillTooltip(tooltipContent());
        }

        // ---------- Top bar ----------

        void RefreshTopBar()
        {
            var world = battle.World;
            var player = BattleController.PlayerFaction;
            var port = battle.ActivePort;
            if (port != null)
            {
                var wood = port.Available(ResourceType.Wood);
                var silver = port.Available(ResourceType.Silver);
                if (lastWood != long.MinValue && wood != lastWood) Flash(woodValue);
                if (lastSilver != long.MinValue && silver != lastSilver) Flash(silverValue);
                lastWood = wood;
                lastSilver = silver;
                SetText(woodValue, (wood / Rules.Centi).ToString());
                SetText(woodCap, $"/ {port.Capacity(ResourceType.Wood) / Rules.Centi}");
                SetText(silverValue, (silver / Rules.Centi).ToString());
                SetText(silverCap, $"/ {port.Capacity(ResourceType.Silver) / Rules.Centi}");
                woodValue.EnableInClassList("res-short", wood < Rules.ReserveWood);
                silverValue.EnableInClassList("res-short", silver < Rules.ReserveSilver);
            }

            long woodTransit = 0, silverTransit = 0;
            var fleet = 0;
            foreach (var ship in world.Ships)
            {
                if (ship.Owner != player) continue;
                fleet++;
                woodTransit += ship.HoldWood + (ship.CargoType == ResourceType.Wood ? ship.Cargo : 0);
                silverTransit += ship.HoldSilver + (ship.CargoType == ResourceType.Silver ? ship.Cargo : 0);
            }
            SetText(transitValue, $"W drodze  {woodTransit / Rules.Centi} D · {silverTransit / Rules.Centi} S");
            SetText(fleetValue, fleet.ToString());
            SetText(fleetLimit, $"/ {Rules.FleetLimit}");
            SetText(portsValue, world.PortsOf(player).Count().ToString());
            var seconds = world.Tick / Rules.TickRate;
            SetText(timeValue, $"{seconds / 60}:{seconds % 60:00}");
            SetText(speedValue, $"×{battle.Speed:0.#}");
            pauseGlyph.style.backgroundImage = Icon(battle.Paused ? "right" : "pause");
            pauseGlyph.parent.EnableInClassList("active", battle.Paused);
        }

        void Flash(Label label)
        {
            if (reducedMotion) return;
            flashing[label] = Time.unscaledTime + FlashSeconds;
            label.AddToClassList("res-flash");
        }

        void UpdateFlashes()
        {
            if (flashing.Count == 0) return;
            foreach (var label in flashing.Where(pair => Time.unscaledTime >= pair.Value).Select(pair => pair.Key).ToList())
            {
                label.RemoveFromClassList("res-flash");
                flashing.Remove(label);
            }
        }

        TipInfo StoreTip(ResourceType type)
        {
            var port = battle.ActivePort;
            if (port == null) return default;
            var name = type == ResourceType.Wood ? "Drewno" : "Srebro";
            return new TipInfo
            {
                Title = $"{name} – {battle.PortName(port)}",
                Body = $"Dostępne do wydania: {port.Available(type) / Rules.Centi}. Kliknij, aby rozwinąć magazyn.",
                Key = "Klik: szczegóły magazynu",
            };
        }

        /// <summary>The store of the chosen port in separate rows: free, reserved, physical, protected and capacity.</summary>
        void OpenStore(ResourceType type, VisualElement anchor)
        {
            var port = battle.ActivePort;
            if (port == null) return;
            var unit = type == ResourceType.Wood ? "D" : "S";
            var reserved = type == ResourceType.Wood ? port.ReservedWood : port.ReservedSilver;
            var protectedPart = type == ResourceType.Wood ? Rules.ReserveWood : Rules.ReserveSilver;
            var transit = battle.World.Ships.Where(ship => ship.Owner == BattleController.PlayerFaction)
                .Sum(ship => (type == ResourceType.Wood ? ship.HoldWood : ship.HoldSilver) + (ship.CargoType == type ? ship.Cargo : 0));
            var panel = OpenPopover(anchor, $"{(type == ResourceType.Wood ? "Drewno" : "Srebro")} – {battle.PortName(port)}");
            PopoverRow(panel, "Dostępne do wydania", $"{port.Available(type) / Rules.Centi} {unit}");
            PopoverRow(panel, "Zarezerwowane na zamówienia", $"{reserved / Rules.Centi} {unit}");
            PopoverRow(panel, "Zapas w magazynie (fizyczny)", $"{port.Stock(type) / Rules.Centi} {unit}");
            PopoverRow(panel, "W tym zapas chroniony", $"{protectedPart / Rules.Centi} {unit}");
            PopoverRow(panel, "Pojemność magazynu", $"{port.Capacity(type) / Rules.Centi} {unit}");
            PopoverRow(panel, "W drodze na statkach", $"{transit / Rules.Centi} {unit}");
            Text("Zapas chroniony jest częścią dostępnych środków, nie drugą rezerwacją. Rezerwacja to część zapasu fizycznego. Ładunek w drodze nie liczy się do magazynu.", "popover-note", panel);
        }

        // ---------- Over the map ----------

        void RefreshOverMap()
        {
            var port = battle.ActivePort;
            SetText(portName, battle.PortName(port));
            Show(portSelector, !collapsed);
            Show(restoreButton, collapsed);

            // Fleet groups: number, class icon and count.
            var groups = battle.Groups.Where(group => group.ids.Length > 0).ToList();
            while (groupsBar.childCount > groups.Count) groupsBar.RemoveAt(groupsBar.childCount - 1);
            for (var i = 0; i < groups.Count; i++)
            {
                VisualElement chip;
                if (i < groupsBar.childCount) chip = groupsBar[i];
                else
                {
                    chip = new Button { focusable = true };
                    chip.AddToClassList("group-chip");
                    groupsBar.Add(chip);
                    Text(string.Empty, "group-chip-number", chip);
                    Glyph(chip, "trireme", "glyph-small");
                    Text(string.Empty, "group-chip-count", chip);
                    chip.RegisterCallback<ClickEvent>(_ => { if (chip.userData is int number) { battle.SelectGroup(number); Refresh(); } });
                    Tip(chip, () => new TipInfo { Title = $"Grupa {chip.userData}", Body = "Klik wybiera grupę.", Key = $"{chip.userData} · Ctrl+{chip.userData} zapisuje" });
                }
                var (number, ids) = groups[i];
                chip.userData = number;
                var main = ids.Select(battle.World.FindShip).Where(ship => ship != null).GroupBy(ship => ship.Class).OrderByDescending(group => group.Count()).FirstOrDefault();
                SetText((Label)chip[0], number.ToString());
                chip[1].style.backgroundImage = Icon(main != null ? ClassIcon(main.Key) : "fleet");
                SetText((Label)chip[2], ids.Length.ToString());
            }

            var order = battle.PendingOrder;
            var definition = commands.FirstOrDefault(command => command.Targeted == order && order != HudOrder.None);
            Show(commandHint, definition != null);
            if (definition != null)
            {
                commandHintGlyph.style.backgroundImage = Icon(definition.Icon);
                SetText(commandHintTitle, definition.Full);
                SetText(commandHintText, "Wskaż cel na mapie · Shift – kolejne cele · Esc anuluje");
            }
        }

        void OpenPortMenu()
        {
            var panel = OpenPopover(portSelector, "Wybierz port");
            foreach (var port in battle.World.PortsOf(BattleController.PlayerFaction))
            {
                var chosen = port;
                var item = MenuItem(panel, port.IsOutpost ? "outpost" : "settlement", battle.PortName(port), $"{port.Available(ResourceType.Wood) / Rules.Centi} D · {port.Available(ResourceType.Silver) / Rules.Centi} S", () =>
                {
                    battle.SelectPort(chosen.Id);
                    battle.FocusCamera(new Vector3((float)chosen.X, 0f, (float)chosen.Y));
                    ClosePopover();
                    Refresh();
                });
                item.EnableInClassList("chosen", battle.ActivePort?.Id == port.Id);
            }
            Text("Zmiana portu zmienia odczyt magazynu i adres zamówień; towar zostaje na miejscu.", "popover-note", panel);
        }

        void FocusFleet()
        {
            var ships = OwnSelected();
            if (ships.Count > 0)
            {
                battle.FocusCamera(new Vector3((float)ships.Average(ship => ship.X), 0f, (float)ships.Average(ship => ship.Y)));
                return;
            }
            var port = battle.ActivePort;
            if (port != null) battle.FocusCamera(new Vector3((float)port.X, 0f, (float)port.Y));
        }

        void ToggleCollapse()
        {
            collapsed = !collapsed;
            Show(bottomBand, !collapsed);
            ClosePopover();
            Refresh();
        }

        // ---------- Selection ----------

        void RefreshSelection()
        {
            var world = battle.World;
            var player = BattleController.PlayerFaction;
            var own = battle.Selected.Select(world.FindShip).Where(ship => ship != null && ship.Owner == player).OrderBy(ship => ship.Class).ThenBy(ship => ship.Id).ToList();
            var enemies = battle.InspectedEnemies.Select(world.FindShip).Where(ship => ship != null && world.CanSee(player, ship)).ToList();
            var port = world.FindSettlement(battle.InspectedSettlementId);
            if (port != null && (port.Destroyed || (port.Owner != player && !world.CanSee(player, port.X, port.Y)))) port = null;
            var wreck = world.Wrecks.FirstOrDefault(item => item.Id == battle.InspectedWreckId && world.CanSee(player, item.X, item.Y));

            Ship single = own.Count == 1 ? own[0] : own.Count == 0 && enemies.Count >= 1 ? enemies[0] : null;
            var group = own.Count > 1;
            Show(shipView, single != null);
            Show(groupView, group);
            Show(portView, single == null && !group && port != null);
            Show(wreckView, single == null && !group && port == null && wreck != null);
            Show(emptyView, single == null && !group && port == null && wreck == null);
            Show(clearSelectionButton, own.Count > 0 || enemies.Count > 0);

            if (single != null)
            {
                FillShip(single);
                SetText(selectionTitle, single.Owner == player ? "Dowodzenie flotą" : "Podgląd przeciwnika");
                SetText(selectionNote, string.Empty);
            }
            else if (group)
            {
                FillGroup(own);
                SetText(selectionTitle, "Zaznaczona flota");
                SetText(selectionNote, $"{own.Count} {Plural(own.Count, "okręt", "okręty", "okrętów")}");
            }
            else if (port != null)
            {
                FillPort(port);
                // The port's name heads the section, so the card keeps three rows in the band.
                SetText(selectionTitle, battle.PortName(port));
                SetText(selectionNote, string.Empty);
            }
            else if (wreck != null)
            {
                SetText(wreckCargo, $"{wreck.Wood / Rules.Centi} D · {wreck.Silver / Rules.Centi} S" + (wreck.ItemCount > 0 ? $" · machiny {wreck.ItemCount}" : string.Empty));
                SetText(wreckTime, $"Zatonie za {Mathf.Max(0, (wreck.ExpiresTick - world.Tick) / Rules.TickRate)} s");
                SetText(selectionTitle, "Rozpoznany wrak");
                SetText(selectionNote, string.Empty);
            }
            else
            {
                SetText(selectionTitle, "Dowodzenie flotą");
                SetText(selectionNote, string.Empty);
            }
        }

        void FillShip(Ship ship)
        {
            var player = BattleController.PlayerFaction;
            var enemy = ship.Owner != player;
            var definition = ship.Definition;
            shipPortrait.style.backgroundImage = Portrait(ship.Class);
            shipPortrait.EnableInClassList("enemy", enemy);
            shipBadge.style.backgroundImage = Icon(enemy ? "enemy" : "shield");
            SetText(shipName, BattleController.ClassName(ship.Class));
            SetText(shipTag, enemy ? "wróg · widoczny teraz" : string.Empty);
            var hull = (float)(ship.Hull / definition.Hull);
            shipHullMeter.Set(hull, HudPalette.Hull(hull), patterns && hull <= 0.3f);
            SetText(shipHull, $"{Mathf.CeilToInt((float)ship.Hull)} / {Mathf.CeilToInt((float)definition.Hull)}");
            shipShownId = ship.Id;
            // Own crews against their plan; an enemy's only as seen now.
            string Count(int now, int plan) => enemy || now == plan ? now.ToString() : $"{now}/{plan}";
            SetText(shipSailors, Count(ship.Sailors, ship.Plan.Sailors));
            SetText(shipSoldiers, Count(ship.Swordsmen, ship.Plan.Swordsmen));
            SetText(shipJavelins, Count(ship.Javelins, ship.Plan.Javelins));
            SetText(shipArchers, Count(ship.Archers, ship.Plan.Archers));
            SetText(shipWounded, ship.Wounded.ToString());
            Show(shipCrewButton, !enemy && !definition.IsEconomic);
            Show(shipWeaponsButton, !enemy && ship.Mounts.Length > 0);
            if (!enemy && !definition.IsEconomic) shipCrewButton.text = $"Skład: {BattleController.PresetName(ship.Plan.Preset)}";
            Show(shipConvoyButton, !enemy && ship.Class == ShipClass.Merchant);
            shipConvoyButton.text = ship.Order == OrderKind.Convoy ? "Konwój: tak" : "Konwój";

            var link = battle.BoardingOf(ship.Id);
            Show(shipBoardingRow, link != null);
            if (link != null)
            {
                var attacking = link.AttackerId == ship.Id;
                var ours = attacking ? link.AttackerStrength : link.TargetStrength;
                var theirs = attacking ? link.TargetStrength : link.AttackerStrength;
                var most = Mathf.Max(1f, (float)Math.Max(ours, theirs));
                shipBoardingOurs.Set((float)ours / most, HudPalette.Ally);
                shipBoardingTheirs.Set((float)theirs / most, HudPalette.Enemy);
                SetText(shipBoarding, link.Phase == BoardingPhase.Hooks
                    ? (attacking ? "Haki – liny" : "Haki wroga – tniemy liny") + $" {Mathf.RoundToInt((float)link.CutProgress * 100)}%"
                    : $"Walka na burtach · front {(attacking ? link.AttackerFront : link.TargetFront)}");
            }
            var morale = Mathf.RoundToInt((float)ship.Morale);
            SetText(shipMorale, $"{morale}%");
            moraleGlyph.style.backgroundImage = Icon(morale >= 70 ? "morale-high" : morale >= 40 ? "morale-mid" : "morale-low");

            // Cargo and orders of an enemy are not revealed by sight alone.
            var capacity = definition.Capacity * Rules.Centi;
            Show(shipCargoRow, capacity > 0 && !enemy);
            if (capacity > 0 && !enemy)
            {
                var held = ship.Cargo + ship.HoldWood + ship.HoldSilver;
                var silver = ship.HoldSilver > 0 || (ship.CargoType == ResourceType.Silver && ship.Cargo > 0);
                shipCargoMeter.Set((float)held / capacity, silver ? HudPalette.Silver : HudPalette.Timber);
                var amount = ship.HoldWood + ship.HoldSilver > 0 ? BattleController.DescribeCargo(ship) : ship.Cargo > 0 ? $"{ship.Cargo / Rules.Centi} {(ship.CargoType == ResourceType.Wood ? "D" : "S")}" : "pusta";
                SetText(shipCargo, $"{amount} / {definition.Capacity}");
            }
            SetText(shipPhase, enemy ? "Stan widoczny, dopóki twoje statki go obserwują. Rozkazy wroga są nieznane." : battle.DescribeOrder(ship).Replace("Rozkaz: ", string.Empty));
        }

        void FillGroup(List<Ship> ships)
        {
            for (var i = 0; i < unitCards.Count; i++)
            {
                var (card, portrait, name, meter) = unitCards[i];
                var shown = i < ships.Count && (ships.Count <= MaxCards || i < MaxCards - 1);
                Show(card, shown);
                if (!shown) continue;
                var ship = ships[i];
                card.userData = ship.Id;
                portrait.style.backgroundImage = Portrait(ship.Class);
                SetText(name, ShortClass(ship.Class));
                var hull = (float)(ship.Hull / ship.Definition.Hull);
                meter.Set(hull, HudPalette.Hull(hull), patterns && hull <= 0.3f);
            }
            var hidden = ships.Count > MaxCards ? ships.Count - (MaxCards - 1) : 0;
            Show(moreCard, hidden > 0);
            SetText(moreLabel, $"+{hidden}");

            var classes = ships.GroupBy(ship => ship.Class).OrderByDescending(group => group.Count()).ToList();
            for (var i = 0; i < classCounts.Count; i++)
            {
                var (row, glyph, text) = classCounts[i];
                Show(row, i < classes.Count);
                if (i >= classes.Count) continue;
                glyph.style.backgroundImage = Icon(ClassIcon(classes[i].Key));
                SetText(text, $"{classes[i].Count()} × {BattleController.ClassName(classes[i].Key)}");
            }
            var hullShare = ships.Sum(ship => ship.Hull) / ships.Sum(ship => ship.Definition.Hull);
            SetText(groupHull, $"{Mathf.RoundToInt((float)hullShare * 100f)}%");
            SetText(groupCrew, $"{ships.Sum(ship => ship.Sailors + ship.Soldiers)} załogi");
        }

        void ClickUnit(Button card, bool shift)
        {
            if (!(card.userData is int id)) return;
            if (shift) battle.Deselect(id); else battle.SelectOnly(id);
            Refresh();
        }

        TipInfo UnitTip(Button card)
        {
            if (!(card.userData is int id)) return default;
            var ship = battle.World.FindShip(id);
            if (ship == null) return default;
            return new TipInfo
            {
                Title = BattleController.ClassName(ship.Class),
                Body = $"Kadłub {Mathf.CeilToInt((float)ship.Hull)}/{Mathf.CeilToInt((float)ship.Definition.Hull)}, załoga {ship.Sailors + ship.Soldiers}. {battle.DescribeOrder(ship)}",
                Key = "Klik: tylko ten · Shift+klik: usuń z zaznaczenia",
            };
        }

        void OpenFullSelection()
        {
            var ships = OwnSelected();
            var panel = OpenPopover(moreCard, $"Zaznaczenie: {ships.Count}");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 360;
            panel.Add(scroll);
            foreach (var ship in ships)
            {
                var id = ship.Id;
                var hull = Mathf.RoundToInt((float)(ship.Hull / ship.Definition.Hull) * 100f);
                var item = MenuItem(scroll, ClassIcon(ship.Class), BattleController.ClassName(ship.Class), $"{hull}%", null);
                item.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.shiftKey) battle.Deselect(id); else battle.SelectOnly(id);
                    ClosePopover();
                    Refresh();
                });
            }
            Text("Klik: tylko ten · Shift+klik: usuń z zaznaczenia.", "popover-note", panel);
        }

        void FillPort(Settlement port)
        {
            var player = BattleController.PlayerFaction;
            var own = port.Owner == player;
            portPortrait.style.backgroundImage = Icon(port.IsOutpost ? "outpost" : "settlement", 128);
            portPortrait.EnableInClassList("enemy", !own);
            var hull = (float)(port.Hull / port.MaxHull);
            portHullMeter.Set(hull, HudPalette.Hull(hull), patterns && hull <= 0.3f);
            SetText(portHullValue, $"{Mathf.CeilToInt((float)port.Hull)} / {Mathf.CeilToInt((float)port.MaxHull)}");
            Show(portStores, own);
            if (own)
            {
                portWoodMeter.Set((float)port.Wood / Math.Max(1L, port.Capacity(ResourceType.Wood)), HudPalette.Timber);
                portSilverMeter.Set((float)port.Silver / Math.Max(1L, port.Capacity(ResourceType.Silver)), HudPalette.Silver);
                SetText(portWoodValue, $"{port.Wood / Rules.Centi} / {port.Capacity(ResourceType.Wood) / Rules.Centi}");
                SetText(portSilverValue, $"{port.Silver / Rules.Centi} / {port.Capacity(ResourceType.Silver) / Rules.Centi}");
                SetText(portBerths, $"Stanowiska {port.DockedShips.Count}/{Rules.PortBerths}" + (port.PortWaiting.Count > 0 ? $" · czeka {port.PortWaiting.Count}" : string.Empty));
                var yard = port.HasShipyard ? "Stocznia" : port.ShipyardBuild != null ? $"Stocznia: budowa {Mathf.RoundToInt(100f * port.ShipyardBuild.DoneTicks / port.ShipyardBuild.TotalTicks)}%" : "Brak stoczni";
                SetText(portYard, $"{yard} · {WorksSummary(port)}");
                Show(portWorkshopButton, port.HasWorkshop);
            }
            else
            {
                SetText(portBerths, "Magazyny, kolejki i stocznia wroga są nieznane.");
                SetText(portYard, string.Empty);
                Show(portWorkshopButton, false);
            }
            Show(portBuildButton, own);
            Show(portConvoyButton, own);
        }

        /// <summary>One line: the project under way or the buildings standing, the workshop's current job and the arsenal.</summary>
        string WorksSummary(Settlement port)
        {
            var parts = new List<string>();
            if (port.Project != null) parts.Add($"{BattleController.ProjectName(port.ProjectKind)} {Mathf.FloorToInt(100f * port.Project.DoneTicks / Mathf.Max(1, port.Project.TotalTicks))}%");
            else
            {
                var standing = new List<string>();
                if (port.HasWorkshop) standing.Add("Warsztat");
                if (port.IsTown) standing.Add("Miasto");
                if (port.HasDock) standing.Add("Dok");
                if (port.HasSageSchool) standing.Add("Szkoła");
                parts.Add(standing.Count > 0 ? string.Join(" · ", standing) : "Bez rozbudowy");
            }
            if (port.WorkshopQueue.Count > 0)
            {
                var order = port.WorkshopQueue[0];
                parts.Add($"{WeaponCatalog.Get(order.Weapon).Name} {Mathf.FloorToInt(100f * order.DoneTicks / Mathf.Max(1, order.TotalTicks))}%" + (port.WorkshopQueue.Count > 1 ? $" (+{port.WorkshopQueue.Count - 1})" : string.Empty));
            }
            var stored = port.Arsenal.Sum();
            if (stored > 0) parts.Add($"arsenał {stored}");
            return string.Join(" · ", parts);
        }

        // ---------- Production ----------

        void RefreshQueues()
        {
            var port = battle.ActivePort;
            if (port == null) return;
            FillQueue(warRow, port, port.WarQueue);
            FillQueue(portRow, port, port.PortQueue);
        }

        void FillQueue(QueueRow row, Settlement port, List<BuildOrder> queue)
        {
            var noYard = row.War && !port.HasShipyard;
            for (var i = 0; i < row.Slots.Count; i++)
            {
                var (item, glyph, stage, time, bar) = row.Slots[i];
                var shown = i < queue.Count && !noYard;
                Show(item, shown);
                if (!shown) continue;
                var order = queue[i];
                var progress = order.TotalTicks > 0 ? (float)order.DoneTicks / order.TotalTicks : 0f;
                glyph.style.backgroundImage = Icon(ClassIcon(order.Class));
                item.EnableInClassList("current", i == 0);
                Show(stage, i == 0);
                if (i == 0) stage.style.backgroundImage = Icon(StageIcon(progress));
                SetText(time, i == 0 ? $"{Mathf.CeilToInt(BattleController.SecondsLeft(order))}s" : string.Empty);
                bar.Set(i == 0 ? progress : 0f, HudPalette.Warning);
            }
            if (noYard)
            {
                SetText(row.Empty, port.ShipyardBuild != null ? $"Stocznia w budowie: {Mathf.RoundToInt(100f * port.ShipyardBuild.DoneTicks / port.ShipyardBuild.TotalTicks)}%" : "Brak stoczni");
                Show(row.Empty, true);
            }
            else
            {
                SetText(row.Empty, queue.Count == 0 ? "Wolna" : queue.Count > MaxQueueShown ? $"+{queue.Count - MaxQueueShown}" : string.Empty);
                Show(row.Empty, queue.Count == 0 || queue.Count > MaxQueueShown);
            }
            Show(row.Add, !noYard || (port.IsOutpost && port.ShipyardBuild == null));
        }

        TipInfo QueueTip(bool war, int index)
        {
            var port = battle.ActivePort;
            var queue = port == null ? null : war ? port.WarQueue : port.PortQueue;
            if (queue == null || index >= queue.Count) return default;
            var order = queue[index];
            var progress = order.TotalTicks > 0 ? (float)order.DoneTicks / order.TotalTicks : 0f;
            return new TipInfo
            {
                Title = $"{BattleController.ClassName(order.Class)} · zamówienie {index + 1} linii {(war ? "wojennej" : "portowej")}",
                Body = index == 0
                    ? $"Na pochylni: {BattleController.StageName(progress)}, {Mathf.FloorToInt(progress * 100f)}%. Do wodowania {Mathf.CeilToInt(BattleController.SecondsLeft(order))} s."
                    : $"Czeka w kolejce; budowa {Mathf.CeilToInt(order.TotalTicks / (float)Rules.TickRate)} s.",
                Key = $"PPM lub {KeyBindings.Label(KeyBindings.Key(GameAction.CancelOrder))}: anuluj (powyżej 50% z potwierdzeniem)",
            };
        }

        bool FocusedQueueItem(out bool war, out int index)
        {
            war = false;
            index = -1;
            if (!(root.panel?.focusController?.focusedElement is VisualElement focused) || !(focused.userData is ValueTuple<bool, int> slot)) return false;
            (war, index) = slot;
            return true;
        }

        /// <summary>
        /// Cancels an order. Past half way a confirmation shows the settlement computed by the simulation's own
        /// cancel rule; earlier ones cancel at once and the herald states what came back.
        /// </summary>
        public void RequestCancel(bool war, int index)
        {
            var port = battle.ActivePort;
            if (port == null) return;
            var quote = battle.World.QuoteCancel(port, !war, index);
            if (!quote.Valid) return;
            var queue = war ? port.WarQueue : port.PortQueue;
            var name = BattleController.ClassName(queue[index].Class);
            var returned = $"{quote.ReturnedWood / Rules.Centi} D · {quote.ReturnedSilver / Rules.Centi} S";
            if (quote.Progress > 0.5)
            {
                OpenConfirm($"Anulować: {name}?", $"Budowa jest zaawansowana ({Mathf.FloorToInt((float)quote.Progress * 100f)}%). Rozliczenie według zasad gry:",
                    new[]
                    {
                        ("Zwolniona rezerwacja", $"{quote.ReleasedWood / Rules.Centi} D · {quote.ReleasedSilver / Rules.Centi} S"),
                        ("Wraca do magazynu", returned),
                        ("Strata organizacyjna", $"{quote.LostWood / Rules.Centi} D · {quote.LostSilver / Rules.Centi} S"),
                    },
                    "Anuluj zamówienie", () => battle.CancelQueue(war, index), true);
                return;
            }
            battle.CancelQueue(war, index);
            OnHerald(new HeraldMessage($"Anulowano: {name}. Wraca {returned}.", HeraldLevel.Info, null, HeraldTopic.General));
        }

        /// <summary>Templates with complete price and time; ordering checks the yard, the funds and the protected stock.</summary>
        void OpenProduction(bool? war, VisualElement anchor)
        {
            var port = battle.ActivePort;
            if (port == null) return;
            var panel = OpenPopover(anchor, $"Zamów – {battle.PortName(port)}");
            var fleet = battle.World.Ships.Count(ship => ship.Owner == BattleController.PlayerFaction);
            productionClasses = 0;
            if (!war.HasValue || war.Value)
            {
                // Warships with their class's weapons, or bare hulls fitted later from the plan and the workshop (docs/UZBROJENIE.md).
                var armed = battle.DefaultArming;
                var arming = MenuItem(panel, armed ? "check" : "close", "Domyślne uzbrojenie", armed ? "tak" : "nie", () =>
                {
                    battle.DefaultArming = !battle.DefaultArming;
                    OpenProduction(war, anchor);
                });
                arming.AddToClassList("compact");
                Tip(arming, () => new TipInfo
                {
                    Title = "Domyślne uzbrojenie",
                    Body = "Tak: okręt wodowany z bronią swojej klasy, w cenie kompletnego szablonu. Nie: kadłub tańszy dokładnie o wartość tej broni, z pustymi gniazdami i pustym planem uzbrojenia; broń dokłada potem plan uzbrojenia i Warsztat portu. Taran zostaje.",
                    Key = "Klik: przełącz",
                });
            }
            foreach (var shipClass in new[] { ShipClass.Cutter, ShipClass.Merchant, ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder, ShipClass.ShipKiller, ShipClass.Bomber })
            {
                var definition = ShipCatalog.Get(shipClass);
                if (war.HasValue && war.Value == definition.IsEconomic) continue;
                var chosen = shipClass;
                var bare = Bare(definition);
                var (wood, silver) = World.OrderPrice(definition, bare);
                var item = MenuItem(panel, ClassIcon(shipClass), BattleController.ClassName(shipClass), $"{wood / Rules.Centi} D · {silver / Rules.Centi} S · {definition.BuildSeconds:0} s", () => Order(chosen));
                var problem = OrderProblem(port, definition, fleet);
                item.EnableInClassList("disabled", problem != null);
                productionClasses++;
                Tip(item, () => new TipInfo
                {
                    Title = BattleController.ClassName(chosen),
                    Body = $"Kadłub {definition.Hull}, prędkość {definition.Speed:0.#}, załoga do {definition.MaxCrew}" + (definition.Capacity > 0 ? $", ładownia {definition.Capacity}." : ".") + (problem != null ? "\n" + problem : string.Empty),
                    Cost = bare
                        ? $"Kadłub bez domyślnej broni: {wood / Rules.Centi} D · {silver / Rules.Centi} S · {definition.BuildSeconds:0} s (kompletny szablon: {definition.CostWood} D · {definition.CostSilver} S)"
                        : $"Cena kompletnego szablonu (z obsadą i uzbrojeniem): {definition.CostWood} D · {definition.CostSilver} S · {definition.BuildSeconds:0} s",
                    Key = definition.IsEconomic ? "Linia portowa" : "Linia wojenna",
                });
            }
            if (!war.HasValue || war.Value)
            {
                // Fleet blueprints: a hull at its class price, its other weapons ordered from the workshop, its crew trained.
                foreach (var blueprint in battle.World.Blueprints.Where(item => item.Owner == BattleController.PlayerFaction))
                {
                    var chosen = blueprint;
                    var definition = ShipCatalog.Get(blueprint.Class);
                    var extra = blueprint.Loadout.Where((weapon, i) => weapon != (definition.DefaultLoadout.Length > i ? definition.DefaultLoadout[i] : WeaponType.None) && WeaponCatalog.Get(weapon).IsMachine).ToList();
                    var extraWood = extra.Sum(weapon => WeaponCatalog.Get(weapon).CostWood);
                    var extraSilver = extra.Sum(weapon => WeaponCatalog.Get(weapon).CostSilver);
                    var item = MenuItem(panel, ClassIcon(blueprint.Class), $"Szablon: {blueprint.Name}",
                        $"{definition.CostWood} D · {definition.CostSilver} S" + (extra.Count > 0 ? $" + broń {extraWood} D · {extraSilver} S" : string.Empty),
                        () => { battle.QueueBlueprint(chosen.Id); ClosePopover(); Refresh(); });
                    var problem = OrderProblem(port, definition, fleet);
                    item.EnableInClassList("disabled", problem != null);
                    Tip(item, () => new TipInfo
                    {
                        Title = $"Szablon: {chosen.Name}",
                        Body = $"{BattleController.ClassName(chosen.Class)}, skład {BattleController.PresetName(chosen.Preset)}; broń: {string.Join(", ", chosen.Loadout.Select(weapon => WeaponCatalog.Get(weapon).Name))}."
                            + " Kadłub w cenie klasy; brakującą broń port od razu zamawia w Warsztacie, a po wodowaniu przegrupowanie zakłada ją i szkoli załogę." + (problem != null ? "\n" + problem : string.Empty),
                    });
                }
                var selectedShip = battle.Selected.Count == 1 ? battle.World.FindShip(battle.Selected.First()) : null;
                if (selectedShip != null && selectedShip.Owner == BattleController.PlayerFaction && !selectedShip.Definition.IsEconomic)
                {
                    var name = $"{BattleController.ClassName(selectedShip.Class)} {battle.World.Blueprints.Count(item => item.Owner == BattleController.PlayerFaction) + 1}";
                    var save = MenuItem(panel, "save", $"Zapisz szablon: {name}", "z zaznaczonego okrętu", () => { battle.SaveBlueprint(selectedShip.Id, name); ClosePopover(); });
                    Tip(save, () => new TipInfo { Title = "Szablon floty", Body = $"Zapisuje klasę, plan uzbrojenia i skład załogi zaznaczonego okrętu (najwyżej {Rules.MaxBlueprints} szablonów)." });
                }
            }
            if ((!war.HasValue || war.Value) && port.IsOutpost && !port.HasShipyard && port.ShipyardBuild == null)
            {
                var affordable = port.Available(ResourceType.Wood) >= Rules.ShipyardWood && port.Available(ResourceType.Silver) >= Rules.ShipyardSilver;
                var item = MenuItem(panel, "shipyard", "Stocznia (linia wojenna)", $"{Rules.ShipyardWood / Rules.Centi} D · {Rules.ShipyardSilver / Rules.Centi} S · {Rules.ShipyardBuildSeconds:0} s", () => { battle.BuildShipyard(); ClosePopover(); Refresh(); });
                item.EnableInClassList("disabled", !affordable);
            }
            Text("Brak stoczni nie daje darmowej linii wojennej. Zapas chroniony 60 D / 20 S jest częścią dostępnych środków.", "popover-note", panel);
        }

        /// <summary>A warship ordered as a bare hull, when the player turned the default weapons off.</summary>
        bool Bare(ShipDefinition definition) => !battle.DefaultArming && !definition.IsEconomic && definition.Slots.Length > 0;

        int productionClasses;

        /// <summary>Opens the order menu (war line, port line or both) and returns how many classes it lists (tests).</summary>
        public int OpenProductionForTest(bool? war)
        {
            OpenProduction(war, portBuildButton);
            return productionClasses;
        }

        /// <summary>The values (prices, states) of the items the open popover lists, in order (tests).</summary>
        public IReadOnlyList<string> PopoverValuesForTest =>
            popover == null ? (IReadOnlyList<string>)Array.Empty<string>() : popover.Query<Label>(className: "menu-item-value").ToList().Select(label => label.text).ToList();

        string OrderProblem(Settlement port, ShipDefinition definition, int fleet)
        {
            var requirement = battle.World.ShipProblem(port, definition.Class);
            if (requirement != null) return requirement;
            if (fleet >= Rules.FleetLimit) return $"Flota osiągnęła limit {Rules.FleetLimit}.";
            var (priceWood, priceSilver) = World.OrderPrice(definition, Bare(definition));
            var wood = priceWood - port.Available(ResourceType.Wood);
            var silver = priceSilver - port.Available(ResourceType.Silver);
            if (wood > 0 || silver > 0) return $"Brakuje: {Math.Max(0, wood) / Rules.Centi} D · {Math.Max(0, silver) / Rules.Centi} S.";
            return null;
        }

        void Order(ShipClass shipClass)
        {
            var port = battle.ActivePort;
            if (port == null) return;
            var definition = ShipCatalog.Get(shipClass);
            var problem = OrderProblem(port, definition, battle.World.Ships.Count(ship => ship.Owner == BattleController.PlayerFaction));
            if (problem != null) { OnHerald(new HeraldMessage($"Nie można zamówić: {BattleController.ClassName(shipClass)}. {problem}", HeraldLevel.Info, null, HeraldTopic.General)); return; }
            var bare = Bare(definition);
            var (priceWood, priceSilver) = World.OrderPrice(definition, bare);
            var leftWood = port.Available(ResourceType.Wood) - priceWood;
            var leftSilver = port.Available(ResourceType.Silver) - priceSilver;
            ClosePopover();
            if (leftWood < Rules.ReserveWood || leftSilver < Rules.ReserveSilver)
            {
                OpenConfirm("Naruszenie zapasu chronionego", $"Po zamówieniu ({BattleController.ClassName(shipClass)}) w porcie zostanie {leftWood / Rules.Centi} D · {leftSilver / Rules.Centi} S, mniej niż zapas chroniony {Rules.ReserveWood / Rules.Centi} D · {Rules.ReserveSilver / Rules.Centi} S.",
                    null, "Zamów mimo to", () => battle.QueueShip(shipClass, bare), false);
                return;
            }
            battle.QueueShip(shipClass, bare);
            Refresh();
        }

        // ---------- Commands ----------

        void RefreshCommands()
        {
            var ships = OwnSelected();
            foreach (var command in commands)
            {
                var able = ships.Count > 0 && ships.Any(command.Able);
                Show(command.Button, able);
                if (!able) continue;
                command.Button.EnableInClassList("selected", command.Targeted != HudOrder.None && battle.PendingOrder == command.Targeted);
                SetText(command.Hotkey, KeyBindings.Label(KeyBindings.Key(command.Action)));
            }
            Show(commandsEmpty, ships.Count == 0);
            var group = ships.Count > 1;
            Show(formationButton, group);
            if (group) formationButton.text = FormationLabel(battle.GroupFormation);
        }

        /// <summary>The formation switch's text, which names the formation a group move takes now.</summary>
        public static string FormationLabel(FormationKind kind) => kind == FormationKind.Wedge ? "Szyk: klin" : "Szyk: linia";

        /// <summary>The formation switch as the interface shows it: hidden for one ship, its text for a group.</summary>
        public string FormationSwitchForTest => formationButton != null && formationButton.style.display != DisplayStyle.None ? formationButton.text : null;

        TipInfo FormationTip() => new TipInfo
        {
            Title = "Szyk grupy",
            Body = battle.GroupFormation == FormationKind.Wedge
                ? "Klin: prowadzący płynie do wskazanego punktu, reszta w dwóch skrzydłach za nim. Kliknięcie zmienia szyk na linię."
                : "Linia: statki stają burta przy burcie w poprzek kursu, z miejscem na wiosła. Kliknięcie zmienia szyk na klin.",
            Key = $"Skrót: {KeyBindings.Label(KeyBindings.Key(GameAction.Formation))} · dotyczy rozkazu ruchu dla grupy",
        };

        TipInfo CommandTip(CommandButton command)
        {
            var ships = OwnSelected();
            var able = ships.Where(command.Able).ToList();
            var scope = ships.Count > 1 ? $"\nDotyczy {able.Count} z {ships.Count} statków" + (able.Count < ships.Count ? $" ({string.Join(", ", able.GroupBy(ship => ship.Class).Select(group => BattleController.ClassName(group.Key)))})." : ".") : string.Empty;
            return new TipInfo
            {
                Title = command.Full,
                Body = command.Body + scope,
                Key = $"Skrót: {KeyBindings.Label(KeyBindings.Key(command.Action))}" + (command.Targeted != HudOrder.None ? " · potem LPM na celu, Esc anuluje" : string.Empty),
            };
        }

        TipInfo HelpTip() => new TipInfo
        {
            Title = "Rozkazy",
            Body = "Kolejność stała: " + string.Join(", ", commands.Select(command => $"{command.Label} {KeyBindings.Label(KeyBindings.Key(command.Action))}")) + ". Alt+PPM: ostrzał abordażowcem. PPM na mapie wybiera czynność według celu.",
            Key = $"{KeyBindings.Label(KeyBindings.Key(GameAction.Pause))}: pauza · {KeyBindings.Label(KeyBindings.Key(GameAction.FocusAlert))}: do alarmu · {KeyBindings.Label(KeyBindings.Key(GameAction.CollapseHud))}: zwiń panele",
        };

        // ---------- Cursor ----------

        void UpdateCursor()
        {
            string name = null;
            if (!HoldsInput && !IsPointerOverInterface(GameInput.MousePosition))
            {
                var order = battle.PendingOrder;
                name = order != HudOrder.None ? CursorFor(order) : battle.ContextCursor(GameInput.MousePosition);
            }
            if (name == lastCursor) return;
            lastCursor = name;
            var texture = name == null ? null : Load($"Hud/Cursors/{name}-32");
            UnityEngine.Cursor.SetCursor(texture, texture == null ? Vector2.zero : new Vector2(16f, 16f), CursorMode.Auto);
        }

        static string CursorFor(HudOrder order)
        {
            switch (order)
            {
                case HudOrder.Move: return "move";
                case HudOrder.Ram: return "ram";
                case HudOrder.Harvest: return "wood";
                case HudOrder.Deliver: return "deliver";
                case HudOrder.Outpost: return "outpost";
                case HudOrder.Escort: return "escort";
                default: return "attack";
            }
        }

        // ---------- World overlays ----------

        void UpdateHealthBars()
        {
            var camera = battle.StrategicCamera;
            var panel = root.panel;
            var used = 0;
            if (camera != null && panel != null)
            {
                foreach (var ship in battle.World.Ships)
                {
                    if (!battle.ShowsHealthBar(ship)) continue;
                    var at = battle.Views.TryGetValue(ship.Id, out var view) ? view.transform.position : new Vector3((float)ship.X, 0f, (float)ship.Y);
                    var radius = (float)ship.Definition.Radius;
                    if (!Place(camera, panel, at + Vector3.up * (radius * 0.6f + 3f), radius, out var position, out var width)) continue;
                    var bar = Bar(used++);
                    var hull = (float)(ship.Hull / ship.Definition.Hull);
                    var own = ship.Owner == BattleController.PlayerFaction;
                    bar.hull.Set(hull, own ? HudPalette.Hull(hull) : HudPalette.Enemy, patterns && !own);
                    bar.crew.Set((float)ship.Crew / Mathf.Max(1, ship.Definition.MaxCrew), HudPalette.Warning);
                    bar.crew.style.display = DisplayStyle.Flex;
                    bar.root.style.width = width;
                    bar.root.style.translate = new StyleTranslate(new Translate(position.x - width * 0.5f, position.y));
                }
                var inspected = battle.World.FindSettlement(battle.InspectedSettlementId);
                if (inspected != null && !inspected.Destroyed)
                {
                    var at = new Vector3((float)Rules.SettlementLandX(inspected), 12f, (float)inspected.Y);
                    if (Place(camera, panel, at, 22f, out var position, out var width))
                    {
                        var bar = Bar(used++);
                        var hull = (float)(inspected.Hull / inspected.MaxHull);
                        bar.hull.Set(hull, inspected.Owner == BattleController.PlayerFaction ? HudPalette.Hull(hull) : HudPalette.Enemy, patterns && inspected.Owner != BattleController.PlayerFaction);
                        bar.crew.style.display = DisplayStyle.None;
                        bar.root.style.width = width * 1.3f;
                        bar.root.style.translate = new StyleTranslate(new Translate(position.x - width * 0.65f, position.y));
                    }
                }
            }
            for (var i = used; i < bars.Count; i++) Show(bars[i].root, false);
            VisibleHealthBars = used;
            UpdateBoardingMarkers(camera, panel);
        }

        readonly List<(VisualElement root, Label ours, Label sep, Label theirs)> boardingMarkers = new List<(VisualElement, Label, Label, Label)>();

        /// <summary>Boarding markers now shown over the map (tests).</summary>
        public int VisibleBoardingMarkers { get; private set; }

        /// <summary>The text of the first boarding marker shown, or empty (tests).</summary>
        public string BoardingMarkerText => VisibleBoardingMarkers > 0 ? $"{boardingMarkers[0].ours.text}{boardingMarkers[0].sep.text}{boardingMarkers[0].theirs.text}" : string.Empty;

        /// <summary>
        /// Over every boarding the player can see: crossed blades and, while the hooks are fought over, how far they
        /// have bitten; once the decks fight, the men at each side's front in that side's colour.
        /// </summary>
        void UpdateBoardingMarkers(Camera camera, IPanel panel)
        {
            var used = 0;
            if (camera != null && panel != null)
            {
                foreach (var link in battle.World.Boardings)
                {
                    if (!battle.Views.TryGetValue(link.AttackerId, out var attackerView) || !battle.Views.TryGetValue(link.TargetId, out var targetView)) continue;
                    if (!attackerView.gameObject.activeSelf || !targetView.gameObject.activeSelf) continue;
                    var attacker = battle.World.FindShip(link.AttackerId);
                    var target = battle.World.FindShip(link.TargetId);
                    if (attacker == null || target == null) continue;
                    var at = (attackerView.transform.position + targetView.transform.position) * 0.5f + Vector3.up * 9f;
                    if (!Place(camera, panel, at, 10f, out var position, out _)) continue;
                    var marker = BoardingMarker(used++);
                    var player = BattleController.PlayerFaction;
                    if (link.Hooked)
                    {
                        SetText(marker.ours, link.AttackerFront.ToString());
                        SetText(marker.sep, " : ");
                        SetText(marker.theirs, link.TargetFront.ToString());
                    }
                    else
                    {
                        var total = Mathf.Max(1f, (float)(Rules.BoardHookSeconds * Rules.TickRate));
                        SetText(marker.ours, $"haki {Mathf.RoundToInt(100f * (1f - link.HookTicks / total))}%");
                        SetText(marker.sep, string.Empty);
                        SetText(marker.theirs, string.Empty);
                    }
                    marker.ours.style.color = attacker.Owner == player ? HudPalette.Ally : HudPalette.Enemy;
                    marker.theirs.style.color = target.Owner == player ? HudPalette.Ally : HudPalette.Enemy;
                    var width = marker.root.layout.width > 0f ? marker.root.layout.width : 80f;
                    marker.root.style.translate = new StyleTranslate(new Translate(position.x - width * 0.5f, position.y));
                }
            }
            for (var i = used; i < boardingMarkers.Count; i++) Show(boardingMarkers[i].root, false);
            VisibleBoardingMarkers = used;
        }

        (VisualElement root, Label ours, Label sep, Label theirs) BoardingMarker(int index)
        {
            while (boardingMarkers.Count <= index)
            {
                var element = Element("boarding-marker", worldLayer);
                element.pickingMode = PickingMode.Ignore;
                Glyph(element, "board", "glyph-small").pickingMode = PickingMode.Ignore;
                var ours = Text(string.Empty, "boarding-marker-value", element);
                var sep = Text(string.Empty, "boarding-marker-value", element);
                var theirs = Text(string.Empty, "boarding-marker-value", element);
                ours.style.marginLeft = 4;
                boardingMarkers.Add((element, ours, sep, theirs));
            }
            var marker = boardingMarkers[index];
            Show(marker.root, true);
            return marker;
        }

        static bool Place(Camera camera, IPanel panel, Vector3 world, float radius, out Vector2 position, out float width)
        {
            position = default;
            width = 0f;
            if (camera.WorldToScreenPoint(world).z <= 0f) return false;
            position = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, camera);
            var side = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world + camera.transform.right * radius, camera);
            width = Mathf.Clamp(Vector2.Distance(position, side) * 1.6f, 36f, 120f);
            return true;
        }

        (VisualElement root, MeterBar hull, MeterBar crew) Bar(int index)
        {
            while (bars.Count <= index)
            {
                var element = Element("hp", worldLayer);
                element.pickingMode = PickingMode.Ignore;
                var hull = new MeterBar(HudPalette.Healthy, 5);
                hull.AddToClassList("hp-hull");
                element.Add(hull);
                var crew = new MeterBar(HudPalette.Warning);
                crew.AddToClassList("hp-crew");
                element.Add(crew);
                bars.Add((element, hull, crew));
            }
            var bar = bars[index];
            Show(bar.root, true);
            return bar;
        }

        void UpdateSelectionBox()
        {
            if (!battle.IsDragging || root.panel == null) { Show(selectBox, false); return; }
            var rect = battle.DragScreenRect;
            var a = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(rect.xMin, Screen.height - rect.yMax));
            var b = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(rect.xMax, Screen.height - rect.yMin));
            Show(selectBox, true);
            selectBox.style.left = a.x;
            selectBox.style.top = a.y;
            selectBox.style.width = b.x - a.x;
            selectBox.style.height = b.y - a.y;
        }

        // ---------- Herald ----------

        void OnHerald(HeraldMessage message)
        {
            if (heraldColumn == null) return;
            // The same kind of news about the same port or convoy joins the entry already shown. An alarm waits for
            // confirmation, so later alarms of its group join it however long it has been shown.
            var same = heralds.FirstOrDefault(item => !item.Acknowledged && item.Message.GroupKey == message.GroupKey
                && (message.Level == HeraldLevel.Critical && item.Message.Level == HeraldLevel.Critical || Time.unscaledTime - item.Born < HeraldMerge + (item.Count - 1) * 2f));
            if (same != null)
            {
                same.Count++;
                same.Message = message;
                same.Until = Time.unscaledTime + HeraldInformational;
                same.Texts.Add(message.Text);
                if (message.EntityId >= 0) same.Entities.Add(message.EntityId);
                SetText(same.Title, MergedText(same));
                SetText(same.Sub, $"×{same.Count} · {SubText(message)}");
                return;
            }

            var herald = new Herald { Message = message, Born = Time.unscaledTime, Until = Time.unscaledTime + HeraldInformational };
            herald.Texts.Add(message.Text);
            if (message.EntityId >= 0) herald.Entities.Add(message.EntityId);
            var item = new Button { focusable = true };
            item.AddToClassList("herald-item");
            if (message.Level == HeraldLevel.Important) item.AddToClassList("important");
            if (message.Level == HeraldLevel.Critical) item.AddToClassList("critical");
            var icon = Element("herald-icon", item);
            icon.pickingMode = PickingMode.Ignore;
            var glyph = Element("herald-icon-glyph", icon);
            glyph.pickingMode = PickingMode.Ignore;
            glyph.style.backgroundImage = Icon(message.Level == HeraldLevel.Critical || message.Topic == HeraldTopic.Alarm ? "warning" : message.Topic == HeraldTopic.Launch ? "shipyard" : "check");
            var text = Element("herald-text", item);
            text.pickingMode = PickingMode.Ignore;
            herald.Title = Text(message.Text, "herald-title", text);
            herald.Sub = Text(SubText(message), "herald-sub", text);
            herald.Key = Text(message.Level == HeraldLevel.Critical ? KeyBindings.Label(KeyBindings.Key(GameAction.FocusAlert)) : string.Empty, "herald-key", item);
            herald.Element = item;
            item.clicked += () => GoTo(herald);
            Tip(item, () => new TipInfo { Title = herald.Message.Text, Body = herald.Message.Level == HeraldLevel.Critical ? "Pozostaje do potwierdzenia." : "Znika po 8 s.", Key = "Klik: pokaż miejsce zdarzenia" });

            heraldColumn.Insert(0, item);
            heralds.Insert(0, herald);
            if (message.At.HasValue && message.Topic == HeraldTopic.Alarm) minimap?.Alarm(message.At.Value);
            while (heralds.Count > MaxHeralds)
            {
                // Drop the oldest that is not a critical waiting for confirmation, else the oldest.
                var victim = heralds.LastOrDefault(item2 => item2.Message.Level != HeraldLevel.Critical) ?? heralds[heralds.Count - 1];
                victim.Element.RemoveFromHierarchy();
                heralds.Remove(victim);
            }
        }

        /// <summary>
        /// The title of a merged entry: the latest text while it is all one kind of news; an alarm about several
        /// different ships counts them, one about ships and ports together counts the targets.
        /// </summary>
        string MergedText(Herald herald)
        {
            if (herald.Texts.Count <= 1 || herald.Message.Topic != HeraldTopic.Alarm || herald.Entities.Count < 2) return herald.Message.Text;
            var count = herald.Entities.Count;
            if (herald.Entities.All(id => battle.World.FindSettlement(id) == null)) return $"Atak na {count} {ShipsWord(count)}!";
            return $"Ataki w okolicy: {count} {(count % 10 >= 2 && count % 10 <= 4 && (count % 100 < 12 || count % 100 > 14) ? "cele" : "celów")}!";
        }

        /// <summary>Polish plural of "okręt" after a number of two or more.</summary>
        public static string ShipsWord(int count) => count % 10 >= 2 && count % 10 <= 4 && (count % 100 < 12 || count % 100 > 14) ? "okręty" : "okrętów";

        string SubText(HeraldMessage message)
        {
            if (message.EntityId >= 0)
            {
                var settlement = battle.World.FindSettlement(message.EntityId);
                if (settlement != null) return battle.PortName(settlement);
            }
            return message.At.HasValue ? NearestPortName(message.At.Value) : "Kronika potyczki";
        }

        string NearestPortName(Vector3 at)
        {
            var ports = battle.World.PortsOf(BattleController.PlayerFaction).ToList();
            if (ports.Count == 0) return "Na morzu";
            var nearest = ports.OrderBy(port => World.Distance(at.x, at.z, port.X, port.Y)).First();
            return World.Distance(at.x, at.z, nearest.X, nearest.Y) < 200 ? $"Wody: {battle.PortName(nearest)}" : "Na otwartym morzu";
        }

        void UpdateHeralds()
        {
            for (var i = heralds.Count - 1; i >= 0; i--)
            {
                var herald = heralds[i];
                if (herald.Message.Level == HeraldLevel.Critical && !herald.Acknowledged) continue;
                var left = herald.Until - Time.unscaledTime;
                if (left <= 0f)
                {
                    herald.Element.RemoveFromHierarchy();
                    heralds.RemoveAt(i);
                    continue;
                }
                herald.Element.style.opacity = reducedMotion ? 1f : Mathf.Clamp01(left);
            }
        }

        /// <summary>To the object of the event while it exists and may be shown, else to its saved position; acknowledges it.</summary>
        void GoTo(Herald herald)
        {
            var target = herald.Message.At;
            var ship = herald.Message.EntityId >= 0 ? battle.World.FindShip(herald.Message.EntityId) : null;
            if (ship != null && (ship.Owner == BattleController.PlayerFaction || battle.World.CanSee(BattleController.PlayerFaction, ship))) target = new Vector3((float)ship.X, 0f, (float)ship.Y);
            if (target.HasValue) battle.FocusCamera(target.Value);
            herald.Acknowledged = true;
            herald.Element.RemoveFromHierarchy();
            heralds.Remove(herald);
        }

        void FocusAlarm()
        {
            var alarm = heralds.FirstOrDefault(item => item.Message.Level == HeraldLevel.Critical && !item.Acknowledged);
            if (alarm != null) GoTo(alarm);
        }

        // ---------- Overlays: popovers, modals, settings ----------

        VisualElement OpenPopover(VisualElement anchor, string title)
        {
            ClosePopover();
            popover = Element("popover", popoverLayer);
            Text(title, "popover-title", popover);
            var panel = popover;
            void Place()
            {
                if (popover != panel) return;
                var box = anchor.worldBound;
                var size = panel.layout.size;
                var bounds = root.worldBound;
                var x = Mathf.Clamp(box.xMin, bounds.xMin + 16f, bounds.xMax - size.x - 16f);
                float y;
                // A button in the bottom band opens its panel above the band, never over the card it belongs to.
                if (bottomBand != null && bottomBand.Contains(anchor)) y = bottomBand.worldBound.yMin - size.y - 6f;
                else
                {
                    y = box.yMax + 6f;
                    if (y + size.y > bounds.yMax - 16f) y = box.yMin - size.y - 6f;
                }
                y = Mathf.Max(bounds.yMin + 16f, y);
                panel.style.left = x;
                panel.style.top = y;
            }
            // The panel's size is known only after layout; it is placed again once its geometry settles.
            panel.schedule.Execute(Place).StartingIn(0);
            panel.RegisterCallback<GeometryChangedEvent>(e => { if (e.oldRect.size != e.newRect.size) Place(); });
            return popover;
        }

        void ClosePopover()
        {
            popover?.RemoveFromHierarchy();
            popover = null;
        }

        static void PopoverRow(VisualElement panel, string label, string value)
        {
            var row = Element("popover-row", panel);
            Text(label, "popover-row-label", row);
            Text(value, "popover-row-value", row);
        }

        Button MenuItem(VisualElement panel, string icon, string label, string value, Action click)
        {
            var item = new Button(() => click?.Invoke()) { focusable = true };
            item.AddToClassList("menu-item");
            panel.Add(item);
            if (icon != null) Glyph(item, icon, "glyph-small");
            Text(label, "menu-item-label", item);
            if (value != null) Text(value, "menu-item-value", item);
            return item;
        }

        VisualElement OpenModal(string title, bool wide = false)
        {
            CloseModal();
            ClosePopover();
            HideTooltip();
            var backdrop = Element("modal-backdrop", modalLayer);
            modal = Element("modal", backdrop);
            if (wide) modal.AddToClassList("wide");
            Text(title, "modal-title", modal);
            return modal;
        }

        void CloseModal()
        {
            if (modal == null) return;
            modal.parent?.RemoveFromHierarchy();
            modal = null;
            modalConfirm = null;
            capturing = null;
            KeyBindings.Capturing = false;
            if (menuPaused) { battle.Paused = pausedBeforeMenu; menuPaused = false; }
        }

        void ConfirmModal()
        {
            var action = modalConfirm;
            CloseModal();
            action?.Invoke();
            Refresh();
        }

        Button ModalButton(VisualElement row, string label, Action click, string kind = null)
        {
            var button = new Button(click) { text = label, focusable = true };
            button.AddToClassList("text-button");
            if (kind != null) button.AddToClassList(kind);
            row.Add(button);
            return button;
        }

        void OpenConfirm(string title, string text, (string, string)[] rows, string confirm, Action action, bool danger)
        {
            var panel = OpenModal(title);
            Text(text, "modal-text", panel);
            if (rows != null) foreach (var (label, value) in rows) PopoverRow(panel, label, value);
            var buttons = Element("button-row", panel);
            ModalButton(buttons, "Wróć", CloseModal);
            var ok = ModalButton(buttons, confirm, ConfirmModal, danger ? "danger" : "primary");
            modalConfirm = action;
            ok.schedule.Execute(() => ok.Focus()).StartingIn(0);
        }

        void OpenMenu()
        {
            var panel = OpenModal("Menu potyczki");
            pausedBeforeMenu = battle.Paused;
            battle.Paused = true;
            menuPaused = true;
            var column = Element(null, panel);
            var resume = MenuItem(column, "right", "Wznów", "Esc", CloseModal);
            MenuItem(column, "save", "Zapisz", KeyBindings.Label(KeyBindings.Key(GameAction.Save)), () => { battle.QuickSave(BattleController.DefaultSavePath); CloseModal(); });
            MenuItem(column, "load", "Wczytaj", KeyBindings.Label(KeyBindings.Key(GameAction.Load)), () => { CloseModal(); battle.QuickLoad(BattleController.DefaultSavePath); });
            MenuItem(column, "settings", "Ustawienia", null, () => OpenSettings("Obraz"));
            MenuItem(column, "undo", "Nowa bitwa", null, OpenNewBattle);
            resume.schedule.Execute(() => resume.Focus()).StartingIn(0);
        }

        /// <summary>A new skirmish of a chosen length: map size, deposits and the opponent's pace follow MatchProfile.</summary>
        void OpenNewBattle()
        {
            var keepPause = menuPaused;
            var before = pausedBeforeMenu;
            var panel = OpenModal("Nowa bitwa");
            menuPaused = keepPause;
            pausedBeforeMenu = before;
            Text("Zakończyć tę potyczkę i rozpocząć nową? Niezapisany postęp przepadnie. Wybierz długość: większa mapa, bogatsze złoża i wolniejsze wydobycie.", "modal-text", panel);
            foreach (var length in new[] { MatchLength.Short, MatchLength.Standard, MatchLength.Long })
            {
                var profile = MatchProfile.Of(length);
                PopoverRow(panel, MatchProfile.Name(length), $"ok. {profile.TargetMinutes} min, ok. {profile.TargetFleet} okrętów");
            }
            var buttons = Element("button-row", panel);
            ModalButton(buttons, "Wróć", CloseModal);
            Button first = null;
            foreach (var length in new[] { MatchLength.Short, MatchLength.Standard, MatchLength.Long })
            {
                var chosen = length;
                var button = ModalButton(buttons, MatchProfile.Name(length), () => { CloseModal(); battle.NewGame(chosen); }, length == battle.World.Length ? "primary" : null);
                if (length == battle.World.Length) first = button;
            }
            first?.schedule.Execute(() => first.Focus()).StartingIn(0);
        }

        void OpenSettings(string tab)
        {
            var keepPause = menuPaused;
            var before = pausedBeforeMenu;
            settingsTab = tab;
            var panel = OpenModal("Ustawienia", true);
            if (keepPause) { menuPaused = true; pausedBeforeMenu = before; battle.Paused = true; }
            var tabs = Element("tabs", panel);
            foreach (var name in new[] { "Obraz", "Interfejs", "Sterowanie", "Dźwięk" })
            {
                var chosen = name;
                var button = new Button(() => OpenSettings(chosen)) { text = name, focusable = true };
                button.AddToClassList("tab");
                button.EnableInClassList("chosen", name == tab);
                tabs.Add(button);
            }
            var body = Element("settings-body", panel);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("settings-scroll");
            body.Add(scroll);
            switch (tab)
            {
                case "Obraz": SettingsImage(scroll); break;
                case "Interfejs": SettingsInterface(scroll); break;
                case "Sterowanie": SettingsControls(scroll); break;
                default: Text("Gra nie ma jeszcze kanałów dźwięku w mikserze; głośności gry, muzyki, głosów i interfejsu pojawią się razem z nimi.", "modal-text", scroll); break;
            }
            var buttons = Element("button-row", panel);
            ModalButton(buttons, "Zamknij", CloseModal, "primary");
        }

        void SettingsImage(VisualElement scroll)
        {
            var row = Element("setting-row", scroll);
            Text("Profil graficzny", "setting-label", row);
            foreach (var profile in GraphicsProfiles.All)
            {
                var chosen = profile;
                Option(row, GraphicsProfiles.DisplayName(profile), profiles != null && profiles.Current == profile, () => { profiles?.Select(chosen); OpenSettings("Obraz"); });
            }
            var resolution = Element("setting-row", scroll);
            Text("Rozdzielczość", "setting-label", resolution);
            if (display != null)
            {
                foreach (var mode in display.Available)
                {
                    var chosen = mode;
                    Option(resolution, mode.ToString(), Screen.width == mode.Width && Screen.height == mode.Height, () => { display.Request(chosen, Screen.fullScreen); OpenSettings("Obraz"); });
                }
                if (display.Session.State == ResolutionChangeState.AwaitingConfirmation)
                {
                    var confirm = Element("setting-row", scroll);
                    Text($"Zachować {display.Session.Requested}? Powrót za {Mathf.CeilToInt(display.Session.RemainingSeconds)} s.", "setting-label", confirm);
                    Option(confirm, "Zachowaj", true, () => { display.Confirm(); OpenSettings("Obraz"); });
                    Option(confirm, "Przywróć", false, () => { display.Session.Revert(); OpenSettings("Obraz"); });
                }
            }
            else Text("Ustawienia rozdzielczości są niedostępne w tej scenie.", "setting-value", resolution);
        }

        void SettingsInterface(VisualElement scroll)
        {
            var scale = Element("setting-row", scroll);
            Text("Skala interfejsu", "setting-label", scale);
            foreach (var value in new[] { 0.9f, 1f, 1.1f, 1.25f })
            {
                var chosen = value;
                Option(scale, $"{Mathf.RoundToInt(value * 100f)}%", Mathf.Approximately(userScale, value), () => { userScale = chosen; SaveOptions(); ApplyOptions(); OpenSettings("Interfejs"); });
            }
            Toggle(scroll, "Wyższy kontrast", highContrast, value => highContrast = value);
            Toggle(scroll, "Wzory oprócz kolorów (kreskowanie wroga i stanu krytycznego)", patterns, value => patterns = value);
            Toggle(scroll, "Ograniczenie animacji", reducedMotion, value => reducedMotion = value);
            Text("Przy skali powyżej 100% powtarzalne etykiety zasobów i rozkazów ustępują ikonom; liczby i podpowiedzi zostają.", "setting-value", scroll);
        }

        void Toggle(VisualElement scroll, string label, bool value, Action<bool> set)
        {
            var row = Element("setting-row", scroll);
            Text(label, "setting-label", row);
            Option(row, "Wł.", value, () => { set(true); SaveOptions(); ApplyOptions(); OpenSettings("Interfejs"); });
            Option(row, "Wył.", !value, () => { set(false); SaveOptions(); ApplyOptions(); OpenSettings("Interfejs"); });
        }

        void SettingsControls(VisualElement scroll)
        {
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                var row = Element("setting-row", scroll);
                Text(KeyBindings.Name(action), "setting-label", row);
                var conflicts = KeyBindings.ConflictsOf(action);
                if (conflicts.Count > 0) Text("konflikt: " + string.Join(", ", conflicts.Select(KeyBindings.Name)), "setting-value bad", row);
                var chosen = action;
                var key = Option(row, capturing == action ? "Naciśnij klawisz…" : KeyBindings.Label(KeyBindings.Key(action)), capturing == action, () => StartCapture(chosen));
                key.AddToClassList("key-option");
                key.EnableInClassList("conflict", conflicts.Count > 0);
            }
            var reset = Element("setting-row", scroll);
            Text("Przypisania domyślne", "setting-label", reset);
            Option(reset, "Przywróć", false, () => { KeyBindings.ResetAll(); OpenSettings("Sterowanie"); });
        }

        Button Option(VisualElement row, string label, bool chosen, Action click)
        {
            var option = new Button(click) { text = label, focusable = true };
            option.AddToClassList("option");
            option.EnableInClassList("chosen", chosen);
            row.Add(option);
            return option;
        }

        void StartCapture(GameAction action)
        {
            capturing = action;
            KeyBindings.Capturing = true;
            OpenSettingsKeepingCapture();
        }

        void OpenSettingsKeepingCapture()
        {
            var action = capturing;
            OpenSettings("Sterowanie");
            capturing = action;
            KeyBindings.Capturing = action.HasValue;
            // Rebuild once more so the row shows it is waiting for a key.
            if (modal == null) return;
            var scroll = modal.Q<ScrollView>();
            scroll?.Clear();
            if (scroll != null) SettingsControls(scroll);
        }

        void CaptureKey()
        {
            if (!GameInput.AnyKeyDown) return;
            if (GameInput.GetKeyDown(KeyCode.Escape)) { capturing = null; KeyBindings.Capturing = false; OpenSettings("Sterowanie"); return; }
            // Keyboard keys only, as before: a mouse button cannot be bound.
            if (!GameInput.TryCapture(out var key)) return;
            var action = capturing.Value;
            capturing = null;
            KeyBindings.Capturing = false;
            var conflicts = KeyBindings.Bind(action, key);
            OpenSettings("Sterowanie");
            if (conflicts.Count > 0) OnHerald(new HeraldMessage($"Klawisz {KeyBindings.Label(key)} jest też przypisany: {string.Join(", ", conflicts.Select(KeyBindings.Name))}.", HeraldLevel.Important, null, HeraldTopic.General));
        }

        void RefreshOverlays()
        {
            var over = battle.World.Outcome != OutcomeKind.InProgress;
            if (over && modal == null)
            {
                var won = battle.World.Winner == BattleController.PlayerFaction;
                var panel = OpenModal(battle.World.Outcome == OutcomeKind.Draw ? "Remis" : won ? "Zwycięstwo" : "Porażka");
                Text(battle.World.Outcome == OutcomeKind.Draw ? "Żadna ze stron nie przeważyła." : won ? "Port wroga padł. Szlaki są twoje." : "Twój port macierzysty padł.", "modal-text", panel);
                var buttons = Element("button-row", panel);
                ModalButton(buttons, "Nowa bitwa", () => { CloseModal(); battle.NewGame(); }, "primary");
            }
            if (profiles != null && profiles.Notice != lastProfileNotice)
            {
                lastProfileNotice = profiles.Notice;
                if (!string.IsNullOrEmpty(lastProfileNotice)) OnHerald(new HeraldMessage(lastProfileNotice, HeraldLevel.Important, null, HeraldTopic.General));
            }
            if (display != null && display.Notice != lastDisplayNotice)
            {
                lastDisplayNotice = display.Notice;
                if (!string.IsNullOrEmpty(lastDisplayNotice)) OnHerald(new HeraldMessage(lastDisplayNotice, HeraldLevel.Important, null, HeraldTopic.General));
            }
        }

        // ---------- Options ----------

        void LoadOptions()
        {
            userScale = Mathf.Clamp(PlayerPrefs.GetFloat("hud.scale", 1f), 0.9f, 1.25f);
            highContrast = PlayerPrefs.GetInt("hud.contrast", 0) == 1;
            patterns = PlayerPrefs.GetInt("hud.patterns", 0) == 1;
            reducedMotion = PlayerPrefs.GetInt("hud.motion", 0) == 1;
        }

        void SaveOptions()
        {
            PlayerPrefs.SetFloat("hud.scale", userScale);
            PlayerPrefs.SetInt("hud.contrast", highContrast ? 1 : 0);
            PlayerPrefs.SetInt("hud.patterns", patterns ? 1 : 0);
            PlayerPrefs.SetInt("hud.motion", reducedMotion ? 1 : 0);
            PlayerPrefs.Save();
        }

        void ApplyOptions()
        {
            panelSettings.scale = userScale;
            var hud = root.Q(className: "hud-root");
            hud?.EnableInClassList("compact", userScale > 1.05f);
            hud?.EnableInClassList("contrast", highContrast);
            HudMinimap.ReducedMotion = reducedMotion;
        }

        // ---------- Tooltips (400 ms, pointer or keyboard focus) ----------

        void Tip(VisualElement element, Func<TipInfo> content)
        {
            if (content == null) return;
            void Arm()
            {
                tooltipOwner = element;
                tooltipContent = content;
                tooltipTimer?.Pause();
                tooltipTimer = element.schedule.Execute(() =>
                {
                    if (tooltipOwner != element || modal != null && !modal.Contains(element)) return;
                    var tip = content();
                    if (string.IsNullOrEmpty(tip.Title) && string.IsNullOrEmpty(tip.Body)) return;
                    FillTooltip(tip);
                    Show(tooltip, true);
                    tooltip.BringToFront();
                    tooltip.schedule.Execute(PlaceTooltip).StartingIn(0);
                }).StartingIn((long)(TooltipDelay * 1000f));
            }
            void Disarm()
            {
                if (tooltipOwner != element) return;
                HideTooltip();
            }
            element.RegisterCallback<PointerEnterEvent>(_ => Arm());
            element.RegisterCallback<PointerLeaveEvent>(_ => Disarm());
            element.RegisterCallback<FocusInEvent>(_ => Arm());
            element.RegisterCallback<FocusOutEvent>(_ => Disarm());
            // An element taken away under the pointer gets no leave event; its tooltip goes with it.
            element.RegisterCallback<DetachFromPanelEvent>(_ => Disarm());
        }

        void HideTooltip()
        {
            tooltipTimer?.Pause();
            tooltipOwner = null;
            tooltipContent = null;
            if (tooltip != null) Show(tooltip, false);
        }

        void FillTooltip(TipInfo tip)
        {
            SetText(tipTitle, tip.Title ?? string.Empty);
            SetText(tipBody, tip.Body ?? string.Empty);
            SetText(tipCost, tip.Cost ?? string.Empty);
            SetText(tipKey, tip.Key ?? string.Empty);
            Show(tipBody, !string.IsNullOrEmpty(tip.Body));
            Show(tipCost, !string.IsNullOrEmpty(tip.Cost));
            Show(tipKey, !string.IsNullOrEmpty(tip.Key));
        }

        void PlaceTooltip()
        {
            if (tooltipOwner == null || root.panel == null) return;
            var owner = tooltipOwner.worldBound;
            var size = tooltip.layout.size;
            var bounds = root.worldBound;
            var x = Mathf.Clamp(owner.center.x - size.x * 0.5f, bounds.xMin + 16f, bounds.xMax - size.x - 16f);
            var y = owner.yMin - size.y - 10f;
            if (y < bounds.yMin + 52f) y = owner.yMax + 10f;
            tooltip.style.left = x;
            tooltip.style.top = y;
        }

        // ---------- Assets and helpers ----------

        Texture2D Load(string path)
        {
            if (textures.TryGetValue(path, out var texture)) return texture;
            texture = Resources.Load<Texture2D>(path);
            textures[path] = texture;
            return texture;
        }

        StyleBackground Icon(string name, int size = 64) => new StyleBackground(Load($"Hud/Icons/{name}-{size}"));

        StyleBackground Portrait(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.Merchant: return new StyleBackground(Load("Hud/Art/handlowiec"));
                case ShipClass.Cutter: return Icon("collector", 128);
                default: return new StyleBackground(Load("Hud/Art/triera"));
            }
        }

        /// <summary>The icon of a hull class in Hud/Icons (64 and 128 px).</summary>
        public static string ClassIcon(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.Cutter: return "collector";
                case ShipClass.Ram: return "trireme";
                case ShipClass.CrewDestroyer: return "archer-ship";
                case ShipClass.Merchant: return "merchant";
                // Drawn in the package's style by Editor/HudClassIcons (3c part 2).
                case ShipClass.ShipKiller: return "ship-killer";
                case ShipClass.Bomber: return "bomber";
                default: return "boarding-ship";
            }
        }

        static string ShortClass(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.Cutter: return "Kuter";
                case ShipClass.Ram: return "Taran.";
                case ShipClass.CrewDestroyer: return "Niszcz.";
                case ShipClass.Merchant: return "Handl.";
                case ShipClass.ShipKiller: return "N. okr.";
                case ShipClass.Bomber: return "Bomb.";
                default: return "Abord.";
            }
        }

        static string StageIcon(float progress)
        {
            if (progress < 0.1f) return "stage-keel";
            if (progress < 0.35f) return "stage-frames";
            if (progress < 0.7f) return "stage-planking";
            if (progress < 0.85f) return "stage-deck";
            if (progress < 0.95f) return "stage-mast";
            return "stage-oars";
        }

        static string Plural(int count, string one, string few, string many)
        {
            if (count == 1) return one;
            var tens = count % 100;
            return count % 10 >= 2 && count % 10 <= 4 && (tens < 12 || tens > 14) ? few : many;
        }

        static string Keys(GameAction action) => "Skrót: " + KeyBindings.Label(KeyBindings.Key(action));

        static VisualElement Element(string classes, VisualElement parent)
        {
            var element = new VisualElement();
            if (!string.IsNullOrEmpty(classes)) foreach (var name in classes.Split(' ')) element.AddToClassList(name);
            parent?.Add(element);
            return element;
        }

        static Label Text(string text, string classes, VisualElement parent)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            if (!string.IsNullOrEmpty(classes)) foreach (var name in classes.Split(' ')) label.AddToClassList(name);
            parent?.Add(label);
            return label;
        }

        VisualElement Glyph(VisualElement parent, string icon, string classes)
        {
            var glyph = Element(classes, parent);
            glyph.pickingMode = PickingMode.Ignore;
            glyph.style.backgroundImage = Icon(icon);
            return glyph;
        }

        static void Divider(VisualElement parent) => Element("divider", parent).pickingMode = PickingMode.Ignore;

        static void Show(VisualElement element, bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != display) element.style.display = display;
        }

        static void SetText(Label label, string text)
        {
            if (label.text != text) label.text = text;
        }
    }
}
