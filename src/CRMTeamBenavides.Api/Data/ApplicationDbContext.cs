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
    public DbSet<HistorialEstadoOrden> HistorialEstadosOrden => Set<HistorialEstadoOrden>();

    // Inventario
    public DbSet<CategoriaProducto> CategoriasProducto => Set<CategoriaProducto>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();

    // Ventas
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Comprobante> Comprobantes => Set<Comprobante>();

    // Chatbot
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<ConsultaChatbot> ConsultasChatbot => Set<ConsultaChatbot>();

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
        modelBuilder.Entity<Producto>().HasIndex(p => p.Codigo).IsUnique();
        modelBuilder.Entity<Rol>().HasIndex(r => r.Nombre).IsUnique();
        modelBuilder.Entity<Permiso>().HasIndex(p => p.Codigo).IsUnique();

        // Vehiculo: placa opcional con índice único filtrado por unidades activas con placa
        modelBuilder.Entity<Vehiculo>(entity =>
        {
            entity.HasIndex(v => v.Placa)
                .IsUnique()
                .HasFilter("\"Activo\" = true AND \"Placa\" IS NOT NULL AND \"Placa\" <> ''");

            entity.HasIndex(v => v.NumeroSerieVIN);
        });

        // Relación Usuario-Cliente (ownership base para Portal Cliente)
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(c => c.UsuarioId)
                .IsUnique()
                .HasFilter("\"Activo\" = true AND \"UsuarioId\" IS NOT NULL");
        });

        // --- Propiedades calculadas ---
        modelBuilder.Entity<DetalleServicio>().Ignore(d => d.Subtotal);
        modelBuilder.Entity<DetalleVenta>().Ignore(d => d.Subtotal);

        modelBuilder.HasSequence<long>("OrdenServicioNumeroSeq")
            .StartsAt(1)
            .IncrementsBy(1);

        // --- Evitar borrado en cascada accidental y configurar Taller ---
        modelBuilder.Entity<OrdenServicio>(entity =>
        {
            entity.HasOne(o => o.Vehiculo)
                .WithMany(v => v.OrdenesServicio)
                .HasForeignKey(o => o.VehiculoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Cliente)
                .WithMany()
                .HasForeignKey(o => o.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(o => o.NumeroOrden)
                .IsUnique();
            entity.HasIndex(o => o.FechaIngreso);
            entity.HasIndex(o => o.ClienteId);
        });

        modelBuilder.Entity<HistorialEstadoOrden>(entity =>
        {
            entity.ToTable("HistorialEstadosOrden");

            entity.HasOne(h => h.OrdenServicio)
                .WithMany(o => o.HistorialEstados)
                .HasForeignKey(h => h.OrdenServicioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.Usuario)
                .WithMany()
                .HasForeignKey(h => h.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(h => h.OrdenServicioId);
            entity.HasIndex(h => h.FechaCambio);
        });

        modelBuilder.Entity<Venta>(entity =>
        {
            entity.HasOne(v => v.Cliente)
                .WithMany()
                .HasForeignKey(v => v.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.OrdenServicio)
                .WithMany(o => o.Ventas)
                .HasForeignKey(v => v.OrdenServicioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DetalleServicio>()
            .HasOne(d => d.Producto)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DetalleVenta>()
            .HasOne(d => d.Producto)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ConsultaChatbot>()
            .HasOne(c => c.Cliente)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ConsultaChatbot>()
            .HasOne(c => c.AgenteAsignado)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ConsultaChatbot>()
            .HasOne(c => c.FaqItem)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
