#!/bin/bash
# Crear la ruta en el Home
mkdir -p /home/darwin/mssql_turnify_data
# Dar permisos al UID de SQL Server
sudo chown -R 10001:0 /home/darwin/mssql_turnify_data
sudo chmod -R 770 /home/darwin/mssql_turnify_data
echo "Entorno de Turnify reforzado ✅"
