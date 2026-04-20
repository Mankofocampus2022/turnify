# ==========================================
# ETAPA 1: Compilación y Preparación (Build)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiamos los archivos de proyecto primero para aprovechar el caché de capas de Docker
COPY *.csproj ./
RUN dotnet restore

# Copiamos todo el código fuente, incluyendo la carpeta frontend/dist
COPY . ./

# Publicamos el API. OJO: Esto genera los binarios en la carpeta /app/out
RUN dotnet publish -c Release -o out

# ==========================================
# ETAPA 2: Entorno de Ejecución (Runtime)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 1. Copiamos los binarios publicados del API
COPY --from=build /app/out .

# 2. 🛡️ EL BLINDAJE: Copiamos explícitamente la carpeta del frontend.
# Sin esta línea, el servidor de .NET no encuentra el login.html y tira 404.
# Copiamos desde la etapa 'build' hacia la carpeta 'frontend' del contenedor final.
COPY --from=build /app/frontend ./frontend

# Exponemos el puerto en el que corre el contenedor (8080 por defecto en .NET 8)
EXPOSE 8080

# Comando de arranque del sistema
ENTRYPOINT ["dotnet", "Turnify.Api.dll"]