namespace Bookit.Domain.Common;

/// <summary>A business rule was broken. The API will map it to a 422 response.</summary>
public sealed class DomainException(string message) : Exception(message);
