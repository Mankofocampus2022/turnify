using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models // Ajusta el namespace según tu proyecto
{
    [Table("roles")] // Esto le dice a EF que la tabla en SQL se llama "roles" en minúsculas
    public class Roles
    {
        [Key]
        public Guid id { get; set; }

        [Required]
        [StringLength(30)]
        public string nombre { get; set; } = string.Empty;
    }
}