using System;
using System.Collections.Generic;

using DynamicRPG.Items;

#nullable enable

namespace DynamicRPG.Characters;

/// <summary>
/// Represents a creature or person within the world, including both players and NPCs.
/// </summary>
public class Character
{
    private const int BaseHealth = 10;
    private const int HealthPerConstitutionPoint = 2;
    private const int BaseMana = 10;
    private const int ManaPerIntelligencePoint = 2;
    private const double ExperienceGrowthFactor = 1.5;

    /// <summary>
    /// Gets or sets the in-world display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this character is controlled by the player.
    /// </summary>
    public bool IsPlayer { get; set; } = false;

    /// <summary>
    /// Gets or sets the strength attribute, representing physical power and melee potency.
    /// </summary>
    public int Strength { get; set; } = 10;

    /// <summary>
    /// Gets or sets the dexterity attribute, representing agility and reflexes.
    /// </summary>
    public int Dexterity { get; set; } = 10;

    /// <summary>
    /// Gets or sets the constitution attribute, representing toughness and resilience.
    /// </summary>
    public int Constitution { get; set; } = 10;

    /// <summary>
    /// Gets or sets the intelligence attribute, representing arcane aptitude.
    /// </summary>
    public int Intelligence { get; set; } = 10;

    /// <summary>
    /// Gets or sets the wisdom attribute, representing perception and insight.
    /// </summary>
    public int Wisdom { get; set; } = 10;

    /// <summary>
    /// Gets or sets the charisma attribute, representing social influence.
    /// </summary>
    public int Charisma { get; set; } = 10;

    /// <summary>
    /// Gets the maximum health points the character can have.
    /// </summary>
    public int MaxHealth { get; private set; }

    /// <summary>
    /// Gets the current health points remaining.
    /// </summary>
    public int CurrentHealth { get; private set; }

    /// <summary>
    /// Gets the maximum mana available for spellcasting.
    /// </summary>
    public int MaxMana { get; private set; }

    /// <summary>
    /// Gets the current mana available for spellcasting.
    /// </summary>
    public int CurrentMana { get; private set; }

    /// <summary>
    /// Gets the base defense derived from attributes and permanent bonuses.
    /// </summary>
    public int BaseDefense { get; private set; }

    /// <summary>
    /// Gets the effective defense value that includes equipped armor bonuses.
    /// </summary>
    public int TotalDefense => BaseDefense + (EquippedArmor?.DefenseBonus ?? 0);

    /// <summary>
    /// Gets the attack rating derived from equipment and attributes.
    /// </summary>
    public int AttackRating { get; private set; }

    /// <summary>
    /// Gets the current character level.
    /// </summary>
    public int Level { get; private set; } = 1;

    /// <summary>
    /// Gets the current accumulated experience points.
    /// </summary>
    public int Experience { get; private set; }

    /// <summary>
    /// Gets the experience threshold required to reach the next level.
    /// </summary>
    public int ExperienceToNextLevel { get; private set; } = 100;

    /// <summary>
    /// Gets the inventory storing unequipped items.
    /// </summary>
    public Inventory Inventory { get; } = new();

    /// <summary>
    /// Gets a mutable dictionary of skills and their levels.
    /// </summary>
    public Dictionary<string, int> Skills { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OneHandedWeapons"] = 0,
        ["Archery"] = 0,
        ["Defense"] = 0,
        ["Stealth"] = 0,
        ["Pyromancy"] = 0,
        ["Necromancy"] = 0,
        ["Persuasion"] = 0,
        ["Crafting"] = 0,
        ["Lore"] = 0,
    };

    /// <summary>
    /// Gets the weapon currently equipped by the character.
    /// </summary>
    public Item? EquippedWeapon { get; private set; }

    /// <summary>
    /// Gets the armor currently equipped by the character.
    /// </summary>
    public Item? EquippedArmor { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Character"/> class.
    /// </summary>
    public Character()
    {
        RecalculateDerivedAttributes();
    }

    /// <summary>
    /// Adds experience points to the character and resolves level-ups when thresholds are reached.
    /// </summary>
    /// <param name="amount">The amount of experience to award.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is negative.</exception>
    public void AddExperience(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Experience cannot be negative.");
        }

        Experience += amount;

        while (Experience >= ExperienceToNextLevel)
        {
            Experience -= ExperienceToNextLevel;
            LevelUp();
        }
    }

    /// <summary>
    /// Equips the provided item into the appropriate slot if the type is supported.
    /// </summary>
    /// <param name="item">The item to equip.</param>
    /// <returns><c>true</c> when the item is equipped; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    public bool EquipItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!IsInInventory(item))
        {
            Inventory.AddItem(item);
        }

        if (IsItemOfType(item, "Weapon"))
        {
            EquippedWeapon = item;
            RefreshCombatStatistics();
            return true;
        }

        if (IsItemOfType(item, "Armor"))
        {
            EquippedArmor = item;
            RefreshCombatStatistics();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies incoming damage, returns a value indicating whether the character died.
    /// </summary>
    /// <param name="damageAmount">The incoming damage before mitigation.</param>
    /// <returns><c>true</c> if the character's health reaches zero; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="damageAmount"/> is negative.</exception>
    public bool TakeDamage(int damageAmount)
    {
        if (damageAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damageAmount), damageAmount, "Damage cannot be negative.");
        }

        var mitigatedDamage = Math.Max(damageAmount - TotalDefense, 0);
        CurrentHealth = Math.Max(CurrentHealth - mitigatedDamage, 0);

        return CurrentHealth == 0;
    }

    /// <summary>
    /// Recalculates derived statistics after attributes change.
    /// </summary>
    public void RecalculateDerivedAttributes()
    {
        var previousMaxHealth = MaxHealth;
        var previousMaxMana = MaxMana;

        MaxHealth = BaseHealth + (Constitution * HealthPerConstitutionPoint);
        MaxMana = BaseMana + (Intelligence * ManaPerIntelligencePoint);

        CurrentHealth = NormalizeResourceValue(CurrentHealth, previousMaxHealth, MaxHealth);
        CurrentMana = NormalizeResourceValue(CurrentMana, previousMaxMana, MaxMana);

        RefreshCombatStatistics();
    }

    private void RefreshCombatStatistics()
    {
        BaseDefense = CalculateBaseDefense();
        AttackRating = CalculateAttackRating();
    }

    private int CalculateBaseDefense()
    {
        var dexterityContribution = (int)Math.Floor(Dexterity * 0.5);
        return 10 + dexterityContribution;
    }

    private int CalculateAttackRating()
    {
        var strengthContribution = (int)Math.Floor(Strength * 0.5);

        if (EquippedWeapon is null)
        {
            return 10 + strengthContribution;
        }

        var minDamage = EquippedWeapon.MinDamage ?? 0;
        var maxDamage = EquippedWeapon.MaxDamage ?? minDamage;
        var weaponAverage = (int)Math.Round((minDamage + maxDamage) / 2.0, MidpointRounding.AwayFromZero);

        return weaponAverage + strengthContribution;
    }

    private static bool IsItemOfType(Item item, string type) =>
        string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase);

    private bool IsInInventory(Item item)
    {
        foreach (var existing in Inventory.Items)
        {
            if (ReferenceEquals(existing, item))
            {
                return true;
            }
        }

        return false;
    }

    private static int NormalizeResourceValue(int currentValue, int previousMaximum, int newMaximum)
    {
        if (newMaximum <= 0)
        {
            return 0;
        }

        if (previousMaximum <= 0)
        {
            return newMaximum;
        }

        var ratio = Math.Clamp(currentValue, 0, previousMaximum) / (double)previousMaximum;
        return Math.Clamp((int)Math.Round(newMaximum * ratio, MidpointRounding.AwayFromZero), 0, newMaximum);
    }

    private void LevelUp()
    {
        Level++;
        Strength++;
        Dexterity++;
        Constitution++;
        Intelligence++;
        Wisdom++;
        Charisma++;

        RecalculateDerivedAttributes();
        ExperienceToNextLevel = CalculateNextExperienceThreshold(ExperienceToNextLevel);

        Console.WriteLine($"Level up! New Level: {Level}");
        Console.WriteLine("Level up! Distribuisci i punti abilità.");
    }

    private static int CalculateNextExperienceThreshold(int currentThreshold)
    {
        var nextThreshold = (int)Math.Ceiling(currentThreshold * ExperienceGrowthFactor);
        return Math.Max(nextThreshold, currentThreshold + 1);
    }
}
