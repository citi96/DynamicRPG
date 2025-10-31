using System.Collections.Generic;
using Godot;
using DynamicRPG.World;

#nullable enable

namespace DynamicRPG;

public partial class Game : Node2D
{
    private readonly List<Region> _worldRegions = new();

    private Node? _world;
    private CanvasLayer? _ui;

    // Placeholder references for global singletons to be initialized later.
    private Node? _digitalGameMaster;
    private Node? _questManager;

    /// <summary>
    /// Exposes the generated world regions for read-only access.
    /// </summary>
    public IReadOnlyList<Region> WorldRegions => _worldRegions;

    public override void _Ready()
    {
        _world = GetNode<Node>("World");
        _ui = GetNode<CanvasLayer>("UI");

        // TODO: Initialize the digital game master singleton when available.
        _digitalGameMaster = null;

        // TODO: Initialize the quest manager singleton when available.
        _questManager = null;

        GenerateWorld();

        GD.Print("Game Started");
    }

    public override void _Process(double delta)
    {
        // TODO: Update global timers or world simulation systems here.

        // TODO: Update AI systems or other per-frame managers here.
    }

    /// <summary>
    /// Generates the initial world regions using a procedural approach placeholder.
    /// </summary>
    private void GenerateWorld()
    {
        _worldRegions.Clear();

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
            GD.Print($"Regione generata: {region.Name} ({region.EnvironmentType})");
        }
    }
}
