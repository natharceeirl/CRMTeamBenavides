using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Data;

public class ApplicationDbContext : IdentityUserContext<Usuario, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // Usuarios / RBAC
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();

    // CRM
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();

    // Taller
    public DbSet<OrdenServicio> OrdenesServicio => Set<OrdenServicio>();
    public DbSet<DetalleServicio> DetallesServicio => Set<DetalleServicio>();

    // Inventario
    public DbSet<CategoriaProducto> CategoriasProducto => Set<CategoriaProducto>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();

    // Ventas
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Comprobante> Comprobantes => Set<Comprobante>();

    // Auditoría
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");

            entity.Property(rt => rt.Token)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasIndex(rt => rt.Token).IsUnique();

            entity.Property(rt => rt.ReemplazadoPorToken)
                .HasMaxLength(256);

            entity.Property(rt => rt.CreadoPorIp)
                .HasMaxLength(45);

            entity.HasOne(rt => rt.Usuario)
                .WithMany()
                .HasForeignKey(rt => rt.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Identity por defecto mapea a "AspNetUsers"; se conserva el nombre
        // de tabla que ya existe en la migración inicial.
        modelBuilder.Entity<Usuario>().ToTable("Usuarios");

        // --- Claves compuestas para tablas puente ---
        modelBuilder.Entity<RolPermiso>().HasKey(rp => new { rp.RolId, rp.PermisoId });
        modelBuilder.Entity<UsuarioRol>().HasKey(ur => new { ur.UsuarioId, ur.RolId });

        // --- Índices únicos ---
        modelBuilder.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Vehiculo>().HasIndex(v => v.Placa).IsUnique();
        modelBuilder.Entity<Producto>().HasIndex(p => p.Codigo).IsUnique();
        modelBuilder.Entity<Rol>().HasIndex(r => r.Nombre).IsUnique();
        modelBuilder.Entity<Permiso>().HasIndex(p => p.Codigo).IsUnique();

        // --- Propiedades calculadas ---
        modelBuilder.Entity<DetalleServicio>().Ignore(d => d.Subtotal);
        modelBuilder.Entity<DetalleVenta>().Ignore(d => d.Subtotal);

        // --- Evitar borrado en cascada accidental ---
        modelBuilder.Entity<OrdenServicio>()
            .HasOne(o => o.Vehiculo)
            .WithMany(v => v.OrdenesServicio)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Venta>()
            .HasOne(v => v.Cliente)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DetalleServicio>()
            .HasOne(d => d.Producto)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DetalleVenta>()
            .HasOne(d => d.Producto)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
