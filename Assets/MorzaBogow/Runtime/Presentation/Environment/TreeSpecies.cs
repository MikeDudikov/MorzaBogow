using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>How sure a claim is: read in a source, inferred from sources, a guess, or a choice made for the game.</summary>
    public enum Confidence { Established, Interpretation, Speculation, GameChoice }

    /// <summary>One claim behind a number or a shape, with the source (a key of docs/art/ReferenceRegister.json) and the place in it where it was read.</summary>
    public readonly struct SourceNote
    {
        public readonly string Claim;
        public readonly string Reference;
        public readonly string Place;
        public readonly Confidence Confidence;

        public SourceNote(string claim, string reference, string place, Confidence confidence)
        {
            Claim = claim;
            Reference = reference;
            Place = place;
            Confidence = confidence;
        }
    }

    /// <summary>
    /// A tree species as the generator and the wind response need it: the form (in metres), the leaves, the wood and
    /// how the crown meets the wind. Every number has a note in <see cref="Sources"/>; a number chosen for the game
    /// (a size kept low so trees never hide hulls) says so. Presentation only.
    /// </summary>
    public sealed class TreeSpecies
    {
        public string Id;
        public string LatinName;
        public string Name;

        // Form, metres and degrees.
        public float HeightMin, HeightMax;
        public float ForkHeightMin, ForkHeightMax;
        public float StemDiameterMin, StemDiameterMax;
        public int StemsMin = 1, StemsMax = 1;
        public int ScaffoldsMin, ScaffoldsMax;
        public int BranchesMin = 3, BranchesMax = 4;
        public int ShootsMin = 2, ShootsMax = 4;
        public float ScaffoldTiltMin, ScaffoldTiltMax;
        public float CrownWidthRatio;
        public float TrunkTwist;
        public float TrunkFluting;

        // Leaves: the leaf itself (for the baked texture) and the sprays of leaves drawn as cards.
        public float LeafLengthMin, LeafLengthMax, LeafWidth;
        public float SprayLength;
        public float SprayDensity;
        public Color LeafTop, LeafUnder;

        // Wood and wind.
        public float WoodDensity;
        public float WoodModulus;
        public float CrownDrag;
        public float ReconfigurationSpeed;
        public float VogelExponent;

        /// <summary>
        /// Broadleaved trees follow a pendulum, f ∝ H^(−1/2); conifers a cantilever, f ∝ dbh/H² (Jackson et al. 2021,
        /// tab. 2: the models that fit their 111 + 89 broadleaved and 631 coniferous trees best).
        /// </summary>
        public bool Pendulum = true;
        public float SwayFrequency;
        public float ReferenceDiameter, ReferenceHeight;
        public float TrunkDamping;
        public float BranchDamping;
        public float FlutterFrequency;
        public float FlutterOnset;

        public SourceNote[] Sources = System.Array.Empty<SourceNote>();

        /// <summary>First sway frequency of a tree of this size, scaled from the reference tree by the species' model.</summary>
        public float FrequencyFor(float stemDiameter, float height)
        {
            var scale = Pendulum
                ? Mathf.Sqrt(ReferenceHeight / Mathf.Max(height, 0.5f))
                : (stemDiameter / ReferenceDiameter) / Mathf.Pow(height / ReferenceHeight, 2f);
            return SwayFrequency * Mathf.Clamp(scale, 0.4f, 2.5f);
        }

        /// <summary>
        /// Effective frontal area over the area in still air: the crown streamlines above the reconfiguration speed,
        /// so drag grows as U^(2+V) instead of U² (Gosselin 2019; Vogel number V below zero).
        /// </summary>
        public float Reconfiguration(float speed) =>
            speed <= ReconfigurationSpeed ? 1f : Mathf.Pow(speed / ReconfigurationSpeed, VogelExponent);

        /// <summary>
        /// The cultivated olive of an Aegean grove: a short thick stem, a few limbs in an open vase, hanging shoots.
        /// Kept at 4.5–7 m, between young pruned orchard trees (3.3–4 m) and old unpruned ones (15–18 m): a tended,
        /// mature tree low enough to stay under the masts moored beside it.
        /// </summary>
        public static readonly TreeSpecies OliveCultivated = new TreeSpecies
        {
            Id = "olea_europaea_cultivated",
            LatinName = "Olea europaea L. (uprawna)",
            Name = "Oliwka uprawna",
            HeightMin = 4.5f, HeightMax = 7f,
            ForkHeightMin = 0.9f, ForkHeightMax = 1.6f,
            StemDiameterMin = 0.3f, StemDiameterMax = 0.6f,
            ScaffoldsMin = 3, ScaffoldsMax = 4,
            ScaffoldTiltMin = 25f, ScaffoldTiltMax = 45f,
            CrownWidthRatio = 0.95f,
            TrunkTwist = 0.5f,
            TrunkFluting = 0.16f,
            LeafLengthMin = 0.03f, LeafLengthMax = 0.07f, LeafWidth = 0.009f,
            SprayLength = 0.8f,
            SprayDensity = 3.5f,
            LeafTop = new Color(0.36f, 0.41f, 0.31f),
            LeafUnder = new Color(0.72f, 0.73f, 0.67f),
            WoodDensity = 1050f,
            WoodModulus = 9e9f,
            CrownDrag = 0.3f,
            ReconfigurationSpeed = 3f,
            VogelExponent = -0.8f,
            SwayFrequency = 1.4f,
            ReferenceDiameter = 0.4f,
            ReferenceHeight = 5.5f,
            TrunkDamping = 0.05f,
            BranchDamping = 0.08f,
            FlutterFrequency = 6f,
            FlutterOnset = 1.35f,
            Sources = OliveSources(cultivated: true),
        };

        /// <summary>The wild olive of the maquis on a dry islet: a many-stemmed shrub or small tree with denser shoots.</summary>
        public static readonly TreeSpecies OliveWild = new TreeSpecies
        {
            Id = "olea_europaea_sylvestris",
            LatinName = "Olea europaea var. sylvestris (Mill.) Lehr",
            Name = "Oliwka dzika",
            HeightMin = 2.5f, HeightMax = 5f,
            ForkHeightMin = 0.4f, ForkHeightMax = 0.9f,
            StemDiameterMin = 0.1f, StemDiameterMax = 0.22f,
            StemsMin = 2, StemsMax = 4,
            ScaffoldsMin = 2, ScaffoldsMax = 3,
            BranchesMin = 2, BranchesMax = 3,
            ShootsMin = 1, ShootsMax = 3,
            ScaffoldTiltMin = 18f, ScaffoldTiltMax = 38f,
            CrownWidthRatio = 1.15f,
            TrunkTwist = 0.3f,
            TrunkFluting = 0.08f,
            LeafLengthMin = 0.03f, LeafLengthMax = 0.05f, LeafWidth = 0.008f,
            SprayLength = 0.65f,
            SprayDensity = 4f,
            LeafTop = new Color(0.33f, 0.39f, 0.29f),
            LeafUnder = new Color(0.68f, 0.70f, 0.63f),
            WoodDensity = 1050f,
            WoodModulus = 9e9f,
            CrownDrag = 0.3f,
            ReconfigurationSpeed = 3f,
            VogelExponent = -0.8f,
            SwayFrequency = 1.4f,
            ReferenceDiameter = 0.4f,
            ReferenceHeight = 5.5f,
            TrunkDamping = 0.05f,
            BranchDamping = 0.08f,
            FlutterFrequency = 6f,
            FlutterOnset = 1.35f,
            Sources = OliveSources(cultivated: false),
        };

        public static IReadOnlyList<TreeSpecies> All { get; } = new[] { OliveCultivated, OliveWild };

        static SourceNote[] OliveSources(bool cultivated)
        {
            var notes = new List<SourceNote>
            {
                new SourceNote("Drzewo zimozielone 8–15 m; krótki, gruby pień, wiele konarów, zwisające pędy; liście srebrzystozielone, skórzaste, naprzeciwległe.", "JRC2016-Olea", "s. 111", Confidence.Established),
                new SourceNote("Liście 3–7 cm × 8–10 mm, ogonek do 10 mm; wierzch szarozielony, nagi; spód gęsto pokryty srebrnymi łuskami tarczowatymi.", "Jeanes1999-FloraVictoria", "strona taksonu Olea europaea subsp. europaea", Confidence.Established),
                new SourceNote("Włoski tarczowate gęste tylko na spodzie dojrzałego liścia.", "Koudounas2015", "abstrakt i wstęp", Confidence.Established),
                new SourceNote("Udział oliwki w pyłkach Grecji rośnie od LH III, wyraźnie po ok. 1050 p.n.e.", "Weiberg2019", "omówienie wyników pyłkowych (strony nie zapisano)", Confidence.Established),
                new SourceNote("Drewno przy wilgotności 12%: gęstość pnia 994–1023, gałęzi 1020–1070 kg/m³; moduł sprężystości pnia 7,4–8,5, gałęzi 9,0–10,5 GPa. Dla drewna świeżego danych brak: użyto wartości przy 12%.", "Mammoliti2024", "tab. 1 i tab. 3", Confidence.Interpretation),
                new SourceNote("Drzewa liściaste kołyszą się jak wahadło, f ∝ H^(−1/2); wolnostojące o wysokości 5–20 m mają ok. 0,4–1,5 Hz. Pomiaru dla oliwki brak: przyjęto 1,4 Hz dla 5,5 m.", "Jackson2021", "tab. 2, rozdz. 3.1, rys. 2a (zakres odczytany z wykresu)", Confidence.Interpretation),
                new SourceNote("Tłumienie wewnętrzne drzew zwykle ζ < 0,05; liście zwiększają tłumienie gałęzi ok. dwukrotnie; przy wymuszeniu pnia oliwki (10–25 Hz) ζ = 0,16–0,30. Przyjęto ζ = 0,05 dla pnia i 0,08 dla konarów, tłumienie oporu powietrza liczone osobno.", "Moore2004; SolaGuirado2022; CastroGarcia2008; Hoshyarmanesh2017", "abstrakty", Confidence.Interpretation),
                new SourceNote("Rekonfiguracja: V od −0,72 do −1,1, −1 dla gęstych struktur porowatych; Cd 0,15–0,22 (liściaste) i 0,22–0,47 (iglaste); powierzchnia czołowa przy 20 m/s spada do 20–37%. Dla oliwki przyjęto V = −0,8, Cd = 0,3.", "Gosselin2019; Vollsinger2005; Rudnicki2004", "tab. 1 (preprint); abstrakty", Confidence.Interpretation),
                new SourceNote("Próg rekonfiguracji: zmiana reżimu powyżej ok. 2 m/s (Prunus ok. 3 m); trzepotanie liści od 1,35 m/s przy 6 ± 1 Hz. Przyjęto U_r = 3 m/s i 6 Hz przez analogię.", "Tadrist2018", "rozdz. 4, tab. 1, zał. B.3", Confidence.Interpretation),
                new SourceNote("Masa liści z pędem na jedną rysowaną gałązkę 0,1 kg (ok. 50 kg listowia na drzewo).", "szacunek", "-", Confidence.Speculation),
            };
            if (cultivated)
            {
                notes.Add(new SourceNote("Sady przycinane: 3,3–3,5 m przy pniu 18–21 cm; 'Picual' w formie wazowej 3,95 m, pień 180 mm, korona 3,63 m; drzewa 70-letnie bez cięcia 15,5–17,6 m przy pniu 67–70 cm.", "JimenezJimenez2015; Ghonimy2025; Mammoliti2024", "tab. 1–2 s. 272; tab. 2; tab. 1", Confidence.Established));
                notes.Add(new SourceNote("Oliwka gaju 4,5–7 m: dojrzałe drzewo przycinane, niższe od masztów przy nabrzeżu.", "decyzja gry (CLAUDE.md: ląd i drzewa nie zasłaniają statków)", "-", Confidence.GameChoice));
            }
            else
            {
                notes.Add(new SourceNote("Dzika oliwka częściej rośnie jako krzew; na dobrych glebach 7–8 m i pień ponad 70 cm.", "MonteroLopezLeiva2023", "s. 7", Confidence.Established));
                notes.Add(new SourceNote("Na suchej, skalistej wysepce: krzew wielopniowy 2,5–5 m.", "wniosek z MonteroLopezLeiva2023", "-", Confidence.Interpretation));
            }
            return notes.ToArray();
        }
    }
}
