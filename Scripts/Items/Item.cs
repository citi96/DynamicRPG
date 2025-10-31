using System;

#nullable enable

namespace DynamicRPG.Items;

/// <summary>
/// Represents any item that can exist within the game's inventory system, including weapons,
/// armor, consumables, crafting materials, or miscellaneous objects.
/// </summary>
public class Item
{
    /// <summary>
    /// Gets or sets the display name of the item.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the item (e.g., Weapon, Armor, Consumable, Material).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a human-friendly description of the item.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item's weight per unit expressed in in-game units (e.g., kilograms).
    /// </summary>
    public double Weight { get; set; }

    /// <summary>
    /// Gets or sets the base monetary value of the item.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Gets or sets how many instances of the item are grouped together when stacked.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum damage value for weapon-type items.
    /// </summary>
    public int? MinDamage { get; set; }

    /// <summary>
    /// Gets or sets the maximum damage value for weapon-type items.
    /// </summary>
    public int? MaxDamage { get; set; }

    /// <summary>
    /// Gets or sets the accuracy bonus granted by weapon-type items.
    /// </summary>
    public int? AccuracyBonus { get; set; }

    /// <summary>
    /// Gets or sets the defense bonus granted by armor-type items.
    /// </summary>
    public int? DefenseBonus { get; set; }

    /// <summary>
    /// Gets or sets the textual description of the consumable effect.
    /// </summary>
    public string? Effect { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Item"/> class.
    /// </summary>
    public Item()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Item"/> class with a name, category, and quantity.
    /// </summary>
    /// <param name="name">The display name of the item.</param>
    /// <param name="category">The category that identifies the item's general usage.</param>
    /// <param name="quantity">How many instances of the item are stacked together.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="category"/> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="quantity"/> is less than one.</exception>
    public Item(string name, string category, int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Item name cannot be null or whitespace.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Item category cannot be null or whitespace.", nameof(category));
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be at least 1.");
        }

        Name = name;
        Type = category;
        Quantity = quantity;
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
