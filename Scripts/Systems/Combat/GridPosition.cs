namespace DynamicRPG.Systems.Combat;

/// <summary>
/// Represents an immutable coordinate on the combat grid.
/// </summary>
public readonly record struct GridPosition(int X, int Y)
{
    /// <summary>
    /// Gets a new position offset by the provided delta values.
    /// </summary>
    /// <param name="deltaX">Offset along the horizontal axis.</param>
    /// <param name="deltaY">Offset along the vertical axis.</param>
    /// <returns>The resulting position after applying the offset.</returns>
    public GridPosition Offset(int deltaX, int deltaY) => new(X + deltaX, Y + deltaY);

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y})";
}
