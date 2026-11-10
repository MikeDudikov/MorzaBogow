using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// What the profile "aegean-12c" lets ships of one class differ in, and how far: the workshops of each side with their
    /// bands and paint ranges, the finishes of a hull's history, the tones of every paint, the sail cloths, the marks, and
    /// the gear with the named places on deck it may stand at and the rules that keep it clear of crews, passages, oars,
    /// posts and weapons. The status of each choice (finding or interpretation, with sources) is in
    /// docs/art/ShipVariants.md. Ids here are stable: saved ships refer to them. A new or changed choice that would alter
    /// ships already composed belongs in a new profile version.
    /// </summary>
    public static class ShipLookLibrary
    {
        public const string Aegean = "aegean-12c/1", Plain = "plain";
        public const int MaxMarks = 6, MaxPatches = 3, MaxGear = 4;

        // Tones ----------------------------------------------------------------------------------------------------------
        /// <summary>
        /// A tone of a paint: sRGB colour (for sail cloths, a multiplier of the owner's dye), smoothness, and the texture that
        /// gives it grain or weave with its tiling per UV unit (UV is in metres, the sail's down its height in 0..1).
        /// </summary>
        public sealed class Tone
        {
            public string Id;
            public GreekMaterial Paint;
            public Color Colour;
            public float Smoothness;
            public string Texture;
            public Vector2 Tiling = Vector2.one;
        }

        static Tone T(string id, GreekMaterial paint, float r, float g, float b, float smoothness, string texture = null, float tileU = 1f, float tileV = 1f) =>
            new Tone { Id = id, Paint = paint, Colour = new Color(r, g, b), Smoothness = smoothness, Texture = texture, Tiling = new Vector2(tileU, tileV) };

        /// <summary>Every tone of the profile. Wood tones go with a grain texture chosen by the finish, never freely.</summary>
        public static readonly Tone[] Tones =
        {
            T("pitch.glossy", GreekMaterial.Pitch, 0.070f, 0.058f, 0.050f, 0.62f),
            T("pitch.matte", GreekMaterial.Pitch, 0.085f, 0.074f, 0.064f, 0.28f),
            T("pitch.brown", GreekMaterial.Pitch, 0.115f, 0.078f, 0.052f, 0.45f),
            T("pitch.salt", GreekMaterial.Pitch, 0.105f, 0.100f, 0.094f, 0.22f),
            T("ochre.bright", GreekMaterial.Ochre, 0.66f, 0.24f, 0.12f, 0.30f),
            T("ochre.deep", GreekMaterial.Ochre, 0.52f, 0.17f, 0.09f, 0.32f),
            T("ochre.faded", GreekMaterial.Ochre, 0.66f, 0.38f, 0.27f, 0.20f),
            T("white.lime", GreekMaterial.Paint, 0.93f, 0.91f, 0.84f, 0.20f),
            T("white.cream", GreekMaterial.Paint, 0.87f, 0.81f, 0.66f, 0.22f),
            T("wood.honey", GreekMaterial.Wood, 0.62f, 0.46f, 0.28f, 0.35f),
            T("wood.pale", GreekMaterial.Wood, 0.70f, 0.58f, 0.42f, 0.30f),
            T("wood.brown", GreekMaterial.Wood, 0.48f, 0.34f, 0.21f, 0.38f),
            T("wood.grey", GreekMaterial.Wood, 0.58f, 0.54f, 0.47f, 0.25f),
            T("wood.fresh", GreekMaterial.Wood, 0.80f, 0.64f, 0.44f, 0.30f),
            T("rope.flax", GreekMaterial.Rope, 0.66f, 0.56f, 0.38f, 0.12f, "rope", 4f, 4f),
            T("rope.dark", GreekMaterial.Rope, 0.48f, 0.40f, 0.28f, 0.12f, "rope", 4f, 4f),
            T("linen.natural", GreekMaterial.Linen, 0.84f, 0.79f, 0.68f, 0.15f),
            T("linen.bleached", GreekMaterial.Linen, 0.90f, 0.87f, 0.80f, 0.15f),
            T("wicker", GreekMaterial.Wicker, 0.68f, 0.55f, 0.34f, 0.18f, "wicker", 4f, 4f),
            T("stone.limestone", GreekMaterial.Stone, 0.74f, 0.71f, 0.64f, 0.18f),
            T("terracotta", GreekMaterial.Terracotta, 0.64f, 0.36f, 0.23f, 0.25f),
            // Pitch worn through to the wood: the texture darkens toward the patch's edge to the colour of pitch.
            T("worn.a", GreekMaterial.Wood, 0.62f, 0.46f, 0.28f, 0.30f, "worn_a"),
            T("worn.b", GreekMaterial.Wood, 0.62f, 0.46f, 0.28f, 0.30f, "worn_b"),
            // Sail cloths: the colour multiplies the owner's dye; the texture gives the cloths, their seams and wear.
            T("canvas.narrow.light", GreekMaterial.Sail, 1.12f, 1.12f, 1.12f, 0.15f, "canvas_narrow", 0.25f, 1f),
            T("canvas.narrow.mid", GreekMaterial.Sail, 1f, 1f, 1f, 0.15f, "canvas_narrow", 0.25f, 1f),
            T("canvas.wide.mid", GreekMaterial.Sail, 1f, 1f, 1f, 0.15f, "canvas_wide", 0.25f, 1f),
            T("canvas.wide.dark", GreekMaterial.Sail, 0.86f, 0.86f, 0.86f, 0.15f, "canvas_wide", 0.25f, 1f),
            T("canvas.band.mid", GreekMaterial.Sail, 1f, 1f, 1f, 0.15f, "canvas_band", 0.25f, 1f),
            T("canvas.grimy.dark", GreekMaterial.Sail, 0.9f, 0.9f, 0.9f, 0.15f, "canvas_grimy", 0.25f, 1f),
        };

        /// <summary>The grain textures of the wood, by id; each is tiled once every four metres.</summary>
        public static readonly string[] Grains = { "grain.fine", "grain.straight", "grain.knotty", "grain.weathered" };

        public static string GrainTexture(string grain) => grain?.Replace('.', '_');

        static readonly Dictionary<string, Tone> tones = Tones.ToDictionary(t => t.Id);

        public static Tone ToneOf(string id) => id != null && tones.TryGetValue(id, out var tone) ? tone : null;

        // Workshops and finishes -------------------------------------------------------------------------------------------
        /// <summary>A workshop: the band it paints on the wale and the tones it uses for each finish of a hull.</summary>
        public sealed class Family
        {
            public string Id, Name;
            public (string band, int weight)[] Bands;
            public Dictionary<string, string[]> Pitch, Ochre;
            public string[] White, Canvas;
        }

        static Dictionary<string, string[]> ByFinish(string[] fresh, string[] seasoned, string[] old) =>
            new Dictionary<string, string[]> { ["fresh"] = fresh, ["seasoned"] = seasoned, ["old"] = old };

        public static readonly Family[] Families =
        {
            new Family
            {
                Id = "workshop.harbour", Name = "Stocznia portowa",
                Bands = new[] { ("zigzag", 1) },
                Pitch = ByFinish(new[] { "pitch.glossy" }, new[] { "pitch.glossy", "pitch.matte" }, new[] { "pitch.matte", "pitch.salt" }),
                Ochre = ByFinish(new[] { "ochre.bright" }, new[] { "ochre.bright", "ochre.deep" }, new[] { "ochre.faded" }),
                White = new[] { "white.lime" },
                Canvas = new[] { "canvas.narrow.light", "canvas.narrow.mid", "canvas.band.mid" },
            },
            new Family
            {
                Id = "workshop.island", Name = "Warsztat wyspiarski",
                Bands = new[] { ("wave", 9), ("zigzag", 1) },
                Pitch = ByFinish(new[] { "pitch.brown" }, new[] { "pitch.brown", "pitch.matte" }, new[] { "pitch.salt", "pitch.brown" }),
                Ochre = ByFinish(new[] { "ochre.deep" }, new[] { "ochre.deep" }, new[] { "ochre.faded", "ochre.deep" }),
                White = new[] { "white.cream" },
                Canvas = new[] { "canvas.wide.mid", "canvas.wide.dark", "canvas.band.mid" },
            },
            new Family
            {
                Id = "workshop.modest", Name = "Warsztat skromny",
                Bands = new[] { ("none", 4), ("wave", 1) },
                Pitch = ByFinish(new[] { "pitch.matte" }, new[] { "pitch.matte", "pitch.salt" }, new[] { "pitch.salt" }),
                Ochre = ByFinish(new[] { "ochre.deep" }, new[] { "ochre.faded", "ochre.deep" }, new[] { "ochre.faded" }),
                White = new[] { "white.cream" },
                Canvas = new[] { "canvas.wide.mid", "canvas.narrow.mid", "canvas.grimy.dark" },
            },
        };

        public static Family FamilyOf(string id) => Families.FirstOrDefault(f => f.Id == id);

        /// <summary>The workshops of a side: its own for about three ships in four, the plain workshop for the rest.</summary>
        public static (string family, int weight)[] WorkshopsOf(int side) => side switch
        {
            0 => new[] { ("workshop.harbour", 3), ("workshop.modest", 1) },
            1 => new[] { ("workshop.island", 3), ("workshop.modest", 1) },
            _ => new[] { ("workshop.modest", 1) },
        };

        /// <summary>A hull's history: how many marks and patches it may carry and which wood, rope and linen go with it.</summary>
        public sealed class Finish
        {
            public string Id;
            public int Weight, MarksMin, MarksMax, PatchesMin, PatchesMax;
            public (string wood, string grain)[] Wood;
            public string[] Rope, Linen, ExtraCanvas;
        }

        public static readonly Finish[] Finishes =
        {
            new Finish
            {
                Id = "fresh", Weight = 3, MarksMin = 0, MarksMax = 1, PatchesMin = 0, PatchesMax = 0,
                Wood = new[] { ("wood.honey", "grain.fine"), ("wood.pale", "grain.straight") },
                Rope = new[] { "rope.flax" }, Linen = new[] { "linen.bleached" }, ExtraCanvas = new string[0],
            },
            new Finish
            {
                Id = "seasoned", Weight = 5, MarksMin = 1, MarksMax = 3, PatchesMin = 0, PatchesMax = 1,
                Wood = new[] { ("wood.honey", "grain.straight"), ("wood.brown", "grain.knotty") },
                Rope = new[] { "rope.flax", "rope.dark" }, Linen = new[] { "linen.natural", "linen.bleached" }, ExtraCanvas = new string[0],
            },
            new Finish
            {
                Id = "old", Weight = 2, MarksMin = 3, MarksMax = MaxMarks, PatchesMin = 1, PatchesMax = MaxPatches,
                Wood = new[] { ("wood.brown", "grain.knotty"), ("wood.grey", "grain.weathered") },
                Rope = new[] { "rope.dark" }, Linen = new[] { "linen.natural" }, ExtraCanvas = new[] { "canvas.grimy.dark" },
            },
        };

        public static Finish FinishOf(string id) => Finishes.FirstOrDefault(f => f.Id == id);

        // Gear -------------------------------------------------------------------------------------------------------------
        /// <summary>
        /// Gear a crew keeps on deck. Places: the kinds of named places it may stand at. NearOnly: drawn only up close. Gone
        /// below a part of the hull (washed over the side when the ship is badly holed, back after a repair). Floats: drifts
        /// off when the ship sinks. The room it takes comes from its mesh. None of it is a weapon or armour.
        /// </summary>
        public sealed class GearItem
        {
            public string Id, Mesh;
            public string[] Places;
            public bool NearOnly, Floats, OnCutter = true;
            public float LostBelow;

            (float radius, float height)? footprint;

            /// <summary>The circle on deck it fills whatever its turn, and its height, from its mesh.</summary>
            public (float radius, float height) Footprint
            {
                get
                {
                    if (footprint.HasValue) return footprint.Value;
                    var bounds = GreekShips.Prop(Mesh).Mesh.bounds;
                    var x = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x));
                    var z = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z));
                    footprint = (Mathf.Sqrt(x * x + z * z), bounds.max.y);
                    return footprint.Value;
                }
            }
        }

        public static readonly GearItem[] Items =
        {
            new GearItem { Id = "water-jar", Mesh = "gear.water-jar", Places = new[] { "stern", "gap" }, Floats = true },
            new GearItem { Id = "sacks", Mesh = "gear.sacks", Places = new[] { "stern", "gap", "bow" }, NearOnly = true, Floats = true, LostBelow = 0.3f },
            new GearItem { Id = "basket", Mesh = "gear.basket", Places = new[] { "stern", "gap", "bow" }, NearOnly = true, Floats = true, LostBelow = 0.3f },
            new GearItem { Id = "rope-coil", Mesh = "gear.rope-coil", Places = new[] { "stern", "gap", "bow" }, NearOnly = true },
            new GearItem { Id = "cloth", Mesh = "gear.cloth", Places = new[] { "stern", "gap" }, NearOnly = true, Floats = true, LostBelow = 0.3f, OnCutter = false },
            new GearItem { Id = "anchor-stones", Mesh = "gear.anchor-stones", Places = new[] { "bow" }, OnCutter = false },
        };

        public static GearItem ItemOf(string id) => Items.FirstOrDefault(i => i.Id == id);

        public static (int min, int max) GearCount(ShipClass shipClass) =>
            shipClass == ShipClass.Cutter ? (0, 2) : shipClass == ShipClass.Merchant ? (2, MaxGear) : (1, 3);

        /// <summary>A named place on deck where gear may stand, of a kind (stern, gap, bow), at a point on the deck or platform.</summary>
        public sealed class Place
        {
            public string Id, Kind;
            public Vector3 Point;
        }

        static readonly Dictionary<ShipClass, Place[]> places = new Dictionary<ShipClass, Place[]>();

        /// <summary>The raised platforms of a galley (GreekShips.BuildDeck): stern from s 0.05 to 0.18, bow from 0.82 to 0.95.</summary>
        public const float SternPlatformEnd = 0.18f, BowPlatformStart = 0.82f;

        /// <summary>
        /// The places of a class's ship, port (L) and starboard (P), or on the middle line (S) of an open boat: a galley's
        /// stern platform aft of the helmsman, the deck between each platform and the rowers, and the front of the bow
        /// platform; a merchantman's deck at stern and bow and amidships behind its cargo; a boat's bottom boards at stern and
        /// bow, clear of the rowers and the steering oars.
        /// </summary>
        public static IReadOnlyList<Place> PlacesOf(ShipClass shipClass)
        {
            if (places.TryGetValue(shipClass, out var found)) return found;
            var parts = GreekShips.Parts(shipClass);
            var f = parts.Form;
            var list = new List<Place>();
            void Add(string id, string kind, float s, float x, float y)
            {
                if (x == 0f)
                {
                    list.Add(new Place { Id = $"{id}.S", Kind = kind, Point = new Vector3(0f, y, GreekShips.Z(f, s)) });
                    return;
                }
                foreach (var (suffix, side) in new[] { ("L", -1f), ("P", 1f) })
                    list.Add(new Place { Id = $"{id}.{suffix}", Kind = kind, Point = new Vector3(side * x, y, GreekShips.Z(f, s)) });
            }
            var (firstOar, lastOar) = OarSpan(parts);
            if (f.Platforms > 0f)
            {
                // On the platforms gear keeps to the sides, as far out as the rail band lets it; on the deck, off the gangway.
                var stern = PlatformHalfWidth(f, true);
                var bow = PlatformHalfWidth(f, false);
                Add("rufa.za.sternikiem", "stern", 0.085f, stern * 0.42f, GreekShips.Sheer(f, 0.115f) + f.Platforms * (f.Castles ? 1f : 0.85f));
                var aft = (SternPlatformEnd + firstOar) * 0.5f;
                Add("rufa.przed.ikrion", "gap", aft, GreekShips.HalfWidth(f, aft) * 0.42f, GreekShips.DeckAt(f, aft));
                var fore = (lastOar + BowPlatformStart) * 0.5f;
                Add("dziob.za.ikrion", "gap", fore, GreekShips.HalfWidth(f, fore) * 0.42f, GreekShips.DeckAt(f, fore));
                Add("dziob.przod", "bow", 0.905f, bow * 0.42f, GreekShips.Sheer(f, 0.885f) + f.Platforms);
            }
            else if (shipClass == ShipClass.Merchant)
            {
                Add("rufa", "stern", 0.17f, GreekShips.HalfWidth(f, 0.17f) * 0.32f, GreekShips.DeckAt(f, 0.17f));
                Add("srodek", "gap", 0.48f, GreekShips.HalfWidth(f, 0.48f) * 0.4f, GreekShips.DeckAt(f, 0.48f));
                Add("dziob", "bow", 0.83f, GreekShips.HalfWidth(f, 0.83f) * 0.3f, GreekShips.DeckAt(f, 0.83f));
            }
            else
            {
                // An open boat: on the middle line at either end, between the steering oars and the rowers' thwarts.
                Add("rufa", "stern", 0.18f, 0f, GreekShips.DeckAt(f, 0.18f));
                Add("dziob", "bow", 0.83f, 0f, GreekShips.DeckAt(f, 0.83f));
            }
            found = list.ToArray();
            places[shipClass] = found;
            return found;
        }

        /// <summary>Half the width of a galley's stern or bow platform, as the generator builds it.</summary>
        static float PlatformHalfWidth(GreekShipForm f, bool stern) => GreekShips.HalfWidth(f, stern ? 0.115f : 0.885f) * 0.9f;

        public static Place PlaceOf(ShipClass shipClass, string id) => PlacesOf(shipClass).FirstOrDefault(p => p.Id == id);

        /// <summary>The part of the hull (s, 0 stern to 1 bow) where the rowers sit, from the first to the last thole pin.</summary>
        public static (float first, float last) OarSpan(GreekShipParts parts)
        {
            var f = parts.Form;
            float first = 1f, last = 0f;
            foreach (var pin in parts.OarPins)
            {
                var s = Mathf.InverseLerp(GreekShips.Z(f, 0f), GreekShips.Z(f, 1f), pin.z);
                first = Mathf.Min(first, s);
                last = Mathf.Max(last, s);
            }
            return (first, last);
        }

        /// <summary>
        /// Why a piece of gear may not stand at a place, or null if it may: it must stay on the deck or its platform, off the
        /// stem and stern posts, out of the rowers' benches, the gangway and the band along the rail where crews board, fight
        /// and work, clear of the mast, the steering oars and every weapon post with room for its crew, and low.
        /// </summary>
        public static string Blocked(ShipClass shipClass, GearItem item, Place place, float margin = 0f)
        {
            var parts = GreekShips.Parts(shipClass);
            var f = parts.Form;
            var (radius, height) = item.Footprint;
            radius += margin;
            var p = place.Point;
            var span = GreekShips.Z(f, 1f) - GreekShips.Z(f, 0f);
            var s = (p.z - GreekShips.Z(f, 0f)) / span;
            var reach = radius / span;
            if (height > 1f) return "too tall";
            if (s - reach < 0.04f || s + reach > 0.96f) return "the stem or stern post";
            var onPlatform = f.Platforms > 0f && place.Kind != "gap";
            if (!onPlatform)
            {
                var narrowest = Mathf.Min(GreekShips.HalfWidth(f, s - reach), GreekShips.HalfWidth(f, s + reach));
                if (Mathf.Abs(p.x) + radius > narrowest * 0.9f) return "off the deck";
            }
            // Boarding lines stand at three quarters of the half-beam (ShipVisual.RailNear), crews step off at four fifths.
            if (Mathf.Abs(p.x) + radius > f.HullBeam * 0.5f * 0.55f) return "the rail, where crews board, fight and work";
            // A rower sits and swings his oar about 0.4 m either way of his thole pin.
            var (firstOar, lastOar) = OarSpan(parts);
            if (p.z + radius > GreekShips.Z(f, firstOar) - 0.4f && p.z - radius < GreekShips.Z(f, lastOar) + 0.4f) return "the rowers' benches";
            // The gangway runs along the middle between the platforms, or along the rowers on a ship without platforms.
            var (passFrom, passTo) = f.Platforms > 0f ? (SternPlatformEnd, BowPlatformStart) : (firstOar, lastOar);
            if (s + reach > passFrom && s - reach < passTo && Mathf.Abs(p.x) - radius < 0.35f) return "the gangway along the middle";
            var mast = new Vector2(parts.MastFoot.x, parts.MastFoot.z);
            // The mast's own radius (as the rig builds it) and room to work its halyards.
            if (Vector2.Distance(mast, new Vector2(p.x, p.z)) < radius + 0.012f * f.Length + 0.08f + 0.4f) return "the mast";
            foreach (var side in new[] { -1f, 1f })
            {
                var pin = new Vector2(side * (GreekShips.HalfWidth(f, 0.15f) + 0.15f), GreekShips.Z(f, 0.15f));
                if (Vector2.Distance(pin, new Vector2(p.x, p.z)) < radius + 0.7f) return "the steering oars";
            }
            var slots = ShipCatalog.Get(shipClass).Slots;
            for (var i = 0; i < slots.Length && i < parts.Mounts.Length; i++)
            {
                // The post and 0.8 m around it where its crew works.
                var bounds = GreekShips.Weapon(slots[i]).Mesh.bounds;
                bounds.center += parts.Mounts[i];
                bounds.Expand(new Vector3(1.6f, 0f, 1.6f));
                var nearest = new Vector2(Mathf.Clamp(p.x, bounds.min.x, bounds.max.x), Mathf.Clamp(p.z, bounds.min.z, bounds.max.z));
                if (Vector2.Distance(nearest, new Vector2(p.x, p.z)) < radius && p.y + height > bounds.min.y - 0.5f && p.y < bounds.max.y + 0.5f) return "a weapon post and its crew";
            }
            if (place.Kind != "gap" && f.Platforms > 0f)
            {
                // On a platform it must stay on the platform.
                var (s0, s1) = place.Kind == "stern" ? (0.05f, 0.18f) : (0.82f, 0.95f);
                if (s - reach < s0 || s + reach > s1) return "off its platform";
                if (Mathf.Abs(p.x) + radius > GreekShips.HalfWidth(f, (s0 + s1) * 0.5f) * 0.85f) return "off its platform";
            }
            return null;
        }
    }
}
