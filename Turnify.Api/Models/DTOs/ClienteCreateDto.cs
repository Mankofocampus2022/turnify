using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Turnify.Api.Models.DTOs
{
    public class EmpleadoCreateDto
    {
        [Required(ErrorMessage = "El nombre del empleado es obligatorio.")]
        [MaxLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Formato de correo electrónico no válido.")]
        [MaxLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
        public string? Email { get; set; }

        [MaxLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
        public string? Telefono { get; set; }

        [Required(ErrorMessage = "El tipo de contrato es obligatorio.")]
        [MaxLength(50)]
        public string TipoContrato { get; set; } = "Porcentaje"; // "Porcentaje" o "Fijo"

        [Range(0, 999999999, ErrorMessage = "El valor del contrato debe ser un número positivo.")]
        public decimal ValorContrato { get; set; } = 0;

        // 🖼️ HU-08: Archivo de fotografía recibido desde el modal (FormData)
        public IFormFile? Foto { get; set; }

        // Ruta de texto opcional por si se recibe una URL precargada
        public string? FotoUrl { get; set; }
    }
}