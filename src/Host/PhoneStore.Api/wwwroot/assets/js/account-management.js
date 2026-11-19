/* Recovery credentials stay in page memory, never in URLs or browser storage. */
const AccountManagement = (() => {
    const e = value => UI.escapeHtml(String(value ?? ''));
    const field = (name, label, type = 'text', extra = '') => `<label class="field"><span class="field__label">${label}</span><input name="${name}" type="${type}" ${extra}></label>`;
    function bindForm(id, action) {
        const form = document.getElementById(id);
        form.addEventListener('submit', async event => {
            event.preventDefault();
            const button = form.querySelector('[type="submit"]');
            button.disabled = true;
            try { await action(form); }
            catch (error) { UI.toast(error.message, 'error'); }
            finally { button.disabled = false; }
        });
    }
    function requireAdmin() {
        if (Api.getUser()?.role === 'ADMIN') return true;
        UI.render('<div class="container"><p role="alert">Chức năng dành cho quản trị viên.</p></div>');
        return false;
    }
    const nav = '<p><a href="#/tai-khoan">Tài khoản</a> · <a href="#/quan-ly-khach-hang">Khách hàng</a> · <a href="#/nhat-ky-quan-tri">Nhật ký quản trị</a></p>';

    function recovery() {
        let challengeId = null;
        let resetToken = null;
        let nextRequestAt = 0;
        UI.render(`<div class="container"><div class="auth-card">
            <h2>Khôi phục mật khẩu</h2>
            <p>Nhập email tài khoản. SMS chỉ gửi tới số đã xác minh trước đó.</p>
            <form id="recovery-request" class="form">
                ${field('email', 'Email', 'email', 'required autocomplete="username"')}
                <label class="field">Kênh nhận mã<select name="channel"><option value="email">Email</option><option value="sms">SMS</option></select></label>
                <button class="btn btn--primary" type="submit">Yêu cầu mã / gửi lại sau 60 giây</button>
            </form>
            <p id="recovery-status" role="status"></p>
            <form id="recovery-verify" class="form" hidden>
                ${field('code', 'OTP 6 chữ số', 'text', 'required inputmode="numeric" pattern="[0-9]{6}" maxlength="6" autocomplete="one-time-code"')}
                <button class="btn btn--primary" type="submit">Xác minh OTP</button>
            </form>
            <form id="recovery-complete" class="form" hidden>
                ${field('newPassword', 'Mật khẩu mới (ít nhất 8 ký tự, có chữ và số)', 'password', 'required minlength="8" autocomplete="new-password"')}
                ${field('confirmation', 'Nhập lại mật khẩu mới', 'password', 'required autocomplete="new-password"')}
                <button class="btn btn--primary" type="submit">Đặt mật khẩu mới</button>
            </form><p><a href="#/dang-nhap">Về đăng nhập</a></p>
        </div></div>`);
        bindForm('recovery-request', async form => {
            if (Date.now() < nextRequestAt) throw new Error(`Vui lòng chờ ${Math.ceil((nextRequestAt - Date.now()) / 1000)} giây trước khi yêu cầu lại.`);
            const result = await Api.request('/auth/forgot-password', { method: 'POST', body: { email: form.email.value.trim(), channel: form.channel.value } });
            nextRequestAt = Date.now() + 60000;
            challengeId = result.challengeId;
            resetToken = null;
            document.getElementById('recovery-status').textContent = result.message;
            document.getElementById('recovery-complete').hidden = true;
            document.getElementById('recovery-complete').reset();
            document.getElementById('recovery-verify').hidden = false;
            document.getElementById('recovery-verify').reset();
        });
        bindForm('recovery-verify', async form => {
            const result = await Api.request('/auth/verify-reset-otp', { method: 'POST', body: { challengeId, code: form.code.value.trim() } });
            resetToken = result.resetToken;
            form.reset(); form.hidden = true;
            document.getElementById('recovery-complete').hidden = false;
            document.getElementById('recovery-status').textContent = 'OTP hợp lệ. Hãy đặt mật khẩu mới trong 5 phút.';
        });
        bindForm('recovery-complete', async form => {
            if (form.newPassword.value !== form.confirmation.value) throw new Error('Hai mật khẩu chưa trùng nhau.');
            await Api.request('/auth/reset-password', { method: 'POST', body: { challengeId, resetToken, newPassword: form.newPassword.value } });
            challengeId = null; resetToken = null; form.reset();
            Api.setSession(null, null);
            UI.toast('Đã đặt mật khẩu mới và thu hồi mọi phiên. Hãy đăng nhập lại.', 'success');
            window.location.hash = '#/dang-nhap';
        });
    }

    async function phone() {
        if (!Api.isAuthenticated()) { window.location.hash = '#/dang-nhap'; return; }
        let challengeId;
        const profile = await Api.auth.profile();
        UI.render(`<div class="container"><div class="auth-card"><h2>Xác minh số điện thoại</h2>
            <p>Số trong hồ sơ: ${e(profile.phone || 'Chưa có')}. ${profile.phoneVerified ? 'Đã xác minh.' : 'Chưa xác minh.'}</p>
            <p>Cập nhật số trong hồ sơ trước khi yêu cầu mã. Số đã xác minh dùng cho khôi phục bằng SMS.</p>
            <form id="phone-request" class="form">
                ${field('currentPassword', 'Mật khẩu hiện tại', 'password', 'required autocomplete="current-password"')}
                <button class="btn btn--primary" type="submit">Gửi mã SMS</button>
            </form>
            <form id="phone-confirm" class="form" hidden>
                ${field('code', 'OTP 6 chữ số', 'text', 'required pattern="[0-9]{6}" inputmode="numeric" autocomplete="one-time-code"')}
                <button class="btn btn--primary" type="submit">Xác minh</button>
            </form><p><a href="#/tai-khoan">Về tài khoản</a></p></div></div>`);
        bindForm('phone-request', async form => {
            const result = await Api.request('/me/phone-verification', { method: 'POST', body: { currentPassword: form.currentPassword.value } });
            challengeId = result.challengeId; form.reset();
            document.getElementById('phone-confirm').hidden = false;
            UI.toast(result.message, 'success');
        });
        bindForm('phone-confirm', async form => {
            await Api.request('/me/phone-verification/confirm', { method: 'POST', body: { challengeId, code: form.code.value.trim() } });
            form.reset(); UI.toast('Đã xác minh số điện thoại.', 'success');
            window.location.hash = '#/tai-khoan';
        });
    }

    async function customers(query = {}) {
        if (!requireAdmin()) return;
        const filters = { search: '', sort: 'createdAt', page: 1, pageSize: 20, ...query };
        const result = await Api.request(`/admin/customers?${new URLSearchParams(filters)}`);
        UI.render(`<div class="container">${nav}<h2>Quản lý khách hàng</h2>
            <p>Chỉ vô hiệu hóa tài khoản để giữ nguyên đơn hàng và địa chỉ. Email và vai trò không sửa tại đây.</p>
            <form id="customer-filter" class="form">
                ${field('search', 'Tên / email / số điện thoại', 'search', `maxlength="100" value="${e(filters.search)}"`)}
                <label>Trạng thái<select name="isActive"><option value="">Tất cả</option><option value="true">Hoạt động</option><option value="false">Đã khóa</option></select></label>
                <label>Sắp xếp<select name="sort"><option value="createdAt">Ngày tạo</option><option value="fullName">Họ tên</option><option value="email">Email</option></select></label>
                <label><input name="descending" type="checkbox" ${filters.descending === 'true' ? 'checked' : ''}> Giảm dần</label>
                <button class="btn btn--outline" type="submit">Lọc</button>
            </form>
            <p>${result.totalCount} khách hàng · Trang ${result.page}</p>
            <div class="info-grid">${result.items.map(user => `<div class="info-tile"><b>${e(user.fullName)}</b><span>${e(user.email)}</span><span>${e(user.phone)} · ${user.isActive ? 'Hoạt động' : 'Đã khóa'}</span><button class="btn btn--outline" data-customer-id="${user.id}">Chi tiết / thao tác</button></div>`).join('') || '<p>Không có kết quả.</p>'}</div>
            <button class="btn" id="customer-prev" ${result.page <= 1 ? 'disabled' : ''}>Trang trước</button>
            <button class="btn" id="customer-next" ${result.page * result.pageSize >= result.totalCount ? 'disabled' : ''}>Trang sau</button>
            <div id="customer-editor"></div>
            <details><summary>Tạo tài khoản khách hàng</summary><form class="form" id="customer-create">
                ${field('fullName', 'Họ tên', 'text', 'required maxlength="100"')}
                ${field('email', 'Email', 'email', 'required maxlength="254"')}
                ${field('phone', 'Số điện thoại')}
                ${field('password', 'Mật khẩu ban đầu', 'password', 'required minlength="8" autocomplete="new-password"')}
                <button class="btn btn--primary" type="submit">Tạo khách hàng</button>
            </form></details></div>`);
        const filter = document.getElementById('customer-filter');
        filter.isActive.value = filters.isActive || ''; filter.sort.value = filters.sort;
        bindForm('customer-filter', form => customers({ search: form.search.value.trim(), sort: form.sort.value,
            descending: String(form.descending.checked), ...(form.isActive.value ? { isActive: form.isActive.value } : {}) }));
        document.getElementById('customer-prev').onclick = () => customers({ ...filters, page: result.page - 1 }).catch(error => UI.toast(error.message, 'error'));
        document.getElementById('customer-next').onclick = () => customers({ ...filters, page: result.page + 1 }).catch(error => UI.toast(error.message, 'error'));
        document.querySelectorAll('[data-customer-id]').forEach(button => button.onclick = async () => {
            button.disabled = true;
            try { await customerEditor(button.dataset.customerId, filters); }
            catch (error) { UI.toast(error.message, 'error'); }
            finally { button.disabled = false; }
        });
        bindForm('customer-create', async form => {
            await Api.request('/admin/customers', { method: 'POST', body: { email: form.email.value.trim(), fullName: form.fullName.value.trim(), phone: form.phone.value.trim() || null, password: form.password.value } });
            form.reset(); UI.toast('Đã tạo khách hàng.', 'success'); await customers(filters);
        });
    }

    async function customerEditor(id, filters) {
        const detail = await Api.request(`/admin/customers/${id}`);
        const user = detail.profile;
        const editor = document.getElementById('customer-editor');
        editor.innerHTML = `<h3>${e(user.email)}</h3><p>SMS: ${user.phoneVerified ? 'Đã xác minh' : 'Chưa xác minh'}</p>
            <form id="customer-edit" class="form">
                ${field('fullName', 'Họ tên', 'text', `required maxlength="100" value="${e(user.fullName)}"`)}
                ${field('phone', 'Số điện thoại', 'text', `value="${e(user.phone)}"`)}
                ${field('reason', 'Lý do thao tác (không nhập mật khẩu / OTP)', 'text', 'required maxlength="500"')}
                <button class="btn btn--primary" type="submit">Lưu hồ sơ</button>
                <button class="btn btn--outline" id="customer-lock" type="button">${user.isActive ? 'Khóa' : 'Mở khóa'}</button>
                <button class="btn btn--outline" id="customer-revoke" type="button">Thu hồi mọi phiên</button>
            </form><form class="form" id="customer-reset">
                ${field('newPassword', 'Đặt mật khẩu mới cho khách hàng', 'password', 'required minlength="8" autocomplete="new-password"')}
                <button class="btn btn--outline" type="submit">Đặt lại mật khẩu và thu hồi phiên</button>
            </form><h4>Địa chỉ đã lưu</h4><ul>${detail.addresses.map(a => `<li>${e(a.receiverName)} · ${e(a.receiverPhone)} · ${e(a.addressText)}</li>`).join('') || '<li>Chưa có địa chỉ.</li>'}</ul>`;
        bindForm('customer-reset', async form => {
            if (!window.confirm('Đặt lại mật khẩu và đăng xuất mọi phiên của khách hàng?')) return;
            await Api.request(`/admin/customers/${id}/reset-password`, { method: 'POST', body: { newPassword: form.newPassword.value } });
            form.reset(); UI.toast('Đã đặt lại mật khẩu.', 'success');
        });
        bindForm('customer-edit', async form => {
            await Api.request(`/admin/customers/${id}`, { method: 'PATCH', body: { fullName: form.fullName.value.trim(), phone: form.phone.value.trim() || null, expectedUpdatedAt: user.updatedAt, reason: form.reason.value.trim() } });
            UI.toast('Đã cập nhật.', 'success'); await customers(filters);
        });
        for (const action of ['lock', 'revoke']) document.getElementById(`customer-${action}`).onclick = async event => {
            const button = event.currentTarget; button.disabled = true;
            try {
                const reason = document.getElementById('customer-edit').reason.value.trim();
                if (action === 'lock' && !reason) throw new Error('Cần nhập lý do khóa/mở tài khoản.');
                if (!window.confirm('Xác nhận thao tác với khách hàng này?')) return;
                await Api.request(`/admin/customers/${id}/${action === 'lock' ? 'lock' : 'revoke-sessions'}`, {
                    method: action === 'lock' ? 'PATCH' : 'POST', ...(action === 'lock' ? { body: { isLocked: user.isActive, reason } } : {})
                });
                UI.toast('Đã thực hiện.', 'success'); await customers(filters);
            } catch (error) { UI.toast(error.message, 'error'); }
            finally { button.disabled = false; }
        };
    }

    async function audit(query = {}) {
        if (!requireAdmin()) return;
        const result = await Api.request(`/admin/audit?${new URLSearchParams({ page: 1, pageSize: 20, ...query })}`);
        UI.render(`<div class="container">${nav}<h2>Nhật ký quản trị</h2>
            <form id="audit-filter" class="form">
                ${field('actorId', 'ID người thực hiện', 'number', 'min="1"')}
                ${field('targetId', 'ID tài khoản bị tác động', 'number', 'min="1"')}
                ${field('action', 'Loại thao tác (ví dụ CustomerLocked)', 'text', 'maxlength="50"')}
                ${field('from', 'Từ thời điểm', 'datetime-local')}${field('to', 'Đến thời điểm', 'datetime-local')}
                <button class="btn btn--outline" type="submit">Lọc nhật ký</button>
            </form><p>${result.totalCount} bản ghi · Trang ${result.page}</p>
            <div class="info-grid">${result.items.map(row => `<div class="info-tile"><b>${e(row.action)}</b><span>${e(row.createdAt)}</span><span>Người thực hiện: ${row.actorId} · Tài khoản: ${row.targetId}</span><span>${e(row.reason)}</span></div>`).join('') || '<p>Chưa có bản ghi phù hợp.</p>'}</div>
            <button class="btn" id="audit-prev" ${result.page <= 1 ? 'disabled' : ''}>Trang trước</button>
            <button class="btn" id="audit-next" ${result.page * result.pageSize >= result.totalCount ? 'disabled' : ''}>Trang sau</button></div>`);
        const form = document.getElementById('audit-filter');
        for (const name of ['actorId', 'targetId', 'action']) form.elements[name].value = query[name] || '';
        for (const name of ['from', 'to']) if (query[name]) {
            const date = new Date(query[name]);
            form.elements[name].value = new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
        }
        bindForm('audit-filter', form => {
            const next = {};
            for (const name of ['actorId', 'targetId', 'action', 'from', 'to']) {
                const value = form.elements[name].value.trim();
                if (value) next[name] = name === 'from' || name === 'to' ? new Date(value).toISOString() : value;
            }
            return audit(next);
        });
        document.getElementById('audit-prev').onclick = () => audit({ ...query, page: result.page - 1 }).catch(error => UI.toast(error.message, 'error'));
        document.getElementById('audit-next').onclick = () => audit({ ...query, page: result.page + 1 }).catch(error => UI.toast(error.message, 'error'));
    }
    return { recovery, phone, customers: () => customers(), audit: () => audit() };
})();
