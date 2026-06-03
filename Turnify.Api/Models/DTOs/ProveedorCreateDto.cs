namespace Turnify.Api.Models.DTOs
{
    public class ProveedorCreateDto
    {
        public string nombre_comercial { get; set; } = string.Empty;
        public string direccion { get; set; } = string.Empty;
        public string tipo { get; set; } = string.Empty;

        // 🧠 BLINDAJE MASTER SENIOR: Captura la categoría enviada desde el JSON ("Barbero" o "Manicurista")
        // Se define con un valor por defecto seguro en caso de que falte en el request.
        public string? categoria { get; set; } = "Barbero";

        public Guid usuarioId { get; set; }
        public bool trabaja_domicilio { get; set; } 
        public bool activo { get; set; } = true;
    }
}