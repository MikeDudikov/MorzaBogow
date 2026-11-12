using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MorzaBogow.Simulation;
using MorzaBogow.Simulation.Catalog;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// P02: the catalogue of definitions as data (Runtime/Simulation/Catalog). Its counts, its references, its agreement
    /// with what the running game does today, the rule of availability, the source notes and its identity.
    /// </summary>
    public sealed class CatalogTests
    {
        static World Empty()
        {
            var world = new World(7);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = 5000 * Rules.Centi, Silver = 5000 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 5000 * Rules.Centi, Silver = 5000 * Rules.Centi });
            return world;
        }

        // ---------- Counts and soundness ----------

        [Test]
        public void Catalogue_HoldsTheNumbersThePlanNames()
        {
            // docs/PLAN_WDROZENIA.md §6: every number has its own denominator; none is a share of readiness.
            var catalog = GameCatalog.Default;
            Assert.That(catalog.ShipVariants.Select(item => item.Id), Is.EqualTo(Enumerable.Range(1, 12).Select(i => "N" + i.ToString("00"))), "Twelve fleet roles.");
            Assert.That(catalog.ShipVariants.Count(item => item.Role == RoleKind.Economic), Is.EqualTo(2));
            Assert.That(catalog.ShipVariants.Count(item => item.Role == RoleKind.Combat), Is.EqualTo(6));
            Assert.That(catalog.ShipVariants.Count(item => item.Role == RoleKind.Specialisation), Is.EqualTo(4));
            foreach (var faction in new[] { FactionId.A, FactionId.B })
            {
                Assert.That(catalog.VariantsOf(faction).Count(), Is.EqualTo(10), $"Ten roles for faction {faction}.");
                Assert.That(catalog.VariantsOf(faction).Count(item => item.Role == RoleKind.Specialisation), Is.EqualTo(2));
            }
            Assert.That(catalog.Forts.Select(item => item.Id), Is.EqualTo(new[] { "F01", "F02", "F03" }));
            Assert.That(catalog.WeaponFamilies.Count, Is.EqualTo(6), "Six families of throwing machines.");
            Assert.That(catalog.WeaponFamilies.Sum(item => item.RuntimeTypes.Length), Is.EqualTo(7), "Seven machine types of the running game.");
            Assert.That(catalog.SpecialWeapons.Count, Is.EqualTo(2));
            Assert.That(catalog.UpgradeLevels.Count(item => item.Kind == UpgradeKind.Armor), Is.EqualTo(3));
            Assert.That(catalog.UpgradeLevels.Count(item => item.Kind == UpgradeKind.Ram), Is.EqualTo(3));
            Assert.That(catalog.Abilities.Select(item => item.Id), Is.EqualTo(Enumerable.Range(1, 12).Select(i => "M" + i.ToString("00"))));
            Assert.That(catalog.Heroes.Select(item => item.Id), Is.EqualTo(Enumerable.Range(1, 9).Select(i => "H0" + i)));
            Assert.That(catalog.Artifacts.Where(item => item.Kind == ArtifactKind.Utility).Select(item => item.Id), Is.EqualTo(Enumerable.Range(1, 11).Select(i => "T" + i.ToString("00"))));
            Assert.That(catalog.Artifacts.Where(item => item.Kind == ArtifactKind.RecruitSign).Select(item => item.Id), Is.EqualTo(Enumerable.Range(1, 9).Select(i => "R0" + i)));
            Assert.That(catalog.Encounters.Where(item => item.Kind == EncounterKind.Threat).Select(item => item.Id), Is.EqualTo(Enumerable.Range(1, 11).Select(i => "Z" + i.ToString("00"))));
            Assert.That(catalog.Encounters.Count(item => item.Kind == EncounterKind.Shrine), Is.EqualTo(2));
            Assert.That(catalog.Encounters.Count(item => item.Kind == EncounterKind.FlotillaConfiguration), Is.EqualTo(5));
            Assert.That(catalog.Encounters.Count(item => item.Kind == EncounterKind.EconomicTarget), Is.EqualTo(1));
            Assert.That(catalog.Scenarios.Where(item => item.Kind == ScenarioKind.Campaign).Select(item => item.Id), Is.EqualTo(Enumerable.Range(1, 14).Select(i => "K" + i.ToString("00"))));
            Assert.That(catalog.Scenarios.Where(item => item.Kind == ScenarioKind.Skirmish).Select(item => item.Id), Is.EqualTo(new[] { "S00", "S01", "S02", "S03" }));
            Assert.That(catalog.Technologies.Where(item => item.Scope == TechnologyScope.PortBuilding).Select(item => item.Id), Is.EqualTo(Enumerable.Range(1, 10).Select(i => "B" + i.ToString("00"))));
            Assert.That(catalog.Technologies.Count(item => item.Id.StartsWith("TECH-WS-")), Is.EqualTo(18), "Nine workshop axes of two grades.");
            Assert.That(catalog.Technologies.Count(item => item.Id.StartsWith("TECH-LEARN-")), Is.EqualTo(12), "One learning a power.");
        }

        [Test]
        public void Catalogue_IsSoundEveryIdOnceAndEveryReferenceResolved()
        {
            var catalog = GameCatalog.Default;
            Assert.That(catalog.Problems(), Is.Empty);
            var ids = catalog.AllIds().ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "Every ID once.");
            Assert.That(ids.All(id => !string.IsNullOrWhiteSpace(id)), Is.True);

            // References are text and resolve inside the catalogue; the effects alone point outside it, by ID only.
            foreach (var hero in catalog.Heroes)
            {
                var sign = catalog.Artifact(hero.RecruitSignId);
                Assert.That((sign?.Kind, sign?.HeroId), Is.EqualTo(((ArtifactKind?)ArtifactKind.RecruitSign, hero.Id)), hero.Id);
            }
            foreach (var ability in catalog.Abilities)
            {
                Assert.That(catalog.Technology(ability.LearnTechnologyId), Is.Not.Null, ability.Id);
            }
            foreach (var weapon in catalog.SpecialWeapons) Assert.That(catalog.Variant(weapon.CarrierVariantId).BuiltInWeaponId, Is.EqualTo(weapon.Id));
            foreach (var level in catalog.UpgradeLevels.Where(item => item.Level > 1)) Assert.That(catalog.Technology(level.TechnologyId), Is.Not.Null, level.Id);
            var technologies = catalog.ShipVariants.SelectMany(item => item.Availability.Technologies.Concat(item.Availability.TargetTechnologies))
                .Concat(catalog.Forts.SelectMany(item => item.Availability.TargetTechnologies)).Concat(catalog.Technologies.SelectMany(item => item.Availability.TargetTechnologies));
            foreach (var id in technologies) Assert.That(catalog.Technology(id), Is.Not.Null, id);
        }

        [Test]
        public void Effects_AreNamedByTheirOwnersIdUniqueAndShortEnoughToSave()
        {
            // The effect contract keeps effects under the catalogue's own IDs, and they go into a save. An owner with one
            // effect names it by its own ID; an owner with several by its ID, a dot and a kind in capital ASCII letters
            // and digits. No effect is named twice, and none is longer than an effect record keeps.
            var catalog = GameCatalog.Default;
            var effects = catalog.AllEffectRefs().ToList();
            Assert.That(effects.Count, Is.GreaterThan(50));
            foreach (var (ownerId, effectId) in effects)
            {
                Assert.That(effectId == ownerId || effectId.StartsWith(ownerId + ".", StringComparison.Ordinal), Is.True, $"{ownerId} names {effectId}");
                Assert.That(effectId.Length, Is.LessThanOrEqualTo(64), effectId);
                Assert.That(EffectIds.IsValidFor(ownerId, effectId), Is.True, effectId);
                if (effectId == ownerId) continue;
                var kind = effectId.Substring(ownerId.Length + 1);
                Assert.That(kind, Is.Not.Empty, effectId);
                Assert.That(kind.All(character => (character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9')), Is.True, effectId + ": the kind is capital ASCII letters and digits");
            }
            Assert.That(effects.Select(item => item.effectId).Distinct().Count(), Is.EqualTo(effects.Count), "Every effect is named once in the whole catalogue.");
            Assert.That(EffectIds.MaxLength, Is.EqualTo(64));

            // An owner names either its one effect by its own ID, or two and more by kinds; never both ways at once.
            foreach (var owner in effects.GroupBy(item => item.ownerId))
            {
                var own = owner.Count(item => item.effectId == owner.Key);
                Assert.That(own == 1 ? owner.Count() == 1 : own == 0 && owner.Count() >= 2, Is.True, $"{owner.Key} names {string.Join(", ", owner.Select(item => item.effectId))}");
            }
            string[] Of(string ownerId) => effects.Where(item => item.ownerId == ownerId).Select(item => item.effectId).ToArray();
            Assert.That(Of("M11"), Is.EqualTo(new[] { "M11" }));
            Assert.That(Of("T05"), Is.EqualTo(new[] { "T05" }));
            Assert.That(Of("M01"), Is.EqualTo(new[] { "M01.ZONE", "M01.PULL" }), "The whirlpool harms and pulls.");
            Assert.That(Of("H06"), Is.EqualTo(new[] { "H06.RENEW", "H06.WARD" }), "The shipwright renews and wards.");
            Assert.That(Of("T01"), Is.EqualTo(new[] { "T01.HULL", "T01.MODULES", "T01.CREW" }));
            Assert.That(Of("T09"), Is.EqualTo(new[] { "T09.FIRE", "T09.GAZE" }));
            Assert.That(Of(SpecialWeaponIds.Fire), Is.EqualTo(new[] { SpecialWeaponIds.Fire }));
            // Every power, hero, useful object, threat and shrine names what it does; signs, flotillas and the neutral target name nothing.
            foreach (var id in catalog.Abilities.Select(item => item.Id).Concat(catalog.Heroes.Select(item => item.Id))
                .Concat(catalog.Artifacts.Where(item => item.Kind == ArtifactKind.Utility).Select(item => item.Id))
                .Concat(catalog.Encounters.Where(item => item.Kind == EncounterKind.Threat || item.Kind == EncounterKind.Shrine).Select(item => item.Id)))
                Assert.That(Of(id), Is.Not.Empty, id);
            foreach (var id in catalog.Artifacts.Where(item => item.Kind == ArtifactKind.RecruitSign).Select(item => item.Id)
                .Concat(catalog.Encounters.Where(item => item.Kind == EncounterKind.FlotillaConfiguration || item.Kind == EncounterKind.EconomicTarget).Select(item => item.Id)))
                Assert.That(Of(id), Is.Empty, id);

            // The rule itself.
            Assert.That(EffectIds.Of("M01", "ZONE"), Is.EqualTo("M01.ZONE"));
            foreach (var (owner, effect, valid) in new[]
            {
                ("M01", "M01", true), ("M01", "M01.ZONE", true), ("M01", "M01.Z9", true), ("Z-WIND", "Z-WIND.SPOUT", true),
                ("M01", "EFF-M01", false), ("M01", "M01.", false), ("M01", "M01.zone", false), ("M01", "M01.ZO NE", false), ("M01", "M01.ZONE.X", false),
                ("M01", "M010", false), ("M01", "M02", false), ("M01", "M01-ZONE", false), ("M01", "", false), ("M01", null, false), ("", "X", false),
                ("M01", "M01." + new string('A', 60), true), ("M01", "M01." + new string('A', 61), false),
            })
                Assert.That(EffectIds.IsValidFor(owner, effect), Is.EqualTo(valid), $"{owner} / {effect}");
            // The channel names are those of the effect contract, as text.
            Assert.That(EffectChannelNames.All.Distinct().Count(), Is.EqualTo(EffectChannelNames.All.Length));
            Assert.That(EffectChannelNames.All, Does.Contain("Wind").And.Contain("FireZone").And.Contain("GorgonGaze").And.Contain("SirenPull"));
        }

        [Test]
        public void Effects_TheCataloguesChannelNamesAndIdLimitAreTheEffectContractsOwn()
        {
            // The catalogue names channels as text and keeps its own limit of an effect ID's length, so that it does not
            // depend on the effect contract's types. This test is where the two are held together: a channel added to the
            // contract, or a limit changed there, fails here until the catalogue follows.
            var channels = ((MorzaBogow.Simulation.Effects.EffectChannel[])Enum.GetValues(typeof(MorzaBogow.Simulation.Effects.EffectChannel)))
                .Where(channel => channel != MorzaBogow.Simulation.Effects.EffectChannel.None)
                .OrderBy(channel => (int)channel)
                .Select(channel => channel.ToString())
                .ToArray();
            Assert.That(channels.Length, Is.GreaterThan(10));
            Assert.That(EffectChannelNames.All, Is.EqualTo(channels), "Every channel of the contract but None, in the order of their numbers.");
            Assert.That(EffectIds.MaxLength, Is.EqualTo(MorzaBogow.Simulation.Effects.EffectSerializer.MaxDefinitionIdLength), "An effect ID the catalogue allows is one an effect record can save.");
            // A note that was never filled in claims nothing.
            Assert.That(new SourceNote().Status, Is.EqualTo(SourceStatus.Undetermined));
            Assert.That(new ReinforcementManifest().Source.Status, Is.EqualTo(SourceStatus.Undetermined));
        }

        [Test]
        public void Catalogue_ItsCheckFindsWhatIsBroken()
        {
            // The soundness check must bite: each fault put into a catalogue of its own is reported.
            void Finds(Action<GameCatalog> fault, string expected)
            {
                var catalog = GameCatalog.Create();
                fault(catalog);
                Assert.That(catalog.Problems(), Has.Some.Matches<string>(problem => problem.Contains(expected)), expected);
            }
            Finds(c => c.Heroes[1].Id = c.Heroes[0].Id, "the ID is used");
            Finds(c => c.ShipVariants[5].Availability.TargetTechnologies = new[] { "TECH-NOT-THERE" }, "asks for a technology that is not in the catalogue");
            Finds(c => c.ShipVariants[8].BuiltInWeaponId = "C04-NOT-THERE", "a built-in weapon that is not in the catalogue");
            Finds(c => c.ShipVariants[7].CostSilver = 10, "the crew costs more silver than the ship");
            Finds(c => c.ShipVariants[2].CostWood = -1, "a negative price");
            Finds(c => c.Technologies[3].CostSilver = -1, "a negative price or time");
            Finds(c => c.Forts[0].PoolCost = 16, "a pool cost outside the settlement's pool");
            Finds(c => c.Heroes[0].RecruitSignId = "R02", "its recruitment sign is missing or belongs to another");
            Finds(c => c.Abilities[0].LearnTechnologyId = "TECH-LEARN-M99", "its learning is not in the catalogue");
            Finds(c => c.Abilities[0].DurationSeconds = 0, "a timed power without a time");
            Finds(c => c.Encounters[0].OwnerSlot = Owners.MainAi, "belongs to the independent slot or to nobody");
            Finds(c => c.WeaponFamilies[3].RuntimeTypes = new[] { WeaponType.DeckBallista }, "a machine belongs to exactly one family");
            Finds(c => c.WeaponFamilies[0].RuntimeTypes = new[] { WeaponType.Scorpion, WeaponType.ArcherBank }, "not a machine, yet in a family");
            Finds(c => c.Scenarios[0].SectorMeters = 2700, "The sector is larger than the domain of the map");
            Finds(c => c.AiProfiles[0].ReactionTicks = 0, "at least a tick between decisions");
            Finds(c => c.Abilities[2].Source.Locator = " ", "no source locator");
            // The research graph: a research must never need what only it opens (docs/KATALOG_MECHANIK.md §2).
            Finds(c => c.Technology("B08").Availability.TargetTechnologies = new[] { TechnologyIds.HeavyArms }, "the research graph has a cycle");
            Finds(c => c.Abilities[10].EffectRefs = new[] { "EFF-M11" }, "the effect ID EFF-M11 is not its own ID");
            Finds(c => c.Heroes[5].EffectRefs = new[] { "H06.renew" }, "is not its own ID, nor its ID with a kind");
            Finds(c => c.Artifacts[4].EffectRefs = new[] { "T05." + new string('A', 61) }, "or is too long");
            Finds(c => c.Artifacts[5].EffectRefs = new[] { "T06", "T06" }, "the effect is named 2 times");
            Finds(c => c.Encounters[11].ImmuneChannel = "wind", "proof against a channel that is not");
            // Broken lists are reported, never thrown at the caller.
            Finds(c => c.Scenarios[0].Sides[1] = null, "The list of sides is missing or has an empty entry");
            Finds(c => c.Scenarios[0].Sides = null, "The list of sides is missing or has an empty entry");
            Finds(c => c.Scenarios[0].Carry = null, "No carry policy");
            Finds(c => { c.Reinforcements.Add(SoundWave()); c.Reinforcements[0].Units[0] = null; }, "Every entry holds one ship or more");
            Finds(c => { c.Reinforcements.Add(SoundWave()); c.Reinforcements[0].Source.Locator = ""; }, "no source locator");
            Finds(c => { c.Reinforcements.Add(SoundWave()); c.Reinforcements[0].Source = null; }, "no source locator");
            Finds(c => c.Reinforcements.Add(new ReinforcementManifest { Id = "W1", ScenarioId = "K99", SourceId = "PortW1", Source = SoundWave().Source, Units = new[] { new ManifestUnit { VariantId = "N03", Count = 2 } }, Trigger = ReinforcementTrigger.AtTick, TriggerArg = "900", AnnounceSeconds = 10 }),
                "a wave of no scenario");
        }

        [Test]
        public void Research_HasNoCycleAndHeavyArmsNeedNothingTheyOpen()
        {
            var catalog = GameCatalog.Default;
            // Walk every technology's prerequisites to the roots: no path comes back to where it started.
            foreach (var technology in catalog.Technologies)
            {
                var seen = new HashSet<string>();
                var open = new Stack<string>(catalog.Prerequisites(technology));
                while (open.Count > 0)
                {
                    var id = open.Pop();
                    Assert.That(id, Is.Not.EqualTo(technology.Id), $"{technology.Id} needs itself.");
                    if (!seen.Add(id)) continue;
                    foreach (var next in catalog.Prerequisites(catalog.Technology(id))) open.Push(next);
                }
            }
            var heavy = catalog.Technology(TechnologyIds.HeavyArms);
            var needs = new HashSet<string>(catalog.Prerequisites(heavy));
            Assert.That(needs, Is.EquivalentTo(new[] { "B04", "B08" }), "Heavy arms: a workshop and the knowledge centre.");
            var opened = catalog.ShipVariants.Where(item => item.Availability.TargetTechnologies.Contains(TechnologyIds.HeavyArms)).Select(item => item.Id)
                .Concat(catalog.Forts.Where(item => item.Availability.TargetTechnologies.Contains(TechnologyIds.HeavyArms)).Select(item => item.Id));
            Assert.That(opened, Is.EquivalentTo(new[] { "N06", "F03" }), "What it opens is not among what it needs.");
        }

        // ---------- The fleet roles and the running game ----------

        [Test]
        public void FleetRoles_N01ToN07AreTheSevenClassesOfTheRunningGameValueForValue()
        {
            var catalog = GameCatalog.Default;
            var mapping = new (string id, ShipClass shipClass)[]
            {
                ("N01", ShipClass.Cutter), ("N02", ShipClass.Merchant), ("N03", ShipClass.Ram), ("N04", ShipClass.CrewDestroyer),
                ("N05", ShipClass.ShipKiller), ("N06", ShipClass.Bomber), ("N07", ShipClass.Boarder),
            };
            Assert.That(mapping.Select(item => item.shipClass), Is.EquivalentTo((ShipClass[])Enum.GetValues(typeof(ShipClass))), "Every class of the running game has its role.");
            foreach (var (id, shipClass) in mapping)
            {
                var variant = catalog.Variant(id);
                var definition = ShipCatalog.Get(shipClass);
                Assert.That((variant.IsRuntimeClass, variant.HullClass), Is.EqualTo((true, shipClass)), id);
                Assert.That(catalog.VariantOf(shipClass), Is.SameAs(variant), id);
                Assert.That(variant.Slots, Is.EqualTo(definition.Slots), id + " posts");
                Assert.That(variant.DefaultLoadout, Is.EqualTo(definition.DefaultLoadout), id + " weapons");
                Assert.That((variant.CostWood, variant.CostSilver, variant.MaxCrew, variant.BuildSeconds), Is.EqualTo((definition.CostWood, definition.CostSilver, definition.MaxCrew, definition.BuildSeconds)), id);
                Assert.That(variant.Role == RoleKind.Economic, Is.EqualTo(definition.IsEconomic), id);
                Assert.That(variant.HeroSlots, Is.EqualTo(definition.IsEconomic ? 0 : HeroDefinition.PerWarship), id + ": no hero aboard a working ship");
                Assert.That((variant.ArtifactSlots, variant.Availability.Implemented, variant.Provisional, variant.BuiltInWeaponId), Is.EqualTo((ArtifactDefinition.PerShip, true, false, (string)null)), id);
                Assert.That(variant.Availability.Date, Is.Null, id + ": a role of the game, asked for no date");
            }
            foreach (var planned in catalog.ShipVariants.Where(item => !item.IsRuntimeClass))
                Assert.That((planned.Availability.Implemented, planned.Provisional), Is.EqualTo((false, true)), planned.Id + " is planned");
            var command = catalog.Variant("N08");
            Assert.That((command.ArtifactSlots, command.CanBeFlagship, command.HeroSlots), Is.EqualTo((ArtifactDefinition.PerCommandShip, true, 1)), "The command ship: two places and the flagship's right.");
            Assert.That(catalog.ShipVariants.Count(item => item.CanBeFlagship), Is.EqualTo(1));
            // No two roles share one array of posts: changing one role's posts must not change another's.
            foreach (var first in catalog.ShipVariants)
                foreach (var second in catalog.ShipVariants)
                    if (first != second)
                    {
                        Assert.That(ReferenceEquals(first.Slots, second.Slots) && first.Slots.Length > 0, Is.False, $"{first.Id} and {second.Id} share their posts");
                        Assert.That(ReferenceEquals(first.DefaultLoadout, second.DefaultLoadout) && first.DefaultLoadout.Length > 0, Is.False, $"{first.Id} and {second.Id} share their weapons");
                    }
            Assert.That(ReferenceEquals(catalog.Variant("N08").Slots, catalog.Variant("N12").Slots), Is.False, "The command ship and the mourning ship each have their own posts.");
            Assert.That(catalog.Variant("N08").Slots, Is.EqualTo(catalog.Variant("N12").Slots), "Though they are the same posts today.");
            Assert.That(catalog.ShipVariants.Where(item => item.Availability.Faction == FactionId.A).Select(item => item.Id), Is.EqualTo(new[] { "N09", "N10" }));
            Assert.That(catalog.ShipVariants.Where(item => item.Availability.Faction == FactionId.B).Select(item => item.Id), Is.EqualTo(new[] { "N11", "N12" }));
        }

        [Test]
        public void FleetRoles_NoPriceIsNegativeAndEveryCrewFitsItsHullAndItsPrice()
        {
            // Q03: the price of the hull, of the crew and of the weapons are each nothing or more and add up to the price
            // paid; no crew is larger than its hull.
            var catalog = GameCatalog.Default;
            foreach (var variant in catalog.ShipVariants)
            {
                Assert.That(variant.CostWood, Is.GreaterThanOrEqualTo(0), variant.Id);
                Assert.That(variant.CostSilver, Is.GreaterThanOrEqualTo(0), variant.Id);
                Assert.That(variant.BuildSeconds, Is.GreaterThan(0), variant.Id);
                Assert.That(variant.MaxCrew, Is.GreaterThan(0), variant.Id);

                var profile = ShipCatalog.Get(variant.HullClass);
                var sailors = profile.IsEconomic ? variant.MaxCrew : (int)Math.Ceiling(variant.MaxCrew * 0.25);
                var crewSilver = sailors * Rules.Centi / 2 + (variant.MaxCrew - sailors) * Rules.Centi;
                Assert.That(crewSilver, Is.LessThanOrEqualTo(variant.CostSilver * Rules.Centi), variant.Id + ": the crew is paid out of the price");

                if (!variant.IsRuntimeClass) continue;
                foreach (CrewPreset preset in Enum.GetValues(typeof(CrewPreset)))
                {
                    if (preset == CrewPreset.Custom) continue;
                    var plan = profile.PlanFor(preset);
                    Assert.That(plan.Sailors + plan.Swordsmen + plan.Javelins + plan.Archers, Is.LessThanOrEqualTo(variant.MaxCrew), $"{variant.Id} {preset}: the crew fits the hull");
                    Assert.That(new[] { plan.Sailors, plan.Swordsmen, plan.Javelins, plan.Archers }, Is.All.GreaterThanOrEqualTo(0), $"{variant.Id} {preset}");
                }
                var parts = new[]
                {
                    TemplatePricing.ConstructionWood(profile), TemplatePricing.EquipmentWood(profile), TemplatePricing.ConstructionSilver(profile),
                    TemplatePricing.EquipmentSilver(profile), TemplatePricing.CrewSilver(profile),
                };
                Assert.That(parts, Is.All.GreaterThanOrEqualTo(0), variant.Id + ": no part of the price is negative");
                Assert.That(parts[0] + parts[1], Is.EqualTo(variant.CostWood * Rules.Centi), variant.Id + ": the wood adds up");
                Assert.That(parts[2] + parts[3] + parts[4], Is.EqualTo(variant.CostSilver * Rules.Centi), variant.Id + ": the silver adds up");
                // Where the hull carries anything of worth (a default weapon, a ram), those things are worth the equipment share.
                if (profile.CanRam || profile.DefaultLoadout.Any(weapon => weapon != WeaponType.None))
                {
                    Assert.That(profile.ItemValues().Sum(item => item.wood), Is.EqualTo(parts[1]), variant.Id + ": the weapons' worth is the equipment share in wood");
                    Assert.That(profile.ItemValues().Sum(item => item.silver), Is.EqualTo(parts[3]), variant.Id + ": and in silver");
                }
            }
            foreach (var technology in catalog.Technologies) Assert.That(new[] { technology.CostWood, technology.CostSilver }, Is.All.GreaterThanOrEqualTo(0), technology.Id);
            foreach (var fort in catalog.Forts) Assert.That(new[] { fort.CostWood, fort.CostSilver }, Is.All.GreaterThanOrEqualTo(0), fort.Id);
            foreach (var ability in catalog.Abilities) Assert.That(new[] { ability.CostWood, ability.CostSilver }, Is.All.GreaterThanOrEqualTo(0), ability.Id);
        }

        /// <summary>Every state a port of the running game can be in that the availability rules look at.</summary>
        static IEnumerable<(World world, Settlement port, string name)> PortStates()
        {
            for (var mask = 0; mask < 128; mask++)
            {
                var world = Empty();
                var port = world.SettlementOf(0);
                port.HasShipyard = (mask & 1) != 0;
                port.HasWorkshop = (mask & 2) != 0;
                port.IsTown = (mask & 4) != 0;
                port.HasSageSchool = (mask & 8) != 0;
                port.HasDock = (mask & 16) != 0;
                port.IsOutpost = (mask & 32) != 0;
                if ((mask & 64) != 0) world.HeavyArmsResearched |= 1 << port.Owner;
                // A home port of the side must still exist when this one is a bridgehead.
                if (port.IsOutpost) world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -700, Y = 0 });
                yield return (world, port, $"yard {port.HasShipyard}, workshop {port.HasWorkshop}, town {port.IsTown}, school {port.HasSageSchool}, dock {port.HasDock}, outpost {port.IsOutpost}, heavy arms {world.HasHeavyArms(port.Owner)}");
            }
        }

        [Test]
        public void Availability_OfEveryClassIsWhatTheRunningGameAsksInEveryStateOfAPort()
        {
            // D10: the catalogue's requirements are those the running game enforces today (World.ShipProblem), for all
            // seven classes in all 128 states of a port. The planned requirements are data beside them.
            var catalog = GameCatalog.Default;
            foreach (var (world, port, name) in PortStates())
            {
                var context = AvailabilityRules.ContextOf(world, port);
                foreach (ShipClass shipClass in Enum.GetValues(typeof(ShipClass)))
                {
                    var variant = catalog.VariantOf(shipClass);
                    Assert.That(AvailabilityRules.IsAvailable(variant.Availability, context), Is.EqualTo(world.ShipProblem(port, shipClass) == null), $"{variant.Id} ({shipClass}) at a port with {name}");
                }
            }
        }

        [Test]
        public void Availability_OfEveryProjectIsWhatTheRunningGameAsksInEveryStateOfAPort()
        {
            // The five projects of the running game: with the price in store, nothing being built and the thing not yet
            // there, a port may start one exactly when the catalogue's requirements are met (World.ProjectProblem).
            var catalog = GameCatalog.Default;
            var projects = new[] { ProjectKind.Workshop, ProjectKind.Town, ProjectKind.SageSchool, ProjectKind.HeavyArmsResearch, ProjectKind.Dock };
            Assert.That(catalog.Technologies.Where(item => item.RuntimeProject != ProjectKind.None).Select(item => item.RuntimeProject), Is.EquivalentTo(projects));
            var compared = 0;
            foreach (var (world, port, name) in PortStates())
            {
                var context = AvailabilityRules.ContextOf(world, port);
                foreach (var project in projects)
                {
                    var technology = catalog.TechnologyOf(project);
                    var already = technology.Scope == TechnologyScope.PortBuilding ? (context.Buildings & technology.Grants) != 0 : context.Technologies.Contains(technology.Id);
                    if (already) continue;
                    compared++;
                    Assert.That(AvailabilityRules.IsAvailable(technology.Availability, context), Is.EqualTo(world.ProjectProblem(port, project) == null), $"{technology.Id} ({project}) at a port with {name}");
                }
            }
            Assert.That(compared, Is.GreaterThan(250), "The comparison ran over the states where the project is still to be done.");
            foreach (var project in projects)
            {
                var technology = catalog.TechnologyOf(project);
                var (wood, silver, seconds) = World.ProjectCost(project);
                Assert.That((technology.CostWood * Rules.Centi, technology.CostSilver * Rules.Centi, technology.Seconds), Is.EqualTo((wood, silver, seconds)), $"{technology.Id}: the price and time of the running game");
            }
            Assert.That((catalog.Technology("B01").CostWood * Rules.Centi, catalog.Technology("B01").CostSilver * Rules.Centi, catalog.Technology("B01").Seconds),
                Is.EqualTo((Rules.OutpostWood, Rules.OutpostSilver, Rules.OutpostBuildSeconds)), "A bridgehead costs what the running game takes for it.");
            Assert.That((catalog.Technology("B03").CostWood * Rules.Centi, catalog.Technology("B03").CostSilver * Rules.Centi, catalog.Technology("B03").Seconds),
                Is.EqualTo((Rules.ShipyardWood, Rules.ShipyardSilver, Rules.ShipyardBuildSeconds)), "So does a bridgehead's shipyard.");
        }

        [Test]
        public void Availability_ThePlannedRequirementsAreThoseOfTheParametersDocument()
        {
            // docs/PARAMETRY_ROBOCZE.md §1: O settlement, P harbour, S shipyard, W workshop, K forge, M town, U knowledge.
            const PortBuildingSet O = PortBuildingSet.Settlement, S = PortBuildingSet.Shipyard, W = PortBuildingSet.Workshop, K = PortBuildingSet.Forge, M = PortBuildingSet.Town, U = PortBuildingSet.Knowledge;
            var catalog = GameCatalog.Default;
            var expected = new (string id, PortBuildingSet buildings, string[] technologies, int wood, int silver, double seconds)[]
            {
                ("N01", O | S, new string[0], 60, 20, 18), ("N02", M | S, new string[0], 140, 60, 28), ("N03", M | S, new string[0], 240, 100, 35),
                ("N04", O | S | W, new string[0], 180, 90, 30), ("N05", O | S | W, new string[0], 220, 140, 38),
                ("N06", M | S | W | U, new[] { TechnologyIds.HeavyArms, TechnologyIds.Armor2 }, 400, 260, 60), ("N07", O | S | W | K, new string[0], 200, 180, 40),
                ("N08", O | S | W | K, new[] { TechnologyIds.Command }, 300, 200, 50), ("N09", M | S | W | U, new[] { TechnologyIds.SpecA1 }, 260, 180, 45),
                ("N10", M | S | W | U, new[] { TechnologyIds.SpecA2 }, 280, 200, 45), ("N11", M | S | W | U, new[] { TechnologyIds.SpecB1 }, 240, 160, 45),
                ("N12", M | S | W | U, new[] { TechnologyIds.SpecB2 }, 280, 220, 50),
            };
            foreach (var (id, buildings, technologies, wood, silver, seconds) in expected)
            {
                var variant = catalog.Variant(id);
                Assert.That(variant.Availability.TargetBuildings, Is.EqualTo(buildings), id);
                Assert.That(variant.Availability.TargetTechnologies, Is.EqualTo(technologies), id);
                Assert.That((variant.CostWood, variant.CostSilver, variant.BuildSeconds), Is.EqualTo((wood, silver, seconds)), id);
            }
            // By the planned requirements a siege ship needs a town with a shipyard, a workshop and the knowledge centre,
            // heavy arms and armour II; the running game asks less today, and says so.
            var siege = catalog.Variant("N06").Availability;
            var port = new AvailabilityContext { Buildings = S | W, Technologies = new[] { TechnologyIds.HeavyArms }, OwnerSlot = 0 };
            Assert.That(AvailabilityRules.Evaluate(siege, port), Is.EqualTo(Unavailable.None), "Today: a shipyard, a workshop and heavy arms.");
            port.ByTarget = true;
            Assert.That(AvailabilityRules.Evaluate(siege, port), Is.EqualTo(Unavailable.MissingBuilding | Unavailable.MissingTechnology), "Planned: more.");
            port.Buildings = O | S | W | M | U;
            port.Technologies = new[] { TechnologyIds.HeavyArms, TechnologyIds.Armor2 };
            Assert.That(AvailabilityRules.Evaluate(siege, port), Is.EqualTo(Unavailable.None));
        }

        // ---------- The rule of availability ----------

        [Test]
        public void Availability_TruthTablesOfDateRulesetFactionSlotAndState()
        {
            var open = new Availability { Implemented = true };
            AvailabilityContext Context(int? year = null, string ruleset = Rulesets.Skirmish, FactionId faction = FactionId.Any, int slot = 0) =>
                new AvailabilityContext { YearBc = year, Ruleset = ruleset, Faction = faction, OwnerSlot = slot };
            Assert.That(AvailabilityRules.Evaluate(open, Context()), Is.EqualTo(Unavailable.None));

            // A date window: inside when Latest ≤ year ≤ Earliest; a ruleset without a date asks for none.
            var dated = new Availability { Implemented = true, Date = new DateWindow { EarliestYearBc = 330, LatestYearBc = 322 } };
            foreach (var (year, inside) in new[] { (331, false), (330, true), (325, true), (322, true), (321, false) })
            {
                Assert.That(dated.Date.Contains(year), Is.EqualTo(inside), year.ToString());
                Assert.That(AvailabilityRules.Evaluate(dated, Context(year, Rulesets.CampaignMaterial)), Is.EqualTo(inside ? Unavailable.None : Unavailable.OutsideDate), year.ToString());
            }
            Assert.That(AvailabilityRules.Evaluate(dated, Context(null, Rulesets.Skirmish)), Is.EqualTo(Unavailable.None), "A skirmish has no date.");
            Assert.That(AvailabilityRules.Evaluate(open, Context(250, Rulesets.CampaignMaterial)), Is.EqualTo(Unavailable.None), "No window: any year.");

            // Rulesets: an empty list is every ruleset; otherwise only those named.
            var mythic = new Availability { Implemented = true, Rulesets = new[] { Rulesets.CampaignMythic, Rulesets.Skirmish } };
            foreach (var ruleset in Rulesets.All)
            {
                Assert.That(AvailabilityRules.Evaluate(open, Context(null, ruleset)), Is.EqualTo(Unavailable.None), ruleset);
                Assert.That(AvailabilityRules.Evaluate(mythic, Context(null, ruleset)), Is.EqualTo(ruleset == Rulesets.CampaignMaterial ? Unavailable.WrongRuleset : Unavailable.None), ruleset);
            }
            Assert.That(AvailabilityRules.Evaluate(mythic, Context(null, "unknown")), Is.EqualTo(Unavailable.WrongRuleset));

            // Factions and owner slots.
            var ofA = new Availability { Implemented = true, Faction = FactionId.A };
            Assert.That(new[] { FactionId.Any, FactionId.A, FactionId.B }.Select(faction => AvailabilityRules.Evaluate(ofA, Context(faction: faction))),
                Is.EqualTo(new[] { Unavailable.WrongFaction, Unavailable.None, Unavailable.WrongFaction }));
            Assert.That(new[] { FactionId.Any, FactionId.A, FactionId.B }.Select(faction => AvailabilityRules.Evaluate(open, Context(faction: faction))), Is.All.EqualTo(Unavailable.None));
            var independent = new Availability { Implemented = true, OwnerSlots = Availability.IndependentSlot };
            Assert.That(new[] { -1, 0, 1, 2, 3 }.Select(slot => AvailabilityRules.Evaluate(open, Context(slot: slot))),
                Is.EqualTo(new[] { Unavailable.WrongSlot, Unavailable.None, Unavailable.None, Unavailable.WrongSlot, Unavailable.WrongSlot }), "By default the two main slots.");
            Assert.That(new[] { -1, 0, 1, 2, 3 }.Select(slot => AvailabilityRules.Evaluate(independent, Context(slot: slot))),
                Is.EqualTo(new[] { Unavailable.WrongSlot, Unavailable.WrongSlot, Unavailable.WrongSlot, Unavailable.None, Unavailable.WrongSlot }));

            // Not implemented, the home port, and several reasons at once.
            Assert.That(AvailabilityRules.Evaluate(new Availability(), Context()), Is.EqualTo(Unavailable.NotImplemented));
            var home = new Availability { Implemented = true, HomePortOnly = true };
            Assert.That(AvailabilityRules.Evaluate(home, new AvailabilityContext { IsOutpost = true }), Is.EqualTo(Unavailable.NotHomePort));
            Assert.That(AvailabilityRules.Evaluate(home, new AvailabilityContext()), Is.EqualTo(Unavailable.None));
            var many = new Availability { Buildings = PortBuildingSet.Forge, Technologies = new[] { "TECH-X" }, Faction = FactionId.B, Date = dated.Date, Rulesets = new[] { Rulesets.CampaignMythic } };
            Assert.That(AvailabilityRules.Evaluate(many, Context(300, Rulesets.CampaignMaterial, FactionId.A, 2)),
                Is.EqualTo(Unavailable.NotImplemented | Unavailable.MissingBuilding | Unavailable.MissingTechnology | Unavailable.WrongFaction | Unavailable.WrongSlot | Unavailable.OutsideDate | Unavailable.WrongRuleset));
            // The rule is a function of its arguments alone: asked twice, it answers the same and changes neither.
            var context = Context(325, Rulesets.CampaignMaterial);
            Assert.That(AvailabilityRules.Evaluate(dated, context), Is.EqualTo(AvailabilityRules.Evaluate(dated, context)));
            Assert.That((context.YearBc, dated.Date.EarliestYearBc, dated.Date.LatestYearBc), Is.EqualTo(((int?)325, 330, 322)));
        }

        // ---------- Sources, machines and dates ----------

        [Test]
        public void Sources_EveryDefinitionHasItsStatusItsLayerAndItsLocator()
        {
            // Q10: nothing in the catalogue stands without saying what it leans on.
            var catalog = GameCatalog.Default;
            var notes = catalog.AllSources().ToList();
            Assert.That(notes.Count, Is.EqualTo(catalog.AllIds().Count()), "Every definition has a note.");
            foreach (var (id, note) in notes)
            {
                Assert.That(note, Is.Not.Null, id);
                Assert.That(Enum.IsDefined(typeof(SourceStatus), note.Status) && Enum.IsDefined(typeof(WorldLayer), note.Layer), Is.True, id);
                Assert.That(note.Locator, Is.Not.Null.And.Not.Empty, id);
            }
            Assert.That(((SourceStatus[])Enum.GetValues(typeof(SourceStatus))).Select(SourceNote.Code),
                Is.EqualTo(new[] { "POTWIERDZONE_W_ZAKRESIE", "REKONSTRUKCJA", "TRADYCJA_MITOLOGICZNA", "REGUŁA_GRY", "NIEUSTALONE", "BLOCKED_RESEARCH" }), "The six statuses of the protocol.");
            Assert.That(Enum.GetNames(typeof(WorldLayer)), Is.EqualTo(new[] { "Material", "Mythic", "GameRule" }));

            // A tradition is claimed only where a source was read; what the myth does in the game stays a rule of the game.
            var traditions = notes.Where(item => item.note.Status == SourceStatus.MythTradition).ToList();
            Assert.That(traditions.Select(item => item.id), Is.EquivalentTo(new[] { "M10", "H08", "H09", "T09", "Z04", "Z05", "K10", "K11" }));
            foreach (var (id, note) in traditions)
            {
                Assert.That(note.Layer, Is.EqualTo(WorldLayer.Mythic), id);
                Assert.That(note.Locator, Does.Contain("Apollodoros").Or.Contain("Odyseja"), id);
                Assert.That(note.Locator, Does.Contain("REGUŁA_GRY").Or.Contain("fikcyjny"), id + ": the game's own part is named");
            }
            // The fleet roles are rules of the game, never presented as historical hull types.
            foreach (var variant in catalog.ShipVariants) Assert.That((variant.Source.Status, variant.Source.Layer), Is.EqualTo((SourceStatus.GameRule, WorldLayer.GameRule)), variant.Id);
            Assert.That(notes.Count(item => item.note.Status == SourceStatus.ConfirmedInScope), Is.EqualTo(3), "Only the three hull types of the inventories.");
        }

        [Test]
        public void Machines_SevenTypesInSixFamiliesAndNoneClaimedAsAtticFleetFitting()
        {
            var catalog = GameCatalog.Default;
            var machines = ((WeaponType[])Enum.GetValues(typeof(WeaponType))).Where(type => type != WeaponType.None && WeaponCatalog.Get(type).IsMachine).ToList();
            Assert.That(machines.Count, Is.EqualTo(7));
            foreach (var type in machines) Assert.That(catalog.FamilyOf(type), Is.Not.Null, type.ToString());
            Assert.That(catalog.FamilyOf(WeaponType.ArcherBank), Is.Null, "A bank of archers is no machine.");
            Assert.That(catalog.FamilyOf(WeaponType.JavelinBank), Is.Null);
            Assert.That(catalog.FamilyOf(WeaponType.DeckBallista), Is.SameAs(catalog.FamilyOf(WeaponType.LeadBallista)), "The two ballistae are variants of one family.");
            Assert.That(catalog.FamilyOf(WeaponType.DeckBallista).Id, Is.EqualTo("C03-4"));
            Assert.That(catalog.WeaponFamilies.Where(item => item.Id != "C03-4").Select(item => item.RuntimeTypes.Length), Is.All.EqualTo(1));
            foreach (var family in catalog.WeaponFamilies)
            {
                Assert.That((family.Source.Status, family.Source.Layer), Is.EqualTo((SourceStatus.Undetermined, WorldLayer.GameRule)), family.Id);
                Assert.That(family.Source.Locator, Does.Contain("REGUŁA_GRY").And.Contain("NIEUSTALONE").And.Contain("Arrian").And.Contain("kontekst oblężenia"), family.Id);
            }
            Assert.That(catalog.SpecialWeapons.Select(item => (item.Id, item.CarrierVariantId)), Is.EqualTo(new[] { (SpecialWeaponIds.Fire, "N09"), (SpecialWeaponIds.Mist, "N10") }));
        }

        [Test]
        public void Dates_TheHullTypesOfTheInventoriesAreReferenceDataWithTheirYears()
        {
            // D10: fours from 330/29, fives from 325/4, threes throughout; no fleet role asks for a date.
            var catalog = GameCatalog.Default;
            DateWindow Window(string id) => catalog.HullProfiles.Single(item => item.Id == id).Date;
            Assert.That(Window("HULL-TRIERES"), Is.Null);
            Assert.That((Window("HULL-TETRERES").EarliestYearBc, Window("HULL-PENTERES").EarliestYearBc), Is.EqualTo((330, 325)));
            foreach (var (year, fours, fives) in new[] { (331, false, false), (330, true, false), (326, true, false), (325, true, true), (322, true, true) })
            {
                Assert.That(Window("HULL-TETRERES").Contains(year), Is.EqualTo(fours), $"Fours in {year}");
                Assert.That(Window("HULL-PENTERES").Contains(year), Is.EqualTo(fives), $"Fives in {year}");
            }
            foreach (var profile in catalog.HullProfiles)
            {
                Assert.That((profile.Source.Status, profile.Source.Layer), Is.EqualTo((SourceStatus.ConfirmedInScope, WorldLayer.Material)), profile.Id);
                Assert.That(profile.Source.Locator, Does.Contain("Gabrielsen").And.Contain("s. 39"), profile.Id);
                // Each reading is credited to who made it: pages 37-40 to the coordinator's report, page 43 note 32 and
                // Diodorus to the reviewer's.
                Assert.That(profile.Source.Locator, Does.Contain("s. 37–40: odczyt koordynatora, docs/research/sessions/2026-11-12-0504-p02.md"), profile.Id);
                Assert.That(profile.Source.Locator, Does.Contain("0642").And.Contain("odczyt recenzenta, docs/research/sessions/2026-11-12-0642-p02-recenzent.md"), profile.Id);
                // The whole phrase, so that the note and Diodorus are tied to the reviewer's report and not merely named beside it.
                Assert.That(profile.Source.Locator, Does.Contain("s. 43 przyp. 32 oraz Diod. 14.41.3/14.42.2: odczyt recenzenta, docs/research/sessions/2026-11-12-0642-p02-recenzent.md"), profile.Id);
                Assert.That(profile.Source.Locator, Does.Contain("the Athenian navy: recent archaeological and historical advances"), profile.Id + ": the title as the publisher prints it");
            }
            Assert.That(catalog.ShipVariants.Select(item => item.Availability.Date), Is.All.Null);
            Assert.That((GameEpoch.EarliestYearBc, GameEpoch.LatestYearBc), Is.EqualTo((400, 301)));
        }

        // ---------- The numbers of the parameters document ----------

        [Test]
        public void Parameters_ThePowersFortsAndGradesCarryTheWorkingNumbers()
        {
            var catalog = GameCatalog.Default;
            // docs/PARAMETRY_ROBOCZE.md §4: price D/S, range (0 the whole map), radius, time, cooldown.
            var powers = new (string id, int wood, int silver, double range, double radius, double seconds, double cooldown)[]
            {
                ("M01", 180, 300, 300, 35, 60, 120), ("M02", 180, 300, 300, 25, 0, 120), ("M03", 20, 40, 0, 0, 45, 60), ("M04", 80, 140, 375, 25, 30, 90),
                ("M05", 0, 30, 0, 150, 30, 45), ("M06", 180, 260, 300, 0, 90, 120), ("M07", 220, 350, 0, 0, 30, 120), ("M08", 300, 800, 300, 60, 0, 180),
                ("M09", 100, 180, 300, 60, 20, 90), ("M10", 30, 80, 375, 0, 45, 90), ("M11", 30, 80, 375, 0, 45, 60), ("M12", 30, 80, 375, 0, 45, 60),
            };
            foreach (var (id, wood, silver, range, radius, seconds, cooldown) in powers)
            {
                var ability = catalog.Ability(id);
                Assert.That((ability.CostWood, ability.CostSilver, ability.RangeMeters, ability.RadiusMeters, ability.DurationSeconds, ability.CooldownSeconds),
                    Is.EqualTo((wood, silver, range, radius, seconds, cooldown)), id);
                Assert.That(ability.WholeMapRange, Is.EqualTo(range == 0), id);
            }
            Assert.That(catalog.Abilities.Where(item => item.WholeMapRange).Select(item => item.Id), Is.EqualTo(new[] { "M03", "M05", "M07" }));
            Assert.That((catalog.Ability("M02").Duration, catalog.Ability("M08").Duration), Is.EqualTo((DurationKind.Match, DurationKind.Strike)));
            Assert.That(catalog.Abilities.Where(item => item.Reach == ReachKind.SingleObject).Select(item => item.WarningSeconds), Is.All.EqualTo(0.5), "A single protection is announced half a second ahead.");
            Assert.That(catalog.Abilities.Where(item => item.Reach != ReachKind.SingleObject).Select(item => item.WarningSeconds), Is.All.EqualTo(3.0));
            Assert.That(catalog.Abilities.Count(item => item.Sanctuary == SanctuaryKind.Sea), Is.EqualTo(6));
            Assert.That(catalog.Abilities.Count(item => item.Sanctuary == SanctuaryKind.War), Is.EqualTo(6));

            // §3: the forts and the settlement's pool n1 + 2·n2 + 3·n3 ≤ 15.
            Assert.That(catalog.Forts.Select(item => (item.PoolCost, item.CostWood, item.CostSilver, item.BuildSeconds, item.Hull)),
                Is.EqualTo(new[] { (1, 180, 100, 45.0, 1200.0), (2, 300, 180, 60.0, 2200.0), (3, 460, 280, 90.0, 3600.0) }));
            Assert.That(catalog.Forts.Select(item => (item.Slots.Count(slot => slot == SlotSize.Light), item.Slots.Count(slot => slot == SlotSize.Medium), item.Slots.Count(slot => slot == SlotSize.Heavy))),
                Is.EqualTo(new[] { (2, 2, 0), (2, 3, 1), (2, 3, 3) }));
            Assert.That(FortDefinition.SettlementPool, Is.EqualTo(15));
            foreach (var fort in catalog.Forts)
            {
                Assert.That((fort.CanBeRammed, fort.CanBeBoarded, fort.ArtifactSlots, fort.HeroSlots), Is.EqualTo((false, false, ArtifactDefinition.PerFort, 0)), fort.Id + ": neither rammed nor boarded, one artefact, no hero");
                // The building graph is the main sides'; the forts of the independent slot get a policy of their own (P09).
                Assert.That(fort.Availability.OwnerSlots, Is.EqualTo(Availability.MainSlots), fort.Id);
            }

            // §2: armour adds 0 / 10 / 20 points; the ram multiplies by 1 / 1.15 / 1.30; grade I needs no research.
            Assert.That(catalog.UpgradeLevels.Where(item => item.Kind == UpgradeKind.Armor).Select(item => item.Value), Is.EqualTo(new[] { 0, 0.10, 0.20 }));
            Assert.That(catalog.UpgradeLevels.Where(item => item.Kind == UpgradeKind.Ram).Select(item => item.Value), Is.EqualTo(new[] { 1.0, 1.15, 1.30 }));
            Assert.That(catalog.UpgradeLevels.Where(item => item.Level == 1).Select(item => item.TechnologyId), Is.All.Null);
            Assert.That(0.20 + ShipCatalog.Get(ShipClass.Bomber).ProjectileArmor, Is.LessThanOrEqualTo(Rules.MaxHullReduction), "Armour III on the best-armoured hull stays under the limit of material protection.");

            // §6: the neutral target of harvest.
            var target = catalog.Encounters.Single(item => item.Kind == EncounterKind.EconomicTarget);
            Assert.That((target.Id, target.Unowned, target.RewardWood, target.RewardSilver, target.WorkSeconds), Is.EqualTo(("E05", true, 960, 800, 60.0)));
            Assert.That(target.Availability.OwnerSlots, Is.EqualTo(Availability.MainSlots), "The two main sides compete for it.");
            Assert.That(catalog.Encounters.Where(item => item.Kind == EncounterKind.Shrine).Select(item => (item.Id, item.ImmuneChannel)),
                Is.EqualTo(new[] { ("Z-WIND", EffectChannelNames.Wind), ("Z-FIRE", EffectChannelNames.FireZone) }), "Each shrine is proof against its own channel, by the contract's name for it.");
            foreach (var encounter in catalog.Encounters.Where(item => !item.Unowned)) Assert.That(encounter.OwnerSlot, Is.EqualTo(Owners.Independent), encounter.Id);
            Assert.That(catalog.Encounters.Where(item => item.Kind == EncounterKind.FlotillaConfiguration).Select(item => item.ScuttlesWhenBoardingLost), Is.All.True);
        }

        // ---------- Scenarios ----------

        [Test]
        public void Scenarios_ConditionsAreDataAndAnUnboundOperationDoesNotStart()
        {
            var catalog = GameCatalog.Default;
            foreach (var scenario in catalog.Scenarios)
            {
                Assert.That(ScenarioRules.Problems(scenario), Is.Empty, scenario.Id);
                Assert.That(scenario.SectorMeters, Is.InRange(1.0, ScenarioDefinition.LargestSectorMeters), scenario.Id);
                Assert.That(scenario.Sides.Select(side => side.Slot).Take(2), Is.EqualTo(new[] { Owners.Human, Owners.MainAi }), scenario.Id + ": both main sides");
                Assert.That(new SideRelations(scenario.HostilePairs), Is.EqualTo(SideRelations.AllHostile), scenario.Id + ": every pair at war unless the scenario says otherwise");
            }
            // The document's conditions, word for word in its own notation.
            Assert.That(catalog.Scenario("K01").Success.Text(), Is.EqualTo("(Delivered(DostawaT1, ManifestT1, PortOdbioru) AND Delivered(DostawaT2, ManifestT2, PortOdbioru))"));
            Assert.That(catalog.Scenario("K03").Success.Text(), Is.EqualTo("(InfrastructureReady(Wymagane) AND LoadoutsReady(Eskadra) AND Resolved(W1))"));
            Assert.That(catalog.Scenario("K08").Success.Text(), Is.EqualTo("(SourcesDisabled(S1, S2) AND OutpostOperational(P1))"));
            Assert.That(catalog.Scenario("K13").Success.Text(), Is.EqualTo("(Exited(CelRuchomyId, Strefa) AND Exited(Artefakty, Strefa) AND BeforeDeadline(Deadline))"));
            Assert.That(catalog.Scenario("K14").Success.Text(),
                Is.EqualTo("(FlagshipAlive() AND ((Delivered(DostawaKonwojów, Manifest, PortOdbioru) AND Exited(Ewakuowani, Strefa)) OR Destroyed(GłównyPortCelu)))"));

            // An operation's quantities, zones and objects are bound by its own data later: until then it may not start,
            // and missing data never counts as a goal reached.
            foreach (var operation in catalog.Scenarios.Where(item => item.Kind == ScenarioKind.Campaign))
            {
                Assert.That((operation.Implemented, operation.ParametersBound, ScenarioRules.CanStart(operation)), Is.EqualTo((false, false, false)), operation.Id);
                Assert.That(operation.Ruleset, Is.Not.EqualTo(Rulesets.Skirmish), operation.Id);
                Assert.That((operation.Carry.Flagship, operation.Carry.FlagshipArtifacts, operation.Carry.Heroes, operation.Carry.EvacuatedStore), Is.EqualTo((true, true, true, true)), operation.Id + ": the default carry policy");
            }
            var skirmish = catalog.Scenario("S00");
            Assert.That((skirmish.Implemented, ScenarioRules.CanStart(skirmish), skirmish.Ruleset, skirmish.HasYear), Is.EqualTo((true, true, Rulesets.Skirmish, false)), "The skirmish of the running game.");
            Assert.That(skirmish.Success.Text(), Is.EqualTo("MainSideDefeated(1)"));
            Assert.That(skirmish.Sides.Length, Is.EqualTo(2), "Two main sides, as MapFactory.CreateSkirmish makes them.");
            Assert.That(catalog.Scenarios.Where(item => item.Kind == ScenarioKind.Skirmish && item.Id != "S00").Select(ScenarioRules.CanStart), Is.All.False);

            // A bound and implemented operation with sound data may start; take any one thing away and it may not.
            ScenarioDefinition Ready()
            {
                var scenario = GameCatalog.Create().Scenario("K01");
                scenario.Implemented = true;
                scenario.ParametersBound = true;
                return scenario;
            }
            Assert.That(ScenarioRules.CanStart(Ready()), Is.True);
            void Blocks(Action<ScenarioDefinition> fault, string name)
            {
                var scenario = Ready();
                fault(scenario);
                Assert.That(ScenarioRules.CanStart(scenario), Is.False, name);
            }
            Blocks(s => s.ParametersBound = false, "parameters not bound");
            Blocks(s => s.Implemented = false, "not implemented");
            Blocks(s => s.Success = null, "no condition");
            Blocks(s => s.Success = Predicate.Leaf(PredicateKind.Delivered, "DostawaT1", "ManifestT1"), "an argument missing");
            Blocks(s => s.Success = Predicate.Leaf(PredicateKind.Delivered, "DostawaT1", " ", "PortOdbioru"), "an empty argument");
            Blocks(s => s.Success = Predicate.All(s.Success.Children[0]), "an AND of one branch");
            Blocks(s => s.SectorMeters = 2601, "a sector beyond the domain");
            Blocks(s => s.Sides = s.Sides.Take(1).ToArray(), "a main side missing");
            Blocks(s => s.Sides[1].Controller = SideController.Encounters, "the main opponent played by encounters");
            Blocks(s => s.Ruleset = Rulesets.Skirmish, "a campaign operation under the skirmish ruleset");
            Blocks(s => { s.HasYear = true; s.YearBc = 250; }, "a year outside the epoch");
            Blocks(s => s.HostilePairs = 8, "a pair that is not");
        }

        // ---------- Identity ----------

        [Test]
        public void Identity_TheSameContentHasTheSameHashAndItIsTheGamesOwn()
        {
            var first = GameCatalog.Create();
            var second = GameCatalog.Create();
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.CanonicalText(), Is.EqualTo(second.CanonicalText()));
            Assert.That(first.Hash(), Is.EqualTo(second.Hash()));
            Assert.That(first.Hash(), Is.EqualTo(first.Hash()), "Asked twice, the same.");
            Assert.That(GameCatalog.Default.Hash(), Is.EqualTo(first.Hash()), "The game's catalogue is this content.");
            Assert.That(GameCatalog.Default, Is.SameAs(GameCatalog.Default));
            Assert.That(first.Identity(), Is.EqualTo(GameCatalog.CatalogId + "#" + first.Hash().ToString("X16")));
            Assert.That(GameCatalog.CatalogId, Is.EqualTo("morza-bogow/catalog@1"));
            // Written out so the value from the Unity runner can be compared with the one from other runners.
            TestContext.WriteLine(GameCatalog.Default.Identity());
            // FNV-1a, 64 bits: the known values of the empty text and of "a".
            Assert.That(CanonicalWriter.Hash(""), Is.EqualTo(14695981039346656037UL));
            Assert.That(CanonicalWriter.Hash("a"), Is.EqualTo(0xAF63DC4C8601EC8CUL));
            // The text holds no time and nothing of the machine: only the definitions, each with its ID.
            var text = first.CanonicalText();
            foreach (var id in first.AllIds()) Assert.That(text, Does.Contain("#\"" + id + "\"{"), id);
            // Null and the empty text are told apart, and so are a mark inside a text and the form's own marks.
            Assert.That(new CanonicalWriter().F("a", (string)null).ToString(), Is.Not.EqualTo(new CanonicalWriter().F("a", "").ToString()));
            Assert.That(new CanonicalWriter().F("a", "x\";b=\"y").ToString(), Is.Not.EqualTo(new CanonicalWriter().F("a", "x").F("b", "y").ToString()));
        }

        /// <summary>The definitions whose every field must enter the hash: the first and the last of each list, and a wave of reinforcements.</summary>
        static List<(string name, object item)> Roots(GameCatalog catalog)
        {
            var roots = new List<(string, object)>();
            void Ends<T>(string name, List<T> list)
            {
                roots.Add((name + "[0]", list[0]));
                if (list.Count > 1) roots.Add(($"{name}[{list.Count - 1}]", list[list.Count - 1]));
            }
            Ends("ShipVariants", catalog.ShipVariants);
            Ends("HullProfiles", catalog.HullProfiles);
            Ends("WeaponFamilies", catalog.WeaponFamilies);
            Ends("SpecialWeapons", catalog.SpecialWeapons);
            Ends("UpgradeLevels", catalog.UpgradeLevels);
            Ends("Technologies", catalog.Technologies);
            Ends("Forts", catalog.Forts);
            Ends("Abilities", catalog.Abilities);
            Ends("Heroes", catalog.Heroes);
            Ends("Artifacts", catalog.Artifacts);
            Ends("Encounters", catalog.Encounters);
            Ends("Scenarios", catalog.Scenarios);
            Ends("Tasks", catalog.Tasks);
            Ends("AiProfiles", catalog.AiProfiles);
            Ends("Reinforcements", catalog.Reinforcements);
            return roots;
        }

        static ReinforcementManifest SoundWave() => new ReinforcementManifest
        {
            Id = "K03-W1", ScenarioId = "K03", SourceId = "WejścieZatoki", Units = new[] { new ManifestUnit { VariantId = "N03", Count = 2 } },
            Trigger = ReinforcementTrigger.ObjectiveReached, TriggerArg = "Wymagane", AnnounceSeconds = 20, PaidWood = 480, PaidSilver = 200,
            Source = new SourceNote { Status = SourceStatus.GameRule, Layer = WorldLayer.GameRule, Locator = "docs/MAPY_I_SCENARIUSZE.md §4 K03 (fala próbna testu)" },
        };

        static GameCatalog WithAWave()
        {
            var catalog = GameCatalog.Create();
            catalog.Reinforcements.Add(SoundWave());
            return catalog;
        }

        /// <summary>Every way to change one public field of a definition, however deep: the path to it and the change.</summary>
        static IEnumerable<(string path, Action change)> Changes(object target, string path)
        {
            foreach (var field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                var type = field.FieldType;
                var value = field.GetValue(target);
                var name = path + "." + field.Name;
                void Set(object changed) => field.SetValue(target, changed);
                if (type == typeof(string)) yield return (name, () => Set((string)value + "~"));
                else if (type == typeof(int)) yield return (name, () => Set((int)value + 1));
                else if (type == typeof(long)) yield return (name, () => Set((long)value + 1));
                else if (type == typeof(double)) yield return (name, () => Set((double)value + 0.5));
                else if (type == typeof(bool)) yield return (name, () => Set(!(bool)value));
                else if (type.IsEnum) yield return (name, () => Set(Enum.ToObject(type, Convert.ToInt32(value) ^ 1)));
                else if (type.IsArray)
                {
                    var element = type.GetElementType();
                    var array = (Array)value;
                    object added = element == typeof(string) ? "~" : element == typeof(Parameter) ? new Parameter("~", 1)
                        : element == typeof(ScenarioSide) ? new ScenarioSide { Slot = 2 } : element == typeof(ManifestUnit) ? new ManifestUnit { VariantId = "N04", Count = 1 }
                        : element == typeof(Predicate) ? Predicate.Leaf(PredicateKind.FlagshipAlive) : Activator.CreateInstance(element);
                    yield return (name + "+", () =>
                    {
                        var longer = Array.CreateInstance(element, array.Length + 1);
                        Array.Copy(array, longer, array.Length);
                        longer.SetValue(added, array.Length);
                        Set(longer);
                    });
                    if (array.Length > 0 && element.IsClass && element != typeof(string))
                        foreach (var inner in Changes(array.GetValue(0), name + "[0]")) yield return inner;
                }
                else if (type == typeof(DateWindow) && value == null) yield return (name, () => Set(new DateWindow { EarliestYearBc = 330, LatestYearBc = 301 }));
                else if (type.IsClass && value != null)
                    foreach (var inner in Changes(value, name)) yield return inner;
                else Assert.Fail($"{name}: a field of type {type.Name} the test cannot change; teach it, and the canonical form.");
            }
        }

        [Test]
        public void Identity_ChangingAnyFieldOfAnyDefinitionChangesTheHash()
        {
            // The hash must feel every field: a field left out of the canonical form would let two different catalogues
            // pass as one. Each public field of the first and last definition of every kind is changed alone, in a
            // catalogue of its own, and the hash must differ.
            var hash = WithAWave().Hash();
            Assert.That(hash, Is.Not.EqualTo(GameCatalog.Default.Hash()), "A wave added is a different catalogue.");
            var names = Roots(WithAWave()).SelectMany(root => Changes(root.item, root.name)).Select(change => change.path).ToList();
            Assert.That(names.Count, Is.GreaterThan(500), "The fields of every kind of definition are walked.");
            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count));
            for (var i = 0; i < names.Count; i++)
            {
                var catalog = WithAWave();
                var change = Roots(catalog).SelectMany(root => Changes(root.item, root.name)).ElementAt(i);
                Assert.That(change.path, Is.EqualTo(names[i]));
                change.change();
                Assert.That(catalog.Hash(), Is.Not.EqualTo(hash), $"{change.path}: changed, and the hash is the same.");
            }
            // And the order of the definitions is part of the content.
            var swapped = WithAWave();
            (swapped.Heroes[0], swapped.Heroes[1]) = (swapped.Heroes[1], swapped.Heroes[0]);
            Assert.That(swapped.Hash(), Is.Not.EqualTo(hash));
        }
    }
}
