// Source: cross-verified against tartakynov/audioswitch/IPolicyConfig.h (canonical C++
// header) and EarTrumpet dev branch IPolicyConfigWin7 (2026) — both agree on this
// 12-method / SetDefaultEndpoint-at-slot-11 layout. Windows 7 and later only (no Vista
// fallback needed per project STACK.md). Do NOT omit ResetDeviceFormat: at least one
// commonly-circulated copy (aifdsc/AudioChanger) omits it, shifting every later vtable
// slot and making SetDefaultEndpoint silently invoke the wrong native method
// (03-RESEARCH.md Pitfall A).
using System.Runtime.InteropServices;

namespace RigToggle.Windows.Audio;

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
}

[Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat();
    [PreserveSig] int GetDeviceFormat();
    [PreserveSig] int ResetDeviceFormat();
    [PreserveSig] int SetDeviceFormat();
    [PreserveSig] int GetProcessingPeriod();
    [PreserveSig] int SetProcessingPeriod();
    [PreserveSig] int GetShareMode();
    [PreserveSig] int SetShareMode();
    [PreserveSig] int GetPropertyValue();
    [PreserveSig] int SetPropertyValue();
    [PreserveSig] int SetDefaultEndpoint(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    [PreserveSig] int SetEndpointVisibility();
}

[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfigClient
{
}
