#!/bin/bash

# ===========================================
# CapBot Docker Management Scripts
# ===========================================

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo -e "${BLUE}===========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}===========================================${NC}"
}

# Function to check if Docker is running
check_docker() {
    if ! docker info > /dev/null 2>&1; then
        print_error "Docker is not running. Please start Docker Desktop."
        exit 1
    fi
}

# Function to check if .env file exists
check_env() {
    if [ ! -f ".env" ]; then
        print_warning ".env file not found. Creating from template..."
        cp docker-compose.env .env
        print_status "Please edit .env file with your actual API keys and credentials."
        exit 1
    fi
}

# Start all services
start_all() {
    print_header "Starting CapBot Full Stack"
    check_docker
    check_env
    
    print_status "Starting all services..."
    docker-compose --env-file .env up -d
    
    print_status "Waiting for services to be ready..."
    sleep 10
    
    print_status "Checking service status..."
    docker-compose ps
    
    print_status "Services started successfully!"
    print_status "Python Agent: http://localhost:8000"
    print_status ".NET API: http://localhost:7190"
    print_status "SQL Server: localhost:1433"
    print_status "Elasticsearch: http://localhost:9200"
}

# Stop all services
stop_all() {
    print_header "Stopping CapBot Full Stack"
    check_docker
    
    print_status "Stopping all services..."
    docker-compose down
    
    print_status "All services stopped."
}

# Restart all services
restart_all() {
    print_header "Restarting CapBot Full Stack"
    check_docker
    check_env
    
    print_status "Restarting all services..."
    docker-compose --env-file .env restart
    
    print_status "Services restarted successfully!"
}

# Rebuild and start
rebuild_all() {
    print_header "Rebuilding CapBot Full Stack"
    check_docker
    check_env
    
    print_status "Stopping services..."
    docker-compose down
    
    print_status "Rebuilding images..."
    docker-compose --env-file .env build --no-cache
    
    print_status "Starting services..."
    docker-compose --env-file .env up -d
    
    print_status "Rebuild completed successfully!"
}

# Show logs
show_logs() {
    print_header "CapBot Service Logs"
    check_docker
    
    if [ -n "$1" ]; then
        print_status "Showing logs for service: $1"
        docker-compose logs -f "$1"
    else
        print_status "Showing logs for all services..."
        docker-compose logs -f
    fi
}

# Check service health
check_health() {
    print_header "CapBot Service Health Check"
    check_docker
    
    print_status "Checking service status..."
    docker-compose ps
    
    print_status "Checking Python Agent health..."
    if curl -f http://localhost:8000/health > /dev/null 2>&1; then
        print_status "✅ Python Agent is healthy"
    else
        print_error "❌ Python Agent is not responding"
    fi
    
    print_status "Checking .NET API health..."
    if curl -f http://localhost:7190/health > /dev/null 2>&1; then
        print_status "✅ .NET API is healthy"
    else
        print_error "❌ .NET API is not responding"
    fi
    
    print_status "Checking Elasticsearch health..."
    if curl -f http://localhost:9200/_cluster/health > /dev/null 2>&1; then
        print_status "✅ Elasticsearch is healthy"
    else
        print_error "❌ Elasticsearch is not responding"
    fi
}

# Clean up Docker resources
cleanup() {
    print_header "Cleaning Up Docker Resources"
    check_docker
    
    print_warning "This will remove all unused Docker resources. Continue? (y/N)"
    read -r response
    if [[ "$response" =~ ^([yY][eE][sS]|[yY])$ ]]; then
        print_status "Cleaning up Docker resources..."
        docker system prune -a --volumes -f
        print_status "Cleanup completed!"
    else
        print_status "Cleanup cancelled."
    fi
}

# Reset everything
reset_all() {
    print_header "Resetting CapBot Full Stack"
    check_docker
    
    print_warning "This will remove ALL data and containers. Continue? (y/N)"
    read -r response
    if [[ "$response" =~ ^([yY][eE][sS]|[yY])$ ]]; then
        print_status "Stopping and removing all services..."
        docker-compose down -v
        
        print_status "Removing all images..."
        docker rmi $(docker images "capbot*" -q) 2>/dev/null || true
        
        print_status "Cleaning up Docker resources..."
        docker system prune -a --volumes -f
        
        print_status "Reset completed!"
    else
        print_status "Reset cancelled."
    fi
}

# Show help
show_help() {
    print_header "CapBot Docker Management Scripts"
    echo "Usage: $0 [COMMAND]"
    echo ""
    echo "Commands:"
    echo "  start       Start all services"
    echo "  stop        Stop all services"
    echo "  restart     Restart all services"
    echo "  rebuild     Rebuild and start all services"
    echo "  logs        Show logs (optionally specify service name)"
    echo "  health      Check service health"
    echo "  cleanup     Clean up unused Docker resources"
    echo "  reset       Reset everything (WARNING: removes all data)"
    echo "  help        Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 start"
    echo "  $0 logs capbot-agent"
    echo "  $0 health"
}

# Main script logic
case "$1" in
    start)
        start_all
        ;;
    stop)
        stop_all
        ;;
    restart)
        restart_all
        ;;
    rebuild)
        rebuild_all
        ;;
    logs)
        show_logs "$2"
        ;;
    health)
        check_health
        ;;
    cleanup)
        cleanup
        ;;
    reset)
        reset_all
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        print_error "Unknown command: $1"
        show_help
        exit 1
        ;;
esac
