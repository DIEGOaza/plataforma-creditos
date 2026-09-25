namespace PlataformaCreditos.Models;

public sealed record SolicitudEstadoActualizadoNotification(
    int SolicitudId,
    string Estado,
    string? MotivoRechazo,
    decimal MontoSolicitado,
    DateTime FechaSolicitud);
