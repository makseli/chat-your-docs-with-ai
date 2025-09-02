# Ana dizindeki Dockerfile - backend-api için
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Proje dosyasını kopyala ve restore işlemini yap
COPY ["backend-api/backend-api.csproj", "backend-api/"]
RUN dotnet restore "backend-api/backend-api.csproj"

# Tüm kaynak kodları kopyala
COPY backend-api/ backend-api/

# Uygulamayı build et ve publish et
WORKDIR "/src/backend-api"
RUN dotnet build "backend-api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "backend-api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image'ı oluştur
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Non-root user oluştur (güvenlik için) - Host sistemle uyumlu ID
RUN addgroup --system --gid 1000 dotnetgroup
RUN adduser --system --uid 1000 --ingroup dotnetgroup dotnetuser

# Publish edilmiş dosyaları kopyala
COPY --from=publish /app/publish .

# Gerekli dizinleri oluştur ve izinleri ayarla
RUN mkdir -p /app/data/uploaded_files /app/data/vectors && \
    chown -R dotnetuser:dotnetgroup /app && \
    chmod -R 755 /app && \
    chmod 755 /app/data && \
    chmod 755 /app/data/uploaded_files && \
    chmod 755 /app/data/vectors

# User'ı değiştir
USER dotnetuser

# Port'u expose et
EXPOSE 8080

# Health check ekle
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

# Uygulamayı başlat
ENTRYPOINT ["dotnet", "backend-api.dll"]
