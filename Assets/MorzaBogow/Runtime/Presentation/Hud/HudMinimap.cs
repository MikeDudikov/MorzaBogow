using System;
using System.Linq;
using MorzaBogow.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace MorzaBogow.Presentation.Hud
{
    /// <summary>
    /// The chart of the sea (pinax): water, the true waterlines of the land, the fog in its three states, ports,
    /// known work points, free shores, visible ships and the camera's view. A click moves the view there; a right
    /// click orders the selected ships there. Land is drawn once; the rest is redrawn a few times a second.
    /// </summary>
    public sealed class HudMinimap : VisualElement
    {
        readonly VisualElement land, live, view;
        BattleController battle;
        Rect worldBounds;
        bool dragging;
        float alarmUntil;
        Vector2 alarmAt;

        public HudMinimap()
        {
            AddToClassList("minimap");
            land = Layer(DrawLand);
            live = Layer(DrawLive);
            view = Layer(DrawView);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<GeometryChangedEvent>(_ => land.MarkDirtyRepaint());
        }

        VisualElement Layer(Action<MeshGenerationContext> draw)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            layer.generateVisualContent += draw;
            Add(layer);
            return layer;
        }

        public void Bind(BattleController controller)
        {
            battle = controller;
            var world = controller.World;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            void Grow(double x, double y, double margin)
            {
                minX = Mathf.Min(minX, (float)(x - margin)); maxX = Mathf.Max(maxX, (float)(x + margin));
                minY = Mathf.Min(minY, (float)(y - margin)); maxY = Mathf.Max(maxY, (float)(y + margin));
            }
            foreach (var island in world.Islands) Grow(island.X, island.Y, island.Radius + 30);
            foreach (var settlement in world.Settlements) Grow(settlement.X, settlement.Y, 60);
            foreach (var node in world.Nodes) Grow(node.X, node.Y, 40);
            foreach (var site in world.Sites) Grow(site.X, site.Y, 40);
            worldBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            land.MarkDirtyRepaint();
            Refresh();
        }

        public void Refresh() => live.MarkDirtyRepaint();
        public void RefreshView() => view.MarkDirtyRepaint();

        /// <summary>Set by the interface options: no pulsing on the chart.</summary>
        public static bool ReducedMotion;

        public void Alarm(Vector3 at)
        {
            alarmAt = new Vector2(at.x, at.z);
            alarmUntil = Time.unscaledTime + 4f;
        }

        /// <summary>The chart keeps the world's proportions, centred in the element.</summary>
        Rect ChartRect()
        {
            var rect = contentRect;
            var aspect = worldBounds.width / Mathf.Max(1f, worldBounds.height);
            var width = rect.width;
            var height = width / aspect;
            if (height > rect.height) { height = rect.height; width = height * aspect; }
            return new Rect(rect.center.x - width * 0.5f, rect.center.y - height * 0.5f, width, height);
        }

        // The game's +Y (north) is drawn upward.
        Vector2 ToChart(Rect chart, double x, double y) => new Vector2(
            chart.xMin + (float)((x - worldBounds.xMin) / worldBounds.width) * chart.width,
            chart.yMax - (float)((y - worldBounds.yMin) / worldBounds.height) * chart.height);

        Vector3 ToWorld(Vector2 local)
        {
            var chart = ChartRect();
            var x = worldBounds.xMin + (local.x - chart.xMin) / chart.width * worldBounds.width;
            var y = worldBounds.yMin + (chart.yMax - local.y) / chart.height * worldBounds.height;
            return new Vector3(x, 0f, y);
        }

        void DrawLand(MeshGenerationContext context)
        {
            if (battle == null || worldBounds.width <= 0f) return;
            var chart = ChartRect();
            HudDraw.Gradient(context, chart, HudPalette.Sea, HudPalette.SeaDeep);
            var painter = context.painter2D;
            foreach (var island in battle.World.Islands)
            {
                painter.fillColor = island.Kind == IslandKind.Coast ? HudPalette.Land : HudPalette.LandDark;
                painter.strokeColor = HudPalette.WithAlpha(HudPalette.SurfaceDeep, 0.7f);
                painter.lineWidth = 1f;
                painter.BeginPath();
                var count = LandShape.VertexCount(island);
                if (count == 0)
                {
                    painter.Arc(ToChart(chart, island.X, island.Y), (float)island.Radius / worldBounds.width * chart.width, Angle.Degrees(0f), Angle.Degrees(360f));
                }
                else
                {
                    for (var i = 0; i < count; i++)
                    {
                        LandShape.Vertex(island, i, out var x, out var y);
                        var point = ToChart(chart, x, y);
                        if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                    }
                }
                painter.ClosePath();
                painter.Fill();
                painter.Stroke();
            }
        }

        void DrawLive(MeshGenerationContext context)
        {
            if (battle == null || worldBounds.width <= 0f) return;
            var world = battle.World;
            var player = BattleController.PlayerFaction;
            var chart = ChartRect();
            var painter = context.painter2D;

            // Fog: dark parchment where never seen, a lighter veil where explored but not watched now.
            var cellWidth = (float)(World.FogCellSize / worldBounds.width * chart.width);
            var cellHeight = (float)(World.FogCellSize / worldBounds.height * chart.height);
            for (var j = 0; j < World.FogCells; j++)
            {
                var y = World.CellCenter(j);
                if (y < worldBounds.yMin - World.FogCellSize || y > worldBounds.yMax + World.FogCellSize) continue;
                for (var i = 0; i < World.FogCells; i++)
                {
                    var x = World.CellCenter(i);
                    if (x < worldBounds.xMin - World.FogCellSize || x > worldBounds.xMax + World.FogCellSize) continue;
                    var state = world.GetFog(player, i, j);
                    if (state == FogState.Visible) continue;
                    var centre = ToChart(chart, x, y);
                    var cell = Rect.MinMaxRect(Mathf.Max(chart.xMin, centre.x - cellWidth * 0.5f), Mathf.Max(chart.yMin, centre.y - cellHeight * 0.5f),
                        Mathf.Min(chart.xMax, centre.x + cellWidth * 0.5f), Mathf.Min(chart.yMax, centre.y + cellHeight * 0.5f));
                    if (cell.width <= 0f || cell.height <= 0f) continue;
                    var color = state == FogState.Unexplored ? new Color(0.09f, 0.06f, 0.04f, 0.93f) : new Color(0.09f, 0.06f, 0.04f, 0.45f);
                    HudDraw.Gradient(context, cell, color, color);
                }
            }

            foreach (var site in world.Sites)
            {
                if (!world.IsSiteFree(site) || !world.IsExplored(player, site.X, site.Y)) continue;
                var at = ToChart(chart, site.X, site.Y);
                HudDraw.Ring(painter, at, 3.2f, 1.2f, HudPalette.Text);
            }

            foreach (var node in world.Nodes)
            {
                if ((node.KnownMask & (1 << player)) == 0 || node.Stock <= 0) continue;
                var at = ToChart(chart, node.X, node.Y);
                HudDraw.Disc(painter, at, 3.4f, HudPalette.SurfaceDeep);
                HudDraw.Disc(painter, at, 2.4f, node.Type == ResourceType.Wood ? HudPalette.Healthy : HudPalette.Silver);
            }

            foreach (var settlement in world.Settlements)
            {
                if (settlement.Destroyed) continue;
                if (settlement.Owner != player && !world.IsExplored(player, settlement.X, settlement.Y)) continue;
                var at = ToChart(chart, Rules.SettlementLandX(settlement), settlement.Y);
                var size = settlement.IsOutpost ? 3.5f : 5f;
                var rect = new Rect(at.x - size - 1f, at.y - size - 1f, size * 2f + 2f, size * 2f + 2f);
                HudDraw.Gradient(context, rect, HudPalette.SurfaceDeep, HudPalette.SurfaceDeep);
                rect = new Rect(at.x - size, at.y - size, size * 2f, size * 2f);
                var color = HudPalette.Owner(settlement.Owner, player);
                HudDraw.Gradient(context, rect, color, HudPalette.Owner(settlement.Owner, player));
            }

            foreach (var ship in world.Ships)
            {
                if (!world.CanSee(player, ship)) continue;
                var at = ToChart(chart, ship.X, ship.Y);
                var radius = Mathf.Clamp((float)ship.Definition.Radius * 0.22f, 1.6f, 3.2f);
                var own = ship.Owner == player;
                HudDraw.Disc(painter, at, radius + 1f, HudPalette.SurfaceDeep);
                if (own) HudDraw.Disc(painter, at, radius, battle.Selected.Contains(ship.Id) ? HudPalette.Text : HudPalette.Ally);
                else
                {
                    // Enemies as small diamonds, so the two owners differ by shape as well as colour.
                    painter.fillColor = HudPalette.Enemy;
                    painter.BeginPath();
                    painter.MoveTo(at + new Vector2(0f, -radius - 0.6f));
                    painter.LineTo(at + new Vector2(radius + 0.6f, 0f));
                    painter.LineTo(at + new Vector2(0f, radius + 0.6f));
                    painter.LineTo(at + new Vector2(-radius - 0.6f, 0f));
                    painter.ClosePath();
                    painter.Fill();
                }
            }

            if (Time.unscaledTime < alarmUntil)
            {
                // With reduced motion the alarm is a still double ring instead of a pulse.
                if (ReducedMotion)
                {
                    HudDraw.Ring(painter, ToChart(chart, alarmAt.x, alarmAt.y), 6f, 2f, HudPalette.Enemy);
                    HudDraw.Ring(painter, ToChart(chart, alarmAt.x, alarmAt.y), 10f, 1.5f, HudPalette.Enemy);
                }
                else
                {
                    var pulse = (alarmUntil - Time.unscaledTime) % 1f;
                    HudDraw.Ring(painter, ToChart(chart, alarmAt.x, alarmAt.y), 4f + (1f - pulse) * 12f, 2f, HudPalette.WithAlpha(HudPalette.Enemy, pulse));
                }
            }
        }

        /// <summary>The ground the camera sees: its four screen corners cast onto the sea.</summary>
        void DrawView(MeshGenerationContext context)
        {
            if (battle == null || battle.StrategicCamera == null || worldBounds.width <= 0f) return;
            var camera = battle.StrategicCamera;
            var chart = ChartRect();
            var plane = new Plane(Vector3.up, Vector3.zero);
            var corners = new[] { new Vector2(0f, 0f), new Vector2(Screen.width, 0f), new Vector2(Screen.width, Screen.height), new Vector2(0f, Screen.height) };
            var painter = context.painter2D;
            painter.strokeColor = HudPalette.Text;
            painter.lineWidth = 1.5f;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            for (var i = 0; i < corners.Length; i++)
            {
                var ray = camera.ScreenPointToRay(corners[i]);
                var point = plane.Raycast(ray, out var distance) ? ray.GetPoint(Mathf.Min(distance, 3000f)) : ray.GetPoint(3000f);
                var at = ToChart(chart, Mathf.Clamp(point.x, worldBounds.xMin - 200f, worldBounds.xMax + 200f), Mathf.Clamp(point.z, worldBounds.yMin - 200f, worldBounds.yMax + 200f));
                at.x = Mathf.Clamp(at.x, chart.xMin, chart.xMax);
                at.y = Mathf.Clamp(at.y, chart.yMin, chart.yMax);
                if (i == 0) painter.MoveTo(at); else painter.LineTo(at);
            }
            painter.ClosePath();
            painter.Stroke();
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (battle == null) return;
            var point = ToWorld(evt.localPosition);
            if (evt.button == 0)
            {
                battle.FocusCamera(point);
                dragging = true;
                this.CapturePointer(evt.pointerId);
            }
            else if (evt.button == 1) battle.OrderAt(point);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || battle == null) return;
            battle.FocusCamera(ToWorld(evt.localPosition));
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (!dragging) return;
            dragging = false;
            this.ReleasePointer(evt.pointerId);
        }
    }
}
