#!/bin/bash

echo "=== Rebuilding CapBot Agent with HTTPS ==="

# 1. Tạo SSL certificates nếu chưa có
echo "Creating SSL certificates..."
chmod +x create-ssl-certs.sh
./create-ssl-certs.sh 152.42.227.169

# 2. Tạo file .env
echo "Creating .env file..."
cp docker-compose.env .env

# 3. Dừng Agent container
echo "Stopping Agent container..."
docker stop capbot-agent
docker rm capbot-agent

# 4. Rebuild Agent
echo "Rebuilding Agent..."
docker-compose -f docker-compose-full.yml build --no-cache capbot-agent

# 5. Chạy lại Agent
echo "Starting Agent..."
docker-compose -f docker-compose-full.yml up -d capbot-agent

# 6. Đợi Agent khởi động
echo "Waiting for Agent to start..."
sleep 15

# 7. Kiểm tra status
echo "=== Container Status ==="
docker ps | grep capbot-agent

# 8. Kiểm tra logs
echo "=== Agent Logs (last 10 lines) ==="
docker logs capbot-agent 2>&1 | tail -10

# 9. Test endpoints
echo "=== Testing Endpoints ==="
echo "HTTP Agent:"
curl -s --connect-timeout 5 http://152.42.227.169:8000/docs | head -3 || echo "❌ HTTP Agent Error"

echo -e "\nHTTPS Agent:"
curl -k -s --connect-timeout 5 https://152.42.227.169/agent/docs | head -3 || echo "❌ HTTPS Agent Error"

echo -e "\nAgent Health:"
curl -s --connect-timeout 5 http://152.42.227.169:8000/api/v1/health || echo "❌ Agent Health Error"

echo -e "\nHTTPS Agent Health:"
curl -k -s --connect-timeout 5 https://152.42.227.169/agent/api/v1/health || echo "❌ HTTPS Agent Health Error"

echo -e "\n=== Rebuild Complete ==="
