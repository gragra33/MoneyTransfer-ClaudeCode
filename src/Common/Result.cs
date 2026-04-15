namespace MoneyTransfer.Common;

/// <summary>Non-generic result for operations with no return value.</summary>
public sealed class Result
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the error message when <see cref="IsSuccess"/> is <c>false</c>; otherwise empty.</summary>
    public string Error { get; }

    private Result(bool isSuccess, string error) => (IsSuccess, Error) = (isSuccess, error);

    /// <summary>Returns a successful result.</summary>
    public static Result Ok() => new(true, string.Empty);

    /// <summary>Returns a failed result with the given <paramref name="error"/> message.</summary>
    public static Result Fail(string error) => new(false, error);

    /// <summary>Returns a successful <see cref="Result{T}"/> carrying <paramref name="value"/>.</summary>
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);

    /// <summary>Returns a failed <see cref="Result{T}"/> with the given <paramref name="error"/> message.</summary>
    public static Result<T> Fail<T>(string error) => Result<T>.Fail(error);
}

/// <summary>Generic result carrying a typed value on success.</summary>
/// <typeparam name="T">The type of the value carried on success.</typeparam>
public sealed class Result<T>
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the value when <see cref="IsSuccess"/> is <c>true</c>; otherwise <c>default</c>.</summary>
    public T? Value { get; }

    /// <summary>Gets the error message when <see cref="IsSuccess"/> is <c>false</c>; otherwise empty.</summary>
    public string Error { get; }

    private Result(bool isSuccess, T? value, string error) =>
        (IsSuccess, Value, Error) = (isSuccess, value, error);

    /// <summary>Returns a successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Ok(T value) => new(true, value, string.Empty);

    /// <summary>Returns a failed result with the given <paramref name="error"/> message.</summary>
    public static Result<T> Fail(string error) => new(false, default, error);
}
