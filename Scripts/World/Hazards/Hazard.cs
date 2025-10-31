namespace DynamicRPG.World.Hazards;

using System;

#nullable enable

/// <summary>
/// Describes an environmental threat that can be encountered while traversing between locations.
/// </summary>
[Serializable]
public sealed class Hazard
{
    public Hazard(string name, string type, string description, int dangerLevel)
    {
        Name = name;
        Type = type;
        Description = description;
        DangerLevel = dangerLevel;
    }

    /// <summary>
    /// Display name of the hazard.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Machine-friendly identifier of the hazard category (e.g. bog, avalanche).
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Narrative description of the danger posed by the hazard.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Relative danger level, on an arbitrary scale from 1 (minor) to 5 (deadly).
    /// </summary>
    public int DangerLevel { get; }
}
