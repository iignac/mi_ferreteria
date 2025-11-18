namespace mi_ferreteria.Models
{
    public class VentaPago
    {
        public long Id { get; set; }
        public long VentaId { get; set; }
        public string Tipo { get; set; } // EFECTIVO | TARJETA | TRANSFERENCIA | CUENTA_CORRIENTE
        public decimal Monto { get; set; }
        public string? Detalle { get; set; }
    }
}

