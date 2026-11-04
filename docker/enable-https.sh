#!/usr/bin/env bash
# Bật HTTPS cho PhoneStore bằng Let's Encrypt. Chạy trên server, trong thư mục gốc dự án.
# Yêu cầu: bản ghi A của DOMAIN đã trỏ về IP server, cổng 80/443 mở.
#   ./docker/enable-https.sh phonestore.lazi.id.vn you@example.com
set -euo pipefail

DOMAIN="${1:?Thiếu domain}"
EMAIL="${2:?Thiếu email nhận cảnh báo hết hạn chứng chỉ}"
COMPOSE="docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod"
ENVFILE=docker/env.prod

mkdir -p certbot-www
certbot certonly --webroot -w "$PWD/certbot-www" -d "$DOMAIN" \
    --non-interactive --agree-tos -m "$EMAIL" --keep-until-expiring

set_env() { grep -q "^$1=" "$ENVFILE" && sed -i "s|^$1=.*|$1=$2|" "$ENVFILE" || echo "$1=$2" >> "$ENVFILE"; }
set_env NGINX_CONF nginx.ssl.conf
set_env PUBLIC_ORIGIN "https://$DOMAIN"

$COMPOSE up -d

# Tự gia hạn: certbot timer chạy định kỳ, nginx nạp lại chứng chỉ mới sau khi gia hạn.
mkdir -p /etc/letsencrypt/renewal-hooks/deploy
cat > /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh <<'HOOK'
#!/bin/sh
docker exec phonestore-web nginx -s reload
HOOK
chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh
echo "Đã bật HTTPS: https://$DOMAIN"
