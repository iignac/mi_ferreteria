# Secciones Completadas — Carpeta del Proyecto Mi_Ferreteria
> Generado automáticamente desde el código fuente. Copiar al documento principal.

---

## 1.6 Lenguajes de programación y herramientas utilizadas en el proyecto

### Backend
| Tecnología | Versión | Rol |
|---|---|---|
| C# | 13 | Lenguaje principal del backend |
| .NET / ASP.NET Core | 9.0 | Framework web, MVC, middleware, DI |
| Npgsql | 9.x | Driver ADO.NET para PostgreSQL (raw SQL, sin ORM) |
| BCrypt / PBKDF2 | — | Hashing de contraseñas (almacenamiento seguro) |

### Base de Datos
| Tecnología | Versión | Rol |
|---|---|---|
| PostgreSQL | 14+ | Motor de base de datos relacional principal |
| Schema `venta` | — | Schema principal de la aplicación |
| Schema `public` | — | Tablas de usuarios, roles y permisos |

### Frontend
| Tecnología | Versión | Rol |
|---|---|---|
| Razor (.cshtml) | ASP.NET Core | Motor de plantillas del lado servidor |
| Tailwind CSS | 3.x | Framework CSS utilitario (compilado via Node.js) |
| Alpine.js | 3.x | Interactividad declarativa en el cliente (toggles, dropdowns, formularios) |
| Bootstrap | 5.x | Componentes legacy (uso reducido, en migración a Tailwind) |

### Herramientas de Desarrollo
| Herramienta | Uso |
|---|---|
| Visual Studio / VS Code | IDE de desarrollo |
| Node.js + npm | Compilación de Tailwind CSS (`npm run build:css`) |
| Swagger / Swashbuckle | Documentación de API REST (disponible en `/swagger` en entorno Development) |
| Git | Control de versiones |

### Autenticación y Autorización
- **Autenticación:** Cookie-based authentication (ASP.NET Core)
- **Autorización:** Claims-based RBAC con políticas dinámicas generadas desde `PermisosDefinicion`
- **Sin uso de:** Entity Framework, ASP.NET Identity (implementación propia en `Security/`)

---

## 4.4 Diccionario de Datos — Procesos adicionales

> El proceso "Crear Venta" ya está documentado. Se agregan los restantes procesos principales.

---

### DD — Proceso: Autenticación (Login)

#### Flujos y estructuras de datos

**Entrada — Formulario de Login**

| Campo | Tipo | Restricciones | Descripción |
|---|---|---|---|
| Email | string | Requerido, formato email, max 120 chars | Dirección de correo del usuario |
| Password | string | Requerido | Contraseña en texto plano (se compara contra hash) |
| RememberMe | bool | Opcional, default false | Si es true, la cookie persiste más allá de la sesión |
| ReturnUrl | string | Opcional | URL de redirección post-login |

**Proceso interno**

| Paso | Acción | Dato involucrado |
|---|---|---|
| 1 | Buscar usuario por email (case-insensitive) | `usuario.email` |
| 2 | Verificar que el usuario esté activo | `usuario.activo` |
| 3 | Verificar contraseña contra hash almacenado | `usuario.password_hash`, `usuario.password_salt` |
| 4 | Cargar roles del usuario | `usuario_rol`, `rol` |
| 5 | Cargar permisos consolidados (por rol + directos) | `rol_permiso`, `usuario_permiso`, `permiso` |
| 6 | Emitir cookie de autenticación con claims | Claims: `Name`, `Role`, `permiso_*` |
| 7 | Registrar acción en auditoría | `auditoria_usuario` |

**Salida — Cookie de sesión (Claims)**

| Claim | Valor | Fuente |
|---|---|---|
| `ClaimTypes.NameIdentifier` | id del usuario | `usuario.id` |
| `ClaimTypes.Name` | nombre del usuario | `usuario.nombre` |
| `ClaimTypes.Role` | nombre del rol (puede haber múltiples) | `rol.nombre` |
| `permiso_*` | nombre del permiso | `permiso.nombre` |

#### Almacenamientos afectados

| Tabla | Operación | Condición |
|---|---|---|
| `usuario` | SELECT | Siempre (buscar por email) |
| `usuario_rol` | SELECT | Si el usuario existe y está activo |
| `rol` | SELECT | JOIN con usuario_rol |
| `rol_permiso` | SELECT | Para consolidar permisos |
| `usuario_permiso` | SELECT | Para permisos directos adicionales |
| `permiso` | SELECT | JOIN con rol_permiso y usuario_permiso |
| `auditoria_usuario` | INSERT | Al completar el login (éxito o fallo) |

---

### DD — Proceso: Crear Producto

#### Flujos y estructuras de datos

**Entrada — Formulario de Producto**

| Campo | Tipo | Restricciones | Descripción |
|---|---|---|---|
| Sku | string | Requerido, único (case-insensitive), max 32 chars | Código interno del producto |
| Nombre | string | Requerido, max 150 chars | Nombre descriptivo del producto |
| Descripcion | string | Opcional, max 1000 chars | Detalle adicional del producto |
| CategoriaIds | List\<long\> | Mínimo 1, máximo 3 | IDs de categorías asociadas (BR-PROD-CAT-LIMIT) |
| PrecioVentaActual | decimal | Requerido, >= 0, max 9.999.999.999,99 | Precio de venta al público |
| PrecioCostoActual | decimal | Opcional, >= 0 | Precio de costo / compra al proveedor |
| StockMinimo | int | >= 0 | Cantidad mínima antes de alertar stock crítico |
| UnidadMedida | string | Requerido (ej: "unidad", "kg", "mt") | Unidad de medida del producto |
| UbicacionCodigo | string | Opcional, formato: letra + 1-2 dígitos (ej: A1, B12) | Ubicación física en el depósito |
| Activo | bool | Default true | Estado del producto en el sistema |
| Barcodes | List\<string\> | Opcional, máximo 5 (BR-BARCODE-LIMIT) | Códigos de barra asociados |

**Proceso interno**

| Paso | Acción | Dato involucrado |
|---|---|---|
| 1 | Validar unicidad de SKU (case-insensitive) | `producto.sku` |
| 2 | Validar unicidad de cada código de barra | `producto_codigo_barra.codigo_barra` |
| 3 | Validar que las categorías existan | `categoria.id` |
| 4 | Insertar registro en `producto` | Todos los campos del producto |
| 5 | Insertar registros en `producto_codigo_barra` (si los hay) | `producto_id`, `codigo_barra`, `tipo` |
| 6 | Insertar registros en `producto_categoria` (relación M:N) | `producto_id`, `categoria_id` |
| 7 | Registrar precio en historial | `precio_producto_historial` |
| 8 | Registrar acción en auditoría | `auditoria_usuario` |

**Salida**

| Dato | Descripción |
|---|---|
| `producto.id` | ID generado por la base de datos (SERIAL/BIGSERIAL) |
| Redirección | A la vista de detalle o listado del producto creado |
| Mensaje de éxito | TempData["Success"] mostrado en el layout global |

#### Almacenamientos afectados

| Tabla | Operación | Condición |
|---|---|---|
| `producto` | INSERT | Siempre |
| `producto_codigo_barra` | INSERT | Si se proporcionaron códigos de barra |
| `producto_categoria` | INSERT | Siempre (mínimo 1 categoría) |
| `precio_producto_historial` | INSERT | Al crear o actualizar precio |
| `auditoria_usuario` | INSERT | Siempre |

---

### DD — Proceso: Gestionar Stock (Ingreso / Egreso)

#### Flujos y estructuras de datos

**Entrada — Formulario de movimiento de stock**

| Campo | Tipo | Restricciones | Descripción |
|---|---|---|---|
| TipoMovimiento | string | "INGRESO" o "EGRESO" | Dirección del movimiento |
| Motivo | string | Opcional, max 200 chars | Razón del movimiento (compra, ajuste, devolución, etc.) |
| Lineas | List | Mínimo 1 | Lista de productos y cantidades involucradas |
| Lineas[].ProductoId | long | Requerido, debe existir | ID del producto |
| Lineas[].Cantidad | long | Requerido, >= 1 | Unidades a ingresar o egresar |
| Lineas[].PrecioCompra | decimal | Opcional (solo INGRESO), >= 0 | Precio de costo en esta compra |

**Proceso interno**

| Paso | Acción | Dato involucrado |
|---|---|---|
| 1 | Validar que cada producto exista y esté activo | `producto.id`, `producto.activo` |
| 2 | Para EGRESO: verificar stock suficiente (BR-STOCK-NONNEG) | `producto_stock.cantidad` |
| 3 | Actualizar cantidad en `producto_stock` (upsert) | `producto_stock.cantidad` |
| 4 | Insertar movimiento en `producto_stock_mov` | `producto_id`, `tipo`, `cantidad`, `motivo`, `precio_compra` |
| 5 | Para INGRESO con precio_compra: actualizar `precio_costo_actual` del producto | `producto.precio_costo_actual` |
| 6 | Registrar acción en auditoría | `auditoria_usuario` |

**Salida**

| Dato | Descripción |
|---|---|
| Stock actualizado | Nuevo valor de `producto_stock.cantidad` |
| Movimiento registrado | Nuevo registro en `producto_stock_mov` |
| Mensaje de éxito | Confirmación visible en la UI |

#### Almacenamientos afectados

| Tabla | Operación | Condición |
|---|---|---|
| `producto_stock` | INSERT ON CONFLICT UPDATE | Siempre (upsert) |
| `producto_stock_mov` | INSERT | Siempre |
| `producto` | UPDATE (`precio_costo_actual`) | Solo en INGRESO con precio de compra |
| `auditoria_usuario` | INSERT | Siempre |

---

### DD — Proceso: Gestionar Cliente (Crear / Editar)

#### Flujos y estructuras de datos

**Entrada — Formulario de Cliente**

| Campo | Tipo | Restricciones | Descripción |
|---|---|---|---|
| Nombre | string | Requerido, max 120 chars, solo letras | Nombre del cliente |
| Apellido | string | Opcional, max 120 chars (vacío si CUIT) | Apellido (solo para personas físicas) |
| TipoDocumento | string | "DNI" o "CUIT" | Tipo de documento identificatorio |
| NumeroDocumento | string | Opcional, max 20 chars | Número de DNI o CUIT |
| DireccionCalle | string | Opcional, max 120 chars | Calle del domicilio |
| DireccionNumero | string | Opcional, max 10 chars | Número de puerta |
| DireccionLocalidad | string | Opcional, max 120 chars | Ciudad/localidad |
| Telefono | string | Opcional, max 30 chars | Teléfono de contacto |
| Email | string | Opcional, formato email | Correo electrónico |
| TipoCliente | string | "CONSUMIDOR_FINAL" o "CUENTA_CORRIENTE" | Define si puede tener cuenta corriente |
| CuentaCorrienteHabilitada | bool | Default false | Activa la cuenta corriente |
| LimiteCredito | decimal | >= 0 | Monto máximo de deuda permitido (BR-CC-LIMITE) |
| SaldoInicialCuentaCorriente | decimal | >= 0, solo en creación | Saldo a favor inicial (si aplica) |
| Activo | bool | Default true | Estado del cliente |

**Proceso interno — Creación**

| Paso | Acción | Dato involucrado |
|---|---|---|
| 1 | Validar campos obligatorios y formatos | ViewModel annotations |
| 2 | Insertar registro en `cliente` | Todos los campos del formulario |
| 3 | Si hay saldo inicial > 0: registrar movimiento de saldo inicial | `cliente_cuenta_corriente_mov` (tipo PAGO) |
| 4 | Registrar acción en auditoría | `auditoria_usuario` |

#### Almacenamientos afectados

| Tabla | Operación | Condición |
|---|---|---|
| `cliente` | INSERT / UPDATE | Siempre |
| `cliente_cuenta_corriente_mov` | INSERT | Solo si hay saldo inicial > 0 (en creación) |
| `auditoria_usuario` | INSERT | Siempre |

---

### DD — Proceso: Registrar Cobranza (Cuenta Corriente)

#### Flujos y estructuras de datos

**Entrada**

| Campo | Tipo | Restricciones | Descripción |
|---|---|---|---|
| ClienteId | long | Requerido, debe existir y estar activo | ID del cliente con cuenta corriente |
| Monto | decimal | Requerido, > 0 | Importe del pago recibido |
| Descripcion | string | Opcional | Detalle del pago (ej: "Pago parcial factura 123") |
| MovimientoRelacionadoId | long | Opcional | ID del movimiento de deuda al que se imputa |

**Proceso interno**

| Paso | Acción | Dato involucrado |
|---|---|---|
| 1 | Verificar que el cliente tenga cuenta corriente habilitada | `cliente.cuenta_corriente_habilitada` |
| 2 | Registrar movimiento tipo PAGO | `cliente_cuenta_corriente_mov` |
| 3 | Recalcular saldo de cuenta corriente | SUM de movimientos del cliente |
| 4 | Registrar acción en auditoría | `auditoria_usuario` |

#### Almacenamientos afectados

| Tabla | Operación | Condición |
|---|---|---|
| `cliente_cuenta_corriente_mov` | INSERT | Siempre |
| `auditoria_usuario` | INSERT | Siempre |

---

## 5.1 Diseño Físico de Base de Datos

El sistema utiliza PostgreSQL con dos schemas: `public` (usuarios, roles, permisos) y `venta` (el resto de entidades del negocio). Se establece `SET search_path TO venta, public` en las conexiones.

---

### Tabla: `usuario` (schema: public)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | SERIAL | PK | Identificador único del usuario |
| `nombre` | VARCHAR(120) | NOT NULL | Nombre completo del usuario |
| `email` | VARCHAR(120) | NOT NULL, UNIQUE | Email de acceso (case-insensitive) |
| `activo` | BOOLEAN | NOT NULL, DEFAULT true | Estado del usuario |
| `password_hash` | BYTEA | | Hash de la contraseña |
| `password_salt` | BYTEA | | Salt para el hash |

**Índices:** `idx_usuario_email` en `email`

---

### Tabla: `rol` (schema: public)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | SERIAL | PK | Identificador del rol |
| `nombre` | VARCHAR(60) | NOT NULL, UNIQUE | Nombre del rol (ej: Administrador, Vendedor, Stock) |
| `descripcion` | VARCHAR(255) | | Descripción del rol |

---

### Tabla: `permiso` (schema: public)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | SERIAL | PK | Identificador del permiso |
| `nombre` | VARCHAR(80) | NOT NULL, UNIQUE | Nombre del permiso (ej: Ventas.Crear) |
| `descripcion` | VARCHAR(255) | | Descripción legible del permiso |

---

### Tabla: `usuario_rol` (schema: public)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `usuario_id` | INT | PK, FK → usuario(id) ON DELETE CASCADE | ID del usuario |
| `rol_id` | INT | PK, FK → rol(id) ON DELETE CASCADE | ID del rol |

**Índices:** `idx_usuario_rol_usuario_id` en `usuario_id`

---

### Tabla: `rol_permiso` (schema: public)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `rol_id` | INT | PK, FK → rol(id) ON DELETE CASCADE | ID del rol |
| `permiso_id` | INT | PK, FK → permiso(id) ON DELETE CASCADE | ID del permiso |

**Índices:** `idx_rol_permiso_rol_id` en `rol_id`

---

### Tabla: `usuario_permiso` (schema: public)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `usuario_id` | INT | PK, FK → usuario(id) ON DELETE CASCADE | ID del usuario |
| `permiso_id` | INT | PK, FK → permiso(id) ON DELETE CASCADE | ID del permiso directo |

**Índices:** `idx_usuario_permiso_usuario_id` en `usuario_id`

---

### Tabla: `auditoria_usuario` (schema: public)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del registro |
| `fecha` | TIMESTAMPTZ | NOT NULL, DEFAULT now() | Fecha y hora de la acción |
| `usuario_id` | INT | FK → usuario(id) | ID del usuario que realizó la acción |
| `usuario_nombre` | VARCHAR(120) | NOT NULL | Nombre del usuario (desnormalizado para historial) |
| `accion` | VARCHAR(255) | NOT NULL | Acción realizada (módulo + operación) |
| `detalle` | TEXT | | Detalle adicional de la acción |

**Índices:** `idx_auditoria_usuario_id` en `usuario_id`

---

### Tabla: `categoria` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador de la categoría |
| `nombre` | VARCHAR(100) | NOT NULL, UNIQUE | Nombre de la categoría |
| `descripcion` | TEXT | | Descripción opcional |
| `id_padre` | BIGINT | FK → categoria(id) | Categoría padre (jerarquía, opcional) |
| `activo` | BOOLEAN | NOT NULL, DEFAULT true | Estado de la categoría |

---

### Tabla: `producto` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del producto |
| `sku` | VARCHAR(32) | NOT NULL, UNIQUE | Código único del producto (BR-UNQ-SKU) |
| `nombre` | VARCHAR(150) | NOT NULL | Nombre descriptivo del producto |
| `descripcion` | TEXT | | Descripción opcional |
| `categoria_id` | BIGINT | FK → categoria(id) | Categoría principal (legacy, ver producto_categoria) |
| `precio_venta_actual` | NUMERIC(14,2) | NOT NULL, DEFAULT 0 | Precio de venta vigente (BR-PRICE-NONNEG) |
| `precio_costo_actual` | NUMERIC(14,2) | | Precio de costo/compra vigente |
| `stock_minimo` | INT | NOT NULL, DEFAULT 0 | Umbral de alerta de stock bajo (BR-STOCK-NONNEG) |
| `unidad_medida` | VARCHAR(30) | NOT NULL, DEFAULT 'unidad' | Unidad de medida del producto |
| `activo` | BOOLEAN | NOT NULL, DEFAULT true | Estado del producto en el catálogo |
| `ubicacion_preferida_id` | BIGINT | | ID de ubicación física (referencia interna) |
| `ubicacion_codigo` | VARCHAR(10) | Formato: letra + 1-2 dígitos | Código de ubicación en depósito (ej: A1, B12) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT now() | Fecha de creación del registro |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT now() | Fecha de última modificación |

**Índices:** `idx_producto_activo` en `activo`

---

### Tabla: `producto_categoria` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `producto_id` | BIGINT | PK, FK → producto(id) ON DELETE CASCADE | ID del producto |
| `categoria_id` | BIGINT | PK, FK → categoria(id) ON DELETE CASCADE | ID de la categoría |

> Relación M:N. Un producto puede tener hasta 3 categorías (BR-PROD-CAT-LIMIT).

---

### Tabla: `producto_codigo_barra` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del código de barra |
| `producto_id` | BIGINT | NOT NULL, FK → producto(id) ON DELETE CASCADE | ID del producto propietario |
| `codigo_barra` | VARCHAR(60) | NOT NULL, UNIQUE | Código de barra (EAN-13, QR, etc.) |
| `tipo` | VARCHAR(20) | | Tipo de código (EAN13, QR, etc.) |

> Un producto puede tener hasta 5 códigos de barra (BR-BARCODE-LIMIT).

---

### Tabla: `producto_stock` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `producto_id` | BIGINT | PK, FK → producto(id) ON DELETE CASCADE | ID del producto |
| `cantidad` | BIGINT | NOT NULL, DEFAULT 0 | Stock disponible actual |

> Una fila por producto. Se actualiza mediante upsert en cada movimiento.

---

### Tabla: `producto_stock_mov` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del movimiento |
| `fecha` | TIMESTAMPTZ | NOT NULL, DEFAULT now() | Fecha y hora del movimiento |
| `producto_id` | BIGINT | NOT NULL, FK → producto(id) | ID del producto |
| `tipo` | VARCHAR(10) | NOT NULL ('INGRESO' / 'EGRESO') | Dirección del movimiento |
| `cantidad` | BIGINT | NOT NULL | Unidades movidas |
| `motivo` | VARCHAR(200) | | Razón del movimiento |
| `precio_compra` | NUMERIC(14,2) | | Precio de costo en este ingreso (solo INGRESO) |

---

### Tabla: `precio_producto_historial` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del registro |
| `producto_id` | BIGINT | NOT NULL, FK → producto(id) | ID del producto |
| `fecha_vigencia_desde` | DATE | NOT NULL | Fecha desde la que aplica el precio |
| `precio` | NUMERIC(14,2) | NOT NULL | Precio de venta en ese período |

---

### Tabla: `cliente` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del cliente |
| `nombre` | VARCHAR(120) | NOT NULL | Nombre del cliente |
| `apellido` | VARCHAR(120) | | Apellido (vacío para personas jurídicas/CUIT) |
| `tipo_documento` | VARCHAR(10) | 'DNI' / 'CUIT' | Tipo de documento |
| `numero_documento` | VARCHAR(20) | | Número de DNI o CUIT |
| `direccion` | VARCHAR(300) | | Dirección completa |
| `telefono` | VARCHAR(30) | | Teléfono de contacto |
| `email` | VARCHAR(120) | | Correo electrónico |
| `tipo_cliente` | VARCHAR(30) | DEFAULT 'CONSUMIDOR_FINAL' | 'CONSUMIDOR_FINAL' / 'CUENTA_CORRIENTE' |
| `cuenta_corriente_habilitada` | BOOLEAN | NOT NULL, DEFAULT false | Si puede comprar a crédito |
| `limite_credito` | NUMERIC(14,2) | NOT NULL, DEFAULT 0 | Límite máximo de deuda permitido |
| `activo` | BOOLEAN | NOT NULL, DEFAULT true | Estado del cliente |
| `fecha_alta` | TIMESTAMPTZ | NOT NULL, DEFAULT now() | Fecha de registro |

**Índices:** `idx_cliente_activo`, `idx_cliente_numero_documento`

---

### Tabla: `cliente_cuenta_corriente_mov` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del movimiento |
| `cliente_id` | BIGINT | NOT NULL, FK → cliente(id) | ID del cliente |
| `venta_id` | BIGINT | FK → venta(id) | Venta asociada (si aplica) |
| `fecha` | TIMESTAMPTZ | NOT NULL, DEFAULT now() | Fecha del movimiento |
| `fecha_vencimiento` | TIMESTAMPTZ | | Fecha de vencimiento (para deudas) |
| `movimiento_relacionado_id` | BIGINT | FK → cliente_cuenta_corriente_mov(id) | Movimiento al que se imputa (ej: pago contra deuda) |
| `tipo` | VARCHAR(20) | NOT NULL | 'DEUDA' / 'PAGO' / 'AJUSTE' / 'NOTA_DEBITO' / 'NOTA_CREDITO' / 'CONSUMO_SALDO' |
| `monto` | NUMERIC(14,2) | NOT NULL | Importe del movimiento |
| `descripcion` | TEXT | | Detalle del movimiento |
| `usuario_id` | INT | FK → usuario(id) | Usuario que registró el movimiento |

**Índices:** `idx_cc_mov_cliente_id`, `idx_cc_mov_tipo`

---

### Tabla: `venta` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador de la venta |
| `fecha` | TIMESTAMPTZ | NOT NULL, DEFAULT now() | Fecha y hora de la venta |
| `cliente_id` | BIGINT | FK → cliente(id) | ID del cliente (NULL si consumidor final) |
| `tipo_cliente` | VARCHAR(20) | NOT NULL | 'CONSUMIDOR_FINAL' / 'REGISTRADO' |
| `tipo_pago` | VARCHAR(20) | NOT NULL | 'CONTADO' / 'CUENTA_CORRIENTE' |
| `total` | NUMERIC(14,2) | NOT NULL | Total de la venta |
| `total_en_letras` | VARCHAR(500) | | Total expresado en texto (para comprobante) |
| `usuario_id` | INT | NOT NULL, FK → usuario(id) | Usuario que registró la venta |
| `estado` | VARCHAR(30) | NOT NULL | 'CONFIRMADA' / 'ANULADA' / 'PENDIENTE_AUTORIZACION' / 'RECHAZADA' |
| `observaciones` | TEXT | | Notas adicionales (ej: motivo de anulación) |

**Índices:** `idx_venta_estado`, `idx_venta_fecha`, `idx_venta_cliente_id`

---

### Tabla: `venta_detalle` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del ítem |
| `venta_id` | BIGINT | NOT NULL, FK → venta(id) ON DELETE CASCADE | Venta a la que pertenece |
| `producto_id` | BIGINT | NOT NULL, FK → producto(id) | Producto vendido |
| `descripcion` | VARCHAR(200) | NOT NULL | Nombre del producto al momento de la venta |
| `cantidad` | NUMERIC(12,3) | NOT NULL | Cantidad vendida |
| `precio_unitario` | NUMERIC(14,2) | NOT NULL | Precio unitario al momento de la venta |
| `subtotal` | NUMERIC(14,2) | NOT NULL | cantidad × precio_unitario |
| `permite_venta_sin_stock` | BOOLEAN | NOT NULL, DEFAULT false | Si se autorizó la venta con stock insuficiente |

**Índices:** `idx_venta_detalle_venta_id`

---

### Tabla: `venta_pago` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del pago |
| `venta_id` | BIGINT | NOT NULL, FK → venta(id) ON DELETE CASCADE | Venta asociada |
| `tipo` | VARCHAR(20) | NOT NULL | 'EFECTIVO' / 'TARJETA' / 'TRANSFERENCIA' / 'CUENTA_CORRIENTE' |
| `monto` | NUMERIC(14,2) | NOT NULL | Importe del pago |
| `detalle` | VARCHAR(200) | | Información adicional (ej: últimos 4 dígitos de tarjeta) |

**Índices:** `idx_venta_pago_venta_id`

---

### Tabla: `venta_auditoria` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador del registro |
| `venta_id` | BIGINT | NOT NULL, FK → venta(id) | Venta asociada |
| `usuario_id` | INT | NOT NULL, FK → usuario(id) | Usuario que realizó la acción |
| `fecha` | TIMESTAMPTZ | NOT NULL, DEFAULT now() | Fecha y hora de la acción |
| `accion` | VARCHAR(50) | NOT NULL | 'CREACION' / 'ANULACION' / 'AUTORIZACION_SOBRE_LIMITE' / 'OTRO' |
| `detalle` | TEXT | | Información adicional de la acción |

---

### Tabla: `factura` (schema: venta)

| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| `id` | BIGSERIAL | PK | Identificador de la factura |
| `venta_id` | BIGINT | NOT NULL, UNIQUE, FK → venta(id) | Venta a la que pertenece (1:1) |
| `tipo_comprobante` | VARCHAR(20) | NOT NULL, DEFAULT 'FACTURA_B' | Tipo de comprobante fiscal |
| `punto_venta` | INT | NOT NULL, DEFAULT 1 | Punto de venta |
| `numero` | BIGINT | NOT NULL | Número correlativo del comprobante |
| `fecha_emision` | TIMESTAMPTZ | NOT NULL | Fecha de emisión del comprobante |
| `total` | NUMERIC(14,2) | NOT NULL | Total de la factura |
| `total_en_letras` | VARCHAR(500) | | Total en texto (para impresión) |
| `cliente_nombre` | VARCHAR(255) | NOT NULL | Nombre del cliente (desnormalizado) |
| `cliente_documento` | VARCHAR(30) | | Documento del cliente (desnormalizado) |
| `cliente_direccion` | VARCHAR(300) | | Dirección del cliente (desnormalizado) |

**Índices:** `idx_factura_venta_id`

---

### Relaciones entre tablas — Resumen

```
usuario ─────────────────────────┐
  │ (usuario_rol)                 │
  ▼                               │
rol ──── (rol_permiso) ──── permiso
  │
  │ (usuario_permiso) ──── permiso [permisos directos]

usuario ──────────────────────── venta ──── venta_detalle ──── producto
                                        ├── venta_pago
                                        ├── factura
                                        └── venta_auditoria

producto ──── producto_categoria ──── categoria
         ├─── producto_codigo_barra
         ├─── producto_stock
         └─── producto_stock_mov

cliente ──── venta
        └─── cliente_cuenta_corriente_mov
```

---

## 5.2 Diseño de Clases

El sistema sigue una arquitectura en capas con separación explícita de responsabilidades. A continuación se describe cada capa con sus clases principales.

---

### Capa: Controllers (Controladores)

Reciben las peticiones HTTP, orquestan la lógica llamando a los repositorios y retornan vistas o redirecciones. Son delgados: no contienen lógica de negocio ni SQL.

| Controlador | Responsabilidad principal |
|---|---|
| `AuthController` | Login, logout, acceso denegado |
| `HomeController` | Página de inicio |
| `AdminController` | Dashboard, finanzas, ventas pendientes, auditoría, reportes |
| `UsuarioController` | ABM de usuarios, asignación de roles y permisos |
| `CategoriaController` | ABM de categorías |
| `ProductoController` | ABM de productos, importación de catálogos |
| `StockController` | Movimientos de stock, listado de críticos |
| `VentaController` | Creación de ventas, historial, comprobantes |
| `ClienteController` | ABM de clientes, cuenta corriente, cobranzas |
| `ProductoApiController` | API REST de productos (JSON) |
| `UsuarioApiController` | API REST de usuarios (JSON) |

---

### Capa: Models (Modelos de dominio)

Representan las entidades del negocio tal como se almacenan en la base de datos. No contienen lógica de presentación.

| Clase | Descripción |
|---|---|
| `Usuario` | Usuario del sistema con roles y hash de contraseña |
| `Rol` | Rol de acceso (Administrador, Vendedor, Stock) con lista de permisos |
| `Permiso` | Permiso individual con nombre y descripción |
| `Categoria` | Categoría de productos con soporte de jerarquía (id_padre) |
| `Producto` | Producto del catálogo con precio, stock mínimo y ubicación |
| `ProductoCodigoBarra` | Código de barra asociado a un producto |
| `ProductoStockCritico` | Proyección de productos con stock por debajo del mínimo |
| `StockMovimiento` | Registro de ingreso o egreso de stock |
| `Cliente` | Cliente con datos personales y configuración de cuenta corriente |
| `ClienteCuentaCorrienteMovimiento` | Movimiento de cuenta corriente (deuda, pago, ajuste) |
| `ClienteCuentaCorrienteFacturaPendiente` | Factura pendiente de cobro de un cliente |
| `Venta` | Cabecera de una venta con estado y tipo de pago |
| `VentaDetalle` | Línea de producto dentro de una venta |
| `VentaPago` | Registro del medio de pago utilizado en una venta |
| `VentaAuditoria` | Auditoría específica de operaciones sobre ventas |
| `Factura` | Comprobante fiscal generado a partir de una venta |
| `AuditoriaRegistro` | Registro de auditoría general de acciones del sistema |
| `ReporteFinancieroResumen` | Resumen financiero con totales, top productos y top clientes |
| `ReporteFinancieroTopProducto` | Producto más vendido en un período |
| `ReporteFinancieroTopCliente` | Cliente con mayor volumen de compras |
| `ReporteFinancieroDeudor` | Cliente con saldo pendiente en cuenta corriente |

---

### Capa: ViewModels (Modelos de vista)

Adaptan los datos para cada vista específica. Separan la representación del dominio del modelo de presentación. Contienen anotaciones de validación.

| Clase | Vista asociada | Datos que combina |
|---|---|---|
| `LoginViewModel` | Auth/Login | Credenciales de acceso |
| `UsuarioFormViewModel` | Usuario/Crear, Usuario/Editar | Usuario + roles disponibles + permisos consolidados |
| `UsuarioViewModel` | Usuario/Index | Datos básicos del usuario para listado |
| `UsuarioRolViewModel` | — | Asignación de roles a un usuario |
| `RolPermisoViewModel` | — | Asignación de permisos a un rol |
| `ProductoFormViewModel` | Producto/Crear, Producto/Editar | Producto + categorías disponibles + códigos de barra |
| `StockCargaViewModel` | Stock/Carga | Lista de líneas de movimiento |
| `StockCargaLineaViewModel` | — | Producto + cantidad + precio de compra |
| `StockCriticosViewModel` | Stock/Criticos | Lista paginada de productos con stock bajo |
| `StockCriticoViewModel` | — | Datos mínimos de un producto crítico |
| `ClienteCreateViewModel` | Cliente/Crear, Cliente/Editar | Datos del cliente + configuración de cuenta corriente |
| `ClienteCuentaCorrienteViewModel` | Cliente/CuentaCorriente | Cliente + movimientos + facturas pendientes + saldo |
| `ClienteMovimientoComprobanteViewModel` | Cliente/Comprobante | Cliente + movimiento específico |
| `VentaCrearViewModel` | Venta/Crear | Líneas del carrito + selección de cliente + tipo de pago |
| `VentaLineaViewModel` | — | Producto + cantidad + precio + stock disponible |
| `VentaComprobanteViewModel` | Venta/Comprobante | Venta + detalles + factura |
| `VentaPendienteItemViewModel` | Admin/VentasPendientes | Venta + cliente + saldo actual y proyectado |
| `AdminDashboardViewModel` | Admin/Dashboard | Estadísticas del sistema + últimas operaciones |
| `FinanzasDashboardViewModel` | Admin/Finanzas | Resumen financiero con filtros aplicados |
| `AuditoriaListadoViewModel` | Admin/Auditoria | Registros de auditoría paginados con filtros |
| `ImportarProductosViewModel` | Producto/Importar | Archivo CSV/Excel para importación |
| `ImportarProductosResultadoViewModel` | Producto/ImportarResultado | Resultado fila por fila de la importación |

---

### Capa: Data / Repositorios

Implementan el acceso a datos con SQL raw via Npgsql. Cada entidad tiene una interfaz y una implementación concreta.

| Interfaz | Implementación | Entidad |
|---|---|---|
| `IUsuarioRepository` | `UsuarioRepository` | usuario, usuario_rol |
| `IRolRepository` | `RolRepository` | rol |
| `IPermisoRepository` | `PermisoRepository` | permiso, rol_permiso, usuario_permiso |
| `ICategoriaRepository` | `CategoriaRepository` | categoria |
| `IProductoRepository` | `ProductoRepository` | producto, producto_categoria, producto_codigo_barra, precio_producto_historial |
| `IStockRepository` | `StockRepository` | producto_stock, producto_stock_mov |
| `IClienteRepository` | `ClienteRepository` | cliente, cliente_cuenta_corriente_mov |
| `IVentaRepository` | `VentaRepository` | venta, venta_detalle, venta_pago, venta_auditoria, factura |
| `IAuditoriaRepository` | `AuditoriaRepository` | auditoria_usuario |
| `IReporteFinancieroRepository` | `ReporteFinancieroRepository` | (consultas agregadas sobre venta, producto, cliente) |

Todos los repositorios se registran como **Transient** en `Program.cs` y se inyectan en los controladores via constructor.

---

### Capa: Security (Seguridad)

| Clase | Descripción |
|---|---|
| `AuthService` / `IAuthService` | Login, logout, validación de credenciales, emisión de claims |
| `PermisosDefinicion` | Constantes con los 18 nombres de permisos del sistema |
| Filtros de autorización | Políticas dinámicas generadas en `Program.cs` para cada permiso |
| `AuditoriaActionFilter` | Action filter global que registra cada acción ejecutada en `auditoria_usuario` |

---

### Capa: Helpers y DTOs

| Clase | Descripción |
|---|---|
| `Helpers/` | Utilidades compartidas (formateo de números en letras, helpers de paginación, etc.) |
| `Dtos/` | Objetos de transferencia de datos para respuestas de la API REST |
