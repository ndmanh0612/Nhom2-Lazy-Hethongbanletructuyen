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

    async function request(path, options = {}) {
        const method = (options.method || 'GET').toUpperCase();
        const headers = Object.assign({ 'Accept': 'application/json' }, options.headers || {});

        if (options.body !== undefined && typeof options.body !== 'string') {
            headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(options.body);
        }

        if (accessToken) {
            headers['Authorization'] = `Bearer ${accessToken}`;
        }

        // Endpoint dùng cookie (refresh / logout) cần CSRF token khớp cookie.
        if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
            const csrfToken = readCsrfToken();
            if (csrfToken) {
                headers['X-CSRF-TOKEN'] = csrfToken;
            }
        }

        const response = await fetch(`${BASE}${path}`, Object.assign({}, options, { method, headers }));

        if (response.status === 401) {
            const refreshed = await tryRefreshToken();
            if (refreshed) {
                return request(path, options);
            }
            setSession(null, null);
            throw new ApiError('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.', 'UNAUTHORIZED', 401);
        }

        const text = await response.text();
        const payload = text ? safeParse(text) : null;

        if (!response.ok) {
            const message = payload?.message || `Yêu cầu thất bại (HTTP ${response.status}).`;
            throw new ApiError(message, payload?.code || 'HTTP_ERROR', response.status, payload?.fieldErrors);
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
        try {
            const csrfToken = readCsrfToken();
            const response = await fetch(`${BASE}/auth/refresh`, {
                method: 'POST',
                headers: csrfToken ? { 'X-CSRF-TOKEN': csrfToken } : {}
            });
            if (!response.ok) {
                return false;
            }
            const payload = await response.json();
            const token = payload?.data?.accessToken;
            if (!token) {
                return false;
            }
            setSession(token, getUser());
            return true;
        } catch {
            return false;
        }
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
            return request('/me', { method: 'PATCH', body: payload });
        },

        async changePassword(currentPassword, newPassword) {
            return request('/me/change-password', { method: 'POST', body: { currentPassword, newPassword } });
        },

        async addresses() {
            return request('/me/addresses');
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
