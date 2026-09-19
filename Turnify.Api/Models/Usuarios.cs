using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models
{
    [Table("usuarios")]
    public class Usuarios
    {
        [Key]
        [Column("id")]
        [JsonPropertyName("id")]
        public Guid id { get; set; }

        [Required]
        [Column("rol_id")]
        [JsonPropertyName("rol_id")] 
        public Guid rol_id { get; set; }

        [Required]
        [StringLength(100)]
        [Column("nombre")]
        [JsonPropertyName("nombre")]
        public string nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [Column("email")]
        [JsonPropertyName("email")]
        public string email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        [Column("password_hash")] 
        [JsonPropertyName("password_hash")]
        public string password_hash { get; set; } = string.Empty;

        // 🛡️ PROPIEDAD VIRTUAL (No existe en la tabla física 'usuarios')
        [NotMapped] 
        [StringLength(20)]
        [JsonPropertyName("telefono")] 
        public string? telefono { get; set; } = string.Empty;

        [Column("activo")]
        [JsonPropertyName("activo")]
        public bool? activo { get; set; } = true;

        [Column("fecha_creacion")]
        [JsonPropertyName("fecha_creacion")]
        public DateTime fecha_creacion { get; set; } = DateTime.UtcNow;

        // --- CAMPOS DE GESTIÓN ---
        
        [Column("esta_bloqueado")]
        [JsonPropertyName("esta_bloqueado")]
        public bool? esta_bloqueado { get; set; } = false;

        [Column("suscripcion_fin")]
        [JsonPropertyName("suscripcion_fin")]
        public DateTime? suscripcion_fin { get; set; }

        [Column("ultima_conexion")]
        [JsonPropertyName("ultima_conexion")]
        public DateTime? ultima_conexion { get; set; }

        // --- CAMPOS DE RECUPERACIÓN ---
        
        [Column("reset_token")]
        [JsonPropertyName("reset_token")]
        public string? ResetToken { get; set; }

        [Column("reset_token_expires")]
        [JsonPropertyName("reset_token_expires")]
        public DateTime? ResetTokenExpires { get; set; }

        // --- 🚩 RELACIONES DE IDENTIDAD ---
        
        [ForeignKey("rol_id")]
        [JsonIgnore] 
        public virtual Roles? Rol { get; set; }

        [JsonIgnore]
        public virtual Clientes? Cliente { get; set; }

        [JsonIgnore]
        public virtual Proveedores? Proveedor { get; set; }
    }
}