using System;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class EmpleadoResponseDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("proveedorId")]
        public Guid ProveedorId { get; set; }

        [JsonPropertyName("usuarioId")]
        public Guid? UsuarioId { get; set; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("telefono")]
        public string Telefono { get; set; } = string.Empty;

        // 🖼️ HU-08 & HU-09: URL o ruta accesible de la foto de perfil para el frontend/directorio
        [JsonPropertyName("fotoUrl")]
        public string? FotoUrl { get; set; }

        [JsonPropertyName("tipoContrato")]
        public string TipoContrato { get; set; } = string.Empty;

        [JsonPropertyName("valorContrato")]
        public decimal ValorContrato { get; set; }

        [JsonPropertyName("porcentajeComision")]
        public decimal? PorcentajeComision { get; set; }

        [JsonPropertyName("activo")]
        public bool Activo { get; set; }

        [JsonPropertyName("emailUsuarioVinculado")]
        public string? EmailUsuarioVinculado { get; set; } = string.Empty; // Solo informativo
    }
}