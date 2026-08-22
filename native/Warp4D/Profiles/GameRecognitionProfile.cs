using System.Text.Json;
using Warp4D.Emulation;

namespace Warp4D.Profiles;

internal sealed class GameRecognitionProfile
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public string Name { get; set; } = "Custom game profile";
    public string RomName { get; set; } = string.Empty;
    public string RomSha256 { get; set; } = string.Empty;
    public Dictionary<string, BackgroundObjectRule> BackgroundRules { get; set; } = [];

    public static GameRecognitionProfile Create(string romName, string romSha256) => new()
    {
        Name = $"{Path.GetFileNameWithoutExtension(romName)} objects",
        RomName = Path.GetFileName(romName),
        RomSha256 = romSha256
    };

    public BackgroundObjectRule? Match(MetatileSignature signature) =>
        BackgroundRules.TryGetValue(signature.Key, out BackgroundObjectRule? rule) ? rule : null;

    public GameRecognitionProfile Clone()
    {
        GameRecognitionProfile clone = new()
        {
            FormatVersion = FormatVersion,
            Name = Name,
            RomName = RomName,
            RomSha256 = RomSha256,
            BackgroundRules = (BackgroundRules ?? []).ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.Clone() ?? new BackgroundObjectRule(),
                StringComparer.OrdinalIgnoreCase)
        };
        clone.Normalize();
        return clone;
    }

    public void Normalize()
    {
        if (FormatVersion > CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"This game profile uses format {FormatVersion}, but this Warp4D build supports format {CurrentFormatVersion}.");
        }

        FormatVersion = CurrentFormatVersion;
        Name = string.IsNullOrWhiteSpace(Name) ? "Custom game profile" : Name.Trim();
        if (Name.Length > 64) Name = Name[..64];
        RomName = string.IsNullOrWhiteSpace(RomName) ? "Unknown ROM" : Path.GetFileName(RomName.Trim());
        RomSha256 = (RomSha256 ?? string.Empty).Trim().ToUpperInvariant();
        BackgroundRules ??= [];

        Dictionary<string, BackgroundObjectRule> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, BackgroundObjectRule? candidate) in BackgroundRules)
        {
            if (!MetatileSignature.IsValidKey(key))
            {
                continue;
            }
            BackgroundObjectRule rule = candidate ?? new BackgroundObjectRule();
            if (!Enum.TryParse(rule.Kind, ignoreCase: true, out SceneObjectKind kind))
            {
                kind = SceneObjectKind.Terrain;
            }
            rule.Kind = kind.ToString();
            rule.Label = string.IsNullOrWhiteSpace(rule.Label)
                ? ProjectionProfile.DisplayName(kind)
                : rule.Label.Trim();
            if (rule.Label.Length > 48) rule.Label = rule.Label[..48];
            normalized[key.ToUpperInvariant()] = rule;
        }
        BackgroundRules = normalized;
    }
}

internal sealed class BackgroundObjectRule
{
    public string Kind { get; set; } = SceneObjectKind.Terrain.ToString();
    public string Label { get; set; } = "Object";

    public SceneObjectKind ObjectKind =>
        Enum.TryParse(Kind, ignoreCase: true, out SceneObjectKind kind) ? kind : SceneObjectKind.Terrain;

    public BackgroundObjectRule Clone() => new()
    {
        Kind = Kind,
        Label = Label
    };
}

internal readonly record struct MetatileSignature(
    byte Palette,
    byte TopLeft,
    byte BottomLeft,
    byte TopRight,
    byte BottomRight)
{
    public string Key => $"P{Palette:X1}-{TopLeft:X2}{BottomLeft:X2}{TopRight:X2}{BottomRight:X2}";
    public string Description =>
        $"Palette {Palette} · tiles {TopLeft:X2} {TopRight:X2} / {BottomLeft:X2} {BottomRight:X2}";

    public static bool IsValidKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length != 11 ||
            char.ToUpperInvariant(key[0]) != 'P' || key[2] != '-')
        {
            return false;
        }
        return int.TryParse(key.AsSpan(1, 1), System.Globalization.NumberStyles.HexNumber, null, out _) &&
               uint.TryParse(key.AsSpan(3, 8), System.Globalization.NumberStyles.HexNumber, null, out _);
    }

    public static MetatileSignature Read(NesFrame frame, int worldTileX, int worldTileY)
    {
        (byte topLeft, byte palette) = ReadTile(frame, worldTileX, worldTileY);
        (byte bottomLeft, _) = ReadTile(frame, worldTileX, worldTileY + 1);
        (byte topRight, _) = ReadTile(frame, worldTileX + 1, worldTileY);
        (byte bottomRight, _) = ReadTile(frame, worldTileX + 1, worldTileY + 1);
        return new MetatileSignature(palette, topLeft, bottomLeft, topRight, bottomRight);
    }

    private static (byte Tile, byte Palette) ReadTile(NesFrame frame, int worldTileX, int worldTileY)
    {
        int wrappedX = Mod(worldTileX, 64);
        int wrappedY = Mod(worldTileY, 60);
        int table = (wrappedX >= 32 ? 1 : 0) + (wrappedY >= 30 ? 2 : 0);
        int tileIndex = (wrappedY % 30) * 32 + wrappedX % 32;
        byte attribute = frame.Attributes[table][tileIndex];
        int shift = ((wrappedY & 0x02) << 1) | (wrappedX & 0x02);
        return (frame.Tiles[table][tileIndex], (byte)((attribute >> shift) & 0x03));
    }

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
}

internal static class GameRecognitionProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Warp4D",
        "game-profiles");

    public static GameRecognitionProfile? LoadForRom(string romSha256)
    {
        try
        {
            string path = PathForRom(romSha256);
            return File.Exists(path) ? ReadFromFile(path) : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(GameRecognitionProfile profile)
    {
        profile.Normalize();
        if (string.IsNullOrWhiteSpace(profile.RomSha256))
        {
            throw new InvalidDataException("The game profile is not bound to a ROM hash.");
        }
        WriteToFile(PathForRom(profile.RomSha256), profile);
    }

    public static GameRecognitionProfile ReadFromFile(string path)
    {
        string json = File.ReadAllText(Path.GetFullPath(path));
        GameRecognitionProfile profile = JsonSerializer.Deserialize<GameRecognitionProfile>(json, JsonOptions)
            ?? throw new InvalidDataException("The selected file does not contain a Warp4D game profile.");
        profile.Normalize();
        return profile;
    }

    public static void WriteToFile(string path, GameRecognitionProfile profile)
    {
        GameRecognitionProfile normalized = profile.Clone();
        string absolutePath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(absolutePath, JsonSerializer.Serialize(normalized, JsonOptions));
    }

    private static string PathForRom(string romSha256)
    {
        string safeHash = new((romSha256 ?? string.Empty)
            .Where(character => Uri.IsHexDigit(character))
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (safeHash.Length != 64)
        {
            throw new InvalidDataException("The ROM SHA-256 is invalid.");
        }
        return Path.Combine(DirectoryPath, safeHash + ".warp4d-game.json");
    }
}
