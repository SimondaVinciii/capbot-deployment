# Hướng dẫn Deploy HTTPS với IP Address cho CapBot System

## Tổng quan
Hướng dẫn này sẽ giúp bạn cấu hình HTTPS cho hệ thống CapBot sử dụng IP address thay vì domain name.

## Yêu cầu
- DigitalOcean droplet với Docker và Docker Compose
- IP address của droplet (ví dụ: 152.42.227.169)
- Port 80 và 443 đã được mở

## Bước 1: Tạo Self-signed Certificates

### 1.1 Chạy script tạo certificates
```bash
# Thay YOUR_DROPLET_IP bằng IP thực tế của bạn
./create-ssl-certs.sh 152.42.227.169
```

### 1.2 Kiểm tra certificates đã được tạo
```bash
ls -la traefik/certs/
# Kết quả mong đợi:
# server.crt
# server.csr  
# server.key
```

## Bước 2: Cấu hình Environment Variables

### 2.1 Cập nhật docker-compose.env
```bash
# Cập nhật IP address của bạn
DROPLET_IP=152.42.227.169

# Cập nhật URLs
HOME_URL=https://152.42.227.169/api
HOME_API_DOMAIN=152.42.227.169
CDN_URL=https://152.42.227.169
```

### 2.2 Cập nhật CORS trong code
Thay đổi IP address trong các file sau:

**capbot_agent/main.py:**
```python
allow_origins=[
    "https://your-frontend-domain.vercel.app",
    "https://*.vercel.app",
    "https://152.42.227.169",  # Thay bằng IP của bạn
    "http://152.42.227.169",   # Thay bằng IP của bạn
    "http://localhost:3000",
    "http://localhost:3001",
],
```

**CBAI_API/CapBot.api/Program.cs:**
```csharp
build.WithOrigins(
    "https://your-frontend-domain.vercel.app",
    "https://*.vercel.app", 
    "https://152.42.227.169",  // Thay bằng IP của bạn
    "http://152.42.227.169",   // Thay bằng IP của bạn
    "http://localhost:3000",
    "http://localhost:3001"
)
```

## Bước 3: Deploy với HTTPS

### 3.1 Chạy Docker Compose
```bash
# Chạy với Traefik proxy
docker-compose -f docker-compose-full.yml --profile proxy up -d
```

### 3.2 Kiểm tra services
```bash
# Kiểm tra containers đang chạy
docker ps

# Kiểm tra logs của Traefik
docker logs capbot-traefik

# Kiểm tra logs của API
docker logs capbot-api

# Kiểm tra logs của Agent
docker logs capbot-agent
```

## Bước 4: Test HTTPS Endpoints

### 4.1 Test API endpoints
```bash
# Test API với HTTPS
curl -k https://152.42.227.169/api/api/v1/health

# Test Agent với HTTPS  
curl -k https://152.42.227.169/agent/api/v1/health
```

### 4.2 Test từ browser
Mở browser và truy cập:
- `https://152.42.227.169/api` - .NET API
- `https://152.42.227.169/agent` - FastAPI Agent
- `https://152.42.227.169:8080` - Traefik Dashboard

**⚠️ Lưu ý:** Browser sẽ hiển thị warning về self-signed certificate. Bạn cần:
1. Click "Advanced" 
2. Click "Proceed to 152.42.227.169 (unsafe)"

## Bước 5: Cấu hình Frontend Vercel

### 5.1 Cập nhật API URLs trong Frontend
```javascript
// Thay vì
const API_URL = 'http://152.42.227.169:7190';

// Sử dụng
const API_URL = 'https://152.42.227.169/api';
const AGENT_URL = 'https://152.42.227.169/agent';
```

### 5.2 Cấu hình Environment Variables trong Vercel
Trong Vercel dashboard, thêm:
```
NEXT_PUBLIC_API_URL=https://152.42.227.169/api
NEXT_PUBLIC_AGENT_URL=https://152.42.227.169/agent
```

### 5.3 Xử lý Self-signed Certificate trong Frontend
Nếu frontend gặp lỗi về self-signed certificate, thêm vào fetch requests:

```javascript
// Tạm thời disable SSL verification (chỉ dùng cho development)
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

// Hoặc sử dụng custom fetch với ignore SSL
const fetchWithIgnoreSSL = (url, options = {}) => {
  return fetch(url, {
    ...options,
    // Node.js environment
    agent: new (require('https').Agent)({
      rejectUnauthorized: false
    })
  });
};
```

## Bước 6: Troubleshooting

### 6.1 Lỗi "Certificate not trusted"
**Nguyên nhân:** Self-signed certificate không được browser tin tưởng
**Giải pháp:** 
- Accept certificate trong browser
- Hoặc import certificate vào browser

### 6.2 Lỗi CORS
**Nguyên nhân:** IP address không được thêm vào CORS origins
**Giải pháp:**
- Kiểm tra CORS settings trong code
- Đảm bảo IP address được thêm vào allow_origins

### 6.3 Lỗi Connection Refused
**Nguyên nhân:** Services chưa start hoặc port bị block
**Giải pháp:**
```bash
# Kiểm tra services
docker ps

# Kiểm tra ports
netstat -tlnp | grep :443
netstat -tlnp | grep :80

# Restart services
docker-compose -f docker-compose-full.yml restart
```

### 6.4 Lỗi "Invalid certificate"
**Nguyên nhân:** Certificate không match với IP
**Giải pháp:**
```bash
# Tạo lại certificates
rm -rf traefik/certs/*
./create-ssl-certs.sh YOUR_DROPLET_IP

# Restart Traefik
docker-compose -f docker-compose-full.yml restart traefik
```

## Bước 7: Monitoring

### 7.1 Kiểm tra SSL Certificate
```bash
# Kiểm tra certificate info
openssl x509 -in traefik/certs/server.crt -text -noout

# Test SSL connection
openssl s_client -connect 152.42.227.169:443 -servername 152.42.227.169
```

### 7.2 Monitor Services
```bash
# Monitor logs
docker-compose -f docker-compose-full.yml logs -f

# Monitor specific service
docker logs -f capbot-api
docker logs -f capbot-agent
docker logs -f capbot-traefik
```

## Lưu ý quan trọng

1. **Self-signed certificates** sẽ hiển thị warning trong browser
2. **IP address** phải được cập nhật đúng trong tất cả cấu hình
3. **Port 80 và 443** phải được mở trên firewall
4. **CORS settings** phải include IP address
5. **Frontend** cần xử lý self-signed certificate

## Kết quả mong đợi

Sau khi hoàn thành:
- ✅ HTTPS hoạt động với IP address
- ✅ Self-signed certificates được tạo
- ✅ Traefik routing hoạt động
- ✅ Frontend Vercel có thể kết nối (với warning về certificate)
- ✅ CORS được cấu hình đúng

## Alternative Solutions

Nếu self-signed certificates gây vấn đề, bạn có thể:

1. **Sử dụng HTTP thay vì HTTPS** (không khuyến khích cho production)
2. **Mua domain name** và sử dụng Let's Encrypt
3. **Sử dụng Cloudflare** để proxy HTTPS
4. **Sử dụng reverse proxy** như Nginx với SSL

## Support

Nếu gặp vấn đề:
1. Kiểm tra logs: `docker logs capbot-traefik`
2. Kiểm tra certificates: `ls -la traefik/certs/`
3. Test connectivity: `curl -k https://YOUR_IP/api`
4. Kiểm tra firewall: `ufw status`
