namespace DynamicRPG.World;

using System;
using System.Collections.Generic;
using DynamicRPG.World.Hazards;
using DynamicRPG.World.Locations;
using DynamicRPG.World.Weather;

#nullable enable

/// <summary>
/// Represents a named world region grouping several locations together.
/// </summary>
[Serializable]
public sealed class Region
{
    /// <summary>
    /// Human-readable name of the region.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Describes the biome or environment type of the region (e.g. Forest, Desert).
    /// </summary>
    public string EnvironmentType { get; set; } = string.Empty;

    /// <summary>
    /// Base climate used to determine weather probabilities.
    /// </summary>
    public RegionClimate BaseClimate { get; set; } = RegionClimate.Temperate;

    /// <summary>
    /// Current weather affecting the region.
    /// </summary>
    public WeatherCondition CurrentWeather { get; private set; } = WeatherCondition.Clear;

    /// <summary>
    /// Flag indicating whether the weather has been initialized at least once.
    /// </summary>
    public bool HasWeatherBeenInitialized { get; private set; }

    /// <summary>
    /// Locations that belong to this region.
    /// </summary>
    public List<Location> Locations { get; } = new();

    /// <summary>
    /// Environmental hazards that affect travel within or around the region.
    /// </summary>
    public List<Hazard> EnvironmentalHazards { get; } = new();

    /// <summary>
    /// Identifies the faction currently in control of the region. Placeholder for a dedicated Faction type.
    /// </summary>
    public string? ControllingFaction { get; set; }

    /// <summary>
    /// Applies the provided weather condition to the region, updating initialization state accordingly.
    /// </summary>
    /// <param name="condition">Weather condition to apply.</param>
    public void ApplyWeather(WeatherCondition condition)
    {
        CurrentWeather = condition;
        HasWeatherBeenInitialized = true;
    }
}
