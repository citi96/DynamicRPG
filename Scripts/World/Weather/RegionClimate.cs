namespace DynamicRPG.World.Weather;

/// <summary>
/// Represents the dominant climate of a region. Drives weather generation probabilities.
/// </summary>
public enum RegionClimate
{
    /// <summary>
    /// Mild climate with balanced seasons (e.g. deciduous forests, plains).
    /// </summary>
    Temperate,

    /// <summary>
    /// Hot and dry areas such as deserts and scorched badlands.
    /// </summary>
    Arid,

    /// <summary>
    /// Cold high-altitude climates commonly found on mountain ranges.
    /// </summary>
    Alpine,

    /// <summary>
    /// Frigid and windswept biomes such as tundra or permafrost.
    /// </summary>
    Polar,

    /// <summary>
    /// Moist and mist-laden zones like swamps or marshlands.
    /// </summary>
    Humid,

    /// <summary>
    /// Ruined or arcane-infused landscapes where fog and storms are frequent.
    /// </summary>
    Ruined,
}
