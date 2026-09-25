using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PlataformaCreditos.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El usuario es obligatorio.")]
    public string UsuarioId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los ingresos mensuales son obligatorios.")]
    [Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = "Los ingresos mensuales deben ser mayores que cero.")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal IngresosMensuales { get; set; }

    public bool Activo { get; set; } = true;

    public virtual IdentityUser? Usuario { get; set; }

    public virtual ICollection<SolicitudCredito> Solicitudes { get; set; } = new List<SolicitudCredito>();
}
