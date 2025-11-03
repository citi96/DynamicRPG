using System;
using System.Collections.Generic;
using System.Linq;
using DynamicRPG.Characters;
using DynamicRPG.Gameplay.Map;
using DynamicRPG.Gameplay.Players;
using DynamicRPG.Gameplay.World;
using DynamicRPG.Systems.Combat;
using DynamicRPG.Systems.Time;
using DynamicRPG.UI;
using DynamicRPG.World;
using DynamicRPG.World.Exploration;
using DynamicRPG.World.Locations;
using Godot;

#nullable enable

namespace DynamicRPG;

/// <summary>
/// Central entry point coordinating high-level systems such as world generation and UI wiring.
/// </summary>
public partial class Game : Node2D
{
    public static Game? Instance { get; private set; }

    private readonly IWorldGenerator _worldGenerator = new ProceduralWorldGenerator();
    private readonly IWorldMapBuilder _worldMapBuilder = new BiomeMapBuilder();
    private readonly IPlayerFactory _playerFactory = new DefaultPlayerFactory();

    private WorldGenerationResult _worldState = WorldGenerationResult.Empty;

    private Node2D? _world;
    private TileMapLayer? _worldTileLayer;
    private Node? _explorationRoot;
    private ExplorationController? _explorationController;
    private CanvasLayer? _uiLayer;

    private Node? _digitalGameMaster;
    private Node? _questManager;

    public Hud? Hud { get; private set; }

    public CombatManager? CombatManager { get; private set; }

    public PlayerController? PlayerController { get; private set; }

    public Node? ExplorationRoot => _explorationRoot;

    public TimeManager TimeManager { get; } = new();

    public Region CurrentRegion { get; private set; } = null!;
    public Location CurrentLocation { get; private set; } = null!;

    /// <summary>
    /// Gets the primary player-controlled character instance.
    /// </summary>
    public Character Player { get; private set; } = null!;

    /// <summary>
    /// Exposes the generated world regions for read-only access.
    /// </summary>
    public IReadOnlyList<Region> WorldRegions => _worldState.Regions;

    /// <summary>
    /// Exposes the generated world locations for read-only access.
    /// </summary>
    public IReadOnlyList<Location> WorldLocations => _worldState.Locations;

    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
    }

    public override void _Ready()
    {
        base._Ready();

        CacheSceneReferences();
        LinkUiWithCombatSystems();
        InitializeGlobalSingletons();

        TimeManager.OnNewDay += HandleNewDay;

        _worldState = _worldGenerator.GenerateWorld();
        BuildWorldMap();
        InitializeStartingLocation();
        InitializePlayer();
        InitializeWeatherForRegions();
        ApplyExplorationTheme(CurrentRegion);

        _explorationController?.RefreshFromGame();

        GD.Print($"Mondo generato con {WorldRegions.Count} regioni, posizione iniziale: {CurrentLocation.Name}");
        GD.Print("Game Started");
    }

    public override void _ExitTree()
    {
        TimeManager.OnNewDay -= HandleNewDay;
        CombatManager = null;
        _explorationRoot = null;
        _explorationController = null;
        PlayerController = null;
        Hud = null;
        _uiLayer = null;
        _world = null;
        Instance = null;

        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        // TODO: Update global timers or world simulation systems here.
        // TODO: Update AI systems or other per-frame managers here.
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
            Hud?.AddLogMessage("Sei già impegnato in un combattimento!");
            return false;
        }

        var enemyList = enemies.Where(character => character is not null).ToList();
        if (enemyList.Count == 0)
        {
            GD.PushWarning("Nessun nemico valido fornito per l'incontro.");
            return false;
        }

        var party = new List<Character> { Player };

        Hud?.ClearLog();

        if (!string.IsNullOrWhiteSpace(encounterDescription))
        {
            Hud?.AddLogMessage(encounterDescription);
        }

        CombatManager.StartCombat(party, enemyList);
        return true;
    }

    private void CacheSceneReferences()
    {
        _world = GetNodeOrNull<Node2D>("World");
        _explorationRoot = _world?.GetNodeOrNull<Node>("Exploration");
        _explorationController = _explorationRoot as ExplorationController;
        PlayerController = _explorationController?.Player
            ?? _explorationRoot?.GetNodeOrNull<PlayerController>("World/Player");
        CombatManager = _world?.GetNodeOrNull<CombatManager>("Combat");
        _uiLayer = GetNodeOrNull<CanvasLayer>("UI");
        Hud = _uiLayer?.GetNodeOrNull<Hud>("HUD");
    }

    private void LinkUiWithCombatSystems()
    {
        if (Hud is { } hud && CombatManager is { } combatManager)
        {
            combatManager.ActionMenu = hud.ActionMenu;
            hud.ActionMenu?.HideMenu();
        }
    }

    private void InitializeGlobalSingletons()
    {
        // TODO: Initialize the digital game master singleton when available.
        _digitalGameMaster = null;

        // TODO: Initialize the quest manager singleton when available.
        _questManager = null;
    }

    private void BuildWorldMap()
    {
        if (_world is null)
        {
            GD.PushWarning("World node non trovato: impossibile creare la mappa.");
            return;
        }

        if (_worldTileLayer is { } existingLayer && existingLayer.IsInsideTree())
        {
            existingLayer.QueueFree();
            _worldTileLayer = null;
        }

        if (WorldRegions.Count == 0)
        {
            return;
        }

        var tileMapLayer = _worldMapBuilder.BuildBiomeMap(WorldRegions);
        _world.AddChild(tileMapLayer);
        _worldTileLayer = tileMapLayer;
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

    private void InitializePlayer()
    {
        Player = _playerFactory.CreatePlayer(CurrentRegion, CurrentLocation);

        Hud?.UpdatePlayerStats(Player.CurrentHealth, Player.MaxHealth, Player.CurrentMana, Player.MaxMana);
        Hud?.UpdateStatusEffects(Player.GetStatusEffectsDisplay());

        GD.Print($"Giocatore inizializzato: STR={Player.Strength}, HP={Player.HP}/{Player.MaxHP}, arma equip={Player.EquippedWeapon?.Name}");
    }

    private void InitializeWeatherForRegions()
    {
        foreach (var region in WorldRegions)
        {
            TimeManager.UpdateWeather(region);
        }
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

    private void HandleNewDay(int currentDay)
    {
        GD.Print($"Nuovo giorno {currentDay} del mese {TimeManager.CurrentMonth}, anno {TimeManager.CurrentYear}.");

        foreach (var region in WorldRegions)
        {
            TimeManager.UpdateWeather(region);
        }
    }
}
