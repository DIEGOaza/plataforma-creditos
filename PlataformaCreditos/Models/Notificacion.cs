using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models;

public class Notificacion
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El MessageId es obligatorio.")]
    [MaxLength(36, ErrorMessage = "El MessageId no tiene un formato válido.")]
    public string MessageId { get; set; } = string.Empty;

    [Required(ErrorMessage = "La solicitud es obligatoria.")]
    public int SolicitudId { get; set; }

    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [MaxLength(450, ErrorMessage = "El usuario no puede superar los 450 caracteres.")]
    public string UsuarioId { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de notificación es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El tipo no puede superar los 100 caracteres.")]
    public string Tipo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El mensaje es obligatorio.")]
    [MaxLength(500, ErrorMessage = "El mensaje no puede superar los 500 caracteres.")]
    public string Mensaje { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public bool Leida { get; set; }
}
