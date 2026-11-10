using System;
using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Staged pictures of the crews (-morzaCrewShowcase &lt;prefix&gt;, docs/art/ZALOGI.md):
    /// - every class up close under way: rowers in one stroke, the helmsman at the tillers;
    /// - a ram's charge, a bank's volley, javelins, the machines and a boarding;
    /// - a fleet from the strategic camera.
    /// Not a test: it uses the showcase's timing (game time passes, the camera rests) and the game quits after. Each scene
    /// is set in open water away from the ships before it; the log records what the crews drew for each picture.
    /// </summary>
    public sealed partial class BattleController
    {
        bool crewShowcase;

        void ReadCrewShowcase(string[] arguments)
        {
            var index = Array.IndexOf(arguments, "-morzaCrewShowcase");
            if (index < 0 || index + 1 >= arguments.Length) return;
            showcasePrefix = arguments[index + 1];
            crewShowcase = true;
        }

        void LogCrews(string picture)
        {
            if (crewDirector == null) return;
            var a = crewDirector.Averages;
            Debug.Log($"[CrewShowcase] {picture}: {crewDirector.FiguresDrawn} men on {crewDirector.ShipsDrawn} ships, LOD {crewDirector.Lod}, " +
                      $"{crewDirector.Renderer.Instances} pieces, {crewDirector.Renderer.Draws} draws, pose {crewDirector.PoseMilliseconds:0.00} ms; " +
                      $"average {crewDirector.AverageFigures:0} men, {crewDirector.AverageDraws:0} draws, decide {a.Decide:0.00}, build {a.Build:0.00}, " +
                      $"pose {a.Pose:0.00}, list {a.List:0.00}, place and submit {a.Draw:0.00}, all {a.Decide + a.Build + a.Pose + a.List + a.Draw:0.00} ms");
        }

        ShowcaseStep[] BuildCrewShowcase()
        {
            var spread = (float)World.Profile.Spread;
            var enemy = 1 - PlayerFaction;
            var steps = new List<ShowcaseStep>();
            string last = null;

            // A scene's water, its camera, and the log of the picture before it.
            Vector3 Begin(float height0, float yaw0)
            {
                if (last != null) LogCrews(last);
                var spot = FormationWater(spread);
                pivot = spot;
                height = height0;
                yaw = yaw0;
                tilt = ObliqueCamera.DefaultTilt;
                return spot;
            }

            Ship Spawn(int owner, ShipClass shipClass, Vector3 at, double heading = 0)
            {
                var ship = World.SpawnShip(owner, shipClass, at.x, at.z);
                ship.Heading = heading;
                return ship;
            }

            void Order(Ship ship, CommandKind kind, Ship target = null, Vector3 to = default) =>
                World.Issue(new Command { Faction = ship.Owner, Kind = kind, ShipIds = new[] { ship.Id }, TargetId = target?.Id ?? -1, X = to.x, Y = to.z });

            void Add(string shot, int seconds, Func<Ship> follow, Action setup)
            {
                var name = shot;
                steps.Add(new ShowcaseStep { Shot = name, Ticks = seconds * Rules.TickRate, Follow = follow, Setup = () => { setup(); last = name; } });
            }

            // Every class under way, close enough for the full detail.
            foreach (ShipClass shipClass in Enum.GetValues(typeof(ShipClass)))
            {
                Ship ship = null;
                Add($"rejs-{shipClass}", 4, () => ship, () =>
                {
                    var spot = Begin(shipClass == ShipClass.Boarder || shipClass == ShipClass.Bomber || shipClass == ShipClass.Merchant ? 34f : 26f, -35f);
                    ship = Spawn(PlayerFaction, shipClass, spot + new Vector3(-60f, 0f, 0f));
                    Order(ship, CommandKind.Move, to: spot + new Vector3(400f, 0f, 0f));
                });
            }

            // A ram's charge: the sprint, the warriors braced, the blow.
            Ship ram = null;
            Add("taran", 5, () => ram, () =>
            {
                var spot = Begin(40f, -30f);
                ram = Spawn(PlayerFaction, ShipClass.Ram, spot + new Vector3(-70f, 0f, 0f));
                var mark = Spawn(enemy, ShipClass.CrewDestroyer, spot + new Vector3(-20f, 0f, 14f), Math.PI / 2);
                mark.Hull = mark.Definition.Hull * 4;
                Order(ram, CommandKind.Ram, mark);
            });

            // A crew destroyer's four archer banks loosing at a ship that shoots back.
            Ship archers = null;
            Add("salwa", 4, () => archers, () =>
            {
                var spot = Begin(30f, -30f);
                archers = Spawn(PlayerFaction, ShipClass.CrewDestroyer, spot + new Vector3(-40f, 0f, 0f));
                var mark = Spawn(enemy, ShipClass.CrewDestroyer, spot + new Vector3(30f, 0f, 45f));
                mark.Hull = mark.Definition.Hull * 4;
                Order(archers, CommandKind.AttackShip, mark);
            });

            // The boarder's javelin banks at close range.
            Ship javelins = null;
            Add("oszczepy", 4, () => javelins, () =>
            {
                var spot = Begin(34f, -30f);
                javelins = Spawn(PlayerFaction, ShipClass.Boarder, spot + new Vector3(-40f, 0f, 0f));
                var mark = Spawn(enemy, ShipClass.Ram, spot + new Vector3(-10f, 0f, 25f));
                mark.Hull = mark.Definition.Hull * 4;
                Order(javelins, CommandKind.AttackShip, mark);
            });

            // Ballistas and heavy catapults: winding, loading, loosing, the machines turned to their mark.
            Ship killer = null;
            Add("machiny", 5, () => killer, () =>
            {
                var spot = Begin(45f, -25f);
                killer = Spawn(PlayerFaction, ShipClass.ShipKiller, spot + new Vector3(-60f, 0f, 0f));
                var bomber = Spawn(PlayerFaction, ShipClass.Bomber, spot + new Vector3(-60f, 0f, 35f));
                var mark = Spawn(enemy, ShipClass.CrewDestroyer, spot + new Vector3(50f, 0f, 20f));
                mark.Hull = mark.Definition.Hull * 6;
                Order(killer, CommandKind.AttackShip, mark);
                Order(bomber, CommandKind.AttackShip, mark);
            });

            // A boarding: grapples, the hull hauled alongside, the fronts at the rails.
            Ship boarder = null;
            Add("abordaz", 7, () => boarder, () =>
            {
                var spot = Begin(30f, -30f);
                boarder = Spawn(PlayerFaction, ShipClass.Boarder, spot + new Vector3(-20f, 0f, 0f));
                var prey = Spawn(enemy, ShipClass.Ram, spot + new Vector3(-20f, 0f, 13f));
                Order(boarder, CommandKind.Board, prey);
            });

            // A fleet from the default strategic camera: what reads of the crews at a few pixels a man.
            Ship flagship = null;
            Add("flota-z-daleka", 4, () => flagship, () =>
            {
                var spot = Begin(130f, ObliqueCamera.DefaultYaw);
                var classes = new[] { ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder, ShipClass.ShipKiller, ShipClass.Bomber, ShipClass.Merchant };
                for (var i = 0; i < classes.Length; i++)
                {
                    var ship = Spawn(PlayerFaction, classes[i], spot + new Vector3(-60f - i % 2 * 30f, 0f, (i - 2.5f) * 26f));
                    Order(ship, CommandKind.Move, to: spot + new Vector3(400f, 0f, (i - 2.5f) * 26f));
                    if (i == 2) flagship = ship;
                }
            });

            // A battle of forty warships, every class of war on both sides, at the default strategic camera: what the crews
            // cost when a fleet fights in view (LOD1); then the same battle from afar (LOD2).
            Ship centre = null;
            Add("bitwa-40", 8, () => centre, () =>
            {
                var spot = Begin(130f, ObliqueCamera.DefaultYaw);
                var warships = new[] { ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder, ShipClass.ShipKiller, ShipClass.Bomber };
                var ours = new List<Ship>();
                var theirs = new List<Ship>();
                for (var i = 0; i < 20; i++)
                {
                    var row = i / 5;
                    var column = i % 5;
                    ours.Add(Spawn(PlayerFaction, warships[i % warships.Length], spot + new Vector3(-150f + row * 22f, 0f, -60f + column * 30f)));
                    theirs.Add(Spawn(enemy, warships[(i + 2) % warships.Length], spot + new Vector3(20f + row * 22f, 0f, -60f + column * 30f), Math.PI));
                }
                for (var i = 0; i < 20; i++)
                {
                    theirs[i].Hull = theirs[i].Definition.Hull * 4;
                    ours[i].Hull = ours[i].Definition.Hull * 4;
                    Order(ours[i], CommandKind.AttackShip, theirs[i]);
                    Order(theirs[i], CommandKind.AttackShip, ours[i]);
                }
                centre = ours[12];
            });
            Add("bitwa-40-z-daleka", 4, () => centre, () =>
            {
                if (last != null) LogCrews(last);
                height = 320f;
            });

            // The last picture's numbers.
            steps.Add(new ShowcaseStep { Ticks = 1, Setup = () => { if (last != null) LogCrews(last); last = null; } });
            return steps.ToArray();
        }
    }
}
