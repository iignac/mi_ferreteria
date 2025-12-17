namespace mi_ferreteria.Models
{
    public class ReporteFinancieroDeudor
    {
        public long ClienteId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal DeudaTotal { get; set; }
        public decimal LimiteCredito { get; set; }
    }
}
