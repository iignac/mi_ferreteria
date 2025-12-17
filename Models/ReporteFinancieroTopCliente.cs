namespace mi_ferreteria.Models
{
    public class ReporteFinancieroTopCliente
    {
        public long ClienteId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int CantidadCompras { get; set; }
        public decimal ImporteTotal { get; set; }
    }
}
