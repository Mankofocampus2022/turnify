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

        [JsonPropertyName("rol_id")] // <--- ESTO ES VITAL
        public Guid RolId { get; set; } 
    }
}