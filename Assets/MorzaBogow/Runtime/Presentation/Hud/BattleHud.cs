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
    /// The battle interface. Built with UI Toolkit on a 1920×1080 reference that scales evenly up to 2560×1440.
    /// It reads the battle controller and issues orders through it; it never changes the game state itself.
    /// Text is refreshed eight times a second, bars and boxes every frame, the chart four times a second.
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        const float TextRefresh = 0.125f, ChartRefresh = 0.25f;
        static readonly string[] Shipwright = { "Kuter", "Handlowiec", "Taranowiec", "Niszczyciel załóg", "Abordażowiec" };

        BattleController battle;
        UIDocument document;
        VisualElement root;
        FontDefinition titleFont, boldFont;
        float textTimer, chartTimer;

        // Top bar
        Medallion portMedallion;
        Label portName, portHull, woodValue, woodCap, woodReserve, silverValue, silverCap, silverReserve, transitValue, fleetValue, portsValue, timeValue, speedValue;
        MeterBar woodMeter, silverMeter;
        Medallion pauseButton, slowerButton, fasterButton;

        // Herald
        VisualElement heraldColumn;
        readonly List<(VisualElement element, float until, HeraldMessage message)> heralds = new List<(VisualElement, float, HeraldMessage)>();

        // Dock
        HudMinimap minimap;
        VisualElement shipCard, groupView, settlementCard, helpView;
        Medallion cardPortrait;
        Label cardName, cardOwner, hullValue, sailorsValue, soldiersValue, moraleValue, cargoValue, cargoLabel, orderText;
        MeterBar hullMeter, cargoMeter;
        VisualElement cargoLine, crewLine;
        readonly List<(VisualElement cell, Medallion medallion, MeterBar meter)> groupCells = new List<(VisualElement, Medallion, MeterBar)>();
        Label groupMore;
        Medallion portPortrait;
        Label portCardName, portCardHull, portWood, portSilver, portBerths, portYard;
        MeterBar portHullMeter, portWoodMeter, portSilverMeter;
        Label yardTitle;
        YardLine warLine, portLine;
        readonly List<Slot> slots = new List<Slot>();
        bool production;
        string slotSignature = string.Empty;

        // Overlays
        VisualElement worldLayer, selectBox, modeHint, tooltip, pauseBanner, endOverlay, settingsOverlay;
        IconElement modeIcon;
        Label modeText, tipTitle, tipBody, tipCost, tipKey, endTitle, endText;
        readonly List<(VisualElement root, MeterBar hull, MeterBar crew)> bars = new List<(VisualElement, MeterBar, MeterBar)>();
        VisualElement tooltipOwner;
        Func<TipContent> tooltipContent;
        GraphicsProfileController profiles;
        DisplaySettingsController display;
        VisualElement profileColumn, resolutionColumn;
        Label settingsNote;
        string lastProfileNotice = string.Empty, lastDisplayNotice = string.Empty;

        public int VisibleHealthBars { get; private set; }
        public bool ShowsProduction => production;
        public string WoodText => woodValue?.text ?? string.Empty;
        public IReadOnlyList<string> HeraldTexts => heralds.Select(item => item.message.Text).ToList();
        public int SlotCount => slots.Count(slot => slot.Root.style.display != DisplayStyle.None);
        public VisualElement Root => root;

        struct TipContent
        {
            public string Title, Body, Cost, Key;
        }

        sealed class Slot
        {
            public VisualElement Root;
            public Medallion Icon;
            public Label Key, Cost;
            public Action Click;
            public Func<TipContent> Tip;
            public bool Enabled;
        }

        sealed class YardLine
        {
            public VisualElement Root, Queue;
            public Medallion Current;
            public Label Name, Stage, Empty;
            public readonly List<Medallion> Next = new List<Medallion>();
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
            host.SetActive(true);
            hud.Build();
            return hud;
        }

        void Build()
        {
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            var sheet = Resources.Load<StyleSheet>("MorzaBogowHud");
            if (sheet != null) root.styleSheets.Add(sheet);

            var hud = new VisualElement { pickingMode = PickingMode.Ignore };
            hud.AddToClassList("hud");
            root.Add(hud);

            var bodyFont = Resources.Load<Font>("Fonts/AlegreyaSans-Medium");
            var bold = Resources.Load<Font>("Fonts/AlegreyaSans-Bold");
            var title = Resources.Load<Font>("Fonts/Cinzel-Variable");
            if (bodyFont != null) hud.style.unityFontDefinition = FontDefinition.FromFont(bodyFont);
            boldFont = bold != null ? FontDefinition.FromFont(bold) : default;
            titleFont = title != null ? FontDefinition.FromFont(title) : default;

            worldLayer = Element("world-layer", hud);
            worldLayer.pickingMode = PickingMode.Ignore;
            selectBox = Element("select-box", worldLayer);
            selectBox.pickingMode = PickingMode.Ignore;
            selectBox.style.display = DisplayStyle.None;

            BuildTopBar(hud);
            heraldColumn = Element("herald", hud);
            heraldColumn.pickingMode = PickingMode.Ignore;
            BuildDock(hud);
            BuildOverlays(hud);

            ApplyFonts(hud);
            foreach (var surface in hud.Query(className: "panel").ToList().Concat(hud.Query(className: "topbar").ToList()).Concat(hud.Query(className: "dialog").ToList()))
                surface.style.backgroundImage = Background.FromTexture2D(HudTextures.Grain);
            tooltip.style.backgroundImage = Background.FromTexture2D(HudTextures.Marble);

            profiles = FindAnyObjectByType<GraphicsProfileController>();
            display = FindAnyObjectByType<DisplaySettingsController>();
            if (profiles != null) profiles.DrawImguiPanel = false;
            if (display != null) display.DrawImguiPanel = false;

            battle.Herald += OnHerald;
            OnMatchStarted();
        }

        void OnDestroy()
        {
            if (battle != null) battle.Herald -= OnHerald;
        }

        public void OnMatchStarted()
        {
            if (minimap == null) return;
            minimap.Bind(battle);
            foreach (var item in heralds) item.element.RemoveFromHierarchy();
            heralds.Clear();
            slotSignature = string.Empty;
            Refresh();
        }

        void ApplyFonts(VisualElement scope)
        {
            if (titleFont.font != null || titleFont.fontAsset != null) scope.Query(className: "title").ForEach(element => element.style.unityFontDefinition = titleFont);
            if (boldFont.font != null || boldFont.fontAsset != null) scope.Query(className: "num").ForEach(element => element.style.unityFontDefinition = boldFont);
        }

        // ---------- Layout ----------

        void BuildTopBar(VisualElement parent)
        {
            var bar = Element("topbar", parent);
            var band = new OrnamentBand(OrnamentKind.Meander, HudPalette.Ochre, HudPalette.ClayNight);
            band.AddToClassList("topbar-ornament");
            bar.Add(band);

            var port = Element("group", bar);
            portMedallion = new Medallion(HudIcon.Settlement);
            portMedallion.AddToClassList("top-medallion");
            port.Add(portMedallion);
            var portText = Element("group-text", port);
            portName = Text("Osada", "title mid", portText);
            portHull = Text(string.Empty, "small", portText);
            port.RegisterCallback<ClickEvent>(_ => CyclePort());
            Tip(port, () => new TipContent { Title = portName.text, Body = "Aktywny port: z jego magazynu płacisz za zamówienia, jego kolejki widzisz w stoczni. Kliknij, aby przejść do następnego własnego portu.", Key = "Klik: następny port" });
            Divider(bar);

            ResourceGroup(bar, HudIcon.Wood, "Drewno", out woodValue, out woodCap, out woodReserve, out woodMeter,
                () => new TipContent { Title = "Drewno (D)", Body = "Dostępne w magazynie aktywnego portu, jego pojemność i rezerwa na rozpoczęte zamówienia. Kutry ścinają je na wyspach leśnych.", Key = "Kliknij złoże prawym przyciskiem, mając zaznaczony kuter." });
            ResourceGroup(bar, HudIcon.Silver, "Srebro", out silverValue, out silverCap, out silverReserve, out silverMeter,
                () => new TipContent { Title = "Srebro (S)", Body = "Dostępne w magazynie aktywnego portu, jego pojemność i rezerwa na rozpoczęte zamówienia. Wydobywane w kopalniach na wyspach skalnych." });
            Divider(bar);

            transitValue = Stat(bar, HudIcon.Transit, () => new TipContent { Title = "W drodze", Body = "Ładunek na własnych statkach: w ładowniach kutrów i handlowców, jeszcze nie w magazynie." });
            fleetValue = Stat(bar, HudIcon.Fleet, () => new TipContent { Title = "Flota", Body = $"Liczba własnych statków i limit floty ({Rules.FleetLimit})." });
            portsValue = Stat(bar, HudIcon.Port, () => new TipContent { Title = "Porty", Body = "Osada i przyczółki. Każdy ma własny magazyn, port i kolejkę portu." });

            Element("spacer", bar);

            timeValue = Stat(bar, HudIcon.Time, () => new TipContent { Title = "Czas bitwy", Body = "Czas gry od początku bitwy." });
            slowerButton = IconButton(bar, HudIcon.Slower, () => battle.SpeedDown(), () => new TipContent { Title = "Wolniej", Body = "Zmniejsza tempo gry.", Key = "Klawisz: −" });
            speedValue = Text("×1", "num speed-label", bar);
            fasterButton = IconButton(bar, HudIcon.Faster, () => battle.SpeedUp(), () => new TipContent { Title = "Szybciej", Body = "Zwiększa tempo gry.", Key = "Klawisz: +" });
            pauseButton = IconButton(bar, HudIcon.Pause, () => battle.TogglePause(), () => new TipContent { Title = battle.Paused ? "Wznów" : "Pauza", Body = "Zatrzymuje grę; rozkazy można wydawać także w pauzie.", Key = "Klawisz: spacja" });
            Divider(bar);
            IconButton(bar, HudIcon.Save, () => battle.QuickSave(BattleController.DefaultSavePath), () => new TipContent { Title = "Zapisz", Body = "Szybki zapis bitwy.", Key = "Klawisz: F5" });
            IconButton(bar, HudIcon.Load, () => battle.QuickLoad(BattleController.DefaultSavePath), () => new TipContent { Title = "Wczytaj", Body = "Wczytuje szybki zapis.", Key = "Klawisz: F8" });
            IconButton(bar, HudIcon.Settings, ToggleSettings, () => new TipContent { Title = "Ustawienia obrazu", Body = "Profil graficzny i rozdzielczość.", Key = "Klawisze: F9, F10" });
        }

        void ResourceGroup(VisualElement bar, HudIcon icon, string name, out Label value, out Label capacity, out Label reserve, out MeterBar meter, Func<TipContent> tip)
        {
            var group = Element("group", bar);
            var medallion = new Medallion(icon);
            medallion.AddToClassList("top-medallion");
            medallion.Set(icon, icon == HudIcon.Wood ? HudPalette.Parchment : HudPalette.Marble);
            group.Add(medallion);
            var text = Element("group-text", group);
            var line = Element(null, text);
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.FlexEnd;
            value = Text("0", "num big", line);
            capacity = Text("/ 0", "small", line);
            capacity.style.marginLeft = 4;
            capacity.style.marginBottom = 3;
            meter = new MeterBar(icon == HudIcon.Wood ? HudPalette.Timber : HudPalette.Silver, 10);
            meter.AddToClassList("res-meter");
            text.Add(meter);
            reserve = Text(string.Empty, "small", text);
            Tip(group, tip);
        }

        Label Stat(VisualElement bar, HudIcon icon, Func<TipContent> tip)
        {
            var group = Element("group", bar);
            var medallion = new Medallion(icon);
            medallion.AddToClassList("top-medallion");
            group.Add(medallion);
            var value = Text(string.Empty, "num mid group-text", group);
            Tip(group, tip);
            return value;
        }

        Medallion IconButton(VisualElement bar, HudIcon icon, Action click, Func<TipContent> tip)
        {
            var button = new Medallion(icon);
            button.AddToClassList("icon-button");
            bar.Add(button);
            button.RegisterCallback<ClickEvent>(_ => click());
            Tip(button, tip);
            return button;
        }

        void Divider(VisualElement parent) => Element("divider", parent).pickingMode = PickingMode.Ignore;

        void BuildDock(VisualElement parent)
        {
            var dock = Element("dock", parent);
            dock.pickingMode = PickingMode.Ignore;

            var map = Panel(dock, "map-panel", "Mapa mórz");
            minimap = new HudMinimap();
            map.Add(minimap);
            Tip(minimap, () => new TipContent { Title = "Mapa mórz", Body = "Ląd, mgła wojny, porty, złoża, statki i obszar widoku kamery. Klik i przeciąganie przesuwają widok.", Key = "LPM: przesuń widok · PPM: rozkaz ruchu" });

            var selection = Panel(dock, "selection-panel", null);
            BuildShipCard(selection);
            BuildGroupView(selection);
            BuildSettlementCard(selection);
            BuildHelp(selection);

            var yard = Panel(dock, "yard-panel", "Stocznia");
            yardTitle = yard.Q<Label>(className: "panel-title");
            warLine = YardRow(yard, true);
            portLine = YardRow(yard, false);

            var orders = Panel(dock, "orders-panel", "Rozkazy");
            var grid = Element("order-grid", orders);
            for (var i = 0; i < 12; i++) slots.Add(MakeSlot(grid));
        }

        VisualElement Panel(VisualElement dock, string kind, string title)
        {
            var panel = Element("panel", dock);
            panel.AddToClassList(kind);
            panel.Add(new PanelFrame(HudPalette.OchreLight));
            if (title != null) Text(title, "title panel-title", panel);
            return panel;
        }

        void BuildShipCard(VisualElement parent)
        {
            shipCard = Element("card-row", parent);
            cardPortrait = new Medallion(HudIcon.Cutter);
            cardPortrait.AddToClassList("card-portrait");
            shipCard.Add(cardPortrait);
            var body = Element("card-body", shipCard);
            var head = Element("card-line", body);
            head.style.marginTop = 0;
            cardName = Text(string.Empty, "title card-name", head);
            cardOwner = Text(string.Empty, "small bad", head);
            cardOwner.style.marginLeft = 12;

            var hull = Element("card-line", body);
            Icon(hull, HudIcon.Hull, HudPalette.Parchment);
            Text("Kadłub", "small", hull);
            hullMeter = new MeterBar(HudPalette.OliveLight, 10);
            hullMeter.AddToClassList("card-meter");
            hull.Add(hullMeter);
            hullValue = Text(string.Empty, "num card-value", hull);
            Tip(hull, () => new TipContent { Title = "Kadłub", Body = "Wytrzymałość kadłuba. Przy zerze statek tonie, a z wraku można zebrać część kosztu. Remont w stoczni własnego portu.", Key = "R: remont" });

            crewLine = Element("card-line", body);
            sailorsValue = CardStat(crewLine, HudIcon.Sailor, () => new TipContent { Title = "Marynarze", Body = "Wioślarze i obsługa. Mniej marynarzy niż minimum to wolniejszy statek." });
            soldiersValue = CardStat(crewLine, HudIcon.Soldier, () => new TipContent { Title = "Żołnierze", Body = "Walczą w abordażu i bronią przed nim." });
            moraleValue = CardStat(crewLine, HudIcon.Morale, () => new TipContent { Title = "Morale", Body = "Duch załogi. Niskie morale osłabia w walce wręcz." });

            cargoLine = Element("card-line", body);
            Icon(cargoLine, HudIcon.Cargo, HudPalette.Parchment);
            cargoLabel = Text("Ładownia", "small", cargoLine);
            cargoMeter = new MeterBar(HudPalette.Timber, 6);
            cargoMeter.AddToClassList("card-meter");
            cargoLine.Add(cargoMeter);
            cargoValue = Text(string.Empty, "num card-value", cargoLine);

            orderText = Text(string.Empty, "card-order", body);
        }

        Label CardStat(VisualElement line, HudIcon icon, Func<TipContent> tip)
        {
            var stat = Element("card-stat", line);
            Icon(stat, icon, HudPalette.Parchment);
            var value = Text(string.Empty, "num", stat);
            Tip(stat, tip);
            return value;
        }

        void BuildGroupView(VisualElement parent)
        {
            groupView = Element(null, parent);
            Text("Zaznaczona flota", "title panel-title", groupView);
            var grid = Element("group-grid", groupView);
            for (var i = 0; i < 22; i++)
            {
                var cell = Element("group-cell", grid);
                var medallion = new Medallion(HudIcon.Cutter);
                medallion.AddToClassList("group-medallion");
                cell.Add(medallion);
                var meter = new MeterBar(HudPalette.OliveLight);
                meter.AddToClassList("group-meter");
                cell.Add(meter);
                var index = i;
                cell.RegisterCallback<ClickEvent>(evt => ClickGroupCell(index, evt.shiftKey));
                Tip(cell, () => GroupTip(index));
                groupCells.Add((cell, medallion, meter));
            }
            groupMore = Text(string.Empty, "small", groupView);
        }

        void BuildSettlementCard(VisualElement parent)
        {
            settlementCard = Element("card-row", parent);
            portPortrait = new Medallion(HudIcon.Settlement);
            portPortrait.AddToClassList("card-portrait");
            settlementCard.Add(portPortrait);
            var body = Element("card-body", settlementCard);
            portCardName = Text(string.Empty, "title card-name", body);
            var hull = Element("card-line", body);
            Icon(hull, HudIcon.Settlement, HudPalette.Parchment);
            Text("Mury", "small", hull);
            portHullMeter = new MeterBar(HudPalette.OliveLight, 10);
            portHullMeter.AddToClassList("card-meter");
            hull.Add(portHullMeter);
            portCardHull = Text(string.Empty, "num card-value", hull);
            var wood = Element("card-line", body);
            Icon(wood, HudIcon.Wood, HudPalette.Parchment);
            portWoodMeter = new MeterBar(HudPalette.Timber, 10);
            portWoodMeter.AddToClassList("card-meter");
            wood.Add(portWoodMeter);
            portWood = Text(string.Empty, "num card-value", wood);
            var silver = Element("card-line", body);
            Icon(silver, HudIcon.Silver, HudPalette.Marble);
            portSilverMeter = new MeterBar(HudPalette.Silver, 10);
            portSilverMeter.AddToClassList("card-meter");
            silver.Add(portSilverMeter);
            portSilver = Text(string.Empty, "num card-value", silver);
            var berths = Element("card-line", body);
            Icon(berths, HudIcon.Berth, HudPalette.Parchment);
            portBerths = Text(string.Empty, "small", berths);
            portYard = Text(string.Empty, "small", berths);
            portYard.style.marginLeft = 18;
        }

        void BuildHelp(VisualElement parent)
        {
            helpView = Element(null, parent);
            Text("Dowodzenie flotą", "title panel-title", helpView);
            foreach (var line in new[]
            {
                "<color=#F0C978>LPM</color> zaznacza statek, ramka – wiele. Klik na port wybiera go jako aktywny.",
                "<color=#F0C978>PPM</color> wydaje rozkaz według celu: wróg – atak, złoże – zbiór, wolny brzeg z handlowcem – przyczółek, własny port – dostawa, własny handlowiec – eskorta.",
                "<color=#F0C978>Ctrl+1–9</color> zapamiętuje grupę, <color=#F0C978>1–9</color> ją wybiera. <color=#F0C978>WASD</color>, kółko, <color=#F0C978>Q/E</color>, środkowy przycisk – kamera.",
                "Bez zaznaczenia panel rozkazów służy do zamawiania statków w aktywnym porcie.",
            })
            {
                var label = Text(line, "help-line", helpView);
                label.enableRichText = true;
            }
        }

        YardLine YardRow(VisualElement panel, bool war)
        {
            var line = new YardLine { Root = Element("yard-line", panel) };
            var label = Element("yard-label", line.Root);
            Icon(label, war ? HudIcon.WarLine : HudIcon.PortLine, HudPalette.OchreLight).AddToClassList("yard-label-icon");
            Text(war ? "Wojenna" : "Portowa", "small", label);
            Tip(label, () => new TipContent
            {
                Title = war ? "Linia wojenna" : "Linia portowa",
                Body = war ? "Okręty wojenne: taranowce, niszczyciele załóg i abordażowce. Wymaga stoczni." : "Statki gospodarcze: kutry i handlowce. Ma ją każdy port.",
            });

            line.Current = new Medallion(HudIcon.Cutter);
            line.Current.AddToClassList("yard-current");
            line.Root.Add(line.Current);
            line.Current.RegisterCallback<ClickEvent>(_ => battle.CancelQueue(war, 0));
            Tip(line.Current, () => QueueTip(war, 0));

            var info = Element("yard-info", line.Root);
            line.Name = Text(string.Empty, "num", info);
            line.Stage = Text(string.Empty, "small", info);
            line.Empty = Text(string.Empty, "yard-empty", line.Root);

            line.Queue = Element(null, line.Root);
            line.Queue.style.flexDirection = FlexDirection.Row;
            for (var i = 1; i <= 3; i++)
            {
                var next = new Medallion(HudIcon.Cutter);
                next.AddToClassList("yard-next");
                line.Queue.Add(next);
                var index = i;
                next.RegisterCallback<ClickEvent>(_ => battle.CancelQueue(war, index));
                Tip(next, () => QueueTip(war, index));
                line.Next.Add(next);
            }
            return line;
        }

        Slot MakeSlot(VisualElement grid)
        {
            var slot = new Slot { Root = Element("slot", grid) };
            slot.Icon = new Medallion(HudIcon.None);
            slot.Icon.AddToClassList("slot-medallion");
            slot.Root.Add(slot.Icon);
            slot.Key = Text(string.Empty, "slot-key", slot.Root);
            slot.Cost = Text(string.Empty, "slot-cost", slot.Root);
            slot.Root.RegisterCallback<ClickEvent>(_ => { if (slot.Enabled) slot.Click?.Invoke(); });
            Tip(slot.Root, () => slot.Tip != null ? slot.Tip() : default);
            return slot;
        }

        void BuildOverlays(VisualElement parent)
        {
            modeHint = Element("mode-hint", parent);
            modeHint.pickingMode = PickingMode.Ignore;
            modeIcon = new IconElement(HudIcon.Attack, HudPalette.OchreLight);
            modeIcon.AddToClassList("mode-hint-icon");
            modeHint.Add(modeIcon);
            modeText = Text(string.Empty, null, modeHint);
            modeHint.style.display = DisplayStyle.None;

            pauseBanner = Element("pause-banner", parent);
            pauseBanner.pickingMode = PickingMode.Ignore;
            var card = Element("pause-card", pauseBanner);
            card.pickingMode = PickingMode.Ignore;
            var top = new OrnamentBand(OrnamentKind.Meander, HudPalette.Ochre, Color.clear);
            top.AddToClassList("pause-band");
            card.Add(top);
            Text("PAUZA", "title pause-text", card);
            var bottom = new OrnamentBand(OrnamentKind.Wave, HudPalette.Ochre, Color.clear);
            bottom.AddToClassList("pause-band");
            card.Add(bottom);
            pauseBanner.style.display = DisplayStyle.None;

            endOverlay = Element("overlay", parent);
            var dialog = Element("dialog", endOverlay);
            dialog.Add(new PanelFrame(HudPalette.OchreLight));
            endTitle = Text(string.Empty, "title dialog-title", dialog);
            var band = new OrnamentBand(OrnamentKind.EggAndDart, HudPalette.Ochre, Color.clear);
            band.style.width = 360;
            band.style.height = 12;
            band.style.marginBottom = 14;
            dialog.Add(band);
            endText = Text(string.Empty, "dialog-text", dialog);
            TextButton(dialog, "Nowa bitwa", () => battle.NewGame());
            endOverlay.style.display = DisplayStyle.None;

            settingsOverlay = Element("overlay", parent);
            var settings = Element("dialog", settingsOverlay);
            settings.style.width = 640;
            settings.Add(new PanelFrame(HudPalette.OchreLight));
            Text("Ustawienia obrazu", "title dialog-title", settings).style.fontSize = 30;
            var columns = Element("settings-columns", settings);
            profileColumn = Element("settings-column", columns);
            resolutionColumn = Element("settings-column", columns);
            settingsNote = Text(string.Empty, "dialog-text small", settings);
            TextButton(settings, "Zamknij", ToggleSettings);
            settingsOverlay.style.display = DisplayStyle.None;

            tooltip = Element("tooltip", parent);
            tooltip.pickingMode = PickingMode.Ignore;
            tipTitle = Text(string.Empty, "title tooltip-title", tooltip);
            tipBody = Text(string.Empty, "tooltip-body", tooltip);
            tipCost = Text(string.Empty, "tooltip-cost", tooltip);
            tipKey = Text(string.Empty, "tooltip-key", tooltip);
            tooltip.style.display = DisplayStyle.None;
        }

        // ---------- Frame ----------

        void Update()
        {
            if (battle == null || root == null || battle.World == null) return;
            HandleKeys();

            textTimer -= Time.unscaledDeltaTime;
            if (textTimer <= 0f)
            {
                textTimer = TextRefresh;
                Refresh();
            }

            chartTimer -= Time.unscaledDeltaTime;
            if (chartTimer <= 0f)
            {
                chartTimer = ChartRefresh;
                minimap.Refresh();
            }
            minimap.RefreshView();

            UpdateHealthBars();
            UpdateSelectionBox();
            UpdateModeHint();
            UpdateHeralds();
        }

        /// <summary>True while the pointer is over a panel, a button or a dialog, so a click there never reaches the sea.</summary>
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
            RefreshSelection();
            RefreshYard();
            RefreshSlots();
            RefreshOverlays();
            if (tooltipOwner != null && tooltipContent != null) FillTooltip(tooltipContent());
        }

        void RefreshTopBar()
        {
            var world = battle.World;
            var player = BattleController.PlayerFaction;
            var port = battle.ActivePort;
            if (port != null)
            {
                SetText(portName, port.IsOutpost ? "Przyczółek" : "Osada");
                SetText(portHull, $"mury {Mathf.CeilToInt((float)port.Hull)} / {Mathf.CeilToInt((float)port.MaxHull)}");
                portMedallion.Set(port.IsOutpost ? HudIcon.Outpost : HudIcon.Settlement, HudPalette.Parchment, HudPalette.Aegean);
                Resource(port, ResourceType.Wood, woodValue, woodCap, woodReserve, woodMeter, port.ReservedWood);
                Resource(port, ResourceType.Silver, silverValue, silverCap, silverReserve, silverMeter, port.ReservedSilver);
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
            SetText(transitValue, $"{woodTransit / Rules.Centi} D · {silverTransit / Rules.Centi} S");
            SetText(fleetValue, $"{fleet} / {Rules.FleetLimit}");
            SetText(portsValue, world.PortsOf(player).Count().ToString());
            var seconds = world.Tick / Rules.TickRate;
            SetText(timeValue, $"{seconds / 60}:{seconds % 60:00}");
            SetText(speedValue, $"×{battle.Speed:0.#}");
            pauseButton.Set(battle.Paused ? HudIcon.Play : HudIcon.Pause, battle.Paused ? HudPalette.OchreLight : HudPalette.Parchment);
            slowerButton.Set(HudIcon.Slower, HudPalette.Parchment, null, -1f, !battle.CanSpeedDown);
            fasterButton.Set(HudIcon.Faster, HudPalette.Parchment, null, -1f, !battle.CanSpeedUp);
        }

        static void Resource(Settlement port, ResourceType type, Label value, Label capacity, Label reserve, MeterBar meter, long reserved)
        {
            var available = port.Available(type) / Rules.Centi;
            var cap = port.Capacity(type) / Rules.Centi;
            SetText(value, available.ToString());
            SetText(capacity, $"/ {cap}");
            SetText(reserve, reserved > 0 ? $"rez. {reserved / Rules.Centi}" : "bez rezerwy");
            var fill = cap > 0 ? (float)port.Stock(type) / port.Capacity(type) : 0f;
            meter.Set(fill, fill > 0.9f ? HudPalette.CinnabarLight : type == ResourceType.Wood ? HudPalette.Timber : HudPalette.Silver);
        }

        void RefreshSelection()
        {
            var world = battle.World;
            var own = battle.Selected.Select(world.FindShip).Where(ship => ship != null).OrderBy(ship => ship.Class).ThenBy(ship => ship.Id).ToList();
            var enemies = battle.InspectedEnemies.Select(world.FindShip).Where(ship => ship != null && world.CanSee(BattleController.PlayerFaction, ship)).ToList();
            var inspectedPort = world.FindSettlement(battle.InspectedSettlementId);

            Ship single = own.Count == 1 ? own[0] : own.Count == 0 && enemies.Count >= 1 ? enemies[0] : null;
            Show(shipCard, single != null);
            Show(groupView, single == null && own.Count > 1);
            Show(settlementCard, single == null && own.Count <= 1 && inspectedPort != null && !inspectedPort.Destroyed);
            Show(helpView, single == null && own.Count <= 1 && (inspectedPort == null || inspectedPort.Destroyed));

            if (single != null) FillShipCard(single);
            else if (own.Count > 1) FillGroup(own);
            else if (inspectedPort != null && !inspectedPort.Destroyed) FillSettlementCard(inspectedPort);
        }

        void FillShipCard(Ship ship)
        {
            var player = BattleController.PlayerFaction;
            var definition = ship.Definition;
            var enemy = ship.Owner != player;
            cardPortrait.Set(HudIcons.ForClass(ship.Class), enemy ? HudPalette.ParchmentDark : HudPalette.Parchment, HudPalette.Owner(ship.Owner, player));
            SetText(cardName, BattleController.ClassName(ship.Class));
            SetText(cardOwner, enemy ? "WRÓG – podgląd" : string.Empty);
            var hull = (float)(ship.Hull / definition.Hull);
            hullMeter.Set(hull, HudPalette.Hull(hull));
            SetText(hullValue, $"{Mathf.CeilToInt((float)ship.Hull)} / {Mathf.CeilToInt((float)definition.Hull)}");
            SetText(sailorsValue, ship.Sailors.ToString());
            SetText(soldiersValue, ship.Soldiers.ToString());
            SetText(moraleValue, $"{Mathf.RoundToInt((float)ship.Morale)}%");

            var capacity = definition.Capacity * Rules.Centi;
            Show(cargoLine, capacity > 0 && !enemy);
            if (capacity > 0)
            {
                var held = ship.Cargo + ship.HoldWood + ship.HoldSilver;
                cargoMeter.Set((float)held / capacity, ship.HoldSilver > 0 || ship.CargoType == ResourceType.Silver && ship.Cargo > 0 ? HudPalette.Silver : HudPalette.Timber);
                var kind = ship.HoldWood + ship.HoldSilver > 0 ? BattleController.DescribeCargo(ship) : ship.Cargo > 0 ? $"{ship.Cargo / Rules.Centi} {(ship.CargoType == ResourceType.Wood ? "D" : "S")}" : "pusta";
                SetText(cargoValue, $"{kind} / {definition.Capacity}");
            }
            SetText(orderText, enemy ? "Stan widoczny, dopóki twoje statki go obserwują." : battle.DescribeOrder(ship));
        }

        void FillGroup(List<Ship> ships)
        {
            var player = BattleController.PlayerFaction;
            for (var i = 0; i < groupCells.Count; i++)
            {
                var (cell, medallion, meter) = groupCells[i];
                var shown = i < ships.Count;
                Show(cell, shown);
                if (!shown) continue;
                var ship = ships[i];
                medallion.Set(HudIcons.ForClass(ship.Class), HudPalette.Parchment, HudPalette.Owner(ship.Owner, player));
                var hull = (float)(ship.Hull / ship.Definition.Hull);
                meter.Set(hull, HudPalette.Hull(hull));
                cell.userData = ship.Id;
            }
            SetText(groupMore, ships.Count > groupCells.Count ? $"oraz {ships.Count - groupCells.Count} kolejnych" : $"{ships.Count} statków · klik: tylko ten · Shift+klik: odznacz");
        }

        void FillSettlementCard(Settlement port)
        {
            var player = BattleController.PlayerFaction;
            var own = port.Owner == player;
            portPortrait.Set(port.IsOutpost ? HudIcon.Outpost : HudIcon.Settlement, own ? HudPalette.Parchment : HudPalette.ParchmentDark, HudPalette.Owner(port.Owner, player));
            SetText(portCardName, (port.IsOutpost ? "Przyczółek" : "Osada") + (own ? string.Empty : " wroga"));
            var hull = (float)(port.Hull / port.MaxHull);
            portHullMeter.Set(hull, HudPalette.Hull(hull));
            SetText(portCardHull, $"{Mathf.CeilToInt((float)port.Hull)} / {Mathf.CeilToInt((float)port.MaxHull)}");
            if (own)
            {
                portWoodMeter.Set((float)port.Wood / Math.Max(1L, port.Capacity(ResourceType.Wood)), HudPalette.Timber);
                portSilverMeter.Set((float)port.Silver / Math.Max(1L, port.Capacity(ResourceType.Silver)), HudPalette.Silver);
                SetText(portWood, $"{port.Wood / Rules.Centi} / {port.Capacity(ResourceType.Wood) / Rules.Centi}");
                SetText(portSilver, $"{port.Silver / Rules.Centi} / {port.Capacity(ResourceType.Silver) / Rules.Centi}");
                SetText(portBerths, $"Stanowiska {port.DockedShips.Count}/{Rules.PortBerths}" + (port.PortWaiting.Count > 0 ? $", czeka {port.PortWaiting.Count}" : string.Empty));
                SetText(portYard, port.HasShipyard ? "Stocznia: czynna" : port.ShipyardBuild != null ? $"Stocznia: budowa {Mathf.RoundToInt(100f * port.ShipyardBuild.DoneTicks / port.ShipyardBuild.TotalTicks)}%" : "Stocznia: brak");
            }
            else
            {
                portWoodMeter.Set(0f, HudPalette.Timber);
                portSilverMeter.Set(0f, HudPalette.Silver);
                SetText(portWood, "nieznane");
                SetText(portSilver, "nieznane");
                SetText(portBerths, "Magazyny wroga są niewidoczne.");
                SetText(portYard, string.Empty);
            }
        }

        void RefreshYard()
        {
            var port = battle.ActivePort;
            if (port == null) return;
            SetText(yardTitle, port.IsOutpost ? "Stocznia przyczółka" : "Stocznia osady");
            FillYardLine(warLine, port.WarQueue, port.IsOutpost && !port.HasShipyard ? (port.ShipyardBuild != null ? $"Stocznia w budowie: {Mathf.RoundToInt(100f * port.ShipyardBuild.DoneTicks / port.ShipyardBuild.TotalTicks)}%" : "Brak stoczni – zbuduj ją w rozkazach") : "Wolna – zamów okręt wojenny");
            FillYardLine(portLine, port.PortQueue, "Wolna – zamów kuter lub handlowca");
        }

        void FillYardLine(YardLine line, List<BuildOrder> queue, string idle)
        {
            var busy = queue.Count > 0;
            Show(line.Current, busy);
            Show(line.Name.parent, busy);
            Show(line.Empty, !busy);
            SetText(line.Empty, idle);
            if (busy)
            {
                var order = queue[0];
                var progress = order.TotalTicks > 0 ? (float)order.DoneTicks / order.TotalTicks : 0f;
                line.Current.Set(HudIcons.ForClass(order.Class), HudPalette.Parchment, null, progress);
                SetText(line.Name, BattleController.ClassName(order.Class));
                SetText(line.Stage, $"{BattleController.StageName(progress)} · {Mathf.FloorToInt(progress * 100f)}% · {Mathf.CeilToInt(BattleController.SecondsLeft(order))} s");
            }
            for (var i = 0; i < line.Next.Count; i++)
            {
                var shown = i + 1 < queue.Count;
                Show(line.Next[i], shown);
                if (shown) line.Next[i].Set(HudIcons.ForClass(queue[i + 1].Class), HudPalette.ParchmentDark);
            }
        }

        // ---------- Orders and production ----------

        sealed class OrderDef
        {
            public HudIcon Icon;
            public string Name, Key, Body;
            public KeyCode Code;
            public Func<Ship, bool> Able;
            public HudOrder Targeted;
            public Action<BattleController> Instant;
        }

        static readonly OrderDef[] Orders =
        {
            new OrderDef { Icon = HudIcon.Move, Name = "Płyń", Key = "V", Code = KeyCode.V, Able = _ => true, Targeted = HudOrder.Move, Body = "Wskaż miejsce na morzu. Statki omijają ląd i płycizny." },
            new OrderDef { Icon = HudIcon.Stop, Name = "Stój", Key = "X", Code = KeyCode.X, Able = _ => true, Instant = b => b.StopSelected(), Body = "Przerywa rozkaz; statki zatrzymują się." },
            new OrderDef { Icon = HudIcon.Attack, Name = "Ostrzał", Key = "T", Code = KeyCode.T, Able = s => s.Definition.LightSlots > 0, Targeted = HudOrder.Attack, Body = "Łucznicy ostrzeliwują wskazany wrogi okręt lub osadę z dystansu." },
            new OrderDef { Icon = HudIcon.RamOrder, Name = "Taran", Key = "G", Code = KeyCode.G, Able = s => s.Class == ShipClass.Ram, Targeted = HudOrder.Ram, Body = "Taranowiec szarżuje na wskazany okręt, odskakuje i ponawia atak." },
            new OrderDef { Icon = HudIcon.Board, Name = "Abordaż", Key = "B", Code = KeyCode.B, Able = s => s.Class == ShipClass.Boarder, Targeted = HudOrder.Board, Body = "Abordażowiec rzuca haki i walczy wręcz, by zdobyć okręt." },
            new OrderDef { Icon = HudIcon.Volley, Name = "Ostrzał z abordażowca", Key = "F", Code = KeyCode.F, Able = s => s.Class == ShipClass.Boarder, Targeted = HudOrder.Volley, Body = "Abordażowiec strzela z łuków zamiast szukać zwarcia." },
            new OrderDef { Icon = HudIcon.Harvest, Name = "Zbiór", Key = "H", Code = KeyCode.H, Able = s => s.Definition.CanHarvest, Targeted = HudOrder.Harvest, Body = "Kutry płyną do złoża drewna lub srebra i wożą urobek do portu; mogą też zbierać wraki." },
            new OrderDef { Icon = HudIcon.Deliver, Name = "Dostawa", Key = "L", Code = KeyCode.L, Able = s => s.Class == ShipClass.Merchant, Targeted = HudOrder.Deliver, Body = "Handlowiec wiezie ładunek z aktywnego portu do wskazanego własnego portu." },
            new OrderDef { Icon = HudIcon.FoundOutpost, Name = "Przyczółek", Key = "P", Code = KeyCode.P, Able = s => s.Class == ShipClass.Merchant, Targeted = HudOrder.Outpost, Body = $"Handlowiec zakłada przyczółek na wskazanym wolnym brzegu ({Rules.OutpostWood / Rules.Centi} D / {Rules.OutpostSilver / Rules.Centi} S z ładowni)." },
            new OrderDef { Icon = HudIcon.Escort, Name = "Eskorta", Key = "K", Code = KeyCode.K, Able = s => !s.Definition.IsEconomic, Targeted = HudOrder.Escort, Body = "Okręty płyną przy burcie wskazanego handlowca i bronią go w jego strefie." },
            new OrderDef { Icon = HudIcon.Repair, Name = "Remont", Key = "R", Code = KeyCode.None, Able = _ => true, Instant = b => b.RepairSelected(), Body = "Statki płyną do stoczni najbliższego własnego portu na naprawę." },
            new OrderDef { Icon = HudIcon.Scrap, Name = "Złomowanie", Key = "Del", Code = KeyCode.Delete, Able = _ => true, Instant = b => b.ScrapSelected(), Body = "Rozbiera statek w porcie i odzyskuje część materiałów. Nie da się cofnąć." },
        };

        void RefreshSlots()
        {
            var world = battle.World;
            var ships = battle.Selected.Select(world.FindShip).Where(ship => ship != null && ship.Owner == BattleController.PlayerFaction).ToList();
            production = ships.Count == 0;
            if (production) RefreshProduction();
            else
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    var order = Orders[i];
                    var able = ships.Any(order.Able);
                    var active = order.Targeted != HudOrder.None && battle.PendingOrder == order.Targeted;
                    ShowSlot(slot, order.Icon, order.Key, string.Empty, able, active);
                    var definition = order;
                    slot.Click = () => Run(definition);
                    slot.Tip = () => new TipContent
                    {
                        Title = definition.Name,
                        Body = definition.Body + (ships.Any(definition.Able) ? string.Empty : "\nŻaden z zaznaczonych statków nie wykona tego rozkazu."),
                        Key = definition.Targeted != HudOrder.None ? $"Klawisz: {definition.Key}, potem LPM na celu (Shift – kolejne cele, PPM/Esc – anuluj)" : $"Klawisz: {definition.Key}",
                    };
                }
            }
        }

        void RefreshProduction()
        {
            var port = battle.ActivePort;
            var classes = new[] { ShipClass.Cutter, ShipClass.Merchant, ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder };
            var fleet = battle.World.Ships.Count(ship => ship.Owner == BattleController.PlayerFaction);
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (port == null) { Show(slot.Root, false); continue; }
                if (i < classes.Length)
                {
                    var shipClass = classes[i];
                    var definition = ShipCatalog.Get(shipClass);
                    var yardMissing = !definition.IsEconomic && !port.HasShipyard;
                    var affordable = port.Available(ResourceType.Wood) >= definition.CostWood * Rules.Centi && port.Available(ResourceType.Silver) >= definition.CostSilver * Rules.Centi;
                    var able = !yardMissing && affordable && fleet < Rules.FleetLimit;
                    ShowSlot(slot, HudIcons.ForClass(shipClass), string.Empty, $"{definition.CostWood} D · {definition.CostSilver} S", able, false);
                    slot.Click = () => battle.QueueShip(shipClass);
                    slot.Tip = () => new TipContent
                    {
                        Title = BattleController.ClassName(shipClass),
                        Body = ClassDescription(shipClass) + $"\nKadłub {definition.Hull}, prędkość {definition.Speed:0.#}, załoga do {definition.MaxCrew}" + (definition.Capacity > 0 ? $", ładownia {definition.Capacity}." : ".")
                               + (yardMissing ? "\nWymaga stoczni w tym porcie." : !affordable ? "\nZa mało zasobów w magazynie aktywnego portu." : string.Empty),
                        Cost = $"Koszt: {definition.CostWood} D · {definition.CostSilver} S · {definition.BuildSeconds:0} s budowy",
                        Key = definition.IsEconomic ? "Linia portowa" : "Linia wojenna",
                    };
                }
                else if (i == classes.Length && !port.HasShipyard && port.ShipyardBuild == null)
                {
                    var affordable = port.Available(ResourceType.Wood) >= Rules.ShipyardWood && port.Available(ResourceType.Silver) >= Rules.ShipyardSilver;
                    ShowSlot(slot, HudIcon.Shipyard, string.Empty, $"{Rules.ShipyardWood / Rules.Centi} D · {Rules.ShipyardSilver / Rules.Centi} S", affordable, false);
                    slot.Click = () => battle.BuildShipyard();
                    slot.Tip = () => new TipContent
                    {
                        Title = "Stocznia",
                        Body = "Pochylnia linii wojennej na przyczółku: pozwala budować tu okręty wojenne." + (affordable ? string.Empty : "\nZa mało zasobów w magazynie przyczółka."),
                        Cost = $"Koszt: {Rules.ShipyardWood / Rules.Centi} D · {Rules.ShipyardSilver / Rules.Centi} S · {Rules.ShipyardBuildSeconds:0} s",
                    };
                }
                else Show(slot.Root, false);
            }
        }

        static string ClassDescription(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.Cutter: return "Mały statek roboczy: zbiera drewno i srebro na złożach i wraki.";
                case ShipClass.Merchant: return "Duży transportowiec: wozi zasoby między portami i zakłada przyczółki. Bez uzbrojenia.";
                case ShipClass.Ram: return "Szybki okręt z brązowym taranem; jedno stanowisko łuczników.";
                case ShipClass.CrewDestroyer: return "Cztery stanowiska łuczników: przerzedza załogi wroga z dystansu.";
                default: return "Ciężki okręt z liczną załogą i hakami: zdobywa wrogie statki; dwa stanowiska łuczników.";
            }
        }

        static void ShowSlot(Slot slot, HudIcon icon, string key, string cost, bool enabled, bool active)
        {
            Show(slot.Root, true);
            slot.Enabled = enabled;
            slot.Icon.Set(icon, active ? HudPalette.OchreLight : HudPalette.Parchment, null, -1f, !enabled, active);
            SetText(slot.Key, key);
            SetText(slot.Cost, cost);
            slot.Root.EnableInClassList("disabled", !enabled);
        }

        void Run(OrderDef order)
        {
            if (order.Instant != null) { order.Instant(battle); return; }
            battle.PendingOrder = battle.PendingOrder == order.Targeted ? HudOrder.None : order.Targeted;
            Refresh();
        }

        void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.F9) || Input.GetKeyDown(KeyCode.F10)) ToggleSettings();
            if (production || settingsOverlay.style.display == DisplayStyle.Flex) return;
            var ships = battle.Selected.Select(battle.World.FindShip).Where(ship => ship != null).ToList();
            foreach (var order in Orders)
            {
                if (order.Code == KeyCode.None || !Input.GetKeyDown(order.Code)) continue;
                if (ships.Any(order.Able)) Run(order);
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
                    bar.hull.Set(hull, own ? HudPalette.Hull(hull) : HudPalette.CinnabarLight);
                    bar.crew.Set((float)(ship.Sailors + ship.Soldiers) / Mathf.Max(1, ship.Definition.MaxCrew), HudPalette.Ochre);
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
                        bar.hull.Set(hull, inspected.Owner == BattleController.PlayerFaction ? HudPalette.Hull(hull) : HudPalette.CinnabarLight);
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
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return false;
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
                var hull = new MeterBar(HudPalette.OliveLight, 5);
                hull.AddToClassList("hp-hull");
                element.Add(hull);
                var crew = new MeterBar(HudPalette.Ochre);
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

        void UpdateModeHint()
        {
            var order = battle.PendingOrder;
            if (order == HudOrder.None || root.panel == null) { Show(modeHint, false); return; }
            var definition = Orders.FirstOrDefault(item => item.Targeted == order);
            if (definition == null) { Show(modeHint, false); return; }
            Show(modeHint, true);
            modeIcon.Set(definition.Icon, HudPalette.OchreLight);
            SetText(modeText, $"{definition.Name}: wskaż cel · PPM/Esc anuluje");
            var mouse = Input.mousePosition;
            var at = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            modeHint.style.translate = new StyleTranslate(new Translate(at.x + 22f, at.y + 18f));
        }

        // ---------- Herald ----------

        void OnHerald(HeraldMessage message)
        {
            if (heraldColumn == null) return;
            var item = Element("herald-item", null);
            if (message.Level == HeraldLevel.Important) item.AddToClassList("important");
            if (message.Level == HeraldLevel.Critical) item.AddToClassList("critical");
            item.style.backgroundImage = Background.FromTexture2D(HudTextures.Marble);
            var seal = new Medallion(HudIcon.Load);
            seal.Set(message.Topic == HeraldTopic.Alarm || message.Level == HeraldLevel.Critical ? HudIcon.Alarm : message.Topic == HeraldTopic.Launch ? HudIcon.Shipyard : HudIcon.Load,
                message.Level == HeraldLevel.Critical ? HudPalette.CinnabarLight : HudPalette.Parchment);
            seal.AddToClassList("herald-seal");
            item.Add(seal);
            Text(message.Text, "herald-text", item);
            item.RegisterCallback<ClickEvent>(_ =>
            {
                if (message.At.HasValue) battle.FocusCamera(message.At.Value);
                item.RemoveFromHierarchy();
                heralds.RemoveAll(entry => entry.element == item);
            });
            if (message.At.HasValue && message.Topic == HeraldTopic.Alarm) minimap?.Alarm(message.At.Value);

            heraldColumn.Insert(0, item);
            var life = message.Level == HeraldLevel.Critical ? 30f : message.Level == HeraldLevel.Important ? 10f : 6f;
            heralds.Insert(0, (item, Time.unscaledTime + life, message));
            while (heralds.Count > 5)
            {
                heralds[heralds.Count - 1].element.RemoveFromHierarchy();
                heralds.RemoveAt(heralds.Count - 1);
            }
        }

        void UpdateHeralds()
        {
            for (var i = heralds.Count - 1; i >= 0; i--)
            {
                var (element, until, _) = heralds[i];
                var left = until - Time.unscaledTime;
                if (left <= 0f)
                {
                    element.RemoveFromHierarchy();
                    heralds.RemoveAt(i);
                    continue;
                }
                element.style.opacity = Mathf.Clamp01(left);
            }
        }

        // ---------- Overlays: pause, end of battle, settings ----------

        void RefreshOverlays()
        {
            Show(pauseBanner, battle.Paused && battle.World.Outcome == OutcomeKind.InProgress);
            var over = battle.World.Outcome != OutcomeKind.InProgress;
            Show(endOverlay, over);
            if (over)
            {
                var won = battle.World.Winner == BattleController.PlayerFaction;
                SetText(endTitle, battle.World.Outcome == OutcomeKind.Draw ? "REMIS" : won ? "ZWYCIĘSTWO" : "PORAŻKA");
                SetText(endText, battle.World.Outcome == OutcomeKind.Draw ? "Żadna ze stron nie przeważyła." : won ? "Osada wroga padła. Morza są twoje." : "Twoja osada padła. Flota rozproszona.");
            }

            if (settingsOverlay.style.display == DisplayStyle.Flex) RefreshSettings();
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

        void ToggleSettings()
        {
            var open = settingsOverlay.style.display != DisplayStyle.Flex;
            Show(settingsOverlay, open);
            if (open) RefreshSettings(true);
        }

        void RefreshSettings(bool rebuild = false)
        {
            if (rebuild || profileColumn.childCount == 0)
            {
                profileColumn.Clear();
                resolutionColumn.Clear();
                Text("Profil graficzny", "title panel-title", profileColumn);
                foreach (var profile in GraphicsProfiles.All)
                {
                    var chosen = profile;
                    TextButton(profileColumn, GraphicsProfiles.DisplayName(profile), () => { profiles?.Select(chosen); RefreshSettings(true); })
                        .EnableInClassList("chosen", profiles != null && profiles.Current == profile);
                }
                Text("Rozdzielczość", "title panel-title", resolutionColumn);
                if (display != null)
                {
                    foreach (var mode in display.Available)
                    {
                        var chosen = mode;
                        TextButton(resolutionColumn, mode.ToString(), () => { display.Request(chosen, Screen.fullScreen); RefreshSettings(true); })
                            .EnableInClassList("chosen", Screen.width == mode.Width && Screen.height == mode.Height);
                    }
                }
                ApplyFonts(settingsOverlay);
            }

            if (display != null && display.Session.State == ResolutionChangeState.AwaitingConfirmation)
            {
                SetText(settingsNote, $"Zachować {display.Session.Requested}? Powrót za {Mathf.CeilToInt(display.Session.RemainingSeconds)} s – kliknij rozdzielczość ponownie, aby zatwierdzić.");
                if (!settingsNote.ClassListContains("confirm"))
                {
                    settingsNote.AddToClassList("confirm");
                    settingsNote.RegisterCallback<ClickEvent>(_ => display.Confirm());
                }
            }
            else SetText(settingsNote, "Profil zmienia jakość oświetlenia, wody i efektów. Rozdzielczość trzeba potwierdzić w ciągu 15 s.");
        }

        // ---------- Tooltips ----------

        void Tip(VisualElement element, Func<TipContent> content)
        {
            element.RegisterCallback<PointerEnterEvent>(_ =>
            {
                tooltipOwner = element;
                tooltipContent = content;
                var tip = content();
                if (string.IsNullOrEmpty(tip.Title) && string.IsNullOrEmpty(tip.Body)) return;
                FillTooltip(tip);
                Show(tooltip, true);
                tooltip.schedule.Execute(PlaceTooltip).StartingIn(0);
            });
            element.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (tooltipOwner != element) return;
                tooltipOwner = null;
                tooltipContent = null;
                Show(tooltip, false);
            });
        }

        void FillTooltip(TipContent tip)
        {
            SetText(tipTitle, tip.Title ?? string.Empty);
            SetText(tipBody, tip.Body ?? string.Empty);
            SetText(tipCost, tip.Cost ?? string.Empty);
            SetText(tipKey, tip.Key ?? string.Empty);
            Show(tipCost, !string.IsNullOrEmpty(tip.Cost));
            Show(tipKey, !string.IsNullOrEmpty(tip.Key));
        }

        /// <summary>Above the element it explains, kept inside the screen.</summary>
        void PlaceTooltip()
        {
            if (tooltipOwner == null || root.panel == null) return;
            var owner = tooltipOwner.worldBound;
            var size = tooltip.layout.size;
            var bounds = root.worldBound;
            var x = Mathf.Clamp(owner.center.x - size.x * 0.5f, bounds.xMin + 8f, bounds.xMax - size.x - 8f);
            var y = owner.yMin - size.y - 10f;
            if (y < bounds.yMin + 64f) y = owner.yMax + 10f;
            tooltip.style.left = x;
            tooltip.style.top = y;
        }

        TipContent QueueTip(bool war, int index)
        {
            var port = battle.ActivePort;
            var queue = port == null ? null : war ? port.WarQueue : port.PortQueue;
            if (queue == null || index >= queue.Count) return default;
            var order = queue[index];
            var progress = order.TotalTicks > 0 ? (float)order.DoneTicks / order.TotalTicks : 0f;
            return new TipContent
            {
                Title = BattleController.ClassName(order.Class),
                Body = index == 0 ? $"Na pochylni: {BattleController.StageName(progress)}, {Mathf.FloorToInt(progress * 100f)}%. Do wodowania {Mathf.CeilToInt(BattleController.SecondsLeft(order))} s." : $"Czeka w kolejce na miejscu {index + 1}.",
                Key = "Klik: anuluj zamówienie (zasoby wracają według zasad kolejki)",
            };
        }

        TipContent GroupTip(int index)
        {
            var cell = groupCells[index].cell;
            if (!(cell.userData is int id)) return default;
            var ship = battle.World.FindShip(id);
            if (ship == null) return default;
            return new TipContent
            {
                Title = BattleController.ClassName(ship.Class),
                Body = $"Kadłub {Mathf.CeilToInt((float)ship.Hull)}/{Mathf.CeilToInt((float)ship.Definition.Hull)}, załoga {ship.Sailors + ship.Soldiers}. {battle.DescribeOrder(ship)}",
                Key = "Klik: zaznacz tylko ten · Shift+klik: odznacz",
            };
        }

        void ClickGroupCell(int index, bool shift)
        {
            if (!(groupCells[index].cell.userData is int id)) return;
            if (shift) battle.Deselect(id);
            else battle.SelectOnly(id);
            Refresh();
        }

        void CyclePort()
        {
            var ports = battle.World.PortsOf(BattleController.PlayerFaction).ToList();
            if (ports.Count == 0) return;
            var current = battle.ActivePort;
            var index = current == null ? -1 : ports.FindIndex(item => item.Id == current.Id);
            var next = ports[(index + 1) % ports.Count];
            battle.SelectPort(next.Id);
            battle.FocusCamera(new Vector3((float)next.X, 0f, (float)next.Y));
            Refresh();
        }

        // ---------- Small helpers ----------

        static VisualElement Element(string classes, VisualElement parent)
        {
            var element = new VisualElement();
            if (!string.IsNullOrEmpty(classes)) foreach (var name in classes.Split(' ')) element.AddToClassList(name);
            parent?.Add(element);
            return element;
        }

        Label Text(string text, string classes, VisualElement parent)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            if (!string.IsNullOrEmpty(classes)) foreach (var name in classes.Split(' ')) label.AddToClassList(name);
            parent?.Add(label);
            return label;
        }

        static IconElement Icon(VisualElement parent, HudIcon icon, Color ink)
        {
            var element = new IconElement(icon, ink);
            element.AddToClassList("card-icon");
            parent.Add(element);
            return element;
        }

        VisualElement TextButton(VisualElement parent, string text, Action click)
        {
            var button = Element("text-button", parent);
            Text(text, "title", button).style.color = HudPalette.Glaze;
            button.RegisterCallback<ClickEvent>(_ => click());
            ApplyFonts(button);
            return button;
        }

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
