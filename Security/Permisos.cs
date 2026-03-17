using System.Collections.Generic;

namespace mi_ferreteria.Security
{
    public static class Permisos
    {
        public static class Ventas
        {
            public const string Crear = "Ventas.Crear";
            public const string Ver = "Ventas.Ver";
            public const string Autorizar = "Ventas.Autorizar";
            public const string Anular = "Ventas.Anular";
        }

        public static class Stock
        {
            public const string Ver = "Stock.Ver";
            public const string Ajustar = "Stock.Ajustar";
        }

        public static class Productos
        {
            public const string Ver = "Productos.Ver";
            public const string Crear = "Productos.Crear";
            public const string Editar = "Productos.Editar";
            public const string Eliminar = "Productos.Eliminar";
        }

        public static class Categorias
        {
            public const string Ver = "Categorias.Ver";
            public const string Gestionar = "Categorias.Gestionar";
        }

        public static class Clientes
        {
            public const string Ver = "Clientes.Ver";
            public const string Gestionar = "Clientes.Gestionar";
            public const string Cobranzas = "Clientes.Cobranzas";
        }

        public static class Reportes
        {
            public const string Finanzas = "Reportes.Finanzas";
            public const string Auditoria = "Reportes.Auditoria";
        }

        public static class Usuarios
        {
            public const string Ver = "Usuarios.Ver";
            public const string Gestionar = "Usuarios.Gestionar";
        }

        // Helper metod to get all permissions for the UI
        public static IEnumerable<(string Grupo, string Nombre, string Descripcion)> Todos()
        {
            return new List<(string, string, string)>
            {
                ("Ventas", Ventas.Crear, "Crear nuevas ventas"),
                ("Ventas", Ventas.Ver, "Ver listado de ventas"),
                ("Ventas", Ventas.Autorizar, "Autorizar ventas pendientes"),
                ("Ventas", Ventas.Anular, "Anular ventas"),
                
                ("Stock", Stock.Ver, "Ver inventario y movimientos"),
                ("Stock", Stock.Ajustar, "Ajustar stock e ingresar mercadería"),
                
                ("Productos", Productos.Ver, "Ver catálogo de productos"),
                ("Productos", Productos.Crear, "Crear productos"),
                ("Productos", Productos.Editar, "Editar productos"),
                ("Productos", Productos.Eliminar, "Eliminar productos"),
                
                ("Categorías", Categorias.Ver, "Ver categorías"),
                ("Categorías", Categorias.Gestionar, "Crear, editar o eliminar categorías"),
                
                ("Clientes", Clientes.Ver, "Ver listado de clientes y cuentas"),
                ("Clientes", Clientes.Gestionar, "Crear, editar o eliminar clientes"),
                ("Clientes", Clientes.Cobranzas, "Registrar cobranzas y consumos de cuenta corriente"),
                
                ("Reportes", Reportes.Finanzas, "Ver tableros financieros"),
                ("Reportes", Reportes.Auditoria, "Ver registros de auditoría del sistema"),
                
                ("Usuarios", Usuarios.Ver, "Ver listado de usuarios"),
                ("Usuarios", Usuarios.Gestionar, "Administrar usuarios, roles y sus permisos")
            };
        }
    }
}
