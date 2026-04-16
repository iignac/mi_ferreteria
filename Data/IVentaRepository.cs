using System;
using System.Collections.Generic;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;

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

        /// <summary>
        /// Devuelve todas las ventas en el rango de fechas indicado (para exportación).
        /// </summary>
        IEnumerable<Venta> GetParaExportar(DateTime? desde, DateTime? hasta);

        /// <summary>
        /// Cantidad de ventas que cumplen los filtros indicados.
        /// </summary>
        int CountFiltrado(string? producto, string? clienteNombre, DateTime? fechaDesde, DateTime? fechaHasta, decimal? montoMin, decimal? montoMax, string? tipoCliente);

        /// <summary>
        /// Página de ventas filtradas, ordenadas por fecha descendente.
        /// </summary>
        IEnumerable<Venta> GetFiltrado(int page, int pageSize, string? producto, string? clienteNombre, DateTime? fechaDesde, DateTime? fechaHasta, decimal? montoMin, decimal? montoMax, string? tipoCliente);

        int CountFacturasPorCliente(long clienteId, string? q = null, DateTime? desde = null, DateTime? hasta = null);

        IEnumerable<ClienteFacturaItemViewModel> GetFacturasPorCliente(long clienteId, string? q, DateTime? desde, DateTime? hasta, int page, int pageSize);
    }
}
