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

        [MaxLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
        public string Telefono { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de contrato es obligatorio (Ej: Fijo o Porcentaje).")]
        [MaxLength(50)]
        public string TipoContrato { get; set; } = string.Empty;

        [Required(ErrorMessage = "El valor del contrato es obligatorio.")]
        [Range(0, 99999999.99, ErrorMessage = "El valor debe ser un número positivo válido.")]
        public decimal ValorContrato { get; set; }

        // Opcional: Si el dueño quiere que el barbero tenga su propio usuario para iniciar sesión
        [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
        [MaxLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
        public string? EmailParaUsuario { get; set; } = string.Empty; 

        [MinLength(6, ErrorMessage = "La contraseña del usuario debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        public string? PasswordParaUsuario { get; set; } = string.Empty;

        // 🖼️ HU-08: Archivo de imagen adjunto desde el formulario multipart/form-data
        public IFormFile? Foto { get; set; }

        // Ruta o URL de texto en caso de que se pase una dirección precargada
        [MaxLength(500, ErrorMessage = "La URL de la foto no puede superar los 500 caracteres.")]
        public string? FotoUrl { get; set; }
    }
}