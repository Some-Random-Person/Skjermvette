namespace Skjermvette.Server.Contracts;

/// <summary>The key to add to an authenticator app: as text, as an <c>otpauth://</c> link, and as a QR code (SVG).</summary>
public sealed record AuthenticatorSetupResponse(string Key, string Uri, string QrCodeSvg);
