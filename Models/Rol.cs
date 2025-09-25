using System.Collections.Generic;

namespace mi_ferreteria.Models
{
    public class Rol
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public List<Permiso> Permisos { get; set; } = new List<Permiso>();
    }
}
