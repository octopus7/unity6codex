using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopdownShooter.Server.WorldMap;

public static class WorldMapLoader
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static bool TryLoad(
        string mapFilePath,
        string baseDirectory,
        out WorldMapConfig mapConfig,
        out List<string> errors)
    {
        errors = [];
        mapConfig = WorldMapDefaults.Create();

        if (string.IsNullOrWhiteSpace(mapFilePath))
        {
            errors.Add("Map file path is empty.");
            return false;
        }

        var resolvedPath = ResolvePath(mapFilePath, baseDirectory);
        if (!File.Exists(resolvedPath))
        {
            errors.Add($"Map file was not found: {resolvedPath}");
            return false;
        }

        WorldMapConfig? loaded;
        try
        {
            var json = File.ReadAllText(resolvedPath);
            loaded = JsonSerializer.Deserialize<WorldMapConfig>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            errors.Add($"Failed to parse map file '{resolvedPath}': {exception.Message}");
            return false;
        }

        if (loaded is null)
        {
            errors.Add($"Map file '{resolvedPath}' is empty or invalid.");
            return false;
        }

        if (!WorldMapValidator.TryValidate(loaded, out var validationErrors))
        {
            errors.Add($"Map validation failed for '{resolvedPath}':");
            foreach (var validationError in validationErrors)
            {
                errors.Add("- " + validationError);
            }

            return false;
        }

        mapConfig = loaded;
        return true;
    }

    private static string ResolvePath(string mapFilePath, string baseDirectory)
    {
        if (Path.IsPathRooted(mapFilePath))
        {
            return mapFilePath;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, mapFilePath));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
