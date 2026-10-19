using System.ComponentModel;

namespace Skjermvette.Core.DeviceApi;

public enum DevicePlatformEnum
{
    [Description("Windows")]
    Windows = 1,

    [Description("Android")]
    Android = 2,

    [Description("iOS / iPadOS")]
    Ios = 3,

    [Description("macOS")]
    MacOs = 4,

    [Description("Linux")]
    Linux = 5,
}
