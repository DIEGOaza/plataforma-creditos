using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PlataformaCreditos.Models;

public sealed class MisSolicitudesViewModel : IValidatableObject
{
    [EnumDataType(typeof(EstadoSolicitud), ErrorMessage = "El estado seleccionado no es válido.")]
    public EstadoSolicitud? Estado { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "El monto mínimo no puede ser negativo.")]
    public decimal? MontoMinimo { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "El monto máximo no puede ser negativo.")]
    public decimal? MontoMaximo { get; set; }

    [DataType(DataType.Date, ErrorMessage = "La fecha de inicio no tiene un formato válido.")]
    public DateTime? FechaInicio { get; set; }

    [DataType(DataType.Date, ErrorMessage = "La fecha de fin no tiene un formato válido.")]
    public DateTime? FechaFin { get; set; }

    [BindNever]
    public IReadOnlyList<SolicitudCredito> Solicitudes { get; set; } = Array.Empty<SolicitudCredito>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MontoMinimo.HasValue && MontoMaximo.HasValue && MontoMinimo > MontoMaximo)
        {
            yield return new ValidationResult(
                "El monto mínimo no puede ser mayor que el monto máximo.",
                [nameof(MontoMinimo), nameof(MontoMaximo)]);
        }

        if (FechaInicio.HasValue && FechaFin.HasValue && FechaInicio.Value.Date > FechaFin.Value.Date)
        {
            yield return new ValidationResult(
                "La fecha de inicio no puede ser posterior a la fecha de fin.",
                [nameof(FechaInicio), nameof(FechaFin)]);
        }
    }
}
