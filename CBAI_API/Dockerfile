
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY CapBot.api/*.csproj ./CapBot.api/
RUN dotnet restore "CapBot.api/CapBot.api.csproj" --disable-parallel --no-cache

# Copy the rest of the source code and publish
COPY . .
WORKDIR "/src/CapBot.api"
RUN dotnet publish "CapBot.api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage (Debian-based for MS ODBC driver support)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install SQL Server ODBC driver (Debian/Ubuntu path)
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl \
        gnupg \
        ca-certificates \
        unixodbc \
        unixodbc-dev \
    && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft.gpg \
    && echo "deb [arch=amd64,arm64 signed-by=/usr/share/keyrings/microsoft.gpg] https://packages.microsoft.com/debian/12/prod bookworm main" > /etc/apt/sources.list.d/microsoft-prod.list \
    && apt-get update && ACCEPT_EULA=Y apt-get install -y --no-install-recommends msodbcsql18 \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create logs directory with proper permissions
RUN mkdir -p /app/Logs && chmod 755 /app/Logs

# Create non-root user for security (Debian)
RUN groupadd --system appuser \
    && useradd --system --gid appuser --create-home --shell /usr/sbin/nologin appuser \
    && chown -R appuser:appuser /app
USER appuser

# Expose port
EXPOSE 7190

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://+:7190

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:7190/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "CapBot.api.dll"]
