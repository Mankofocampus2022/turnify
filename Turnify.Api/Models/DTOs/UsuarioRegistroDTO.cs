using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class UsuarioRegistroDTO
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        // 🛡️ BLINDAJE TC-002: Límite superior estricto al nombre para mitigar String Bloat
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]*$", ErrorMessage = "El nombre solo admite caracteres alfabéticos y espacios.")]
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio.")]
        // 🛡️ BLINDAJE TC-002: Límite estructural RFC estándar para correos
        [StringLength(150, ErrorMessage = "El correo electrónico no puede superar los 150 caracteres.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        // Mantener tu validación original intacta (Límite 100 caracteres)
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "El RolId es obligatorio.")]
        [JsonPropertyName("rol_id")]
        public Guid RolId { get; set; }

        // 🛡️ BLINDAJE TC-002: Opcional para evitar fallos si el formulario de origen no envía teléfono de entrada
        [StringLength(20, ErrorMessage = "El número telefónico no puede exceder los 20 caracteres.")]
        [Phone(ErrorMessage = "Formato de teléfono no válido.")]
        [JsonPropertyName("telefono")]
        public string? Telefono { get; set; } = string.Empty;

        [JsonPropertyName("nombreComercial")]
        // 🛡️ BLINDAJE TC-002: Sanitización y límite estricto para el establecimiento comercial
        [StringLength(100, ErrorMessage = "El nombre comercial del establecimiento no puede superar los 100 caracteres.")]
        [RegularExpression(@"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑ\s\.,#\-&]*$", ErrorMessage = "El nombre comercial contiene caracteres especiales no válidos.")]
        public string? NombreComercial { get; set; }

        // 🔹 Alias de respaldo JSON para evitar fallos si el cliente manda nombre_comercial
        [JsonPropertyName("nombre_comercial")]
        public string? NombreComercialSnakeCase
        {
            get => NombreComercial;
            set { if (string.IsNullOrEmpty(NombreComercial)) NombreComercial = value; }
        }

        [JsonPropertyName("tipoNegocio")]
        // 🛡️ BLINDAJE TC-002: Límite defensivo para rubro comercial
        [StringLength(50, ErrorMessage = "El tipo de negocio no puede superar los 50 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]*$", ErrorMessage = "El tipo de negocio solo admite caracteres alfabéticos y espacios.")]
        public string? TipoNegocio { get; set; }

        // 🔹 Alias de respaldo JSON para evitar fallos si el cliente manda tipo_negocio
        [JsonPropertyName("tipo_negocio")]
        public string? TipoNegocioSnakeCase
        {
            get => TipoNegocio;
            set { if (string.IsNullOrEmpty(TipoNegocio)) TipoNegocio = value; }
        }

        // 🚀 MEJORA COMPATIBILIDAD STAFF/INDEPENDIENTE: Permite explicitar la modalidad si se registra un proveedor/colaborador
        [JsonPropertyName("esIndependiente")]
        public bool EsIndependiente { get; set; } = false;

        [JsonPropertyName("es_independiente")]
        public bool EsIndependienteSnakeCase
        {
            get => EsIndependiente;
            set => EsIndependiente = value;
        }
    }
}