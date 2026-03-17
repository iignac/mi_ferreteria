-- Migración RBAC: Creación de la tabla usuario_permiso y población de la tabla permiso

CREATE TABLE IF NOT EXISTS public.usuario_permiso (
    usuario_id INT NOT NULL,
    permiso_id INT NOT NULL,
    PRIMARY KEY (usuario_id, permiso_id),
    CONSTRAINT fk_usuario_permiso_usuario FOREIGN KEY (usuario_id) REFERENCES public.usuario (id) ON DELETE CASCADE,
    CONSTRAINT fk_usuario_permiso_permiso FOREIGN KEY (permiso_id) REFERENCES public.permiso (id) ON DELETE CASCADE
);

-- Insertar los permisos base definidos en la clase Permisos.cs
-- Ventas
INSERT INTO permiso (nombre, descripcion) VALUES ('Ventas.Crear', 'Crear nuevas ventas') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Ventas.Ver', 'Ver listado de ventas') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Ventas.Autorizar', 'Autorizar ventas pendientes') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Ventas.Anular', 'Anular ventas') ON CONFLICT DO NOTHING;

-- Stock
INSERT INTO permiso (nombre, descripcion) VALUES ('Stock.Ver', 'Ver inventario y movimientos') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Stock.Ajustar', 'Ajustar stock e ingresar mercadería') ON CONFLICT DO NOTHING;

-- Productos
INSERT INTO permiso (nombre, descripcion) VALUES ('Productos.Ver', 'Ver catálogo de productos') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Productos.Crear', 'Crear productos') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Productos.Editar', 'Editar productos') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Productos.Eliminar', 'Eliminar productos') ON CONFLICT DO NOTHING;

-- Categorías
INSERT INTO permiso (nombre, descripcion) VALUES ('Categorias.Ver', 'Ver categorías') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Categorias.Gestionar', 'Crear, editar o eliminar categorías') ON CONFLICT DO NOTHING;

-- Clientes
INSERT INTO permiso (nombre, descripcion) VALUES ('Clientes.Ver', 'Ver listado de clientes y cuentas') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Clientes.Gestionar', 'Crear, editar o eliminar clientes') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Clientes.Cobranzas', 'Registrar cobranzas y consumos de cuenta corriente') ON CONFLICT DO NOTHING;

-- Reportes
INSERT INTO permiso (nombre, descripcion) VALUES ('Reportes.Finanzas', 'Ver tableros financieros') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Reportes.Auditoria', 'Ver registros de auditoría del sistema') ON CONFLICT DO NOTHING;

-- Usuarios
INSERT INTO permiso (nombre, descripcion) VALUES ('Usuarios.Ver', 'Ver listado de usuarios') ON CONFLICT DO NOTHING;
INSERT INTO permiso (nombre, descripcion) VALUES ('Usuarios.Gestionar', 'Administrar usuarios, roles y sus permisos') ON CONFLICT DO NOTHING;

-- Asignar automáticamente todos los permisos nuevos al rol "Administrador" (ID 1 típicamente)
-- Primero nos aseguramos de no duplicar insertando solo lo que falta
INSERT INTO rol_permiso (rol_id, permiso_id)
SELECT r.id, p.id
FROM rol r
CROSS JOIN permiso p
WHERE r.nombre = 'Administrador'
  AND NOT EXISTS (
      SELECT 1 FROM rol_permiso rp WHERE rp.rol_id = r.id AND rp.permiso_id = p.id
  );

-- Nota: Si la tabla permiso no tiene un constraint UNIQUE en la columna nombre, los "ON CONFLICT DO NOTHING" 
-- no funcionarán. En ese caso, asegúrese de agregar el constraint:
-- ALTER TABLE permiso ADD CONSTRAINT uq_permiso_nombre UNIQUE (nombre);

