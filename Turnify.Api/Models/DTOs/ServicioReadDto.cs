using System;
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

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;
        
        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }
        
        [JsonPropertyName("precio")]
        public decimal Precio { get; set; }

        [JsonPropertyName("duracionMinutos")]
        public int DuracionMinutos { get; set; }
        
        [JsonPropertyName("categoria")]
        public string Categoria { get; set; } = string.Empty;
        
        [JsonPropertyName("imagenUrl")]
        public string? ImagenUrl { get; set; }

        [JsonPropertyName("comisionPorcentaje")]
        public decimal ComisionPorcentaje { get; set; }
        
        [JsonPropertyName("activo")]
        public int Activo { get; set; } // 🚩 Mantenemos int (0, 1, 2) para estados complejos
    }
}