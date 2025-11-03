using System.Collections.Generic;
using DynamicRPG.World;
using Godot;

#nullable enable

namespace DynamicRPG.Gameplay.Map;

/// <summary>
/// Builds a minimalist tile map describing the dominant biome for each generated region.
/// </summary>
public sealed class BiomeMapBuilder : IWorldMapBuilder
{
    /// <inheritdoc />
    public TileMapLayer BuildBiomeMap(IReadOnlyList<Region> regions)
    {
        var tileMapLayer = new TileMapLayer
        {
            Name = "BiomeMap",
        };

        var tileSet = new TileSet
        {
            TileSize = new Vector2I(32, 32),
        };

        tileMapLayer.TileSet = tileSet;

        if (regions.Count == 0)
        {
            return tileMapLayer;
        }

        var biomeTileSources = new Dictionary<Color, (int sourceId, TileSetAtlasSource source)>();

        var regionCount = regions.Count;
        var columns = Mathf.CeilToInt(Mathf.Sqrt(regionCount));

        for (var index = 0; index < regionCount; index++)
        {
            var region = regions[index];
            var biomeColor = GetBiomeColor(region.EnvironmentType);

            if (!biomeTileSources.TryGetValue(biomeColor, out var atlasData))
            {
                atlasData = CreateAtlasEntry(tileSet, biomeColor);
                biomeTileSources.Add(biomeColor, atlasData);
            }

            var column = index % columns;
            var row = index / columns;
            var cellPosition = new Vector2I(column, row);

            tileMapLayer.SetCell(cellPosition, atlasData.sourceId, Vector2I.Zero);
        }

        tileMapLayer.TileSet = tileSet;
        return tileMapLayer;
    }

    private static (int sourceId, TileSetAtlasSource source) CreateAtlasEntry(TileSet tileSet, Color biomeColor)
    {
        var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        image.Fill(biomeColor);
        var texture = ImageTexture.CreateFromImage(image);

        var atlasSource = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = new Vector2I(32, 32),
        };

        var sourceId = tileSet.AddSource(atlasSource);
        atlasSource.CreateTile(Vector2I.Zero);

        return (sourceId, atlasSource);
    }

    private static Color GetBiomeColor(string environmentType)
    {
        var lowered = environmentType.ToLowerInvariant();

        if (lowered.Contains("tundra") || lowered.Contains("ghiacci"))
        {
            return new Color(0.65f, 0.85f, 1.0f);
        }

        if (lowered.Contains("foresta") || lowered.Contains("bosco"))
        {
            return new Color(0.2f, 0.6f, 0.2f);
        }

        if (lowered.Contains("deserto") || lowered.Contains("dune"))
        {
            return new Color(0.9f, 0.8f, 0.55f);
        }

        if (lowered.Contains("palud") || lowered.Contains("bruma"))
        {
            return new Color(0.35f, 0.45f, 0.25f);
        }

        if (lowered.Contains("rovine") || lowered.Contains("citt"))
        {
            return new Color(0.5f, 0.5f, 0.55f);
        }

        return new Color(0.6f, 0.6f, 0.6f);
    }
}
