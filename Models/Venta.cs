using System;
using System.Collections.Generic;

namespace mi_ferreteria.Models
{
    public class Venta
    {
        public long Id { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public long? ClienteId { get; set; }
        public string TipoCliente { get; set; } = "CONSUMIDOR_FINAL"; // CONSUMIDOR_FINAL | REGISTRADO
        public string TipoPago { get; set; } = "CONTADO"; // CONTADO | CUENTA_CORRIENTE
        public decimal Total { get; set; }
        public string TotalEnLetras { get; set; }
        public int UsuarioId { get; set; }
        public string Estado { get; set; } = "CONFIRMADA"; // CONFIRMADA | ANULADA
        public string? Observaciones { get; set; }

        public List<VentaDetalle> Detalles { get; set; } = new();
    }
}

