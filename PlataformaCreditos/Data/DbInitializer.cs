using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public static class DbInitializer
{
    private const string NombreRolAnalista = "Analista";
    private const string EmailAnalista = "analista@plataforma.com";
    private const string PasswordAnalista = "Analista123!";
    private const string EmailCliente = "cliente@plataforma.com";
    private const string PasswordCliente = "Cliente123!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await SeedAsync(context, userManager, roleManager);
    }

    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);

        await context.Database.MigrateAsync();

        var analista = await GetOrCreateUserAsync(
            userManager,
            EmailAnalista,
            PasswordAnalista);
        var clienteUsuario = await GetOrCreateUserAsync(
            userManager,
            EmailCliente,
            PasswordCliente);

        await EnsureAnalistaRoleAsync(roleManager, userManager, analista);

        var cliente1 = await GetOrCreateClienteAsync(
            context,
            clienteUsuario.Id,
            ingresosMensuales: 5000m);
        var cliente2 = await GetOrCreateClienteAsync(
            context,
            analista.Id,
            ingresosMensuales: 8000m);

        await context.SaveChangesAsync();

        var tienePendiente = await context.SolicitudesCredito.AnyAsync(solicitud =>
            solicitud.ClienteId == cliente1.Id &&
            solicitud.Estado == EstadoSolicitud.Pendiente);

        if (!tienePendiente)
        {
            context.SolicitudesCredito.Add(new SolicitudCredito
            {
                ClienteId = cliente1.Id,
                MontoSolicitado = 15000m,
                FechaSolicitud = DateTime.UtcNow.AddDays(-5),
                Estado = EstadoSolicitud.Pendiente
            });
        }

        var tieneAprobada = await context.SolicitudesCredito.AnyAsync(solicitud =>
            solicitud.ClienteId == cliente2.Id &&
            solicitud.Estado == EstadoSolicitud.Aprobado);

        if (!tieneAprobada)
        {
            context.SolicitudesCredito.Add(new SolicitudCredito
            {
                ClienteId = cliente2.Id,
                MontoSolicitado = 20000m,
                FechaSolicitud = DateTime.UtcNow.AddDays(-10),
                Estado = EstadoSolicitud.Aprobado
            });
        }

        await context.SaveChangesAsync();
    }

    // Alias compatible con las versiones anteriores del inicializador.
    public static Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        return SeedAsync(context, userManager, roleManager);
    }

    private static async Task<IdentityUser> GetOrCreateUserAsync(
        UserManager<IdentityUser> userManager,
        string email,
        string password)
    {
        var usuario = await userManager.FindByNameAsync(email);
        usuario ??= await userManager.FindByEmailAsync(email);

        if (usuario is null)
        {
            usuario = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var resultado = await userManager.CreateAsync(usuario, password);
            EnsureSucceeded(resultado, $"crear el usuario {email}");
        }

        return usuario;
    }

    private static async Task EnsureAnalistaRoleAsync(
        RoleManager<IdentityRole> roleManager,
        UserManager<IdentityUser> userManager,
        IdentityUser analista)
    {
        if (!await roleManager.RoleExistsAsync(NombreRolAnalista))
        {
            var resultadoRol = await roleManager.CreateAsync(
                new IdentityRole(NombreRolAnalista));
            EnsureSucceeded(resultadoRol, "crear el rol Analista");
        }

        if (!await userManager.IsInRoleAsync(analista, NombreRolAnalista))
        {
            var resultadoAsignacion = await userManager.AddToRoleAsync(
                analista,
                NombreRolAnalista);
            EnsureSucceeded(resultadoAsignacion, "asignar el rol Analista");
        }
    }

    private static async Task<Cliente> GetOrCreateClienteAsync(
        ApplicationDbContext context,
        string usuarioId,
        decimal ingresosMensuales)
    {
        var cliente = await context.Clientes.FirstOrDefaultAsync(item =>
            item.UsuarioId == usuarioId);

        if (cliente is null)
        {
            cliente = new Cliente
            {
                UsuarioId = usuarioId,
                IngresosMensuales = ingresosMensuales,
                Activo = true
            };
            context.Clientes.Add(cliente);
            await context.SaveChangesAsync();
        }

        return cliente;
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"No se pudo {operation}: {errors}");
    }
}
