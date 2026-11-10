using System;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// The hull a class floats on, from the catalogue's length (2 × Radius), beam and draft (with cargo, Rules.DraftPerLoad)
    /// and a few explicit form coefficients, which are calibration data, not measurements:
    /// <list type="bullet">
    /// <item>waterline 0.88 of the length and 0.8 of the beam; block coefficient by type (galleys 0.42–0.45, the broad
    /// boarder 0.50, the bomber 0.55, the merchant 0.60), waterplane coefficient (1 + 2·C_B)/3;</item>
    /// <item>ten stations whose half-breadth follows the waterplane coefficient, sections y = b·(z/T)^q with the section
    /// coefficient C_B/C_WP, wall-sided above the waterline up to the deck;</item>
    /// <item>mass = ρ_w·V at the draft (the hull floats at its draft by construction);</item>
    /// <item>GM 8–10% of the waterline beam, radii of gyration 0.4·B (roll) and 0.25·L (pitch);</item>
    /// <item>added mass in heave 0.7·ρ·(π/8)·B²·L (strip theory with a 3-D factor), in pitch the same times L²/12, in roll
    /// 0.2 of the roll inertia; damping as fractions of critical: 0.25 in heave and pitch, 0.15 in roll (+0.1 with the
    /// oars in the water) and a quadratic roll term;</item>
    /// <item>square sail 0.07–0.08·L² on galleys (auxiliary sail), 0.15·L² on the cutter, 0.25·L² on the merchant, centre
    /// of effort 0.22–0.30·L above the water.</item>
    /// </list>
    /// </summary>
    public sealed class HullForm
    {
        public const int Stations = 10;

        public readonly ShipClass Class;
        public readonly double OverallLength, Length, Beam, Draft, Freeboard;
        public readonly double BlockCoefficient, WaterplaneCoefficient, SectionCoefficient, SectionPower;
        public readonly double WaterDensity, Volume, Mass;
        public readonly double Metacentric, RollInertia, PitchInertia;
        public readonly double HeaveAddedMass, PitchAddedInertia, RollAddedInertia;
        public readonly double HeaveStiffness, PitchStiffness, RollStiffness;
        public readonly double HeaveDamping, PitchDamping, RollDamping, RollDampingOars, RollQuadratic;
        public readonly double SailArea, SailHeight, WindageSide, WindageFront, WindageHeight;
        public readonly double WettedSurface;
        public readonly double StationLength;
        public readonly double[] StationX = new double[Stations];
        public readonly double[] StationHalfBeam = new double[Stations];

        struct FormData
        {
            public double Block, MetacentricShare, SailShare, SailHeightShare;
            public FormData(double block, double gm, double sail, double height) { Block = block; MetacentricShare = gm; SailShare = sail; SailHeightShare = height; }
        }

        static FormData Data(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.Cutter: return new FormData(0.45, 0.10, 0.15, 0.30);
                case ShipClass.Ram: return new FormData(0.42, 0.08, 0.08, 0.25);
                case ShipClass.CrewDestroyer: return new FormData(0.42, 0.08, 0.08, 0.25);
                case ShipClass.Boarder: return new FormData(0.50, 0.08, 0.07, 0.22);
                case ShipClass.Merchant: return new FormData(0.60, 0.10, 0.25, 0.30);
                case ShipClass.ShipKiller: return new FormData(0.45, 0.08, 0.08, 0.25);
                case ShipClass.Bomber: return new FormData(0.55, 0.09, 0.07, 0.22);
                default: return new FormData(0.45, 0.08, 0.08, 0.25);
            }
        }

        /// <summary>The hull of a class carrying a share of its hold (0–1), in the climate's water.</summary>
        public static HullForm For(ShipClass shipClass, double load, MarineClimate climate) =>
            new HullForm(ShipCatalog.Get(shipClass), load, climate?.WaterDensity ?? 1026.3);

        HullForm(ShipDefinition definition, double load, double waterDensity)
        {
            var data = Data(definition.Class);
            var g = MarineUnits.Gravity;
            Class = definition.Class;
            WaterDensity = waterDensity;
            OverallLength = MarineUnits.Metres(2 * definition.Radius);
            Length = 0.88 * OverallLength;
            Beam = 0.8 * MarineUnits.Metres(definition.Beam);
            Draft = MarineUnits.Metres(definition.Draft) * (1 + Rules.DraftPerLoad * Math.Max(0, Math.Min(1, load)));
            Freeboard = (definition.IsEconomic ? 0.8 : 0.7) * MarineUnits.Metres(definition.Draft) + 0.4;
            BlockCoefficient = data.Block;
            WaterplaneCoefficient = (1 + 2 * data.Block) / 3;
            SectionCoefficient = data.Block / WaterplaneCoefficient;
            SectionPower = 1 / SectionCoefficient - 1;

            // Stations: half-breadth b(x) = B/2·(1 − |2x/L|^p), p from the waterplane coefficient p/(p + 1) = C_WP.
            var p = WaterplaneCoefficient / (1 - WaterplaneCoefficient);
            StationLength = Length / Stations;
            double area = 0, inertiaL = 0, volume = 0;
            for (var i = 0; i < Stations; i++)
            {
                var x = -Length / 2 + (i + 0.5) * StationLength;
                StationX[i] = x;
                // Mean half-breadth over the station's length (Simpson), so the stations add up to C_WP exactly.
                double b = 0;
                for (var s = 0; s <= 8; s++)
                {
                    var u = Math.Abs(2 * (x - StationLength / 2 + s * StationLength / 8) / Length);
                    var weight = s == 0 || s == 8 ? 1 : (s % 2 == 1 ? 4 : 2);
                    b += weight * Beam / 2 * (1 - Math.Pow(Math.Min(1, u), p));
                }
                StationHalfBeam[i] = b / 24;
                area += 2 * StationHalfBeam[i] * StationLength;
                inertiaL += 2 * StationHalfBeam[i] * StationLength * x * x;
                volume += SectionArea(i, Draft) * StationLength;
            }
            Volume = volume;
            Mass = waterDensity * volume;
            Metacentric = data.MetacentricShare * Beam;
            RollInertia = Mass * (0.4 * Beam) * (0.4 * Beam);
            PitchInertia = Mass * (0.25 * Length) * (0.25 * Length);
            HeaveAddedMass = 0.7 * waterDensity * Math.PI / 8 * Beam * Beam * Length;
            PitchAddedInertia = HeaveAddedMass * Length * Length / 12;
            RollAddedInertia = 0.2 * RollInertia;
            HeaveStiffness = waterDensity * g * area;
            PitchStiffness = waterDensity * g * inertiaL;
            RollStiffness = Mass * g * Metacentric;
            HeaveDamping = 2 * 0.25 * Math.Sqrt(HeaveStiffness * (Mass + HeaveAddedMass));
            PitchDamping = 2 * 0.25 * Math.Sqrt(PitchStiffness * (PitchInertia + PitchAddedInertia));
            var critical = 2 * Math.Sqrt(RollStiffness * (RollInertia + RollAddedInertia));
            // 0.15 of critical: small open hulls damp roll by keel, hull and eddy drag at 0.1–0.2 (Ikeda's components);
            // at 0.08 a 12 m cutter rolled 20° in a 0.8 m beam sea at resonance, its deck edge in the water.
            RollDamping = 0.15 * critical;
            RollDampingOars = 0.1 * critical;
            // Quadratic roll damping: worth another 0.05 of critical at 0.1 rad/s.
            RollQuadratic = 0.05 * critical / 0.1;

            SailArea = data.SailShare * OverallLength * OverallLength;
            SailHeight = data.SailHeightShare * OverallLength;
            WindageSide = OverallLength * (Freeboard + 0.8);
            WindageFront = Beam / 0.8 * (Freeboard + 0.8);
            WindageHeight = (Freeboard + 0.8) / 2;
            WettedSurface = Length * (1.7 * Draft + BlockCoefficient * Beam);
        }

        // (z/T)^(q+1) tabulated over the draft: the sections' only costly term, read many times a tick.
        const int ShapeSamples = 256;
        double[] shapeTable;

        double SectionShape(double fraction)
        {
            if (shapeTable == null)
            {
                shapeTable = new double[ShapeSamples + 1];
                for (var n = 0; n <= ShapeSamples; n++) shapeTable[n] = Math.Pow(n / (double)ShapeSamples, SectionPower + 1);
            }
            var t = Math.Max(0, Math.Min(1, fraction)) * ShapeSamples;
            var i = Math.Min(ShapeSamples - 1, (int)t);
            return shapeTable[i] + (shapeTable[i + 1] - shapeTable[i]) * (t - i);
        }

        /// <summary>Immersed area of a station's section at an immersion (m): y = b·(z/T)^q below the waterline, wall-sided above, nothing above the deck.</summary>
        public double SectionArea(int station, double immersion)
        {
            if (immersion <= 0) return 0;
            var b = StationHalfBeam[station];
            var below = Math.Min(immersion, Draft);
            var area = 2 * b * Draft * SectionCoefficient * SectionShape(below / Draft);
            if (immersion > Draft) area += 2 * b * (Math.Min(immersion, Draft + Freeboard) - Draft);
            return area;
        }

        public double Weight => Mass * MarineUnits.Gravity;

        /// <summary>Natural periods from stiffness and mass with added mass, seconds.</summary>
        public double HeavePeriod => 2 * Math.PI * Math.Sqrt((Mass + HeaveAddedMass) / HeaveStiffness);
        public double PitchPeriod => 2 * Math.PI * Math.Sqrt((PitchInertia + PitchAddedInertia) / PitchStiffness);
        public double RollPeriod => 2 * Math.PI * Math.Sqrt((RollInertia + RollAddedInertia) / RollStiffness);
    }

    /// <summary>Forces of the wind on a ship's sail and hull, in its own frame: forward, to port, and the moments they heel and trim it by.</summary>
    public struct WindForces
    {
        public double Drive, Side, HeelMoment, TrimMoment;
        public double SailDrive, SailSide, YardAngle;

        /// <summary>Share of the sail set: 1 all of it, less with the brails drawn up (SailForces.HeelLimit), 0 furled.</summary>
        public double SailShare;
        public bool SailSet;
    }

    /// <summary>
    /// Wind on a square sail and on the hull. The apparent wind at the sail's centre of effort (logarithmic profile of the
    /// local wind, COARE roughness) gives lift and drag of a square sail, C_L = 1.1·sin 2α, C_D = 0.12 + 1.15·sin²α (a
    /// parametric fit to square-rig coefficients, Marchaj 1996, for calibration); the crew braces the yard within ±60° of
    /// athwartships for the most drive and furls the sail when no bracing drives the ship forward. Hull and rigging take
    /// drag 0.9 on their projected area at 2 m. The heeling arm runs from the centre of effort to half the draft.
    /// The crew also shortens sail: the brails draw up the loose foot of the square sail (Casson 1995, 38–39; Whitewright
    /// 2018, IJNA 47) and the sheets are eased in a gust, so the wind never heels the ship past HeelLimit.
    /// </summary>
    public static class SailForces
    {
        public const double MaxBrace = 60 * Math.PI / 180;

        public static double Lift(double attack) => 1.1 * Math.Sin(2 * attack);
        public static double Drag(double attack) => 0.12 + 1.15 * Math.Sin(attack) * Math.Sin(attack);

        /// <summary>
        /// The steady heel the crew lets the wind give her: IMO's guide for heel under a steady wind, 16° or 80% of the
        /// angle at which the deck edge goes under, whichever is less (Res. A.562(14), 1985, kept in the 2008 IS Code), with
        /// the gunwale of an open hull for the deck edge. Borrowed from ships' stability standards: how far an ancient crew
        /// let her heel is not recorded.
        /// </summary>
        public static double HeelLimit(HullForm hull) =>
            Math.Min(16 * Math.PI / 180, 0.8 * Math.Atan(hull.Freeboard / (hull.Beam / 2)));

        /// <summary>
        /// Forces for a ship heading (radians, simulation plane) and moving with (vx, vy), in a wind sample.
        /// </summary>
        public static WindForces Compute(HullForm hull, double heading, double vx, double vy, WindSample wind, double airDensity)
        {
            var result = new WindForces();
            var ex = Math.Cos(heading);
            var ey = Math.Sin(heading);
            var speed = wind.Speed;
            var dx = speed > 1e-9 ? wind.X / speed : 1;
            var dy = speed > 1e-9 ? wind.Y / speed : 0;
            var depthArm = hull.Draft / 2;

            // Hull, crew and rigging: drag along the apparent wind near the water.
            var low = wind.SpeedAt(2);
            var hx = low * dx - vx;
            var hy = low * dy - vy;
            var hf = hx * ex + hy * ey;
            var hp = -hx * ey + hy * ex;
            var hullApparent = Math.Sqrt(hf * hf + hp * hp);
            double windDrive = 0, windSide = 0;
            if (hullApparent > 1e-6)
            {
                var projected = hull.WindageFront * Math.Abs(hf) / hullApparent + hull.WindageSide * Math.Abs(hp) / hullApparent;
                var drag = 0.5 * airDensity * hullApparent * hullApparent * 0.9 * projected;
                windDrive = drag * hf / hullApparent;
                windSide = drag * hp / hullApparent;
            }

            // Sail: apparent wind at the centre of effort, in the ship's frame (forward, port).
            var high = wind.SpeedAt(hull.SailHeight);
            var ax = high * dx - vx;
            var ay = high * dy - vy;
            var forward = ax * ex + ay * ey;
            var port = -ax * ey + ay * ex;
            var apparent = Math.Sqrt(forward * forward + port * port);
            if (apparent > 1e-6 && hull.SailArea > 0)
            {
                var q = 0.5 * airDensity * apparent * apparent * hull.SailArea;
                var fx = forward / apparent;
                var fy = port / apparent;
                double bestDrive = 0, bestSide = 0, bestYard = 0;
                var found = false;
                for (var step = -24; step <= 24; step++)
                {
                    var yard = step * MaxBrace / 24;
                    // The sail's normal points forward when the yard lies athwartships.
                    var nx = Math.Cos(yard);
                    var ny = Math.Sin(yard);
                    var along = fx * nx + fy * ny;
                    if (along <= 0) continue; // taken aback
                    var attack = Math.PI / 2 - Math.Acos(Math.Min(1, along));
                    // Lift is across the flow, toward the sail's normal.
                    var px = nx - along * fx;
                    var py = ny - along * fy;
                    var pl = Math.Sqrt(px * px + py * py);
                    if (pl > 1e-9) { px /= pl; py /= pl; }
                    var drive = q * (Drag(attack) * fx + Lift(attack) * px);
                    var side = q * (Drag(attack) * fy + Lift(attack) * py);
                    if (!found || drive > bestDrive) { bestDrive = drive; bestSide = side; bestYard = yard; found = true; }
                }
                if (found && bestDrive > 0)
                {
                    // Shortened sail: the share that keeps the heel of sail and hull together within the limit (the
                    // hull's own windage is not the crew's to shorten). All forces of the sail scale with its area.
                    var limit = hull.RollStiffness * Math.Sin(HeelLimit(hull));
                    var hullMoment = -windSide * (hull.WindageHeight + depthArm);
                    var sailMoment = -bestSide * (hull.SailHeight + depthArm);
                    var share = 1.0;
                    if (Math.Abs(hullMoment + sailMoment) > limit && Math.Abs(sailMoment) > 1e-9)
                        share = Math.Max(0, Math.Min(1, (limit - hullMoment * Math.Sign(sailMoment)) / Math.Abs(sailMoment)));
                    if (share > 0)
                    {
                        result.SailSet = true;
                        result.SailShare = share;
                        result.SailDrive = bestDrive * share;
                        result.SailSide = bestSide * share;
                        result.YardAngle = bestYard;
                    }
                }
            }

            result.Drive = result.SailDrive + windDrive;
            result.Side = result.SailSide + windSide;
            // A force to port high up heels the ship to port (port side down: negative roll); forward drive trims her by the head.
            result.HeelMoment = -(result.SailSide * (hull.SailHeight + depthArm) + windSide * (hull.WindageHeight + depthArm));
            result.TrimMoment = -(result.SailDrive * (hull.SailHeight + depthArm) + windDrive * (hull.WindageHeight + depthArm));
            return result;
        }
    }

    /// <summary>Heave (m, up), roll (rad, port side up) and pitch (rad, bow up) of a hull and their rates.</summary>
    public struct ShipMotion
    {
        public double Heave, HeaveRate, Roll, RollRate, Pitch, PitchRate;
    }

    /// <summary>
    /// Heave, roll and pitch of a hull in the waves, three degrees of freedom:
    /// <list type="bullet">
    /// <item>heave and pitch from buoyancy integrated over ten stations on each side, at the local effective surface
    /// (Froude–Krylov with the Smith reduction over the draft) — nonlinear in the immersion;</item>
    /// <item>roll from GM about the effective wave slope across the hull (linear restoring, Δ·g·GM·sin(φ − α));</item>
    /// <item>damping on the motion relative to the water's vertical velocity, linear plus quadratic in roll;</item>
    /// <item>wind heel and trim moments from SailForces;</item>
    /// <item>semi-implicit Euler, two sub-steps per tick, the surface sampled once per tick and carried by its own
    /// vertical velocity inside the tick.</item>
    /// </list>
    /// Horizontal motion is not integrated here: the ship's track belongs to the simulation.
    /// </summary>
    public sealed class Seakeeping
    {
        /// <summary>Two sub-steps a tick: ω·h ≤ 0.08 for the stiffest hull, far inside semi-implicit Euler's limit of 2.</summary>
        public const int Substeps = 2;
        const int N = HullForm.Stations;

        readonly double[] portHeight = new double[N], starboardHeight = new double[N];
        readonly double[] portRate = new double[N], starboardRate = new double[N];

        /// <summary>Settles a hull at rest on calm water: floating at its draft, level.</summary>
        public static ShipMotion AtRest() => new ShipMotion();

        /// <summary>Effective water height at the bow station in the last step (for spray and bow immersion).</summary>
        public double BowHeight => 0.5 * (portHeight[N - 1] + starboardHeight[N - 1]);

        /// <summary>
        /// Lays a hull on the water it floats in (heave at the mean height, pitch and roll along the effective slopes, at
        /// rest), so a hull coming into view does not drop onto the waves from calm water.
        /// </summary>
        public void Settle(HullForm hull, ref ShipMotion motion, SeaSurface surface, double x, double y, double heading, double time, double blend)
        {
            motion = new ShipMotion();
            if (surface == null) return;
            var ex = Math.Cos(heading);
            var ey = Math.Sin(heading);
            var half = hull.Beam / 4;
            var spacing = hull.StationLength;
            var sternX = x + ex * hull.StationX[0];
            var sternY = y + ey * hull.StationX[0];
            surface.SampleLine(sternX - ey * half, sternY + ex * half, ex * spacing, ey * spacing, N, time, blend, hull.Draft, portHeight, portRate);
            surface.SampleLine(sternX + ey * half, sternY - ex * half, ex * spacing, ey * spacing, N, time, blend, hull.Draft, starboardHeight, starboardRate);
            double mean = 0, along = 0, sumX2 = 0, across = 0;
            for (var i = 0; i < N; i++)
            {
                var h = 0.5 * (portHeight[i] + starboardHeight[i]);
                mean += h / N;
                along += hull.StationX[i] * h;
                sumX2 += hull.StationX[i] * hull.StationX[i];
                across += (portHeight[i] - starboardHeight[i]) / (2 * half) / N;
            }
            motion.Heave = mean;
            motion.Pitch = Math.Atan(along / sumX2);
            motion.Roll = Math.Atan(across);
        }

        /// <summary>
        /// Advances the motion over dt seconds for a hull centred at (x, y) heading along an angle, on the surface at a
        /// simulation time, with wind moments (N·m) and the oars in the water or not.
        /// </summary>
        public void Step(HullForm hull, ref ShipMotion motion, SeaSurface surface, double x, double y, double heading,
            double time, double blend, double dt, double heelMoment, double trimMoment, bool oars)
        {
            var ex = Math.Cos(heading);
            var ey = Math.Sin(heading);
            var px = -ey;
            var py = ex;
            var half = hull.Beam / 4;
            var spacing = hull.StationLength;
            var sternX = x + ex * hull.StationX[0];
            var sternY = y + ey * hull.StationX[0];
            if (surface != null)
            {
                // Both sides share the amplitudes taken at the port line's middle: they are a quarter beam apart.
                surface.SampleHull(sternX + px * half, sternY + py * half, sternX - px * half, sternY - py * half, ex * spacing, ey * spacing, N,
                    time, blend, hull.Draft, portHeight, portRate, starboardHeight, starboardRate);
            }
            else
            {
                Array.Clear(portHeight, 0, N); Array.Clear(starboardHeight, 0, N);
                Array.Clear(portRate, 0, N); Array.Clear(starboardRate, 0, N);
            }

            // The effective slope of the water across the hull and its rate.
            double slope = 0, slopeRate = 0;
            for (var i = 0; i < N; i++)
            {
                slope += (portHeight[i] - starboardHeight[i]) / (2 * half) / N;
                slopeRate += (portRate[i] - starboardRate[i]) / (2 * half) / N;
            }

            var g = MarineUnits.Gravity;
            var rho = hull.WaterDensity;
            var h = dt / Substeps;
            var heaveMass = hull.Mass + hull.HeaveAddedMass;
            var pitchMass = hull.PitchInertia + hull.PitchAddedInertia;
            var rollMass = hull.RollInertia + hull.RollAddedInertia;
            var rollDamping = hull.RollDamping + (oars ? hull.RollDampingOars : 0);
            var stationDamping = hull.HeaveDamping / N;
            for (var s = 0; s < Substeps; s++)
            {
                var tau = s * h;
                var sinPitch = Math.Sin(motion.Pitch);
                var sinRoll = Math.Sin(motion.Roll);
                var cosPitch = Math.Cos(motion.Pitch);
                double force = -hull.Weight, moment = 0;
                for (var i = 0; i < N; i++)
                {
                    var xi = hull.StationX[i];
                    var hullZ = motion.Heave + xi * sinPitch;
                    var port = hull.Draft + portHeight[i] + portRate[i] * tau - (hullZ + half * sinRoll);
                    var starboard = hull.Draft + starboardHeight[i] + starboardRate[i] * tau - (hullZ - half * sinRoll);
                    var lift = rho * g * 0.5 * (hull.SectionArea(i, port) + hull.SectionArea(i, starboard)) * hull.StationLength;
                    // Damping on each station's motion relative to the water there.
                    var relative = motion.HeaveRate + xi * motion.PitchRate * cosPitch - 0.5 * (portRate[i] + starboardRate[i]);
                    var damping = -stationDamping * relative;
                    force += lift + damping;
                    moment += xi * (lift + damping);
                }
                var alpha = slope + slopeRate * tau;
                var rollMoment = -hull.RollStiffness * Math.Sin(motion.Roll - alpha)
                    - rollDamping * (motion.RollRate - slopeRate)
                    - hull.RollQuadratic * motion.RollRate * Math.Abs(motion.RollRate)
                    + heelMoment;
                motion.HeaveRate += force / heaveMass * h;
                motion.PitchRate += (moment + trimMoment) / pitchMass * h;
                motion.RollRate += rollMoment / rollMass * h;
                motion.Heave += motion.HeaveRate * h;
                motion.Pitch += motion.PitchRate * h;
                motion.Roll += motion.RollRate * h;
            }
        }

        /// <summary>Steady heel under a constant heeling moment (N·m): Δ·g·GM·sin φ = M.</summary>
        public static double StaticHeel(HullForm hull, double heelMoment) => Math.Asin(Math.Max(-1, Math.Min(1, heelMoment / hull.RollStiffness)));
    }
}
