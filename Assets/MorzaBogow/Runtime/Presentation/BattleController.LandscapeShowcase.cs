using System;
using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Staged pictures of the environment (-morzaLandscapeShowcase &lt;prefix&gt;, docs/art/EnvironmentProfiles.md): the
    /// olives of the home port up close and at the default zoom, the cliff with its notch and the gentle shore with its
    /// beachrock, the wild olives of the silver islet and a far view. Not a test: it uses the showcase's timing and the
    /// game quits after. The log records the trees, their triangles and the time their wind takes for each picture.
    /// </summary>
    public sealed partial class BattleController
    {
        bool landscapeShowcase;

        void ReadLandscapeShowcase(string[] arguments)
        {
            var index = Array.IndexOf(arguments, "-morzaLandscapeShowcase");
            if (index < 0 || index + 1 >= arguments.Length) return;
            showcasePrefix = arguments[index + 1];
            landscapeShowcase = true;
        }

        static void LogTrees(string picture) =>
            Debug.Log($"[LandscapeShowcase] {picture}: {Landscape.TreeGrove.TreesActive} trees, triangles LOD0/1/2 {Landscape.TreeGrove.Triangles(0)}/{Landscape.TreeGrove.Triangles(1)}/{Landscape.TreeGrove.Triangles(2)}, " +
                      $"wind and bones {Landscape.TreeGrove.FrameMilliseconds:0.000} ms/frame");

        ShowcaseStep[] BuildLandscapeShowcase()
        {
            var home = World.SettlementOf(PlayerFaction);
            var land = World.Islands.Where(item => item.Kind == IslandKind.Settlement)
                .OrderBy(item => World.Distance(item.X, item.Y, Rules.SettlementLandX(home), home.Y)).First();
            var centre = new Vector3((float)land.X, 0f, (float)land.Y);
            // The port lies toward the settlement point; the cliffs face the open sea on the other side.
            var portSide = new Vector3((float)(home.X - land.X), 0f, (float)(home.Y - land.Y));
            portSide = portSide.sqrMagnitude > 1e-3f ? portSide.normalized : Vector3.right;
            var radius = (float)land.Radius;
            var grove = UnityEngine.Object.FindObjectsByType<Landscape.TreeGrove>(FindObjectsSortMode.None)
                .OrderBy(item => (item.transform.position - centre).sqrMagnitude).FirstOrDefault();
            var olives = grove != null && grove.Renderers.Count > 0 ? grove.Renderers[0].bounds.center : centre;
            olives.y = 0f;
            string last = null;
            var steps = new List<ShowcaseStep>();

            void Add(string shot, int seconds, Vector3 at, float zoom, float view, float angle)
            {
                steps.Add(new ShowcaseStep
                {
                    Shot = shot,
                    Ticks = seconds * Rules.TickRate,
                    Setup = () =>
                    {
                        if (last != null) LogTrees(last);
                        pivot = at;
                        height = zoom;
                        yaw = view;
                        tilt = angle;
                        last = shot;
                    },
                });
            }

            float Facing(Vector3 from) => Mathf.Atan2(-from.x, -from.z) * Mathf.Rad2Deg;

            Add("oliwki-blisko", 3, olives, 24f, ObliqueCamera.DefaultYaw, 35f);
            Add("oliwki-domyslna", 3, olives, 130f, ObliqueCamera.DefaultYaw, ObliqueCamera.DefaultTilt);
            var cliff = centre - portSide * radius * 0.98f;
            Add("klif-notch", 3, cliff, 26f, Facing(-portSide), 28f);
            var gentle = centre + Quaternion.Euler(0f, 35f, 0f) * portSide * radius * 0.98f;
            Add("brzeg-beachrock", 3, gentle, 24f, Facing(Quaternion.Euler(0f, 35f, 0f) * portSide), 32f);
            var silver = World.Nodes.Where(item => item.Type == ResourceType.Silver).OrderBy(item => World.Distance(item.X, item.Y, home.X, home.Y)).FirstOrDefault();
            if (silver != null) Add("srebro-zbocza", 3, new Vector3((float)silver.X, 0f, (float)(silver.Y + Rules.SiteLandOffset * 0.6)), 34f, ObliqueCamera.DefaultYaw, 38f);
            // The home port's slipways and the shipyard's gear beside them.
            Add("stocznia", 3, new Vector3((float)(home.X + Rules.SettlementLandX(home)) * 0.5f, 0f, (float)home.Y + 6f), 34f, ObliqueCamera.DefaultYaw, 40f);
            Add("archipelag", 3, centre, 300f, ObliqueCamera.DefaultYaw, ObliqueCamera.DefaultTilt);
            steps.Add(new ShowcaseStep { Ticks = 1, Setup = () => { if (last != null) LogTrees(last); last = null; } });
            return steps.ToArray();
        }
    }
}
