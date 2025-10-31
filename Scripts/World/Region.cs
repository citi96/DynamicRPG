namespace DynamicRPG.World;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;
using DynamicRPG.Characters;

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
    public List<Location> Locations { get; set; } = new();

    /// <summary>
    /// Identifies the faction currently in control of the region. Placeholder for a dedicated Faction type.
    /// </summary>
    public string? ControllingFaction { get; set; }
}

/// <summary>
/// Represents a discoverable location contained inside a region.
/// </summary>
[Serializable]
public sealed class Location
{
    /// <summary>
    /// Human-readable name of the location.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Describes the location type (e.g. Village, Dungeon, Resource).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Region that owns the location. Ignored during serialization to prevent circular references.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Region? Region { get; set; }

    /// <summary>
    /// Optional position of the location within the game world.
    /// </summary>
    public Vector2? Coordinates { get; set; }

    /// <summary>
    /// Locations directly connected to this location.
    /// </summary>
    public List<Location> Connections { get; set; } = new();

    /// <summary>
    /// NPCs present within the location.
    /// </summary>
    public List<NPC> NPCs { get; set; } = new();
}
