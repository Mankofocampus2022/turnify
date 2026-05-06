using System;
using System.ComponentModel.DataAnnotations; // 🚩 Agregado para el blindaje
using System.Text.Json.Serialization;       // 🛡️ Agregado para mapeo exacto con JS

namespace Turnify.Api.Models.DTOs
{
    // 🚩 DTO para solicitar la recuperación (Línea 62)
    public class ForgotPasswordDto
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido.")]
        public string Email { get; set; } = string.Empty;
    }

    // 🚩 DTO para restablecer la contraseña (Línea 79)
    public class ResetPasswordDto
    {
        // 🚩 AJUSTE DE BLINDAJE: Se comenta el Required para permitir la validación por Email + Teléfono
        // [Required(ErrorMessage = "El token es necesario.")]
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [JsonPropertyName("newPassword")] 
        public string NewPassword { get; set; } = string.Empty;

        // 🚩 AGREGADOS PARA VALIDACIÓN DUAL
        // Estos campos permiten que el sistema funcione incluso si el token está vacío
        [Required(ErrorMessage = "El email es necesario para la validación dual.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es necesario para la validación dual.")]
        [Phone(ErrorMessage = "Formato de teléfono no válido.")] // 🛡️ Blindaje: Valida que sea un número telefónico real
        [StringLength(20, MinimumLength = 7, ErrorMessage = "El teléfono debe tener entre 7 y 20 caracteres.")] // 🛡️ Blindaje: Evita strings vacíos o excesivos
        [JsonPropertyName("telefono")]
        public string Telefono { get; set; } = string.Empty;
    }
}