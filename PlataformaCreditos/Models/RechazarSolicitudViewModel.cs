using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PlataformaCreditos.Models;

public sealed class RechazarSolicitudViewModel
{
    [Required(ErrorMessage = "Debes indicar el motivo del rechazo.")]
    [StringLength(500, ErrorMessage = "El motivo del rechazo no puede superar los 500 caracteres.")]
    [Display(Name = "Motivo del rechazo")]
    public string? MotivoRechazo { get; set; }

    [BindNever]
    [ValidateNever]
    public int SolicitudId { get; set; }

    [BindNever]
    [ValidateNever]
    public SolicitudCredito? Solicitud { get; set; }
}
