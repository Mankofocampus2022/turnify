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

        // 🧠 BLINDAJE HU-10 / HU-12: Nullable para permitir proveedores dependientes 
        // o mapeos directos sin obligar una cuenta de usuario exclusiva por cada perfil.
        [Column("usuario_id")]
        [JsonPropertyName("usuario_id")]
        public Guid? UsuarioId { get; set; }

        [Required]
        [Column("tipo")]
        [JsonPropertyName("tipo")]
        public string Tipo { get; set; } = "negocio";

        // 🧠 ARQUITECTURA MASTER SENIOR: Columna discriminadora para separar los flujos del Bot.
        // Mapea con el ALTER TABLE de DBeaver y puede recibir "Barbero" o "Manicurista".
        // 🧠 BLINDAJE ULTRA SENIOR: Modificado a nullable (string?) para tolerar nulos de la DB sin crashear.
        [Column("categoria")]
        [JsonPropertyName("categoria")]
        public string? Categoria { get; set; } = "Barbero";

        [Required]
        [Column("nombre_comercial")] 
        [JsonPropertyName("nombre_comercial")]
        public string NombreComercial { get; set; } = string.Empty;

        // 🚩 AGREGADO: Sincronización para Validación Dual (Killer Fix)
        [Column("email")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        // 🛡️ FIX NUCLEAR DE PERSISTENCIA DBA: Cambiado a nullable (string?) con valor por defecto seguro
        // Esto elimina el choque relacional en el tracker de Entity Framework Core permitiendo actualizaciones directas
        [Column("telefono")]
        [StringLength(20)]
        [JsonPropertyName("telefono")]
        public string? Telefono { get; set; } = string.Empty;

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


        // =========================================================================
        // 🚀 NUEVOS CAMPOS REQUERIDOS (HU-08, HU-09, HU-10, HU-12, HU-22)
        // =========================================================================

        /// <summary>
        /// HU-08 & HU-11: Ruta relativa de la foto de perfil o avatar guardada en servidor.
        /// Ejemplo: /uploads/proveedores/a8f3b2c1-foto.jpg
        /// </summary>
        [Column("foto_url")]
        [JsonPropertyName("foto_url")]
        public string? FotoUrl { get; set; }

        /// <summary>
        /// HU-10 & HU-12 & HU-22: Indica si opera como profesional independiente (100% ganancias brutas).
        /// Si es false, se trata de un proveedor dependiente o local comercial.
        /// </summary>
        [Column("es_independiente")]
        [JsonPropertyName("es_independiente")]
        public bool EsIndependiente { get; set; } = false;

        /// <summary>
        /// 🟢 HU-22: Propiedad con getter/setter en minúscula sin mapear para evitar errores CS1061 de reflexión/compilador
        /// </summary>
        [NotMapped]
        [JsonIgnore]
        public bool es_independiente 
        { 
            get => EsIndependiente; 
            set => EsIndependiente = value; 
        }

        /// <summary>
        /// HU-10 & HU-12: Clave foránea opcional que apunta al Staff / Dueño del negocio.
        /// Será null si el profesional es independiente (EsIndependiente = true).
        /// </summary>
        [Column("staff_id")]
        [JsonPropertyName("staff_id")]
        public Guid? StaffId { get; set; }

        /// <summary>
        /// HU-09 & HU-12: Porcentaje de comisión asignado para el dependiente.
        /// Para independientes se establece por defecto en 0.00 (ganancia directa total).
        /// </summary>
        [Column("porcentaje_comision", TypeName = "decimal(5,2)")]
        [JsonPropertyName("porcentaje_comision")]
        public decimal PorcentajeComision { get; set; } = 0.00m;


        // --- 🚩 RELACIONES DE IDENTIDAD (El Blindaje Maestro) ---

        [ForeignKey("UsuarioId")]
        [JsonIgnore] // Evita ciclos en el login
        public virtual Usuarios? Usuario { get; set; }

        /// <summary>
        /// HU-12: Relación de navegación opcional hacia el Staff/Dueño del establecimiento.
        /// </summary>
        [ForeignKey("StaffId")]
        [JsonIgnore]
        public virtual Usuarios? Staff { get; set; }

        [JsonPropertyName("horarios")]
        public virtual ICollection<HorariosAtencion> Horarios { get; set; } = new List<HorariosAtencion>();
        
        [JsonIgnore]
        public virtual ICollection<Suscripciones> Suscripciones { get; set; } = new List<Suscripciones>();

        // 🛡️ Vínculo directo con Citas
        [JsonIgnore]
        public virtual ICollection<Citas> Citas { get; set; } = new List<Citas>();
    }
}