/* ==========================================================================
   PhoneStore Storefront — API client
   - Dùng backend thật cho các chức năng đã hiện thực (Identity, Catalog,
     Cart, Order, Inventory).
   - Các chức năng chưa có endpoint được đánh dấu MOCK và trả dữ liệu giả
     lập từ `mock.js` (khuyến mãi, trả góp, so sánh, đánh giá, yêu thích...).
   ========================================================================== */

const Api = (() => {
    const BASE = '/api/v1';
    const ACCESS_TOKEN_KEY = 'phonestore.accessToken';
    const USER_KEY = 'phonestore.user';

    let accessToken = sessionStorage.getItem(ACCESS_TOKEN_KEY) || null;
    let refreshPromise = null;

    /* ---------------- Token / user state ---------------- */

    function getAccessToken() {
        return accessToken;
    }

    function setSession(token, user) {
        accessToken = token || null;
        if (token) {
            sessionStorage.setItem(ACCESS_TOKEN_KEY, token);
        } else {
            sessionStorage.removeItem(ACCESS_TOKEN_KEY);
        }
        if (user) {
            localStorage.setItem(USER_KEY, JSON.stringify(user));
        } else {
            localStorage.removeItem(USER_KEY);
        }
        document.dispatchEvent(new CustomEvent('phonestore:auth-changed'));
    }

    function getUser() {
        try {
            return JSON.parse(localStorage.getItem(USER_KEY) || 'null');
        } catch {
            return null;
        }
    }

    function isAuthenticated() {
        return Boolean(accessToken);
    }

    function isStaff() {
        const user = getUser();
        return Boolean(user && (user.role === 'STAFF' || user.role === 'ADMIN'));
    }

    /* ---------------- HTTP core ---------------- */

    function readCsrfToken() {
        const match = document.cookie.split('; ').find(row => row.startsWith('phonestore_csrf='));
        return match ? decodeURIComponent(match.split('=')[1]) : null;
    }

    async function request(path, options = {}, mayRetry = true) {
        const method = (options.method || 'GET').toUpperCase();
        const headers = Object.assign({ 'Accept': 'application/json' }, options.headers || {});
        const requestToken = accessToken;
        let body = options.body;
        if (body !== undefined && typeof body !== 'string' && !(body instanceof FormData)) {
            headers['Content-Type'] = 'application/json';
            body = JSON.stringify(body);
        }
        if (requestToken) headers['Authorization'] = `Bearer ${requestToken}`;
        if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
            const csrfToken = readCsrfToken();
            if (csrfToken) headers['X-CSRF-TOKEN'] = csrfToken;
        }
        const response = await fetch(`${BASE}${path}`, { ...options, body, method, headers });
        const text = await response.text();
        const payload = text ? safeParse(text) : null;

        // Credentials errors must retain the backend error. Protected requests retry once.
        if (response.status === 401 && !path.startsWith('/auth/')) {
            if (mayRetry && requestToken) {
                const renewed = accessToken && accessToken !== requestToken
                    ? true : await tryRefreshToken();
                if (renewed) return request(path, options, false);
            }
            if (accessToken === requestToken) setSession(null, null);
        }
        if (!response.ok) {
            throw new ApiError(payload?.message || `Yêu cầu thất bại (HTTP ${response.status}).`,
                payload?.code || 'HTTP_ERROR', response.status, payload?.fieldErrors);
        }
        return payload?.data !== undefined ? payload.data : payload;
    }

    function safeParse(text) {
        try {
            return JSON.parse(text);
        } catch {
            return null;
        }
    }

    async function tryRefreshToken() {
        if (refreshPromise) return refreshPromise;
        const previousToken = accessToken;
        refreshPromise = (async () => {
            try {
                const csrfToken = readCsrfToken();
                const response = await fetch(`${BASE}/auth/refresh`, {
                    method: 'POST',
                    headers: csrfToken ? { 'X-CSRF-TOKEN': csrfToken } : {}
                });
                if (!response.ok) return false;
                const payload = await response.json();
                const data = payload?.data;
                // A logout or another login while refreshing must win.
                if (!data?.accessToken || accessToken !== previousToken) return false;
                setSession(data.accessToken, {
                    id: data.userId, email: data.email, fullName: data.fullName, role: data.role
                });
                return true;
            } catch {
                return false;
            }
        })();
        try { return await refreshPromise; }
        finally { refreshPromise = null; }
    }

    class ApiError extends Error {
        constructor(message, code, status, fieldErrors) {
            super(message);
            this.name = 'ApiError';
            this.code = code;
            this.status = status;
            this.fieldErrors = fieldErrors || null;
        }
    }

    /* ---------------- Identity (FR-01 → FR-03) ---------------- */

    const auth = {
        async login(email, password) {
            const data = await request('/auth/login', { method: 'POST', body: { email, password } });
            setSession(data.accessToken, { id: data.userId, email: data.email, fullName: data.fullName, role: data.role });
            return data;
        },

        async register(payload) {
            const data = await request('/auth/register', { method: 'POST', body: payload });
            setSession(data.accessToken, { id: data.userId, email: data.email, fullName: data.fullName, role: data.role });
            return data;
        },

        async logout() {
            try {
                await request('/auth/logout', { method: 'POST' });
            } finally {
                setSession(null, null);
            }
        },

        async profile() {
            return request('/me');
        },

        async updateProfile(payload) {
            const profile = await request('/me', { method: 'PATCH', body: payload });
            setSession(accessToken, { ...getUser(), ...profile });
            return profile;
        },

        async changePassword(currentPassword, newPassword) {
            const result = await request('/me/change-password', { method: 'POST', body: { currentPassword, newPassword } });
            setSession(null, null);
            return result;
        },

        async addresses() {
            return request('/me/addresses');
        },

        async updateAddress(id, payload) {
            return request(`/me/addresses/${id}`, { method: 'PUT', body: payload });
        },

        async deleteAddress(id) {
            return request(`/me/addresses/${id}`, { method: 'DELETE' });
        },

        async addAddress(payload) {
            return request('/me/addresses', { method: 'POST', body: payload });
        }
    };

    /* ---------------- Catalog (FR-04, FR-05, FR-12) ---------------- */

    const catalog = {
        async products(query = {}) {
            const params = new URLSearchParams();
            Object.entries(query).forEach(([key, value]) => {
                if (value !== null && value !== undefined && value !== '') {
                    params.set(key, value);
                }
            });
            const suffix = params.toString() ? `?${params.toString()}` : '';
            return request(`/products${suffix}`);
        },

        async product(id) {
            return request(`/products/${id}`);
        },

        async categories() {
            return request('/categories');
        },

        async brands() {
            return request('/brands');
        }
    };

    /* ---------------- Cart (FR-06) ---------------- */

    const cart = {
        async get() {
            return request('/cart');
        },

        async addItem(productVariantId, quantity = 1) {
            return request('/cart/items', { method: 'POST', body: { productVariantId, quantity } });
        },

        async updateItem(cartItemId, quantity) {
            return request(`/cart/items/${cartItemId}`, { method: 'PUT', body: { quantity } });
        },

        async removeItem(cartItemId) {
            return request(`/cart/items/${cartItemId}`, { method: 'DELETE' });
        }
    };

    /* ---------------- Order (FR-07 → FR-11) ---------------- */

    const order = {
        async list(query = {}) {
            const params = new URLSearchParams();
            Object.entries(query).forEach(([key, value]) => {
                if (value !== null && value !== undefined && value !== '') {
                    params.set(key, value);
                }
            });
            const suffix = params.toString() ? `?${params.toString()}` : '';
            return request(`/orders${suffix}`);
        },

        async detail(orderId) {
            return request(`/orders/${orderId}`);
        },

        async checkout(payload) {
            return request('/orders/checkout', { method: 'POST', body: payload });
        },

        async cancel(orderId, reason) {
            return request(`/orders/${orderId}/cancel`, { method: 'POST', body: { reason } });
        }
    };

    /* ---------------- Inventory & staff (FR-10, FR-11, FR-14, FR-15) ---------------- */

    const admin = {
        async stocks() {
            return request('/admin/inventory/stocks');
        },

        async lowStock(threshold = 10) {
            return request(`/admin/inventory/low-stock?threshold=${threshold}`);
        },

        async movements(query = {}) {
            const params = new URLSearchParams(query);
            return request(`/admin/inventory/movements?${params.toString()}`);
        },

        async codCollections(query = {}) {
            const params = new URLSearchParams(query);
            return request(`/admin/orders/cod-collections?${params.toString()}`);
        },

        async staff(query = {}) {
            const params = new URLSearchParams(query);
            return request(`/admin/staff?${params.toString()}`);
        },

        async createStaff(payload) {
            return request('/admin/staff', { method: 'POST', body: payload });
        },

        async changeRole(id, role) {
            return request(`/admin/staff/${id}/role`, { method: 'PATCH', body: { role } });
        },

        async lockAccount(id, isLocked, reason) {
            return request(`/admin/staff/${id}/lock`, { method: 'PATCH', body: { isLocked, reason } });
        },

        async resetPassword(id, newPassword) {
            return request(`/admin/staff/${id}/reset-password`, { method: 'POST', body: { newPassword } });
        },

        async dashboard(query = {}) {
            return request(`/admin/reports/dashboard?${new URLSearchParams(query)}`);
        },

        async confirmOrder(orderId, note) {
            return request(`/orders/${orderId}/confirm`, { method: 'POST', body: { note } });
        },

        async shipOrder(orderId, payload) {
            return request(`/orders/${orderId}/ship`, { method: 'POST', body: payload });
        },

        async deliverOrder(orderId, collectedAmount, note) {
            return request(`/orders/${orderId}/deliver`, { method: 'POST', body: { collectedAmount, note } });
        }
    };

    return { auth, catalog, cart, order, admin, ApiError, getAccessToken, getUser, setSession, isAuthenticated, isStaff, request };
})();
