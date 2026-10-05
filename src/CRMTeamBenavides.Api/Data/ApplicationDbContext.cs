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
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<FotoOrdenServicio> FotosOrdenServicio => Set<FotoOrdenServicio>();

    // Agenda / Citas
    public DbSet<Cita> Citas => Set<Cita>();
    public DbSet<HistorialEstadoCita> HistorialEstadosCita => Set<HistorialEstadoCita>();

    // Pedidos Lima
    public DbSet<PedidoLima> PedidosLima => Set<PedidoLima>();
    public DbSet<DetallePedidoLima> DetallesPedidoLima => Set<DetallePedidoLima>();
    public DbSet<HistorialEstadoPedidoLima> HistorialEstadosPedidoLima => Set<HistorialEstadoPedidoLima>();

    // Configuración
    public DbSet<ConfiguracionEmpresa> ConfiguracionesEmpresa => Set<ConfiguracionEmpresa>();
    public DbSet<HistorialTipoCambio> HistorialTiposCambio => Set<HistorialTipoCambio>();

    // Caja Chica
    public DbSet<CajaChica> CajasChicas => Set<CajaChica>();
    public DbSet<MovimientoCajaChica> MovimientosCajaChica => Set<MovimientoCajaChica>();

    // Inventario
    public DbSet<CategoriaProducto> CategoriasProducto => Set<CategoriaProducto>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();

    // Ventas y Pagos
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Comprobante> Comprobantes => Set<Comprobante>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<Pago> Pagos => Set<Pago>();

    // Chatbot
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<ConsultaChatbot> ConsultasChatbot => Set<ConsultaChatbot>();

    // Auditoría y Aprobaciones
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();
    public DbSet<SolicitudAprobacion> SolicitudesAprobacion => Set<SolicitudAprobacion>();
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
        modelBuilder.Entity<Rol>().HasIndex(r => r.Nombre).IsUnique();
        modelBuilder.Entity<Permiso>().HasIndex(p => p.Codigo).IsUnique();

        // Producto e Inventario: precisión monetaria (numeric(12,2)) e índices
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.Property(p => p.PrecioVenta).HasPrecision(12, 2);
            entity.Property(p => p.Costo).HasPrecision(12, 2);
            entity.HasIndex(p => p.Codigo).IsUnique();
        });

        modelBuilder.Entity<MovimientoInventario>(entity =>
        {
            entity.Property(m => m.CostoUnitario).HasPrecision(12, 2);
        });

        // Vehiculo: placa opcional con índice único filtrado por unidades activas con placa, índices de búsqueda y precisión
        modelBuilder.Entity<Vehiculo>(entity =>
        {
            entity.Property(v => v.LecturaMedidorActual).HasPrecision(12, 2);
            entity.Property(v => v.ValorEstimado).HasPrecision(12, 2);
            entity.Property(v => v.HorasUso).HasPrecision(12, 2);

            entity.HasIndex(v => v.Placa)
                .IsUnique()
                .HasFilter("\"Activo\" = true AND \"Placa\" IS NOT NULL AND \"Placa\" <> ''");

            entity.HasIndex(v => v.NumeroSerieVIN);
            entity.HasIndex(v => v.NumeroMotor);
            entity.HasIndex(v => v.Modelo);
            entity.HasIndex(v => v.Marca);
        });

        // Relación Usuario-Cliente e índice único filtrado de NumeroDocumento para clientes activos
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(c => c.UsuarioId)
                .IsUnique()
                .HasFilter("\"Activo\" = true AND \"UsuarioId\" IS NOT NULL");

            entity.HasIndex(c => c.NumeroDocumento)
                .IsUnique()
                .HasFilter("\"Activo\" = true AND \"NumeroDocumento\" IS NOT NULL AND \"NumeroDocumento\" <> ''");
        });

        // --- Propiedades calculadas ---
        modelBuilder.Entity<DetalleServicio>().Ignore(d => d.Subtotal);
        modelBuilder.Entity<DetalleVenta>().Ignore(d => d.Subtotal);
        modelBuilder.Entity<DetallePedidoLima>().Ignore(d => d.Subtotal);

        modelBuilder.HasSequence<long>("OrdenServicioNumeroSeq")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.HasSequence<long>("CitaNumeroSeq")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.HasSequence<long>("PedidoLimaNumeroSeq")
            .StartsAt(1)
            .IncrementsBy(1);

        // Configuración de Empresa
        modelBuilder.Entity<ConfiguracionEmpresa>(entity =>
        {
            entity.Property(c => c.PorcentajeIgv).HasPrecision(5, 2);
            entity.Property(c => c.TipoCambioVigente).HasPrecision(8, 4);
        });

        // Tipo de Cambio
        modelBuilder.Entity<HistorialTipoCambio>(entity =>
        {
            entity.Property(h => h.ValorCompra).HasPrecision(8, 4);
            entity.Property(h => h.ValorVenta).HasPrecision(8, 4);

            entity.HasOne(h => h.Usuario)
                .WithMany()
                .HasForeignKey(h => h.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(h => h.FechaVigencia);
        });

        // Caja Chica
        modelBuilder.Entity<CajaChica>(entity =>
        {
            entity.Property(c => c.MontoApertura).HasPrecision(12, 2);
            entity.Property(c => c.MontoCierre).HasPrecision(12, 2);
            entity.Property(c => c.SaldoCalculado).HasPrecision(12, 2);

            entity.HasOne(c => c.UsuarioApertura)
                .WithMany()
                .HasForeignKey(c => c.UsuarioAperturaId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.UsuarioCierre)
                .WithMany()
                .HasForeignKey(c => c.UsuarioCierreId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(c => c.Movimientos)
                .WithOne(m => m.CajaChica)
                .HasForeignKey(m => m.CajaChicaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => c.Estado)
                .IsUnique()
                .HasFilter("\"Estado\" = 0");
            entity.HasIndex(c => c.FechaApertura);
        });

        modelBuilder.Entity<MovimientoCajaChica>(entity =>
        {
            entity.Property(m => m.Monto).HasPrecision(12, 2);

            entity.HasOne(m => m.Usuario)
                .WithMany()
                .HasForeignKey(m => m.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(m => m.CajaChicaId);
            entity.HasIndex(m => m.Fecha);
            entity.HasIndex(m => m.Tipo);
        });

        // Catálogo de Servicios
        modelBuilder.Entity<Servicio>(entity =>
        {
            entity.Property(s => s.PrecioSugerido).HasPrecision(12, 2);
            entity.HasIndex(s => s.Nombre);
        });

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

            entity.Property(o => o.LecturaMedidorIngreso).HasPrecision(12, 2);
            entity.Property(o => o.HorasUsoIngreso).HasPrecision(12, 2);
            entity.Property(o => o.SubtotalGravado).HasPrecision(12, 2);
            entity.Property(o => o.SubtotalExonerado).HasPrecision(12, 2);
            entity.Property(o => o.SubtotalInafecto).HasPrecision(12, 2);
            entity.Property(o => o.MontoIgv).HasPrecision(12, 2);
            entity.Property(o => o.Total).HasPrecision(12, 2);

            entity.HasOne(o => o.UsuarioAprobacionGerencia)
                .WithMany()
                .HasForeignKey(o => o.UsuarioAprobacionGerenciaId)
                .OnDelete(DeleteBehavior.SetNull);

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

        modelBuilder.Entity<DetalleServicio>(entity =>
        {
            entity.Property(d => d.PrecioUnitario).HasPrecision(12, 2);
            entity.Property(d => d.CostoUnitarioHistorico).HasPrecision(12, 2);
            entity.Property(d => d.SubtotalGravado).HasPrecision(12, 2);
            entity.Property(d => d.PorcentajeIgvAplicado).HasPrecision(5, 2);
            entity.Property(d => d.MontoIgv).HasPrecision(12, 2);
            entity.Property(d => d.Total).HasPrecision(12, 2);

            entity.HasOne(d => d.OrdenServicio)
                .WithMany(o => o.Detalles)
                .HasForeignKey(d => d.OrdenServicioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Servicio)
                .WithMany()
                .HasForeignKey(d => d.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MetodoPago>(entity =>
        {
            entity.HasIndex(m => m.Codigo).IsUnique();
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.Property(p => p.Monto).HasPrecision(12, 2);

            entity.HasOne(p => p.MetodoPago)
                .WithMany()
                .HasForeignKey(p => p.MetodoPagoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Venta)
                .WithMany(v => v.Pagos)
                .HasForeignKey(p => p.VentaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.OrdenServicio)
                .WithMany(o => o.Pagos)
                .HasForeignKey(p => p.OrdenServicioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => p.VentaId);
            entity.HasIndex(p => p.OrdenServicioId);
            entity.HasIndex(p => p.Fecha);
        });

        modelBuilder.Entity<Venta>(entity =>
        {
            entity.Property(v => v.SubtotalGravado).HasPrecision(12, 2);
            entity.Property(v => v.SubtotalExonerado).HasPrecision(12, 2);
            entity.Property(v => v.SubtotalInafecto).HasPrecision(12, 2);
            entity.Property(v => v.MontoIgv).HasPrecision(12, 2);
            entity.Property(v => v.Total).HasPrecision(12, 2);

            entity.HasOne(v => v.Cliente)
                .WithMany()
                .HasForeignKey(v => v.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.OrdenServicio)
                .WithMany(o => o.Ventas)
                .HasForeignKey(v => v.OrdenServicioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DetalleVenta>(entity =>
        {
            entity.Property(d => d.PrecioUnitario).HasPrecision(12, 2);
            entity.Property(d => d.CostoUnitarioHistorico).HasPrecision(12, 2);
            entity.Property(d => d.SubtotalGravado).HasPrecision(12, 2);
            entity.Property(d => d.PorcentajeIgvAplicado).HasPrecision(5, 2);
            entity.Property(d => d.MontoIgv).HasPrecision(12, 2);
            entity.Property(d => d.Total).HasPrecision(12, 2);

            entity.HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Servicio)
                .WithMany()
                .HasForeignKey(d => d.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Comprobante>(entity =>
        {
            entity.Property(c => c.SubtotalGravado).HasPrecision(12, 2);
            entity.Property(c => c.SubtotalExonerado).HasPrecision(12, 2);
            entity.Property(c => c.SubtotalInafecto).HasPrecision(12, 2);
            entity.Property(c => c.PorcentajeIgv).HasPrecision(5, 2);
            entity.Property(c => c.MontoIgv).HasPrecision(12, 2);
            entity.Property(c => c.Total).HasPrecision(12, 2);

            entity.HasOne(c => c.OrdenServicio)
                .WithMany(o => o.Comprobantes)
                .HasForeignKey(c => c.OrdenServicioId)
                .OnDelete(DeleteBehavior.SetNull);
        });

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

        modelBuilder.Entity<FotoOrdenServicio>(entity =>
        {
            entity.ToTable("FotosOrdenServicio");

            entity.HasOne(f => f.OrdenServicio)
                .WithMany(o => o.Fotos)
                .HasForeignKey(f => f.OrdenServicioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.Usuario)
                .WithMany()
                .HasForeignKey(f => f.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(f => f.NombreArchivoOriginal).HasMaxLength(255);
            entity.Property(f => f.NombreArchivoAlmacenado).HasMaxLength(255);
            entity.Property(f => f.RutaRelativa).HasMaxLength(500);
            entity.Property(f => f.ContentType).HasMaxLength(100);
            entity.Property(f => f.Observacion).HasMaxLength(1000);

            entity.HasIndex(f => f.OrdenServicioId);
        });

        // --- Citas / Agenda del Taller ---
        modelBuilder.Entity<Cita>(entity =>
        {
            entity.ToTable("Citas");

            entity.HasOne(c => c.Cliente)
                .WithMany()
                .HasForeignKey(c => c.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Vehiculo)
                .WithMany()
                .HasForeignKey(c => c.VehiculoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.OrdenServicio)
                .WithMany()
                .HasForeignKey(c => c.OrdenServicioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(c => c.HistorialEstados)
                .WithOne(h => h.Cita)
                .HasForeignKey(h => h.CitaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => c.NumeroCita).IsUnique();
            entity.HasIndex(c => c.ClienteId);
            entity.HasIndex(c => c.VehiculoId);
            entity.HasIndex(c => c.FechaHoraProgramada);
            entity.HasIndex(c => c.Estado);
        });

        modelBuilder.Entity<HistorialEstadoCita>(entity =>
        {
            entity.ToTable("HistorialEstadosCita");

            entity.HasOne(h => h.Usuario)
                .WithMany()
                .HasForeignKey(h => h.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(h => h.CitaId);
            entity.HasIndex(h => h.Fecha);
        });

        // --- Pedidos Especiales de Lima ---
        modelBuilder.Entity<PedidoLima>(entity =>
        {
            entity.ToTable("PedidosLima");

            entity.Property(p => p.SubtotalGravado).HasPrecision(12, 2);
            entity.Property(p => p.SubtotalExonerado).HasPrecision(12, 2);
            entity.Property(p => p.SubtotalInafecto).HasPrecision(12, 2);
            entity.Property(p => p.PorcentajeIgv).HasPrecision(5, 2);
            entity.Property(p => p.MontoIgv).HasPrecision(12, 2);
            entity.Property(p => p.Total).HasPrecision(12, 2);

            entity.HasOne(p => p.Cliente)
                .WithMany()
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.Detalles)
                .WithOne(d => d.PedidoLima)
                .HasForeignKey(d => d.PedidoLimaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.HistorialEstados)
                .WithOne(h => h.PedidoLima)
                .HasForeignKey(h => h.PedidoLimaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => p.NumeroPedido).IsUnique();
            entity.HasIndex(p => p.ClienteId);
            entity.HasIndex(p => p.Fecha);
            entity.HasIndex(p => p.Estado);
            entity.HasIndex(p => p.NumeroGuia);
        });

        modelBuilder.Entity<DetallePedidoLima>(entity =>
        {
            entity.ToTable("DetallesPedidoLima");

            entity.Property(d => d.PrecioUnitario).HasPrecision(12, 2);
            entity.Property(d => d.CostoUnitarioHistorico).HasPrecision(12, 2);
            entity.Property(d => d.SubtotalGravado).HasPrecision(12, 2);
            entity.Property(d => d.PorcentajeIgvAplicado).HasPrecision(5, 2);
            entity.Property(d => d.MontoIgv).HasPrecision(12, 2);
            entity.Property(d => d.Total).HasPrecision(12, 2);

            entity.HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(d => d.PedidoLimaId);
            entity.HasIndex(d => d.ProductoId);
        });

        modelBuilder.Entity<HistorialEstadoPedidoLima>(entity =>
        {
            entity.ToTable("HistorialEstadosPedidoLima");

            entity.HasOne(h => h.Usuario)
                .WithMany()
                .HasForeignKey(h => h.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(h => h.PedidoLimaId);
            entity.HasIndex(h => h.Fecha);
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.HasOne(p => p.PedidoLima)
                .WithMany(pl => pl.Pagos)
                .HasForeignKey(p => p.PedidoLimaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => p.PedidoLimaId);
        });

        modelBuilder.Entity<MovimientoCajaChica>(entity =>
        {
            entity.HasOne(m => m.Pago)
                .WithMany()
                .HasForeignKey(m => m.PagoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(m => m.MetodoPago)
                .WithMany()
                .HasForeignKey(m => m.MetodoPagoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(m => m.PagoId);
            entity.HasIndex(m => m.MetodoPagoId);
        });

        modelBuilder.Entity<Venta>(entity =>
        {
            entity.HasOne(v => v.UsuarioAprobacionGerencia)
                .WithMany()
                .HasForeignKey(v => v.UsuarioAprobacionGerenciaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PedidoLima>(entity =>
        {
            entity.HasOne(p => p.UsuarioAprobacionGerencia)
                .WithMany()
                .HasForeignKey(p => p.UsuarioAprobacionGerenciaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SolicitudAprobacion>(entity =>
        {
            entity.ToTable("SolicitudesAprobacion");

            entity.Property(s => s.Tipo).HasMaxLength(50).IsRequired();
            entity.Property(s => s.Entidad).HasMaxLength(50).IsRequired();
            entity.Property(s => s.EntidadId).HasMaxLength(100).IsRequired();
            entity.Property(s => s.DetalleCambio).HasMaxLength(1000).IsRequired();
            entity.Property(s => s.ValorAnterior).HasPrecision(12, 2);
            entity.Property(s => s.ValorSolicitado).HasPrecision(12, 2);
            entity.Property(s => s.Motivo).HasMaxLength(500);
            entity.Property(s => s.UsuarioSolicitanteNombre).HasMaxLength(150);
            entity.Property(s => s.UsuarioAprobadorNombre).HasMaxLength(150);
            entity.Property(s => s.ObservacionesRespuesta).HasMaxLength(500);

            entity.HasOne(s => s.UsuarioSolicitante)
                .WithMany()
                .HasForeignKey(s => s.UsuarioSolicitanteId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(s => s.UsuarioAprobador)
                .WithMany()
                .HasForeignKey(s => s.UsuarioAprobadorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(s => s.Estado);
            entity.HasIndex(s => s.Entidad);
            entity.HasIndex(s => s.EntidadId);
            entity.HasIndex(s => s.FechaSolicitud);
        });
    }
}
