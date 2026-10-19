using System.ComponentModel;

namespace Skjermvette.Server.Contracts;

/// <summary>The second step at sign-in.</summary>
public enum TwoFactorMethodEnum
{
    [Description("Code from an authenticator app")]
    App = 1,

    [Description("Code sent by email")]
    Email = 2,

    [Description("Recovery code")]
    RecoveryCode = 3,
}
