using Godot;

#nullable enable

namespace DynamicRPG.World.Exploration;

/// <summary>
/// Describes the color palette and procedural parameters for an exploration biome.
/// </summary>
public readonly record struct ExplorationTheme(
    Color GrassColor,
    Color PathColor,
    Color WaterColor,
    Color StoneColor,
    int TreeCount,
    float NoiseFrequency);

/// <summary>
/// Resolves an <see cref="ExplorationTheme"/> based on the current region environment.
/// </summary>
public static class ExplorationThemeResolver
{
    private static readonly ExplorationTheme DefaultTheme = new(
        new Color(0.188f, 0.353f, 0.220f),
        new Color(0.474f, 0.349f, 0.204f),
        new Color(0.137f, 0.337f, 0.522f),
        new Color(0.396f, 0.396f, 0.396f),
        180,
        0.05f);

    public static ExplorationTheme Resolve(string environmentType)
    {
        if (string.IsNullOrWhiteSpace(environmentType))
        {
            return DefaultTheme;
        }

        var lowered = environmentType.ToLowerInvariant();

        if (lowered.Contains("foresta") || lowered.Contains("bosco"))
        {
            return new ExplorationTheme(
                new Color(0.16f, 0.42f, 0.21f),
                new Color(0.43f, 0.29f, 0.14f),
                new Color(0.11f, 0.27f, 0.45f),
                new Color(0.28f, 0.28f, 0.28f),
                240,
                0.042f);
        }

        if (lowered.Contains("deserto") || lowered.Contains("dune"))
        {
            return new ExplorationTheme(
                new Color(0.86f, 0.75f, 0.51f),
                new Color(0.78f, 0.62f, 0.34f),
                new Color(0.23f, 0.52f, 0.67f),
                new Color(0.58f, 0.48f, 0.34f),
                36,
                0.032f);
        }

        if (lowered.Contains("tundra") || lowered.Contains("ghiacci"))
        {
            return new ExplorationTheme(
                new Color(0.78f, 0.86f, 0.92f),
                new Color(0.69f, 0.74f, 0.78f),
                new Color(0.42f, 0.62f, 0.78f),
                new Color(0.76f, 0.8f, 0.82f),
                48,
                0.06f);
        }

        if (lowered.Contains("palud") || lowered.Contains("bruma"))
        {
            return new ExplorationTheme(
                new Color(0.2f, 0.32f, 0.21f),
                new Color(0.31f, 0.23f, 0.13f),
                new Color(0.12f, 0.24f, 0.28f),
                new Color(0.24f, 0.27f, 0.24f),
                200,
                0.048f);
        }

        if (lowered.Contains("rovine") || lowered.Contains("citt"))
        {
            return new ExplorationTheme(
                new Color(0.35f, 0.35f, 0.37f),
                new Color(0.42f, 0.32f, 0.26f),
                new Color(0.18f, 0.3f, 0.46f),
                new Color(0.5f, 0.5f, 0.52f),
                110,
                0.045f);
        }

        return DefaultTheme;
    }
}
