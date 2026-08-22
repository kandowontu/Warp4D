using System.Drawing;
using System.Drawing.Imaging;
using Warp4D.Emulation;
using Warp4D.Rendering;

namespace Warp4D.Profiles;

internal sealed class SmbProfile
{
    private const int OperModeAddress = 0x0770;
    private const int OperModeTaskAddress = 0x0772;
    private const int DemoTimerAddress = 0x07A2;
    private static readonly ProjectionProfile DefaultProjectionProfile = ProjectionProfile.CreateDefault();
    private static readonly HashSet<int> BushMetatiles =
    [
        Pack(0x24, 0x24, 0x24, 0x35), Pack(0x36, 0x25, 0x37, 0x25), Pack(0x24, 0x38, 0x24, 0x24)
    ];
    private static readonly HashSet<int> HillMetatiles =
    [
        Pack(0x24, 0x30, 0x30, 0x26), Pack(0x26, 0x26, 0x34, 0x26),
        Pack(0x24, 0x31, 0x24, 0x32), Pack(0x33, 0x26, 0x24, 0x33),
        Pack(0x34, 0x26, 0x26, 0x26), Pack(0x26, 0x26, 0x26, 0x26)
    ];
    private static readonly HashSet<int> TreeMetatiles =
    [
        Pack(0xB8, 0xBA, 0xB9, 0xBB), Pack(0xB8, 0xBC, 0xB9, 0xBD),
        Pack(0xBA, 0xBC, 0xBB, 0xBD), Pack(0x4B, 0x4C, 0x4D, 0x4E),
        Pack(0x4D, 0x4F, 0x4D, 0x4F), Pack(0x4D, 0x4E, 0x50, 0x51)
    ];
    private static readonly HashSet<int> PipeMetatiles =
    [
        Pack(0x60, 0x64, 0x61, 0x65), Pack(0x62, 0x66, 0x63, 0x67),
        Pack(0x68, 0x68, 0x69, 0x69), Pack(0x26, 0x26, 0x6A, 0x6A),
        Pack(0x86, 0x8A, 0x87, 0x8B), Pack(0x88, 0x8C, 0x88, 0x8C),
        Pack(0x89, 0x8D, 0x69, 0x69), Pack(0x8E, 0x91, 0x8F, 0x92),
        Pack(0x26, 0x93, 0x26, 0x93), Pack(0x90, 0x94, 0x69, 0x69)
    ];
    private static readonly HashSet<int> CloudMetatiles =
    [
        Pack(0x24, 0x24, 0x24, 0x35), Pack(0x36, 0x25, 0x37, 0x25),
        Pack(0x24, 0x38, 0x24, 0x24), Pack(0x24, 0x24, 0x39, 0x24),
        Pack(0x3A, 0x24, 0x3B, 0x24), Pack(0x3C, 0x24, 0x24, 0x24)
    ];
    private static readonly HashSet<int> BrickMetatiles =
    [
        Pack(0x45, 0x47, 0x45, 0x47), Pack(0x47, 0x47, 0x47, 0x47)
    ];
    private static readonly HashSet<int> CastleMetatiles =
    [
        Pack(0x9D, 0x47, 0x9E, 0x47), Pack(0x47, 0x47, 0x27, 0x27),
        Pack(0x47, 0x47, 0x47, 0x47), Pack(0x27, 0x27, 0x47, 0x47),
        Pack(0xA9, 0x47, 0xAA, 0x47), Pack(0x9B, 0x27, 0x9C, 0x27),
        Pack(0x27, 0x27, 0x27, 0x27)
    ];
    private static readonly HashSet<int> FlagpoleMetatiles =
    [
        Pack(0x24, 0x2F, 0x24, 0x3D), Pack(0xA2, 0xA2, 0xA3, 0xA3)
    ];
    private static readonly HashSet<int> QuestionMetatiles =
    [
        Pack(0x53, 0x55, 0x54, 0x56), Pack(0x57, 0x59, 0x58, 0x5A)
    ];

    private static readonly Dictionary<SceneObjectKind, Color> Accents = new()
    {
        [SceneObjectKind.Bush] = Color.FromArgb(128, 255, 100),
        [SceneObjectKind.Cloud] = Color.FromArgb(210, 235, 255),
        [SceneObjectKind.Hill] = Color.FromArgb(88, 226, 121),
        [SceneObjectKind.Tree] = Color.FromArgb(81, 213, 128),
        [SceneObjectKind.Pipe] = Color.FromArgb(90, 255, 118),
        [SceneObjectKind.QuestionBlock] = Color.FromArgb(255, 208, 56),
        [SceneObjectKind.Brick] = Color.FromArgb(255, 118, 52),
        [SceneObjectKind.Terrain] = Color.FromArgb(214, 98, 45),
        [SceneObjectKind.Castle] = Color.FromArgb(220, 140, 88),
        [SceneObjectKind.Flagpole] = Color.FromArgb(240, 244, 207),
        [SceneObjectKind.Player] = Color.FromArgb(255, 245, 127),
        [SceneObjectKind.Enemy] = Color.FromArgb(255, 91, 97),
        [SceneObjectKind.Item] = Color.FromArgb(105, 226, 255),
        [SceneObjectKind.Sprite] = Color.FromArgb(198, 147, 255)
    };

    private static readonly Dictionary<int, string> EnemyNames = new()
    {
        [0x00] = "Green Koopa", [0x02] = "Buzzy Beetle", [0x03] = "Red Koopa",
        [0x05] = "Hammer Bro", [0x06] = "Goomba", [0x07] = "Bloober",
        [0x08] = "Bullet Bill", [0x0A] = "Cheep-Cheep", [0x0B] = "Red Cheep-Cheep",
        [0x0C] = "Podoboo", [0x0D] = "Piranha Plant", [0x0E] = "Paratroopa",
        [0x0F] = "Red Paratroopa", [0x10] = "Flying Paratroopa", [0x11] = "Lakitu",
        [0x12] = "Spiny", [0x14] = "Flying Cheep-Cheep", [0x15] = "Bowser Flame",
        [0x16] = "Firework", [0x17] = "Bullet/Cheep Frenzy", [0x2D] = "Bowser",
        [0x2E] = "Power-up", [0x2F] = "Vine", [0x30] = "Flag",
        [0x31] = "Star Flag", [0x32] = "Spring"
    };

    public SmbScene Build(
        NesFrame frame,
        bool exactProfile,
        ProjectionProfile? projectionProfile = null,
        GameRecognitionProfile? gameProfile = null)
    {
        projectionProfile ??= DefaultProjectionProfile;
        Bitmap background = ComposeBackground(frame, exactProfile);
        List<SceneObject> objects = [];

        bool normalGameplay = ReadRam(frame, OperModeAddress) == 1;
        bool attractDemo = exactProfile && IsAttractDemo(frame);
        bool hasCustomBackgroundRules = gameProfile?.BackgroundRules.Count > 0;
        if ((exactProfile && (normalGameplay || attractDemo)) || (!exactProfile && hasCustomBackgroundRules))
        {
            objects.AddRange(ExtractBackgroundObjects(
                background,
                frame,
                projectionProfile,
                exactProfile,
                gameProfile));
        }
        objects.AddRange(ExtractSprites(frame, exactProfile, projectionProfile));

        int world = ReadRam(frame, 0x075F) + 1;
        int level = ReadRam(frame, 0x075C) + 1;
        string location = exactProfile
            ? $"SMB  {world}-{level}{(attractDemo ? "  ·  ATTRACT DEMO" : string.Empty)}"
            : gameProfile?.Name ?? "Generic NES";
        string recognitionProfileName = exactProfile
            ? gameProfile is null ? "SMB WORLD" : $"SMB WORLD + {gameProfile.Name}"
            : gameProfile?.Name ?? "GENERIC SPRITES";

        return new SmbScene
        {
            Background = background,
            Objects = objects,
            Location = location,
            ExactProfile = exactProfile,
            RecognitionProfileName = recognitionProfileName,
            ProjectionProfileName = projectionProfile.Name,
            Sequence = frame.Sequence
        };
    }

    internal static bool IsAttractDemo(NesFrame frame) =>
        ReadRam(frame, OperModeAddress) == 0 &&
        ReadRam(frame, OperModeTaskAddress) == 3 &&
        ReadRam(frame, DemoTimerAddress) == 0;

    internal static Bitmap ComposeBackground(NesFrame frame, bool exactProfile)
    {
        Bitmap bitmap = new(NesFrame.ScreenWidth, NesFrame.ScreenHeight, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        unsafe
        {
            int* target = (int*)data.Scan0;
            for (int y = 0; y < NesFrame.ScreenHeight; y++)
            {
                bool smbStatusBar = exactProfile && y < 32;
                int worldY = smbStatusBar ? y : Mod(y + frame.ScrollY, 480);
                int tableY = worldY >= 240 ? 2 : 0;
                int localY = worldY % 240;
                for (int x = 0; x < NesFrame.ScreenWidth; x++)
                {
                    int worldX = smbStatusBar ? x : Mod(x + frame.ScrollX, 512);
                    int table = tableY + (worldX >= 256 ? 1 : 0);
                    int localX = worldX & 0xFF;
                    int color = frame.NametablePixels[table][localY * 256 + localX];
                    target[y * (data.Stride / 4) + x] = color | unchecked((int)0xFF000000);
                }
            }
        }

        bitmap.UnlockBits(data);
        return bitmap;
    }

    private static IEnumerable<SceneObject> ExtractBackgroundObjects(
        Bitmap background,
        NesFrame frame,
        ProjectionProfile projectionProfile,
        bool exactProfile,
        GameRecognitionProfile? gameProfile)
    {
        Dictionary<(int X, int Y), BackgroundObjectIdentity> classified = [];
        int backdropRgb = FindDominantRgb(background);

        int startTileX = (frame.ScrollX / 8) - 1;
        int startTileY = (frame.ScrollY / 8) - 1;
        int endTileX = ((frame.ScrollX + 255) / 8) + 1;
        int endTileY = ((frame.ScrollY + 239) / 8) + 1;

        int firstMetatileX = (startTileX & ~1) - 2;
        int firstMetatileY = (startTileY & ~1) - 2;
        for (int worldTileY = firstMetatileY; worldTileY <= endTileY; worldTileY += 2)
        {
            for (int worldTileX = firstMetatileX; worldTileX <= endTileX; worldTileX += 2)
            {
                MetatileSignature signature = MetatileSignature.Read(frame, worldTileX, worldTileY);
                BackgroundObjectIdentity? identity = ClassifyMetatile(signature, exactProfile, gameProfile);
                if (identity is not null)
                {
                    classified[(worldTileX, worldTileY)] = identity.Value;
                    classified[(worldTileX, worldTileY + 1)] = identity.Value;
                    classified[(worldTileX + 1, worldTileY)] = identity.Value;
                    classified[(worldTileX + 1, worldTileY + 1)] = identity.Value;
                }
            }
        }

        foreach (List<(int X, int Y)> group in GroupTiles(classified))
        {
            BackgroundObjectIdentity identity = classified[group[0]];
            SceneObjectKind kind = identity.Kind;
            int left = group.Min(point => point.X * 8 - frame.ScrollX);
            int top = group.Min(point => point.Y * 8 - frame.ScrollY);
            int right = group.Max(point => point.X * 8 - frame.ScrollX + 8);
            int bottom = group.Max(point => point.Y * 8 - frame.ScrollY + 8);
            Rectangle bounds = Rectangle.Intersect(
                new Rectangle(left, top, right - left, bottom - top),
                new Rectangle(0, 0, 256, 240));

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                continue;
            }

            Bitmap crop = CropWithTileMask(background, bounds, group, frame.ScrollX, frame.ScrollY);
            if (kind is SceneObjectKind.Bush or SceneObjectKind.Cloud or SceneObjectKind.Hill or
                SceneObjectKind.Tree or SceneObjectKind.Pipe or SceneObjectKind.Flagpole)
            {
                MakeBackdropTransparent(crop, backdropRgb);
            }
            EraseObjectFromBackground(background, crop, bounds, backdropRgb);
            ObjectProjectionRule rule = projectionProfile.RuleFor(kind);
            yield return new SceneObject
            {
                Kind = kind,
                Label = identity.Label,
                Bounds = bounds,
                Image = crop,
                Accent = Accents[kind],
                Depth = DepthFor(kind, bounds) * rule.DepthScale,
                ProjectionEnabled = rule.Enabled,
                SortOrder = kind is SceneObjectKind.Terrain ? -5 : 0
            };
        }
    }

    private static BackgroundObjectIdentity? ClassifyMetatile(
        MetatileSignature signature,
        bool exactProfile,
        GameRecognitionProfile? gameProfile)
    {
        BackgroundObjectRule? customRule = gameProfile?.Match(signature);
        if (customRule is not null)
        {
            return new BackgroundObjectIdentity(customRule.ObjectKind, customRule.Label);
        }

        if (!exactProfile)
        {
            return null;
        }

        int metatile = Pack(signature.TopLeft, signature.BottomLeft, signature.TopRight, signature.BottomRight);
        byte palette = signature.Palette;
        SceneObjectKind? kind = null;
        if (palette == 0 && BushMetatiles.Contains(metatile)) kind = SceneObjectKind.Bush;
        else if (palette == 0 && HillMetatiles.Contains(metatile)) kind = SceneObjectKind.Hill;
        else if (palette == 0 && TreeMetatiles.Contains(metatile)) kind = SceneObjectKind.Tree;
        else if (palette == 0 && PipeMetatiles.Contains(metatile)) kind = SceneObjectKind.Pipe;
        else if (palette == 0 && FlagpoleMetatiles.Contains(metatile)) kind = SceneObjectKind.Flagpole;
        else if (palette == 2 && CloudMetatiles.Contains(metatile)) kind = SceneObjectKind.Cloud;
        else if (palette == 1 && CastleMetatiles.Contains(metatile)) kind = SceneObjectKind.Castle;
        else if (palette == 1 && BrickMetatiles.Contains(metatile)) kind = SceneObjectKind.Brick;
        else if (palette == 3 && QuestionMetatiles.Contains(metatile)) kind = SceneObjectKind.QuestionBlock;
        return kind is null ? null : new BackgroundObjectIdentity(kind.Value, KindLabel(kind.Value));
    }

    private static IEnumerable<List<(int X, int Y)>> GroupTiles(
        Dictionary<(int X, int Y), BackgroundObjectIdentity> classified)
    {
        HashSet<(int X, int Y)> remaining = [.. classified.Keys];
        while (remaining.Count > 0)
        {
            (int X, int Y) seed = remaining.First();
            BackgroundObjectIdentity identity = classified[seed];
            Queue<(int X, int Y)> queue = new();
            List<(int X, int Y)> group = [];
            queue.Enqueue(seed);
            remaining.Remove(seed);

            while (queue.Count > 0)
            {
                (int X, int Y) point = queue.Dequeue();
                group.Add(point);
                foreach ((int X, int Y) neighbor in new[]
                {
                    (point.X - 1, point.Y), (point.X + 1, point.Y),
                    (point.X, point.Y - 1), (point.X, point.Y + 1)
                })
                {
                    if (remaining.Contains(neighbor) && classified[neighbor] == identity)
                    {
                        remaining.Remove(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            yield return group;
        }
    }

    private static unsafe Bitmap CropWithTileMask(
        Bitmap source,
        Rectangle bounds,
        List<(int X, int Y)> tiles,
        int scrollX,
        int scrollY)
    {
        Bitmap crop = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (Graphics clear = Graphics.FromImage(crop))
        {
            clear.Clear(Color.Transparent);
        }

        BitmapData sourceData = source.LockBits(
            new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        BitmapData targetData = crop.LockBits(
            new Rectangle(0, 0, crop.Width, crop.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            foreach ((int tileX, int tileY) in tiles)
            {
                Rectangle sourceCell = Rectangle.Intersect(
                    new Rectangle(tileX * 8 - scrollX, tileY * 8 - scrollY, 8, 8),
                    bounds);
                if (sourceCell.Width <= 0 || sourceCell.Height <= 0)
                {
                    continue;
                }
                for (int row = 0; row < sourceCell.Height; row++)
                {
                    int* sourcePixels = (int*)((byte*)sourceData.Scan0 + (sourceCell.Y + row) * sourceData.Stride) + sourceCell.X;
                    int* targetPixels = (int*)((byte*)targetData.Scan0 + (sourceCell.Y - bounds.Y + row) * targetData.Stride) + sourceCell.X - bounds.X;
                    Buffer.MemoryCopy(sourcePixels, targetPixels, sourceCell.Width * sizeof(int), sourceCell.Width * sizeof(int));
                }
            }
        }
        finally
        {
            source.UnlockBits(sourceData);
            crop.UnlockBits(targetData);
        }
        return crop;
    }

    private static void MakeBackdropTransparent(Bitmap bitmap, int backdropRgb)
    {
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            Color color = bitmap.GetPixel(x, y);
            if ((color.ToArgb() & 0x00FFFFFF) == backdropRgb)
            {
                bitmap.SetPixel(x, y, Color.Transparent);
            }
        }
    }

    private static unsafe int FindDominantRgb(Bitmap bitmap)
    {
        Dictionary<int, int> colors = [];
        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 32; y < bitmap.Height; y++)
            {
                int* row = (int*)((byte*)data.Scan0 + y * data.Stride);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int rgb = row[x] & 0x00FFFFFF;
                    colors[rgb] = colors.GetValueOrDefault(rgb) + 1;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return colors.Count == 0 ? 0 : colors.MaxBy(pair => pair.Value).Key;
    }

    private static unsafe void EraseObjectFromBackground(
        Bitmap background,
        Bitmap objectImage,
        Rectangle bounds,
        int backdropRgb)
    {
        BitmapData backgroundData = background.LockBits(
            new Rectangle(0, 0, background.Width, background.Height),
            ImageLockMode.ReadWrite,
            PixelFormat.Format32bppArgb);
        BitmapData objectData = objectImage.LockBits(
            new Rectangle(0, 0, objectImage.Width, objectImage.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            int replacement = unchecked((int)0xFF000000) | backdropRgb;
            for (int y = 0; y < objectImage.Height; y++)
            {
                int* objectRow = (int*)((byte*)objectData.Scan0 + y * objectData.Stride);
                int* backgroundRow = (int*)((byte*)backgroundData.Scan0 + (bounds.Y + y) * backgroundData.Stride) + bounds.X;
                for (int x = 0; x < objectImage.Width; x++)
                {
                    if ((objectRow[x] & unchecked((int)0xFF000000)) != 0)
                    {
                        backgroundRow[x] = replacement;
                    }
                }
            }
        }
        finally
        {
            objectImage.UnlockBits(objectData);
            background.UnlockBits(backgroundData);
        }
    }

    private static IEnumerable<SceneObject> ExtractSprites(
        NesFrame frame,
        bool exactProfile,
        ProjectionProfile projectionProfile)
    {
        List<SpriteTile> tiles = [];
        for (int index = 0; index < 64; index++)
        {
            int offset = index * 4;
            int y = frame.Oam[offset] + 1;
            int x = frame.Oam[offset + 3];
            if (y >= 240 || x >= 256)
            {
                continue;
            }

            Bitmap tile = DecodeSpriteTile(
                frame.Chr,
                frame.Palette,
                frame.Oam[offset + 1],
                frame.Oam[offset + 2]);
            if (HasVisiblePixel(tile))
            {
                tiles.Add(new SpriteTile(index, new Rectangle(x, y, 8, 8), tile));
            }
            else
            {
                tile.Dispose();
            }
        }

        foreach (List<SpriteTile> cluster in ClusterSpriteTiles(tiles))
        {
            Rectangle bounds = cluster.Select(item => item.Bounds).Aggregate(Rectangle.Union);
            Bitmap image = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.Transparent);
                foreach (SpriteTile tile in cluster.OrderByDescending(item => item.OamIndex))
                {
                    graphics.DrawImageUnscaled(tile.Image, tile.Bounds.X - bounds.X, tile.Bounds.Y - bounds.Y);
                }
            }

            foreach (SpriteTile tile in cluster)
            {
                tile.Image.Dispose();
            }

            (SceneObjectKind kind, string label) = IdentifySprite(frame, bounds, exactProfile);
            ObjectProjectionRule rule = projectionProfile.RuleFor(kind);
            yield return new SceneObject
            {
                Kind = kind,
                Label = label,
                Bounds = bounds,
                Image = image,
                Accent = Accents[kind],
                Depth = (kind == SceneObjectKind.Player ? 1.25f : 1.05f) * rule.DepthScale,
                ProjectionEnabled = rule.Enabled,
                SortOrder = 20
            };
        }
    }

    private static Bitmap DecodeSpriteTile(byte[] chr, byte[] palette, byte tileIndex, byte attributes)
    {
        Bitmap bitmap = new(8, 8, PixelFormat.Format32bppArgb);
        if (chr.Length < 4096 || palette.Length < 32)
        {
            return bitmap;
        }

        bool flipX = (attributes & 0x40) != 0;
        bool flipY = (attributes & 0x80) != 0;
        int paletteOffset = 0x10 + (attributes & 0x03) * 4;
        int tileOffset = tileIndex * 16; // SMB uses the $0000 sprite pattern table.

        for (int outputY = 0; outputY < 8; outputY++)
        {
            int sourceY = flipY ? 7 - outputY : outputY;
            byte low = chr[tileOffset + sourceY];
            byte high = chr[tileOffset + sourceY + 8];
            for (int outputX = 0; outputX < 8; outputX++)
            {
                int sourceX = flipX ? 7 - outputX : outputX;
                int bit = 7 - sourceX;
                int color = ((low >> bit) & 1) | (((high >> bit) & 1) << 1);
                if (color == 0)
                {
                    continue;
                }
                bitmap.SetPixel(outputX, outputY, NesPalette.Get(palette[paletteOffset + color]));
            }
        }
        return bitmap;
    }

    private static IEnumerable<List<SpriteTile>> ClusterSpriteTiles(List<SpriteTile> source)
    {
        HashSet<SpriteTile> remaining = [.. source];
        while (remaining.Count > 0)
        {
            SpriteTile seed = remaining.First();
            remaining.Remove(seed);
            Queue<SpriteTile> queue = new();
            List<SpriteTile> cluster = [];
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                SpriteTile tile = queue.Dequeue();
                cluster.Add(tile);
                Rectangle reach = Rectangle.Inflate(tile.Bounds, 3, 3);
                foreach (SpriteTile neighbor in remaining.Where(candidate => reach.IntersectsWith(candidate.Bounds)).ToArray())
                {
                    remaining.Remove(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
            yield return cluster;
        }
    }

    private static (SceneObjectKind Kind, string Label) IdentifySprite(
        NesFrame frame,
        Rectangle bounds,
        bool exactProfile)
    {
        if (!exactProfile)
        {
            return (SceneObjectKind.Sprite, "Sprite");
        }

        int screenLeft = ReadRam(frame, 0x071A) * 256 + ReadRam(frame, 0x071C);
        int playerX = ReadRam(frame, 0x006D) * 256 + ReadRam(frame, 0x0086) - screenLeft;
        int playerY = ReadRam(frame, 0x00CE);
        if (Near(bounds, playerX, playerY, 22))
        {
            return (SceneObjectKind.Player, "Mario");
        }

        for (int slot = 0; slot < 6; slot++)
        {
            if (ReadRam(frame, 0x000F + slot) == 0)
            {
                continue;
            }
            int enemyX = ReadRam(frame, 0x006E + slot) * 256 + ReadRam(frame, 0x0087 + slot) - screenLeft;
            int enemyY = ReadRam(frame, 0x00CF + slot);
            if (!Near(bounds, enemyX, enemyY, 24))
            {
                continue;
            }

            int id = ReadRam(frame, 0x0016 + slot);
            string name = EnemyNames.GetValueOrDefault(id, $"Object {id:X2}");
            SceneObjectKind kind = id is >= 0x2E and <= 0x32 ? SceneObjectKind.Item : SceneObjectKind.Enemy;
            return (kind, name);
        }

        return (SceneObjectKind.Sprite, "Sprite effect");
    }

    private static bool Near(Rectangle bounds, int x, int y, int tolerance)
    {
        Rectangle target = Rectangle.Inflate(bounds, tolerance, tolerance);
        return target.Contains(x, y) || target.Contains(x + 8, y + 8);
    }

    private static bool HasVisiblePixel(Bitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, y).A != 0) return true;
        }
        return false;
    }

    private static float DepthFor(SceneObjectKind kind, Rectangle bounds) => kind switch
    {
        SceneObjectKind.Bush => 0.72f + Math.Min(0.35f, bounds.Width / 180f),
        SceneObjectKind.Cloud => 0.40f,
        SceneObjectKind.Hill => 0.52f,
        SceneObjectKind.Tree => 0.66f,
        SceneObjectKind.Pipe => 0.95f,
        SceneObjectKind.QuestionBlock => 1.12f,
        SceneObjectKind.Brick => 0.88f,
        SceneObjectKind.Terrain => 0.38f,
        SceneObjectKind.Castle => 0.62f,
        SceneObjectKind.Flagpole => 0.75f,
        _ => 0.7f
    };

    private static string KindLabel(SceneObjectKind kind) => kind switch
    {
        SceneObjectKind.QuestionBlock => "Question block",
        _ => kind.ToString()
    };

    private static int ReadRam(NesFrame frame, int address) =>
        address >= 0 && address < frame.Ram.Length ? frame.Ram[address] : 0;

    private static int Pack(int topLeft, int bottomLeft, int topRight, int bottomRight) =>
        (topLeft << 24) | (bottomLeft << 16) | (topRight << 8) | bottomRight;

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;

    private sealed record SpriteTile(int OamIndex, Rectangle Bounds, Bitmap Image);
    private readonly record struct BackgroundObjectIdentity(SceneObjectKind Kind, string Label);
}
