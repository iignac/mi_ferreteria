-- Migración: Agrega columna precio_costo_actual a la tabla producto
ALTER TABLE venta.producto
    ADD COLUMN IF NOT EXISTS precio_costo_actual NUMERIC(14,2) NULL;

COMMENT ON COLUMN venta.producto.precio_costo_actual IS 'Precio de costo/compra actual del producto, actualizable desde lista de precios de proveedores';
