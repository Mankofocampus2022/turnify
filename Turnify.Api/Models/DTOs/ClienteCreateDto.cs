namespace Turnify.Api.Models.DTOs
{
    public class ClienteCreateDto
    {
        public required string Nombre { get; set; }
        public string? Email { get; set; }
        public required string Telefono { get; set; }
        
        // Esta es la pieza que le falta al rompecabezas:
        public required Guid UsuarioId { get; set; } 
    }
}