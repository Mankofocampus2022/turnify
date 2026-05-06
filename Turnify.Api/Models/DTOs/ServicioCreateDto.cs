using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class ServicioCreateDto
    {
        [Required(ErrorMessage = "El ID del proveedor es obligatorio.")]
        [JsonPropertyName("proveedorId")] // 🚩 TRADUCTOR: Recibe 'proveedorId' del JS y lo guarda en 'proveedor_id'
        public Guid proveedor_id { get; set; }

        [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
        public string nombre { get; set; } = string.Empty;

        public string? descripcion { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0.")]
        public decimal precio { get; set; }

        [Required]
        [JsonPropertyName("duracionMinutos")] // 🚩 TRADUCTOR: Sincronizado con el JS
        public int duracion_minutos { get; set; }

        // 🛡️ NUEVOS CAMPOS BLINDADOS (Para que coincidan con tu JS)
        
        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string categoria { get; set; } = "Barbería";

        [JsonPropertyName("comisionPorcentaje")]
        public decimal comision_porcentaje { get; set; }

        public bool activo { get; set; } = true;
    }
}