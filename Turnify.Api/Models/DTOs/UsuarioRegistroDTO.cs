using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class UsuarioRegistroDTO
    {
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
        [JsonPropertyName("rol_id")]
        public Guid RolId { get; set; }

        // 🚩 Campos nuevos para que el Service no chille
        [JsonPropertyName("telefono")]
        public string Telefono { get; set; } = string.Empty;
        [JsonPropertyName("nombreComercial")]
        public string? NombreComercial { get; set; }
        [JsonPropertyName("tipoNegocio")]
        public string? TipoNegocio { get; set; }
    }
}