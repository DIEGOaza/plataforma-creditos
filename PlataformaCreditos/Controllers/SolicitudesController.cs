using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private const string RolAnalista = "Analista";
    private const string ListadoCacheVersionKey = "solicitudes:listado:version";
    private static readonly TimeSpan ListadoCacheExpiration = TimeSpan.FromSeconds(60);

    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    public SolicitudesController(
        ApplicationDbContext context,
        IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return Forbid();
        }

        var modelo = new CrearSolicitudViewModel();
        var cliente = await ObtenerClienteAsync(usuarioId);

        if (cliente is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "No se encontró un cliente asociado con tu usuario.");
            return View(modelo);
        }

        if (!cliente.Activo)
        {
            ModelState.AddModelError(
                string.Empty,
                "Tu cliente está inactivo y no puede crear solicitudes.");
            return View(modelo);
        }

        CargarLimitePermitido(modelo, cliente);

        if (await TieneSolicitudPendienteAsync(cliente.Id))
        {
            ModelState.AddModelError(
                string.Empty,
                "Ya tienes una solicitud en estado Pendiente y no puedes crear otra.");
        }

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([Bind] CrearSolicitudViewModel? modelo)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return Forbid();
        }

        modelo ??= new CrearSolicitudViewModel();
        var cliente = await ObtenerClienteAsync(usuarioId);

        if (cliente is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "No se encontró un cliente asociado con tu usuario.");
            return View(modelo);
        }

        if (!cliente.Activo)
        {
            ModelState.AddModelError(
                string.Empty,
                "Tu cliente está inactivo y no puede crear solicitudes.");
            return View(modelo);
        }

        CargarLimitePermitido(modelo, cliente);
        var tieneSolicitudPendiente = await TieneSolicitudPendienteAsync(cliente.Id);
        if (tieneSolicitudPendiente)
        {
            ModelState.AddModelError(
                string.Empty,
                "Ya tienes una solicitud en estado Pendiente y no puedes crear otra.");
        }

        var limitePermitido = modelo.MontoMaximoPermitido!.Value;
        if (modelo.MontoSolicitado.HasValue && modelo.MontoSolicitado.Value > limitePermitido)
        {
            ModelState.AddModelError(
                nameof(CrearSolicitudViewModel.MontoSolicitado),
                $"El monto solicitado no puede superar 10 veces tus ingresos mensuales (máximo: {limitePermitido:C}).");
        }

        if (!ModelState.IsValid || !TryValidateModel(modelo))
        {
            return View(modelo);
        }

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = modelo.MontoSolicitado!.Value,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };

        _context.SolicitudesCredito.Add(solicitud);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                string.Empty,
                "No fue posible registrar la solicitud. Inténtalo nuevamente.");
            return View(modelo);
        }

        await InvalidarCacheListadoAsync();
        TempData["MensajeExito"] = "Tu solicitud de crédito fue registrada correctamente.";
        return RedirectToAction(nameof(MisSolicitudes));
    }

    [HttpGet]
    public async Task<IActionResult> MisSolicitudes(
        [FromQuery] MisSolicitudesViewModel? filtros = null)
    {
        filtros ??= new MisSolicitudesViewModel();

        // Los filtros llegan desde la URL, pero también se validan aquí para no
        // confiar exclusivamente en las validaciones del cliente.
        if (!ModelState.IsValid || !TryValidateModel(filtros))
        {
            return View(filtros);
        }

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return Forbid();
        }

        var cacheVersion = await ObtenerVersionCacheListadoAsync();
        var cacheKey = ConstruirClaveCacheListado(usuarioId, filtros, cacheVersion);
        var listadoSerializado = await _cache.GetStringAsync(cacheKey);

        if (listadoSerializado is not null)
        {
            try
            {
                filtros.Solicitudes = JsonSerializer.Deserialize<List<SolicitudCredito>>(listadoSerializado)
                    ?? new List<SolicitudCredito>();
                return View(filtros);
            }
            catch (JsonException)
            {
                // Si una entrada antigua o corrupta existe, se reemplaza al consultar la base de datos.
            }
        }

        var consulta = _context.SolicitudesCredito
            .AsNoTracking()
            .Where(solicitud => solicitud.Cliente!.UsuarioId == usuarioId);

        if (filtros.Estado.HasValue)
        {
            var estado = filtros.Estado.Value;
            consulta = consulta.Where(solicitud => solicitud.Estado == estado);
        }

        if (filtros.MontoMinimo.HasValue)
        {
            var montoMinimo = filtros.MontoMinimo.Value;
            consulta = consulta.Where(solicitud => solicitud.MontoSolicitado >= montoMinimo);
        }

        if (filtros.MontoMaximo.HasValue)
        {
            var montoMaximo = filtros.MontoMaximo.Value;
            consulta = consulta.Where(solicitud => solicitud.MontoSolicitado <= montoMaximo);
        }

        if (filtros.FechaInicio.HasValue)
        {
            var fechaInicio = filtros.FechaInicio.Value.Date;
            consulta = consulta.Where(solicitud => solicitud.FechaSolicitud >= fechaInicio);
        }

        if (filtros.FechaFin.HasValue)
        {
            var fechaFin = filtros.FechaFin.Value.Date;
            // El límite superior es exclusivo para incluir todo el día seleccionado.
            if (fechaFin < DateTime.MaxValue.Date)
            {
                var inicioDiaSiguiente = fechaFin.AddDays(1);
                consulta = consulta.Where(solicitud => solicitud.FechaSolicitud < inicioDiaSiguiente);
            }
            else
            {
                consulta = consulta.Where(solicitud => solicitud.FechaSolicitud <= fechaFin);
            }
        }

        filtros.Solicitudes = await consulta
            .OrderByDescending(solicitud => solicitud.FechaSolicitud)
            .ThenByDescending(solicitud => solicitud.Id)
            .ToListAsync();

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(filtros.Solicitudes),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ListadoCacheExpiration
            });

        return View(filtros);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return Forbid();
        }

        var solicitud = await _context.SolicitudesCredito
            .AsNoTracking()
            .Include(item => item.Cliente)
            .ThenInclude(cliente => cliente!.Usuario)
            .FirstOrDefaultAsync(item => item.Id == id && item.Cliente!.UsuarioId == usuarioId);

        if (solicitud is null)
        {
            return NotFound();
        }

        HttpContext.Session.SetInt32(SessionKeys.UltimaSolicitudId, solicitud.Id);
        HttpContext.Session.SetString(SessionKeys.UltimaSolicitudUsuarioId, usuarioId);
        HttpContext.Session.SetString(
            SessionKeys.UltimaSolicitudMonto,
            solicitud.MontoSolicitado.ToString("C"));

        return View(solicitud);
    }

    [HttpPost]
    [Authorize(Roles = RolAnalista)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
        int id,
        EstadoSolicitud estado,
        string? motivoRechazo)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        if (!Enum.IsDefined(typeof(EstadoSolicitud), estado))
        {
            ModelState.AddModelError(nameof(estado), "El estado seleccionado no es válido.");
            return BadRequest(ModelState);
        }

        if (estado == EstadoSolicitud.Rechazado && string.IsNullOrWhiteSpace(motivoRechazo))
        {
            ModelState.AddModelError(
                nameof(motivoRechazo),
                "Debes indicar el motivo del rechazo.");
        }

        if (motivoRechazo?.Length > 500)
        {
            ModelState.AddModelError(
                nameof(motivoRechazo),
                "El motivo del rechazo no puede superar los 500 caracteres.");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var solicitud = await _context.SolicitudesCredito.FindAsync(id);
        if (solicitud is null)
        {
            return NotFound();
        }

        solicitud.Estado = estado;
        solicitud.MotivoRechazo = estado == EstadoSolicitud.Rechazado
            ? motivoRechazo!.Trim()
            : null;

        await _context.SaveChangesAsync();
        await InvalidarCacheListadoAsync();

        return NoContent();
    }

    private Task<Cliente?> ObtenerClienteAsync(string usuarioId)
    {
        return _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(cliente => cliente.UsuarioId == usuarioId);
    }

    private Task<bool> TieneSolicitudPendienteAsync(int clienteId)
    {
        return _context.SolicitudesCredito
            .AsNoTracking()
            .AnyAsync(solicitud =>
                solicitud.ClienteId == clienteId &&
                solicitud.Estado == EstadoSolicitud.Pendiente);
    }

    private static void CargarLimitePermitido(
        CrearSolicitudViewModel modelo,
        Cliente cliente)
    {
        modelo.IngresosMensuales = cliente.IngresosMensuales;
        modelo.MontoMaximoPermitido = cliente.IngresosMensuales * 10m;
    }

    private async Task<string> ObtenerVersionCacheListadoAsync()
    {
        var version = await _cache.GetStringAsync(ListadoCacheVersionKey);
        return string.IsNullOrWhiteSpace(version) ? "inicial" : version;
    }

    private async Task InvalidarCacheListadoAsync()
    {
        // La versión forma parte de todas las claves; cambiarla invalida
        // simultáneamente los listados de todos los usuarios y filtros.
        await _cache.SetStringAsync(
            ListadoCacheVersionKey,
            Guid.NewGuid().ToString("N"));
    }

    private static string ConstruirClaveCacheListado(
        string usuarioId,
        MisSolicitudesViewModel filtros,
        string version)
    {
        var estado = filtros.Estado?.ToString() ?? "todos";
        var montoMinimo = filtros.MontoMinimo?.ToString(CultureInfo.InvariantCulture) ?? "";
        var montoMaximo = filtros.MontoMaximo?.ToString(CultureInfo.InvariantCulture) ?? "";
        var fechaInicio = filtros.FechaInicio?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
        var fechaFin = filtros.FechaFin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

        return $"solicitudes:listado:{version}:usuario:{usuarioId}:estado:{estado}:min:{montoMinimo}:max:{montoMaximo}:desde:{fechaInicio}:hasta:{fechaFin}";
    }
}
