using System;
using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.Models.DTOs
{
    public class EstacionTrabajoCreateDto
    {
        [Required(ErrorMessage = "El nombre de la estación de trabajo es obligatorio.")]
        [MaxLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        // --- NUEVOS CAMPOS AGREGADOS ---
        [Required(ErrorMessage = "El tipo de cobro es obligatorio.")]
        [MaxLength(50)]
        public string TipoCobro { get; set; } = "Porcentaje";

        [Range(0, double.MaxValue, ErrorMessage = "El valor base debe ser mayor o igual a 0.")]
        public decimal ValorBase { get; set; } = 0;

        [MaxLength(20)]
        public string Estado { get; set; } = "Disponible";
    }
}