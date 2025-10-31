using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DynamicRPG.Characters;

namespace DynamicRPG.Systems.Combat;

#nullable enable

/// <summary>
/// Provides pathfinding utilities for movement on the combat grid.
/// </summary>
public sealed class GridPathfinder
{
    private static readonly IReadOnlyList<GridPosition> CardinalDirections = new List<GridPosition>
    {
        new(0, -1),
        new(0, 1),
        new(-1, 0),
        new(1, 0),
    }.AsReadOnly();

    /// <summary>
    /// Finds a path between two points using a breadth-first search across cardinal directions.
    /// </summary>
    /// <param name="grid">The combat grid.</param>
    /// <param name="mover">The character attempting to move.</param>
    /// <param name="start">The starting position.</param>
    /// <param name="destination">The desired destination.</param>
    /// <returns>A read-only path including start and destination, or <c>null</c> if unreachable.</returns>
    public IReadOnlyList<GridPosition>? FindPath(
        CombatGrid grid,
        Character mover,
        GridPosition start,
        GridPosition destination)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(mover);

        if (!grid.IsWithinBounds(start) || !grid.IsWithinBounds(destination))
        {
            return null;
        }

        if (start == destination)
        {
            return new ReadOnlyCollection<GridPosition>(new List<GridPosition> { start });
        }

        var queue = new Queue<GridPosition>();
        var cameFrom = new Dictionary<GridPosition, GridPosition>();
        var visited = new HashSet<GridPosition> { start };

        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var direction in CardinalDirections)
            {
                var neighbor = current.Offset(direction.X, direction.Y);

                if (!grid.IsWithinBounds(neighbor))
                {
                    continue;
                }

                if (visited.Contains(neighbor))
                {
                    continue;
                }

                if (!grid.CanOccupy(neighbor, mover) && neighbor != destination)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                visited.Add(neighbor);

                if (neighbor == destination)
                {
                    return BuildPath(start, destination, cameFrom);
                }

                queue.Enqueue(neighbor);
            }
        }

        return null;
    }

    private static IReadOnlyList<GridPosition> BuildPath(
        GridPosition start,
        GridPosition destination,
        IReadOnlyDictionary<GridPosition, GridPosition> cameFrom)
    {
        var path = new List<GridPosition> { destination };
        var current = destination;

        while (!current.Equals(start) && cameFrom.TryGetValue(current, out var previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return new ReadOnlyCollection<GridPosition>(path);
    }
}
