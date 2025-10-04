# ===========================================
# CapBot Docker Management Scripts (PowerShell)
# ===========================================

# Function to print colored output
function Write-Status {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Header {
    param([string]$Message)
    Write-Host "===========================================" -ForegroundColor Blue
    Write-Host $Message -ForegroundColor Blue
    Write-Host "===========================================" -ForegroundColor Blue
}

# Function to check if Docker is running
function Test-Docker {
    try {
        docker info | Out-Null
        return $true
    }
    catch {
        Write-Error "Docker is not running. Please start Docker Desktop."
        exit 1
    }
}

# Function to check if .env file exists
function Test-EnvFile {
    if (-not (Test-Path ".env")) {
        Write-Warning ".env file not found. Creating from template..."
        Copy-Item "docker-compose.env" ".env"
        Write-Status "Please edit .env file with your actual API keys and credentials."
        exit 1
    }
}

# Start all services
function Start-AllServices {
    Write-Header "Starting CapBot Full Stack"
    Test-Docker
    Test-EnvFile
    
    Write-Status "Starting all services..."
    docker-compose --env-file .env up -d
    
    Write-Status "Waiting for services to be ready..."
    Start-Sleep -Seconds 10
    
    Write-Status "Checking service status..."
    docker-compose ps
    
    Write-Status "Services started successfully!"
    Write-Status "Python Agent: http://localhost:8000"
    Write-Status ".NET API: http://localhost:7190"
    Write-Status "SQL Server: localhost:1433"
    Write-Status "Elasticsearch: http://localhost:9200"
}

# Stop all services
function Stop-AllServices {
    Write-Header "Stopping CapBot Full Stack"
    Test-Docker
    
    Write-Status "Stopping all services..."
    docker-compose down
    
    Write-Status "All services stopped."
}

# Restart all services
function Restart-AllServices {
    Write-Header "Restarting CapBot Full Stack"
    Test-Docker
    Test-EnvFile
    
    Write-Status "Restarting all services..."
    docker-compose --env-file .env restart
    
    Write-Status "Services restarted successfully!"
}

# Rebuild and start
function Rebuild-AllServices {
    Write-Header "Rebuilding CapBot Full Stack"
    Test-Docker
    Test-EnvFile
    
    Write-Status "Stopping services..."
    docker-compose down
    
    Write-Status "Rebuilding images..."
    docker-compose --env-file .env build --no-cache
    
    Write-Status "Starting services..."
    docker-compose --env-file .env up -d
    
    Write-Status "Rebuild completed successfully!"
}

# Show logs
function Show-Logs {
    param([string]$ServiceName = "")
    
    Write-Header "CapBot Service Logs"
    Test-Docker
    
    if ($ServiceName) {
        Write-Status "Showing logs for service: $ServiceName"
        docker-compose logs -f $ServiceName
    }
    else {
        Write-Status "Showing logs for all services..."
        docker-compose logs -f
    }
}

# Check service health
function Test-ServiceHealth {
    Write-Header "CapBot Service Health Check"
    Test-Docker
    
    Write-Status "Checking service status..."
    docker-compose ps
    
    Write-Status "Checking Python Agent health..."
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8000/health" -TimeoutSec 5
        Write-Status "✅ Python Agent is healthy"
    }
    catch {
        Write-Error "❌ Python Agent is not responding"
    }
    
    Write-Status "Checking .NET API health..."
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:7190/health" -TimeoutSec 5
        Write-Status "✅ .NET API is healthy"
    }
    catch {
        Write-Error "❌ .NET API is not responding"
    }
    
    Write-Status "Checking Elasticsearch health..."
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:9200/_cluster/health" -TimeoutSec 5
        Write-Status "✅ Elasticsearch is healthy"
    }
    catch {
        Write-Error "❌ Elasticsearch is not responding"
    }
}

# Clean up Docker resources
function Clear-DockerResources {
    Write-Header "Cleaning Up Docker Resources"
    Test-Docker
    
    $response = Read-Host "This will remove all unused Docker resources. Continue? (y/N)"
    if ($response -match "^[yY]([eE][sS])?$") {
        Write-Status "Cleaning up Docker resources..."
        docker system prune -a --volumes -f
        Write-Status "Cleanup completed!"
    }
    else {
        Write-Status "Cleanup cancelled."
    }
}

# Reset everything
function Reset-AllServices {
    Write-Header "Resetting CapBot Full Stack"
    Test-Docker
    
    $response = Read-Host "This will remove ALL data and containers. Continue? (y/N)"
    if ($response -match "^[yY]([eE][sS])?$") {
        Write-Status "Stopping and removing all services..."
        docker-compose down -v
        
        Write-Status "Removing all images..."
        $images = docker images "capbot*" -q
        if ($images) {
            docker rmi $images
        }
        
        Write-Status "Cleaning up Docker resources..."
        docker system prune -a --volumes -f
        
        Write-Status "Reset completed!"
    }
    else {
        Write-Status "Reset cancelled."
    }
}

# Show help
function Show-Help {
    Write-Header "CapBot Docker Management Scripts"
    Write-Host "Usage: .\docker-scripts.ps1 [COMMAND]"
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  start       Start all services"
    Write-Host "  stop        Stop all services"
    Write-Host "  restart     Restart all services"
    Write-Host "  rebuild     Rebuild and start all services"
    Write-Host "  logs        Show logs (optionally specify service name)"
    Write-Host "  health      Check service health"
    Write-Host "  cleanup     Clean up unused Docker resources"
    Write-Host "  reset       Reset everything (WARNING: removes all data)"
    Write-Host "  help        Show this help message"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\docker-scripts.ps1 start"
    Write-Host "  .\docker-scripts.ps1 logs capbot-agent"
    Write-Host "  .\docker-scripts.ps1 health"
}

# Main script logic
if ($args.Count -eq 0) {
    $Command = "help"
} else {
    $Command = $args[0]
}

switch ($Command.ToLower()) {
    "start" {
        Start-AllServices
    }
    "stop" {
        Stop-AllServices
    }
    "restart" {
        Restart-AllServices
    }
    "rebuild" {
        Rebuild-AllServices
    }
    "logs" {
        Show-Logs $args[0]
    }
    "health" {
        Test-ServiceHealth
    }
    "cleanup" {
        Clear-DockerResources
    }
    "reset" {
        Reset-AllServices
    }
    "help" {
        Show-Help
    }
    default {
        Write-Error "Unknown command: $Command"
        Show-Help
        exit 1
    }
}
