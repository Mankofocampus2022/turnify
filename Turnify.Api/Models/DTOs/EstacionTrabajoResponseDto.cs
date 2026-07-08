using System;

namespace Turnify.Api.Models.DTOs
{
    public class EstacionTrabajoResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProveedorId { get; set; }
        public string Nombre { get; set; }
        public bool Activo { get; set; }
    }
}