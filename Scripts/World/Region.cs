namespace DynamicRPG.World;

using System;
using System.Collections.Generic;
using DynamicRPG.World.Locations;

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
    /// Locations that belong to this region.
    /// </summary>
    public List<Location> Locations { get; } = new();

    /// <summary>
    /// Identifies the faction currently in control of the region. Placeholder for a dedicated Faction type.
    /// </summary>
    public string? ControllingFaction { get; set; }
}
