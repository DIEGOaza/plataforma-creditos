using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PlataformaCreditos.Models;

public sealed class CrearSolicitudViewModel
{
    [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
    [Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = "El monto solicitado debe ser mayor que cero.")]
    [DataType(DataType.Currency, ErrorMessage = "El monto solicitado no tiene un formato válido.")]
    [Display(Name = "Monto solicitado")]
    public decimal? MontoSolicitado { get; set; }

    [BindNever]
    public decimal? IngresosMensuales { get; set; }

    [BindNever]
    public decimal? MontoMaximoPermitido { get; set; }
}
