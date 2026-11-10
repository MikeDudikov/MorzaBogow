using System.Collections.Generic;
using System.Linq;
using System.Text;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Composes a ship's look from its id, class and the side that built it. The same id always gives the same look,
    /// whatever order ships are made in; nothing here touches the game's random numbers, Unity's, instance ids, time or
    /// frames. The seed is FNV-1a of profile, class and id; each aspect draws from its own SplitMix64 stream of it, so a
    /// change to one list of the library does not shift another's choices. The workshop and the finish set the ranges
    /// everything else is chosen from, so a ship's paints, wood, cloth, marks and gear belong together.
    /// </summary>
    public static class ShipLookComposer
    {
        /// <summary>The class's look without variants: the scene's paints, the harbour yard's zigzag, no marks or gear.</summary>
        public static ShipLook Plain(int shipId, ShipClass shipClass, int builtBy) => new ShipLook
        {
            ShipId = shipId, Profile = ShipLookLibrary.Plain, Class = shipClass.ToString(), BuiltBy = builtBy, Band = "zigzag",
        };

        public static ShipLook Compose(int shipId, ShipClass shipClass, int builtBy, string profile = ShipLookLibrary.Aegean)
        {
            if (profile == ShipLookLibrary.Plain) return Plain(shipId, shipClass, builtBy);
            var seed = Hash($"{profile}|{shipClass}|{shipId}");
            var look = new ShipLook
            {
                ShipId = shipId, Profile = profile, Seed = (uint)(seed ^ (seed >> 32)), Class = shipClass.ToString(), BuiltBy = builtBy,
            };
            var family = ShipLookLibrary.FamilyOf(Weighted(Stream(seed, "family"), ShipLookLibrary.WorkshopsOf(builtBy)));
            var finish = ShipLookLibrary.FinishOf(Weighted(Stream(seed, "finish"), ShipLookLibrary.Finishes.Select(f => (f.Id, f.Weight)).ToArray()));
            look.Family = family.Id;
            look.Finish = finish.Id;
            look.Pitch = Pick(Stream(seed, "pitch"), family.Pitch[finish.Id]);
            look.Ochre = Pick(Stream(seed, "ochre"), family.Ochre[finish.Id]);
            look.White = Pick(Stream(seed, "white"), family.White);
            look.Band = Weighted(Stream(seed, "band"), family.Bands);
            (look.Wood, look.Grain) = Pick(Stream(seed, "wood"), finish.Wood);
            look.Rope = Pick(Stream(seed, "rope"), finish.Rope);
            look.Linen = Pick(Stream(seed, "linen"), finish.Linen);
            var cloths = family.Canvas.Concat(finish.ExtraCanvas).ToArray();
            var cloth = Stream(seed, "canvas");
            look.Canvas = Pick(cloth, cloths);
            // Patches are cut from another bolt of the same dye: a different cloth than the sail's own.
            look.PatchCanvas = Pick(cloth, ShipLookLibrary.Tones.Where(t => t.Paint == GreekMaterial.Sail && t.Id != look.Canvas).Select(t => t.Id).ToArray());
            ComposePatches(look, finish, Stream(seed, "patches"));
            ComposeMarks(look, shipClass, finish, Stream(seed, "marks"));
            ComposeGear(look, shipClass, Stream(seed, "gear"));
            return look;
        }

        static void ComposePatches(ShipLook look, ShipLookLibrary.Finish finish, LookRandom random)
        {
            var count = random.Range(finish.PatchesMin, finish.PatchesMax);
            for (var attempt = 0; look.Patches.Count < count && attempt < 24; attempt++)
            {
                var patch = new LookPatch { U = random.Between(0.14f, 0.86f), V = random.Between(0.24f, 0.84f), Width = random.Between(0.07f, 0.13f), Height = random.Between(0.06f, 0.12f) };
                if (look.Patches.Any(other => Mathf.Abs(other.U - patch.U) < (other.Width + patch.Width) * 0.5f + 0.03f
                                              && Mathf.Abs(other.V - patch.V) < (other.Height + patch.Height) * 0.5f + 0.03f)) continue;
                look.Patches.Add(patch);
            }
        }

        static void ComposeMarks(ShipLook look, ShipClass shipClass, ShipLookLibrary.Finish finish, LookRandom random)
        {
            var f = GreekShips.Form(shipClass);
            var count = random.Range(finish.MarksMin, finish.MarksMax);
            for (var attempt = 0; look.Marks.Count < count && attempt < 48; attempt++)
            {
                // Merchantmen show wood above the water, so their marks are planks put in; galleys also show pitch worn
                // through at the bow (beaching) and the quarters (the steering oars).
                var repair = f.WoodenTopsides || random.Unit() < 0.65;
                var mark = new LookMark { Kind = repair ? "repair" : "worn", Side = random.Unit() < 0.5 ? -1 : 1 };
                if (repair)
                {
                    mark.S = random.Between(0.24f, 0.8f);
                    mark.Length = random.Between(0.9f, 1.8f);
                    mark.Height = random.Between(0.16f, 0.36f);
                    mark.Angle = random.Between(-2f, 2f);
                    mark.Y = random.Between(0.3f, Mathf.Max(0.32f, GreekShips.Sheer(f, mark.S) * 0.72f - mark.Height * 0.5f));
                }
                else
                {
                    mark.S = random.Unit() < 0.6 ? random.Between(0.74f, 0.82f) : random.Between(0.16f, 0.24f);
                    mark.Length = random.Between(0.6f, 1.3f);
                    mark.Height = random.Between(0.25f, 0.45f);
                    mark.Angle = random.Between(-8f, 8f);
                    mark.Y = random.Between(0.25f, 0.55f);
                }
                if (!Fits(f, mark) || look.Marks.Any(other => Overlap(f, other, mark))) continue;
                look.Marks.Add(mark);
            }
        }

        /// <summary>A mark lies on the hull, above the waterline, below the painted wale and away from the bow's red cheeks.</summary>
        public static bool Fits(GreekShipForm f, LookMark mark)
        {
            var span = GreekShips.Z(f, 1f) - GreekShips.Z(f, 0f);
            var half = mark.Length * 0.5f / span;
            if (mark.S - half < 0.1f || mark.S + half > 0.83f) return false;
            foreach (var s in new[] { mark.S - half, mark.S + half })
            foreach (var y in new[] { mark.Y - mark.Height * 0.5f, mark.Y + mark.Height * 0.5f })
            {
                if (y < 0.15f || y > GreekShips.Sheer(f, s) * 0.78f) return false;
                if (!GreekShips.SurfacePoint(f, s, y, mark.Side, out _, out _)) return false;
            }
            return true;
        }

        static bool Overlap(GreekShipForm f, LookMark a, LookMark b)
        {
            if (a.Side != b.Side) return false;
            var span = GreekShips.Z(f, 1f) - GreekShips.Z(f, 0f);
            return Mathf.Abs(a.S - b.S) * span < (a.Length + b.Length) * 0.5f + 0.4f && Mathf.Abs(a.Y - b.Y) < (a.Height + b.Height) * 0.5f + 0.15f;
        }

        static void ComposeGear(ShipLook look, ShipClass shipClass, LookRandom random)
        {
            var (min, max) = ShipLookLibrary.GearCount(shipClass);
            var count = random.Range(min, max);
            var places = ShipLookLibrary.PlacesOf(shipClass).ToList();
            // The ship's own order of its places; each takes an item that may stand there, is not yet aboard and leaves room.
            for (var i = places.Count - 1; i > 0; i--)
            {
                var j = random.Range(0, i);
                (places[i], places[j]) = (places[j], places[i]);
            }
            foreach (var place in places)
            {
                if (look.Gear.Count >= count) break;
                var items = ShipLookLibrary.Items.Where(item => (shipClass != ShipClass.Cutter || item.OnCutter)
                                                                && item.Places.Contains(place.Kind)
                                                                && look.Gear.All(g => g.Item != item.Id)
                                                                && ShipLookLibrary.Blocked(shipClass, item, place) == null).ToList();
                if (items.Count == 0) continue;
                var chosen = items[random.Range(0, items.Count - 1)];
                if (look.Gear.Any(g => Crowds(shipClass, g, chosen, place))) continue;
                look.Gear.Add(new LookGear { Item = chosen.Id, Anchor = place.Id, Yaw = random.Between(-20f, 20f) + (random.Unit() < 0.5 ? 0f : 180f) });
            }
        }

        static bool Crowds(ShipClass shipClass, LookGear placed, ShipLookLibrary.GearItem item, ShipLookLibrary.Place place)
        {
            var other = ShipLookLibrary.PlaceOf(shipClass, placed.Anchor);
            var otherItem = ShipLookLibrary.ItemOf(placed.Item);
            if (other == null || otherItem == null) return false;
            var gap = new Vector2(other.Point.x - place.Point.x, other.Point.z - place.Point.z).magnitude;
            return gap < otherItem.Footprint.radius + item.Footprint.radius + 0.1f;
        }

        // The presentation's own random numbers ---------------------------------------------------------------------------
        /// <summary>FNV-1a, 64 bits, of the text's UTF-8 bytes: the same on every platform and run.</summary>
        public static ulong Hash(string text)
        {
            var hash = 14695981039346656037UL;
            foreach (var value in Encoding.UTF8.GetBytes(text))
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        static LookRandom Stream(ulong seed, string aspect) => new LookRandom(seed ^ Hash(aspect));

        static T Pick<T>(LookRandom random, IReadOnlyList<T> items) => items[random.Range(0, items.Count - 1)];

        static string Weighted(LookRandom random, (string id, int weight)[] items)
        {
            var total = items.Sum(i => i.weight);
            var roll = random.Range(0, total - 1);
            foreach (var (id, weight) in items)
            {
                if (roll < weight) return id;
                roll -= weight;
            }
            return items[items.Length - 1].id;
        }

        /// <summary>SplitMix64: a small generator for the presentation alone, seeded per ship and aspect.</summary>
        sealed class LookRandom
        {
            ulong state;

            public LookRandom(ulong seed) => state = seed;

            ulong Next()
            {
                state += 0x9E3779B97F4A7C15UL;
                var z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }

            public double Unit() => (Next() >> 11) * (1.0 / (1UL << 53));

            /// <summary>A whole number from min to max, both included.</summary>
            public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (ulong)(max - min + 1));

            public float Between(float min, float max) => (float)(min + (max - min) * Unit());
        }
    }
}
