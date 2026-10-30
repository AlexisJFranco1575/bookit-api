namespace Bookit.Application.Auth;

/// <summary>Wrong email or password. The message is the same in both cases so it reveals nothing about which accounts exist.</summary>
public sealed class InvalidCredentialsException() : Exception("Invalid email or password.");