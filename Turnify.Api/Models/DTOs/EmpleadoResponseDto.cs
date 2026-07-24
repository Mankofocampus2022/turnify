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

        // 🖼️ HU-08 & HU-09: URL o ruta accesible de la foto de perfil para el frontend/directorio
        public string? FotoUrl { get; set; }

        public string TipoContrato { get; set; } = string.Empty;
        public decimal ValorContrato { get; set; }
        public bool Activo { get; set; }
        public string? EmailUsuarioVinculado { get; set; } = string.Empty; // Solo informativo
    }
}