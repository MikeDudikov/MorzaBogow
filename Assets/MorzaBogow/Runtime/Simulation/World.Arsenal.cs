using System;
using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// A port's buildings and research, its workshop (arsenal) line, refitting ships to their planned weapons, and fleet
    /// blueprints (docs/UZBROJENIE.md). All paid from the port's local store like ship orders: reserved in full, used
    /// with progress, cancelled with the same settlement (QuoteOrder).
    /// </summary>
    public sealed partial class World
    {
        /// <summary>Heavy weapons researched, one bit per side.</summary>
        public int HeavyArmsResearched;
        public readonly List<Blueprint> Blueprints = new List<Blueprint>();
        public int NextBlueprintId = 1;

        public bool HasHeavyArms(int faction) => (HeavyArmsResearched & (1 << faction)) != 0;

        // ---------- Projects ----------

        public static (long wood, long silver, double seconds) ProjectCost(ProjectKind kind)
        {
            switch (kind)
            {
                case ProjectKind.Workshop: return (Rules.WorkshopWood, Rules.WorkshopSilver, Rules.WorkshopSeconds);
                case ProjectKind.Town: return (Rules.TownWood, Rules.TownSilver, Rules.TownSeconds);
                case ProjectKind.SageSchool: return (Rules.SchoolWood, Rules.SchoolSilver, Rules.SchoolSeconds);
                case ProjectKind.HeavyArmsResearch: return (Rules.HeavyArmsWood, Rules.HeavyArmsSilver, Rules.HeavyArmsSeconds);
                case ProjectKind.Dock: return (Rules.DockWood, Rules.DockSilver, Rules.DockSeconds);
                default: return (0, 0, 0);
            }
        }

        /// <summary>
        /// Why a port cannot start a project now, or null if it can. The chain: workshop at a port with a shipyard;
        /// the home port with its workshop grows into a town; a town raises the Sages' School and upgrades its shipyard
        /// into a dock; the school and a workshop research heavy weapons for the whole side. One project at a time; the
        /// price must be in the local store.
        /// </summary>
        public string ProjectProblem(Settlement port, ProjectKind kind) => ProjectCheck(port, kind).text;

        /// <summary>ProjectProblem's checks with the reason a refused order reports (J02); one rule for both.</summary>
        (CommandReason reason, string text) ProjectCheck(Settlement port, ProjectKind kind)
        {
            if (port == null || port.Destroyed) return (CommandReason.InvalidTarget, "Port nie istnieje.");
            if (port.Project != null) return (CommandReason.NoSpace, "Port już coś buduje.");
            switch (kind)
            {
                case ProjectKind.Workshop:
                    if (port.HasWorkshop) return (CommandReason.NothingToDo, "Warsztat już stoi.");
                    if (!port.HasShipyard) return (CommandReason.NotCapable, "Wymaga stoczni.");
                    break;
                case ProjectKind.Town:
                    if (port.IsTown) return (CommandReason.NothingToDo, "To już Miasto.");
                    if (port.IsOutpost) return (CommandReason.NotCapable, "Miastem może stać się tylko port macierzysty.");
                    if (!port.HasShipyard || !port.HasWorkshop) return (CommandReason.NotCapable, "Wymaga stoczni i Warsztatu.");
                    break;
                case ProjectKind.SageSchool:
                    if (port.HasSageSchool) return (CommandReason.NothingToDo, "Szkoła już stoi.");
                    if (!port.IsTown) return (CommandReason.NotCapable, "Wymaga Miasta.");
                    break;
                case ProjectKind.HeavyArmsResearch:
                    if (HasHeavyArms(port.Owner)) return (CommandReason.NothingToDo, "Już zbadane.");
                    if (!port.HasSageSchool || !port.HasWorkshop) return (CommandReason.NotCapable, "Wymaga Szkoły Mędrców i Warsztatu.");
                    break;
                case ProjectKind.Dock:
                    if (port.HasDock) return (CommandReason.NothingToDo, "Dok już stoi.");
                    if (!port.IsTown || !port.HasShipyard) return (CommandReason.NotCapable, "Wymaga Miasta ze stocznią.");
                    break;
                default: return (CommandReason.InvalidOrder, "Nieznany projekt.");
            }
            var (wood, silver, _) = ProjectCost(kind);
            if (port.Available(ResourceType.Wood) < wood || port.Available(ResourceType.Silver) < silver) return (CommandReason.NoFunds, "Brak środków w magazynie portu.");
            return (CommandReason.None, null);
        }

        CommandReason BeginProject(int faction, int settlementId, ProjectKind kind)
        {
            var port = FindSettlement(settlementId);
            if (port == null || port.Owner != faction) return CommandReason.InvalidTarget;
            var check = ProjectCheck(port, kind).reason;
            if (check != CommandReason.None) return check;
            var (wood, silver, seconds) = ProjectCost(kind);
            port.ReservedWood += wood;
            port.ReservedSilver += silver;
            port.Project = new BuildOrder { PaidWood = wood, PaidSilver = silver, TotalTicks = (int)(seconds * Rules.TickRate) };
            port.ProjectKind = kind;
            return CommandReason.None;
        }

        CommandReason CancelProject(int faction, int settlementId)
        {
            var port = FindSettlement(settlementId);
            if (port == null || port.Owner != faction) return CommandReason.InvalidTarget;
            if (port.Project == null) return CommandReason.NothingToDo;
            Settle(port, QuoteOrder(port.Project));
            port.Project = null;
            port.ProjectKind = ProjectKind.None;
            return CommandReason.None;
        }

        /// <summary>The cancel rule of every order (E04): unused reservation released; after the start 80% of it comes back.</summary>
        public static CancelQuote QuoteOrder(BuildOrder order)
        {
            var unusedWood = order.PaidWood - order.UsedWood;
            var unusedSilver = order.PaidSilver - order.UsedSilver;
            var quote = new CancelQuote
            {
                Valid = true, Started = order.Started,
                Progress = order.TotalTicks > 0 ? (double)order.DoneTicks / order.TotalTicks : 0,
                ReleasedWood = unusedWood, ReleasedSilver = unusedSilver,
            };
            if (order.Started)
            {
                quote.LostWood = unusedWood - (long)(unusedWood * Rules.RefundAfterStart);
                quote.LostSilver = unusedSilver - (long)(unusedSilver * Rules.RefundAfterStart);
            }
            return quote;
        }

        void Settle(Settlement port, CancelQuote quote)
        {
            port.ReservedWood -= quote.ReleasedWood;
            port.ReservedSilver -= quote.ReleasedSilver;
            port.Wood -= quote.LostWood;
            port.Silver -= quote.LostSilver;
            LostWood += quote.LostWood;
            LostSilver += quote.LostSilver;
        }

        void StepProjects(Settlement port)
        {
            if (port.Project != null && AdvanceOrder(port, port.Project))
            {
                var kind = port.ProjectKind;
                port.Project = null;
                port.ProjectKind = ProjectKind.None;
                switch (kind)
                {
                    case ProjectKind.Workshop: port.HasWorkshop = true; break;
                    case ProjectKind.Town:
                        port.IsTown = true;
                        var before = port.MaxHull;
                        port.Walls *= Rules.TownWalls;
                        port.Hull += port.MaxHull - before;
                        break;
                    case ProjectKind.SageSchool: port.HasSageSchool = true; break;
                    case ProjectKind.HeavyArmsResearch: HeavyArmsResearched |= 1 << port.Owner; break;
                    case ProjectKind.Dock: port.HasDock = true; break;
                }
                Emit("project_done", port.Id, port.Owner, port.X, port.Y, (int)kind);
            }

            // The workshop line: one weapon at a time into the port's arsenal.
            if (!port.HasWorkshop || port.WorkshopQueue.Count == 0) return;
            var order = port.WorkshopQueue[0];
            if (!AdvanceOrder(port, order)) return;
            port.WorkshopQueue.RemoveAt(0);
            port.Arsenal[(int)order.Weapon]++;
            Emit("weapon_made", port.Id, port.Owner, port.X, port.Y, (int)order.Weapon);
        }

        // ---------- Workshop ----------

        /// <summary>Why a port cannot order this weapon, or null if it can: workshop, a machine, heavy ones after research, the price.</summary>
        public string WeaponProblem(Settlement port, WeaponType weapon) => WeaponCheck(port, weapon).text;

        /// <summary>WeaponProblem's checks with the reason a refused order reports (J02); one rule for both.</summary>
        (CommandReason reason, string text) WeaponCheck(Settlement port, WeaponType weapon)
        {
            if (port == null || port.Destroyed) return (CommandReason.InvalidTarget, "Port nie istnieje.");
            if (!port.HasWorkshop) return (CommandReason.NotCapable, "Wymaga Warsztatu w tym porcie.");
            var definition = WeaponCatalog.Get(weapon);
            if (!definition.IsMachine || definition.CostWood + definition.CostSilver == 0) return (CommandReason.InvalidOrder, "Tego Warsztat nie wytwarza.");
            if (definition.Slot == SlotSize.Heavy && !HasHeavyArms(port.Owner)) return (CommandReason.NotCapable, "Broń ciężka wymaga badania w Szkole Mędrców.");
            if (port.Available(ResourceType.Wood) < definition.CostWood * Rules.Centi || port.Available(ResourceType.Silver) < definition.CostSilver * Rules.Centi) return (CommandReason.NoFunds, "Brak środków w magazynie portu.");
            return (CommandReason.None, null);
        }

        CommandReason QueueWeapon(int faction, int settlementId, WeaponType weapon)
        {
            var port = FindSettlement(settlementId);
            if (port == null || port.Owner != faction) return CommandReason.InvalidTarget;
            var check = WeaponCheck(port, weapon).reason;
            if (check != CommandReason.None) return check;
            var definition = WeaponCatalog.Get(weapon);
            var wood = definition.CostWood * Rules.Centi;
            var silver = definition.CostSilver * Rules.Centi;
            port.ReservedWood += wood;
            port.ReservedSilver += silver;
            port.WorkshopQueue.Add(new BuildOrder { Weapon = weapon, PaidWood = wood, PaidSilver = silver, TotalTicks = (int)(definition.WorkshopSeconds * Rules.TickRate) });
            return CommandReason.None;
        }

        CommandReason CancelWeapon(int faction, int settlementId, int index)
        {
            var port = FindSettlement(settlementId);
            if (port == null || port.Owner != faction) return CommandReason.InvalidTarget;
            if (index < 0 || index >= port.WorkshopQueue.Count) return CommandReason.InvalidOrder;
            Settle(port, QuoteOrder(port.WorkshopQueue[index]));
            port.WorkshopQueue.RemoveAt(index);
            return CommandReason.None;
        }

        /// <summary>The port orders one of a weapon unless one is already lying in its arsenal or on its workshop line.</summary>
        void OrderIfMissing(Settlement port, WeaponType weapon)
        {
            if (port == null || weapon == WeaponType.None || !port.HasWorkshop) return;
            if (port.Arsenal[(int)weapon] > 0 || port.WorkshopQueue.Any(order => order.Weapon == weapon)) return;
            QueueWeapon(port.Owner, port.Id, weapon);
        }

        // ---------- Refitting ----------

        /// <summary>A loadout fits the hull's posts; heavy machines only after the side's research.</summary>
        public bool LoadoutValid(int faction, ShipClass shipClass, WeaponType[] loadout)
        {
            var slots = ShipCatalog.Get(shipClass).Slots;
            if (loadout == null || loadout.Length != slots.Length) return false;
            for (var i = 0; i < slots.Length; i++)
            {
                if (!WeaponCatalog.Fits(loadout[i], slots[i])) return false;
                var weapon = WeaponCatalog.Get(loadout[i]);
                if (weapon.Slot == SlotSize.Heavy && loadout[i] != WeaponType.None && !HasHeavyArms(faction)) return false;
            }
            return true;
        }

        bool SetLoadout(Ship ship, WeaponType[] loadout)
        {
            if (!LoadoutValid(ship.Owner, ship.Class, loadout)) return false;
            ship.Loadout = (WeaponType[])loadout.Clone();
            var port = ClosestYard(ship);
            for (var i = 0; i < loadout.Length; i++) if (loadout[i] != ship.Mounts[i]) OrderIfMissing(port, loadout[i]);
            return true;
        }

        /// <summary>A post differs from the plan and the port can act on it now: take the weapon off, or fit one it holds.</summary>
        public static bool NeedsRefit(Ship ship, Settlement port)
        {
            for (var i = 0; i < ship.Mounts.Length && i < ship.Loadout.Length; i++)
            {
                var want = ship.Loadout[i];
                if (want == ship.Mounts[i]) continue;
                if (want == WeaponType.None || (port != null && port.Arsenal[(int)want] > 0)) return true;
            }
            return false;
        }

        /// <summary>
        /// One refit job at the dock: a post that should be empty is cleared; a post whose planned weapon is in the
        /// arsenal has the old one taken off (3 s) and then the new one fitted (5 s medium, 10 s heavy), loaded empty,
        /// valued at its workshop price. A planned weapon the port lacks is ordered and the ship does not wait for it.
        /// </summary>
        bool Refit(Ship ship, Settlement port)
        {
            for (var i = 0; i < ship.Mounts.Length && i < ship.Loadout.Length; i++)
            {
                var want = ship.Loadout[i];
                var have = ship.Mounts[i];
                if (want == have) continue;
                if (want != WeaponType.None && port.Arsenal[(int)want] == 0)
                {
                    OrderIfMissing(port, want);
                    continue;
                }
                if (have != WeaponType.None)
                {
                    if (WeaponCatalog.Get(have).IsMachine) port.Arsenal[(int)have]++;
                    ship.Mounts[i] = WeaponType.None;
                    ship.ItemWood[i] = ship.ItemSilver[i] = 0;
                    ship.MusterTicks = (int)(Rules.DismountSeconds * Rules.TickRate) - 1;
                    Emit("weapon_dismounted", ship.Id, port.Id, ship.X, ship.Y, (int)have);
                    return true;
                }
                var definition = WeaponCatalog.Get(want);
                port.Arsenal[(int)want]--;
                ship.Mounts[i] = want;
                ship.ModuleHp[i] = Rules.ModuleHitPoints;
                ship.Progress[i] = 0;
                ship.ItemWood[i] = definition.CostWood * Rules.Centi;
                ship.ItemSilver[i] = definition.CostSilver * Rules.Centi;
                ship.MusterTicks = (int)((definition.Slot == SlotSize.Heavy ? Rules.MountHeavySeconds : Rules.MountMediumSeconds) * Rules.TickRate) - 1;
                Emit("weapon_mounted", ship.Id, port.Id, ship.X, ship.Y, (int)want);
                return true;
            }
            return false;
        }

        // ---------- Blueprints ----------

        CommandReason SaveBlueprint(Command command)
        {
            if (string.IsNullOrWhiteSpace(command.Name) || command.Loadout == null) return CommandReason.InvalidOrder;
            var slots = ShipCatalog.Get(command.Class).Slots;
            if (command.Loadout.Length != slots.Length) return CommandReason.InvalidOrder;
            for (var i = 0; i < slots.Length; i++) if (!WeaponCatalog.Fits(command.Loadout[i], slots[i])) return CommandReason.InvalidOrder;
            var name = command.Name.Trim();
            var existing = Blueprints.FirstOrDefault(item => item.Owner == command.Faction && item.Name == name);
            if (existing == null)
            {
                if (Blueprints.Count(item => item.Owner == command.Faction) >= Rules.MaxBlueprints) return CommandReason.NoSpace;
                existing = new Blueprint { Id = NextBlueprintId++, Owner = command.Faction, Name = name };
                Blueprints.Add(existing);
            }
            existing.Class = command.Class;
            existing.Loadout = (WeaponType[])command.Loadout.Clone();
            existing.Preset = command.Preset == CrewPreset.Custom ? ShipCatalog.Get(command.Class).DefaultPreset : command.Preset;
            return CommandReason.None;
        }

        /// <summary>A blueprint's hull is ordered at the class price; the port at once orders the weapons it lacks.</summary>
        void OrderBlueprintWeapons(Settlement port, Blueprint blueprint)
        {
            var defaults = ShipCatalog.Get(blueprint.Class).DefaultLoadout;
            for (var i = 0; i < blueprint.Loadout.Length; i++)
                if (blueprint.Loadout[i] != (defaults.Length > i ? defaults[i] : WeaponType.None)) OrderIfMissing(port, blueprint.Loadout[i]);
        }

        /// <summary>A launched hull built after a blueprint takes its plans; the regroup in port does the rest.</summary>
        void ApplyBlueprint(Ship ship, int blueprintId)
        {
            var blueprint = Blueprints.FirstOrDefault(item => item.Id == blueprintId && item.Owner == ship.Owner && item.Class == ship.Class);
            if (blueprint == null) return;
            ship.Plan = ship.Definition.PlanFor(blueprint.Preset);
            if (LoadoutValid(ship.Owner, ship.Class, blueprint.Loadout)) ship.Loadout = (WeaponType[])blueprint.Loadout.Clone();
        }
    }
}
