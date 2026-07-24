using System;
using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.Models.DTOs
{
    public class ClienteCreateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Formato de correo no válido.")]
        [MaxLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [MaxLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
        public string Telefono { get; set; } = string.Empty;

        [Required(ErrorMessage = "El UsuarioId es obligatorio.")]
        public Guid UsuarioId { get; set; } 
    }
}