namespace CRMTeamBenavides.Api.Features.Clientes;

/// <summary>Si el cliente ya entra a la app y, si no, hasta cuándo sirve su código.</summary>
public record CuentaAppClienteResponse(bool TieneCuenta, string? Email, DateTime? CodigoVigenteHasta);

/// <summary>El código se muestra una sola vez: la base guarda solo su hash.</summary>
public record CodigoActivacionResponse(string Codigo, DateTime Vence);

/// <summary>El correo es opcional: el cliente entra con su DNI.</summary>
public record ActivarCuentaRequest(string NumeroDocumento, string Codigo, string Password, string? Email);
