namespace CRMTeamBenavides.Api.Services;

public enum ServiceResultStatus
{
    Success,
    NotFound,
    ValidationError,
    Forbidden,
    Conflict
}

public class ServiceResult<T>
{
    public ServiceResultStatus Status { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }

    public bool IsSuccess => Status == ServiceResultStatus.Success;

    public static ServiceResult<T> Success(T data) =>
        new() { Status = ServiceResultStatus.Success, Data = data };

    public static ServiceResult<T> NotFound(string? error = null) =>
        new() { Status = ServiceResultStatus.NotFound, Error = error };

    public static ServiceResult<T> Invalid(string error) =>
        new() { Status = ServiceResultStatus.ValidationError, Error = error };

    public static ServiceResult<T> Forbidden(string? error = null) =>
        new() { Status = ServiceResultStatus.Forbidden, Error = error ?? "No tiene autorización para realizar esta acción." };

    public static ServiceResult<T> Conflict(string error) =>
        new() { Status = ServiceResultStatus.Conflict, Error = error };
}
