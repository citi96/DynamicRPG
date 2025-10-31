using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using DynamicRPG.World;
using DynamicRPG.World.Generation;
using DynamicRPG.World.Hazards;
using DynamicRPG.World.Locations;

#nullable enable

namespace DynamicRPG;

public partial class Game : Node2D
{
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

    private Node? _world;
    private CanvasLayer? _ui;

    // Placeholder references for global singletons to be initialized later.
    private Node? _digitalGameMaster;
    private Node? _questManager;

    public Region CurrentRegion { get; private set; } = null!;
    public Location CurrentLocation { get; private set; } = null!;

    /// <summary>
    /// Exposes the generated world regions for read-only access.
    /// </summary>
    public IReadOnlyList<Region> WorldRegions => _worldRegions;

    /// <summary>
    /// Exposes the generated world locations for read-only access.
    /// </summary>
    public IReadOnlyList<Location> WorldLocations => _worldLocations;

    public override void _Ready()
    {
        _world = GetNode<Node>("World");
        _ui = GetNode<CanvasLayer>("UI");

        // TODO: Initialize the digital game master singleton when available.
        _digitalGameMaster = null;

        // TODO: Initialize the quest manager singleton when available.
        _questManager = null;

        GenerateWorld();
        InitializeStartingLocation();

        GD.Print($"Mondo generato con {WorldRegions.Count} regioni, posizione iniziale: {CurrentLocation.Name}");
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
            _regionLocationGenerator.GenerateForRegion(region);
            AssignControllingFaction(region);
            ConnectRegionLocations(region);
            _worldLocations.AddRange(region.Locations);

            GD.Print($"Regione generata: {region.Name} ({region.EnvironmentType}) - Location: {region.Locations.Count}");
        }

        ConnectRegions(_worldRegions);
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

    private void AssignControllingFaction(Region region)
    {
        if (RegionFactionOverrides.TryGetValue(region.Name, out var faction))
        {
            region.ControllingFaction = faction;
            return;
        }

        var environment = region.EnvironmentType.ToLowerInvariant();

        if (environment.Contains("mont") || environment.Contains("tundra") || environment.Contains("ghiacc"))
        {
            region.ControllingFaction = "Clan dei Picchi del Nord";
        }
        else if (environment.Contains("foresta"))
        {
            region.ControllingFaction = "Guardiani di Bosco Profondo";
        }
        else if (environment.Contains("deserto") || environment.Contains("dune"))
        {
            region.ControllingFaction = "Leghe dei Mercanti del Deserto";
        }
        else if (environment.Contains("palud") || environment.Contains("bruma"))
        {
            region.ControllingFaction = "Stirpe degli Stregoni delle Paludi";
        }
        else if (environment.Contains("rovine") || environment.Contains("citt"))
        {
            region.ControllingFaction = "Custodi delle Rovine";
        }
        else
        {
            region.ControllingFaction = "Fazione Indipendente";
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

        var environment = region.EnvironmentType.ToLowerInvariant();

        if (environment.Contains("palud") || environment.Contains("bruma"))
        {
            return _random.NextDouble() < 0.6
                ? new Hazard(
                    "Terreno Velenoso",
                    "toxic_bog",
                    $"Il sentiero tra {from.Name} e {to.Name} attraversa pozze velenose e gas miasmatici.",
                    3)
                : null;
        }

        if (environment.Contains("mont") || environment.Contains("tundra") || environment.Contains("ghiacc"))
        {
            return _random.NextDouble() < 0.4
                ? new Hazard(
                    "Zona Valanghe",
                    "avalanche_zone",
                    $"I pendii ghiacciati tra {from.Name} e {to.Name} possono cedere in una valanga improvvisa.",
                    4)
                : null;
        }

        if (environment.Contains("deserto") || environment.Contains("dune"))
        {
            return _random.NextDouble() < 0.35
                ? new Hazard(
                    "Tempesta di Sabbia",
                    "sandstorm_corridor",
                    $"Il percorso verso {to.Name} è spesso avvolto da sabbie turbinanti che azzerano la visibilità.",
                    3)
                : null;
        }

        if (environment.Contains("foresta"))
        {
            return _random.NextDouble() < 0.3
                ? new Hazard(
                    "Sentiero dei Predatori",
                    "predator_ambush",
                    $"Creature fameliche tendono agguati lungo il sentiero boscoso tra {from.Name} e {to.Name}.",
                    2)
                : null;
        }

        if (environment.Contains("rovine") || environment.Contains("citt"))
        {
            return _random.NextDouble() < 0.45
                ? new Hazard(
                    "Infestazione di Non Morti",
                    "undead_horde",
                    $"Tra {from.Name} e {to.Name} vagano orde di non morti inquiete nelle rovine crollate.",
                    4)
                : null;
        }

        return null;
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
