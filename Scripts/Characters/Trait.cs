using System;

#nullable enable

namespace DynamicRPG.Characters;

/// <summary>
/// Represents a permanent trait or perk that can grant descriptive and mechanical bonuses
/// to a <see cref="Character"/> once learned.
/// </summary>
public sealed class Trait : IEquatable<Trait>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Trait"/> class.
    /// </summary>
    /// <param name="name">The human-readable trait name.</param>
    /// <param name="description">A descriptive summary of the trait's effects.</param>
    /// <param name="applyEffect">An optional action applied to a character when the trait is learned.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public Trait(string name, string description, Action<Character>? applyEffect = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Trait name cannot be null or whitespace.", nameof(name));
        }

        Name = name;
        Description = description ?? string.Empty;
        ApplyEffect = applyEffect;
    }

    /// <summary>
    /// Gets the human-readable trait name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the descriptive summary of the trait's effects.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets an action executed against a character when the trait is learned, if provided.
    /// </summary>
    public Action<Character>? ApplyEffect { get; }

    /// <summary>
    /// Applies the trait's learning effect to the provided character.
    /// </summary>
    /// <param name="character">The character learning the trait.</param>
    public void Apply(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        ApplyEffect?.Invoke(character);
    }

    /// <inheritdoc />
    public bool Equals(Trait? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Trait trait && Equals(trait);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
}
