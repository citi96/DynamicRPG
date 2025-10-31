namespace DynamicRPG.World.Weather;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>
/// Encapsulates the probabilistic weather distribution for a specific climate.
/// </summary>
public sealed class RegionWeatherProfile
{
    private readonly IReadOnlyList<WeatherProbability> _probabilities;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegionWeatherProfile"/> class.
    /// </summary>
    /// <param name="climate">Climate associated with this profile.</param>
    /// <param name="probabilities">Weather probability weights for the profile.</param>
    public RegionWeatherProfile(RegionClimate climate, IEnumerable<WeatherProbability> probabilities)
    {
        Climate = climate;
        _probabilities = new ReadOnlyCollection<WeatherProbability>(probabilities.ToArray());

        if (_probabilities.Count == 0)
        {
            throw new ArgumentException("Un profilo meteo deve contenere almeno una probabilità.", nameof(probabilities));
        }
    }

    /// <summary>
    /// Gets the climate governed by this profile.
    /// </summary>
    public RegionClimate Climate { get; }

    /// <summary>
    /// Gets the collection of configured probabilities.
    /// </summary>
    public IReadOnlyList<WeatherProbability> Probabilities => _probabilities;

    /// <summary>
    /// Picks a weather condition based on the configured probability weights.
    /// </summary>
    /// <param name="random">Random generator used for sampling.</param>
    /// <returns>The randomly selected weather condition.</returns>
    public WeatherCondition Sample(Random random)
    {
        if (random is null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        var totalWeight = _probabilities.Sum(probability => probability.Weight);
        if (totalWeight <= 0)
        {
            return _probabilities[0].Condition;
        }

        var roll = random.NextDouble() * totalWeight;
        var cumulative = 0d;

        foreach (var probability in _probabilities)
        {
            cumulative += probability.Weight;
            if (roll <= cumulative)
            {
                return probability.Condition;
            }
        }

        return _probabilities[^1].Condition;
    }
}
