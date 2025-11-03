using DynamicRPG.World;

#nullable enable

namespace DynamicRPG.Gameplay.World;

/// <summary>
/// Defines functionality for procedurally generating the overarching game world.
/// </summary>
public interface IWorldGenerator
{
    /// <summary>
    /// Creates a new world state composed of regions and their locations.
    /// </summary>
    /// <returns>A result describing the generated world.</returns>
    WorldGenerationResult GenerateWorld();
}
