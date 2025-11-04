using DynamicRPG;
using DynamicRPG.Items;
using DynamicRPG.Systems.Combat;
using DynamicRPG.World;
using DynamicRPG.World.Locations;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace DynamicRPG.Characters;

/// <summary>
/// Represents a creature or person within the world, including both players and NPCs.
/// As a <see cref="Node2D"/>, the character encapsulates both gameplay data and its on-screen representation.
/// </summary>
public partial class Character : Node2D
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
    private string _displayName = string.Empty;

    [ExportGroup("Visuals")]
    [Export]
    public Texture2D? CharacterTexture { get; set; }

    [Export]
    public Vector2 SpriteScale { get; set; } = new(0.25f, 0.25f);

    [Export(PropertyHint.ColorNoAlpha)]
    public Color PlayerColor { get; set; } = new(0.2f, 0.7f, 0.9f);

    [Export(PropertyHint.ColorNoAlpha)]
    public Color EnemyColor { get; set; } = new(0.9f, 0.2f, 0.25f);

    [Export]
    public NodePath SpriteNodePath { get; set; } = new("CharacterSprite");

    /// <summary>
    /// Gets or sets the in-world display name.
    /// </summary>
    public new string Name
    {
        get => _displayName;
        set
        {
            _displayName = value;
            base.Name = value;
        }
    }

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
    /// Gets a value indicating whether the character has been reduced to zero hit points.
    /// </summary>
    public bool IsDead { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the character has spent their reaction during the current round.
    /// </summary>
    public bool HasUsedReaction { get; set; }

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
    /// Gets the collection of active status effects affecting the character.
    /// </summary>
    public List<StatusEffect> StatusEffects { get; } = new();

    /// <summary>
    /// Gets the current mana using the common naming convention.
    /// Alias for CurrentMana to maintain compatibility.
    /// </summary>
    public int Mana => CurrentMana;

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
    /// Sets up the visual representation of the character when it enters the scene tree.
    /// </summary>
    public override void _Ready()
    {
        base._Ready();

        var sprite = SpriteNodePath.IsEmpty
            ? null
            : GetNodeOrNull<Sprite2D>(SpriteNodePath);

        if (sprite is null)
        {
            sprite = new Sprite2D
            {
                Name = SpriteNodePath.IsEmpty ? "CharacterSprite" : SpriteNodePath.GetName(0),
            };

            AddChild(sprite);
            SpriteNodePath = sprite.GetPath();
        }

        sprite.Texture = ResolveTexture();
        sprite.Scale = SpriteScale;
        sprite.Modulate = IsPlayer ? PlayerColor : EnemyColor;
    }

    private Texture2D ResolveTexture()
    {
        if (CharacterTexture is not null)
        {
            return CharacterTexture;
        }

        const string preferredIconPath = "res://icon.png";

        if (ResourceLoader.Exists(preferredIconPath))
        {
            return GD.Load<Texture2D>(preferredIconPath);
        }

        return GD.Load<Texture2D>("res://icon.svg");
    }

    /// <summary>
    /// Applies or refreshes a status effect on the character.
    /// </summary>
    /// <remarks>
    /// Alcuni effetti possono cumulare la potenza (Bleeding) mentre altri aggiornano solo la durata.
    /// </remarks>
    public void ApplyStatus(StatusType type, int duration, int potency = 0)
    {
        if (duration <= 0)
        {
            GD.PushWarning($"Tentativo di applicare {type} a {Name} con durata non positiva ({duration}). Effetto ignorato.");
            return;
        }

        if (potency < 0)
        {
            GD.PushWarning($"Potenza negativa ({potency}) per {type} su {Name}. Il valore è stato normalizzato a 0.");
            potency = 0;
        }

        var existing = FindStatus(type);

        if (existing is not null)
        {
            var previousDuration = existing.RemainingDuration;
            existing.RemainingDuration = Math.Max(existing.RemainingDuration, duration);

            if (type == StatusType.Bleeding)
            {
                var additionalPotency = Math.Max(1, potency);
                existing.Potency += additionalPotency;
                ReportStatusMessage($"{Name} sanguina più copiosamente (Potenza {existing.Potency}).");
            }
            else if (potency > existing.Potency)
            {
                existing.Potency = potency;
            }

            if (existing.RemainingDuration > previousDuration)
            {
                ReportStatusMessage($"La durata di {type} su {Name} è estesa a {existing.RemainingDuration} turni.");
            }

            NotifyStatusEffectsChanged();
            return;
        }

        var effect = new StatusEffect(type, duration, potency);
        StatusEffects.Add(effect);
        ReportStatusMessage($"{Name} è ora affetto da {type} per {duration} turni.");
        NotifyStatusEffectsChanged();
    }

    /// <summary>
    /// Rimuove uno status effect specifico.
    /// </summary>
    public bool RemoveStatus(StatusType type, bool showLog = true)
    {
        var removed = StatusEffects.RemoveAll(effect => effect.Type == type);
        if (removed > 0 && showLog)
        {
            ReportStatusMessage($"{type} rimosso da {Name}.");
        }

        if (removed > 0)
        {
            NotifyStatusEffectsChanged();
        }

        return removed > 0;
    }

    /// <summary>
    /// Verifica se il personaggio ha un determinato status attivo.
    /// </summary>
    public bool HasStatus(StatusType type) => FindStatus(type) is not null;

    /// <summary>
    /// Processa gli effetti degli status all'inizio del turno.
    /// Returns true se il personaggio può agire normalmente.
    /// Effetti come <see cref="StatusType.Invisible"/>, <see cref="StatusType.Silenced"/>,
    /// <see cref="StatusType.Charmed"/>, <see cref="StatusType.Panicked"/> e <see cref="StatusType.Exhausted"/>
    /// sono tracciati ma richiederanno logiche dedicate in sistemi futuri.
    /// </summary>
    public bool ProcessBeginTurnStatusEffects()
    {
        if (FindStatus(StatusType.Stunned) is not null)
        {
            ReportStatusMessage($"{Name} è stordito e perde il turno.");
            return false;
        }

        if (FindStatus(StatusType.Frozen) is not null)
        {
            ReportStatusMessage($"{Name} è congelato e non può agire.");
            return false;
        }

        var effectiveMovement = CurrentMovementAllowance;

        if (FindStatus(StatusType.Prone) is { } prone)
        {
            StatusEffects.Remove(prone); // Rialzarsi rimuove lo stato, ma consuma parte del turno.
            effectiveMovement = Math.Max(1, effectiveMovement / 2);
            ReportStatusMessage($"{Name} si rialza e ha movimento ridotto per questo turno.");
            NotifyStatusEffectsChanged();
        }

        if (FindStatus(StatusType.Slowed) is not null)
        {
            effectiveMovement = Math.Max(1, effectiveMovement / 2);
            ReportStatusMessage($"{Name} è rallentato e può muoversi meno del solito.");
        }

        if (FindStatus(StatusType.Hasted) is not null)
        {
            effectiveMovement += 2;
            ReportStatusMessage($"{Name} è accelerato e ottiene movimento aggiuntivo.");
        }

        RemainingMovement = Math.Max(0, effectiveMovement);
        return true;
    }

    /// <summary>
    /// Processa gli effetti degli status alla fine del turno (DoT, decremento durate).
    /// Effetti non dannosi come invisibilità o silenzio verranno gestiti in moduli successivi.
    /// </summary>
    public void ProcessEndTurnStatusEffects()
    {
        if (StatusEffects.Count == 0)
        {
            return;
        }

        var effectsToRemove = new List<StatusEffect>();

        var statusChanged = false;

        foreach (var effect in StatusEffects)
        {
            switch (effect.Type)
            {
                case StatusType.Bleeding:
                    ApplyDamageOverTime(effect, Math.Max(1, effect.Potency) * 2, "sanguinamento");
                    break;

                case StatusType.Poisoned:
                    ApplyDamageOverTime(effect, Math.Max(1, effect.Potency == 0 ? 2 : effect.Potency), "avvelenamento");
                    break;

                case StatusType.Burning:
                    ApplyDamageOverTime(effect, Math.Max(1, effect.Potency == 0 ? 5 : effect.Potency), "ustioni");
                    break;
            }

            statusChanged = true;

            if (effect.DecrementDuration())
            {
                effectsToRemove.Add(effect);
            }
        }

        foreach (var effect in effectsToRemove)
        {
            StatusEffects.Remove(effect);
            ReportStatusMessage($"{effect.Type} su {Name} è svanito.");
        }

        if (statusChanged)
        {
            NotifyStatusEffectsChanged();
        }
    }

    private StatusEffect? FindStatus(StatusType type) =>
        StatusEffects.FirstOrDefault(effect => effect.Type == type);

    private void ApplyDamageOverTime(StatusEffect effect, int damageAmount, string causeLabel)
    {
        if (damageAmount <= 0)
        {
            return;
        }

        var wasAlive = CurrentHealth > 0;
        TakeDamage(damageAmount);
        var potencySuffix = effect.Potency > 0 ? $" (Potenza {effect.Potency})" : string.Empty;
        ReportStatusMessage($"{Name} perde {damageAmount} HP a causa di {causeLabel}.{potencySuffix}");

        if (wasAlive && IsDead)
        {
            ReportStatusMessage($"{Name} soccombe alle ferite inflitte da {causeLabel}.");
        }
    }

    /// <summary>
    /// Restituisce una stringa con tutti gli status attivi per display UI.
    /// </summary>
    public string GetStatusEffectsDisplay()
    {
        if (StatusEffects.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(" ", StatusEffects.Select(effect => effect.ToString()));
    }

    private static void ReportStatusMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        CombatManager.LogMessage(message);
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
    /// Clears the reaction usage flag so the character can react again in the new round.
    /// </summary>
    public void ResetReaction()
    {
        HasUsedReaction = false;
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

        var previousHealth = CurrentHealth;
        CurrentHealth = Math.Max(CurrentHealth - damageAmount, 0);
        IsDead = CurrentHealth <= 0;

        if (CurrentHealth != previousHealth)
        {
            NotifyPlayerStatsChanged();
        }

        return IsDead;
    }

    /// <summary>
    /// Restores health points up to the maximum value.
    /// </summary>
    /// <param name="amount">The amount of HP to restore.</param>
    /// <returns>The actual HP restored.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is negative.</exception>
    public int Heal(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Healing cannot be negative.");
        }

        if (amount == 0 || CurrentHealth >= MaxHealth)
        {
            return 0;
        }

        var previousHealth = CurrentHealth;
        CurrentHealth = Math.Clamp(CurrentHealth + amount, 0, MaxHealth);
        IsDead = CurrentHealth <= 0;

        if (CurrentHealth != previousHealth)
        {
            NotifyPlayerStatsChanged();
        }

        return CurrentHealth - previousHealth;
    }

    /// <summary>
    /// Attempts to spend mana points required for an ability.
    /// </summary>
    /// <param name="amount">The mana cost.</param>
    /// <returns><c>true</c> when enough mana was available; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is negative.</exception>
    public bool TrySpendMana(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Mana cost cannot be negative.");
        }

        if (CurrentMana < amount)
        {
            return false;
        }

        CurrentMana -= amount;
        NotifyPlayerStatsChanged();
        return true;
    }

    /// <summary>
    /// Restores mana points up to the maximum value.
    /// </summary>
    /// <param name="amount">The amount of mana to restore.</param>
    /// <returns>The actual mana restored.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is negative.</exception>
    public int RestoreMana(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Mana restoration cannot be negative.");
        }

        if (amount == 0 || CurrentMana >= MaxMana)
        {
            return 0;
        }

        var previousMana = CurrentMana;
        CurrentMana = Math.Clamp(CurrentMana + amount, 0, MaxMana);

        if (CurrentMana != previousMana)
        {
            NotifyPlayerStatsChanged();
        }

        return CurrentMana - previousMana;
    }

    /// <summary>
    /// Recalculates derived statistics after attributes change.
    /// </summary>
    public void RecalculateDerivedAttributes()
    {
        var previousMaxHealth = MaxHealth;
        var previousMaxMana = MaxMana;
        var previousCurrentHealth = CurrentHealth;
        var previousCurrentMana = CurrentMana;

        MaxHealth = BaseHealth + (Constitution * HealthPerConstitutionPoint);
        MaxMana = BaseMana + (Intelligence * ManaPerIntelligencePoint);

        CurrentHealth = NormalizeResourceValue(CurrentHealth, previousMaxHealth, MaxHealth);
        IsDead = CurrentHealth <= 0;
        CurrentMana = NormalizeResourceValue(CurrentMana, previousMaxMana, MaxMana);

        if (CurrentHealth != previousCurrentHealth || CurrentMana != previousCurrentMana)
        {
            NotifyPlayerStatsChanged();
        }

        RefreshCombatStatistics();
        SynchronizeCarryCapacity();
        HandleInventoryWeightChanged(Inventory.GetTotalWeight());
    }

    private void NotifyPlayerStatsChanged()
    {
        if (!IsPlayer)
        {
            return;
        }

        if (Game.Instance?.HUD is { } hud)
        {
            hud.UpdatePlayerStats(CurrentHealth, MaxHealth, CurrentMana, MaxMana);
        }
    }

    private void NotifyStatusEffectsChanged()
    {
        if (!IsPlayer)
        {
            return;
        }

        if (Game.Instance?.HUD is { } hud)
        {
            hud.UpdateStatusEffects(StatusEffects);
        }
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
