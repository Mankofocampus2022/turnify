using System;
using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.Models.DTOs
{
    public class EmpleadoUpdateDto
    {
        [Required(ErrorMessage = "El nombre del empleado es obligatorio.")]
        [MaxLength(120)]
        public string Nombre { get; set; }

        [MaxLength(20)]
        public string Telefono { get; set; }

        [Required]
        [MaxLength(50)]
        public string TipoContrato { get; set; }

        [Required]
        [Range(0, 99999999.99)]
        public decimal ValorContrato { get; set; }

        public bool Activo { get; set; }
    }
}