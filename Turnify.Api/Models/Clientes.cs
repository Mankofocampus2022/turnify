using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models
{
    [Table("clientes")]
    public class Clientes
    {
        [Key]
        [Column("id")] 
        [JsonPropertyName("id")]
        public Guid id { get; set; }

        [Required]
        [Column("usuario_id")] 
        [JsonPropertyName("usuario_id")]
        public Guid usuario_id { get; set; }

        [Required]
        [StringLength(120)]
        [Column("nombre")]
        [JsonPropertyName("nombre")]
        public string nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Column("telefono")] 
        [JsonPropertyName("telefono")]
        public string telefono { get; set; } = string.Empty;

        [Required] 
        [StringLength(150)]
        [Column("email")] 
        [JsonPropertyName("email")]
        public string email { get; set; } = string.Empty;

        [Column("activo")]
        [JsonPropertyName("activo")]
        public bool activo { get; set; } = true;

        [Column("fecha_creacion")]
        [JsonPropertyName("fecha_creacion")]
        public DateTime fecha_creacion { get; set; } = DateTime.UtcNow; // 🛡️ Sincronizado con Usuarios.cs

        // --- 🚩 RELACIONES DE IDENTIDAD (Blindaje Maestro) ---

        [ForeignKey("usuario_id")]
        [JsonIgnore] // Evita ciclos infinitos en la serialización JSON
        public virtual Usuarios? Usuario { get; set; }

        // 🛡️ [NUEVO] Relación inversa para facilitar consultas desde el Service
        // Esto permite que al buscar una cita, Entity Framework sepa exactamente quién es el cliente
        [JsonIgnore]
        public virtual ICollection<Citas>? Citas { get; set; }
    }
}