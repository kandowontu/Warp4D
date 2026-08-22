namespace Warp4D.Emulation;

internal sealed class ViewportStabilizer
{
    private const int SmoothMovementLimit = 24;
    private const int PendingAgreementLimit = 4;
    private const int PersistentJumpSamples = 6;

    private AxisState _x = new(512);
    private AxisState _y = new(480);

    public (int X, int Y, bool Filtered) Update(int rawX, int rawY)
    {
        (int x, bool filteredX) = _x.Update(rawX);
        (int y, bool filteredY) = _y.Update(rawY);
        return (x, y, filteredX || filteredY);
    }

    public void Reset()
    {
        _x = new AxisState(512);
        _y = new AxisState(480);
    }

    internal static void Validate()
    {
        ViewportStabilizer filter = new();
        filter.Update(0, 0);
        foreach ((int x, int y) in new[] { (256, 0), (0, 239), (256, 239), (0, 0) })
        {
            (int stableX, int stableY, _) = filter.Update(x, y);
            if (CircularDistance(stableX, 0, 512) > 1 || CircularDistance(stableY, 0, 480) > 1)
            {
                throw new InvalidOperationException("Raster-split PPU scroll values escaped the viewport stabilizer.");
            }
        }

        for (int x = 1; x <= 80; x++)
        {
            (int stableX, _, _) = filter.Update(x, 0);
            if (stableX != x)
            {
                throw new InvalidOperationException("The viewport stabilizer blocked normal continuous scrolling.");
            }
        }

        for (int index = 0; index < PersistentJumpSamples; index++)
        {
            filter.Update(300, 200);
        }
        (int jumpedX, int jumpedY, _) = filter.Update(300, 200);
        if (jumpedX != 300 || jumpedY != 200)
        {
            throw new InvalidOperationException("The viewport stabilizer blocked a persistent scene transition.");
        }
    }

    private static int CircularDistance(int first, int second, int modulus) =>
        Math.Abs(SignedDistance(first, second, modulus));

    private static int SignedDistance(int value, int origin, int modulus)
    {
        int difference = Mod(value - origin, modulus);
        return difference > modulus / 2 ? difference - modulus : difference;
    }

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;

    private sealed class AxisState(int modulus)
    {
        private bool _initialized;
        private int _stable;
        private int _pending;
        private int _pendingSamples;

        public (int Value, bool Filtered) Update(int rawValue)
        {
            int raw = Mod(rawValue, modulus);
            if (!_initialized)
            {
                _initialized = true;
                _stable = raw;
                return (_stable, false);
            }

            if (CircularDistance(raw, _stable, modulus) <= SmoothMovementLimit)
            {
                _stable = raw;
                _pendingSamples = 0;
                return (_stable, false);
            }

            if (_pendingSamples > 0 &&
                CircularDistance(raw, _pending, modulus) <= PendingAgreementLimit)
            {
                _pendingSamples++;
            }
            else
            {
                _pending = raw;
                _pendingSamples = 1;
            }

            if (_pendingSamples >= PersistentJumpSamples)
            {
                _stable = raw;
                _pendingSamples = 0;
                return (_stable, false);
            }

            return (_stable, true);
        }
    }
}
