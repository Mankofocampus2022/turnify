using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class UsuarioRegistroDTO
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "El RolId es obligatorio.")]
        [JsonPropertyName("rol_id")]
        public Guid RolId { get; set; }

        // 🔥 VALIDACIÓN REAL PARA GUID (esto te faltaba)
        public bool RolIdValido => RolId != Guid.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio para la creación del perfil.")]
        // 🔥 CAMBIO: más flexible que [Phone]
        [RegularExpression(@"^\d{7,15}$", ErrorMessage = "El teléfono debe contener solo números (7 a 15 dígitos).")]
        [JsonPropertyName("telefono")]
        public string Telefono { get; set; } = string.Empty;

        [JsonPropertyName("nombreComercial")]
        public string? NombreComercial { get; set; }

        [JsonPropertyName("tipoNegocio")]
        public string? TipoNegocio { get; set; }
    }
}