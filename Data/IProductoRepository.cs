using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IProductoRepository
    {
        IEnumerable<Producto> GetAll();
        Producto? GetById(long id);
        void Add(Producto producto);
        void Update(Producto producto);
        void Delete(long id);
        bool SkuExists(string sku, long? excludeId = null);
        IEnumerable<mi_ferreteria.Models.ProductoCodigoBarra> GetBarcodes(long productoId);
        void ReplaceBarcodes(long productoId, IEnumerable<mi_ferreteria.Models.ProductoCodigoBarra> codigos);
        bool BarcodeExists(string codigo, long? excludeProductId = null);
    }
}
