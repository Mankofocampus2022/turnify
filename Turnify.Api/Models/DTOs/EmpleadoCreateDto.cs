using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Turnify.Api.Models.DTOs
{
    public class EmpleadoCreateDto
    {
        [Required(ErrorMessage = "El nombre del empleado es obligatorio.")]
        [MaxLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
        [JsonPropertyName("telefono")]
        public string Telefono { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de contrato es obligatorio (Ej: Fijo o Porcentaje).")]
        [MaxLength(50)]
        [JsonPropertyName("tipoContrato")]
        public string TipoContrato { get; set; } = string.Empty;

        [Required(ErrorMessage = "El valor del contrato es obligatorio.")]
        [Range(0, 99999999.99, ErrorMessage = "El valor debe ser un número positivo válido.")]
        [JsonPropertyName("valorContrato")]
        public decimal ValorContrato { get; set; }

        // Opcional: Para contratos por comisión en negocios de barbería o manicure
        [Range(0, 100, ErrorMessage = "El porcentaje de comisión debe estar entre 0 y 100.")]
        [JsonPropertyName("porcentajeComision")]
        public decimal? PorcentajeComision { get; set; }

        // Opcional: Si el dueño quiere que el barbero tenga su propio usuario para iniciar sesión
        [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
        [MaxLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
        [JsonPropertyName("emailParaUsuario")]
        public string? EmailParaUsuario { get; set; }

        // 🚩 FIX CRÍTICO: Expresión regular que exige mínimo 6 caracteres SOLO SI se proporciona un valor (opcional)
        [RegularExpression(@"^.{6,}$", ErrorMessage = "La contraseña del usuario debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        [JsonPropertyName("passwordParaUsuario")]
        public string? PasswordParaUsuario { get; set; }

        // 🖼️ HU-08: Archivo de imagen adjunto desde el formulario multipart/form-data
        [JsonIgnore] // Se ignora en el mapeo JSON directo para evitar errores en Swagger / Serialización
        public IFormFile? Foto { get; set; }

        // Ruta o URL de texto en caso de que se pase una dirección precargada
        [MaxLength(500, ErrorMessage = "La URL de la foto no puede superar los 500 caracteres.")]
        [JsonPropertyName("fotoUrl")]
        public string? FotoUrl { get; set; }
    }
}