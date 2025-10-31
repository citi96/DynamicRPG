namespace DynamicRPG.World.Generation;

using System;

/// <summary>
/// Defines the procedural generation blueprint for a specific environment type.
/// </summary>
public sealed class LocationGenerationProfile
{
    public static readonly LocationGenerationProfile Forest = new()
    {
        MajorSettlements = 1,
        MinorSettlementRange = (2, 3),
        Outposts = 0,
        DungeonRange = (2, 3),
        DungeonPrefix = "Caverna",
        RuinRange = (1, 1),
        LandmarkRange = (1, 2),
        LandmarkDescriptors = new[] { "Albero Antico", "Cerchio di Pietre", "Groviglio Spirituale" },
        ResourceRange = (2, 4),
        ResourceDescriptors = new[] { "Bosco", "Radura", "Santuario delle Erbe" },
    };

    public static readonly LocationGenerationProfile Desert = new()
    {
        MajorSettlements = 0,
        MinorSettlementRange = (1, 2),
        Outposts = 1,
        DungeonRange = (1, 2),
        DungeonPrefix = "Tempio",
        RuinRange = (2, 3),
        LandmarkRange = (1, 2),
        LandmarkDescriptors = new[] { "Obelisco", "Colonna del Sole", "Oasi" },
        ResourceRange = (1, 2),
        ResourceDescriptors = new[] { "Pozzo", "Deposito di Spezie", "Giacimento di Cristalli" },
    };

    public static readonly LocationGenerationProfile Tundra = new()
    {
        MajorSettlements = 1,
        MinorSettlementRange = (1, 2),
        Outposts = 2,
        DungeonRange = (1, 2),
        DungeonPrefix = "Cripta",
        RuinRange = (1, 2),
        LandmarkRange = (1, 1),
        LandmarkDescriptors = new[] { "Monolite del Gelo", "Picco del Vento" },
        ResourceRange = (1, 2),
        ResourceDescriptors = new[] { "Miniera", "Campo di Ghiaccio" },
    };

    public static readonly LocationGenerationProfile Swamp = new()
    {
        MajorSettlements = 0,
        MinorSettlementRange = (2, 3),
        Outposts = 1,
        DungeonRange = (2, 3),
        DungeonPrefix = "Caverna",
        RuinRange = (1, 2),
        LandmarkRange = (1, 2),
        LandmarkDescriptors = new[] { "Albero del Fato", "Idolo Sommerso" },
        ResourceRange = (2, 3),
        ResourceDescriptors = new[] { "Erbario", "Pozza Alchemica", "Colonia di Funghi" },
    };

    public static readonly LocationGenerationProfile Ruinscape = new()
    {
        MajorSettlements = 1,
        MinorSettlementRange = (1, 1),
        Outposts = 1,
        DungeonRange = (2, 3),
        DungeonPrefix = "Tempio",
        RuinRange = (3, 4),
        LandmarkRange = (1, 2),
        LandmarkDescriptors = new[] { "Biblioteca Perduta", "Anfiteatro", "Portale Infranto" },
        ResourceRange = (1, 2),
        ResourceDescriptors = new[] { "Archivio", "Laboratorio" },
    };

    public static readonly LocationGenerationProfile Generic = new()
    {
        MajorSettlements = 1,
        MinorSettlementRange = (1, 2),
        Outposts = 0,
        DungeonRange = (1, 2),
        DungeonPrefix = "Dungeon",
        RuinRange = (1, 2),
        LandmarkRange = (1, 2),
        LandmarkDescriptors = new[] { "Monumento", "Torre" },
        ResourceRange = (1, 2),
        ResourceDescriptors = new[] { "Deposito", "Campo" },
    };

    public int MajorSettlements { get; init; }

    public (int min, int max) MinorSettlementRange { get; init; }

    public int Outposts { get; init; }

    public (int min, int max) DungeonRange { get; init; }

    public string DungeonPrefix { get; init; } = "Dungeon";

    public (int min, int max) RuinRange { get; init; }

    public (int min, int max) LandmarkRange { get; init; }

    public string[] LandmarkDescriptors { get; init; } = Array.Empty<string>();

    public (int min, int max) ResourceRange { get; init; }

    public string[] ResourceDescriptors { get; init; } = Array.Empty<string>();
}
