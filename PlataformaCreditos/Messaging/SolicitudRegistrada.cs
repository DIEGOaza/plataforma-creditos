namespace PlataformaCreditos.Messaging;

public sealed record SolicitudRegistrada(
    Guid MessageId,
    int SolicitudId,
    int ClienteId,
    string UsuarioId,
    decimal MontoSolicitado,
    DateTime FechaSolicitud);
