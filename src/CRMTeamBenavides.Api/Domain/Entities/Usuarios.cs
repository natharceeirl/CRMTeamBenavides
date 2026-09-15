using Microsoft.AspNetCore.Identity;

namespace CRMTeamBenavides.Domain.Entities;

public class Usuario : IdentityUser<Guid>
{
    public Usuario()
    {
        Id = Guid.NewGuid();
    }

    public string NombreCompleto { get; set; } = string.Empty;

    // Campos de auditoría: antes venían de BaseEntity. Usuario ya no puede
    // heredar BaseEntity porque C# no permite herencia múltiple y ahora
    // hereda de IdentityUser<Guid>. Se agregan directamente aquí, sin
    // cambiar su comportamiento ni su nombre.
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaModificacion { get; set; }
    public Guid? CreadoPorId { get; set; }
    public Guid? ModificadoPorId { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
}

public class Rol : BaseEntity
{
    /// <summary>Ej: Admin, Recepcion, Tecnico, Vendedor.</summary>
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
}

public class Permiso : BaseEntity
{
    /// <summary>Ej: "ordenes.crear", "inventario.editar", "reportes.ver".</summary>
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
}

/// <summary>Tabla puente Rol-Permiso (clave compuesta).</summary>
public class RolPermiso
{
    public Guid RolId { get; set; }
    public Rol Rol { get; set; } = null!;

    public Guid PermisoId { get; set; }
    public Permiso Permiso { get; set; } = null!;
}

/// <summary>Tabla puente Usuario-Rol (clave compuesta). Un usuario puede tener más de un rol.</summary>
public class UsuarioRol
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public Guid RolId { get; set; }
    public Rol Rol { get; set; } = null!;
}
