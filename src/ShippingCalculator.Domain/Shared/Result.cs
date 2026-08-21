namespace ShippingCalculator.Domain.Shared;

/// <summary>
/// A small, dependency-free result type for functional error handling.
///
/// Domain and service methods that can fail for <em>expected</em> business
/// reasons (validation, business-rule violations) return
/// <c>Result&lt;TValue, Error&gt;</c> instead of throwing. Real exceptions are
/// reserved for truly exceptional/infrastructure conditions — never for
/// expected domain failures.
///
/// Reading <see cref="Value"/> on a failed result (or <see cref="Error"/> on a
/// successful one) throws <see cref="InvalidOperationException"/> — that is a
/// programmer bug, not a domain failure, so it is fine for it to throw.
/// </summary>
public readonly struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    private Result(bool isSuccess, TValue? value, TError? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>The success value. Throws if this result is a failure.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed Result.");

    /// <summary>The failure error. Throws if this result is a success.</summary>
    public TError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result.");

    public static Result<TValue, TError> Success(TValue value) => new(true, value, default);

    public static Result<TValue, TError> Failure(TError error) => new(false, default, error);

    /// <summary>
    /// Reduces the result to a single value by invoking exactly one of the two
    /// callbacks. Controllers typically use this to pick an HTTP status.
    /// </summary>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}
