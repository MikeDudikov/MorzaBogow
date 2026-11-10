using System;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// Periods of the Aegean the world can show. Bounds are conventional, ±25–50 years, and differ by region
    /// (Tartaron 2008 tab. 1; Toffolo et al. 2013; Weiberg et al. 2019 tab. 1). Crete uses Subminoan and its own
    /// phase names (Wallace 2010).
    /// </summary>
    public enum Era
    {
        /// <summary>Post-palatial Late Helladic IIIC, about 1190–1070 BC: the project's earlier anchor (profile E1).</summary>
        PostPalatial,
        /// <summary>Submycenaean/Subminoan to Protogeometric, about 1070–900 BC (cut near 1025).</summary>
        EarlyIron,
        /// <summary>Geometric, about 900–700 BC.</summary>
        Geometric,
        /// <summary>Archaic, 700–480 BC.</summary>
        Archaic,
        /// <summary>Classical 5th century, 480–400 BC.</summary>
        Classical,
        /// <summary>
        /// Late Classical 4th century, 400–323 BC (profile E6): the project's anchor since the user's decision of
        /// 2026-11-10 (docs/art/HistoricalConstraints.md). Appended last so serialized values of the others keep.
        /// </summary>
        LateClassical,
    }

    /// <summary>
    /// Regions with different rock and vegetation. Pinus brutia grows on Crete and in the east Aegean, P. halepensis
    /// on the mainland (JRC Atlas p. 122); wild spreading cypress on Crete and in the Dodecanese (JRC p. 88–89);
    /// Cycladic marble and schist (Desruelles 2009); Laurion marble–schist contacts (thorikos.be).
    /// </summary>
    public enum Region
    {
        /// <summary>Cyclades: marble, schist, granite.</summary>
        Cyclades,
        /// <summary>Crete: limestone karst, P. brutia, wild cypress, plane trees by water.</summary>
        Crete,
        /// <summary>East Aegean islands: P. brutia, wild cypress (Samos, Rhodes, Kos).</summary>
        EastAegean,
        /// <summary>Southern mainland and Attica: limestone, P. halepensis, Laurion.</summary>
        SouthMainland,
        /// <summary>Volcanic islands (Thera, Melos): no Kameni islands before 197 BC (Strabo 1.3.16; Forsyth 1992).</summary>
        Volcanic,
    }

    /// <summary>Land use the presentation draws; never a rule of the game.</summary>
    public enum LandUse { Natural, Farmed, Harbour, Mining, Workshop }

    /// <summary>
    /// The time and region the environment is drawn for. A presentation setting, not a scenario: the default is the
    /// project's anchor, Greece of the 4th century BC, in Attica (Piraeus, Zea, Laurion) on the southern mainland.
    /// </summary>
    [Serializable]
    public struct EnvironmentProfile
    {
        public Era Era;
        public Region Region;

        public static EnvironmentProfile Default => new EnvironmentProfile { Era = Era.LateClassical, Region = Region.SouthMainland };

        /// <summary>Years BC the era spans (start, end), conventional bounds.</summary>
        public static (int from, int to) Years(Era era)
        {
            switch (era)
            {
                case Era.PostPalatial: return (1190, 1070);
                case Era.EarlyIron: return (1070, 900);
                case Era.Geometric: return (900, 700);
                case Era.Archaic: return (700, 480);
                case Era.Classical: return (480, 400);
                default: return (400, 323);
            }
        }

        /// <summary>
        /// Share of cultivated olives among the olives of a settled islet; the rest are wild ones of the maquis. Olive
        /// pollen in Greece rises from LH III and markedly after about 1050 BC (Weiberg et al. 2019), so the 12th
        /// century shows more wild trees than later eras (interpretation; the shares themselves are the game's).
        /// </summary>
        public float CultivatedOliveShare => Era == Era.PostPalatial ? 0.7f : Era == Era.EarlyIron ? 0.8f : 0.9f;

        /// <summary>The pine of the region: brutia on Crete and in the east Aegean, halepensis elsewhere.</summary>
        public bool BrutiaPine => Region == Region.Crete || Region == Region.EastAegean;

        /// <summary>Iron tools from the Early Iron Age on; bronze before (shipwrights, miners).</summary>
        public bool IronTools => Era >= Era.EarlyIron;
    }

    /// <summary>Deterministic numbers from stable ids (island, tree, station), never from gameplay random numbers.</summary>
    public static class StableHash
    {
        public static uint Mix(uint a, uint b)
        {
            var h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u + (a << 6) + (a >> 2));
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }

        public static uint Mix(uint a, uint b, uint c) => Mix(Mix(a, b), c);

        /// <summary>A number in [0, 1) from a hash.</summary>
        public static float Unit(uint hash) => (hash >> 8) * (1f / (1 << 24));

        /// <summary>A small generator over a hash sequence: Next(i) is fixed for (seed, i).</summary>
        public struct Sequence
        {
            readonly uint seed;
            uint index;

            public Sequence(uint seed)
            {
                this.seed = seed;
                index = 0;
            }

            public float Next() => Unit(Mix(seed, index++));

            public float Range(float min, float max) => min + (max - min) * Next();

            public int Range(int min, int maxExclusive) => min + (int)(Next() * (maxExclusive - min)) % Math.Max(1, maxExclusive - min);
        }
    }
}
