using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class ServicioReadDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        // 🚩 CLAVE PARA EL FILTRO: Sincronizado para que el JS lo reconozca siempre
        [JsonPropertyName("proveedorId")]
        public Guid? ProveedorId { get; set; }

        [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
        [MaxLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;
        
        [MaxLength(250, ErrorMessage = "La descripción no puede superar los 250 caracteres.")]
        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser mayor o igual a 0.")]
        [JsonPropertyName("precio")]
        public decimal Precio { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La duración en minutos debe ser mayor a 0.")]
        [JsonPropertyName("duracionMinutos")]
        public int DuracionMinutos { get; set; }
        
        [MaxLength(50, ErrorMessage = "La categoría no puede superar los 50 caracteres.")]
        [JsonPropertyName("categoria")]
        public string Categoria { get; set; } = "Barbería";
        
        [MaxLength(500, ErrorMessage = "La URL de la imagen no puede superar los 500 caracteres.")]
        [JsonPropertyName("imagenUrl")]
        public string? ImagenUrl { get; set; }

        [Range(0, 100, ErrorMessage = "El porcentaje de comisión debe estar entre 0 y 100.")]
        [JsonPropertyName("comisionPorcentaje")]
        public decimal ComisionPorcentaje { get; set; }
        
        [JsonPropertyName("activo")]
        public int Activo { get; set; } = 1; // 🚩 Mantenemos int (0, 1, 2) para estados complejos
    }
}