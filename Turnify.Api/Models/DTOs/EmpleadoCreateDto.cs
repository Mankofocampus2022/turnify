using System;
using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.Models.DTOs
{
    public class EmpleadoCreateDto
    {
        [Required(ErrorMessage = "El nombre del empleado es obligatorio.")]
        [MaxLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
        public string Nombre { get; set; }

        [MaxLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
        public string Telefono { get; set; }

        [Required(ErrorMessage = "El tipo de contrato es obligatorio (Ej: Fijo o Porcentaje).")]
        [MaxLength(50)]
        public string TipoContrato { get; set; }

        [Required(ErrorMessage = "El valor del contrato es obligatorio.")]
        [Range(0, 99999999.99, ErrorMessage = "El valor debe ser un número positivo válido.")]
        public decimal ValorContrato { get; set; }

        // Opcional: Si el dueño quiere que el barbero tenga su propio usuario para iniciar sesión
        public string EmailParaUsuario { get; set; } 
        public string PasswordParaUsuario { get; set; }
    }
}