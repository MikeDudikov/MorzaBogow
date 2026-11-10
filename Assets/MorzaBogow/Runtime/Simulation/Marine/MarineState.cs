using System;
using System.IO;
using System.IO.Compression;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// The wind and the sea of a match, owned by the world and advanced by its step (one clock): the wind field, the
    /// wave field and, built from them and the map, the sea surface and the depths. Everything that evolves is saved;
    /// everything derived (grids, tables, the surface) is rebuilt from the map, the climate and the seed.
    /// When WindAffectsMovement is on, the wind and the waves also change the ships' speed, leeway and drift
    /// (C09: within ±25% of the base speed, a current of at most 20%); off (the default), movement is exactly as
    /// without the marine model and the ships' heel and motion are left to the presentation.
    /// </summary>
    public sealed class MarineState
    {
        /// <summary>Each ship's wind and wave forces are worked out five times a second, ships staggered over the ticks.</summary>
        public const int ShipUpdateTicks = 6;

        /// <summary>Total resistance coefficient on the wetted surface at the catalogue speed (calibration, slender hulls).</summary>
        public const double ResistanceCoefficient = 3e-3;

        /// <summary>Ships slower than this are held at anchor or by their moorings: no leeway, no drift.</summary>
        public const double UnderWay = 0.5;

        /// <summary>C09: wind and sails change a ship's speed by at most a quarter; a current carries it at most a fifth of its base speed.</summary>
        public const double WindSpeedShare = 0.25, CurrentSpeedShare = 0.2;

        public readonly MarineClimate Climate;
        public readonly ulong Seed;
        public bool WindAffectsMovement;
        public WindField Wind { get; private set; }
        public WaveField Waves { get; private set; }
        public SeaSurface Surface { get; private set; }
        public Bathymetry Depths { get; private set; }

        readonly World world;
        int islandCount;

        internal MarineState(World world, MarineClimate climate, ulong seed, bool windAffectsMovement)
        {
            this.world = world;
            Climate = climate;
            Seed = seed;
            WindAffectsMovement = windAffectsMovement;
            Wind = new WindField(climate, MarineHash.Mix(seed, MarineHash.Process, 0), world.Islands);
            Build();
        }

        /// <summary>Grids from the map; a new map (an outpost's land) rebuilds them and redoes the step under way.</summary>
        void Build()
        {
            islandCount = world.Islands.Count;
            var previous = Waves;
            Waves = new WaveField(WaveGrid.ForMap(world.Islands), Climate);
            if (previous != null)
            {
                CopyState(previous, Waves);
                Waves.Restore();
            }
            Depths = new Bathymetry(world.Islands);
            Surface = new SeaSurface(Waves, Seed, Depths);
        }

        static void CopyState(WaveField from, WaveField to)
        {
            Array.Copy(from.Energy, to.Energy, from.Energy.Length);
            Array.Copy(from.WindX, to.WindX, from.WindX.Length);
            Array.Copy(from.WindY, to.WindY, from.WindY.Length);
            Array.Copy(from.Foam, to.Foam, from.Foam.Length);
            Array.Copy(from.TailFactor, to.TailFactor, from.TailFactor.Length);
            Array.Copy(from.Outer, to.Outer, from.Outer.Length);
            Array.Copy(from.SampleX, to.SampleX, from.SampleX.Length);
            Array.Copy(from.SampleY, to.SampleY, from.SampleY.Length);
            Array.Copy(from.GustSample, to.GustSample, from.GustSample.Length);
            to.SynopticX = from.SynopticX;
            to.SynopticY = from.SynopticY;
            to.Steps = from.Steps;
            to.Slice = from.Slice;
        }

        /// <summary>The sea at the start of a match: the open sea's state for the wind, settled around the islands.</summary>
        internal void Initialise(double settleSeconds) => Waves.Initialise(Wind, settleSeconds);

        /// <summary>Cost of the marine part of the world's ticks so far (measurement only; never read by the simulation).</summary>
        public double TickMilliseconds { get; private set; }
        public int TimedTicks { get; private set; }

        internal void Tick()
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            if (world.Islands.Count != islandCount) Build();
            Wind.Advance(Rules.Dt * Climate.WeatherTimeScale);
            Waves.Tick(Wind);
            if (WindAffectsMovement)
                foreach (var ship in world.Ships)
                    if ((ship.Id + world.Tick) % ShipUpdateTicks == 0) UpdateShip(ship);
            TickMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            TimedTicks++;
        }

        /// <summary>Seconds of simulation time for the wave phases: the weather's acceleration never changes the dispersion.</summary>
        public double SurfaceTime(double tickFraction = 0) => (world.Tick + tickFraction) * Rules.Dt;

        /// <summary>The share of a ship's hold that is full (0–1), as navigation counts it for the draft.</summary>
        public static double Load(Ship ship)
        {
            var capacity = ship.Definition.Capacity * Rules.Centi;
            return capacity > 0 ? Math.Min(1.0, (double)(ship.Cargo + ship.HoldWood + ship.HoldSilver) / capacity) : 0;
        }

        /// <summary>Wind and wave forces on a ship under way: its speed factor, leeway and the surface current.</summary>
        void UpdateShip(Ship ship)
        {
            var definition = ship.Definition;
            if (ship.Speed < UnderWay)
            {
                ship.WindSpeedFactor = 1;
                ship.DriftX = 0;
                ship.DriftY = 0;
                return;
            }
            var hull = HullForm.For(ship.Class, Load(ship), Climate);
            var wind = Wind.Sample(ship.X, ship.Y);
            var ex = Math.Cos(ship.Heading);
            var ey = Math.Sin(ship.Heading);
            var forces = SailForces.Compute(hull, ship.Heading, ex * ship.Speed + ship.DriftX, ey * ship.Speed + ship.DriftY, wind, Climate.AirDensity);
            var waveResistance = AddedResistance(hull, ship.X, ship.Y, ship.Heading);
            var rho = Climate.WaterDensity;
            var baseSpeed = definition.Speed;
            // The oars hold the catalogue speed against the hull's resistance there; sail, wind and waves add to it.
            var resistance = 0.5 * rho * hull.WettedSurface * ResistanceCoefficient * baseSpeed * baseSpeed;
            var factor = Math.Sqrt(Math.Max(0, 1 + (forces.Drive - waveResistance) / resistance));
            ship.WindSpeedFactor = Math.Max(1 - WindSpeedShare, Math.Min(1 + WindSpeedShare, factor));

            // Leeway: the side force against the hull's lateral resistance, lift at speed and drag across:
            // ½ρ·L·T·v² + ½π·ρ·T²·V·v = |F| (slender body; C_D across = 1).
            var side = Math.Abs(forces.Side);
            var a = 0.5 * rho * hull.Length * hull.Draft;
            var b = 0.5 * Math.PI * rho * hull.Draft * hull.Draft * ship.Speed;
            var leeway = side > 0 ? (-b + Math.Sqrt(b * b + 4 * a * side)) / (2 * a) * Math.Sign(forces.Side) : 0;

            // The wind drift of the surface water carries every hull, at most a fifth of the base speed (C09).
            var drift = Math.Min(Climate.DriftFraction * wind.MeanSpeed, CurrentSpeedShare * baseSpeed);
            var mean = wind.MeanSpeed;
            var cx = mean > 1e-9 ? drift * wind.MeanX / mean : 0;
            var cy = mean > 1e-9 ? drift * wind.MeanY / mean : 0;
            ship.DriftX = -ey * leeway + cx;
            ship.DriftY = ex * leeway + cy;
        }

        /// <summary>
        /// Mean added resistance in waves from ahead (within 45° of the bow), STAWAVE-1 (ITTC 2014, short waves):
        /// R = ρ·g·H²·B·√(B/L_bwl)/16, with H the significant height of the waves meeting the bow and L_bwl the length of
        /// the bow to 95% of the beam.
        /// </summary>
        public double AddedResistance(HullForm hull, double x, double y, double heading)
        {
            var cell = Waves.Grid.CellAt(x, y);
            if (cell < 0) return 0;
            double head = 0;
            var against = heading + Math.PI;
            for (var b = 0; b < WaveSpectrum.Bins; b++)
            {
                var j = b % WaveSpectrum.Directions;
                if (Math.Cos(WaveSpectrum.DirectionAngle[j] - against) >= Math.Cos(Math.PI / 4)) head += Waves.Energy[b * Waves.Cells + cell];
            }
            var height = 4 * Math.Sqrt(head);
            var p = hull.WaterplaneCoefficient / (1 - hull.WaterplaneCoefficient);
            var bow = hull.Length / 2 * (1 - Math.Pow(0.05, 1 / p));
            return Climate.WaterDensity * MarineUnits.Gravity * height * height * hull.Beam * Math.Sqrt(hull.Beam / bow) / 16;
        }

        // Saving -----------------------------------------------------------------------------------------------------------

        internal void Write(BinaryWriter writer)
        {
            Climate.Write(writer);
            writer.Write(Seed);
            writer.Write(WindAffectsMovement);

            writer.Write(Wind.Time); writer.Write(Wind.DirectionOffset); writer.Write(Wind.SpeedFactor);
            writer.Write(Wind.DriftX); writer.Write(Wind.DriftY); writer.Write(Wind.Random.State);
            writer.Write(Wind.Cells.Count);
            foreach (var cell in Wind.Cells)
            {
                writer.Write(cell.SpawnX); writer.Write(cell.SpawnY); writer.Write(cell.BornDriftX); writer.Write(cell.BornDriftY);
                writer.Write(cell.Radius); writer.Write(cell.Amplitude); writer.Write(cell.Born); writer.Write(cell.Life);
            }

            var waves = Waves;
            writer.Write(waves.Steps); writer.Write(waves.Slice);
            writer.Write(waves.SynopticX); writer.Write(waves.SynopticY);
            foreach (var value in waves.Outer) writer.Write(value);
            var arrays = new[] { waves.Energy, waves.WindX, waves.WindY, waves.Foam, waves.TailFactor, waves.SampleX, waves.SampleY, waves.GustSample };
            var length = 0;
            foreach (var array in arrays) length += array.Length;
            var raw = new byte[length * 4];
            var offset = 0;
            foreach (var array in arrays)
            {
                Buffer.BlockCopy(array, 0, raw, offset, array.Length * 4);
                offset += array.Length * 4;
            }
            byte[] packed;
            using (var memory = new MemoryStream())
            {
                using (var deflate = new DeflateStream(memory, CompressionLevel.Fastest, true)) deflate.Write(raw, 0, raw.Length);
                packed = memory.ToArray();
            }
            writer.Write(raw.Length);
            writer.Write(packed.Length);
            writer.Write(packed);
        }

        internal static MarineState Read(BinaryReader reader, World world)
        {
            var climate = MarineClimate.Read(reader);
            var seed = reader.ReadUInt64();
            var movement = reader.ReadBoolean();
            var state = new MarineState(world, climate, seed, movement);

            var wind = state.Wind;
            wind.Time = reader.ReadDouble(); wind.DirectionOffset = reader.ReadDouble(); wind.SpeedFactor = reader.ReadDouble();
            wind.DriftX = reader.ReadDouble(); wind.DriftY = reader.ReadDouble(); wind.Random.State = reader.ReadUInt64();
            var cells = reader.ReadInt32();
            if (cells < 0 || cells > 10000) throw new InvalidDataException("Damaged wind data.");
            for (var i = 0; i < cells; i++)
                wind.Cells.Add(new WindCell
                {
                    SpawnX = reader.ReadDouble(), SpawnY = reader.ReadDouble(), BornDriftX = reader.ReadDouble(), BornDriftY = reader.ReadDouble(),
                    Radius = reader.ReadDouble(), Amplitude = reader.ReadDouble(), Born = reader.ReadDouble(), Life = reader.ReadDouble(),
                });

            var waves = state.Waves;
            waves.Steps = reader.ReadInt32(); waves.Slice = reader.ReadInt32();
            if (waves.Slice < 0 || waves.Slice >= WaveField.TicksPerStep) throw new InvalidDataException("Damaged wave data.");
            waves.SynopticX = reader.ReadDouble(); waves.SynopticY = reader.ReadDouble();
            for (var b = 0; b < waves.Outer.Length; b++) waves.Outer[b] = reader.ReadDouble();
            var rawLength = reader.ReadInt32();
            var packedLength = reader.ReadInt32();
            var arrays = new[] { waves.Energy, waves.WindX, waves.WindY, waves.Foam, waves.TailFactor, waves.SampleX, waves.SampleY, waves.GustSample };
            var expected = 0;
            foreach (var array in arrays) expected += array.Length * 4;
            if (rawLength != expected || packedLength < 0) throw new InvalidDataException("Damaged wave data.");
            var raw = new byte[rawLength];
            using (var deflate = new DeflateStream(new MemoryStream(reader.ReadBytes(packedLength)), CompressionMode.Decompress))
            {
                var read = 0;
                while (read < rawLength)
                {
                    var n = deflate.Read(raw, read, rawLength - read);
                    if (n <= 0) throw new InvalidDataException("Damaged wave data.");
                    read += n;
                }
            }
            var offset = 0;
            foreach (var array in arrays)
            {
                Buffer.BlockCopy(raw, offset, array, 0, array.Length * 4);
                offset += array.Length * 4;
            }
            waves.Restore();
            return state;
        }
    }
}
