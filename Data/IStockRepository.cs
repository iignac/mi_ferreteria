namespace mi_ferreteria.Data
{
    public interface IStockRepository
    {
        long GetStock(long productoId);
        void Ingresar(long productoId, long cantidad, string? motivo = null);
        void Egresar(long productoId, long cantidad, string? motivo = null);
    }
}

