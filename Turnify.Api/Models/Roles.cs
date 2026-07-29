using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models
{
    [Table("roles")] // Indica a EF que la tabla en SQL se llama "roles"
    public class Roles
    {
        [Key]
        [Column("id")]
        [JsonPropertyName("id")]
        public Guid id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(30)]
        [Column("nombre")]
        [JsonPropertyName("nombre")]
        public string nombre { get; set; } = string.Empty;

        // =========================================================================
        // 🚀 CONSTANTES ESTÁTICAS DE ROLES (HU-08 A HU-12)
        // Permite usar [Authorize(Roles = RoleNames.Staff)] o comparaciones en JWT
        // =========================================================================
        public static class RoleNames
        {
            public const string Administrador = "Administrador";
            public const string Cliente = "Cliente";
            public const string Proveedor = "Proveedor";
            public const string SuperAdministrador = "SuperAdministrador";
            public const string Staff = "Staff";
            public const string ProveedorDependiente = "ProveedorDependiente"; // 🚀 HU-09 / HU-12
            public const string ProveedorIndependiente = "ProveedorIndependiente"; // 🚀 HU-10 / HU-12
        }

        // =========================================================================
        // 🚀 GUIDs ESTÁTICOS PARA SEED DATA Y TESTING
        // Mismos IDs mapeados en el HasData de TurnifyDbContext.cs
        // =========================================================================
        public static class RoleIds
        {
            public static readonly Guid Administrador = Guid.Parse("6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43");
            public static readonly Guid Cliente = Guid.Parse("56992F75-6420-4D55-A5F9-9223248C50D7");
            public static readonly Guid Proveedor = Guid.Parse("8854C07C-6E5E-4876-A29A-C7AD5DCFBAB7");
            public static readonly Guid SuperAdministrador = Guid.Parse("6DE2A606-416E-4588-B4EB-CC20856CD80A");
            public static readonly Guid Staff = Guid.Parse("99A2B3C4-E5F6-4789-90AB-C1D2E3F40099");
            public static readonly Guid ProveedorDependiente = Guid.Parse("11B2C3D4-E5F6-7890-A1B2-C3D4E5F60010");
            public static readonly Guid ProveedorIndependiente = Guid.Parse("22C3D4E5-F6A7-8901-B2C3-D4E5F6A70020");
        }
    }
}