using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes { get; set; } = null!;

    public DbSet<SolicitudCredito> SolicitudesCredito { get; set; } = null!;

    public DbSet<Notificacion> Notificaciones { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cliente>(entity =>
        {
            entity.HasKey(cliente => cliente.Id);

            entity.Property(cliente => cliente.UsuarioId)
                .IsRequired();

            entity.Property(cliente => cliente.IngresosMensuales)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            entity.HasOne(cliente => cliente.Usuario)
                .WithMany()
                .HasForeignKey(cliente => cliente.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SolicitudCredito>(entity =>
        {
            entity.HasKey(solicitud => solicitud.Id);

            entity.Property(solicitud => solicitud.MontoSolicitado)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            entity.Property(solicitud => solicitud.FechaSolicitud)
                .IsRequired();

            entity.Property(solicitud => solicitud.Estado)
                .IsRequired();

            entity.HasOne(solicitud => solicitud.Cliente)
                .WithMany(cliente => cliente.Solicitudes)
                .HasForeignKey(solicitud => solicitud.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Un cliente solo puede tener una solicitud pendiente.
            entity.HasIndex(solicitud => new { solicitud.ClienteId, solicitud.Estado })
                .HasFilter("[Estado] = 0")
                .IsUnique();
        });

        builder.Entity<Notificacion>(entity =>
        {
            entity.HasKey(notificacion => notificacion.Id);

            entity.Property(notificacion => notificacion.MessageId)
                .IsRequired()
                .HasMaxLength(36);

            entity.HasIndex(notificacion => notificacion.MessageId)
                .IsUnique();

            entity.Property(notificacion => notificacion.UsuarioId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(notificacion => notificacion.Tipo)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(notificacion => notificacion.Mensaje)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(notificacion => notificacion.FechaCreacion)
                .IsRequired();
        });
    }
}
