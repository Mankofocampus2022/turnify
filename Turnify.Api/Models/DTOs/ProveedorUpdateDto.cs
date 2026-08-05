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

        // 🎯 FLAG DE INDEPENDIENTE / ESTABLECIMIENTO
        // Permite actualizar la modalidad del proveedor desde la configuración o perfil
        [JsonPropertyName("EsIndependiente")]
        public bool EsIndependiente { get; set; }

        // 🛡️ ALIAS DE COMPATIBILIDAD C#: Mapea a snake_case por si el backend o mapeadores 
        // legacy consultan 'es_independiente' en lugar de 'EsIndependiente'
        [JsonIgnore]
        public bool es_independiente
        {
            get => EsIndependiente;
            set => EsIndependiente = value;
        }
    }
}