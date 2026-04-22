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

# 2. 🛡️ EL BLINDAJE: Copiamos el CONTENIDO de dist directamente a frontend.
# Modificamos la ruta de origen para que login.html no quede "escondido" en frontend/dist/
# Ahora quedará directamente en /app/frontend/
COPY --from=build /app/frontend/dist ./frontend

# Exponemos el puerto en el que corre el contenedor (8080 por defecto en .NET 8)
EXPOSE 8080

# Comando de arranque del sistema
ENTRYPOINT ["dotnet", "Turnify.Api.dll"]