using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaCreditos.Models;

public enum EstadoSolicitud
{
    Pendiente = 0,
    Aprobado = 1,
    Rechazado = 2
}

public class SolicitudCredito
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El cliente es obligatorio.")]
    public int ClienteId { get; set; }

    [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
    [Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = "El monto solicitado debe ser mayor que cero.")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MontoSolicitado { get; set; }

    [Required(ErrorMessage = "La fecha de solicitud es obligatoria.")]
    [DataType(DataType.DateTime, ErrorMessage = "La fecha de solicitud no tiene un formato válido.")]
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "El estado de la solicitud es obligatorio.")]
    [EnumDataType(typeof(EstadoSolicitud), ErrorMessage = "El estado de la solicitud no es válido.")]
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [MaxLength(500, ErrorMessage = "El motivo de rechazo no puede superar los 500 caracteres.")]
    public string? MotivoRechazo { get; set; }

    public virtual Cliente? Cliente { get; set; }
}
