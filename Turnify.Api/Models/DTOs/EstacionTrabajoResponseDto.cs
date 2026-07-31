using System;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class EstacionTrabajoResponseDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("proveedorId")]
        public Guid ProveedorId { get; set; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("activo")]
        public bool Activo { get; set; }

        // --- CAMPOS DE ESQUEMA DE COBRO ---
        [JsonPropertyName("tipoCobro")]
        public string TipoCobro { get; set; } = string.Empty;

        [JsonPropertyName("valorBase")]
        public decimal ValorBase { get; set; }

        [JsonPropertyName("estado")]
        public string Estado { get; set; } = string.Empty;

        // --- 🚀 CAMPOS AGREGADOS PARA ACTIVACIÓN TEMPORAL Y VENCIMIENTO (HU-001-C) ---
        [JsonPropertyName("fechaVencimiento")]
        public DateTimeOffset? FechaVencimiento { get; set; }

        [JsonPropertyName("periodicidad")]
        public string? Periodicidad { get; set; }
    }
}