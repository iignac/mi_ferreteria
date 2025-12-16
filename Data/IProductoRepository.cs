using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IProductoRepository
    {
        IEnumerable<Producto> GetAll();
        IEnumerable<Producto> GetPage(int page, int pageSize);
        IEnumerable<Producto> GetPageSorted(int page, int pageSize, string sort);
        int CountAll();
        // BA§squeda paginada por cualquier campo relevante
        int CountSearch(string query);
        IEnumerable<Producto> SearchPage(string query, int page, int pageSize);
        IEnumerable<Producto> SearchPageSorted(string query, int page, int pageSize, string sort);
        Producto? GetById(long id);
        void Add(Producto producto);
        void Update(Producto producto);
        void Delete(long id);
        bool SkuExists(string sku, long? excludeId = null);
        IEnumerable<mi_ferreteria.Models.ProductoCodigoBarra> GetBarcodes(long productoId);
        void ReplaceBarcodes(long productoId, IEnumerable<mi_ferreteria.Models.ProductoCodigoBarra> codigos);
        bool BarcodeExists(string codigo, long? excludeProductId = null);
        int CountInactive();
        IEnumerable<Producto> GetLastCreated(int top);
        IEnumerable<Producto> GetLastUpdated(int top);
        IEnumerable<Producto> GetLastInactive(int top);
        // CategorA-as mA§ltiples por producto (hasta 3)
        System.Collections.Generic.IEnumerable<long> GetCategorias(long productoId);
        void ReplaceCategorias(long productoId, System.Collections.Generic.IEnumerable<long> categoriaIds);
    }
}
