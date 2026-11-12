using System;
using System.Collections.Generic;
using System.IO;

namespace MorzaBogow.Simulation.Effects
{
    // Saved as numbers: add at the end, never reorder. EffectPhase is derived from the timeline and never saved.

    /// <summary>What an effect comes from. Always an ID of simulation state, never a scene object.</summary>
    public enum EffectSourceKind : byte
    {
        None = 0,
        Sanctuary = 1,
        Settlement = 2,
        Ship = 3,
        Hero = 4,
        Artifact = 5,
        Creature = 6,
        Fort = 7,
        Scenario = 8,
    }

    public enum EffectTargetKind : byte
    {
        None = 0,
        /// <summary>One ship, structure or creature by its ID.</summary>
        Entity = 1,
        /// <summary>A circle on the sea.</summary>
        Area = 2,
        /// <summary>Everything of one owner slot.</summary>
        Side = 3,
        WholeMap = 4,
    }

    public enum EffectPhase : byte
    {
        /// <summary>Paid and shown, not yet doing anything.</summary>
        Announced = 1,
        Active = 2,
        /// <summary>Ran out of time.</summary>
        Expired = 3,
        /// <summary>Ended early: its source or target was lost, or it was dispelled.</summary>
        Cancelled = 4,
    }

    public struct EffectTarget : IEquatable<EffectTarget>
    {
        public EffectTargetKind Kind;
        /// <summary>The entity for Entity, the owner slot for Side, otherwise 0.</summary>
        public int EntityId;
        public double X, Y, Radius;

        public static EffectTarget Entity(int id) => new EffectTarget { Kind = EffectTargetKind.Entity, EntityId = id };
        public static EffectTarget Area(double x, double y, double radius) => new EffectTarget { Kind = EffectTargetKind.Area, X = x, Y = y, Radius = radius };
        public static EffectTarget Side(int owner) => new EffectTarget { Kind = EffectTargetKind.Side, EntityId = owner };
        public static EffectTarget WholeMap() => new EffectTarget { Kind = EffectTargetKind.WholeMap };

        public bool Equals(EffectTarget other) =>
            Kind == other.Kind && EntityId == other.EntityId && X.Equals(other.X) && Y.Equals(other.Y) && Radius.Equals(other.Radius);
        public override bool Equals(object obj) => obj is EffectTarget other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397 ^ EntityId) * 397 ^ X.GetHashCode() ^ Y.GetHashCode() ^ Radius.GetHashCode();
    }

    /// <summary>
    /// One use of an effect: the record that lives in the world and in the save. Its times are absolute ticks of the
    /// simulation, so a pause (which runs no tick) neither shortens nor prolongs it.
    /// </summary>
    public sealed class EffectInstance
    {
        /// <summary>EndTick of an effect that no clock ends (until the match ends, or while its source exists).</summary>
        public const int NoEndTick = int.MaxValue;
        /// <summary>CancelTick of an effect nobody has ended early.</summary>
        public const int NotCancelled = -1;

        public int Id;
        /// <summary>The catalogue's text ID of the definition, e.g. "M11".</summary>
        public string DefinitionId;
        public EffectSourceKind SourceKind;
        public int SourceId;
        /// <summary>Owner slot 0..2.</summary>
        public int Owner;
        public EffectTarget Target;
        /// <summary>The tick of the paid use, the first tick it works, and the first tick it no longer does.</summary>
        public int AnnounceTick, StartTick, EndTick;
        /// <summary>The first tick it no longer exists because it was ended early, or <see cref="NotCancelled"/>.</summary>
        public int CancelTick = NotCancelled;
        /// <summary>The ID of the use that made it; whatever it settles is settled once for this ID.</summary>
        public long EventId;

        /// <summary>A new record for a use paid at <paramref name="nowTick"/>, with its whole timeline fixed at once.</summary>
        public static EffectInstance Schedule(
            EffectDefinition definition, int id, long eventId, EffectSourceKind sourceKind, int sourceId, int owner, EffectTarget target, int nowTick)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var broken = definition.Problem();
            if (broken != null) throw new ArgumentException(broken, nameof(definition));
            if (!Owners.IsValid(owner)) throw new ArgumentOutOfRangeException(nameof(owner), owner, "Owner slot must be 0, 1 or 2.");
            var start = checked(nowTick + definition.AnnounceTicks);
            int end;
            switch (definition.Duration.Kind)
            {
                case EffectDurationKind.Ticks: end = checked(start + definition.Duration.Ticks); break;
                case EffectDurationKind.Instant: end = checked(start + 1); break;
                case EffectDurationKind.UntilMatchEnd:
                case EffectDurationKind.WhileSource: end = NoEndTick; break;
                default: throw new ArgumentException($"{definition.Id}: no duration.", nameof(definition));
            }
            return new EffectInstance
            {
                Id = id, DefinitionId = definition.Id, SourceKind = sourceKind, SourceId = sourceId, Owner = owner, Target = target,
                AnnounceTick = nowTick, StartTick = start, EndTick = end, EventId = eventId,
            };
        }

        /// <summary>
        /// The phase at a tick. It is never stored: it follows from the timeline and the cancel tick alone, so a save
        /// cannot hold a phase that disagrees with its times.
        /// </summary>
        public EffectPhase PhaseAt(int tick)
        {
            if (CancelTick != NotCancelled && tick >= CancelTick) return EffectPhase.Cancelled;
            if (tick < StartTick) return EffectPhase.Announced;
            return tick < EndTick ? EffectPhase.Active : EffectPhase.Expired;
        }

        /// <summary>
        /// Ends the effect early from this tick on. False, changing nothing, when it is already over or cancelled, or
        /// the tick is before its announcement.
        /// </summary>
        public bool Cancel(int tick)
        {
            if (CancelTick != NotCancelled || tick < AnnounceTick || tick >= EndTick) return false;
            CancelTick = tick;
            return true;
        }

        public bool IsActiveAt(int tick) => PhaseAt(tick) == EffectPhase.Active;

        /// <summary>Announced or active: it still takes one of its owner's places.</summary>
        public bool IsPendingOrActiveAt(int tick)
        {
            var phase = PhaseAt(tick);
            return phase == EffectPhase.Announced || phase == EffectPhase.Active;
        }

        /// <summary>Null when the record is one a world may hold, otherwise what is damaged.</summary>
        public string Problem()
        {
            if (Id <= 0) return "Effect without an ID.";
            if (string.IsNullOrWhiteSpace(DefinitionId)) return $"Effect {Id}: no definition.";
            if (!Owners.IsValid(Owner)) return $"Effect {Id}: owner {Owner} is not one of the three slots.";
            if (SourceKind == EffectSourceKind.None || !Enum.IsDefined(typeof(EffectSourceKind), SourceKind)) return $"Effect {Id}: unknown source.";
            if (Target.Kind == EffectTargetKind.None || !Enum.IsDefined(typeof(EffectTargetKind), Target.Kind)) return $"Effect {Id}: unknown target.";
            if (Target.Kind == EffectTargetKind.Side && !Owners.IsValid(Target.EntityId)) return $"Effect {Id}: targets a side that does not exist.";
            if (Target.Kind == EffectTargetKind.Entity && Target.EntityId <= 0) return $"Effect {Id}: targets nothing.";
            if (!IsFinite(Target.X) || !IsFinite(Target.Y) || !IsFinite(Target.Radius) || Target.Radius < 0) return $"Effect {Id}: damaged area.";
            if (AnnounceTick < 0 || StartTick < AnnounceTick || EndTick <= StartTick) return $"Effect {Id}: damaged timeline.";
            if (CancelTick != NotCancelled && (CancelTick < AnnounceTick || CancelTick >= EndTick)) return $"Effect {Id}: cancelled outside its life.";
            return null;
        }

        static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// The owner's limit of one definition (e.g. three own reefs): counted before payment, over announced and active uses.
    /// </summary>
    public static class EffectLimits
    {
        public static int CountHeld(EffectDefinition definition, int owner, IReadOnlyList<EffectInstance> effects, int tick)
        {
            var count = 0;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect.Owner == owner && effect.DefinitionId == definition.Id && effect.IsPendingOrActiveAt(tick)) count++;
            }
            return count;
        }

        public static bool HasRoom(EffectDefinition definition, int owner, IReadOnlyList<EffectInstance> effects, int tick) =>
            definition.MaxActivePerOwner == 0 || CountHeld(definition, owner, effects, tick) < definition.MaxActivePerOwner;
    }

    /// <summary>
    /// Binary form of the effect records. Self-contained: it is not part of SaveGame yet (joining it there changes the
    /// save's contract and raises SaveGame.Version, P07). Reading rejects anything a world could not legally hold.
    /// </summary>
    public static class EffectSerializer
    {
        /// <summary>Version of this block alone. Raise it with any change to what is written below.</summary>
        public const byte FormatVersion = 2;
        public const int MaxRecords = 4096;
        public const int MaxDefinitionIdLength = 64;

        public static void Write(BinaryWriter writer, IReadOnlyList<EffectInstance> effects)
        {
            if (effects.Count > MaxRecords) throw new InvalidOperationException("Too many effects to save.");
            writer.Write(FormatVersion);
            writer.Write(effects.Count);
            var ids = new HashSet<int>();
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                var problem = effect.Problem();
                if (problem != null) throw new InvalidOperationException(problem);
                if (effect.DefinitionId.Length > MaxDefinitionIdLength) throw new InvalidOperationException($"Effect {effect.Id}: definition ID too long.");
                if (!ids.Add(effect.Id)) throw new InvalidOperationException($"Effect {effect.Id} twice.");
                writer.Write(effect.Id);
                writer.Write(effect.DefinitionId);
                writer.Write((byte)effect.SourceKind);
                writer.Write(effect.SourceId);
                writer.Write(effect.Owner);
                writer.Write((byte)effect.Target.Kind);
                writer.Write(effect.Target.EntityId);
                writer.Write(effect.Target.X);
                writer.Write(effect.Target.Y);
                writer.Write(effect.Target.Radius);
                writer.Write(effect.AnnounceTick);
                writer.Write(effect.StartTick);
                writer.Write(effect.EndTick);
                writer.Write(effect.CancelTick);
                writer.Write(effect.EventId);
            }
        }

        public static List<EffectInstance> Read(BinaryReader reader)
        {
            var version = reader.ReadByte();
            if (version != FormatVersion) throw new InvalidDataException($"Unsupported effect block version {version}.");
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxRecords) throw new InvalidDataException("Damaged effects.");
            var effects = new List<EffectInstance>(count);
            var ids = new HashSet<int>();
            for (var i = 0; i < count; i++)
            {
                var effect = new EffectInstance { Id = reader.ReadInt32(), DefinitionId = reader.ReadString() };
                effect.SourceKind = (EffectSourceKind)reader.ReadByte();
                effect.SourceId = reader.ReadInt32();
                effect.Owner = reader.ReadInt32();
                effect.Target.Kind = (EffectTargetKind)reader.ReadByte();
                effect.Target.EntityId = reader.ReadInt32();
                effect.Target.X = reader.ReadDouble();
                effect.Target.Y = reader.ReadDouble();
                effect.Target.Radius = reader.ReadDouble();
                effect.AnnounceTick = reader.ReadInt32();
                effect.StartTick = reader.ReadInt32();
                effect.EndTick = reader.ReadInt32();
                effect.CancelTick = reader.ReadInt32();
                effect.EventId = reader.ReadInt64();
                var problem = effect.Problem();
                if (problem != null) throw new InvalidDataException(problem);
                if (effect.DefinitionId.Length > MaxDefinitionIdLength) throw new InvalidDataException($"Effect {effect.Id}: definition ID too long.");
                if (!ids.Add(effect.Id)) throw new InvalidDataException($"Effect {effect.Id} twice.");
                effects.Add(effect);
            }
            return effects;
        }
    }
}
