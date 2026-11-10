using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>How sure a claim is: read in a source, inferred from sources, a guess, or a choice made for the game.</summary>
    public enum Confidence { Established, Interpretation, Speculation, GameChoice }

    /// <summary>
    /// The leaf a species' atlas draws: the olive's lanceolate leaves in opposite pairs, a spiny-toothed oval leaf one
    /// per node (kermes oak), or a pinnate leaf of paired leaflets one per node (lentisk).
    /// </summary>
    public enum LeafKind { Olive, SpinyOval, Pinnate }

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

        // The crown's build. The defaults are the olive's: an open vase on a short stem, shoots hanging from it.

        /// <summary>Lowest the foliage comes, as a share of the height (unless the forks stand higher).</summary>
        public float CrownBase = 0.3f;

        /// <summary>How far the upper stem turns from the lower one, degrees.</summary>
        public float StemKinkMin = 6f, StemKinkMax = 15f;

        /// <summary>How far the main limbs rise into the height left above the fork (share), and how much their outer half curls up (degrees).</summary>
        public float LimbRiseMin = 0.32f, LimbRiseMax = 0.45f, LimbCurlMin = 8f, LimbCurlMax = 16f;

        /// <summary>Side branches: tilt from the vertical (degrees) and length as a share of the height.</summary>
        public float BranchTiltMin = 40f, BranchTiltMax = 80f, BranchLengthMin = 0.2f, BranchLengthMax = 0.32f;

        /// <summary>Leafy shoots: tilt from the vertical (over 90° they hang) and length in metres on a 6 m tree.</summary>
        public float ShootTiltMin = 55f, ShootTiltMax = 105f, ShootLengthMin = 0.35f, ShootLengthMax = 0.7f;

        /// <summary>
        /// Where the foliage sits: the lowest direction on the crown's envelope (y of a unit vector), the inner edge of
        /// the leafy shell (share of the envelope), how far a spray sits from its wood (m) and one spray's mass (kg).
        /// </summary>
        public float SprayFloor = -0.45f, ShellInner = 0.72f, SprayReach = 0.6f, SprayMass = TreeGenerator.SprayMass;

        /// <summary>Largest card of the farther levels, as a share of the crown's radius (the olive's are not capped).</summary>
        public float FarCardCap = float.PositiveInfinity;

        /// <summary>The leaf drawn in the atlas, and what that kind of leaf needs: spines per side, pairs of leaflets.</summary>
        public LeafKind LeafShape = LeafKind.Olive;
        public int TeethMin, TeethMax, LeafletPairsMin, LeafletPairsMax;

        /// <summary>Smoothness of the leaf's upper side and underside, and the colour of young and old shoots (sRGB).</summary>
        public float LeafGlossTop = 0.55f, LeafGlossUnder = 0.3f;
        public Color ShootYoung = new Color(0.50f, 0.54f, 0.38f), ShootOld = new Color(0.46f, 0.43f, 0.36f);

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

        /// <summary>
        /// Kermes oak of the maquis: a dense, rounded, many-stemmed evergreen bush, the oval leaves stiff, spiny-toothed
        /// and wavy, glossy on both sides (report 8.1–8.8). Kept at the browsed and the free form of Greek scrub,
        /// 0.8–2 m, never the tree it grows into when no goat reaches it.
        /// </summary>
        public static readonly TreeSpecies KermesOak = new TreeSpecies
        {
            Id = "quercus_coccifera",
            LatinName = "Quercus coccifera L.",
            Name = "Dąb kermesowy",
            HeightMin = 0.8f, HeightMax = 2f,
            ForkHeightMin = 0.04f, ForkHeightMax = 0.12f,
            StemDiameterMin = 0.03f, StemDiameterMax = 0.07f,
            StemsMin = 3, StemsMax = 6,
            ScaffoldsMin = 2, ScaffoldsMax = 3,
            BranchesMin = 3, BranchesMax = 4,
            ShootsMin = 3, ShootsMax = 5,
            ScaffoldTiltMin = 25f, ScaffoldTiltMax = 50f,
            CrownWidthRatio = 1.3f,
            TrunkTwist = 0.1f,
            TrunkFluting = 0.02f,
            CrownBase = 0.04f,
            StemKinkMin = 2f, StemKinkMax = 8f,
            LimbRiseMin = 0.5f, LimbRiseMax = 0.7f, LimbCurlMin = 5f, LimbCurlMax = 12f,
            BranchTiltMin = 30f, BranchTiltMax = 70f, BranchLengthMin = 0.25f, BranchLengthMax = 0.4f,
            ShootTiltMin = 20f, ShootTiltMax = 70f, ShootLengthMin = 0.25f, ShootLengthMax = 0.45f,
            SprayFloor = -0.95f, ShellInner = 0.6f, SprayReach = 0.3f, SprayMass = 0.02f,
            FarCardCap = 0.6f,
            LeafShape = LeafKind.SpinyOval,
            TeethMin = 4, TeethMax = 8,
            LeafLengthMin = 0.015f, LeafLengthMax = 0.035f, LeafWidth = 0.012f,
            LeafGlossTop = 0.7f, LeafGlossUnder = 0.55f,
            SprayLength = 0.3f,
            SprayDensity = 35f,
            LeafTop = new Color(0.22f, 0.32f, 0.17f),
            LeafUnder = new Color(0.42f, 0.50f, 0.32f),
            ShootYoung = new Color(0.47f, 0.45f, 0.36f),
            ShootOld = new Color(0.40f, 0.38f, 0.34f),
            WoodDensity = 1000f,
            WoodModulus = 9e9f,
            CrownDrag = 0.3f,
            ReconfigurationSpeed = 3f,
            VogelExponent = -1f,
            SwayFrequency = 1.4f,
            ReferenceDiameter = 0.4f,
            ReferenceHeight = 5.5f,
            TrunkDamping = 0.08f,
            BranchDamping = 0.1f,
            FlutterFrequency = 7f,
            FlutterOnset = 2f,
            Sources = ShrubSources(oak: true),
        };

        /// <summary>
        /// Lentisk (mastic) of the maquis by the sea: a dense, rounded evergreen bush of a few stems, the leaves pinnate
        /// with three to six pairs of leathery leaflets on a winged rachis (report 8.9–8.14). Kept at 1–2.8 m.
        /// </summary>
        public static readonly TreeSpecies Lentisk = new TreeSpecies
        {
            Id = "pistacia_lentiscus",
            LatinName = "Pistacia lentiscus L.",
            Name = "Lentyszek",
            HeightMin = 1f, HeightMax = 2.8f,
            ForkHeightMin = 0.05f, ForkHeightMax = 0.15f,
            StemDiameterMin = 0.04f, StemDiameterMax = 0.1f,
            StemsMin = 2, StemsMax = 5,
            ScaffoldsMin = 2, ScaffoldsMax = 3,
            BranchesMin = 3, BranchesMax = 4,
            ShootsMin = 2, ShootsMax = 4,
            ScaffoldTiltMin = 25f, ScaffoldTiltMax = 45f,
            CrownWidthRatio = 1.2f,
            TrunkTwist = 0.1f,
            TrunkFluting = 0.02f,
            CrownBase = 0.05f,
            StemKinkMin = 2f, StemKinkMax = 8f,
            LimbRiseMin = 0.5f, LimbRiseMax = 0.7f, LimbCurlMin = 5f, LimbCurlMax = 12f,
            BranchTiltMin = 30f, BranchTiltMax = 65f, BranchLengthMin = 0.25f, BranchLengthMax = 0.4f,
            ShootTiltMin = 25f, ShootTiltMax = 75f, ShootLengthMin = 0.3f, ShootLengthMax = 0.5f,
            SprayFloor = -0.92f, ShellInner = 0.62f, SprayReach = 0.35f, SprayMass = 0.03f,
            FarCardCap = 0.6f,
            LeafShape = LeafKind.Pinnate,
            LeafletPairsMin = 3, LeafletPairsMax = 5,
            LeafLengthMin = 0.015f, LeafLengthMax = 0.035f, LeafWidth = 0.008f,
            LeafGlossTop = 0.65f, LeafGlossUnder = 0.35f,
            SprayLength = 0.35f,
            SprayDensity = 28f,
            LeafTop = new Color(0.20f, 0.31f, 0.16f),
            LeafUnder = new Color(0.36f, 0.44f, 0.28f),
            ShootYoung = new Color(0.55f, 0.38f, 0.28f),
            ShootOld = new Color(0.45f, 0.43f, 0.40f),
            WoodDensity = 1000f,
            WoodModulus = 9e9f,
            CrownDrag = 0.3f,
            ReconfigurationSpeed = 3f,
            VogelExponent = -1f,
            SwayFrequency = 1.4f,
            ReferenceDiameter = 0.4f,
            ReferenceHeight = 5.5f,
            TrunkDamping = 0.08f,
            BranchDamping = 0.1f,
            FlutterFrequency = 7f,
            FlutterOnset = 2f,
            Sources = ShrubSources(oak: false),
        };

        /// <summary>The maquis shrubs, on the same generator and wind as the trees.</summary>
        public static IReadOnlyList<TreeSpecies> Shrubs { get; } = new[] { KermesOak, Lentisk };

        static SourceNote[] ShrubSources(bool oak)
        {
            var notes = new List<SourceNote>();
            if (oak)
            {
                notes.Add(new SourceNote("Zimozielony krzew do 2 m, rzadko małe drzewo; liście ok. 1,5–4 cm, sztywne, jajowate lub podłużne, brzeg kolczasto-ząbkowany i falisty, ogonek 1–4 mm; wierzch ciemnozielony, spód bladozielony.", "FloraEuropaea1964-vol1 (Plazi)", "s. 62 (długość liścia z tekstu po OCR)", Confidence.Established));
                notes.Add(new SourceNote("Krzew 0,25–1,5 m, drzewo do 15 m, gdy nie jest zgryzany; liście 1–6 × 0,7–3 cm, 4–8 kolców na brzegu, obie strony błyszczące.", "CoombesCameron2021-TreesAndShrubsOnline", "strona gatunku", Confidence.Established));
                notes.Add(new SourceNote("Korona gęsta, zaokrąglona; liście 1–3,5 × 0,7–2 cm, 5–8 par ostrych zębów, falisty brzeg.", "OaksOfTheWorld", "strona gatunku", Confidence.Interpretation));
                notes.Add(new SourceNote("W Grecji forma niska 0,5–0,8 m i wysoka ok. 2 m; zgryzanie zagęszcza pędy.", "Tsiouvaras1987", "abstrakt", Confidence.Established));
                notes.Add(new SourceNote("Krzewy przy zagrodach niższe i rzadsze.", "Papanastasis2009", "s. 49–50", Confidence.Established));
                notes.Add(new SourceNote("Odporny na wypas, na suchych, kamienistych, często wapiennych glebach.", "EUFORGEN-Quercus-coccifera", "karta gatunku", Confidence.Established));
            }
            else
            {
                notes.Add(new SourceNote("Krzew lub małe drzewo 1–8 m; liście parzystopierzaste, (4–)8–12 listków 1–5 cm, skórzaste, z ością; osadka szeroko oskrzydlona.", "FloraEuropaea1981-vol2 (Plazi)", "s. 237 (szerokość listka z tekstu po OCR)", Confidence.Established));
                notes.Add(new SourceNote("0,5–2 m, kora szara lub czerwonawa, liście naprzemianległe, listki całobrzegie.", "BlascoZumeta-FloraPinaDeEbro", "ark. 037", Confidence.Established));
                notes.Add(new SourceNote("1–3(5) m, korona 1–2(10) m, liście ciemnozielone, 3–6 par listków.", "DiputacionCadiz-GuiaVirtual", "ark. 56", Confidence.Established));
                notes.Add(new SourceNote("Korona kulista i gęsta, 3–5 par listków ok. 2,5 cm, młode przyrosty żółtozielone, młoda kora czerwonawa (okaz uprawny).", "UnivArizona-CampusArboretum", "strona taksonu", Confidence.Interpretation));
                notes.Add(new SourceNote("Rośnie w makii od wybrzeża do 700–800(–1000) m n.p.m.", "FILMED-UniwWalencja", "strona gatunku", Confidence.Established));
                notes.Add(new SourceNote("Rośnie w makii z dziką oliwką.", "JRC2016-Olea", "s. 111", Confidence.Established));
            }
            notes.Add(new SourceNote("Flory opisują stan współczesny; ten sam pokrój w latach 1200–400 p.n.e. to interpretacja.", "raport 8 (wstęp)", "-", Confidence.Interpretation));
            notes.Add(new SourceNote("Zmierzonych częstotliwości kołysania krzewów nie znaleziono: model wahadła drzew liściastych (1,4 Hz przy 5,5 m) przeniesiony poniżej swojego zakresu 5–20 m daje 2–3 Hz.", "Jackson2021", "tab. 2 (przeniesienie)", Confidence.Speculation));
            notes.Add(new SourceNote("Gęste struktury porowate dążą do V = −1; przeniesione na gęsty krzew.", "Gosselin2019", "tab. 1, równanie 4", Confidence.Speculation));
            notes.Add(new SourceNote("Drewno, tłumienie i trzepotanie: wartości oliwki, tłumienie nieco większe dla gęstego krzewu; liście sztywne trzepoczą od 2 m/s.", "szacunek", "-", Confidence.Speculation));
            return notes.ToArray();
        }

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
