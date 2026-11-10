using System;
using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The stages a structure goes through as its hull falls in the game (docs/art/DESTRUCTION.md). The game decides the
    /// hull; these stages only show it: a stage is never a cause, and nothing shown here feeds back into the game.
    /// </summary>
    public enum DestructionStage { Intact, SurfaceDamage, LocalFailure, PartialCollapse, WreckOrRuin, Settled }

    /// <summary>What an element does in the structure it belongs to.</summary>
    public enum ElementRole { Foundation, LoadBearing, Beam, Wall, Roof, Decor }

    /// <summary>The stuff an element is made of: how it breaks and what comes off it depend on it.</summary>
    public enum BuildMaterial { Stone, StuccoedStone, Mudbrick, Timber, Ceramic }

    /// <summary>How an element leaves its structure once it or what holds it has failed.</summary>
    public enum DetachMethod
    {
        /// <summary>It stays: the steps under a building.</summary>
        Fixed,
        /// <summary>A column breaks at a bed joint and what stands above tips over; its drums come apart where they land.</summary>
        Topple,
        /// <summary>A block loses what it rests on and tips off it to the ground.</summary>
        Drop,
        /// <summary>Sun-dried brick slumps into its own mound, leaving a ragged stump.</summary>
        Crumble,
        /// <summary>A bay of roof swings down about what still holds it; its tiles break and slide.</summary>
        CaveIn,
    }

    /// <summary>One part of a structure: a drum, a block, a stretch of wall, a bay of roof.</summary>
    public sealed class StructureElement
    {
        public int Index;
        /// <summary>A name that stays the same from build to build: the record of what fell keeps it, not the index.</summary>
        public string Id;
        public ElementRole Role;
        public BuildMaterial Material;
        public DetachMethod Detach;
        /// <summary>The joint it is held by, as built (a bed joint with dowels, two capitals under a block).</summary>
        public string Joint;
        /// <summary>Every one of these must stand for this one to stand.</summary>
        public int[] Supports = Array.Empty<int>();
        /// <summary>It may fail when struck itself (a column at its weak joint, a wall, a bay of roof); the rest fall only with what holds them.</summary>
        public bool Primary;
        /// <summary>The box it fills in the structure's frame: its simplified collider, to find what a shot struck and where scars go.</summary>
        public Bounds Box;
        /// <summary>The column a drum or capital belongs to, else -1.</summary>
        public int Column = -1;
        /// <summary>The way it falls when nothing pushes it otherwise: out of the building, never into its middle.</summary>
        public Vector3 Outward;
    }

    /// <summary>
    /// A structure as elements resting on one another: what holds what. When an element fails, everything that rests on it
    /// fails with it, as far as it goes (Cascade). Validate checks that every element rests, at last, on a foundation.
    /// </summary>
    public sealed class StructureGraph
    {
        public readonly List<StructureElement> Elements = new List<StructureElement>();
        readonly Dictionary<string, int> index = new Dictionary<string, int>();

        public int Count => Elements.Count;
        public StructureElement this[int i] => Elements[i];

        public StructureElement Add(string id, ElementRole role, BuildMaterial material, DetachMethod detach, Bounds box, Vector3 outward, string joint, params int[] supports)
        {
            if (index.ContainsKey(id)) throw new ArgumentException($"The element {id} is there already.");
            var element = new StructureElement
            {
                Index = Elements.Count, Id = id, Role = role, Material = material, Detach = detach, Box = box, Outward = outward, Joint = joint,
                Supports = supports ?? Array.Empty<int>(),
            };
            Elements.Add(element);
            index[id] = element.Index;
            return element;
        }

        public int IndexOf(string id) => id != null && index.TryGetValue(id, out var found) ? found : -1;

        /// <summary>
        /// Marks as failed everything that rests on a failed element, directly or through others. Depth (if given) gets how
        /// many steps of support away from a failure each element went: 0 for those failed in their own right.
        /// </summary>
        public void Cascade(bool[] failed, int[] depth = null)
        {
            if (depth != null)
                for (var i = 0; i < Count; i++) depth[i] = failed[i] ? 0 : -1;
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var element in Elements)
                {
                    if (failed[element.Index]) continue;
                    var deepest = -1;
                    foreach (var support in element.Supports)
                        if (failed[support]) deepest = Mathf.Max(deepest, depth != null ? depth[support] : 0);
                    if (deepest < 0) continue;
                    failed[element.Index] = true;
                    if (depth != null) depth[element.Index] = deepest + 1;
                    changed = true;
                }
            }
        }

        /// <summary>What is wrong with the graph: supports that point nowhere, loops, elements resting on nothing.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var state = new int[Count];
            bool Grounded(int i)
            {
                if (state[i] == 2) return true;
                if (state[i] == 1) { problems.Add($"{Elements[i].Id}: rests on itself through others"); return false; }
                state[i] = 1;
                var element = Elements[i];
                var ok = element.Role == ElementRole.Foundation;
                if (!ok && element.Supports.Length == 0) problems.Add($"{element.Id}: rests on nothing");
                foreach (var support in element.Supports)
                {
                    if (support < 0 || support >= Count) { problems.Add($"{element.Id}: a support that is not there"); continue; }
                    if (Grounded(support)) ok = true;
                }
                state[i] = 2;
                return ok;
            }
            for (var i = 0; i < Count; i++)
            {
                if (state[i] == 0) Grounded(i);
                if (Elements[i].Detach == DetachMethod.Fixed && Elements[i].Role != ElementRole.Foundation) problems.Add($"{Elements[i].Id}: fixed but not a foundation");
                if (Elements[i].Role == ElementRole.Foundation && Elements[i].Detach != DetachMethod.Fixed) problems.Add($"{Elements[i].Id}: a foundation that can fall");
            }
            return problems;
        }
    }

    /// <summary>
    /// How much of a structure is shown broken at a share of its hull lost, read from the game's hull alone: the same
    /// hull always gives the same stage and the same number of failures, whichever shots took it there.
    /// </summary>
    public static class DamagePlan
    {
        /// <summary>The shares of hull lost from which scars show, single elements fail, and whole parts come down.</summary>
        public const float SurfaceFrom = 0.08f, LocalFrom = 0.3f, PartialFrom = 0.6f;

        /// <summary>The most scars a structure shows at once.</summary>
        public const int MaxScars = 18;

        /// <summary>The share of hull lost, 0 for a whole structure and 1 for one at no hull.</summary>
        public static float Damage(double hull, double maxHull) => maxHull <= 0 ? 1f : Mathf.Clamp01(1f - (float)(hull / maxHull));

        public static DestructionStage StageOf(float damage, bool fallen, bool settled)
        {
            if (fallen) return settled ? DestructionStage.Settled : DestructionStage.WreckOrRuin;
            if (damage >= PartialFrom) return DestructionStage.PartialCollapse;
            if (damage >= LocalFrom) return DestructionStage.LocalFailure;
            if (damage >= SurfaceFrom) return DestructionStage.SurfaceDamage;
            return DestructionStage.Intact;
        }

        /// <summary>
        /// How many elements fail in their own right: none before the local failures, one to three of them, four to nine
        /// once the structure partly collapses, and all of them when it falls; never more than it has.
        /// </summary>
        public static int Failures(float damage, bool fallen, int candidates)
        {
            if (fallen) return candidates;
            int count;
            if (damage < LocalFrom) count = 0;
            else if (damage < PartialFrom) count = 1 + Mathf.Min(2, (int)((damage - LocalFrom) / (PartialFrom - LocalFrom) * 3f));
            else count = 4 + Mathf.Min(5, (int)((damage - PartialFrom) / (1f - PartialFrom) * 6f));
            return Mathf.Clamp(count, 0, candidates);
        }

        /// <summary>How many scars show: none on a sound structure, then more as the hull goes, up to MaxScars.</summary>
        public static int Scars(float damage) => damage < SurfaceFrom ? 0 : Mathf.Min(MaxScars, 2 + (int)(damage * 18f));
    }

    /// <summary>
    /// Fixed numbers from names and ids for the look of destruction (FNV-1a, then a SplitMix64 step): never the game's
    /// random, Unity's random, instance ids, the frame or the clock, so the same structure always breaks the same way.
    /// </summary>
    public static class DestructionHash
    {
        public static uint Of(string text, int salt = 0)
        {
            var hash = 2166136261u;
            unchecked
            {
                foreach (var c in text ?? "")
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                hash ^= (uint)salt;
                hash *= 16777619u;
            }
            return hash;
        }

        public static uint Mix(uint a, int b)
        {
            unchecked
            {
                var z = ((ulong)a << 32 | (uint)b) + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return (uint)(z ^ (z >> 31));
            }
        }

        /// <summary>A number in [0, 1) from a hash and a draw.</summary>
        public static float Unit(uint hash, int draw) => (Mix(hash, draw) >> 8) / 16777216f;

        /// <summary>A number between min and max from a hash and a draw.</summary>
        public static float Range(uint hash, int draw, float min, float max) => min + Unit(hash, draw) * (max - min);
    }
}
