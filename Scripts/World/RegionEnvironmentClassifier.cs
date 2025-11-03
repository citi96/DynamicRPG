using System;

#nullable enable

namespace DynamicRPG.World;

/// <summary>
/// Provides helper methods to classify region environment types into shared categories.
/// </summary>
public static class RegionEnvironmentClassifier
{
    private static readonly (RegionEnvironmentCategory Category, string[] Keywords)[] KeywordMappings =
    {
        (RegionEnvironmentCategory.Arctic, new[] { "tundra", "ghiacc", "mont" }),
        (RegionEnvironmentCategory.Forest, new[] { "forest", "bosco" }),
        (RegionEnvironmentCategory.Desert, new[] { "deserto", "dune" }),
        (RegionEnvironmentCategory.Swamp, new[] { "palud", "bruma" }),
        (RegionEnvironmentCategory.Ruins, new[] { "rovine", "citt" }),
    };

    /// <summary>
    /// Resolves the broad environment category for the provided environment description.
    /// </summary>
    public static RegionEnvironmentCategory FromEnvironment(string? environmentType)
    {
        if (string.IsNullOrWhiteSpace(environmentType))
        {
            return RegionEnvironmentCategory.Generic;
        }

        foreach (var (category, keywords) in KeywordMappings)
        {
            if (MatchesAnyKeyword(environmentType, keywords))
            {
                return category;
            }
        }

        return RegionEnvironmentCategory.Generic;
    }

    private static bool MatchesAnyKeyword(string environmentType, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (environmentType.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public enum RegionEnvironmentCategory
{
    Generic = 0,
    Arctic,
    Forest,
    Desert,
    Swamp,
    Ruins,
}
