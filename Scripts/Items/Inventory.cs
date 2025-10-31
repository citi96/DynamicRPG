using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

#nullable enable

namespace DynamicRPG.Items;

/// <summary>
/// Provides storage and management for a collection of <see cref="Item"/> instances, including
/// capacity checks based on weight or slot counts.
/// </summary>
public class Inventory
{
    private readonly List<Item> _items = new();

    /// <summary>
    /// Gets a read-only view of the items currently stored in the inventory.
    /// </summary>
    public IReadOnlyCollection<Item> Items => _items.AsReadOnly();

    /// <summary>
    /// Gets or sets the maximum weight capacity that the inventory can carry. A value of
    /// <c>null</c> removes any weight restriction.
    /// </summary>
    public double? MaxWeight { get; set; } = 50d;

    /// <summary>
    /// Gets or sets the optional maximum number of item slots that the inventory can hold. A
    /// value of <c>null</c> removes any slot restriction.
    /// </summary>
    public int? MaxSlots { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Inventory"/> class.
    /// </summary>
    public Inventory()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Inventory"/> class with the specified
    /// capacity constraints.
    /// </summary>
    /// <param name="maxWeight">The maximum weight that can be carried, or <c>null</c> for unlimited.</param>
    /// <param name="maxSlots">The maximum number of item slots, or <c>null</c> for unlimited.</param>
    public Inventory(double? maxWeight, int? maxSlots = null)
    {
        if (maxWeight is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWeight), maxWeight, "Maximum weight cannot be negative.");
        }

        if (maxSlots is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSlots), maxSlots, "Maximum slots cannot be negative.");
        }

        MaxWeight = maxWeight;
        MaxSlots = maxSlots;
    }

    /// <summary>
    /// Attempts to add an item to the inventory while respecting configured capacity limits.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns><c>true</c> if the item is added successfully; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    public bool AddItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (MaxSlots.HasValue && _items.Count >= MaxSlots.Value)
        {
            Console.WriteLine($"Failed to add {item.Name} to inventory: slot capacity reached ({MaxSlots.Value}).");
            return false;
        }

        var prospectiveWeight = GetTotalWeight() + GetEffectiveItemWeight(item);
        if (MaxWeight.HasValue && prospectiveWeight > MaxWeight.Value)
        {
            Console.WriteLine($"Failed to add {item.Name} to inventory: weight limit exceeded ({prospectiveWeight:F2}/{MaxWeight.Value:F2}).");
            return false;
        }

        _items.Add(item);
        Console.WriteLine($"Added {item.Name} to inventory, total weight {prospectiveWeight:F2}.");
        return true;
    }

    /// <summary>
    /// Removes the specified item from the inventory if present.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns><c>true</c> if the item was removed; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    public bool RemoveItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var wasRemoved = _items.Remove(item);
        var currentWeight = GetTotalWeight();

        Console.WriteLine(wasRemoved
            ? $"Removed {item.Name} from inventory, total weight {currentWeight:F2}."
            : $"Failed to remove {item.Name} from inventory: item not found.");

        return wasRemoved;
    }

    /// <summary>
    /// Finds the first item matching the provided name, using a case-insensitive comparison.
    /// </summary>
    /// <param name="itemName">The name of the item to search for.</param>
    /// <returns>The matching <see cref="Item"/>, or <c>null</c> if not found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="itemName"/> is null or whitespace.</exception>
    public Item? FindItem(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            throw new ArgumentException("Item name cannot be null or whitespace.", nameof(itemName));
        }

        return _items.FirstOrDefault(item => string.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Calculates the combined weight of all items stored in the inventory.
    /// </summary>
    /// <returns>The total weight of all items.</returns>
    public double GetTotalWeight() => _items.Sum(GetEffectiveItemWeight);

    private static double GetEffectiveItemWeight([DisallowNull] Item item)
    {
        var quantity = Math.Max(item.Quantity, 1);
        return item.Weight * quantity;
    }
}
