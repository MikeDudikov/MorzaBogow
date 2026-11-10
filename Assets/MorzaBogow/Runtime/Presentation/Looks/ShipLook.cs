using System;
using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The lasting description of one ship's look, the layers above its type: the workshop that built it (B) and what is its
    /// own (C) — the tones of its pitch, ochre, white and wood, the grain of its timber, its sail cloth and patches, the old
    /// repairs and worn places on its hull and the gear on its deck. Everything is kept by id, never by position in a
    /// library, so a library in another order still draws a saved ship the same. No textures or meshes are stored. The
    /// current state (damage, repairs in this match, the owner's colours) is not part of it: a ship keeps its look when it
    /// is struck, repaired or taken.
    /// </summary>
    [Serializable]
    public sealed class ShipLook
    {
        public int ShipId;
        /// <summary>The library profile, with its version ("aegean-12c/1"), or "plain" for the class's look without variants.</summary>
        public string Profile;
        /// <summary>The presentation's own seed, from the profile, class and id; never the game's random numbers.</summary>
        public uint Seed;
        public string Class;
        /// <summary>The side that built the ship. Its workshop follows that side and stays when the ship is taken.</summary>
        public int BuiltBy;
        public string Family, Finish;
        /// <summary>Tone ids of the hull's paints, the wood and its grain texture, the rope and the linen of the gear.</summary>
        public string Pitch, Ochre, White, Wood, Grain, Rope, Linen;
        /// <summary>The pattern painted on the wale: "zigzag", "wave" or "none".</summary>
        public string Band;
        /// <summary>The sail's cloth (texture and tone) and the cloth of its patches; the dye is always the owner's.</summary>
        public string Canvas, PatchCanvas;
        public List<LookPatch> Patches = new List<LookPatch>();
        public List<LookMark> Marks = new List<LookMark>();
        public List<LookGear> Gear = new List<LookGear>();

        public bool IsPlain => Profile == ShipLookLibrary.Plain;

        public string ToJson() => JsonUtility.ToJson(this);

        public static ShipLook FromJson(string json) => JsonUtility.FromJson<ShipLook>(json);

        /// <summary>Two descriptions draw the same ship.</summary>
        public bool SameAs(ShipLook other) => other != null && ToJson() == other.ToJson();
    }

    /// <summary>A patch sewn on the sail: centre (U across, V down, 0..1) and size as parts of the sail's width and height.</summary>
    [Serializable]
    public sealed class LookPatch
    {
        public float U, V, Width, Height;
    }

    /// <summary>
    /// A mark of the hull's history: "repair" (planks put in long ago, a clean patch in another tone) or "worn" (pitch
    /// worn through to the wood). S along the hull (0 stern, 1 bow), Y metres above the waterline, size in metres.
    /// </summary>
    [Serializable]
    public sealed class LookMark
    {
        public string Kind;
        public float S, Y, Length, Height, Angle;
        public int Side;
    }

    /// <summary>A piece of gear on deck: the item's id, the named place it stands at and its turn in degrees.</summary>
    [Serializable]
    public sealed class LookGear
    {
        public string Item, Anchor;
        public float Yaw;
    }
}
