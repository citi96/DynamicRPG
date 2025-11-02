using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynamicRPG.Characters;
using Godot;

#nullable enable

namespace DynamicRPG.World.Exploration;

public partial class ExplorationController : Node2D
{
    private enum TerrainTile
    {
        Grass,
        Path,
        Water,
        Stone,
    }

    private const int DefaultTileSize = 32;

    private static readonly Color DefaultGrassColor = new(0.188f, 0.353f, 0.220f);
    private static readonly Color DefaultPathColor = new(0.474f, 0.349f, 0.204f);
    private static readonly Color DefaultWaterColor = new(0.137f, 0.337f, 0.521f);
    private static readonly Color DefaultStoneColor = new(0.396f, 0.396f, 0.396f);

    private readonly List<Vector2I> _grassCells = new();
    private readonly RandomNumberGenerator _rng = new();
    private readonly Dictionary<TerrainTile, int> _tileSources = new();

    private Node2D? _worldRoot;
    private Node2D? _enemyContainer;
    private TerrainTile[,]? _terrainGrid;
    private Texture2D? _treeTexture;

    [Export]
    public Label? LocationInfoLabel { get; set; }

    [Export]
    public Label? HelpLabel { get; set; }

    [Export]
    public Marker2D? PlayerSpawn { get; set; }

    [Export]
    public PlayerController? Player { get; set; }

    [Export]
    public Rect2 PlayableArea { get; set; } = new Rect2(new Vector2(-640, -360), new Vector2(1280, 720));

    [Export]
    public TileMapLayer? TerrainTileMap { get; set; }

    [Export]
    public Node2D? FoliageContainer { get; set; }

    [Export]
    public Node2D? EnemyContainer { get; set; }

    [Export]
    public PackedScene? EnemyEncounterScene { get; set; }

    [Export(PropertyHint.Range, "1,16,1")]
    public int EncounterCount { get; set; } = 5;

    [Export(PropertyHint.Range, "32,256,1")]
    public int MapWidth { get; set; } = 96;

    [Export(PropertyHint.Range, "32,256,1")]
    public int MapHeight { get; set; } = 72;

    [Export(PropertyHint.Range, "16,128,1")]
    public int TileSize { get; set; } = DefaultTileSize;

    [Export(PropertyHint.Range, "0,500,1")]
    public int TreeCount { get; set; } = 180;

    [Export(PropertyHint.Range, "0.01,0.5,0.01")]
    public float NoiseFrequency { get; set; } = 0.05f;

    [Export]
    public Color GrassColor { get; set; } = DefaultGrassColor;

    [Export]
    public Color PathColor { get; set; } = DefaultPathColor;

    [Export]
    public Color WaterColor { get; set; } = DefaultWaterColor;

    [Export]
    public Color StoneColor { get; set; } = DefaultStoneColor;

    public override void _Ready()
    {
        base._Ready();

        _worldRoot = GetNodeOrNull<Node2D>("World");

        Player ??= GetNodeOrNull<PlayerController>("World/Player");
        LocationInfoLabel ??= GetNodeOrNull<Label>("InfoLayer/Info");
        HelpLabel ??= GetNodeOrNull<Label>("InfoLayer/Help");
        PlayerSpawn ??= GetNodeOrNull<Marker2D>("World/PlayerSpawn");
        TerrainTileMap ??= _worldRoot?.GetNodeOrNull<TileMapLayer>("Terrain");
        FoliageContainer ??= _worldRoot?.GetNodeOrNull<Node2D>("Foliage");
        _enemyContainer = EnemyContainer ?? _worldRoot?.GetNodeOrNull<Node2D>("Encounters");

        RebuildWorld();
    }

    public void RefreshFromGame()
    {
        UpdateLocationInfo();
    }

    /// <summary>
    /// Applies the provided theme to the exploration view and rebuilds the procedural content.
    /// </summary>
    public void ApplyTheme(ExplorationTheme theme)
    {
        GrassColor = theme.GrassColor;
        PathColor = theme.PathColor;
        WaterColor = theme.WaterColor;
        StoneColor = theme.StoneColor;
        TreeCount = theme.TreeCount;
        NoiseFrequency = theme.NoiseFrequency;

        RebuildWorld();
    }

    /// <summary>
    /// Regenerates the procedural terrain, foliage and encounters.
    /// </summary>
    public void RebuildWorld()
    {
        GenerateTerrain();
        PopulateFoliage();
        SpawnEnemyEncounters();
        AlignPlayerToSpawn();

        UpdateLocationInfo();
        UpdateHelpLabel();
    }

    private void GenerateTerrain()
    {
        if (_worldRoot is null)
        {
            GD.PushWarning("World node non trovato: impossibile generare il terreno di esplorazione.");
            return;
        }

        if (MapWidth <= 0 || MapHeight <= 0)
        {
            GD.PushWarning("Dimensioni della mappa non valide; impossibile generare il terreno.");
            return;
        }

        var tileSize = Math.Max(TileSize, DefaultTileSize);

        TerrainTileMap ??= _worldRoot.GetNodeOrNull<TileMapLayer>("Terrain") ?? new TileMapLayer { Name = "Terrain" };
        if (!TerrainTileMap.IsInsideTree())
        {
            _worldRoot.AddChild(TerrainTileMap);
        }

        TerrainTileMap.TileSet = CreateTileSet(tileSize);
        TerrainTileMap.Clear();
        TerrainTileMap.Position = CalculateTerrainOrigin(tileSize);
        TerrainTileMap.YSortEnabled = false;

        _terrainGrid = new TerrainTile[MapWidth, MapHeight];
        _grassCells.Clear();

        var roadCells = BuildMainRoad();
        var noise = CreateTerrainNoise();

        for (var x = 0; x < MapWidth; x++)
        {
            for (var y = 0; y < MapHeight; y++)
            {
                var cell = new Vector2I(x, y);
                var tileType = DetermineTerrainTile(cell, noise, roadCells);
                _terrainGrid[x, y] = tileType;

                if (tileType == TerrainTile.Grass)
                {
                    _grassCells.Add(cell);
                }

                var sourceId = _tileSources[tileType];
                TerrainTileMap.SetCell(cell, sourceId, Vector2I.Zero, 0);
            }
        }

        UpdatePlayableBounds(tileSize);
    }

    private TileSet CreateTileSet(int tileSize)
    {
        var tileSet = new TileSet
        {
            TileSize = new Vector2I(tileSize, tileSize),
        };

        _tileSources.Clear();
        _tileSources[TerrainTile.Grass] = CreateColorTile(tileSet, GrassColor);
        _tileSources[TerrainTile.Path] = CreateColorTile(tileSet, PathColor);
        _tileSources[TerrainTile.Water] = CreateColorTile(tileSet, WaterColor);
        _tileSources[TerrainTile.Stone] = CreateColorTile(tileSet, StoneColor);

        return tileSet;
    }

    private static int CreateColorTile(TileSet tileSet, Color color)
    {
        var image = Image.CreateEmpty(tileSet.TileSize.X, tileSet.TileSize.Y, false, Image.Format.Rgba8);
        image.Fill(color);
        var texture = ImageTexture.CreateFromImage(image);

        var atlasSource = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = tileSet.TileSize,
        };

        var sourceId = tileSet.AddSource(atlasSource);
        atlasSource.CreateTile(Vector2I.Zero);

        return sourceId;
    }

    private FastNoiseLite CreateTerrainNoise() => new()
    {
        Seed = _rng.RandiRange(int.MinValue, int.MaxValue),
        Frequency = NoiseFrequency,
        FractalOctaves = 2,
        FractalGain = 0.45f,
        FractalLacunarity = 2.0f,
    };

    private HashSet<Vector2I> BuildMainRoad()
    {
        var road = new HashSet<Vector2I>();
        var currentY = MapHeight / 2;
        _rng.Randomize();

        for (var x = 0; x < MapWidth; x++)
        {
            var cell = new Vector2I(x, currentY);
            road.Add(cell);

            if (_rng.Randf() < 0.35f)
            {
                currentY = Mathf.Clamp(currentY + _rng.RandiRange(-1, 1), 2, Math.Max(MapHeight - 3, 2));
            }
        }

        var expanded = new HashSet<Vector2I>(road);
        foreach (var cell in road)
        {
            expanded.Add(new Vector2I(cell.X, Mathf.Clamp(cell.Y + 1, 0, MapHeight - 1)));
            expanded.Add(new Vector2I(cell.X, Mathf.Clamp(cell.Y - 1, 0, MapHeight - 1)));
        }

        return expanded;
    }

    private TerrainTile DetermineTerrainTile(Vector2I cell, FastNoiseLite noise, HashSet<Vector2I> roadCells)
    {
        if (roadCells.Contains(cell))
        {
            return TerrainTile.Path;
        }

        var noiseValue = noise.GetNoise2D(cell.X, cell.Y);

        if (noiseValue < -0.35f)
        {
            return TerrainTile.Water;
        }

        if (noiseValue > 0.45f)
        {
            return TerrainTile.Stone;
        }

        return TerrainTile.Grass;
    }

    private Vector2 CalculateTerrainOrigin(int tileSize)
    {
        var halfWidth = MapWidth * tileSize / 2f;
        var halfHeight = MapHeight * tileSize / 2f;
        return new Vector2(-halfWidth, -halfHeight);
    }

    private void UpdatePlayableBounds(int tileSize)
    {
        var origin = CalculateTerrainOrigin(tileSize);
        PlayableArea = new Rect2(origin, new Vector2(MapWidth * tileSize, MapHeight * tileSize));

        if (Player is not null)
        {
            Player.PlayableArea = PlayableArea;
            UpdatePlayerCameraLimits(tileSize);
        }

        if (PlayerSpawn is not null)
        {
            PlayerSpawn.GlobalPosition = origin + new Vector2(PlayableArea.Size.X / 2f, PlayableArea.Size.Y / 2f);
        }
    }

    private void UpdatePlayerCameraLimits(int tileSize)
    {
        if (Player is null)
        {
            return;
        }

        var camera = Player.GetNodeOrNull<Camera2D>("Camera2D");
        if (camera is null)
        {
            return;
        }

        var origin = CalculateTerrainOrigin(tileSize);
        var max = origin + new Vector2(MapWidth * tileSize, MapHeight * tileSize);

        camera.LimitLeft = Mathf.RoundToInt(origin.X);
        camera.LimitTop = Mathf.RoundToInt(origin.Y);
        camera.LimitRight = Mathf.RoundToInt(max.X);
        camera.LimitBottom = Mathf.RoundToInt(max.Y);
    }

    private void PopulateFoliage()
    {
        if (FoliageContainer is null || _terrainGrid is null || _grassCells.Count == 0)
        {
            return;
        }

        foreach (var child in FoliageContainer.GetChildren())
        {
            child.QueueFree();
        }

        var texture = GetTreeTexture();
        if (texture is null)
        {
            return;
        }

        var origin = TerrainTileMap?.Position ?? Vector2.Zero;
        _rng.Randomize();

        var treeCount = Math.Max(TreeCount, 0);
        for (var index = 0; index < treeCount; index++)
        {
            var cell = _grassCells[_rng.RandiRange(0, _grassCells.Count - 1)];
            var position = CellToWorldPosition(cell, origin) + new Vector2(
                _rng.RandfRange(-TileSize * 0.35f, TileSize * 0.35f),
                _rng.RandfRange(-TileSize * 0.25f, TileSize * 0.35f));

            var sprite = new Sprite2D
            {
                Texture = texture,
                Position = position,
                Scale = new Vector2(0.18f, 0.24f),
                Modulate = new Color(
                    Mathf.Clamp(GrassColor.R - _rng.RandfRange(0f, 0.12f), 0f, 1f),
                    Mathf.Clamp(GrassColor.G + _rng.RandfRange(0.05f, 0.25f), 0f, 1f),
                    Mathf.Clamp(GrassColor.B - _rng.RandfRange(0f, 0.08f), 0f, 1f),
                    0.95f),
            };

            FoliageContainer.AddChild(sprite);
        }
    }

    private Texture2D? GetTreeTexture()
    {
        if (_treeTexture is not null)
        {
            return _treeTexture;
        }

        const string fallbackTexturePath = "res://icon.svg";
        if (!ResourceLoader.Exists(fallbackTexturePath))
        {
            GD.PushWarning("Texture per gli alberi non trovata, nessun fogliame generato.");
            return null;
        }

        _treeTexture = GD.Load<Texture2D>(fallbackTexturePath);
        return _treeTexture;
    }

    private void SpawnEnemyEncounters()
    {
        if (EnemyEncounterScene is null || _enemyContainer is null || _grassCells.Count == 0)
        {
            return;
        }

        foreach (var child in _enemyContainer.GetChildren())
        {
            child.QueueFree();
        }

        var encountersToCreate = Math.Clamp(EncounterCount, 1, 24);
        var attempts = 0;
        var spawned = 0;
        var attemptedLimit = encountersToCreate * 12;
        var usedCells = new HashSet<Vector2I>();

        _rng.Randomize();

        while (spawned < encountersToCreate && attempts < attemptedLimit)
        {
            attempts++;
            var cell = _grassCells[_rng.RandiRange(0, _grassCells.Count - 1)];

            if (!usedCells.Add(cell) || !IsValidEncounterCell(cell))
            {
                continue;
            }

            if (EnemyEncounterScene.Instantiate() is not EnemyEncounter encounter)
            {
                continue;
            }

            var group = CreateRandomEnemyGroup();
            var description = BuildEncounterDescription(group);

            encounter.Position = CellToWorldPosition(cell, TerrainTileMap?.Position ?? Vector2.Zero);
            encounter.ConfigureEncounter(group, description, TileSize * 2.5f);

            _enemyContainer.AddChild(encounter);
            spawned++;
        }
    }

    private bool IsValidEncounterCell(Vector2I cell)
    {
        var center = new Vector2I(MapWidth / 2, MapHeight / 2);
        return cell.DistanceTo(center) > 5f;
    }

    private Vector2 CellToWorldPosition(Vector2I cell, Vector2 origin)
    {
        return origin + new Vector2((cell.X + 0.5f) * TileSize, (cell.Y + 0.5f) * TileSize);
    }

    private IReadOnlyList<EnemyDefinition> CreateRandomEnemyGroup()
    {
        var count = _rng.RandiRange(1, 3);
        var group = new List<EnemyDefinition>(count);

        for (var index = 0; index < count; index++)
        {
            group.Add(CreateEnemyDefinition());
        }

        return group;
    }

    private EnemyDefinition CreateEnemyDefinition()
    {
        return _rng.RandiRange(0, 2) switch
        {
            0 => CreateBanditBrute(),
            1 => CreateBanditScout(),
            _ => CreateBanditSharpshooter(),
        };
    }

    private EnemyDefinition CreateBanditBrute()
    {
        return new EnemyDefinition
        {
            Name = $"Brigante bruto {_rng.RandiRange(100, 999)}",
            Background = "Un predone muscoloso coperto di cicatrici.",
            Strength = 12 + _rng.RandiRange(0, 2),
            Dexterity = 9 + _rng.RandiRange(-1, 1),
            Constitution = 12 + _rng.RandiRange(0, 2),
            Intelligence = 8,
            Wisdom = 8,
            Charisma = 7 + _rng.RandiRange(-1, 1),
            WeaponName = "Mazza chiodata",
            WeaponDescription = "Un pesante randello ricoperto di punte arrugginite.",
            WeaponMinDamage = 3,
            WeaponMaxDamage = 8,
            WeaponAccuracyBonus = 1,
            WeaponWeight = 5.5,
            WeaponValue = 18,
            ArmorName = "Corpetto di cuoio rinforzato",
            ArmorDescription = "Placche di cuoio e metallo recuperato offrono protezione discreta.",
            ArmorDefenseBonus = 2,
            ArmorWeight = 6.5,
            ArmorValue = 22,
        };
    }

    private EnemyDefinition CreateBanditScout()
    {
        return new EnemyDefinition
        {
            Name = $"Esploratore furtivo {_rng.RandiRange(100, 999)}",
            Background = "Un bandito agile pronto a colpire dalle ombre.",
            Strength = 9 + _rng.RandiRange(-1, 1),
            Dexterity = 12 + _rng.RandiRange(0, 2),
            Constitution = 9 + _rng.RandiRange(-1, 1),
            Intelligence = 9,
            Wisdom = 9 + _rng.RandiRange(-1, 1),
            Charisma = 8 + _rng.RandiRange(-1, 1),
            WeaponName = "Pugnale ricurvo",
            WeaponDescription = "Una lama leggera pensata per fendenti rapidi e precisi.",
            WeaponMinDamage = 2,
            WeaponMaxDamage = 5,
            WeaponAccuracyBonus = 2,
            WeaponWeight = 1.4,
            WeaponValue = 14,
            ArmorName = "Giaccone imbottito",
            ArmorDescription = "Strati di stoffa rinforzata che non intralciano i movimenti.",
            ArmorDefenseBonus = 1,
            ArmorWeight = 2.5,
            ArmorValue = 12,
        };
    }

    private EnemyDefinition CreateBanditSharpshooter()
    {
        return new EnemyDefinition
        {
            Name = $"Balestriere imboscato {_rng.RandiRange(100, 999)}",
            Background = "Un tiratore scelto appostato tra gli alberi.",
            Strength = 9 + _rng.RandiRange(-1, 1),
            Dexterity = 13 + _rng.RandiRange(0, 2),
            Constitution = 10 + _rng.RandiRange(-1, 1),
            Intelligence = 9,
            Wisdom = 9 + _rng.RandiRange(-1, 1),
            Charisma = 8,
            WeaponName = "Balestra tascabile",
            WeaponDescription = "Una balestra compatta con dardi preparati a mano.",
            WeaponMinDamage = 3,
            WeaponMaxDamage = 7,
            WeaponAccuracyBonus = 3,
            WeaponWeight = 3.2,
            WeaponValue = 24,
            ArmorName = "Gambesone consunto",
            ArmorDescription = "Un gambesone rattoppato che porta i segni di molte imboscate.",
            ArmorDefenseBonus = 1,
            ArmorWeight = 4.0,
            ArmorValue = 16,
        };
    }

    private static string BuildEncounterDescription(IReadOnlyCollection<EnemyDefinition> group)
    {
        if (group.Count == 0)
        {
            return "Un silenzio inquietante avvolge la radura...";
        }

        if (group.Count == 1)
        {
            return $"Ti imbatti in {group.First().Name.ToLowerInvariant()}!";
        }

        return $"Un gruppo di {group.Count} briganti balza fuori dalla boscaglia!";
    }

    private void AlignPlayerToSpawn()
    {
        if (Player is null)
        {
            return;
        }

        var targetPosition = PlayerSpawn?.GlobalPosition ?? Vector2.Zero;
        Player.GlobalPosition = targetPosition;
        Player.PlayableArea = PlayableArea;
    }

    private void UpdateLocationInfo()
    {
        if (LocationInfoLabel is null)
        {
            return;
        }

        var game = Game.Instance;
        if (game is null || game.WorldRegions.Count == 0 || game.CurrentRegion is null || game.CurrentLocation is null)
        {
            LocationInfoLabel.Text = "In attesa del caricamento del mondo...";
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Regione: {game.CurrentRegion.Name}");
        builder.AppendLine($"Bioma: {game.CurrentRegion.EnvironmentType}");
        builder.AppendLine($"Fazione dominante: {game.CurrentRegion.ControllingFaction}");
        builder.AppendLine($"Location attuale: {game.CurrentLocation.Name} ({game.CurrentLocation.Type})");
        builder.AppendLine($"Connessioni disponibili: {game.CurrentLocation.ConnectedLocations.Count}");

        LocationInfoLabel.Text = builder.ToString();
    }

    private void UpdateHelpLabel()
    {
        if (HelpLabel is null)
        {
            return;
        }

        HelpLabel.Text = "WASD o frecce per muoverti. Avvicinati alle sfere rosse per avviare un incontro.";
    }
}
