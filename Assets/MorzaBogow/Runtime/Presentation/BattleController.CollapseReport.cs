using System;
using System.Linq;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// A measured port collapse in the build (docs/art/DESTRUCTION.md): -morzaCollapseReport file. A town of ours with every
    /// building is staged on the free shore nearest home, the camera on it; it stands, is shelled at its harbour side down to
    /// 40% of its hull, then falls. Frame times, CPU main and render thread and GPU are averaged per phase, with the worst
    /// frame, and written as JSON; then the game quits. Staged like the showcases: the settlement's hull is set directly;
    /// nothing here is a game rule.
    /// </summary>
    public sealed partial class BattleController
    {
        string collapseReport;
        float collapseClock;
        Settlement collapsePort;
        readonly string[] collapsePhases = { "standing", "shelled", "falling", "ruin" };
        readonly double[] phaseFrame = new double[4], phaseWorst = new double[4], phaseCpu = new double[4], phaseRender = new double[4], phaseGpu = new double[4];
        readonly int[] phaseFrames = new int[4], phaseTimed = new int[4];

        void ReadCollapseArguments(string[] arguments)
        {
            var index = Array.IndexOf(arguments, "-morzaCollapseReport");
            if (index >= 0 && index + 1 < arguments.Length) collapseReport = arguments[index + 1];
        }

        void UpdateCollapse()
        {
            var delta = Time.unscaledDeltaTime;
            // The start of the game (loading, shaders) is no part of it: the clock runs after 12 s.
            if (Time.realtimeSinceStartup < 12f) return;
            collapseClock += delta;
            var home = World.SettlementOf(PlayerFaction);
            if (collapsePort == null)
            {
                var site = World.Sites.Where(World.IsSiteFree).OrderBy(item => World.Distance(item.X, item.Y, home.X, home.Y)).FirstOrDefault();
                if (site == null) { Finish("no free shore"); return; }
                collapsePort = new Settlement
                {
                    Id = World.NextId++, Owner = PlayerFaction, X = site.X, Y = site.Y, IsOutpost = true, HasShipyard = true,
                    HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true,
                };
                collapsePort.Hull = collapsePort.MaxHull;
                World.Settlements.Add(collapsePort);
                site.SettlementId = collapsePort.Id;
                World.SpawnShip(PlayerFaction, ShipClass.Cutter, site.X + (site.X < 0 ? 30 : -30), site.Y - 25);
                FramePort(collapsePort, 70f);
                return;
            }
            FramePort(collapsePort, 70f);
            // Phases: standing 2-6 s, shelled from 6 s (measured 6-12), falling from 12 s (measured 12-16), the ruin 16-20.
            if (collapseClock >= 6f && collapseClock - delta < 6f && portBuildings.TryGetValue(collapsePort.Id, out var shelled))
            {
                var box = shelled.Footprint(PortBuilding.Store);
                for (var i = 0; i < 14; i++)
                {
                    var at = shelled.Root.TransformPoint(new Vector3(box.center.x - (i % 4) * 1.6f, 2f, -7.5f + i * 1.15f));
                    shelled.Struck(collapsePort, at, at + new Vector3(90f, 0f, (i % 3 - 1) * 20f), WeaponType.HeavyCatapult, World.Tick + i, true);
                }
                collapsePort.Hull = collapsePort.MaxHull * 0.4;
            }
            if (collapseClock >= 12f && collapseClock - delta < 12f) collapsePort.Hull = 0;
            var phase = collapseClock < 2f ? -1 : collapseClock < 6f ? 0 : collapseClock < 12f ? 1 : collapseClock < 16f ? 2 : collapseClock < 20f ? 3 : 4;
            if (phase == 4) { Finish(null); return; }
            if (phase < 0) return;
            phaseFrame[phase] += delta * 1000.0;
            phaseWorst[phase] = Math.Max(phaseWorst[phase], delta * 1000.0);
            phaseFrames[phase]++;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, frameTiming) > 0)
            {
                phaseTimed[phase]++;
                phaseCpu[phase] += frameTiming[0].cpuMainThreadFrameTime;
                phaseRender[phase] += frameTiming[0].cpuRenderThreadFrameTime;
                phaseGpu[phase] += frameTiming[0].gpuFrameTime;
            }
        }

        void Finish(string error)
        {
            var lines = new System.Collections.Generic.List<string>();
            for (var i = 0; i < 4; i++)
            {
                var n = Math.Max(1, phaseFrames[i]);
                var t = Math.Max(1, phaseTimed[i]);
                lines.Add(FormattableString.Invariant(
                    $"  \"{collapsePhases[i]}\": {{ \"frames\": {phaseFrames[i]}, \"frameMs\": {phaseFrame[i] / n:0.00}, \"worstMs\": {phaseWorst[i]:0.0}, \"cpuMainMs\": {phaseCpu[i] / t:0.00}, \"cpuRenderMs\": {phaseRender[i] / t:0.00}, \"gpuMs\": {phaseGpu[i] / t:0.00} }}"));
            }
            var buildings = collapsePort != null && portBuildings.TryGetValue(collapsePort.Id, out var b) ? b : null;
            var views = buildings == null ? 0 : Enum.GetValues(typeof(PortBuilding)).Cast<PortBuilding>().Sum(kind => buildings.DamagesOf(kind).Count);
            var json = "{\n" + string.Join(",\n", lines) +
                       FormattableString.Invariant($",\n  \"views\": {views}, \"resolution\": \"{Screen.width}x{Screen.height}\", \"quality\": \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\", \"error\": \"{error ?? ""}\"\n}}\n");
            System.IO.File.WriteAllText(collapseReport, json);
            Debug.Log("[Collapse] " + json);
            collapseReport = null;
            Application.Quit();
        }
    }
}
