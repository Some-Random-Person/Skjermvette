namespace Skjermvette.Server.Contracts;

/// <summary>
/// After the password: either signed in (<see cref="Me"/>, on a remembered browser), or the second step
/// that's needed (<see cref="SecondStep"/>: <c>App</c> or <c>Email</c>).
/// </summary>
public sealed record LoginResponse(MeResponse? Me, TwoFactorMethodEnum? SecondStep);
