namespace Warp4D.Emulation;

internal sealed class NesFrame
{
    public const int ScreenWidth = 256;
    public const int ScreenHeight = 240;
    public const int NametablePixelCount = ScreenWidth * ScreenHeight;

    public required int[][] NametablePixels { get; init; }
    public required byte[][] Tiles { get; init; }
    public required byte[][] Attributes { get; init; }
    public required byte[] Oam { get; init; }
    public required byte[] Chr { get; init; }
    public required byte[] Palette { get; init; }
    public required byte[] Ram { get; init; }
    public int ScrollX { get; init; }
    public int ScrollY { get; init; }
    public required string ScrollSource { get; init; }
    public long Sequence { get; init; }
}
