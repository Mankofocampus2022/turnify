using System;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class ProveedorUpdateDto
    {
        [JsonPropertyName("Id")]
        public Guid Id { get; set; }
        
        [JsonPropertyName("NombreComercial")]
        public string NombreComercial { get; set; } = string.Empty;
        
        [JsonPropertyName("Direccion")]
        public string Direccion { get; set; } = string.Empty;
        
        [JsonPropertyName("Tipo")]
        public string Tipo { get; set; } = string.Empty;

        [JsonPropertyName("Categoria")]
        public string? Categoria { get; set; }

        // 🚩 COPLAS DE BLINDAJE: Forzamos a que acepte tanto "Telefono" como "telefono" en el mapeo del JSON
        [JsonPropertyName("Telefono")]
        public string? Telefono { get; set; }
        
        [JsonPropertyName("Email")]
        public string? Email { get; set; }
    }
}