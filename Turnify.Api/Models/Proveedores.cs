using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models
{
    [Table("proveedores")]
    public class Proveedores
    {
        [Key]
        [Column("id")]
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("usuario_id")]
        [JsonPropertyName("usuario_id")]
        public Guid UsuarioId { get; set; }

        [Required]
        [Column("tipo")]
        [JsonPropertyName("tipo")]
        public string Tipo { get; set; } = "negocio";

        [Required]
        [Column("nombre_comercial")] 
        [JsonPropertyName("nombre_comercial")]
        public string NombreComercial { get; set; } = string.Empty;

        // 🚩 AGREGADO: Sincronización para Validación Dual (Killer Fix)
        [Column("email")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        // 🚩 Campo de teléfono para validación y contacto
        [Column("telefono")]
        [StringLength(20)]
        [JsonPropertyName("telefono")]
        public string Telefono { get; set; } = string.Empty;

        [Column("descripcion")]
        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }

        [Column("direccion")]
        [JsonPropertyName("direccion")]
        public string Direccion { get; set; } = string.Empty;

        [Column("ciudad")]
        [JsonPropertyName("ciudad")]
        public string? Ciudad { get; set; }

        [Column("trabaja_domicilio")]
        [JsonPropertyName("trabaja_domicilio")]
        public bool TrabajaDomicilio { get; set; } = false;

        [Column("activo")]
        [JsonPropertyName("activo")]
        public bool Activo { get; set; } = true;

        [Column("eliminado")]
        [JsonPropertyName("eliminado")]
        public bool Eliminado { get; set; } = false;

        [Column("fecha_creacion")]
        [JsonPropertyName("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        [Column("fecha_actualizacion")]
        [JsonPropertyName("fecha_actualizacion")]
        public DateTime? FechaActualizacion { get; set; }

        // --- 🚩 RELACIONES DE IDENTIDAD (El Blindaje Maestro) ---

        [ForeignKey("UsuarioId")]
        [JsonIgnore] // Evita ciclos en el login
        public virtual Usuarios? Usuario { get; set; }
        
        [JsonPropertyName("horarios")]
        public virtual ICollection<HorariosAtencion> Horarios { get; set; } = new List<HorariosAtencion>();
        
        [JsonIgnore]
        public virtual ICollection<Suscripciones> Suscripciones { get; set; } = new List<Suscripciones>();

        // 🛡️ [NUEVO] Vínculo directo con Citas
        // Esto permite que el CitaService encuentre las citas de "Tola y Maruja 2" 
        // simplemente navegando desde el objeto Proveedor.
        [JsonIgnore]
        public virtual ICollection<Citas> Citas { get; set; } = new List<Citas>();
    }
}