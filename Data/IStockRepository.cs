namespace mi_ferreteria.Data
{
    public interface IStockRepository
    {
        long GetStock(long productoId);
        System.Collections.Generic.IDictionary<long, long> GetStocks(System.Collections.Generic.IEnumerable<long> productoIds);
        void Ingresar(long productoId, long cantidad, string motivo, decimal? precioCompra = null);
        void Egresar(long productoId, long cantidad, string motivo);
        // Egreso que permite que el stock quede negativo (por ejemplo, ventas permitidas sin stock)
        void EgresarPermitiendoNegativo(long productoId, long cantidad, string motivo);
        System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetMovimientos(long productoId, string? tipo = null, int top = 100);
        // Últimos movimientos globales (opcionalmente por tipo: INGRESO o EGRESO)
        System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetUltimosMovimientos(string? tipo = null, int top = 10);
        // Paginación de movimientos por producto y tipo
        int CountMovimientos(long productoId, string? tipo = null);
        System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetMovimientosPage(long productoId, string? tipo, int page, int pageSize);
        // Paginación de movimientos globales (opcionalmente por tipo: INGRESO o EGRESO)
        int CountMovimientosGlobal(string? tipo = null);
        System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetMovimientosGlobalPage(string? tipo, int page, int pageSize);
    }
}
