#!/bin/bash

# Script tạo self-signed certificates cho IP address
# Sử dụng: ./create-ssl-certs.sh YOUR_DROPLET_IP

set -e

# Kiểm tra tham số
if [ $# -eq 0 ]; then
    echo "Sử dụng: $0 <DROPLET_IP>"
    echo "Ví dụ: $0 152.42.227.169"
    exit 1
fi

DROPLET_IP=$1

# Tạo thư mục
mkdir -p traefik/certs
mkdir -p traefik/dynamic

# Tạo private key
openssl genrsa -out traefik/certs/server.key 2048

# Tạo certificate signing request
openssl req -new -key traefik/certs/server.key -out traefik/certs/server.csr -subj "/C=VN/ST=HCM/L=HoChiMinh/O=CapBot/OU=IT/CN=${DROPLET_IP}"

# Tạo self-signed certificate
openssl x509 -req -days 365 -in traefik/certs/server.csr -signkey traefik/certs/server.key -out traefik/certs/server.crt -extensions v3_req -extfile <(
cat <<EOF
[req]
distinguished_name = req_distinguished_name
req_extensions = v3_req
prompt = no

[req_distinguished_name]
C = VN
ST = HCM
L = HoChiMinh
O = CapBot
OU = IT
CN = ${DROPLET_IP}

[v3_req]
keyUsage = keyEncipherment, dataEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names

[alt_names]
IP.1 = ${DROPLET_IP}
EOF
)

# Tạo Traefik TLS configuration
cat > traefik/dynamic/tls.yml <<EOF
tls:
  certificates:
    - certFile: /etc/traefik/certs/server.crt
      keyFile: /etc/traefik/certs/server.key
  stores:
    default:
      defaultCertificate:
        certFile: /etc/traefik/certs/server.crt
        keyFile: /etc/traefik/certs/server.key
EOF

# Set permissions
chmod 600 traefik/certs/server.key
chmod 644 traefik/certs/server.crt

echo "✅ Self-signed certificates đã được tạo thành công!"
echo "📁 Certificates location: traefik/certs/"
echo "🔐 Private key: traefik/certs/server.key"
echo "📜 Certificate: traefik/certs/server.crt"
echo ""
echo "⚠️  Lưu ý: Self-signed certificates sẽ hiển thị warning trong browser"
echo "   Bạn cần accept certificate để tiếp tục sử dụng"
echo ""
echo "🚀 Để deploy, chạy:"
echo "   docker-compose -f docker-compose-full.yml --profile proxy up -d"
