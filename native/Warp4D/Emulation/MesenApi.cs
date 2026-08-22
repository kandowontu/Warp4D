using System.Runtime.InteropServices;

namespace Warp4D.Emulation;

internal enum DebugMemoryType
{
    CpuMemory = 0,
    PpuMemory = 1,
    PaletteMemory = 2,
    SpriteMemory = 3,
    SecondarySpriteMemory = 4,
    PrgRom = 5,
    ChrRom = 6,
    ChrRam = 7,
    WorkRam = 8,
    SaveRam = 9,
    InternalRam = 10,
    NametableRam = 11
}

internal enum ControllerType
{
    None = 0,
    StandardController = 1
}

internal enum ConsoleId
{
    Master = 0
}

internal static class MesenApi
{
    private const string DllName = "MesenCore.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void InitDll();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void InitializeEmu(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string homeFolder,
        IntPtr windowHandle,
        IntPtr viewerHandle,
        [MarshalAs(UnmanagedType.I1)] bool noAudio,
        [MarshalAs(UnmanagedType.I1)] bool noVideo,
        [MarshalAs(UnmanagedType.I1)] bool noInput);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Release();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void LoadROM(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string romPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string patchPath);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Run();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Stop();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Pause(ConsoleId consoleId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Resume(ConsoleId consoleId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Reset();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DebugInitialize();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DebugRelease();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DebugGetNametable(
        int nametableIndex,
        int displayMode,
        IntPtr frameBuffer,
        IntPtr tileData,
        IntPtr attributeData);

    [DllImport(DllName, EntryPoint = "DebugGetMemoryState", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint DebugGetMemoryState(DebugMemoryType memoryType, IntPtr destination);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int DebugGetMemorySize(DebugMemoryType memoryType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte DebugGetMemoryValue(DebugMemoryType memoryType, uint address);

    [DllImport(DllName, EntryPoint = "DebugGetPpuScroll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint DebugGetPpuScroll();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DebugSetInputOverride(int port, int buttonMask);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetControllerType(int port, ControllerType controllerType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetMasterVolume(double volume, double volumeReduction, ConsoleId consoleId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetSampleRate(uint sampleRate);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetAudioLatency(uint milliseconds);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetAudioDevice(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string audioDevice);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr GetAudioDevices();
}
