using System;
using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Weapons in posts and the shots they throw (docs/UZBROJENIE.md, after the unit catalogue of Morza Bogów):
    /// each post needs its crew, fills its cycle while manned, and when ready looses a projectile at a target in its
    /// range and arc. The shot's landing point is drawn once from the seeded generator at launch (aim with lead, then
    /// scatter); it flies for real and hits whatever lies there when it lands, never a second roll.
    /// </summary>
    public sealed partial class World
    {
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public int NextProjectileId = 1;

        /// <summary>A settlement's buildings are this far around its port point; a shot landing inside hits them.</summary>
        public const double SettlementHitRadius = 22;

        /// <summary>Fits a hull with weapons, every module whole and every post loaded; the last item value is the ram's.</summary>
        public static void Fit(Ship ship, WeaponType[] loadout, (long wood, long silver)[] values)
        {
            var count = ship.Definition.Slots.Length;
            ship.Mounts = new WeaponType[count];
            ship.Progress = new double[count];
            ship.ModuleHp = new double[count];
            ship.ItemWood = new long[count + 1];
            ship.ItemSilver = new long[count + 1];
            for (var i = 0; i < count; i++)
            {
                ship.Mounts[i] = loadout.Length > i ? loadout[i] : WeaponType.None;
                ship.Progress[i] = 1;
                ship.ModuleHp[i] = Rules.ModuleHitPoints;
            }
            for (var i = 0; i < values.Length && i <= count; i++)
            {
                ship.ItemWood[i] = values[i].wood;
                ship.ItemSilver[i] = values[i].silver;
            }
            ship.Loadout = (WeaponType[])ship.Mounts.Clone();
        }

        /// <summary>
        /// Which posts have their crew, in a fixed order: first the banks, each from four men of its own trade; then
        /// the machines, from swordsmen, then from javelin men and archers left over. Sailors stay at the oars. A post
        /// short of its crew neither shoots nor loads.
        /// </summary>
        public static bool[] Manned(Ship ship)
        {
            var manned = new bool[ship.Mounts.Length];
            var archers = ship.Archers;
            var javelins = ship.Javelins;
            var swordsmen = ship.Swordsmen;
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                var weapon = WeaponCatalog.Get(ship.Mounts[i]);
                if (weapon.CrewKind == WeaponCrew.Archers && ship.Mounts[i] != WeaponType.None && archers >= weapon.Crew) { archers -= weapon.Crew; manned[i] = true; }
                else if (weapon.CrewKind == WeaponCrew.Javelins && javelins >= weapon.Crew) { javelins -= weapon.Crew; manned[i] = true; }
            }
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                if (ship.Mounts[i] == WeaponType.None) continue;
                var weapon = WeaponCatalog.Get(ship.Mounts[i]);
                if (!weapon.IsMachine || swordsmen + javelins + archers < weapon.Crew) continue;
                var need = weapon.Crew;
                var take = Math.Min(swordsmen, need); swordsmen -= take; need -= take;
                take = Math.Min(javelins, need); javelins -= take; need -= take;
                archers -= need;
                manned[i] = true;
            }
            return manned;
        }

        /// <summary>A ship that has any weapon fitted can be ordered to shoot.</summary>
        public static bool HasWeapons(Ship ship) => ship.Mounts.Any(mount => mount != WeaponType.None);

        /// <summary>
        /// The distance a ship ordered to shoot holds from its target (A13, Q04), from the posts that can fire now: a
        /// weapon fitted, its module whole and its crew aboard. Of the distances at Rules.EngageRangeShare of each such
        /// post's greatest range, the one from which the most of them reach (outside their least range too), the
        /// farther on a tie. Null when no post can fire: the ship then does not close in.
        /// </summary>
        public static double? EngageRange(Ship ship)
        {
            var manned = Manned(ship);
            bool Ready(int i) => ship.Mounts[i] != WeaponType.None && ship.ModuleHp[i] > 0 && manned[i];
            double? best = null;
            var bestCount = 0;
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                if (!Ready(i)) continue;
                var candidate = WeaponCatalog.Get(ship.Mounts[i]).MaxRange * Rules.EngageRangeShare;
                var count = 0;
                for (var j = 0; j < ship.Mounts.Length; j++)
                {
                    if (!Ready(j)) continue;
                    var weapon = WeaponCatalog.Get(ship.Mounts[j]);
                    if (candidate >= weapon.MinRange && candidate <= weapon.MaxRange) count++;
                }
                if (count > bestCount || (count > 0 && count == bestCount && candidate > best.Value)) { best = candidate; bestCount = count; }
            }
            return best;
        }

        /// <summary>
        /// The distance actually held: EngageRange, but never beyond Rules.EngageRangeShare of the ship's own sight, so a
        /// ship alone does not lose a target that makes off while it lies still at the edge of what it sees (A13).
        /// </summary>
        public static double? HoldRange(Ship ship)
        {
            var range = EngageRange(ship);
            return range == null ? (double?)null : Math.Min(range.Value, ship.Definition.Vision * Rules.EngageRangeShare);
        }

        /// <summary>
        /// Why a ship ordered to shoot cannot (J02): nothing fitted; weapons whole but too few men for any post; or every
        /// fitted weapon out of action. None when at least one post can fire.
        /// </summary>
        public static CommandReason ShootingProblem(Ship ship)
        {
            if (!HasWeapons(ship)) return CommandReason.NotCapable;
            if (EngageRange(ship) != null) return CommandReason.None;
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                if (ship.Mounts[i] != WeaponType.None && ship.ModuleHp[i] > 0) return CommandReason.NoCrew;
            }
            return CommandReason.WeaponsBroken;
        }

        /// <summary>What keeps every post that can fire from shooting at a point from where the ship lies and heads.</summary>
        public enum FireBlock { None, Range, Arc, Land }

        /// <summary>
        /// Whether a post that can fire reaches the point now (None), and if none does, why: the bow points the wrong way
        /// for those in range (Arc); land stands in the line of the straight shots in range (Land, only where a line of
        /// fire is needed); or the point is nearer than their least range (Range). The load is not asked: a post
        /// reloading counts.
        /// </summary>
        public FireBlock FireBlockAt(Ship ship, double x, double y, bool lineOfFire)
        {
            var manned = Manned(ship);
            var distance = Distance(ship.X, ship.Y, x, y);
            var hidden = lineOfFire && HasIslandBetween(ship.X, ship.Y, x, y);
            var arc = false;
            var land = false;
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                if (ship.Mounts[i] == WeaponType.None || ship.ModuleHp[i] <= 0 || !manned[i]) continue;
                var weapon = WeaponCatalog.Get(ship.Mounts[i]);
                if (distance < weapon.MinRange || distance > weapon.MaxRange) continue;
                if (hidden && !weapon.Lobbed) { land = true; continue; }
                if (InReach(ship, weapon, x, y)) return FireBlock.None;
                arc = true;
            }
            return arc ? FireBlock.Arc : land ? FireBlock.Land : FireBlock.Range;
        }

        /// <summary>The least range among the posts that can fire; a ship closing in for a line of fire comes no nearer.</summary>
        static double LeastRange(Ship ship)
        {
            var manned = Manned(ship);
            var least = double.MaxValue;
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                if (ship.Mounts[i] == WeaponType.None || ship.ModuleHp[i] <= 0 || !manned[i]) continue;
                least = Math.Min(least, WeaponCatalog.Get(ship.Mounts[i]).MinRange);
            }
            return least == double.MaxValue ? 0 : least;
        }

        /// <summary>A point lies within a weapon's reach and arc from this ship.</summary>
        public static bool InReach(Ship ship, WeaponDefinition weapon, double x, double y)
        {
            var distance = Distance(ship.X, ship.Y, x, y);
            if (distance > weapon.MaxRange || distance < weapon.MinRange) return false;
            if (weapon.ArcDegrees >= 360) return true;
            var off = Math.Abs(NormalizeAngle(Math.Atan2(y - ship.Y, x - ship.X) - ship.Heading));
            return off <= weapon.ArcDegrees * Math.PI / 360.0;
        }

        void StepWeapons()
        {
            foreach (var ship in Ships.OrderBy(item => item.Id))
            {
                if (ship.Mounts.Length == 0) continue;
                // Grappled, the men fight on the decks; posts keep their load but do not shoot.
                var locked = Boardings.Any(link => link.Hooked && (link.AttackerId == ship.Id || link.TargetId == ship.Id));
                var manned = Manned(ship);
                var tempo = 0.75 + 0.25 * ship.Morale / 100.0;
                for (var i = 0; i < ship.Mounts.Length; i++)
                {
                    if (ship.Mounts[i] == WeaponType.None || ship.ModuleHp[i] <= 0 || !manned[i]) continue;
                    var weapon = WeaponCatalog.Get(ship.Mounts[i]);
                    if (ship.Progress[i] < 1)
                    {
                        ship.Progress[i] = Math.Min(1, ship.Progress[i] + Rules.Dt * tempo / weapon.Cycle);
                        continue;
                    }
                    if (locked) continue;

                    Settlement settlement = null;
                    Ship target = null;
                    if (ship.Order == OrderKind.AttackSettlement)
                    {
                        settlement = Settlements.FirstOrDefault(item => item.Id == ship.TargetId);
                        if (settlement != null && (settlement.Destroyed || !InReach(ship, weapon, settlement.X, settlement.Y))) settlement = null;
                    }
                    else if (ship.Order == OrderKind.AttackShip)
                    {
                        var ordered = FindShip(ship.TargetId);
                        if (ordered != null && CanShootAt(ship, weapon, ordered)) target = ordered;
                    }
                    if (target == null && settlement == null && ship.Order != OrderKind.Ram) target = NearestTarget(ship, weapon);
                    if (target == null && settlement == null) continue;

                    Fire(ship, i, weapon, target, settlement);
                    ship.Progress[i] = 0;
                }
            }
        }

        bool CanShootAt(Ship ship, WeaponDefinition weapon, Ship target) =>
            target.Owner != ship.Owner && CanSee(ship.Owner, target) && InReach(ship, weapon, target.X, target.Y)
            && (weapon.Lobbed || !HasIslandBetween(ship.X, ship.Y, target.X, target.Y));

        Ship NearestTarget(Ship ship, WeaponDefinition weapon)
        {
            Ship best = null;
            var bestDistance = double.MaxValue;
            foreach (var other in Ships)
            {
                if (other.Owner == ship.Owner) continue;
                var distance = Distance(ship.X, ship.Y, other.X, other.Y);
                if (distance >= bestDistance || !CanShootAt(ship, weapon, other)) continue;
                bestDistance = distance;
                best = other;
            }
            return best;
        }

        /// <summary>
        /// Launches one shot: aimed where the target will be when it lands, scattered within a radius that grows with
        /// range and the shooter's own speed (catalogue rule), drawn uniformly in the disc from the seeded generator.
        /// </summary>
        void Fire(Ship ship, int slot, WeaponDefinition weapon, Ship target, Settlement settlement)
        {
            double aimX, aimY;
            var distance = target != null ? Distance(ship.X, ship.Y, target.X, target.Y) : Distance(ship.X, ship.Y, settlement.X, settlement.Y);
            var flight = weapon.FlightSeconds(distance);
            if (target != null)
            {
                aimX = target.X + Math.Cos(target.Heading) * target.Speed * flight;
                aimY = target.Y + Math.Sin(target.Heading) * target.Speed * flight;
                if (WindMoves)
                {
                    VelocityOf(target, out var leadX, out var leadY);
                    aimX = target.X + leadX * flight;
                    aimY = target.Y + leadY * flight;
                }
            }
            else
            {
                aimX = settlement.X;
                aimY = settlement.Y;
            }
            var ownSpeed = Math.Min(1.0, Math.Abs(ship.Speed) / Math.Max(0.1, ship.Definition.Speed));
            var radius = Math.Max(0.5, weapon.Dispersion * distance / weapon.MaxRange) * (1 + 0.25 * ownSpeed);
            var reach = radius * Math.Sqrt(Random.NextDouble());
            var angle = Random.NextDouble() * Math.PI * 2;
            aimX += Math.Cos(angle) * reach;
            aimY += Math.Sin(angle) * reach;

            var projectile = new Projectile
            {
                Id = NextProjectileId++, OwnerId = ship.Owner, ShooterId = ship.Id, Weapon = weapon.Type,
                TargetShipId = target?.Id ?? -1, TargetSettlementId = settlement?.Id ?? -1,
                FromX = ship.X, FromY = ship.Y, ToX = aimX, ToY = aimY,
                LaunchTick = Tick, ArriveTick = Tick + Math.Max(1, (int)Math.Ceiling(weapon.FlightSeconds(Distance(ship.X, ship.Y, aimX, aimY)) * Rules.TickRate - 1e-9)),
            };
            Projectiles.Add(projectile);
            var targetId = target?.Id ?? settlement.Id;
            Emit("weapon_fired", ship.Id, targetId, aimX, aimY, (int)weapon.Type * 100 + slot);
            // The banks' arrows and javelins keep the volley signal the effects and the herald listen for.
            if (!weapon.IsMachine) Emit("volley", ship.Id, targetId, target?.X ?? settlement.X, target?.Y ?? settlement.Y, weapon.Crew);
        }

        /// <summary>
        /// Shots that have finished their flight land: a straight shot that met land on its way strikes the shore; one
        /// landing on land is spent; otherwise every enemy hull under the point is hit (and, for a splash weapon, every
        /// enemy hull within its radius), each once; a settlement under it takes the hull packet times its multiplier.
        /// </summary>
        void StepProjectiles()
        {
            if (Projectiles.Count == 0) return;
            foreach (var shot in Projectiles.Where(item => item.ArriveTick <= Tick).OrderBy(item => item.Id).ToList())
            {
                Projectiles.Remove(shot);
                var weapon = WeaponCatalog.Get(shot.Weapon);
                if (!weapon.Lobbed && HasIslandBetween(shot.FromX, shot.FromY, shot.ToX, shot.ToY))
                {
                    Emit("projectile_land", shot.Id, shot.ShooterId, shot.ToX, shot.ToY, (int)shot.Weapon);
                    continue;
                }
                if (OnLand(shot.ToX, shot.ToY))
                {
                    Emit("projectile_land", shot.Id, shot.ShooterId, shot.ToX, shot.ToY, (int)shot.Weapon);
                    continue;
                }

                var struck = false;
                foreach (var ship in Ships.Where(item => item.Owner != shot.OwnerId && item.Hull > 0).OrderBy(item => item.Id))
                {
                    var gap = FootprintGap(ship, shot.ToX, shot.ToY);
                    if (gap > 0 && gap > weapon.Splash) continue;
                    ApplyProjectile(ship, weapon);
                    Emit("projectile_hit", ship.Id, shot.ShooterId, shot.ToX, shot.ToY, (int)shot.Weapon);
                    struck = true;
                }
                foreach (var settlement in Settlements.Where(item => item.Owner != shot.OwnerId && !item.Destroyed).OrderBy(item => item.Id))
                {
                    if (Distance(settlement.X, settlement.Y, shot.ToX, shot.ToY) > SettlementHitRadius + weapon.Splash) continue;
                    settlement.Hull -= weapon.Hull * weapon.BuildingMultiplier;
                    Emit("projectile_hit_settlement", settlement.Id, shot.ShooterId, shot.ToX, shot.ToY, (int)shot.Weapon);
                    struck = true;
                    if (settlement.Destroyed) FallSettlement(settlement, shot.ShooterId);
                }
                if (!struck) Emit("projectile_miss", shot.Id, shot.ShooterId, shot.ToX, shot.ToY, (int)shot.Weapon);
            }
        }

        public bool OnLand(double x, double y)
        {
            foreach (var island in Islands)
            {
                if (Distance(island.X, island.Y, x, y) > island.Radius) continue;
                if (LandShape.SignedDistance(island, x, y) < 0) return true;
            }
            return false;
        }

        /// <summary>How far a point lies outside a hull's rectangle (length by beam, along its heading); zero or less inside.</summary>
        public static double FootprintGap(Ship ship, double x, double y)
        {
            var dx = x - ship.X;
            var dy = y - ship.Y;
            var along = dx * Math.Cos(ship.Heading) + dy * Math.Sin(ship.Heading);
            var across = -dx * Math.Sin(ship.Heading) + dy * Math.Cos(ship.Heading);
            var outAlong = Math.Abs(along) - ship.Definition.Radius;
            var outAcross = Math.Abs(across) - ship.Definition.Beam / 2;
            if (outAlong <= 0 && outAcross <= 0) return Math.Max(outAlong, outAcross);
            return Math.Sqrt(Math.Max(0, outAlong) * Math.Max(0, outAlong) + Math.Max(0, outAcross) * Math.Max(0, outAcross));
        }

        /// <summary>
        /// One packet: hull damage less the projectile armour (never more than 70% off), the men's share as crew damage,
        /// and the module share to one post drawn from the seeded generator among those fitted.
        /// </summary>
        void ApplyProjectile(Ship ship, WeaponDefinition weapon)
        {
            var armour = Math.Min(Rules.MaxHullReduction, ship.Definition.ProjectileArmor);
            ApplyHullDamage(ship, weapon.Hull * (1 - armour));
            ApplyCrewDamage(ship, weapon.Men);
            var fitted = 0;
            for (var i = 0; i < ship.Mounts.Length; i++) if (ship.Mounts[i] != WeaponType.None) fitted++;
            if (fitted == 0 || weapon.Module <= 0) return;
            var pick = (int)(Random.NextDouble() * fitted);
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                if (ship.Mounts[i] == WeaponType.None) continue;
                if (pick-- > 0) continue;
                ship.ModuleHp[i] = Math.Max(0, ship.ModuleHp[i] - weapon.Module);
                break;
            }
        }
    }
}
