namespace mi_ferreteria.Data
{
    public interface IStockRepository
    {
        long GetStock(long productoId);
        void Ingresar(long productoId, long cantidad, string? motivo = null);
        void Egresar(long productoId, long cantidad, string? motivo = null);
        System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetMovimientos(long productoId, string? tipo = null, int top = 100);
        // Últimos movimientos globales (opcionalmente por tipo: INGRESO o EGRESO)
        System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetUltimosMovimientos(string? tipo = null, int top = 10);
        // Paginación de movimientos por producto y tipo
        int CountMovimientos(long productoId, string? tipo = null);
        System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetMovimientosPage(long productoId, string? tipo, int page, int pageSize);
    }
}
