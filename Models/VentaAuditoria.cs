using System;

namespace mi_ferreteria.Models
{
    public class VentaAuditoria
    {
        public long Id { get; set; }
        public long VentaId { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public int UsuarioId { get; set; }
        public string Accion { get; set; } // CREACION | ANULACION | AUTORIZACION_SOBRE_LIMITE | OTRO
        public string? Detalle { get; set; }
    }
}

