using System;
using System.Collections.Generic;

using DynamicRPG.Items;
using DynamicRPG.World;
using DynamicRPG.World.Locations;

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
    private const double BaseCarryCapacity = 50d;
    private const double CarryCapacityPerStrengthPoint = 10d;
    private const int DefaultBaseMovementAllowance = 5;

    private double _additionalCarryCapacity;

    /// <summary>
    /// Gets or sets the in-world display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the narrative background assigned to the character.
    /// </summary>
    public string Background { get; set; } = string.Empty;

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
    /// Gets the maximum health points using the common HP naming convention.
    /// </summary>
    public int MaxHP => MaxHealth;

    /// <summary>
    /// Gets the current health points remaining.
    /// </summary>
    public int CurrentHealth { get; private set; }

    /// <summary>
    /// Gets the current health points using the common HP naming convention.
    /// </summary>
    public int HP => CurrentHealth;

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
    public Inventory Inventory { get; }

    /// <summary>
    /// Gets the current encumbrance level derived from the carried load.
    /// 0 = none, 1 = encumbered, 2 = heavily encumbered.
    /// </summary>
    public int EncumbranceLevel { get; private set; }

    /// <summary>
    /// Gets the base movement allowance expressed in tiles per turn.
    /// </summary>
    public int BaseMovementAllowance { get; private set; } = DefaultBaseMovementAllowance;

    /// <summary>
    /// Gets the current movement allowance after applying encumbrance penalties.
    /// </summary>
    public int CurrentMovementAllowance { get; private set; } = DefaultBaseMovementAllowance;

    /// <summary>
    /// Gets the remaining movement points available for the current turn.
    /// </summary>
    public int RemainingMovement { get; private set; } = DefaultBaseMovementAllowance;

    /// <summary>
    /// Gets the current grid position on the combat map along the X axis.
    /// </summary>
    public int PositionX { get; private set; }

    /// <summary>
    /// Gets the current grid position on the combat map along the Y axis.
    /// </summary>
    public int PositionY { get; private set; }

    /// <summary>
    /// Gets the collection of permanent traits that the character has learned.
    /// </summary>
    public List<Trait> Traits { get; } = new();

    /// <summary>
    /// Gets the cumulative initiative bonus granted by traits and other permanent effects.
    /// </summary>
    public int InitiativeBonus { get; private set; }

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
    /// Gets or sets the world region currently hosting the character.
    /// </summary>
    public Region? CurrentRegion { get; set; }

    /// <summary>
    /// Gets or sets the specific location within a region occupied by the character.
    /// </summary>
    public Location? CurrentLocation { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Character"/> class.
    /// </summary>
    public Character()
    {
        Inventory = new Inventory(this);
        RecalculateDerivedAttributes();
        SynchronizeCarryCapacity();
        HandleInventoryWeightChanged(Inventory.GetTotalWeight());
        RemainingMovement = CurrentMovementAllowance;
    }

    /// <summary>
    /// Sets the character position on the combat grid.
    /// </summary>
    /// <param name="x">The horizontal grid coordinate.</param>
    /// <param name="y">The vertical grid coordinate.</param>
    public void SetPosition(int x, int y)
    {
        PositionX = x;
        PositionY = y;
    }

    /// <summary>
    /// Restores the remaining movement points at the start of a new turn.
    /// </summary>
    public void ResetMovementForNewTurn()
    {
        RemainingMovement = CurrentMovementAllowance;
    }

    /// <summary>
    /// Consumes the provided amount of movement points for the current turn.
    /// </summary>
    /// <param name="amount">The number of tiles traversed.</param>
    public void ConsumeMovement(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        RemainingMovement = Math.Max(RemainingMovement - amount, 0);
    }

    /// <summary>
    /// Calculates the maximum weight that the character can carry based on strength and bonuses.
    /// </summary>
    /// <returns>The carry capacity in weight units.</returns>
    public double GetCarryCapacity() =>
        BaseCarryCapacity + (Strength * CarryCapacityPerStrengthPoint) + _additionalCarryCapacity;

    /// <summary>
    /// Learns the provided trait, applying its effects if it was not already known.
    /// </summary>
    /// <param name="trait">The trait to learn.</param>
    /// <returns><c>true</c> when the trait is newly learned; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="trait"/> is <c>null</c>.</exception>
    public bool LearnTrait(Trait trait)
    {
        ArgumentNullException.ThrowIfNull(trait);

        if (Traits.Exists(existing => existing.Equals(trait)))
        {
            return false;
        }

        Traits.Add(trait);
        trait.Apply(this);
        return true;
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
        SynchronizeCarryCapacity();
        HandleInventoryWeightChanged(Inventory.GetTotalWeight());
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

    /// <summary>
    /// Applies a permanent increase to the initiative bonus granted to the character.
    /// </summary>
    /// <param name="bonusAmount">The amount to add to the initiative bonus.</param>
    internal void IncreaseInitiativeBonus(int bonusAmount)
    {
        if (bonusAmount <= 0)
        {
            return;
        }

        InitiativeBonus += bonusAmount;
    }

    /// <summary>
    /// Applies a permanent increase to the maximum carry weight of the character's inventory.
    /// </summary>
    /// <param name="additionalWeight">The amount of weight capacity to add.</param>
    internal void IncreaseInventoryCapacity(double additionalWeight)
    {
        if (additionalWeight <= 0)
        {
            return;
        }

        _additionalCarryCapacity += additionalWeight;
        SynchronizeCarryCapacity();
        HandleInventoryWeightChanged(Inventory.GetTotalWeight());
    }

    internal void SynchronizeCarryCapacity()
    {
        Inventory.MaxWeight = GetCarryCapacity();
    }

    internal void HandleInventoryWeightChanged(double totalWeight)
    {
        var previousEncumbrance = EncumbranceLevel;

        if (Inventory.MaxWeight is not double maxWeight || maxWeight <= 0)
        {
            EncumbranceLevel = 0;
            UpdateMovementAllowance();
            return;
        }

        var loadRatio = totalWeight / maxWeight;

        EncumbranceLevel = loadRatio switch
        {
            <= 1.0 => 0,
            <= 1.5 => 1,
            <= 2.0 => 2,
            _ => 2,
        };

        if (EncumbranceLevel != previousEncumbrance)
        {
            ReportEncumbranceTransition(previousEncumbrance, EncumbranceLevel);
        }

        UpdateMovementAllowance();
    }

    private void UpdateMovementAllowance()
    {
        var adjustedAllowance = BaseMovementAllowance;

        adjustedAllowance = EncumbranceLevel switch
        {
            1 => Math.Max(1, adjustedAllowance - 1),
            2 => Math.Max(1, (int)Math.Ceiling(adjustedAllowance / 2.0)),
            _ => adjustedAllowance,
        };

        CurrentMovementAllowance = adjustedAllowance;
        RemainingMovement = Math.Min(RemainingMovement, CurrentMovementAllowance);
    }

    private static void ReportEncumbranceTransition(int previousEncumbrance, int newEncumbrance)
    {
        if (newEncumbrance == previousEncumbrance)
        {
            return;
        }

        if (newEncumbrance == 1)
        {
            Console.WriteLine("Sei appesantito, movimento ridotto.");
            return;
        }

        if (newEncumbrance == 2)
        {
            Console.WriteLine("Sei sovraccarico, quasi immobile.");
            return;
        }

        if (previousEncumbrance > 0 && newEncumbrance == 0)
        {
            Console.WriteLine("Ti senti più leggero, puoi muoverti liberamente.");
        }
    }
}
