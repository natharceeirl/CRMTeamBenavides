using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface ITokenService
{
    (string Token, DateTime Expiration) GenerateAccessToken(Usuario usuario, IEnumerable<string>? roles = null);
    string GenerateRefreshToken();
}
