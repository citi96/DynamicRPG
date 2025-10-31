namespace DynamicRPG.World.Weather;

using System;
using System.Globalization;

/// <summary>
/// Resolves a region climate from textual environment descriptions.
/// </summary>
public static class RegionClimateResolver
{
    /// <summary>
    /// Determines the most appropriate <see cref="RegionClimate"/> for the provided environment description.
    /// </summary>
    /// <param name="environmentDescription">Human readable environment description.</param>
    /// <returns>The inferred climate.</returns>
    public static RegionClimate Resolve(string environmentDescription)
    {
        if (environmentDescription is null)
        {
            throw new ArgumentNullException(nameof(environmentDescription));
        }

        var normalized = environmentDescription.ToLower(CultureInfo.InvariantCulture);

        if (normalized.Contains("deserto", StringComparison.Ordinal) || normalized.Contains("dune", StringComparison.Ordinal))
        {
            return RegionClimate.Arid;
        }

        if (normalized.Contains("palud", StringComparison.Ordinal) || normalized.Contains("bruma", StringComparison.Ordinal) || normalized.Contains("miasma", StringComparison.Ordinal))
        {
            return RegionClimate.Humid;
        }

        if (normalized.Contains("mont", StringComparison.Ordinal))
        {
            return RegionClimate.Alpine;
        }

        if (normalized.Contains("tundra", StringComparison.Ordinal) || normalized.Contains("ghiacc", StringComparison.Ordinal))
        {
            return RegionClimate.Polar;
        }

        if (normalized.Contains("rovine", StringComparison.Ordinal) || normalized.Contains("citt", StringComparison.Ordinal))
        {
            return RegionClimate.Ruined;
        }

        if (normalized.Contains("foresta", StringComparison.Ordinal))
        {
            return RegionClimate.Temperate;
        }

        return RegionClimate.Temperate;
    }
}
