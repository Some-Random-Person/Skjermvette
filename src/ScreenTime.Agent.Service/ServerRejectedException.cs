namespace ScreenTime.Agent.Service;

/// <summary>The server refused a request as invalid (400). Sending it again won't help.</summary>
public class ServerRejectedException(string problem) : Exception($"The server refused the request: {problem}");
