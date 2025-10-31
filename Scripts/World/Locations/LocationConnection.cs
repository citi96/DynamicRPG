namespace DynamicRPG.World.Locations;

using System;
using DynamicRPG.World.Hazards;

#nullable enable

/// <summary>
/// Represents a traversal link between two locations, optionally containing an environmental hazard.
/// </summary>
public sealed class LocationConnection
{
    public LocationConnection(Location target, Hazard? hazard = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Hazard = hazard;
    }

    /// <summary>
    /// Destination location for the connection.
    /// </summary>
    public Location Target { get; }

    /// <summary>
    /// Hazard information attached to the connection.
    /// </summary>
    public Hazard? Hazard { get; }

    /// <summary>
    /// Indicates whether the connection has an associated hazard.
    /// </summary>
    public bool HasHazard => Hazard is not null;
}
