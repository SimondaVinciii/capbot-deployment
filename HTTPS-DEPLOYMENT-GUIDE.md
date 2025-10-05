# Hướng dẫn Deploy HTTPS cho CapBot System

## Tổng quan
Hướng dẫn này sẽ giúp bạn cấu hình HTTPS cho hệ thống CapBot để frontend Vercel có thể kết nối được với backend trên DigitalOcean.

## Yêu cầu
- Domain name (ví dụ: yourdomain.com)
- DigitalOcean droplet với Docker và Docker Compose
- DNS records đã được cấu hình

## Bước 1: Cấu hình Domain và DNS

### 1.1 Cấu hình DNS Records
Tạo các DNS records sau trong domain provider của bạn:

```
A     yourdomain.com        -> YOUR_DROPLET_IP
A     api.yourdomain.com    -> YOUR_DROPLET_IP  
A     agent.yourdomain.com  -> YOUR_DROPLET_IP
```

### 1.2 Cập nhật Environment Variables
Sửa file `docker-compose.env`:

```bash
# Thay đổi domain của bạn
AGENT_DOMAIN=agent.yourdomain.com
API_DOMAIN=api.yourdomain.com
ACME_EMAIL=admin@yourdomain.com

# Cập nhật URLs
HOME_URL=https://api.yourdomain.com
HOME_API_DOMAIN=api.yourdomain.com
CDN_URL=https://api.yourdomain.com
```

## Bước 2: Cấu hình CORS cho Frontend

### 2.1 Cập nhật CORS trong FastAPI (capbot_agent/main.py)
```python
allow_origins=[
    "https://your-frontend-domain.vercel.app",  # Thay bằng domain Vercel thực tế
    "https://*.vercel.app",
    "http://localhost:3000",
    "http://localhost:3001",
],
```

### 2.2 Cập nhật CORS trong .NET API (CBAI_API/CapBot.api/Program.cs)
```csharp
build.WithOrigins(
    "https://your-frontend-domain.vercel.app",  // Thay bằng domain Vercel thực tế
    "https://*.vercel.app",
    "http://localhost:3000",
    "http://localhost:3001"
)
```

## Bước 3: Deploy với HTTPS

### 3.1 Chạy Docker Compose với Traefik
```bash
# Chạy với profile proxy để bật Traefik
docker-compose -f docker-compose-full.yml --profile proxy up -d
```

### 3.2 Kiểm tra SSL Certificates
```bash
# Kiểm tra logs của Traefik
docker logs capbot-traefik

# Kiểm tra certificates
docker exec capbot-traefik ls -la /letsencrypt/
```

## Bước 4: Cấu hình Frontend Vercel

### 4.1 Cập nhật API URLs trong Frontend
Thay đổi các API endpoints trong frontend:

```javascript
// Thay vì
const API_URL = 'http://your-droplet-ip:7190';

// Sử dụng
const API_URL = 'https://api.yourdomain.com';
const AGENT_URL = 'https://agent.yourdomain.com';
```

### 4.2 Cấu hình Environment Variables trong Vercel
Trong Vercel dashboard, thêm các environment variables:

```
NEXT_PUBLIC_API_URL=https://api.yourdomain.com
NEXT_PUBLIC_AGENT_URL=https://agent.yourdomain.com
```

## Bước 5: Kiểm tra và Test

### 5.1 Kiểm tra HTTPS
```bash
# Test API endpoint
curl -I https://api.yourdomain.com/api/v1/health

# Test Agent endpoint  
curl -I https://agent.yourdomain.com/api/v1/health
```

### 5.2 Kiểm tra SSL Certificate
```bash
# Kiểm tra certificate
openssl s_client -connect api.yourdomain.com:443 -servername api.yourdomain.com
```

## Bước 6: Troubleshooting

### 6.1 Lỗi CORS
Nếu gặp lỗi CORS, kiểm tra:
- Domain trong CORS settings có đúng không
- Frontend domain có được thêm vào allow_origins không

### 6.2 Lỗi SSL Certificate
Nếu certificate không được tạo:
- Kiểm tra DNS records đã propagate chưa
- Kiểm tra firewall có block port 80, 443 không
- Kiểm tra logs của Traefik

### 6.3 Lỗi Connection
Nếu không kết nối được:
- Kiểm tra domain có resolve đúng IP không
- Kiểm tra services có chạy không: `docker ps`
- Kiểm tra Traefik dashboard: `http://your-droplet-ip:8080`

## Bước 7: Monitoring và Maintenance

### 7.1 Monitor SSL Certificates
```bash
# Kiểm tra certificate expiry
docker exec capbot-traefik cat /letsencrypt/acme.json | jq '.letsencrypt.Certificates'
```

### 7.2 Backup Certificates
```bash
# Backup certificates
docker cp capbot-traefik:/letsencrypt/acme.json ./backup-acme.json
```

## Lưu ý quan trọng

1. **Domain phải được cấu hình đúng** trước khi chạy Traefik
2. **Port 80 và 443** phải được mở trên firewall
3. **Let's Encrypt có rate limit**, không nên test quá nhiều lần
4. **Backup certificates** để tránh mất khi container bị xóa
5. **Monitor logs** để phát hiện lỗi sớm

## Kết quả mong đợi

Sau khi hoàn thành, bạn sẽ có:
- ✅ HTTPS cho tất cả services
- ✅ Automatic SSL certificate renewal
- ✅ Frontend Vercel có thể kết nối được với backend
- ✅ CORS được cấu hình đúng
- ✅ Security headers được bật

## Support

Nếu gặp vấn đề, kiểm tra:
1. DNS propagation: https://dnschecker.org
2. SSL certificate: https://www.ssllabs.com/ssltest/
3. Traefik logs: `docker logs capbot-traefik`
