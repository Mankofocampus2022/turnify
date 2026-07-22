using System;

namespace Turnify.Api.Models.DTOs
{
    public class EmpleadoResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProveedorId { get; set; }
        public Guid? UsuarioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string TipoContrato { get; set; } = string.Empty;
        public decimal ValorContrato { get; set; }
        public bool Activo { get; set; }
        public string? EmailUsuarioVinculado { get; set; } = string.Empty; // Solo informativo
    }
}