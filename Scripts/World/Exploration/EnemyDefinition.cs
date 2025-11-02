using DynamicRPG.Characters;
using DynamicRPG.Items;
using Godot;

#nullable enable

namespace DynamicRPG.World.Exploration;

/// <summary>
/// Describes the data required to spawn a combat-ready enemy during the exploration phase.
/// </summary>
[GlobalClass]
public partial class EnemyDefinition : Resource
{
    [Export]
    public string Name { get; set; } = "Predone senza volto";

    [Export(PropertyHint.MultilineText)]
    public string Background { get; set; } = "Un avversario generico in cerca di guai.";

    [Export]
    public int Strength { get; set; } = 10;

    [Export]
    public int Dexterity { get; set; } = 10;

    [Export]
    public int Constitution { get; set; } = 10;

    [Export]
    public int Intelligence { get; set; } = 9;

    [Export]
    public int Wisdom { get; set; } = 9;

    [Export]
    public int Charisma { get; set; } = 9;

    [Export]
    public string WeaponName { get; set; } = "Arma improvvisata";

    [Export(PropertyHint.MultilineText)]
    public string WeaponDescription { get; set; } = "Uno strumento rudimentale ma efficace.";

    [Export]
    public int WeaponMinDamage { get; set; } = 1;

    [Export]
    public int WeaponMaxDamage { get; set; } = 6;

    [Export]
    public int WeaponAccuracyBonus { get; set; } = 0;

    [Export]
    public double WeaponWeight { get; set; } = 2.5;

    [Export]
    public int WeaponValue { get; set; } = 6;

    [Export]
    public string ArmorName { get; set; } = string.Empty;

    [Export(PropertyHint.MultilineText)]
    public string ArmorDescription { get; set; } = string.Empty;

    [Export]
    public int ArmorDefenseBonus { get; set; } = 0;

    [Export]
    public double ArmorWeight { get; set; } = 4.0;

    [Export]
    public int ArmorValue { get; set; } = 10;

    /// <summary>
    /// Creates an instance of <see cref="Character"/> ready to participate in combat.
    /// </summary>
    /// <returns>The configured character.</returns>
    public Character CreateCharacter()
    {
        var character = new Character
        {
            Name = Name,
            Background = Background,
            IsPlayer = false,
            Strength = Strength,
            Dexterity = Dexterity,
            Constitution = Constitution,
            Intelligence = Intelligence,
            Wisdom = Wisdom,
            Charisma = Charisma,
        };

        character.RecalculateDerivedAttributes();

        if (!string.IsNullOrWhiteSpace(WeaponName))
        {
            var weapon = new Item
            {
                Name = WeaponName,
                Type = "Weapon",
                Description = WeaponDescription,
                Weight = WeaponWeight,
                Value = WeaponValue,
                MinDamage = WeaponMinDamage,
                MaxDamage = WeaponMaxDamage,
                AccuracyBonus = WeaponAccuracyBonus,
            };

            character.Inventory.AddItem(weapon);
            character.EquipItem(weapon);
        }

        if (!string.IsNullOrWhiteSpace(ArmorName) && ArmorDefenseBonus > 0)
        {
            var armor = new Item
            {
                Name = ArmorName,
                Type = "Armor",
                Description = ArmorDescription,
                Weight = ArmorWeight,
                Value = ArmorValue,
                DefenseBonus = ArmorDefenseBonus,
            };

            character.Inventory.AddItem(armor);
            character.EquipItem(armor);
        }

        character.RecalculateDerivedAttributes();

        return character;
    }
}
