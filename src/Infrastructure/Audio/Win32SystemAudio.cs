using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Audio;

/// <summary>
/// Core Audio (MMDevice / IAudioEndpointVolume) adapter for <see cref="ISystemAudio"/>.
/// Sets the scalar volume of the default render (master) or capture (microphone) endpoint.
/// </summary>
public sealed class Win32SystemAudio : ISystemAudio
{
    private readonly ILogger<Win32SystemAudio> _logger;

    public Win32SystemAudio(ILogger<Win32SystemAudio>? logger = null)
        => _logger = logger ?? NullLogger<Win32SystemAudio>.Instance;

    public void SetVolume(VolumeTarget target, double level)
    {
        var dataFlow = target == VolumeTarget.Microphone ? EDataFlow.eCapture : EDataFlow.eRender;
        object? enumeratorObj = null;
        IMMDevice? device = null;
        object? endpointObj = null;
        try
        {
            enumeratorObj = new MMDeviceEnumerator();
            var enumerator = (IMMDeviceEnumerator)enumeratorObj;
            if (enumerator.GetDefaultAudioEndpoint(dataFlow, ERole.eMultimedia, out device) != 0 || device is null)
            {
                return;
            }

            var iid = IID_IAudioEndpointVolume;
            if (device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out endpointObj) != 0 || endpointObj is null)
            {
                return;
            }

            var endpoint = (IAudioEndpointVolume)endpointObj;
            var context = Guid.Empty;
            endpoint.SetMasterVolumeLevelScalar((float)Math.Clamp(level, 0, 1), ref context);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetVolume failed for {Target}", target);
        }
        finally
        {
            if (endpointObj is not null) Marshal.ReleaseComObject(endpointObj);
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumeratorObj is not null) Marshal.ReleaseComObject(enumeratorObj);
        }
    }

    // ── Core Audio COM interop ─────────────────────────────────────────────────
    private const uint CLSCTX_ALL = 23;
    private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private enum EDataFlow { eRender, eCapture, eAll }
    private enum ERole { eConsole, eMultimedia, eCommunications }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IntPtr ppDevices);

        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);

        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);

        [PreserveSig] int GetChannelCount(out uint pnChannelCount);

        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);

        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    }
}
