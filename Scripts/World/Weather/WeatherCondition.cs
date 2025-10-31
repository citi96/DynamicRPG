namespace DynamicRPG.World.Weather;

/// <summary>
/// Enumerates the supported weather states applied to regions.
/// </summary>
public enum WeatherCondition
{
    /// <summary>
    /// Clear skies without precipitation.
    /// </summary>
    Clear,

    /// <summary>
    /// Light to moderate rain.
    /// </summary>
    Rain,

    /// <summary>
    /// Intense thunderstorms bringing heavy rain and lightning.
    /// </summary>
    Storm,

    /// <summary>
    /// Snowfall conditions.
    /// </summary>
    Snow,

    /// <summary>
    /// Thick fog reducing visibility.
    /// </summary>
    Fog,
}
