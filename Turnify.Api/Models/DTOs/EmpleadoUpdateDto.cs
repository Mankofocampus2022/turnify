using System;
using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.Models.DTOs
{
    public class EmpleadoUpdateDto
    {
        [Required(ErrorMessage = "El nombre del empleado es obligatorio.")]
        [MaxLength(120)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Telefono { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TipoContrato { get; set; } = string.Empty;

        [Required]
        [Range(0, 99999999.99)]
        public decimal ValorContrato { get; set; }

        public bool Activo { get; set; }
    }
}