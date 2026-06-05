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
        // 🛡️ BLINDAJE TC-002: Límite estricto a nombres de servicio para mitigar desbordamientos y fragmentación
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre del servicio debe tener entre 3 y 100 caracteres.")]
        [RegularExpression(@"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑ\s\.,#\-&]*$", ErrorMessage = "El nombre contiene caracteres especiales no permitidos.")]
        public string nombre { get; set; } = string.Empty;

        // 🛡️ BLINDAJE TC-002: Control de ráfagas de texto gigante en descripciones para proteger RAM y UX del Front
        [StringLength(500, ErrorMessage = "La descripción del servicio no puede superar el umbral corporativo de 500 caracteres.")]
        public string? descripcion { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(0.01, 9999999.99, ErrorMessage = "El precio debe ser un valor positivo válido mayor a 0.")]
        public decimal precio { get; set; }

        [Required(ErrorMessage = "La duración es obligatoria.")]
        [Range(1, 480, ErrorMessage = "La duración del servicio debe estar entre 1 y 480 minutos (8 horas).")]
        [JsonPropertyName("duracionMinutos")] // 🚩 TRADUCTOR: Sincronizado con el JS
        public int duracion_minutos { get; set; }

        // 🛡️ NUEVOS CAMPOS BLINDADOS (Para que coincidan con tu JS)
        
        [Required(ErrorMessage = "La categoría es obligatoria.")]
        // 🛡️ BLINDAJE TC-002: Evita la inyección de payloads maliciosos en la cadena de categoría
        [StringLength(50, ErrorMessage = "La categoría del servicio no puede superar los 50 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]*$", ErrorMessage = "La categoría solo admite caracteres alfabéticos y espacios.")]
        public string categoria { get; set; } = "Barbería";

        [JsonPropertyName("comisionPorcentaje")]
        // 🛡️ BLINDAJE: Restringe que las comisiones se mantengan en un rango financiero lógico (0% a 100%)
        [Range(0.00, 100.00, ErrorMessage = "El porcentaje de comisión debe estar en un rango real entre 0% y 100%.")]
        public decimal comision_porcentaje { get; set; }

        public bool activo { get; set; } = true;
    }
}