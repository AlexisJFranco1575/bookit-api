namespace Bookit.Application.Common;

/// <summary>The thing the caller asked for does not exist. The API will map it to 404.</summary>
public sealed class NotFoundException(string entity, Guid id) : Exception($"{entity} '{id}' was not found.");

/// <summary>The request is valid but clashes with the current state (a taken slot). The API will map it to 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
