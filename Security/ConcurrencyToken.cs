using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using mi_ferreteria.Models;

namespace mi_ferreteria.Security
{
    public static class ConcurrencyToken
    {
        public static string ComputeUsuarioHash(Usuario u)
        {
            var roles = (u.Roles ?? new System.Collections.Generic.List<Rol>())
                .Select(r => r.Id)
                .OrderBy(id => id)
                .ToArray();
            var baseStr = $"{u.Nombre?.Trim()}|{u.Email?.Trim().ToLowerInvariant()}|{(u.Activo ? 1 : 0)}|{string.Join(',', roles)}";
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(baseStr));
            return Convert.ToHexString(bytes);
        }

        public static string ComputeProductoHash(mi_ferreteria.Models.Producto p, System.Collections.Generic.IEnumerable<long> categoriaIds)
        {
            var cats = (categoriaIds ?? System.Array.Empty<long>()).OrderBy(id => id).ToArray();
            var baseStr = string.Join('|', new string[]
            {
                p.Sku?.Trim().ToLowerInvariant() ?? string.Empty,
                p.Nombre?.Trim().ToLowerInvariant() ?? string.Empty,
                (p.Descripcion ?? string.Empty).Trim(),
                (p.CategoriaId?.ToString() ?? string.Empty),
                p.PrecioVentaActual.ToString(System.Globalization.CultureInfo.InvariantCulture),
                p.StockMinimo.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (p.UnidadMedida ?? string.Empty).Trim().ToLowerInvariant(),
                p.Activo ? "1" : "0",
                (p.UbicacionPreferidaId?.ToString() ?? string.Empty),
                (p.UbicacionCodigo ?? string.Empty).Trim().ToUpperInvariant(),
                string.Join(',', cats)
            });
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(baseStr));
            return Convert.ToHexString(bytes);
        }
    }
}
