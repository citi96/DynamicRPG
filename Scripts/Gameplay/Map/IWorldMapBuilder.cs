using System.Collections.Generic;
using DynamicRPG.World;
using Godot;

#nullable enable

namespace DynamicRPG.Gameplay.Map;

/// <summary>
/// Defines a component capable of translating world data into a visual map representation.
/// </summary>
public interface IWorldMapBuilder
{
    /// <summary>
    /// Builds a tile map layer representing the supplied regions.
    /// </summary>
    /// <param name="regions">Regions to visualize.</param>
    /// <returns>The configured tile map layer.</returns>
    TileMapLayer BuildBiomeMap(IReadOnlyList<Region> regions);
}
