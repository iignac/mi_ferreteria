using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IVentaRepository
    {
        Venta CrearVenta(Venta venta, IEnumerable<VentaDetalle> detalles, bool registrarFactura, Cliente? cliente, string tipoComprobante, bool registrarPago = true);

        /// <summary>
        /// Obtiene una venta con sus detalles y, si existe, su factura asociada.
        /// </summary>
        (Venta venta, List<VentaDetalle> detalles, Factura? factura)? ObtenerComprobante(long ventaId);

        /// <summary>
        /// Cantidad total de ventas registradas.
        /// </summary>
        int CountAll();

        /// <summary>
        /// Obtiene una página de ventas ordenadas por fecha descendente.
        /// </summary>
        IEnumerable<Venta> GetPage(int page, int pageSize);

        /// <summary>
        /// Devuelve las ventas que estГЎn pendientes de autorizaciГіn.
        /// </summary>
        IEnumerable<Venta> GetPendientes();

        /// <summary>
        /// Autoriza una venta pendiente generando los comprobantes correspondientes.
        /// </summary>
        (Venta venta, List<VentaDetalle> detalles, Factura? factura)? AutorizarVentaPendiente(long ventaId, Cliente? cliente, string tipoComprobante, bool registrarFactura, bool registrarPago, int usuarioId, string? auditoriaDetalle);

        /// <summary>
        /// Marca una venta pendiente como rechazada.
        /// </summary>
        bool RechazarVentaPendiente(long ventaId, int usuarioId, string? motivo);
    }
}
