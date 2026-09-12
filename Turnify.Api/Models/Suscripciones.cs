using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models
{
    [Table("suscripciones")]
    public class Suscripciones
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("proveedor_id")]
        public Guid ProveedorId { get; set; }

        [ForeignKey(nameof(ProveedorId))]
        public Proveedores Proveedor { get; set; } = null!;

        [Column("plan_id")]
        public Guid PlanId { get; set; }

        [ForeignKey(nameof(PlanId))]
        public PlanSuscripcion Plan { get; set; } = null!;

        [Column("fecha_inicio")]
        public DateTimeOffset FechaInicio { get; set; } = DateTimeOffset.UtcNow;

        [Column("fecha_fin")]
        public DateTimeOffset FechaFin { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("fecha_creacion")]
        public DateTimeOffset FechaCreacion { get; set; } = DateTimeOffset.UtcNow;
    }
}