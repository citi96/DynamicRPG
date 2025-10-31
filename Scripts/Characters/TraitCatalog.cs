#nullable enable

namespace DynamicRPG.Characters;

/// <summary>
/// Provides a collection of commonly used trait definitions for ease of reuse across the game.
/// </summary>
public static class TraitCatalog
{
    /// <summary>
    /// Grants a permanent +5 bonus to initiative rolls.
    /// </summary>
    public static Trait Alert { get; } = new(
        "Allerta",
        "+5 all'Iniziativa",
        character => character.IncreaseInitiativeBonus(5));

    /// <summary>
    /// Increases the carrying capacity of the character's inventory by 50 weight units.
    /// </summary>
    public static Trait StrongBack { get; } = new(
        "Schiena Larga",
        "+50 capacità carico",
        character => character.IncreaseInventoryCapacity(50));
}
