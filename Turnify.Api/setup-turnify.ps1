# Conectar al contenedor SQL y ejecutar comandos
$sql = @"
IF DB_ID('turnify') IS NULL
BEGIN
    CREATE DATABASE turnify;
    PRINT 'Base turnify creada';
END
ELSE
BEGIN
    PRINT 'Base turnify ya existe';
END

ALTER LOGIN sa WITH DEFAULT_DATABASE = turnify;
PRINT 'Login sa configurado para usar turnify como base por defecto';
"@

# Ejecutar sqlcmd apuntando al contenedor
docker exec -i turnify-sql /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "MiPassword123" -Q "$sql"
