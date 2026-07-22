using System;

namespace Turnify.Api.Models.DTOs
{
    public class EstacionTrabajoResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProveedorId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; }

        // --- NUEVOS CAMPOS AGREGADOS ---
        public string TipoCobro { get; set; } = string.Empty;
        public decimal ValorBase { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}