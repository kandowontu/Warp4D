using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Warp4D.Emulation;

internal sealed class NesEmulator : IDisposable
{
    private const string SmbWorldSha256 = "F61548FDF1670CFFEFCC4F0B7BDCDD9EABA0C226E3B74F8666071496988248DE";
    private readonly object _nativeLock = new();
    private readonly ViewportStabilizer _viewportStabilizer = new();
    private bool _initialized;
    private bool _loaded;
    private bool _debugInitialized;
    private long _sequence;
    private int _inputMask;
    private bool _paused;
    private Thread? _runThread;

    public string? RomPath { get; private set; }
    public bool IsLoaded => _loaded;
    public bool IsSmbWorld { get; private set; }
    public string RomSha256 { get; private set; } = string.Empty;
    public bool IsPaused => _paused;
    public bool IsAudioEnabled { get; private set; }
    public string AudioDevices { get; private set; } = string.Empty;

    public void Initialize(IntPtr windowHandle = default, IntPtr viewerHandle = default)
    {
        if (_initialized)
        {
            return;
        }

        string? overriddenHome = Environment.GetEnvironmentVariable("WARP4D_HOME");
        string home = string.IsNullOrWhiteSpace(overriddenHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Warp4D")
            : Path.GetFullPath(overriddenHome);
        Directory.CreateDirectory(home);

        Trace("InitDll");
        MesenApi.InitDll();
        bool canPlayAudio = windowHandle != IntPtr.Zero && viewerHandle != IntPtr.Zero;
        Trace($"InitializeEmu audio={canPlayAudio} window=0x{windowHandle.ToInt64():X} viewer=0x{viewerHandle.ToInt64():X}");
        MesenApi.InitializeEmu(
            home,
            windowHandle,
            viewerHandle,
            noAudio: !canPlayAudio,
            noVideo: true,
            noInput: true);

        if (canPlayAudio)
        {
            // Mesen's Windows audio manager is only constructed when both native
            // handles are present. An empty device name selects DirectSound's
            // current default output device.
            MesenApi.SetAudioDevice(string.Empty);
            MesenApi.SetAudioLatency(60);
            MesenApi.SetSampleRate(48_000);
            MesenApi.SetMasterVolume(2.5, 0, ConsoleId.Master);

            IntPtr devicesPointer = MesenApi.GetAudioDevices();
            AudioDevices = devicesPointer == IntPtr.Zero
                ? string.Empty
                : Marshal.PtrToStringUTF8(devicesPointer) ?? string.Empty;
            IsAudioEnabled = true;
            Trace($"DirectSound initialized; devices={AudioDevices.Replace("||", ", ")}");
        }
        else
        {
            IsAudioEnabled = false;
            Trace("Audio disabled because the emulator is running headless");
        }

        Trace("SetControllerType");
        MesenApi.SetControllerType(0, ControllerType.StandardController);
        _initialized = true;
        Trace("Initialize complete");
    }

    public void Load(string path, IntPtr windowHandle = default, IntPtr viewerHandle = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string absolutePath = Path.GetFullPath(path);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("The selected ROM does not exist.", absolutePath);
        }

        string extension = Path.GetExtension(absolutePath);
        if (!extension.Equals(".nes", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Warp4D currently accepts iNES .nes ROM files.");
        }

        byte[] header = new byte[16];
        using (FileStream stream = File.OpenRead(absolutePath))
        {
            if (stream.Read(header, 0, header.Length) != header.Length ||
                header[0] != (byte)'N' || header[1] != (byte)'E' || header[2] != (byte)'S' || header[3] != 0x1A)
            {
                throw new InvalidDataException("This file does not have a valid iNES header.");
            }
        }

        lock (_nativeLock)
        {
            Trace("Load lock acquired");
            Initialize(windowHandle, viewerHandle);
            if (_loaded)
            {
                StopCoreLoop();
                if (_debugInitialized)
                {
                    MesenApi.DebugRelease();
                    _debugInitialized = false;
                }
                _loaded = false;
            }

            RomSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolutePath)));
            IsSmbWorld = RomSha256 == SmbWorldSha256;
            Trace("LoadROM");
            MesenApi.LoadROM(absolutePath, string.Empty);
            Trace("DebugInitialize");
            MesenApi.DebugInitialize();
            _debugInitialized = true;
            Trace("Input override");
            MesenApi.DebugSetInputOverride(0, 0);
            _loaded = true;
            _paused = false;
            _viewportStabilizer.Reset();
            RomPath = absolutePath;
            StartCoreLoop();
        }
    }

    public unsafe NesFrame? CaptureFrame()
    {
        lock (_nativeLock)
        {
            if (!_loaded || !_debugInitialized)
            {
                return null;
            }

            int[][] pixels = new int[4][];
            byte[][] tiles = new byte[4][];
            byte[][] attributes = new byte[4][];

            for (int table = 0; table < 4; table++)
            {
                pixels[table] = new int[NesFrame.NametablePixelCount];
                tiles[table] = new byte[32 * 30];
                attributes[table] = new byte[32 * 30];

                fixed (int* pixelPtr = pixels[table])
                fixed (byte* tilePtr = tiles[table])
                fixed (byte* attributePtr = attributes[table])
                {
                    MesenApi.DebugGetNametable(
                        table,
                        0,
                        (IntPtr)pixelPtr,
                        (IntPtr)tilePtr,
                        (IntPtr)attributePtr);
                }
            }

            byte[] oam = GetMemory(DebugMemoryType.SpriteMemory, 256);
            byte[] chr = GetMemory(DebugMemoryType.ChrRom, 8192);
            byte[] palette = GetMemory(DebugMemoryType.PaletteMemory, 32);
            byte[] ram = GetMemory(DebugMemoryType.InternalRam, 0x800);
            uint packedScroll = MesenApi.DebugGetPpuScroll();
            int rawScrollX = (int)(packedScroll & 0xFFFF);
            int rawScrollY = (int)(packedScroll >> 16);
            int scrollX = rawScrollX;
            int scrollY = rawScrollY;
            string scrollSource = "PPU register";

            if (IsSmbWorld && ram.Length > 0x071C)
            {
                // SMB changes the live PPU scroll to zero while drawing its fixed
                // status bar. Sampling that register asynchronously therefore
                // alternates between the level and an old nametable page. These
                // RAM variables are SMB's stable logical gameplay viewport.
                scrollX = (ram[0x071A] << 8) | ram[0x071C];
                scrollY = 0;
                scrollSource = "SMB RAM gameplay viewport";
            }
            else
            {
                (scrollX, scrollY, bool filtered) = _viewportStabilizer.Update(rawScrollX, rawScrollY);
                if (filtered)
                {
                    scrollSource = "stabilized PPU viewport";
                }
            }

            return new NesFrame
            {
                NametablePixels = pixels,
                Tiles = tiles,
                Attributes = attributes,
                Oam = oam,
                Chr = chr,
                Palette = palette,
                Ram = ram,
                ScrollX = scrollX,
                ScrollY = scrollY,
                RawScrollX = rawScrollX,
                RawScrollY = rawScrollY,
                ScrollSource = scrollSource,
                Sequence = ++_sequence
            };
        }
    }

    public void SetButton(NesButton button, bool pressed)
    {
        int bit = (int)button;
        if (pressed)
        {
            _inputMask |= bit;
        }
        else
        {
            _inputMask &= ~bit;
        }

        if (_loaded)
        {
            lock (_nativeLock)
            {
                MesenApi.DebugSetInputOverride(0, _inputMask);
            }
        }
    }

    public void Reset()
    {
        lock (_nativeLock)
        {
            if (_loaded)
            {
                MesenApi.Reset();
            }
        }
    }

    public bool TogglePause()
    {
        lock (_nativeLock)
        {
            if (!_loaded)
            {
                return false;
            }
            if (_paused)
            {
                MesenApi.Resume(ConsoleId.Master);
            }
            else
            {
                MesenApi.Pause(ConsoleId.Master);
            }
            _paused = !_paused;
            return _paused;
        }
    }

    private static unsafe byte[] GetMemory(DebugMemoryType type, int fallbackSize)
    {
        int size = MesenApi.DebugGetMemorySize(type);
        if (size <= 0)
        {
            size = fallbackSize;
        }
        size = Math.Min(size, Math.Max(fallbackSize, size));

        byte[] data = new byte[size];
        fixed (byte* pointer = data)
        {
            MesenApi.DebugGetMemoryState(type, (IntPtr)pointer);
        }
        return data;
    }

    private void StartCoreLoop()
    {
        if (_runThread?.IsAlive == true)
        {
            return;
        }

        _runThread = new Thread(() =>
        {
            Trace("Run thread entered");
            MesenApi.Run();
            Trace("Run thread returned");
        })
        {
            IsBackground = true,
            Name = "Mesen emulation core"
        };
        _runThread.Start();
        Trace("Run thread started");
    }

    private void StopCoreLoop()
    {
        if (_runThread is null)
        {
            return;
        }
        MesenApi.Stop();
        if (_runThread.IsAlive)
        {
            _runThread.Join(2000);
        }
        _runThread = null;
    }

    private static void Trace(string message)
    {
        string? path = Environment.GetEnvironmentVariable("WARP4D_TRACE");
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.AppendAllText(path, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
    }

    public void Dispose()
    {
        lock (_nativeLock)
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                if (_loaded)
                {
                    StopCoreLoop();
                    _loaded = false;
                }
                if (_debugInitialized)
                {
                    MesenApi.DebugRelease();
                    _debugInitialized = false;
                }
            }
            finally
            {
                MesenApi.Release();
                _initialized = false;
                IsAudioEnabled = false;
                AudioDevices = string.Empty;
            }
        }
    }
}

[Flags]
internal enum NesButton
{
    A = 0x01,
    B = 0x02,
    Select = 0x04,
    Start = 0x08,
    Up = 0x10,
    Down = 0x20,
    Left = 0x40,
    Right = 0x80
}
