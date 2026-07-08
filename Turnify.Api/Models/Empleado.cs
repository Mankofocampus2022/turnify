using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models
{
    [Table("empleados")]
    public class Empleado
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("proveedor_id")]
        public Guid ProveedorId { get; set; } // La agencia o barbería dueña

        [Column("usuario_id")]
        public Guid? UsuarioId { get; set; } // Opcional: Para cuando les hagamos el login propio

        [Required]
        [StringLength(120)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(20)]
        [Column("telefono")]
        public string? Telefono { get; set; }

        // 🧠 BLINDAJE FINANCIERO: "Porcentaje" o "Fijo"
        [Required]
        [StringLength(50)]
        [Column("tipo_contrato")]
        public string TipoContrato { get; set; } = "Porcentaje"; 

        [Required]
        [Column("valor_contrato", TypeName = "decimal(18, 2)")]
        public decimal ValorContrato { get; set; } = 0; 

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [ForeignKey("ProveedorId")]
        [JsonIgnore]
        public virtual Proveedores? Proveedor { get; set; }

        [ForeignKey("UsuarioId")]
        [JsonIgnore]
        public virtual Usuarios? Usuario { get; set; }
    }
}