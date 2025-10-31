namespace DynamicRPG.World.Weather;

using System;
using System.Collections.Generic;

/// <summary>
/// Provides weather profiles based on the climate of a region.
/// </summary>
public static class WeatherProfileProvider
{
    private static readonly IReadOnlyDictionary<RegionClimate, RegionWeatherProfile> Profiles =
        new Dictionary<RegionClimate, RegionWeatherProfile>
        {
            [RegionClimate.Temperate] = new RegionWeatherProfile(
                RegionClimate.Temperate,
                new[]
                {
                    new WeatherProbability(WeatherCondition.Clear, 0.45),
                    new WeatherProbability(WeatherCondition.Rain, 0.3),
                    new WeatherProbability(WeatherCondition.Storm, 0.1),
                    new WeatherProbability(WeatherCondition.Snow, 0.05),
                    new WeatherProbability(WeatherCondition.Fog, 0.1),
                }),
            [RegionClimate.Arid] = new RegionWeatherProfile(
                RegionClimate.Arid,
                new[]
                {
                    new WeatherProbability(WeatherCondition.Clear, 0.85),
                    new WeatherProbability(WeatherCondition.Rain, 0.1),
                    new WeatherProbability(WeatherCondition.Storm, 0.0),
                    new WeatherProbability(WeatherCondition.Snow, 0.0),
                    new WeatherProbability(WeatherCondition.Fog, 0.05),
                }),
            [RegionClimate.Alpine] = new RegionWeatherProfile(
                RegionClimate.Alpine,
                new[]
                {
                    new WeatherProbability(WeatherCondition.Clear, 0.35),
                    new WeatherProbability(WeatherCondition.Rain, 0.2),
                    new WeatherProbability(WeatherCondition.Storm, 0.15),
                    new WeatherProbability(WeatherCondition.Snow, 0.25),
                    new WeatherProbability(WeatherCondition.Fog, 0.05),
                }),
            [RegionClimate.Polar] = new RegionWeatherProfile(
                RegionClimate.Polar,
                new[]
                {
                    new WeatherProbability(WeatherCondition.Clear, 0.25),
                    new WeatherProbability(WeatherCondition.Rain, 0.05),
                    new WeatherProbability(WeatherCondition.Storm, 0.1),
                    new WeatherProbability(WeatherCondition.Snow, 0.5),
                    new WeatherProbability(WeatherCondition.Fog, 0.1),
                }),
            [RegionClimate.Humid] = new RegionWeatherProfile(
                RegionClimate.Humid,
                new[]
                {
                    new WeatherProbability(WeatherCondition.Clear, 0.2),
                    new WeatherProbability(WeatherCondition.Rain, 0.4),
                    new WeatherProbability(WeatherCondition.Storm, 0.15),
                    new WeatherProbability(WeatherCondition.Snow, 0.0),
                    new WeatherProbability(WeatherCondition.Fog, 0.25),
                }),
            [RegionClimate.Ruined] = new RegionWeatherProfile(
                RegionClimate.Ruined,
                new[]
                {
                    new WeatherProbability(WeatherCondition.Clear, 0.3),
                    new WeatherProbability(WeatherCondition.Rain, 0.25),
                    new WeatherProbability(WeatherCondition.Storm, 0.2),
                    new WeatherProbability(WeatherCondition.Snow, 0.05),
                    new WeatherProbability(WeatherCondition.Fog, 0.2),
                }),
        };

    /// <summary>
    /// Retrieves the weather profile associated with the specified climate.
    /// </summary>
    /// <param name="climate">Target climate.</param>
    /// <returns>The corresponding <see cref="RegionWeatherProfile"/>.</returns>
    public static RegionWeatherProfile GetProfile(RegionClimate climate)
    {
        if (!Profiles.TryGetValue(climate, out var profile))
        {
            throw new ArgumentOutOfRangeException(nameof(climate), climate, "Clima non supportato per la generazione del meteo.");
        }

        return profile;
    }
}
