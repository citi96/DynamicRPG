namespace DynamicRPG.World.Generation;

using System;
using System.Collections.Generic;
using System.Globalization;
using DynamicRPG.World.Locations;
using DynamicRPG.World;

#nullable enable

/// <summary>
/// Generates procedural location data for regions based on their environment type.
/// </summary>
public sealed class RegionLocationGenerator
{
    private readonly Dictionary<string, string[]> _nameSeeds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forest"] = new[] { "Elden", "Lumin", "Myrr", "Silva", "Verdan" },
        ["desert"] = new[] { "Sahim", "Qadir", "Arim", "Nadir", "Sor" },
        ["tundra"] = new[] { "Frostr", "Iskar", "Nordh", "Skadi", "Varyn" },
        ["swamp"] = new[] { "Mirel", "Bogrin", "Sul", "Namar", "Gloom" },
        ["ruin"] = new[] { "Arkan", "Velis", "Tor", "Dravos", "Menera" },
        ["generic"] = new[] { "Aur", "Tal", "Ser", "Vele", "Nor" },
    };

    private readonly Random _random;

    public RegionLocationGenerator(Random? random = null)
    {
        _random = random ?? new Random();
    }

    /// <summary>
    /// Populates the provided region with procedural locations derived from its environment type.
    /// </summary>
    public void GenerateForRegion(Region region)
    {
        region.Locations.Clear();

        var (profile, key) = ResolveProfile(region.EnvironmentType);

        CreateSettlements(region, profile, key);
        CreateDungeons(region, profile, key);
        CreateRuins(region, profile, key);
        CreateLandmarks(region, profile, key);
        CreateResources(region, profile, key);
    }

    private static (LocationGenerationProfile profile, string key) ResolveProfile(string environmentType)
    {
        var lowered = environmentType.ToLowerInvariant();

        if (lowered.Contains("foresta"))
        {
            return (LocationGenerationProfile.Forest, "forest");
        }

        if (lowered.Contains("deserto") || lowered.Contains("dune"))
        {
            return (LocationGenerationProfile.Desert, "desert");
        }

        if (lowered.Contains("tundra") || lowered.Contains("mont"))
        {
            return (LocationGenerationProfile.Tundra, "tundra");
        }

        if (lowered.Contains("palud") || lowered.Contains("bruma"))
        {
            return (LocationGenerationProfile.Swamp, "swamp");
        }

        if (lowered.Contains("rovine") || lowered.Contains("citt"))
        {
            return (LocationGenerationProfile.Ruinscape, "ruin");
        }

        return (LocationGenerationProfile.Generic, "generic");
    }

    private void CreateSettlements(Region region, LocationGenerationProfile profile, string key)
    {
        for (var i = 0; i < profile.MajorSettlements; i++)
        {
            var location = new Location
            {
                Name = $"Città di {GenerateName(key)}",
                Type = LocationType.City,
                Region = region,
            };

            region.Locations.Add(location);
        }

        var minorCount = NextInRange(profile.MinorSettlementRange);
        for (var i = 0; i < minorCount; i++)
        {
            var location = new Location
            {
                Name = $"Villaggio di {GenerateName(key)}",
                Type = LocationType.Village,
                Region = region,
            };

            region.Locations.Add(location);
        }

        for (var i = 0; i < profile.Outposts; i++)
        {
            var location = new Location
            {
                Name = $"Avamposto {GenerateName(key)}",
                Type = LocationType.Outpost,
                Region = region,
            };

            region.Locations.Add(location);
        }
    }

    private void CreateDungeons(Region region, LocationGenerationProfile profile, string key)
    {
        var dungeonCount = NextInRange(profile.DungeonRange);
        for (var i = 0; i < dungeonCount; i++)
        {
            var dungeonName = profile.DungeonPrefix switch
            {
                "Caverna" => $"Caverna di {GenerateName(key)}",
                "Cripta" => $"Cripta di {GenerateName(key)}",
                "Tempio" => $"Tempio Perduto di {GenerateName(key)}",
                _ => $"Dungeon di {GenerateName(key)}",
            };

            var location = new Location
            {
                Name = dungeonName,
                Type = LocationType.Dungeon,
                Region = region,
            };

            region.Locations.Add(location);
        }
    }

    private void CreateRuins(Region region, LocationGenerationProfile profile, string key)
    {
        var ruinCount = NextInRange(profile.RuinRange);
        for (var i = 0; i < ruinCount; i++)
        {
            var location = new Location
            {
                Name = $"Rovina di {GenerateName(key)}",
                Type = LocationType.Ruin,
                Region = region,
            };

            region.Locations.Add(location);
        }
    }

    private void CreateLandmarks(Region region, LocationGenerationProfile profile, string key)
    {
        var landmarkCount = NextInRange(profile.LandmarkRange);
        for (var i = 0; i < landmarkCount; i++)
        {
            var descriptor = profile.LandmarkDescriptors.Length > 0
                ? profile.LandmarkDescriptors[_random.Next(profile.LandmarkDescriptors.Length)]
                : "Monumento";

            var location = new Location
            {
                Name = $"{descriptor} {GenerateName(key)}",
                Type = LocationType.Landmark,
                Region = region,
            };

            region.Locations.Add(location);
        }
    }

    private void CreateResources(Region region, LocationGenerationProfile profile, string key)
    {
        var resourceCount = NextInRange(profile.ResourceRange);
        for (var i = 0; i < resourceCount; i++)
        {
            var descriptor = profile.ResourceDescriptors.Length > 0
                ? profile.ResourceDescriptors[_random.Next(profile.ResourceDescriptors.Length)]
                : "Deposito";

            var location = new Location
            {
                Name = $"{descriptor} di {GenerateName(key)}",
                Type = LocationType.Resource,
                Region = region,
            };

            region.Locations.Add(location);
        }
    }

    private int NextInRange((int min, int max) range)
    {
        var (min, max) = range;
        if (max < min)
        {
            (min, max) = (max, min);
        }

        return max == min ? min : _random.Next(min, max + 1);
    }

    private string GenerateName(string key)
    {
        if (!_nameSeeds.TryGetValue(key, out var seeds))
        {
            seeds = _nameSeeds["generic"];
        }

        var seed = seeds[_random.Next(seeds.Length)];
        var suffix = _random.Next(0, 3) switch
        {
            0 => "",
            1 => "a",
            2 => "ia",
            _ => "",
        };

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase((seed + suffix).ToLowerInvariant());
    }
}
