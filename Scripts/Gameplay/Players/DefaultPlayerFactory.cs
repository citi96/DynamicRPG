using DynamicRPG.Characters;
using DynamicRPG.Items;
using DynamicRPG.World;
using DynamicRPG.World.Locations;

#nullable enable

namespace DynamicRPG.Gameplay.Players;

/// <summary>
/// Creates the story protagonist with a baseline inventory and statistics.
/// </summary>
public sealed class DefaultPlayerFactory : IPlayerFactory
{
    /// <inheritdoc />
    public Character CreatePlayer(Region startingRegion, Location startingLocation)
    {
        var player = new Character
        {
            Name = "Aldren il Vigile",
            Background = "Miliziano di Frontiera",
            IsPlayer = true,
            Strength = 10,
            Dexterity = 10,
            Constitution = 10,
            Intelligence = 10,
            Wisdom = 10,
            Charisma = 10,
        };

        player.RecalculateDerivedAttributes();

        player.Skills["OneHandedWeapons"] = 15;
        player.Skills["Defense"] = 10;
        player.Skills["Lore"] = 5;

        player.LearnTrait(TraitCatalog.Alert);

        var sword = new Item
        {
            Name = "Spada Arrugginita",
            Type = "Weapon",
            Description = "Una vecchia spada di ordinanza, ancora affidabile nonostante la ruggine.",
            Weight = 5,
            Value = 10,
            MinDamage = 1,
            MaxDamage = 6,
            AccuracyBonus = 1,
        };

        var tunic = new Item
        {
            Name = "Tunica Logora",
            Type = "Armor",
            Description = "Vestiario imbottito che offre una minima protezione.",
            Weight = 3,
            Value = 6,
            DefenseBonus = 1,
        };

        var bread = new Item
        {
            Name = "Pane Secco",
            Type = "Consumable",
            Description = "Un tozzo di pane duro ma nutriente.",
            Weight = 0.5,
            Value = 1,
            Effect = "Ripristina il 10% della fame",
        };

        var waterskin = new Item
        {
            Name = "Otre di Acqua",
            Type = "Consumable",
            Description = "Una sacca di cuoio riempita con acqua potabile.",
            Weight = 1.5,
            Value = 2,
            Effect = "Disseta il viaggiatore e riduce la stanchezza",
        };

        player.Inventory.AddItem(sword);
        player.Inventory.AddItem(tunic);
        player.Inventory.AddItem(bread);
        player.Inventory.AddItem(waterskin);

        player.EquipItem(sword);
        player.EquipItem(tunic);

        player.CurrentRegion = startingRegion;
        player.CurrentLocation = startingLocation;

        return player;
    }
}
