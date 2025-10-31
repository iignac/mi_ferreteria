namespace mi_ferreteria.Models
{
    public class Categoria
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public long? IdPadre { get; set; }
        public string? Descripcion { get; set; }
        public bool Activo { get; set; } = true;
    }
}
