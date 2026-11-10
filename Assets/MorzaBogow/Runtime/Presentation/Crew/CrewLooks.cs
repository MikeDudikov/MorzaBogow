using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// A man's look from his lasting identity alone (his ship's id and his number aboard, CrewRoster), by a fixed integer
    /// hash: the same man looks the same after a load and in a replay, and nothing depends on instance ids or on the
    /// game's random numbers. What he wears follows his role (docs/art/CrewRoles.json): rowers and sailors in undyed
    /// linen or wool, often bare to the waist (a reasoned guess, no source shows them clearly); warriors in a tunic of
    /// their side's muted dye (a game convention for reading the sides), with the helmets and shields of LH IIIC paintings;
    /// a leader alone in a bronze corslet and greaves with a sword. Height 1.60 to 1.72 m around 1.66 m (Koukli et al. 2023).
    /// </summary>
    public static class CrewLooks
    {
        /// <summary>A well mixed 32-bit hash of the inputs (a murmur-style finaliser).</summary>
        public static uint Hash(int a, int b, int salt)
        {
            unchecked
            {
                var h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>A number in [0, 1) from the man's identity and a salt for each trait.</summary>
        public static float Unit(int ship, int man, int salt) => (Hash(ship, man, salt) & 0xFFFFFF) / (float)0x1000000;

        static int Pick(float u, params float[] weights)
        {
            var total = 0f;
            foreach (var w in weights) total += w;
            var x = u * total;
            for (var i = 0; i < weights.Length; i++)
            {
                if (x < weights[i]) return i;
                x -= weights[i];
            }
            return weights.Length - 1;
        }

        static readonly CrewPalette.Cell[] Skins = { CrewPalette.Cell.Skin0, CrewPalette.Cell.Skin1, CrewPalette.Cell.Skin2, CrewPalette.Cell.Skin3 };
        static readonly CrewPalette.Cell[] Hairs = { CrewPalette.Cell.Hair0, CrewPalette.Cell.Hair1, CrewPalette.Cell.Hair2 };
        static readonly CrewPalette.Cell[] Undyed = { CrewPalette.Cell.Linen, CrewPalette.Cell.Wool, CrewPalette.Cell.Brown, CrewPalette.Cell.LinenWorn, CrewPalette.Cell.WoolWorn, CrewPalette.Cell.Bleached };

        /// <summary>The side's dye: muted navy for the player, dark red for the other side; worn by sun and salt on some.</summary>
        public static CrewPalette.Cell SideCloth(int owner, bool worn) =>
            owner == 0 ? (worn ? CrewPalette.Cell.NavyWorn : CrewPalette.Cell.Navy) : (worn ? CrewPalette.Cell.MadderWorn : CrewPalette.Cell.Madder);

        public static CrewPalette.Cell SidePaint(int owner) => owner == 0 ? CrewPalette.Cell.ShieldPaintNavy : CrewPalette.Cell.ShieldPaintMadder;

        /// <summary>The look of man <paramref name="man"/> of ship <paramref name="ship"/>; a fighter (by trade) at the bow dresses as a warrior.</summary>
        public static CrewLook For(int ship, int man, CrewRole role, int owner) => For(ship, man, role, owner, CrewRoster.TradeOf(man) != CrewTrade.Sailor);

        public static CrewLook For(int ship, int man, CrewRole role, int owner, bool fighter)
        {
            if (role == CrewRole.Lookout && fighter) role = CrewRole.Marine;
            float U(int salt) => Unit(ship, man, salt);
            // Height: a sum of three draws gives a bell around 1.66 m, held to 1.60–1.72 m.
            var height = Mathf.Clamp(1.66f + (U(1) + U(2) + U(3) - 1.5f) * 0.07f, 1.60f, 1.72f);
            var look = new CrewLook
            {
                Height = height,
                Girth = Mathf.Lerp(0.93f, 1.08f, U(4)),
                Face = Pick(U(5), 1f, 1f, 1f),
                HairStyle = Pick(U(6), 4f, 4f, 2f),
                Hair = Hairs[Pick(U(7), 6f, 3f, 1f)],
                Beard = Pick(U(8), 3f, 4f, 3f),
                Skin = Skins[role == CrewRole.Rower || role == CrewRole.SailHand || role == CrewRole.LineHand || role == CrewRole.BowHand ? Pick(U(9), 1f, 3f, 4f, 2f) : Pick(U(9), 2f, 4f, 3f, 1f)],
            };
            var worn = U(10) < 0.4f;
            switch (role)
            {
                case CrewRole.Commander:
                    look.Dress = 2;
                    look.Cloth = SideCloth(owner, false);
                    look.Trim = CrewPalette.Cell.Bronze;
                    look.Helmet = 2;
                    look.Greaves = true;
                    look.Shield = 1;
                    look.Beard = 2;
                    break;
                case CrewRole.Marine:
                case CrewRole.WarriorRower:
                case CrewRole.MachineCrew:
                case CrewRole.BankJavelin:
                    look.Dress = 1;
                    look.Cloth = SideCloth(owner, worn);
                    look.Trim = CrewPalette.Cell.Leather;
                    look.Helmet = role == CrewRole.MachineCrew && U(12) < 0.5f ? 4 : Pick(U(12), 55f, 10f, 25f, 10f) + 1;
                    look.Shield = role == CrewRole.MachineCrew ? 0 : Pick(U(13), 35f, 40f, 25f) + 1;
                    look.Beard = Pick(U(8), 2f, 4f, 4f);
                    break;
                case CrewRole.BankArcher:
                    look.Dress = 1;
                    look.Cloth = SideCloth(owner, worn);
                    look.Trim = CrewPalette.Cell.Leather;
                    look.Helmet = U(12) < 0.6f ? 0 : 4;
                    look.Shield = 0;
                    break;
                case CrewRole.Skipper:
                    look.Dress = 1;
                    look.Cloth = U(14) < 0.5f ? CrewPalette.Cell.Bleached : CrewPalette.Cell.Wool;
                    look.Trim = CrewPalette.Cell.Ochre;
                    look.HairStyle = 2;
                    break;
                case CrewRole.Wounded:
                    look.Dress = U(15) < 0.5f ? 0 : 1;
                    look.Cloth = look.Dress == 1 ? SideCloth(owner, true) : Undyed[Pick(U(14), 3f, 3f, 2f, 2f, 2f, 1f)];
                    look.Trim = CrewPalette.Cell.Bandage;
                    break;
                default:
                    // Rowers, the helmsman and sailors: undyed cloth, most bare to the waist at the oar.
                    look.Dress = U(15) < 0.7f ? 0 : 1;
                    look.Cloth = Undyed[Pick(U(14), 3f, 3f, 2f, 2f, 2f, 1f)];
                    look.Trim = look.Cloth;
                    // Like the Kynos steersman, some helmsmen wear the warriors' helmet.
                    look.Helmet = role == CrewRole.Helmsman && U(12) < 0.3f ? 1 : 0;
                    break;
            }
            look.ShieldPaint = U(16) < 0.75f ? SidePaint(owner) : CrewPalette.Cell.Hide;
            return look;
        }
    }
}
