@echo off
echo Generando backup en SQL Server...
docker exec -it turnify-sql-final /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Turnify.2026.SQL! -C -Q "BACKUP DATABASE [turnify] TO DISK = N'/var/opt/mssql/data/turnify_backup.bak' WITH INIT"

echo Copiando backup a Windows...
docker cp turnify-sql-final:/var/opt/mssql/data/turnify_backup.bak ./backup_final_%date:~-4,4%%date:~-7,2%%date:~-10,2%.bak

echo ¡Respaldo completado con éxito!
pause