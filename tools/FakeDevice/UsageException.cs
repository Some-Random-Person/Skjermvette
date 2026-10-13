namespace FakeDevice;

/// <summary>A mistake in how the tool was called, or an error from the server, shown to the user without a stack trace.</summary>
public class UsageException(string message) : Exception(message);
