using System;
using System.Collections.Generic;
using DynamicRPG.World;
using DynamicRPG.World.Locations;

#nullable enable

namespace DynamicRPG.Gameplay.World;

/// <summary>
/// Represents the immutable data returned by an <see cref="IWorldGenerator"/> execution.
/// </summary>
public sealed class WorldGenerationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldGenerationResult"/> class.
    /// </summary>
    /// <param name="regions">The regions composing the generated world.</param>
    /// <param name="locations">The flattened list of all generated locations.</param>
    public WorldGenerationResult(IReadOnlyList<Region> regions, IReadOnlyList<Location> locations)
    {
        Regions = regions ?? throw new ArgumentNullException(nameof(regions));
        Locations = locations ?? throw new ArgumentNullException(nameof(locations));
    }

    /// <summary>
    /// Gets an empty representation containing no regions or locations.
    /// </summary>
    public static WorldGenerationResult Empty { get; } = new(Array.Empty<Region>(), Array.Empty<Location>());

    /// <summary>
    /// Gets the read-only collection of generated regions.
    /// </summary>
    public IReadOnlyList<Region> Regions { get; }

    /// <summary>
    /// Gets the read-only collection of generated locations aggregated from all regions.
    /// </summary>
    public IReadOnlyList<Location> Locations { get; }
}
