using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class EstacionTrabajoActivarDto
    {
        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        [JsonPropertyName("metodoPago")]
        public string MetodoPago { get; set; } = "Efectivo"; // Efectivo, Nequi, Daviplata

        [Required(ErrorMessage = "El periodo de activación es obligatorio.")]
        [JsonPropertyName("periodo")]
        public string Periodo { get; set; } = "1 Mes"; // 1 Semana, 15 Días, 1 Mes, 1 Trimestre, 1 Semestre, 1 Año

        [JsonPropertyName("monto")]
        public decimal Monto { get; set; } = 0;

        [JsonPropertyName("comprobante")]
        public string? Comprobante { get; set; }
    }
}