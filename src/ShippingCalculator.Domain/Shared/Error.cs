namespace ShippingCalculator.Domain.Shared;

/// <summary>
/// A domain-level failure: a short machine-readable <see cref="Code"/> plus a
/// human-readable <see cref="Message"/>. Used as the error type of
/// <see cref="Result{TValue, TError}"/> for expected business/validation
/// failures — never thrown as an exception.
/// </summary>
public sealed record Error(string Code, string Message);
