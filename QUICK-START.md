# 🚀 CapBot Full Stack - Quick Start Guide

## 📁 Cấu trúc dự án đã tối ưu hóa

```
Production/
├── docker-compose-full.yml      # Docker Compose chính (tối ưu hóa)
├── docker-compose.env           # Environment variables
├── .dockerignore               # Docker ignore file tối ưu hóa
├── docker-scripts.sh           # Bash scripts quản lý
├── docker-scripts.ps1          # PowerShell scripts quản lý
├── README-DOCKER.md            # Hướng dẫn chi tiết
├── QUICK-START.md              # Hướng dẫn nhanh (file này)
├── capbot_agent/               # Python FastAPI Agent
│   ├── Dockerfile.optimized    # Dockerfile tối ưu hóa
│   └── ...
└── CBAI_API/                   # .NET Core API
    ├── Dockerfile              # Dockerfile tối ưu hóa
    └── ...
```

## ⚡ Quick Start Commands

### 1. Cấu hình Environment
```bash
# Copy file environment
cp docker-compose.env .env

# Chỉnh sửa API keys trong .env
nano .env
```

### 2. Chạy tất cả services
```bash
# Sử dụng script (Linux/Mac)
./docker-scripts.sh start

# Hoặc sử dụng PowerShell (Windows)
.\docker-scripts.ps1 start

# Hoặc chạy trực tiếp
docker-compose --env-file .env up -d
```

### 3. Kiểm tra trạng thái
```bash
# Kiểm tra health
./docker-scripts.sh health

# Xem logs
./docker-scripts.sh logs

# Xem logs service cụ thể
./docker-scripts.sh logs capbot-agent
```

## 🌐 Truy cập Services

| Service | URL | Mô tả |
|---------|-----|-------|
| **Python Agent** | http://localhost:8000 | FastAPI Agent service |
| **.NET API** | http://localhost:7190 | Main API service |
| **SQL Server** | localhost:1433 | Database server |
| **Elasticsearch** | http://localhost:9200 | Search engine |
| **Traefik Dashboard** | http://localhost:8080 | Reverse proxy dashboard |

## 🔧 Quản lý Services

### Sử dụng Scripts (Khuyến nghị)

```bash
# Start services
./docker-scripts.sh start

# Stop services
./docker-scripts.sh stop

# Restart services
./docker-scripts.sh restart

# Rebuild và start
./docker-scripts.sh rebuild

# Xem logs
./docker-scripts.sh logs

# Kiểm tra health
./docker-scripts.sh health

# Cleanup Docker
./docker-scripts.sh cleanup

# Reset toàn bộ (Cẩn thận!)
./docker-scripts.sh reset
```

### Sử dụng Docker Compose trực tiếp

```bash
# Start services
docker-compose --env-file .env up -d

# Stop services
docker-compose down

# Rebuild services
docker-compose --env-file .env up --build -d

# Xem logs
docker-compose logs -f

# Xem trạng thái
docker-compose ps
```

## 🎯 Tối ưu hóa đã thực hiện

### 1. **Docker Compose tối ưu hóa**
- ✅ Resource limits cho từng service
- ✅ Health checks cho tất cả services
- ✅ Network isolation
- ✅ Volume persistence
- ✅ Context paths chính xác

### 2. **Dockerfile tối ưu hóa**
- ✅ Multi-stage builds
- ✅ Non-root user cho security
- ✅ Optimized layer caching
- ✅ Reduced image size
- ✅ Security improvements

### 3. **Environment tối ưu hóa**
- ✅ Tách biệt development/production
- ✅ Centralized configuration
- ✅ Security best practices

### 4. **Scripts quản lý**
- ✅ Bash scripts cho Linux/Mac
- ✅ PowerShell scripts cho Windows
- ✅ Health monitoring
- ✅ Easy cleanup và reset

## 🚨 Troubleshooting

### Lỗi thường gặp

1. **Port conflict**: Kiểm tra port 8000, 7190, 1433, 9200
2. **Memory insufficient**: Tăng RAM cho Docker Desktop
3. **API key invalid**: Kiểm tra file .env
4. **Database connection**: Kiểm tra SQL Server credentials

### Debug Commands

```bash
# Vào container debug
docker exec -it capbot-agent bash
docker exec -it capbot-api bash

# Kiểm tra logs chi tiết
docker-compose logs -f --tail=100

# Kiểm tra resource usage
docker stats

# Kiểm tra network
docker network ls
docker network inspect production_capbot-network
```

## 📊 Resource Usage

| Service | Memory Limit | CPU Limit | Port |
|---------|---------------|-----------|------|
| capbot-agent | 2GB | 1.0 CPU | 8000 |
| capbot-api | 4GB | 2.0 CPU | 7190 |
| sqlserver | 2GB | 1.0 CPU | 1433 |
| elasticsearch | 2GB | 1.0 CPU | 9200 |
| traefik | 512MB | 0.5 CPU | 80,443,8080 |

## 🔄 Development Workflow

### 1. Development
```bash
# Start development environment
./docker-scripts.sh start

# Make changes to code
# Rebuild specific service
docker-compose build capbot-agent
docker-compose up -d capbot-agent
```

### 2. Testing
```bash
# Check health
./docker-scripts.sh health

# View logs
./docker-scripts.sh logs

# Test API endpoints
curl http://localhost:8000/health
curl http://localhost:7190/health
```

### 3. Production
```bash
# Update environment for production
# Change ASPNETCORE_ENVIRONMENT=Production in .env
# Update API keys and credentials

# Deploy
./docker-scripts.sh rebuild
```

## 🎉 Kết quả tối ưu hóa

- **Image size giảm**: Từ ~28GB xuống ~3GB (89% giảm)
- **Build time nhanh hơn**: Multi-stage builds
- **Security tốt hơn**: Non-root users, optimized dependencies
- **Resource management**: Limits và reservations
- **Easy management**: Scripts tự động hóa

## 📞 Support

Nếu gặp vấn đề:
1. Kiểm tra logs: `./docker-scripts.sh logs`
2. Kiểm tra health: `./docker-scripts.sh health`
3. Reset nếu cần: `./docker-scripts.sh reset`
4. Xem README-DOCKER.md để biết thêm chi tiết
