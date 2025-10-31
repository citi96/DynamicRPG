using System.ComponentModel;

namespace DynamicRPG.Systems.Combat;

/// <summary>
/// Represents the type of terrain occupying a combat grid cell.
/// </summary>
public enum TileType
{
    /// <summary>
    /// A traversable cell without penalties.
    /// </summary>
    [Description("Spazio libero")]
    Empty = 0,

    /// <summary>
    /// A blocking element such as a wall or large obstacle.
    /// </summary>
    [Description("Ostacolo")]
    Obstacle = 1,

    /// <summary>
    /// A traversable cell that is harder to cross (e.g. fango o sterpaglia).
    /// </summary>
    [Description("Terreno difficile")]
    Difficult = 2,

    /// <summary>
    /// A hazardous tile that may trigger effects (traps, fuoco, etc.).
    /// </summary>
    [Description("Pericolo")]
    Hazard = 3,
}
