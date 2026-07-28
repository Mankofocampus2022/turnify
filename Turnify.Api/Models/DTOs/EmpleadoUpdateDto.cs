using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Turnify.Api.Models.DTOs
{
    public class EmpleadoUpdateDto
    {
        [MaxLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
        public string? Nombre { get; set; }

        [MaxLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
        public string? Telefono { get; set; }

        [MaxLength(50, ErrorMessage = "El tipo de contrato no puede superar los 50 caracteres.")]
        public string? TipoContrato { get; set; }

        [Range(0, 99999999.99, ErrorMessage = "El valor del contrato debe ser un número positivo válido.")]
        public decimal ValorContrato { get; set; }

        public bool Activo { get; set; } = true;

        // 🖼️ HU-08 & HU-09: Soporte opcional para actualización de fotografía
        public IFormFile? Foto { get; set; }

        [MaxLength(500, ErrorMessage = "La URL de la foto no puede superar los 500 caracteres.")]
        public string? FotoUrl { get; set; }
    }
}