using System.Runtime.InteropServices;

namespace Skjermvette.Agent.Helper;

/// <summary>
/// Finds which programs in this session are making sound, through WASAPI: every active output device's audio sessions
/// (<c>IAudioSessionManager2</c>), keeping those that are playing and whose peak level isn't silent.
/// Must be used from the thread that created it (the Helper's UI thread).
/// </summary>
public sealed class AudioSessions : IDisposable
{
    /// <summary>Quieter than this (about -60 dB) counts as silence, so an open stream playing nothing doesn't count.</summary>
    private const float SilentBelow = 0.001f;

    private const int RenderFlow = 0;
    private const uint DeviceStateActive = 0x1;
    private const uint ClsCtxAll = 0x17;
    private const int SessionStateActive = 1;

    private static readonly Guid MMDeviceEnumeratorId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid SessionManager2Id = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

    private readonly IMMDeviceEnumerator _devices =
        (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(MMDeviceEnumeratorId, throwOnError: true)!)!;

    /// <summary>The ids of processes in this session that are making sound right now. Empty if audio can't be read.</summary>
    public HashSet<uint> Playing()
    {
        var playing = new HashSet<uint>();
        var session = NativeMethods.CurrentSessionId();
        try
        {
            _devices.EnumAudioEndpoints(RenderFlow, DeviceStateActive, out var collection);
            using var releaseCollection = Release(collection);
            collection.GetCount(out var count);
            for (uint i = 0; i < count; i++)
            {
                try
                {
                    AddPlaying(collection, i, session, playing);
                }
                catch (COMException)
                {
                    // The device went away while it was being read.
                }
            }
        }
        catch (COMException)
        {
        }
        return playing;
    }

    private static void AddPlaying(IMMDeviceCollection collection, uint index, uint session, HashSet<uint> playing)
    {
        collection.Item(index, out var device);
        using var releaseDevice = Release(device);
        var managerId = SessionManager2Id;
        device.Activate(ref managerId, ClsCtxAll, IntPtr.Zero, out var activated);
        var manager = (IAudioSessionManager2)activated;
        using var releaseManager = Release(manager);
        manager.GetSessionEnumerator(out var sessions);
        using var releaseSessions = Release(sessions);
        sessions.GetCount(out var count);

        for (var i = 0; i < count; i++)
        {
            sessions.GetSession(i, out var control);
            using var releaseControl = Release(control);
            var control2 = (IAudioSessionControl2)control;
            control2.GetState(out var state);
            if (state != SessionStateActive || control2.IsSystemSoundsSession() == 0)
                continue;

            control2.GetProcessId(out var processId);
            ((IAudioMeterInformation)control).GetPeakValue(out var peak);
            if (processId != 0 && peak >= SilentBelow && NativeMethods.SessionOf(processId) == session)
                playing.Add(processId);
        }
    }

    private static ComRelease Release(object comObject) => new(comObject);

    public void Dispose() => Marshal.FinalReleaseComObject(_devices);

    /// <summary>Releases a COM object at the end of a <c>using</c>, so polling every second doesn't pile up objects for the GC.</summary>
    private readonly struct ComRelease(object comObject) : IDisposable
    {
        public void Dispose() => Marshal.FinalReleaseComObject(comObject);
    }

    // The COM interfaces below declare every method up to the last one used, in vtable order; unused ones take IntPtr.

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        void GetCount(out uint count);
        void Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid interfaceId, uint context, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        // IAudioSessionManager
        void GetAudioSessionControl(IntPtr sessionId, uint flags, out IntPtr control);
        void GetSimpleAudioVolume(IntPtr sessionId, uint flags, out IntPtr volume);

        void GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        void GetCount(out int count);
        void GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // IAudioSessionControl
        void GetState(out int state);
        void GetDisplayName(out IntPtr name);
        void SetDisplayName(IntPtr name, IntPtr eventContext);
        void GetIconPath(out IntPtr path);
        void SetIconPath(IntPtr path, IntPtr eventContext);
        void GetGroupingParam(out Guid groupingParam);
        void SetGroupingParam(IntPtr groupingParam, IntPtr eventContext);
        void RegisterAudioSessionNotification(IntPtr client);
        void UnregisterAudioSessionNotification(IntPtr client);

        void GetSessionIdentifier(out IntPtr id);
        void GetSessionInstanceIdentifier(out IntPtr id);
        void GetProcessId(out uint processId);

        /// <summary>S_OK (0) for the system sounds session, S_FALSE (1) otherwise.</summary>
        [PreserveSig]
        int IsSystemSoundsSession();
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        void GetPeakValue(out float peak);
    }
}
