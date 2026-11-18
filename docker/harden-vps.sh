#!/usr/bin/env bash
# ==============================================================================
# Gia cố bảo mật VPS Ubuntu chạy PhoneStore (chạy bằng root, MỘT lần).
#
#   1. Cài bản vá bảo mật
#   2. fail2ban: chặn IP đoán mật khẩu SSH (3 lần sai trong 10 phút → cấm 24 giờ)
#   3. SSH chỉ đăng nhập bằng key (tắt mật khẩu), tắt X11, giảm số lần thử
#   4. Tường lửa ufw: chỉ mở 22 (SSH), 80, 443
#
# ĐIỀU KIỆN: phải đăng nhập được bằng SSH key (script tự kiểm tra và dừng nếu chưa có key).
# Sau khi chạy, đăng nhập bằng:  ssh -i ~/.ssh/phonestore_deploy root@<IP>
# Lối thoát khẩn cấp: nút "Console" trên trang quản lý VPS của nhà cung cấp.
#
# Chạy từ máy của bạn:
#   ssh -i ~/.ssh/phonestore_deploy root@103.167.89.148 'bash -s' < docker/harden-vps.sh
# ==============================================================================
set -euo pipefail

if [[ $EUID -ne 0 ]]; then
    echo "Hãy chạy bằng root." >&2; exit 1
fi

if [[ ! -s /root/.ssh/authorized_keys ]] || ! grep -qE '^(ssh-|ecdsa-)' /root/.ssh/authorized_keys; then
    echo "DỪNG: /root/.ssh/authorized_keys chưa có SSH key — tắt đăng nhập mật khẩu lúc này sẽ khoá bạn ở ngoài." >&2
    exit 1
fi

echo "==> [1/4] Cập nhật bản vá bảo mật"
export DEBIAN_FRONTEND=noninteractive
apt-get update -q
apt-get -y -q -o Dpkg::Options::="--force-confold" full-upgrade
apt-get -y -q install fail2ban ufw unattended-upgrades
systemctl enable --now unattended-upgrades

echo "==> [2/4] fail2ban cho SSH"
cat > /etc/fail2ban/jail.d/sshd-phonestore.local <<'EOF'
[sshd]
enabled  = true
backend  = systemd
maxretry = 3
findtime = 10m
bantime  = 24h
EOF
systemctl enable fail2ban
systemctl restart fail2ban

echo "==> [3/4] SSH chỉ dùng key"
# Tên bắt đầu bằng 00- để được đọc TRƯỚC 50-cloud-init.conf (sshd lấy giá trị gặp đầu tiên).
cat > /etc/ssh/sshd_config.d/00-phonestore-hardening.conf <<'EOF'
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitRootLogin prohibit-password
PubkeyAuthentication yes
PermitEmptyPasswords no
X11Forwarding no
MaxAuthTries 3
LoginGraceTime 30
ClientAliveInterval 300
ClientAliveCountMax 2
EOF
sshd -t
systemctl reload ssh
sshd -T | grep -E '^(passwordauthentication|permitrootlogin|x11forwarding|maxauthtries) '

echo "==> [4/4] Tường lửa ufw"
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp comment 'SSH'
ufw allow 80/tcp comment 'HTTP'
ufw allow 443/tcp comment 'HTTPS'
ufw --force enable
ufw status verbose | head -20

echo
echo "XONG. Việc còn lại:"
echo "  - Mở cửa sổ terminal MỚI, thử: ssh -i ~/.ssh/phonestore_deploy root@<IP>  (đừng đóng phiên hiện tại trước khi thử)"
echo "  - Đổi mật khẩu root (dùng cho Console khẩn cấp):  passwd"
if [[ -f /var/run/reboot-required ]]; then
    echo "  - Kernel mới đã cài: khởi động lại khi tiện:  reboot   (web tự chạy lại sau ~1 phút)"
fi
