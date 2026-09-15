namespace CRMTeamBenavides.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Token { get; set; } = string.Empty;

    public DateTime FechaExpiracion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaRevocacion { get; set; }

    public string? ReemplazadoPorToken { get; set; }
    public string? CreadoPorIp { get; set; }
}