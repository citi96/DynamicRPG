using System;
using DynamicRPG.Characters;

namespace DynamicRPG.Systems.Combat;

#nullable enable

/// <summary>
/// Represents the tactical map used during a combat encounter.
/// </summary>
public sealed class CombatGrid
{
    private readonly TileType[,] _tiles;
    private readonly Character?[,] _occupants;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombatGrid"/> class with the specified dimensions.
    /// </summary>
    /// <param name="width">The horizontal size of the grid.</param>
    /// <param name="height">The vertical size of the grid.</param>
    /// <param name="defaultTile">The tile type used to populate the grid initially.</param>
    public CombatGrid(int width, int height, TileType defaultTile)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        }

        Width = width;
        Height = height;
        _tiles = new TileType[Width, Height];
        _occupants = new Character?[Width, Height];

        Fill(defaultTile);
    }

    /// <summary>
    /// Gets the grid width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the grid height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the tile type at the specified position.
    /// </summary>
    /// <param name="position">The grid coordinates.</param>
    /// <returns>The tile type found at the provided coordinates.</returns>
    public TileType GetTile(GridPosition position)
    {
        EnsureInBounds(position);
        return _tiles[position.X, position.Y];
    }

    /// <summary>
    /// Assigns a tile type to the specified position.
    /// </summary>
    /// <param name="position">The grid coordinates.</param>
    /// <param name="tileType">The tile type to assign.</param>
    public void SetTile(GridPosition position, TileType tileType)
    {
        EnsureInBounds(position);
        _tiles[position.X, position.Y] = tileType;
    }

    /// <summary>
    /// Determines whether the provided position is within the grid bounds.
    /// </summary>
    /// <param name="position">The grid coordinates.</param>
    /// <returns><c>true</c> when the position belongs to the grid; otherwise <c>false</c>.</returns>
    public bool IsWithinBounds(GridPosition position) =>
        position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

    /// <summary>
    /// Determines whether the tile at the provided position can be traversed.
    /// </summary>
    /// <param name="position">The grid coordinates.</param>
    /// <returns><c>true</c> when the tile is traversable; otherwise <c>false</c>.</returns>
    public bool IsPassable(GridPosition position)
    {
        if (!IsWithinBounds(position))
        {
            return false;
        }

        return GetTile(position) != TileType.Obstacle;
    }

    /// <summary>
    /// Determines whether the tile can be occupied by the specified character.
    /// </summary>
    /// <param name="position">The grid coordinates.</param>
    /// <param name="occupant">The character attempting to occupy the tile.</param>
    /// <returns><c>true</c> when the tile is available; otherwise <c>false</c>.</returns>
    public bool CanOccupy(GridPosition position, Character? occupant)
    {
        if (!IsPassable(position))
        {
            return false;
        }

        var existing = _occupants[position.X, position.Y];
        return existing is null || ReferenceEquals(existing, occupant);
    }

    /// <summary>
    /// Gets the occupant placed on the provided tile, if any.
    /// </summary>
    /// <param name="position">The grid coordinates.</param>
    /// <returns>The occupying character when present; otherwise <c>null</c>.</returns>
    public Character? GetOccupant(GridPosition position)
    {
        if (!IsWithinBounds(position))
        {
            return null;
        }

        return _occupants[position.X, position.Y];
    }

    /// <summary>
    /// Attempts to occupy the specified tile with the provided character.
    /// </summary>
    /// <param name="position">The grid coordinates.</param>
    /// <param name="occupant">The character to place.</param>
    /// <returns><c>true</c> when the operation succeeds; otherwise <c>false</c>.</returns>
    public bool TryOccupy(GridPosition position, Character occupant)
    {
        ArgumentNullException.ThrowIfNull(occupant);

        if (!CanOccupy(position, occupant))
        {
            return false;
        }

        _occupants[position.X, position.Y] = occupant;
        return true;
    }

    /// <summary>
    /// Vacates the specified tile when the provided character is currently occupying it.
    /// </summary>
    /// <param name="position">The grid coordinates.</param>
    /// <param name="occupant">The character that should be removed.</param>
    public void Vacate(GridPosition position, Character occupant)
    {
        ArgumentNullException.ThrowIfNull(occupant);

        if (!IsWithinBounds(position))
        {
            return;
        }

        if (ReferenceEquals(_occupants[position.X, position.Y], occupant))
        {
            _occupants[position.X, position.Y] = null;
        }
    }

    /// <summary>
    /// Attempts to move the provided character from the origin tile to the target tile.
    /// </summary>
    /// <param name="from">The starting tile.</param>
    /// <param name="to">The destination tile.</param>
    /// <param name="occupant">The character that is moving.</param>
    /// <returns><c>true</c> when the transition succeeds; otherwise <c>false</c>.</returns>
    public bool TryTransitionOccupant(GridPosition from, GridPosition to, Character occupant)
    {
        ArgumentNullException.ThrowIfNull(occupant);

        if (!IsWithinBounds(from) || !IsWithinBounds(to))
        {
            return false;
        }

        if (!ReferenceEquals(_occupants[from.X, from.Y], occupant))
        {
            return false;
        }

        if (!CanOccupy(to, occupant))
        {
            return false;
        }

        _occupants[from.X, from.Y] = null;
        _occupants[to.X, to.Y] = occupant;
        return true;
    }

    /// <summary>
    /// Clears any occupant stored on the grid.
    /// </summary>
    public void ClearOccupants()
    {
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                _occupants[x, y] = null;
            }
        }
    }

    /// <summary>
    /// Fills the entire grid with the provided tile type.
    /// </summary>
    /// <param name="tileType">The tile type to assign to every cell.</param>
    public void Fill(TileType tileType)
    {
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                _tiles[x, y] = tileType;
            }
        }
    }

    private void EnsureInBounds(GridPosition position)
    {
        if (!IsWithinBounds(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Position outside of grid bounds.");
        }
    }
}
