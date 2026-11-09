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
        VisualElement shipPortrait, shipBadge, shipCargoRow, moraleGlyph;
        Label shipName, shipTag, shipHull, shipSailors, shipSoldiers, shipMorale, shipCargo, shipPhase;
        MeterBar shipHullMeter, shipCargoMeter;
        readonly List<(Button card, VisualElement portrait, Label name, MeterBar meter)> unitCards = new List<(Button, VisualElement, Label, MeterBar)>();
        Button moreCard;
        Label moreLabel;
        readonly List<(VisualElement row, VisualElement glyph, Label text)> classCounts = new List<(VisualElement, VisualElement, Label)>();
        Label groupHull, groupCrew;
        VisualElement portPortrait;
        Label portCardName, portWoodValue, portSilverValue, portBerths, portYard, portHullValue;
        MeterBar portHullMeter, portWoodMeter, portSilverMeter;
        VisualElement portStores;
        Label wreckCargo, wreckTime;
        QueueRow warRow, portRow;
        readonly List<CommandButton> commands = new List<CommandButton>();
        Label commandsEmpty;
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
        static KeyCode[] allKeys;
        string settingsTab = "Obraz";

        sealed class Herald
        {
            public VisualElement Element;
            public Label Title, Sub, Key;
            public HeraldMessage Message;
            public float Until, Born;
            public int Count = 1;
            public bool Acknowledged;
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
            Tip(hull, () => new TipInfo { Title = "Kadłub", Body = "Wytrzymałość kadłuba. Przy zerze statek tonie i zostawia wrak. Remont w stoczni własnego portu.", Key = Keys(GameAction.Repair) + ": remont" });

            var crew = Element("card-row", card);
            shipSailors = CardStat(crew, "sailor", "marynarze", () => new TipInfo { Title = "Marynarze", Body = "Wioślarze i obsługa. Mniej niż minimum spowalnia statek." });
            shipSoldiers = CardStat(crew, "soldier", "żołnierze", () => new TipInfo { Title = "Żołnierze", Body = "Walczą w abordażu i bronią przed nim." });
            var morale = Element("card-stat", crew);
            moraleGlyph = Glyph(morale, "morale-high", "glyph-small");
            shipMorale = Text(string.Empty, "card-stat-value", morale);
            Text("morale", "card-stat-label", morale);
            Tip(morale, () => new TipInfo { Title = "Morale", Body = "Duch załogi; niskie osłabia w walce wręcz. Ikona i liczba – nie tylko kolor." });

            shipCargoRow = Element("card-row", card);
            Glyph(shipCargoRow, "cargo", "glyph-small");
            Text("Ładownia", "small", shipCargoRow).style.marginLeft = 6;
            shipCargoMeter = new MeterBar(HudPalette.Timber, 6);
            shipCargoMeter.AddToClassList("card-meter");
            shipCargoRow.Add(shipCargoMeter);
            shipCargo = Text(string.Empty, "card-value", shipCargoRow);

            shipPhase = Text(string.Empty, "card-phase", card);
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
            portCardName = Text(string.Empty, "card-name", card);
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
            Text("Kuter z rozkazem zbioru zabiera ładunek wraku.", "help-line", card);
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
            yield return new CommandButton { Action = GameAction.Attack, Icon = "attack", Label = "Atak", Full = "Ostrzał łuczników", Targeted = HudOrder.Attack, Able = ship => ship.Definition.LightSlots > 0, Body = "Łucznicy strzelają do wskazanego okrętu lub osady wroga." };
            yield return new CommandButton { Action = GameAction.Ram, Icon = "ram", Label = "Taran", Full = "Szarża taranem", Targeted = HudOrder.Ram, Able = ship => ship.Class == ShipClass.Ram, Body = "Taranowiec szarżuje, odskakuje i ponawia atak." };
            yield return new CommandButton { Action = GameAction.Board, Icon = "board", Label = "Abordaż", Full = "Abordaż", Targeted = HudOrder.Board, Able = ship => ship.Class == ShipClass.Boarder, Body = "Haki i walka wręcz o zdobycie okrętu." };
            yield return new CommandButton { Action = GameAction.Volley, Icon = "volley", Label = "Ostrzał", Full = "Ostrzał z abordażowca", Targeted = HudOrder.Volley, Able = ship => ship.Class == ShipClass.Boarder, Body = "Abordażowiec strzela zamiast szukać zwarcia. Także Alt+PPM." };
            yield return new CommandButton { Action = GameAction.Harvest, Icon = "gather", Label = "Zbiór", Full = "Zbiór surowca", Targeted = HudOrder.Harvest, Able = ship => ship.Definition.CanHarvest, Body = "Kutry na złoże drewna lub srebra i z urobkiem do portu; także zbiór wraków." };
            yield return new CommandButton { Action = GameAction.Deliver, Icon = "deliver", Label = "Dostawa", Full = "Dostawa do portu", Targeted = HudOrder.Deliver, Able = ship => ship.Class == ShipClass.Merchant, Body = "Handlowiec wiezie ładunek z wybranego portu do wskazanego własnego portu." };
            yield return new CommandButton { Action = GameAction.Outpost, Icon = "outpost", Label = "Buduj", Full = "Zbuduj przyczółek", Targeted = HudOrder.Outpost, Able = ship => ship.Class == ShipClass.Merchant, Body = $"Handlowiec zakłada przyczółek na wskazanym wolnym brzegu ({Rules.OutpostWood / Rules.Centi} D / {Rules.OutpostSilver / Rules.Centi} S z ładowni)." };
            yield return new CommandButton { Action = GameAction.Escort, Icon = "escort", Label = "Eskorta", Full = "Eskortuj handlowca", Targeted = HudOrder.Escort, Able = ship => !ship.Definition.IsEconomic, Body = "Okręty płyną przy burcie wskazanego handlowca i bronią go w jego strefie." };
            yield return new CommandButton { Action = GameAction.Repair, Icon = "repair", Label = "Remont", Full = "Remont w stoczni", Instant = hud => hud.battle.RepairSelected(), Able = _ => true, Body = "Statki płyną do stoczni najbliższego własnego portu." };
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
                if (Input.GetKeyDown(KeyCode.Escape)) { CloseModal(); ConsumedCancel = true; }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmModal();
                return;
            }
            if (popover != null && KeyBindings.Down(GameAction.Cancel)) { ClosePopover(); ConsumedCancel = true; return; }
            if (KeyBindings.Down(GameAction.CollapseHud)) ToggleCollapse();
            if (KeyBindings.Down(GameAction.FocusAlert)) FocusAlarm();
            if (KeyBindings.Down(GameAction.CancelOrder) && FocusedQueueItem(out var war, out var index)) RequestCancel(war, index);
            if (KeyBindings.Down(GameAction.Cancel) && battle.PendingOrder == HudOrder.None && battle.Selected.Count == 0 && battle.InspectedSettlementId < 0) { OpenMenu(); ConsumedCancel = true; }
            if (OwnSelected().Count == 0) return;
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
                SetText(selectionTitle, port.Owner == player ? "Port" : "Port wroga");
                SetText(selectionNote, string.Empty);
            }
            else if (wreck != null)
            {
                SetText(wreckCargo, $"{wreck.Wood / Rules.Centi} D · {wreck.Silver / Rules.Centi} S");
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
            SetText(shipSailors, ship.Sailors.ToString());
            SetText(shipSoldiers, ship.Soldiers.ToString());
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
            SetText(portCardName, battle.PortName(port));
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
                SetText(portYard, port.HasShipyard ? "Stocznia czynna" : port.ShipyardBuild != null ? $"Stocznia: budowa {Mathf.RoundToInt(100f * port.ShipyardBuild.DoneTicks / port.ShipyardBuild.TotalTicks)}%" : "Brak stoczni");
            }
            else
            {
                SetText(portBerths, "Magazyny, kolejki i stocznia wroga są nieznane.");
                SetText(portYard, string.Empty);
            }
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
            foreach (var shipClass in new[] { ShipClass.Cutter, ShipClass.Merchant, ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder })
            {
                var definition = ShipCatalog.Get(shipClass);
                if (war.HasValue && war.Value == definition.IsEconomic) continue;
                var chosen = shipClass;
                var item = MenuItem(panel, ClassIcon(shipClass), BattleController.ClassName(shipClass), $"{definition.CostWood} D · {definition.CostSilver} S · {definition.BuildSeconds:0} s", () => Order(chosen));
                var problem = OrderProblem(port, definition, fleet);
                item.EnableInClassList("disabled", problem != null);
                Tip(item, () => new TipInfo
                {
                    Title = BattleController.ClassName(chosen),
                    Body = $"Kadłub {definition.Hull}, prędkość {definition.Speed:0.#}, załoga do {definition.MaxCrew}" + (definition.Capacity > 0 ? $", ładownia {definition.Capacity}." : ".") + (problem != null ? "\n" + problem : string.Empty),
                    Cost = $"Cena kompletnego szablonu (z obsadą i uzbrojeniem): {definition.CostWood} D · {definition.CostSilver} S · {definition.BuildSeconds:0} s",
                    Key = definition.IsEconomic ? "Linia portowa" : "Linia wojenna",
                });
            }
            if ((!war.HasValue || war.Value) && port.IsOutpost && !port.HasShipyard && port.ShipyardBuild == null)
            {
                var affordable = port.Available(ResourceType.Wood) >= Rules.ShipyardWood && port.Available(ResourceType.Silver) >= Rules.ShipyardSilver;
                var item = MenuItem(panel, "shipyard", "Stocznia (linia wojenna)", $"{Rules.ShipyardWood / Rules.Centi} D · {Rules.ShipyardSilver / Rules.Centi} S · {Rules.ShipyardBuildSeconds:0} s", () => { battle.BuildShipyard(); ClosePopover(); Refresh(); });
                item.EnableInClassList("disabled", !affordable);
            }
            Text("Brak stoczni nie daje darmowej linii wojennej. Zapas chroniony 60 D / 20 S jest częścią dostępnych środków.", "popover-note", panel);
        }

        string OrderProblem(Settlement port, ShipDefinition definition, int fleet)
        {
            if (!definition.IsEconomic && !port.HasShipyard) return "Ten port nie ma stoczni.";
            if (fleet >= Rules.FleetLimit) return $"Flota osiągnęła limit {Rules.FleetLimit}.";
            var wood = definition.CostWood * Rules.Centi - port.Available(ResourceType.Wood);
            var silver = definition.CostSilver * Rules.Centi - port.Available(ResourceType.Silver);
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
            var leftWood = port.Available(ResourceType.Wood) - definition.CostWood * Rules.Centi;
            var leftSilver = port.Available(ResourceType.Silver) - definition.CostSilver * Rules.Centi;
            ClosePopover();
            if (leftWood < Rules.ReserveWood || leftSilver < Rules.ReserveSilver)
            {
                OpenConfirm("Naruszenie zapasu chronionego", $"Po zamówieniu ({BattleController.ClassName(shipClass)}) w porcie zostanie {leftWood / Rules.Centi} D · {leftSilver / Rules.Centi} S, mniej niż zapas chroniony {Rules.ReserveWood / Rules.Centi} D · {Rules.ReserveSilver / Rules.Centi} S.",
                    null, "Zamów mimo to", () => battle.QueueShip(shipClass), false);
                return;
            }
            battle.QueueShip(shipClass);
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
        }

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
            if (!HoldsInput && !IsPointerOverInterface(Input.mousePosition))
            {
                var order = battle.PendingOrder;
                name = order != HudOrder.None ? CursorFor(order) : battle.ContextCursor(Input.mousePosition);
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
                    bar.crew.Set((float)(ship.Sailors + ship.Soldiers) / Mathf.Max(1, ship.Definition.MaxCrew), HudPalette.Warning);
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
            // The same kind of news about the same port or convoy joins the entry already shown.
            var same = heralds.FirstOrDefault(item => !item.Acknowledged && item.Message.GroupKey == message.GroupKey && Time.unscaledTime - item.Born < HeraldMerge + (item.Count - 1) * 2f);
            if (same != null)
            {
                same.Count++;
                same.Message = message;
                same.Until = Time.unscaledTime + HeraldInformational;
                SetText(same.Title, message.Text);
                SetText(same.Sub, $"×{same.Count} · {SubText(message)}");
                return;
            }

            var herald = new Herald { Message = message, Born = Time.unscaledTime, Until = Time.unscaledTime + HeraldInformational };
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
            popover.schedule.Execute(() =>
            {
                if (popover == null) return;
                var box = anchor.worldBound;
                var size = popover.layout.size;
                var bounds = root.worldBound;
                var x = Mathf.Clamp(box.xMin, bounds.xMin + 16f, bounds.xMax - size.x - 16f);
                var y = box.yMax + 6f;
                if (y + size.y > bounds.yMax - 16f) y = box.yMin - size.y - 6f;
                popover.style.left = x;
                popover.style.top = y;
            }).StartingIn(0);
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
            MenuItem(column, "undo", "Nowa bitwa", null, () => OpenConfirm("Nowa bitwa", "Zakończyć tę potyczkę i rozpocząć nową? Niezapisany postęp przepadnie.", null, "Nowa bitwa", () => battle.NewGame(), true));
            resume.schedule.Execute(() => resume.Focus()).StartingIn(0);
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
            if (!Input.anyKeyDown) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { capturing = null; KeyBindings.Capturing = false; OpenSettings("Sterowanie"); return; }
            if (allKeys == null) allKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode))).Where(key => key < KeyCode.Mouse0).ToArray();
            foreach (var key in allKeys)
            {
                if (key == KeyCode.None || !Input.GetKeyDown(key)) continue;
                var action = capturing.Value;
                capturing = null;
                KeyBindings.Capturing = false;
                var conflicts = KeyBindings.Bind(action, key);
                OpenSettings("Sterowanie");
                if (conflicts.Count > 0) OnHerald(new HeraldMessage($"Klawisz {KeyBindings.Label(key)} jest też przypisany: {string.Join(", ", conflicts.Select(KeyBindings.Name))}.", HeraldLevel.Important, null, HeraldTopic.General));
                return;
            }
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

        static string ClassIcon(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.Cutter: return "collector";
                case ShipClass.Ram: return "trireme";
                case ShipClass.CrewDestroyer: return "archer-ship";
                case ShipClass.Merchant: return "merchant";
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
