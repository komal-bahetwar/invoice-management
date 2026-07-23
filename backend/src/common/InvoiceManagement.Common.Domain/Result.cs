namespace InvoiceManagement.Common.Domain;

/// <summary>
/// Generic result type for operation outcomes. Success or failure with errors.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<string> Errors { get; }

    protected Result(bool isSuccess, IEnumerable<string> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors.ToList().AsReadOnly();
    }

    public static Result Success() => new(true, []);
    public static Result Failure(string error) => new(false, [error]);
    public static Result Failure(IEnumerable<string> errors) => new(false, errors);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
    public static Result<T> Failure<T>(IEnumerable<string> errors) => Result<T>.Failure(errors);
}

/// <summary>
/// Generic result type carrying a value on success.
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(T? value, bool isSuccess, IEnumerable<string> errors)
        : base(isSuccess, errors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(value, true, []);
    public new static Result<T> Failure(string error) => new(default, false, [error]);
    public new static Result<T> Failure(IEnumerable<string> errors) => new(default, false, errors);
}
