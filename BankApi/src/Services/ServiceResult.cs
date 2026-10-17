namespace BankApi.Services;

// What went wrong, in terms a controller can turn into an HTTP status:
// NotFound -> 404, Invalid -> 400, Conflict -> 409.
public enum ServiceError
{
    None,
    NotFound,
    Invalid,
    Conflict
}

// The outcome of a service operation: either a value, or an error plus a
// message that is safe to show the user.
public record ServiceResult<T>(T Value, ServiceError Error, string Message)
{
    public bool Succeeded => Error == ServiceError.None;

    public static ServiceResult<T> Ok(T value) => new(value, ServiceError.None, null);

    public static ServiceResult<T> Fail(ServiceError error, string message) => new(default, error, message);
}
