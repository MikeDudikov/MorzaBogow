using System;
using System.Collections.Generic;

namespace MorzaBogow.Settings
{
    /// <summary>Visual profiles from V10, ordered from lowest to highest quality level index.</summary>
    public enum GraphicsProfile
    {
        Low = 0,
        Medium = 1,
        High = 2,
        ExtraHigh = 3,
        Ultra = 4
    }

    public static class GraphicsProfiles
    {
        public const GraphicsProfile Default = GraphicsProfile.Ultra;
        public const GraphicsProfile SuggestionWithoutRayTracing = GraphicsProfile.ExtraHigh;

        public static readonly IReadOnlyList<GraphicsProfile> All = new[]
        {
            GraphicsProfile.Low, GraphicsProfile.Medium, GraphicsProfile.High,
            GraphicsProfile.ExtraHigh, GraphicsProfile.Ultra
        };

        public static string DisplayName(GraphicsProfile profile)
        {
            switch (profile)
            {
                case GraphicsProfile.Low: return "Low";
                case GraphicsProfile.Medium: return "Medium";
                case GraphicsProfile.High: return "High";
                case GraphicsProfile.ExtraHigh: return "Extra High";
                case GraphicsProfile.Ultra: return "Ultra";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        public static bool RequiresRayTracing(GraphicsProfile profile) => profile == GraphicsProfile.Ultra;

        /// <summary>Accepts a saved or command-line name; unknown values keep the default.</summary>
        public static GraphicsProfile ParseOrDefault(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Default;
            var compact = value.Replace(" ", string.Empty);
            foreach (var profile in All)
            {
                if (string.Equals(profile.ToString(), compact, StringComparison.OrdinalIgnoreCase)) return profile;
            }
            return Default;
        }
    }
}
