using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The materials of one ship's look, from a small shared palette. Each tone of the profile (with its grain or cloth
    /// texture) becomes one material, made once and shared by every ship that has the tone: no material per ship and none
    /// per frame, and the SRP Batcher batches them like any other. A palette material is a clone of the ship paint
    /// material (Resources/ShipLook/ShipPaint, the ship paint Shader Graph) with the tone's colour and smoothness and its
    /// texture's colour, normal and mask maps; what differs ship to ship (wear) is not in the material but in each
    /// renderer's value (ShipSurface), which ShipVisual sets. Without the paint material the scene's material for the
    /// paint is cloned instead, base colour only. The sail takes the owner's dye in the ship's own cloth, so a ship that is
    /// taken keeps its cloth, patches and timber and changes only its colours. The plain look uses the scene's materials
    /// unchanged.
    /// </summary>
    public sealed class ShipPaints
    {
        /// <summary>The ship paint material in Resources/ShipLook, and the suffixes of each texture's normal and mask maps there.</summary>
        public const string PaintMaterial = "ShipPaint", NormalSuffix = "_normal", MaskSuffix = "_mask";
        /// <summary>How much of the ship's wear a paint shows: 1 where its mask says, a little everywhere on a paint without texture.</summary>
        public const string WearStrength = "_WearStrength";
        public const float PlainWearStrength = 0.35f;

        readonly ShipLook look;
        readonly PlaceholderMaterials materials;
        static readonly Dictionary<(Material source, string key), Material> palette = new Dictionary<(Material, string), Material>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static Material paintMaterial;

        /// <summary>
        /// The material every palette material is cloned from, or null when the ship paint is missing or its shader cannot
        /// run here. Loaded again if Resources let it go, since no palette material keeps it.
        /// </summary>
        public static Material PaintBase
        {
            get
            {
                if (paintMaterial == null)
                {
                    var loaded = Resources.Load<Material>("ShipLook/" + PaintMaterial);
                    paintMaterial = loaded != null && loaded.shader.isSupported ? loaded : null;
                }
                return paintMaterial;
            }
        }

        public ShipPaints(ShipLook look, PlaceholderMaterials materials)
        {
            this.look = look;
            this.materials = materials;
        }

        bool Plain => look == null || look.IsPlain;

        /// <summary>Materials the palette has made so far, for all ships together; bounded by the profile's tones.</summary>
        public static int PaletteSize
        {
            get
            {
                var count = 0;
                foreach (var made in palette.Values) if (made != null) count++;
                return count;
            }
        }

        public Material Get(GreekMaterial paint)
        {
            var source = GreekShips.MaterialFor(paint, materials);
            if (Plain || source == null) return source;
            return paint switch
            {
                GreekMaterial.Pitch => Variant(source, look.Pitch, null),
                GreekMaterial.Ochre => Variant(source, look.Ochre, null),
                GreekMaterial.Paint => Variant(source, look.White, null),
                GreekMaterial.Wood => Variant(source, look.Wood, look.Grain),
                GreekMaterial.Rope => Variant(source, look.Rope, null),
                GreekMaterial.Linen => Variant(source, look.Linen, null),
                GreekMaterial.Wicker => Variant(source, "wicker", null),
                GreekMaterial.Stone => Variant(source, "stone.limestone", null),
                GreekMaterial.Terracotta => Variant(source, "terracotta", null),
                _ => source,
            };
        }

        /// <summary>The sail: the owner's dye in the ship's own cloth.</summary>
        public Material Sail(int owner) => Cloth(owner, look?.Canvas);

        /// <summary>The sail's patches: the owner's dye in another cloth.</summary>
        public Material SailPatch(int owner) => Cloth(owner, look?.PatchCanvas);

        Material Cloth(int owner, string toneId)
        {
            var dye = owner == 0 ? materials?.PlayerSail : materials?.EnemySail;
            if (Plain || dye == null) return dye;
            var tone = ShipLookLibrary.ToneOf(toneId);
            if (tone == null) return dye;
            var key = "cloth " + toneId;
            if (palette.TryGetValue((dye, key), out var made) && made != null) return made;
            var colour = dye.HasProperty("_BaseColor") ? dye.GetColor("_BaseColor") : dye.color;
            colour = new Color(Mathf.Clamp01(colour.r * tone.Colour.r), Mathf.Clamp01(colour.g * tone.Colour.g), Mathf.Clamp01(colour.b * tone.Colour.b), colour.a);
            made = Make(dye, key, colour, tone.Smoothness, tone.Texture, tone.Tiling);
            palette[(dye, key)] = made;
            return made;
        }

        /// <summary>Planks put in long ago on a pitched hull: a pitch of another age than the hull's, so they show.</summary>
        public Material RepairPitch => Plain ? Get(GreekMaterial.Pitch) : Tone(GreekMaterial.Pitch, look.Pitch == "pitch.glossy" ? "pitch.matte" : "pitch.glossy", null);

        /// <summary>Planks put in long ago on wooden topsides: timber of another tone.</summary>
        public Material RepairWood => Plain ? Get(GreekMaterial.Wood) : Tone(GreekMaterial.Wood, look.Wood == "wood.brown" ? "wood.honey" : "wood.brown", "grain.straight");

        /// <summary>Pitch worn through to the wood, darkening to pitch at the edge.</summary>
        public Material Worn => Plain ? Get(GreekMaterial.Wood) : Tone(GreekMaterial.Wood, (look.Seed & 1) == 0 ? "worn.a" : "worn.b", null);

        /// <summary>New planks of a repair in this match, not yet pitched.</summary>
        public Material FreshWood => Plain ? Get(GreekMaterial.Wood) : Tone(GreekMaterial.Wood, "wood.fresh", "grain.fine");

        Material Tone(GreekMaterial paint, string toneId, string grain)
        {
            var source = GreekShips.MaterialFor(paint, materials);
            return source == null ? null : Variant(source, toneId, grain);
        }

        /// <summary>One material per submesh: the look's paints, and for the sail the owner's dye in the ship's cloth.</summary>
        public Material[] For(GreekMesh mesh, int owner)
        {
            var list = new Material[mesh.Slots.Length];
            for (var i = 0; i < list.Length; i++) list[i] = mesh.Slots[i] == GreekMaterial.Sail ? Sail(owner) : Get(mesh.Slots[i]);
            return list;
        }

        static Material Variant(Material source, string toneId, string grain)
        {
            var tone = ShipLookLibrary.ToneOf(toneId);
            if (tone == null) return source;
            var key = grain == null ? toneId : toneId + " " + grain;
            if (palette.TryGetValue((source, key), out var made) && made != null) return made;
            // Grain is tiled once every four metres of hull, deck or spar.
            made = grain != null
                ? Make(source, key, tone.Colour, tone.Smoothness, ShipLookLibrary.GrainTexture(grain), new Vector2(0.25f, 0.25f))
                : Make(source, key, tone.Colour, tone.Smoothness, tone.Texture, tone.Tiling);
            palette[(source, key)] = made;
            return made;
        }

        /// <summary>
        /// One palette material: a clone of the ship paint (or, without it, of the scene's material) named after the scene's
        /// material and the tone, so it reads the same in every list. The paint takes the source's metal (none for the
        /// paints the looks vary), the tone's colour and smoothness, and the texture's colour, normal and mask maps.
        /// </summary>
        static Material Make(Material source, string key, Color colour, float smoothness, string texture, Vector2 tiling)
        {
            var basis = PaintBase;
            var made = new Material(basis != null ? basis : source) { name = $"{source.name} [{key}]" };
            if (made.HasProperty("_BaseColor")) made.SetColor("_BaseColor", colour);
            made.color = colour;
            if (made.HasProperty("_Smoothness")) made.SetFloat("_Smoothness", smoothness);
            if (basis != null && made.HasProperty("_Metallic")) made.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
            var map = Texture(texture);
            if (map != null && made.HasProperty("_BaseColorMap"))
            {
                made.SetTexture("_BaseColorMap", map);
                made.SetTextureScale("_BaseColorMap", tiling);
            }
            if (basis != null)
            {
                // The paint always samples its normal and mask maps (no keyword to strip): a tone without texture keeps the
                // flat normal and white mask of the paint material and shows the ship's wear only lightly, all over.
                var normal = map != null ? Texture(texture + NormalSuffix) : null;
                var mask = map != null ? Texture(texture + MaskSuffix) : null;
                if (normal != null) made.SetTexture("_NormalMap", normal);
                if (mask != null) made.SetTexture("_MaskMap", mask);
                made.SetFloat(WearStrength, mask != null ? 1f : PlainWearStrength);
            }
            return made;
        }

        static Texture2D Texture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!textures.TryGetValue(name, out var texture) || texture == null) textures[name] = texture = Resources.Load<Texture2D>("ShipLook/" + name);
            return texture;
        }
    }
}
