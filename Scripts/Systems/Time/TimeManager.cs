using System;
using Godot;
using DynamicRPG.World;
using DynamicRPG.World.Weather;

namespace DynamicRPG.Systems.Time;

#nullable enable

/// <summary>
/// Manages the in-game calendar and time of day progression.
/// </summary>
public sealed class TimeManager
{
    private const int HoursPerDay = 24;
    private const int DaysPerMonth = 30;
    private const int MonthsPerYear = 12;

    private const double NightStartHour = 20.0;
    private const double NightEndHour = 6.0;

    private readonly Random _random;

    /// <summary>
    /// Triggered whenever a new in-game day begins.
    /// </summary>
    public event Action<int>? OnNewDay;

    /// <summary>
    /// Current hour of the day (0-23.99...).
    /// </summary>
    public double CurrentHour { get; private set; }

    /// <summary>
    /// Current day of the month (starting at 1).
    /// </summary>
    public int CurrentDay { get; private set; }

    /// <summary>
    /// Current month of the year (starting at 1).
    /// </summary>
    public int CurrentMonth { get; private set; }

    /// <summary>
    /// Current year (starting at 1).
    /// </summary>
    public int CurrentYear { get; private set; }

    /// <summary>
    /// Indicates whether the current time is considered night.
    /// </summary>
    public bool IsNight => CurrentHour >= NightStartHour || CurrentHour < NightEndHour;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeManager"/> class with the default starting time.
    /// </summary>
    /// <param name="random">Optional random generator for weather sampling.</param>
    public TimeManager(Random? random = null)
    {
        _random = random ?? new Random();
        Reset();
    }

    /// <summary>
    /// Advances the in-game time by the specified number of hours.
    /// </summary>
    /// <param name="hours">Number of hours to advance. Must be non-negative.</param>
    public void AdvanceTime(float hours)
    {
        if (hours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "Il tempo non può avanzare con un valore negativo.");
        }

        var totalHours = CurrentHour + (double)hours;
        var daysToAdvance = (int)Math.Floor(totalHours / HoursPerDay);
        var remainingHours = totalHours - (daysToAdvance * HoursPerDay);

        if (remainingHours >= HoursPerDay)
        {
            remainingHours -= HoursPerDay;
            daysToAdvance++;
        }

        CurrentHour = remainingHours;

        AdvanceDays(daysToAdvance);

        GD.Print($"Tempo avanzato di {hours:0.##} ore, ora attuale: {CurrentHour:00.##}, Giorno {CurrentDay}");
    }

    /// <summary>
    /// Samples and applies a new weather condition for the provided region.
    /// </summary>
    /// <param name="region">Region whose weather should be refreshed.</param>
    public void UpdateWeather(Region region)
    {
        if (region is null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        var profile = WeatherProfileProvider.GetProfile(region.BaseClimate);
        var nextCondition = profile.Sample(_random);

        var wasInitialized = region.HasWeatherBeenInitialized;
        var previousCondition = region.CurrentWeather;

        region.ApplyWeather(nextCondition);

        if (!wasInitialized || previousCondition != nextCondition)
        {
            GD.Print($"Meteo in {region.Name}: ora {region.CurrentWeather}");
        }
    }

    private void AdvanceDays(int days)
    {
        if (days <= 0)
        {
            return;
        }

        for (var i = 0; i < days; i++)
        {
            CurrentDay++;

            if (CurrentDay > DaysPerMonth)
            {
                CurrentDay = 1;
                CurrentMonth++;

                if (CurrentMonth > MonthsPerYear)
                {
                    CurrentMonth = 1;
                    CurrentYear++;
                }
            }

            OnNewDay?.Invoke(CurrentDay);
        }
    }

    private void Reset()
    {
        CurrentHour = 0.0;
        CurrentDay = 1;
        CurrentMonth = 1;
        CurrentYear = 1;
    }
}
