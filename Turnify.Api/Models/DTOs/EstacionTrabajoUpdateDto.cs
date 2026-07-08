using System;
using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.Models.DTOs
{
    public class EstacionTrabajoUpdateDto
    {
        [Required(ErrorMessage = "El nombre de la estación de trabajo es obligatorio.")]
        [MaxLength(100)]
        public string Nombre { get; set; }

        [Required]
        public bool Activo { get; set; }
    }
}