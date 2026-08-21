namespace ShippingCalculator.Api.Models;

/// <summary>Response body for a validation failure (400). Mirrors the domain <c>Error</c>.</summary>
public sealed record ErrorResponse(string Code, string Message);
