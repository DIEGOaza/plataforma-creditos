using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Hubs;
using PlataformaCreditos.Infrastructure;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly IHubContext<SolicitudesHub> _hubContext;

    public AnalistaController(
        ApplicationDbContext context,
        IDistributedCache cache,
        IHubContext<SolicitudesHub> hubContext)
    {
        _context = context;
        _cache = cache;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var solicitudes = await _context.SolicitudesCredito
            .AsNoTracking()
            .Include(solicitud => solicitud.Cliente)
            .ThenInclude(cliente => cliente!.Usuario)
            .Where(solicitud => solicitud.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(solicitud => solicitud.FechaSolicitud)
            .ThenBy(solicitud => solicitud.Id)
            .ToListAsync();

        return View(solicitudes);
    }

    [HttpGet]
    [ActionName("Aprobar")]
    public async Task<IActionResult> ConfirmarAprobar(int id)
    {
        var solicitud = await ObtenerSolicitudPendienteAsync(id);
        return solicitud is null ? NotFound() : View(nameof(Aprobar), solicitud);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await ObtenerSolicitudPendienteAsync(id);
        if (solicitud is null)
        {
            return NotFound();
        }

        var limite = solicitud.Cliente!.IngresosMensuales * 5m;
        if (solicitud.MontoSolicitado > limite)
        {
            ModelState.AddModelError(
                nameof(SolicitudCredito.MontoSolicitado),
                $"El monto no puede superar 5 veces los ingresos mensuales del cliente (máximo: {limite:C}).");
            return View(nameof(Aprobar), solicitud);
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        solicitud.MotivoRechazo = null;
        await _context.SaveChangesAsync();
        await InvalidarCacheListadoAsync();
        await NotificarEstadoActualizadoAsync(solicitud);

        TempData["MensajeExito"] = "La solicitud fue aprobada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [ActionName("Rechazar")]
    public async Task<IActionResult> FormularioRechazar(int id)
    {
        var solicitud = await ObtenerSolicitudPendienteAsync(id);
        if (solicitud is null)
        {
            return NotFound();
        }

        return View(nameof(Rechazar), new RechazarSolicitudViewModel
        {
            SolicitudId = id,
            Solicitud = solicitud
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(
        int id,
        [Bind] RechazarSolicitudViewModel? modelo)
    {
        modelo ??= new RechazarSolicitudViewModel();
        modelo.SolicitudId = id;

        var solicitud = await ObtenerSolicitudPendienteAsync(id);
        if (solicitud is null)
        {
            return NotFound();
        }

        modelo.Solicitud = solicitud;

        if (!ModelState.IsValid || !TryValidateModel(modelo))
        {
            return View(nameof(Rechazar), modelo);
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = modelo.MotivoRechazo!.Trim();
        await _context.SaveChangesAsync();
        await InvalidarCacheListadoAsync();
        await NotificarEstadoActualizadoAsync(solicitud);

        TempData["MensajeExito"] = "La solicitud fue rechazada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private Task<SolicitudCredito?> ObtenerSolicitudPendienteAsync(int id)
    {
        return _context.SolicitudesCredito
            .Include(solicitud => solicitud.Cliente)
            .ThenInclude(cliente => cliente!.Usuario)
            .FirstOrDefaultAsync(solicitud =>
                solicitud.Id == id &&
                solicitud.Estado == EstadoSolicitud.Pendiente &&
                solicitud.Cliente != null);
    }

    private Task NotificarEstadoActualizadoAsync(SolicitudCredito solicitud)
    {
        var notificacion = new SolicitudEstadoActualizadoNotification(
            solicitud.Id,
            solicitud.Estado.ToString(),
            solicitud.MotivoRechazo,
            solicitud.MontoSolicitado,
            solicitud.FechaSolicitud);

        return _hubContext.Clients
            .Group(SolicitudesHub.GrupoUsuario(solicitud.Cliente!.UsuarioId))
            .SendAsync("SolicitudEstadoActualizado", notificacion);
    }

    private async Task InvalidarCacheListadoAsync()
    {
        await _cache.SetStringAsync(
            SolicitudesCacheKeys.ListadoVersion,
            Guid.NewGuid().ToString("N"));
    }
}
