-- Migración: Índices de performance
-- Columnas usadas frecuentemente en WHERE, JOIN y ORDER BY

SET search_path TO venta, public;

-- usuario: login por email
CREATE INDEX IF NOT EXISTS idx_usuario_email ON usuario(email);

-- venta: filtros y orden más comunes
CREATE INDEX IF NOT EXISTS idx_venta_estado ON venta(estado);
CREATE INDEX IF NOT EXISTS idx_venta_fecha ON venta(fecha DESC);
CREATE INDEX IF NOT EXISTS idx_venta_cliente_id ON venta(cliente_id);

-- venta_detalle: join con venta (se usa en comprobantes y autorizaciones)
CREATE INDEX IF NOT EXISTS idx_venta_detalle_venta_id ON venta_detalle(venta_id);

-- venta_pago: join con venta
CREATE INDEX IF NOT EXISTS idx_venta_pago_venta_id ON venta_pago(venta_id);

-- factura: join con venta
CREATE INDEX IF NOT EXISTS idx_factura_venta_id ON factura(venta_id);

-- cliente: filtro por activo y búsquedas
CREATE INDEX IF NOT EXISTS idx_cliente_activo ON cliente(activo);
CREATE INDEX IF NOT EXISTS idx_cliente_numero_documento ON cliente(numero_documento);

-- producto: filtros comunes
CREATE INDEX IF NOT EXISTS idx_producto_activo ON producto(activo);

-- auditoria: filtro por usuario
CREATE INDEX IF NOT EXISTS idx_auditoria_usuario_id ON auditoria_usuario(usuario_id);

-- permisos / roles: joins frecuentes
CREATE INDEX IF NOT EXISTS idx_rol_permiso_rol_id ON rol_permiso(rol_id);
CREATE INDEX IF NOT EXISTS idx_usuario_permiso_usuario_id ON usuario_permiso(usuario_id);
CREATE INDEX IF NOT EXISTS idx_usuario_rol_usuario_id ON usuario_rol(usuario_id);

-- cuenta corriente: agrupamientos en reportes
CREATE INDEX IF NOT EXISTS idx_cc_mov_cliente_id ON cliente_cuenta_corriente_mov(cliente_id);
CREATE INDEX IF NOT EXISTS idx_cc_mov_tipo ON cliente_cuenta_corriente_mov(tipo);
