# 🐳 CapBot Full Stack Docker Setup

Hướng dẫn chạy cả 2 project (Python FastAPI Agent + .NET Core API) bằng Docker Compose từ root directory.

## 📋 Yêu cầu hệ thống

- Docker Desktop
- Docker Compose v2.0+
- Ít nhất 8GB RAM
- 20GB dung lượng trống

## 🚀 Cách sử dụng

### 1. Cấu hình Environment

```bash
# Copy file environment mẫu
cp docker-compose.env .env

# Chỉnh sửa các giá trị cần thiết
nano .env
```

**Các biến quan trọng cần cấu hình:**
- `GOOGLE_API_KEY`: API key của Google AI
- `GEMINI_API_KEY`: API key của Gemini
- `EMAIL_PASSWORD`: Mật khẩu ứng dụng Gmail
- `SQLSERVER_SA_PASSWORD`: Mật khẩu SQL Server
- `JWT_SECRET_KEY`: Secret key cho JWT

### 2. Chạy tất cả services

```bash
# Chạy tất cả services cơ bản
docker-compose --env-file docker-compose.env up -d

# Chạy với cache và proxy
docker-compose --env-file docker-compose.env --profile cache --profile proxy up -d
```

### 3. Kiểm tra trạng thái

```bash
# Xem trạng thái các container
docker-compose ps

# Xem logs
docker-compose logs -f

# Xem logs của service cụ thể
docker-compose logs -f capbot-agent
docker-compose logs -f capbot-api
```

## 🌐 Truy cập các services

| Service | URL | Mô tả |
|---------|-----|-------|
| Python Agent API | http://localhost:8000 | FastAPI Agent service |
| .NET Core API | http://localhost:7190 | Main API service |
| SQL Server | localhost:1433 | Database server |
| Elasticsearch | http://localhost:9200 | Search engine |
| Traefik Dashboard | http://localhost:8080 | Reverse proxy dashboard |

## 🔧 Các lệnh hữu ích

### Quản lý services

```bash
# Dừng tất cả services
docker-compose down

# Dừng và xóa volumes
docker-compose down -v

# Rebuild và restart
docker-compose up --build -d

# Restart service cụ thể
docker-compose restart capbot-agent
```

### Debugging

```bash
# Vào container để debug
docker exec -it capbot-agent bash
docker exec -it capbot-api bash

# Xem logs realtime
docker-compose logs -f --tail=100

# Kiểm tra health check
docker-compose ps
```

### Database operations

```bash
# Kết nối SQL Server
docker exec -it capbot-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourStrong@Passw0rd123

# Backup database
docker exec capbot-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourStrong@Passw0rd123 -Q "BACKUP DATABASE capbot_db TO DISK = '/var/opt/mssql/backup/capbot_db.bak'"
```

## 📁 Cấu trúc volumes

- `chroma_data`: Dữ liệu ChromaDB vector database
- `sqlserver_data`: Dữ liệu SQL Server
- `elasticsearch_data`: Dữ liệu Elasticsearch
- `api_logs`: Logs của .NET API

## 🔍 Troubleshooting

### Lỗi thường gặp

1. **Port conflict**: Kiểm tra các port 8000, 7190, 1433, 9200 có bị sử dụng không
2. **Memory insufficient**: Tăng RAM cho Docker Desktop
3. **Database connection**: Kiểm tra connection string và credentials
4. **API key invalid**: Kiểm tra các API keys trong file .env

### Health checks

```bash
# Kiểm tra health của các services
curl http://localhost:8000/health  # Python Agent
curl http://localhost:7190/health  # .NET API
curl http://localhost:9200/_cluster/health  # Elasticsearch
```

### Reset toàn bộ

```bash
# Dừng và xóa tất cả
docker-compose down -v

# Xóa images
docker rmi $(docker images "capbot*" -q)

# Chạy lại từ đầu
docker-compose --env-file docker-compose.env up --build -d
```

## 🚀 Production Deployment

Để deploy production:

1. Thay đổi `ASPNETCORE_ENVIRONMENT=Production`
2. Cập nhật các API keys thực
3. Sử dụng HTTPS certificates
4. Cấu hình firewall và security
5. Setup monitoring và logging

```bash
# Production command
docker-compose --env-file .env.production up -d
```

## 📊 Resource Optimization

File docker-compose đã được tối ưu hóa với:
- **Multi-stage builds** để giảm kích thước image
- **Resource limits** để kiểm soát tài nguyên
- **Health checks** để đảm bảo services hoạt động
- **Volume persistence** để lưu trữ dữ liệu
- **Network isolation** để bảo mật

## 🎯 Quick Start

```bash
# 1. Clone repository
git clone <repository-url>
cd Production

# 2. Cấu hình environment
cp docker-compose.env .env
# Chỉnh sửa .env với API keys thực

# 3. Chạy services
docker-compose --env-file docker-compose.env up -d

# 4. Kiểm tra trạng thái
docker-compose ps

# 5. Truy cập services
# Python Agent: http://localhost:8000
# .NET API: http://localhost:7190
```
