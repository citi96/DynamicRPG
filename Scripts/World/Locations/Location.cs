namespace DynamicRPG.World.Locations;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DynamicRPG.Characters;
using Godot;
using DynamicRPG.World;

#nullable enable

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
    /// Describes the location category (e.g. Village, Dungeon, Resource).
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
    public List<Location> Connections { get; } = new();

    /// <summary>
    /// NPCs present within the location.
    /// </summary>
    public List<NPC> NPCs { get; } = new();
}
