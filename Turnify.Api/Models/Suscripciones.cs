using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models
{
    [Table("suscripciones")]
    public class Suscripciones
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; }

        [Column("ProveedorId")]
        public Guid ProveedorId { get; set; }

        [ForeignKey(nameof(ProveedorId))]
        public Proveedores Proveedor { get; set; } = null!;

        [Column("PlanId")]
        public Guid PlanId { get; set; }

        [ForeignKey(nameof(PlanId))]
        public PlanSuscripcion Plan { get; set; } = null!;

        // 🚀 HOMOLOGACIÓN: Cambiado a DateTime para coincidir con el tipo físico de SQL Server
        [Column("FechaInicio")]
        public DateTime FechaInicio { get; set; }

        [Column("FechaVencimiento")]
        public DateTime FechaVencimiento { get; set; }

        [Column("Estado")]
        public string Estado { get; set; } = "Activo";
    }
}