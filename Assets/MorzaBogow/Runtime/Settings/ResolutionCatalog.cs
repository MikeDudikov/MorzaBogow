using System;
using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Settings
{
    /// <summary>A display resolution in physical pixels (V13).</summary>
    public readonly struct DisplayModeSize : IEquatable<DisplayModeSize>
    {
        public DisplayModeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
        public long Area => (long)Width * Height;

        public bool Equals(DisplayModeSize other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is DisplayModeSize other && Equals(other);
        public override int GetHashCode() => (Width * 397) ^ Height;
        public override string ToString() => $"{Width}x{Height}";
    }

    /// <summary>Filters monitor modes to the supported range from 1920x1080 to 2560x1440 (V13).</summary>
    public static class ResolutionCatalog
    {
        public const int MinWidth = 1920;
        public const int MaxWidth = 2560;
        public const int MinHeight = 1080;
        public const int MaxHeight = 1440;

        public static bool IsInRange(DisplayModeSize mode) =>
            mode.Width >= MinWidth && mode.Width <= MaxWidth &&
            mode.Height >= MinHeight && mode.Height <= MaxHeight;

        /// <summary>Returns only modes the device reports, within range, unique and sorted ascending.</summary>
        public static IReadOnlyList<DisplayModeSize> Build(IEnumerable<DisplayModeSize> reported)
        {
            if (reported == null) throw new ArgumentNullException(nameof(reported));
            return reported.Where(IsInRange).Distinct()
                .OrderBy(mode => mode.Area).ThenBy(mode => mode.Width).ToList();
        }

        /// <summary>First launch picks the highest available mode.</summary>
        public static DisplayModeSize? PickDefault(IReadOnlyList<DisplayModeSize> available) =>
            available.Count == 0 ? (DisplayModeSize?)null
                : available.OrderByDescending(mode => mode.Area).ThenByDescending(mode => mode.Width).First();

        /// <summary>Keeps the saved mode when available, otherwise chooses the closest pixel count.</summary>
        public static DisplayModeSize? PickFallback(DisplayModeSize saved, IReadOnlyList<DisplayModeSize> available)
        {
            if (available.Count == 0) return null;
            if (available.Contains(saved)) return saved;
            return available.OrderBy(mode => Math.Abs(mode.Area - saved.Area))
                .ThenByDescending(mode => mode.Area).First();
        }
    }

    /// <summary>The game opens in full screen unless the command line or a saved choice says otherwise.</summary>
    public static class DisplayStartup
    {
        public static bool WantsFullscreen(string[] commandLine, bool? savedFullscreen)
        {
            var index = Array.IndexOf(commandLine, "-screen-fullscreen");
            if (index >= 0 && index + 1 < commandLine.Length && commandLine[index + 1] == "0") return false;
            if (Array.IndexOf(commandLine, "-window") >= 0 || Array.IndexOf(commandLine, "-windowed") >= 0) return false;
            return savedFullscreen ?? true;
        }
    }

    public enum ResolutionChangeState
    {
        Idle,
        AwaitingConfirmation,
        Confirmed,
        Reverted
    }

    /// <summary>Tracks the 15 second confirmation window after a resolution change (V13).</summary>
    public sealed class ResolutionChangeSession
    {
        public const float ConfirmationSeconds = 15f;

        public ResolutionChangeState State { get; private set; } = ResolutionChangeState.Idle;
        public DisplayModeSize Previous { get; private set; }
        public DisplayModeSize Requested { get; private set; }
        public float RemainingSeconds { get; private set; }

        public void Begin(DisplayModeSize previous, DisplayModeSize requested)
        {
            if (State == ResolutionChangeState.AwaitingConfirmation)
                throw new InvalidOperationException("A resolution change is already waiting for confirmation.");
            Previous = previous;
            Requested = requested;
            RemainingSeconds = ConfirmationSeconds;
            State = ResolutionChangeState.AwaitingConfirmation;
        }

        /// <summary>Advances real time and returns true once when the change must be reverted.</summary>
        public bool Tick(float unscaledDeltaSeconds)
        {
            if (State != ResolutionChangeState.AwaitingConfirmation) return false;
            if (unscaledDeltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            RemainingSeconds = Math.Max(0f, RemainingSeconds - unscaledDeltaSeconds);
            if (RemainingSeconds > 0f) return false;
            State = ResolutionChangeState.Reverted;
            return true;
        }

        public void Confirm()
        {
            if (State != ResolutionChangeState.AwaitingConfirmation)
                throw new InvalidOperationException("No resolution change is waiting for confirmation.");
            State = ResolutionChangeState.Confirmed;
        }

        public void Revert()
        {
            if (State != ResolutionChangeState.AwaitingConfirmation)
                throw new InvalidOperationException("No resolution change is waiting for confirmation.");
            State = ResolutionChangeState.Reverted;
        }
    }
}
