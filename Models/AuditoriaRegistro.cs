using System;

namespace mi_ferreteria.Models
{
    public class AuditoriaRegistro
    {
        public long Id { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
        public string? Detalle { get; set; }
    }
}
