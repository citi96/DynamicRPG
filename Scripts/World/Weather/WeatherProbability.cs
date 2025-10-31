namespace DynamicRPG.World.Weather;

using System;

/// <summary>
/// Represents the likelihood of a specific weather condition occurring.
/// </summary>
public sealed class WeatherProbability
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherProbability"/> class.
    /// </summary>
    /// <param name="condition">Weather condition represented by this entry.</param>
    /// <param name="weight">Relative weight for the condition. Must be non-negative.</param>
    public WeatherProbability(WeatherCondition condition, double weight)
    {
        if (weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Il peso di probabilità deve essere non negativo.");
        }

        Condition = condition;
        Weight = weight;
    }

    /// <summary>
    /// Gets the represented weather condition.
    /// </summary>
    public WeatherCondition Condition { get; }

    /// <summary>
    /// Gets the relative weight associated with the condition.
    /// </summary>
    public double Weight { get; }
}
