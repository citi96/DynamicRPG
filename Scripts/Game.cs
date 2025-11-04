using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using DynamicRPG.Characters;
using DynamicRPG.Items;
using DynamicRPG.Systems.Combat;
using DynamicRPG.Systems.Time;
using DynamicRPG.World;
using DynamicRPG.World.Generation;
using DynamicRPG.World.Hazards;
using DynamicRPG.World.Locations;
using DynamicRPG.World.Weather;
using DynamicRPG.UI;
using DynamicRPG.World.Exploration;

#nullable enable

namespace DynamicRPG;

public partial class Game : Node2D
{
    public static Game? Instance { get; private set; }

    private static readonly Dictionary<string, string> RegionFactionOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Montagne del Nord"] = "Clan del Picco del Gelo",
        ["Foresta Est"] = "Circolo dei Druidi di Luminwald",
        ["Deserto Sud"] = "Consorzio dei Nomadi delle Dune",
        ["Paludi Ovest"] = "Congrega della Nebbia Miasmatica",
        ["Regno Centrale"] = "Rifondazione del Regno Umano",
    };

    private readonly Random _random = new();
    private readonly List<Region> _worldRegions = new();
    private readonly List<Location> _worldLocations = new();
    private readonly RegionLocationGenerator _regionLocationGenerator = new();

    private Node2D? _world;
    private TileMapLayer? _worldTileLayer;
    private Node? _explorationRoot;
    private ExplorationController? _explorationController;
    private CanvasLayer? _ui;

    // Placeholder references for global singletons to be initialized later.
    private Node? _digitalGameMaster;
    private Node? _questManager;

    public HUD? HUD { get; private set; }

    public CombatManager? CombatManager { get; private set; }

    public PlayerController? PlayerController { get; private set; }

    public Node? ExplorationRoot => _explorationRoot;

    public TimeManager TimeMgr { get; } = new();

    public Region CurrentRegion { get; private set; } = null!;
    public Location CurrentLocation { get; private set; } = null!;

    /// <summary>
    /// Gets the primary player-controlled character instance.
    /// </summary>
    public Character Player { get; private set; } = null!;

    /// <summary>
    /// Exposes the generated world regions for read-only access.
    /// </summary>
    public IReadOnlyList<Region> WorldRegions => _worldRegions;

    /// <summary>
    /// Exposes the generated world locations for read-only access.
    /// </summary>
    public IReadOnlyList<Location> WorldLocations => _worldLocations;

    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
    }

    public override void _Ready()
    {
        base._Ready();

        _world = GetNode<Node2D>("World");
        _explorationRoot = _world.GetNodeOrNull<Node>("Exploration");
        _explorationController = _explorationRoot as ExplorationController;
        PlayerController = _explorationController?.Player
            ?? _explorationRoot?.GetNodeOrNull<PlayerController>("World/Player");
        CombatManager = _world.GetNodeOrNull<CombatManager>("Combat");
        _ui = GetNode<CanvasLayer>("UI");

        HUD = _ui?.GetNodeOrNull<HUD>("HUD");

        if (HUD is { } hud && CombatManager is { } combatManager)
        {
            combatManager.ActionMenu = hud.ActionMenu;
            hud.ActionMenu?.HideMenu();
        }

        // TODO: Initialize the digital game master singleton when available.
        _digitalGameMaster = null;

        // TODO: Initialize the quest manager singleton when available.
        _questManager = null;

        TimeMgr.OnNewDay += HandleNewDay;

        GenerateWorld();
        CreateMap();
        InitializeStartingLocation();
        InitializePlayerCharacter();
        InitializeWeatherForRegions();
        ApplyExplorationTheme(CurrentRegion);

        _explorationController?.RefreshFromGame();

        GD.Print($"Mondo generato con {WorldRegions.Count} regioni, posizione iniziale: {CurrentLocation.Name}");
        GD.Print("Game Started");
    }

    public override void _ExitTree()
    {
        TimeMgr.OnNewDay -= HandleNewDay;
        CombatManager = null;
        _explorationRoot = null;
        _explorationController = null;
        PlayerController = null;
        HUD = null;
        _ui = null;
        _world = null;
        Instance = null;

        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        // TODO: Update global timers or world simulation systems here.

        // TODO: Update AI systems or other per-frame managers here.
    }

    private void HandleNewDay(int currentDay)
    {
        // Placeholder hook for future world refresh logic (merchants, resource respawns, etc.).
        GD.Print($"Nuovo giorno {currentDay} del mese {TimeMgr.CurrentMonth}, anno {TimeMgr.CurrentYear}.");

        foreach (var region in _worldRegions)
        {
            TimeMgr.UpdateWeather(region);
        }
    }

    /// <summary>
    /// Creates a simple tile-based visualization of the generated world biomes.
    /// </summary>
    private void CreateMap()
    {
        if (_world is null)
        {
            GD.PushWarning("World node non trovato: impossibile creare la mappa.");
            return;
        }

        if (_worldTileLayer is not null && _worldTileLayer.IsInsideTree())
        {
            _worldTileLayer.QueueFree();
            _worldTileLayer = null;
        }

        if (_worldRegions.Count == 0)
        {
            return;
        }

        // Creazione della TileMapLayer e del TileSet base.
        var tileMapLayer = new TileMapLayer
        {
            Name = "BiomeMap",
        };

        var tileSet = new TileSet
        {
            TileSize = new Vector2I(32, 32),
        };
        tileMapLayer.TileSet = tileSet;

        // Dizionario per riutilizzare le texture monocromatiche per biomi simili.
        var biomeTileSources = new Dictionary<Color, (int sourceId, TileSetAtlasSource source)>();

        // Posizionamento dei tile su una griglia quadrata in base al numero di regioni generate.
        var regionCount = _worldRegions.Count;
        var columns = Mathf.CeilToInt(Mathf.Sqrt(regionCount));

        for (var index = 0; index < regionCount; index++)
        {
            var region = _worldRegions[index];
            var biomeColor = GetBiomeColor(region.EnvironmentType);

            if (!biomeTileSources.TryGetValue(biomeColor, out var atlasData))
            {
                // Generazione della texture 32x32 riempita con il colore del bioma.
                var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
                image.Fill(biomeColor);
                var texture = ImageTexture.CreateFromImage(image);

                // Creazione della tile nel TileSet usando la texture appena generata.
                var atlasSource = new TileSetAtlasSource
                {
                    Texture = texture,
                    TextureRegionSize = new Vector2I(32, 32),
                };
                var sourceId = tileSet.AddSource(atlasSource);
                atlasSource.CreateTile(Vector2I.Zero);

                atlasData = (sourceId, atlasSource);
                biomeTileSources.Add(biomeColor, atlasData);
            }

            var column = index % columns;
            var row = index / columns;
            var cellPosition = new Vector2I(column, row);

            // Riempimento della griglia con il tile associato al bioma.
            tileMapLayer.SetCell(cellPosition, atlasData.sourceId, Vector2I.Zero);
        }

        tileMapLayer.TileSet = tileSet;

        _world.AddChild(tileMapLayer);
        _worldTileLayer = tileMapLayer;
    }

    private static Color GetBiomeColor(string environmentType)
    {
        return RegionEnvironmentClassifier.FromEnvironment(environmentType) switch
        {
            RegionEnvironmentCategory.Arctic => new Color(0.65f, 0.85f, 1.0f),
            RegionEnvironmentCategory.Forest => new Color(0.2f, 0.6f, 0.2f),
            RegionEnvironmentCategory.Desert => new Color(0.9f, 0.8f, 0.55f),
            RegionEnvironmentCategory.Swamp => new Color(0.35f, 0.45f, 0.25f),
            RegionEnvironmentCategory.Ruins => new Color(0.5f, 0.5f, 0.55f),
            _ => new Color(0.6f, 0.6f, 0.6f),
        };
    }

    /// <summary>
    /// Generates the initial world regions using a procedural approach placeholder.
    /// </summary>
    private void GenerateWorld()
    {
        _worldRegions.Clear();
        _worldLocations.Clear();

        var regions = new List<Region>
        {
            new()
            {
                Name = "Montagne del Nord",
                EnvironmentType = "Tundra Ghiacciata",
            },
            new()
            {
                Name = "Foresta Est",
                EnvironmentType = "Foresta Infestata",
            },
            new()
            {
                Name = "Deserto Sud",
                EnvironmentType = "Dune Roventi",
            },
            new()
            {
                Name = "Paludi Ovest",
                EnvironmentType = "Bruma Miasmatica",
            },
            new()
            {
                Name = "Regno Centrale",
                EnvironmentType = "Rovine di Città",
            },
        };

        _worldRegions.AddRange(regions);

        foreach (var region in _worldRegions)
        {
            InitializeRegionClimate(region);
            _regionLocationGenerator.GenerateForRegion(region);
            AssignControllingFaction(region);
            ConnectRegionLocations(region);
            _worldLocations.AddRange(region.Locations);

            GD.Print($"Regione generata: {region.Name} ({region.EnvironmentType}) - Location: {region.Locations.Count}");
        }

        ConnectRegions(_worldRegions);
    }

    private void InitializeRegionClimate(Region region)
    {
        region.BaseClimate = RegionClimateResolver.Resolve(region.EnvironmentType);
    }

    private void InitializeStartingLocation()
    {
        CurrentRegion = WorldRegions
            .FirstOrDefault(region => region.Name.Contains("Regno", StringComparison.OrdinalIgnoreCase))
            ?? WorldRegions.FirstOrDefault()
            ?? throw new InvalidOperationException("Nessuna regione è stata generata per l'inizializzazione del giocatore.");

        CurrentLocation = CurrentRegion.Locations
            .FirstOrDefault(location => location.Type == LocationType.Village)
            ?? CurrentRegion.Locations.FirstOrDefault()
            ?? throw new InvalidOperationException($"La regione {CurrentRegion.Name} non contiene location valide per l'inizializzazione del giocatore.");
    }

    private void InitializePlayerCharacter()
    {
        Player = new Character
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

        Player.RecalculateDerivedAttributes();

        Player.Skills["OneHandedWeapons"] = 15;
        Player.Skills["Defense"] = 10;
        Player.Skills["Lore"] = 5;

        Player.LearnTrait(TraitCatalog.Alert);

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

        Player.Inventory.AddItem(sword);
        Player.Inventory.AddItem(tunic);
        Player.Inventory.AddItem(bread);
        Player.Inventory.AddItem(waterskin);

        Player.EquipItem(sword);
        Player.EquipItem(tunic);

        Player.CurrentRegion = CurrentRegion;
        Player.CurrentLocation = CurrentLocation;

        HUD?.UpdatePlayerStats(Player.CurrentHealth, Player.MaxHealth, Player.CurrentMana, Player.MaxMana);
        HUD?.UpdateStatusEffects(Player.StatusEffects);

        GD.Print($"Giocatore inizializzato: STR={Player.Strength}, HP={Player.HP}/{Player.MaxHP}, arma equip={Player.EquippedWeapon?.Name}");
    }

    /// <summary>
    /// Attempts to start a combat encounter from the exploration scene against the provided enemies.
    /// </summary>
    /// <param name="enemies">The enemy combatants that should join the encounter.</param>
    /// <param name="encounterDescription">Optional narrative description displayed to the player.</param>
    /// <returns><c>true</c> when the encounter has been started; otherwise <c>false</c>.</returns>
    public bool TryStartExplorationEncounter(IEnumerable<Character> enemies, string? encounterDescription = null)
    {
        ArgumentNullException.ThrowIfNull(enemies);

        if (CombatManager is null)
        {
            GD.PushWarning("Impossibile avviare il combattimento: nessun CombatManager attivo.");
            return false;
        }

        if (Player is null)
        {
            GD.PushWarning("Il personaggio del giocatore non è stato inizializzato.");
            return false;
        }

        if (CombatManager.IsCombatActive)
        {
            HUD?.AddLogMessage("Sei già impegnato in un combattimento!");
            return false;
        }

        var enemyList = enemies.Where(character => character is not null).ToList();
        if (enemyList.Count == 0)
        {
            GD.PushWarning("Nessun nemico valido fornito per l'incontro.");
            return false;
        }

        var party = new List<Character> { Player };

        HUD?.ClearLog();

        if (!string.IsNullOrWhiteSpace(encounterDescription))
        {
            HUD?.AddLogMessage(encounterDescription);
        }

        CombatManager.StartCombat(party, enemyList);
        return true;
    }

    private void ApplyExplorationTheme(Region region)
    {
        if (_explorationController is null)
        {
            return;
        }

        var theme = ExplorationThemeResolver.Resolve(region.EnvironmentType);
        _explorationController.ApplyTheme(theme);
    }

    private void AssignControllingFaction(Region region)
    {
        if (RegionFactionOverrides.TryGetValue(region.Name, out var faction))
        {
            region.ControllingFaction = faction;
            return;
        }

        region.ControllingFaction = RegionEnvironmentClassifier.FromEnvironment(region.EnvironmentType) switch
        {
            RegionEnvironmentCategory.Arctic => "Clan dei Picchi del Nord",
            RegionEnvironmentCategory.Forest => "Guardiani di Bosco Profondo",
            RegionEnvironmentCategory.Desert => "Leghe dei Mercanti del Deserto",
            RegionEnvironmentCategory.Swamp => "Stirpe degli Stregoni delle Paludi",
            RegionEnvironmentCategory.Ruins => "Custodi delle Rovine",
            _ => "Fazione Indipendente",
        };
    }

    private void InitializeWeatherForRegions()
    {
        foreach (var region in _worldRegions)
        {
            TimeMgr.UpdateWeather(region);
        }
    }

    private void ConnectRegionLocations(Region region)
    {
        if (region.Locations.Count == 0)
        {
            return;
        }

        var settlements = region.Locations
            .Where(location => location.Type is LocationType.City or LocationType.Village or LocationType.Outpost)
            .ToList();

        var dungeons = region.Locations
            .Where(location => location.Type == LocationType.Dungeon)
            .ToList();

        var pointsOfInterest = region.Locations
            .Where(location => location.Type is LocationType.Ruin or LocationType.Resource or LocationType.Landmark)
            .ToList();

        var hub = SelectPrimarySettlement(settlements) ?? region.Locations.FirstOrDefault();

        if (hub is not null)
        {
            foreach (var settlement in settlements)
            {
                if (ReferenceEquals(settlement, hub))
                {
                    continue;
                }

                AddBidirectionalConnection(hub, settlement, region);
            }
        }

        if (settlements.Count > 1)
        {
            var orderedSettlements = settlements
                .OrderBy(location => location.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < orderedSettlements.Count - 1; i++)
            {
                AddBidirectionalConnection(orderedSettlements[i], orderedSettlements[i + 1], region);
            }
        }

        var anchorPool = settlements.Count > 0
            ? settlements
            : hub is not null
                ? new List<Location> { hub }
                : new List<Location>();

        if (anchorPool.Count == 0)
        {
            return;
        }

        foreach (var dungeon in dungeons)
        {
            var anchor = anchorPool[_random.Next(anchorPool.Count)];
            AddBidirectionalConnection(dungeon, anchor, region);
        }

        foreach (var pointOfInterest in pointsOfInterest)
        {
            var anchor = anchorPool[_random.Next(anchorPool.Count)];
            AddBidirectionalConnection(pointOfInterest, anchor, region);
        }
    }

    private static Location? SelectPrimarySettlement(IReadOnlyCollection<Location> settlements)
    {
        return settlements.FirstOrDefault(location => location.Type == LocationType.City)
            ?? settlements.FirstOrDefault(location => location.Type == LocationType.Village)
            ?? settlements.FirstOrDefault(location => location.Type == LocationType.Outpost);
    }

    private static Location? SelectBorderLocation(Region region)
    {
        return region.Locations.FirstOrDefault(location => location.Type == LocationType.Outpost)
            ?? region.Locations.FirstOrDefault(location => location.Type == LocationType.City)
            ?? region.Locations.FirstOrDefault(location => location.Type == LocationType.Village)
            ?? region.Locations.FirstOrDefault();
    }

    private void ConnectRegions(IReadOnlyList<Region> regions)
    {
        if (regions.Count == 0)
        {
            return;
        }

        var centralRegion = regions.FirstOrDefault(region => region.Name.Contains("Regno Centrale", StringComparison.OrdinalIgnoreCase));

        if (centralRegion is null)
        {
            ConnectSequentialRegions(regions);
            return;
        }

        var centralAnchors = centralRegion.Locations
            .Where(location => location.Type is LocationType.Outpost or LocationType.City or LocationType.Village)
            .ToList();

        if (centralAnchors.Count == 0 && centralRegion.Locations.Count > 0)
        {
            centralAnchors.Add(centralRegion.Locations[0]);
        }

        foreach (var region in regions)
        {
            if (ReferenceEquals(region, centralRegion))
            {
                continue;
            }

            var borderLocation = SelectBorderLocation(region);
            if (borderLocation is null || centralAnchors.Count == 0)
            {
                continue;
            }

            var centralBorder = centralAnchors[_random.Next(centralAnchors.Count)];
            AddBidirectionalConnection(borderLocation, centralBorder, region, centralRegion);
        }
    }

    private void ConnectSequentialRegions(IReadOnlyList<Region> regions)
    {
        for (var i = 0; i < regions.Count - 1; i++)
        {
            var originRegion = regions[i];
            var targetRegion = regions[i + 1];

            var originLocation = SelectBorderLocation(originRegion);
            var targetLocation = SelectBorderLocation(targetRegion);

            if (originLocation is null || targetLocation is null)
            {
                continue;
            }

            AddBidirectionalConnection(originLocation, targetLocation, originRegion, targetRegion);
        }
    }

    private void AddBidirectionalConnection(Location from, Location to, params Region?[] hazardRegions)
    {
        if (from is null || to is null || ReferenceEquals(from, to))
        {
            return;
        }

        if (HasExistingConnection(from, to))
        {
            return;
        }

        Hazard? hazard = null;

        foreach (var region in hazardRegions)
        {
            if (region is null)
            {
                continue;
            }

            hazard ??= GenerateHazardForConnection(region, from, to);

            if (hazard is not null)
            {
                break;
            }
        }

        hazard ??= GenerateHazardForConnection(from.Region, from, to);
        hazard ??= GenerateHazardForConnection(to.Region, from, to);

        from.ConnectedLocations.Add(new LocationConnection(to, hazard));
        to.ConnectedLocations.Add(new LocationConnection(from, hazard));

        if (hazard is not null)
        {
            RegisterHazard(from.Region, hazard);
            RegisterHazard(to.Region, hazard);
        }
    }

    private Hazard? GenerateHazardForConnection(Region? region, Location from, Location to)
    {
        if (region is null)
        {
            return null;
        }

        var category = RegionEnvironmentClassifier.FromEnvironment(region.EnvironmentType);
        return GenerateHazardForCategory(category, from, to);
    }

    private Hazard? GenerateHazardForCategory(RegionEnvironmentCategory category, Location from, Location to)
    {
        return category switch
        {
            RegionEnvironmentCategory.Swamp => TryCreateHazard(
                0.6,
                () => new Hazard(
                    "Terreno Velenoso",
                    "toxic_bog",
                    $"Il sentiero tra {from.Name} e {to.Name} attraversa pozze velenose e gas miasmatici.",
                    3)),
            RegionEnvironmentCategory.Arctic => TryCreateHazard(
                0.4,
                () => new Hazard(
                    "Zona Valanghe",
                    "avalanche_zone",
                    $"I pendii ghiacciati tra {from.Name} e {to.Name} possono cedere in una valanga improvvisa.",
                    4)),
            RegionEnvironmentCategory.Desert => TryCreateHazard(
                0.35,
                () => new Hazard(
                    "Tempesta di Sabbia",
                    "sandstorm_corridor",
                    $"Il percorso verso {to.Name} è spesso avvolto da sabbie turbinanti che azzerano la visibilità.",
                    3)),
            RegionEnvironmentCategory.Forest => TryCreateHazard(
                0.3,
                () => new Hazard(
                    "Sentiero dei Predatori",
                    "predator_ambush",
                    $"Creature fameliche tendono agguati lungo il sentiero boscoso tra {from.Name} e {to.Name}.",
                    2)),
            RegionEnvironmentCategory.Ruins => TryCreateHazard(
                0.45,
                () => new Hazard(
                    "Infestazione di Non Morti",
                    "undead_horde",
                    $"Tra {from.Name} e {to.Name} vagano orde di non morti inquiete nelle rovine crollate.",
                    4)),
            _ => null,
        };
    }

    private Hazard? TryCreateHazard(double probability, Func<Hazard> createHazard)
    {
        return _random.NextDouble() < probability ? createHazard() : null;
    }

    private static bool HasExistingConnection(Location from, Location to)
    {
        return from.ConnectedLocations.Any(connection => ReferenceEquals(connection.Target, to));
    }

    private static void RegisterHazard(Region? region, Hazard hazard)
    {
        if (region is null)
        {
            return;
        }

        if (!region.EnvironmentalHazards.Contains(hazard))
        {
            region.EnvironmentalHazards.Add(hazard);
        }
    }
}
