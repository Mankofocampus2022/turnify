/* PROYECTO: Turnify API
   AUTOR: Darwin Ocampo (Lupe)
   DESCRIPCIÓN: Script de creación de base de datos organizado por jerarquía.
*/

-- 1. SEGURIDAD Y ACCESO
CREATE TABLE [dbo].[roles] (
    [id] uniqueidentifier NOT NULL PRIMARY KEY, 
    [nombre] nvarchar(60) NOT NULL
);

CREATE TABLE [dbo].[usuarios] (
    [id] uniqueidentifier NOT NULL PRIMARY KEY, 
    [rol_id] uniqueidentifier NOT NULL, 
    [nombre] nvarchar(200) NOT NULL, 
    [email] nvarchar(300) NOT NULL, 
    [password_hash] nvarchar(510) NOT NULL, 
    [activo] bit NOT NULL, 
    [esta_bloqueado] bit NOT NULL,
    [fecha_creacion] datetime2 NOT NULL, 
    [ResetToken] nvarchar(MAX) NULL, 
    [ResetTokenExpires] datetime2 NULL, 
    [suscripcion_fin] datetime NULL, 
    [ultima_conexion] datetime NULL
);

-- 2. ACTORES DEL SISTEMA (PROVEEDORES Y CLIENTES)
CREATE TABLE [dbo].[proveedores] (
    [id] uniqueidentifier NOT NULL PRIMARY KEY, 
    [usuario_id] uniqueidentifier NOT NULL, 
    [nombre_comercial] nvarchar(MAX) NOT NULL, 
    [tipo] nvarchar(MAX) NOT NULL, 
    [descripcion] nvarchar(MAX) NULL, 
    [direccion] nvarchar(MAX) NOT NULL, 
    [ciudad] nvarchar(MAX) NULL, 
    [trabaja_domicilio] bit NOT NULL, 
    [activo] bit NOT NULL, 
    [eliminado] bit NOT NULL, 
    [fecha_creacion] datetime2 NOT NULL, 
    [fecha_actualizacion] datetime2 NULL
);

CREATE TABLE [dbo].[clientes] (
    [id] uniqueidentifier NOT NULL PRIMARY KEY, 
    [usuario_id] uniqueidentifier NOT NULL, 
    [nombre] nvarchar(240) NOT NULL, 
    [telefono] nvarchar(40) NOT NULL, 
    [email] nvarchar(300) NULL, 
    [activo] bit NOT NULL, 
    [fecha_creacion] datetime2 NOT NULL
);

-- 3. OPERACIÓN (SERVICIOS Y HORARIOS)
CREATE TABLE [dbo].[servicios] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY, 
    [ProveedorId] uniqueidentifier NOT NULL,
    [Nombre] nvarchar(200) NOT NULL, 
    [Categoria] nvarchar(100) NOT NULL,
    [Descripcion] nvarchar(510) NULL, 
    [Precio] decimal(18,2) NOT NULL, 
    [ComisionPorcentaje] decimal(5,2) NOT NULL,
    [DuracionMinutos] int NOT NULL, 
    [Activo] int NULL, 
    [FechaCreacion] datetime2 NOT NULL, 
    [ImagenUrl] nvarchar(MAX) NULL
);

CREATE TABLE [dbo].[horarios_atencion] (
    [id] uniqueidentifier NOT NULL PRIMARY KEY, 
    [proveedor_id] uniqueidentifier NOT NULL, 
    [dia_semana] int NOT NULL, -- 0: Domingo, 1: Lunes...
    [hora_apertura] time NOT NULL, 
    [hora_cierre] time NOT NULL
);

-- 4. CITAS Y RESERVAS
CREATE TABLE [dbo].[citas] (
    [id] uniqueidentifier NOT NULL PRIMARY KEY, 
    [cliente_id] uniqueidentifier NOT NULL, 
    [proveedor_id] uniqueidentifier NOT NULL, 
    [servicio_id] uniqueidentifier NOT NULL, 
    [fecha] datetime2 NOT NULL, 
    [hora] time NOT NULL, 
    [modalidad] nvarchar(40) NOT NULL, 
    [direccion] nvarchar(400) NULL, 
    [estado] nvarchar(40) NOT NULL, 
    [precio_pactado] decimal(18,2) NOT NULL, 
    [duracion_pactada_min] int NOT NULL,
    [observaciones] nvarchar(510) NULL, 
    [fecha_creacion] datetime2 NOT NULL
);

-- 5. SUSCRIPCIONES Y PLANES
CREATE TABLE [dbo].[planes_suscripcion] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY, 
    [Nombre] nvarchar(100) NOT NULL, 
    [PrecioMensual] decimal(18,2) NOT NULL, 
    [LimiteCitasMes] int NULL, 
    [Activo] bit NOT NULL
);

CREATE TABLE [dbo].[suscripciones] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY, 
    [ProveedorId] uniqueidentifier NOT NULL, 
    [PlanId] uniqueidentifier NOT NULL, 
    [FechaInicio] datetime2 NOT NULL, 
    [FechaVencimiento] datetime2 NOT NULL, 
    [Estado] nvarchar(MAX) NOT NULL
);