using DynamicRPG.Characters;
using DynamicRPG.World;
using DynamicRPG.World.Locations;

#nullable enable

namespace DynamicRPG.Gameplay.Players;

/// <summary>
/// Provides functionality to create the initial player-controlled character.
/// </summary>
public interface IPlayerFactory
{
    /// <summary>
    /// Creates the primary player character for the given starting region and location.
    /// </summary>
    /// <param name="startingRegion">Region where the character begins the adventure.</param>
    /// <param name="startingLocation">Location within the region where the character spawns.</param>
    /// <returns>The fully initialized player character.</returns>
    Character CreatePlayer(Region startingRegion, Location startingLocation);
}
