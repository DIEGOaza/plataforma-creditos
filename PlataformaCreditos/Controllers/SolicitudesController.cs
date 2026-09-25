using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;

    public SolicitudesController(ApplicationDbContext context)
    {
        _context = context;
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

        return solicitud is null ? NotFound() : View(solicitud);
    }
}
