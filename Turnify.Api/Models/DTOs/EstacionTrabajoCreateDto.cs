using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class EstacionTrabajoCreateDto
    {
        [Required(ErrorMessage = "El nombre de la estación de trabajo es obligatorio.")]
        [MaxLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        // 🔹 Alias de respaldo JSON para evitar fallos si el frontend envía nombreSilla / nombre_silla
        [JsonPropertyName("nombreSilla")]
        public string? NombreSillaAlias
        {
            get => Nombre;
            set { if (!string.IsNullOrWhiteSpace(value)) Nombre = value; }
        }

        [JsonPropertyName("nombre_silla")]
        public string? NombreSillaSnakeCase
        {
            get => Nombre;
            set { if (!string.IsNullOrWhiteSpace(value)) Nombre = value; }
        }

        // --- CAMPOS FINANCIEROS Y OPERATIVOS ---
        
        [JsonPropertyName("tipoCobro")]
        [MaxLength(50)]
        public string TipoCobro { get; set; } = "Porcentaje";

        // 🔹 Alias de respaldo JSON para evitar fallos si el frontend envía tipo_cobro
        [JsonPropertyName("tipo_cobro")]
        public string? TipoCobroSnakeCase
        {
            get => TipoCobro;
            set { if (!string.IsNullOrWhiteSpace(value)) TipoCobro = value; }
        }

        [Range(0, double.MaxValue, ErrorMessage = "El valor base debe ser mayor o igual a 0.")]
        [JsonPropertyName("valorBase")]
        public decimal ValorBase { get; set; } = 0m;

        // 🔹 Alias de respaldo JSON para evitar fallos si el frontend envía valor_base
        [JsonPropertyName("valor_base")]
        public decimal? ValorBaseSnakeCase
        {
            get => ValorBase;
            set { if (value.HasValue) ValorBase = value.Value; }
        }

        [MaxLength(20)]
        [JsonPropertyName("estado")]
        public string Estado { get; set; } = "Disponible";

        // --- 🚀 CAMPOS AGREGADOS PARA ACTIVACIÓN Y CONTROL TEMPORAL (HU-001-B / C) ---

        [JsonPropertyName("fechaVencimiento")]
        public DateTimeOffset? FechaVencimiento { get; set; }

        [JsonPropertyName("fecha_vencimiento")]
        public DateTimeOffset? FechaVencimientoSnakeCase
        {
            get => FechaVencimiento;
            set { if (value.HasValue) FechaVencimiento = value.Value; }
        }

        [MaxLength(50)]
        [JsonPropertyName("periodicidad")]
        public string? Periodicidad { get; set; }
    }
}