namespace CRMTeamBenavides.Api.Services;

public enum AuthResultStatus
{
    Success,
    InvalidCredentials,
    InvalidRefreshToken,
    RefreshTokenExpired,
    RefreshTokenRevoked
}

public class AuthResult<T>
{
    public AuthResultStatus Status { get; init; }
    public T? Data { get; init; }

    public bool IsSuccess => Status == AuthResultStatus.Success;

    public static AuthResult<T> Success(T data) =>
        new() { Status = AuthResultStatus.Success, Data = data };

    public static AuthResult<T> Failure(AuthResultStatus status) =>
        new() { Status = status };
}
