namespace CRMTeamBenavides.Api.Services;

public enum ServiceResultStatus
{
    Success,
    NotFound,
    ValidationError
}

public class ServiceResult<T>
{
    public ServiceResultStatus Status { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }

    public bool IsSuccess => Status == ServiceResultStatus.Success;

    public static ServiceResult<T> Success(T data) =>
        new() { Status = ServiceResultStatus.Success, Data = data };

    public static ServiceResult<T> NotFound() =>
        new() { Status = ServiceResultStatus.NotFound };

    public static ServiceResult<T> Invalid(string error) =>
        new() { Status = ServiceResultStatus.ValidationError, Error = error };
}
