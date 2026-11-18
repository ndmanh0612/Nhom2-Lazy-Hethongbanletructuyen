#!/usr/bin/env bash
# ==============================================================================
# Đặt mật khẩu mới cho admin@phonestore.vn trên VPS (khi admin bị khoá vì mật khẩu mẫu).
# Mật khẩu chỉ nhập trên VPS, không hiện ra màn hình, và được xoá khỏi env.prod ngay sau khi áp dụng.
#
# Chạy từ máy của bạn (cần -t để gõ được mật khẩu):
#   ssh -t -i ~/.ssh/phonestore_deploy root@103.167.89.148 "bash /root/set-admin-password.sh"
# ==============================================================================
set -euo pipefail
cd /opt/phonestore
ENV_FILE=docker/env.prod
COMPOSE=(docker compose -f docker/docker-compose.prod.yml --env-file "$ENV_FILE")

read -r -s -p "Mật khẩu mới cho admin@phonestore.vn (>= 12 ký tự): " p1; echo
read -r -s -p "Nhập lại mật khẩu: " p2; echo
if [[ "$p1" != "$p2" ]]; then echo "Hai lần nhập không khớp." >&2; exit 1; fi
if (( ${#p1} < 12 )); then echo "Mật khẩu phải dài ít nhất 12 ký tự." >&2; exit 1; fi
if [[ "$p1" == *$'\n'* || "$p1" == *"'"* || "$p1" == *'"'* || "$p1" == *'$'* || "$p1" == *' '* ]]; then
    echo "Mật khẩu không được chứa dấu cách, dấu nháy hoặc ký tự \$." >&2; exit 1
fi

cleanup() { sed -i '/^BOOTSTRAP_ADMIN_PASSWORD=/d' "$ENV_FILE"; }
trap cleanup EXIT

cleanup
printf 'BOOTSTRAP_ADMIN_PASSWORD=%s\n' "$p1" >> "$ENV_FILE"
unset p1 p2

echo "Đang áp dụng (khởi động lại API)..."
"${COMPOSE[@]}" up -d api >/dev/null 2>&1
for _ in $(seq 1 30); do
    sleep 3
    if docker logs --since 2m phonestore-api 2>&1 | grep -q "Đã đổi mật khẩu admin@phonestore.vn"; then
        cleanup
        "${COMPOSE[@]}" up -d api >/dev/null 2>&1
        echo "XONG: đăng nhập https://phonestore.lazi.id.vn bằng admin@phonestore.vn và mật khẩu vừa đặt."
        echo "Mật khẩu đã được xoá khỏi env.prod."
        exit 0
    fi
done

echo "Không thấy API xác nhận đổi mật khẩu. Có thể admin đã đổi mật khẩu riêng từ trước (khi đó không cần làm gì)." >&2
echo "Log gần nhất:" >&2
docker logs --since 3m phonestore-api 2>&1 | grep -E "admin@phonestore.vn|BootstrapAdminPassword" | tail -3 >&2 || true
exit 1
