using System;
using System.Collections.Generic;

namespace MorzaBogow.Simulation.Effects
{
    public enum EffectResultCode : byte
    {
        Ok = 1,
        /// <summary>The contract or the executor refuses this use; nothing was changed.</summary>
        Rejected = 2,
        /// <summary>No executor carries this kind of effect out yet. Never a silent success.</summary>
        NotImplemented = 3,
    }

    public enum EffectRejection : byte
    {
        None = 0,
        BrokenDefinition = 1,
        BrokenRecord = 2,
        DefinitionMismatch = 3,
        OwnerLimit = 4,
        IllegalTarget = 5,
        SourceLost = 6,
        WrongPhase = 7,
        /// <summary>A reason of the executor's own, outside the shared contract.</summary>
        Executor = 8,
    }

    public readonly struct EffectResult : IEquatable<EffectResult>
    {
        public readonly EffectResultCode Code;
        public readonly EffectRejection Reason;

        EffectResult(EffectResultCode code, EffectRejection reason) { Code = code; Reason = reason; }

        public static readonly EffectResult Ok = new EffectResult(EffectResultCode.Ok, EffectRejection.None);
        public static readonly EffectResult NotImplemented = new EffectResult(EffectResultCode.NotImplemented, EffectRejection.None);

        public static EffectResult Rejected(EffectRejection reason)
        {
            if (reason == EffectRejection.None) throw new ArgumentException("A rejection has a reason.", nameof(reason));
            return new EffectResult(EffectResultCode.Rejected, reason);
        }

        public bool IsOk => Code == EffectResultCode.Ok;

        public bool Equals(EffectResult other) => Code == other.Code && Reason == other.Reason;
        public override bool Equals(object obj) => obj is EffectResult other && Equals(other);
        public override int GetHashCode() => (int)Code << 8 | (int)Reason;
        public override string ToString() => Code == EffectResultCode.Rejected ? $"Rejected({Reason})" : Code.ToString();
    }

    /// <summary>What an executor is given. Everything is simulation state; nothing refers to a scene.</summary>
    public struct EffectContext
    {
        /// <summary>The world the effect acts in. Null until the executor is joined to World (P07).</summary>
        public World World;
        public EffectDefinition Definition;
        public EffectInstance Instance;
        /// <summary>All effects of the world, the instance itself included once it has been added.</summary>
        public IReadOnlyList<EffectInstance> Effects;
        public int Tick;
    }

    /// <summary>
    /// Carries out one kind of effect over its life: use, start, every tick while active, and end.
    /// Validate changes nothing and is asked before payment; the other three change the simulation only.
    /// </summary>
    public interface IEffectExecutor
    {
        EffectKind Kind { get; }
        EffectResult Validate(in EffectContext context);
        EffectResult Activate(in EffectContext context);
        EffectResult Step(in EffectContext context);
        EffectResult Expire(in EffectContext context);
    }

    /// <summary>
    /// The executors by kind. A kind without one answers NotImplemented to everything, so a definition that is only
    /// data can never be taken for a working effect. Neither is a definition still marked ContractOnly carried out, whatever
    /// is registered for its kind. P02 registers none.
    /// </summary>
    public sealed class EffectExecutors
    {
        readonly Dictionary<EffectKind, IEffectExecutor> byKind = new Dictionary<EffectKind, IEffectExecutor>();

        public void Register(IEffectExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (executor.Kind == EffectKind.None || !Enum.IsDefined(typeof(EffectKind), executor.Kind))
                throw new ArgumentException("An executor carries out one existing kind.", nameof(executor));
            if (byKind.ContainsKey(executor.Kind)) throw new InvalidOperationException($"{executor.Kind} already has an executor.");
            byKind.Add(executor.Kind, executor);
        }

        public bool IsImplemented(EffectKind kind) => byKind.ContainsKey(kind);

        IEffectExecutor For(EffectDefinition definition) => byKind.TryGetValue(definition.Kind, out var executor) ? executor : null;

        /// <summary>The shared contract first (definition, record, owner's limit), then the kind's executor.</summary>
        public EffectResult Validate(in EffectContext context)
        {
            var contract = Contract(context);
            if (!contract.IsOk) return contract;
            if (context.Definition.Status != EffectStatus.Executable) return EffectResult.NotImplemented;
            if (!context.Instance.IsPendingOrActiveAt(context.Tick)) return EffectResult.Rejected(EffectRejection.WrongPhase);
            // The limit counts the others: the instance may or may not be in the list yet.
            var definition = context.Definition;
            if (definition.MaxActivePerOwner > 0 && context.Effects != null)
            {
                var held = 0;
                for (var i = 0; i < context.Effects.Count; i++)
                {
                    var other = context.Effects[i];
                    if (other.Id != context.Instance.Id && other.Owner == context.Instance.Owner && other.DefinitionId == definition.Id
                        && other.IsPendingOrActiveAt(context.Tick)) held++;
                }
                if (held >= definition.MaxActivePerOwner) return EffectResult.Rejected(EffectRejection.OwnerLimit);
            }
            return For(definition)?.Validate(context) ?? EffectResult.NotImplemented;
        }

        public EffectResult Activate(in EffectContext context)
        {
            var contract = Contract(context);
            if (!contract.IsOk) return contract;
            if (context.Definition.Status != EffectStatus.Executable) return EffectResult.NotImplemented;
            if (!context.Instance.IsActiveAt(context.Tick)) return EffectResult.Rejected(EffectRejection.WrongPhase);
            return For(context.Definition)?.Activate(context) ?? EffectResult.NotImplemented;
        }

        public EffectResult Step(in EffectContext context)
        {
            var contract = Contract(context);
            if (!contract.IsOk) return contract;
            if (context.Definition.Status != EffectStatus.Executable) return EffectResult.NotImplemented;
            if (!context.Instance.IsActiveAt(context.Tick)) return EffectResult.Rejected(EffectRejection.WrongPhase);
            return For(context.Definition)?.Step(context) ?? EffectResult.NotImplemented;
        }

        public EffectResult Expire(in EffectContext context)
        {
            var contract = Contract(context);
            if (!contract.IsOk) return contract;
            if (context.Definition.Status != EffectStatus.Executable) return EffectResult.NotImplemented;
            var phase = context.Instance.PhaseAt(context.Tick);
            if (phase != EffectPhase.Expired && phase != EffectPhase.Cancelled) return EffectResult.Rejected(EffectRejection.WrongPhase);
            return For(context.Definition)?.Expire(context) ?? EffectResult.NotImplemented;
        }

        static EffectResult Contract(in EffectContext context)
        {
            if (context.Definition == null || context.Definition.Problem() != null) return EffectResult.Rejected(EffectRejection.BrokenDefinition);
            if (context.Instance == null || context.Instance.Problem() != null) return EffectResult.Rejected(EffectRejection.BrokenRecord);
            if (context.Instance.DefinitionId != context.Definition.Id) return EffectResult.Rejected(EffectRejection.DefinitionMismatch);
            return EffectResult.Ok;
        }
    }
}
