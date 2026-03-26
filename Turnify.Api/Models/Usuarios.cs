using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models
{
    [Table("usuarios")]
    public class Usuarios
    {
        [Key]
        [JsonPropertyName("id")]
        public Guid id { get; set; }

        [Required]
        [Column("rol_id")]
        [JsonPropertyName("rol_id")] 
        public Guid rol_id { get; set; }

        [Required]
        [StringLength(100)]
        [JsonPropertyName("nombre")]
        public string nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [JsonPropertyName("email")]
        public string email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        [Column("password_hash")] 
        [JsonPropertyName("password_hash")]
        public string password_hash { get; set; } = string.Empty;

        // 🔥 Agregamos el '?' para que acepte los NULL de la base de datos
        [JsonPropertyName("activo")]
        public bool? activo { get; set; } = true;

        [JsonPropertyName("fecha_creacion")]
        public DateTime fecha_creacion { get; set; } = DateTime.UtcNow;

        // --- CAMPOS DE SUPERADMIN ---
        
        // 🔥 Agregamos el '?' aquí también para evitar el SqlNullValueException
        [JsonPropertyName("esta_bloqueado")]
        public bool? esta_bloqueado { get; set; } = false;

        [JsonPropertyName("suscripcion_fin")]
        public DateTime? suscripcion_fin { get; set; }

        [JsonPropertyName("ultima_conexion")]
        public DateTime? ultima_conexion { get; set; }

        // --- CAMPOS DE RECUPERACIÓN ---
        [JsonPropertyName("reset_token")]
        public string? ResetToken { get; set; }

        [JsonPropertyName("reset_token_expires")]
        public DateTime? ResetTokenExpires { get; set; }

        // --- RELACIONES ---
        [ForeignKey("rol_id")]
        [JsonIgnore] 
        public virtual Roles? Rol { get; set; }
    }
}