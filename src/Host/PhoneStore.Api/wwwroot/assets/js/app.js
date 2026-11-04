/* ==========================================================================
   PhoneStore Storefront — App bootstrap & routing
   ========================================================================== */

const App = (() => {
    const compareList = [];
    // Trang người dùng đang xem trước khi vào đăng nhập/đăng ký → đăng nhập xong quay lại đúng chỗ.
    let returnTo = '#/';
    const AUTH_PATHS = ['/dang-nhap', '/dang-ky'];

    /* ---------------- Routing ---------------- */

    function parseHash() {
        const raw = window.location.hash.replace(/^#/, '') || '/';
        const [pathPart, queryPart] = raw.split('?');
        const segments = pathPart.split('/').filter(Boolean);
        const query = new URLSearchParams(queryPart || '');
        return { path: '/' + segments.join('/'), segments, query };
    }

    const routes = [
        { pattern: '/', handler: Pages.home },
        { pattern: '/san-pham', handler: Pages.productList },
        { pattern: '/danh-muc/:id', handler: (params) => {
            const next = new URLSearchParams(params.query);
            next.set('categoryId', params.segments[1]);
            next.set('page', '1');
            window.location.hash = `#/san-pham?${next.toString()}`;
            return Promise.resolve();
        } },
        { pattern: '/san-pham/:id', handler: Pages.productDetail },
        { pattern: '/gio-hang', handler: Pages.cart },
        { pattern: '/thanh-toan', handler: Pages.checkout },
        { pattern: '/don-hang', handler: Pages.orders },
        { pattern: '/don-hang/:id', handler: Pages.orderDetail },
        { pattern: '/tai-khoan', handler: Pages.account },
        { pattern: '/dang-nhap', handler: Pages.login },
        { pattern: '/dang-ky', handler: Pages.register },
        { pattern: '/khuyen-mai', handler: Pages.promotionsPage },
        { pattern: '/flash-sale', handler: Pages.flashSalePage },
        { pattern: '/tin-tuc', handler: Pages.newsPage },
        { pattern: '/cau-hoi', handler: Pages.faqPage },
        { pattern: '/tra-gop', handler: Pages.installmentPage },
        { pattern: '/cua-hang', handler: Pages.storesPage },
        { pattern: '/so-sanh', handler: Pages.comparePage },
        { pattern: '/chinh-sach/:id', handler: Pages.policyPage },
        { pattern: '/tra-cuu-don-hang', handler: Pages.orderLookup },
        { pattern: '/yeu-thich', handler: Pages.wishlistPage }
    ];

    async function route() {
        const { path, segments, query } = parseHash();
        if (!AUTH_PATHS.includes(path)) {
            returnTo = window.location.hash || '#/';
        }

        // Static informational pages
        const staticPages = {
            '/gioi-thieu': ['Giới thiệu PhoneStore', 'PhoneStore là hệ thống bán lẻ điện thoại và phụ kiện trực tuyến, đồ án Phát Triển Ứng Dụng Phía Máy Chủ Nâng Cao (HP 608819) của Nhóm 2.'],
            '/lien-he': ['Liên hệ', 'Tổng đài 1800 1234 hoạt động 08:00 - 21:30. Email: cskh@phonestore.vn. Trụ sở: 123 Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh.'],
            '/tuy-dung': ['Tuyển dụng', 'Chúng tôi đang cần nhân viên bán hàng, nhân viên kho và kỹ thuật viên. Gửi CV về cskh@phonestore.vn để ứng tuyển.'],
            '/huong-dan': null,
            '/thanh-toan': null,
            '/van-chuyen': null,
            '/bao-hanh': null,
            '/doi-tra': null
        };

        if (staticPages[path] === null) {
            await Pages.policyPage({ get: () => path.replace('/', ''), segments, query });
            return;
        }

        if (staticPages[path]) {
            await Pages.simpleInfoPage(staticPages[path][0], staticPages[path][1]);
            return;
        }

        const match = routes
            .map(route => ({ route, params: matchRoute(route.pattern, segments) }))
            .find(entry => entry.params);

        if (!match) {
            UI.render(`
                <div class="container">
                    ${UI.emptyState('🧭', 'Không tìm thấy trang', 'Đường dẫn bạn truy cập không tồn tại.',
                        '<a class="btn btn--primary" href="#/">Về trang chủ</a>')}
                </div>`);
            return;
        }

        try {
            await match.route.handler({
                // Ưu tiên tham số trên đường dẫn (/san-pham/:id), sau đó mới tới query string.
                get: key => (match.params.values[key] !== undefined ? match.params.values[key] : query.get(key)),
                segments: match.params.segments,
                query
            });
        } catch (error) {
            console.error(error);
            if (error && error.status === 401) {
                // Phiên hết hạn giữa chừng → đưa về đăng nhập, sau đó quay lại trang này.
                UI.toast('Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.', 'error');
                window.location.hash = '#/dang-nhap';
                return;
            }
            UI.errorState(error.message || 'Đã xảy ra lỗi khi tải trang.');
        }
    }

    /** Địa chỉ cần quay lại sau khi đăng nhập/đăng ký (mặc định trang chủ). */
    function afterLoginHash() {
        return returnTo && !AUTH_PATHS.some(path => returnTo.startsWith('#' + path)) ? returnTo : '#/';
    }

    function matchRoute(pattern, segments) {
        const patternSegments = pattern.split('/').filter(Boolean);
        if (patternSegments.length !== segments.length) return null;

        const values = {};
        for (let index = 0; index < patternSegments.length; index += 1) {
            const patternSegment = patternSegments[index];
            if (patternSegment.startsWith(':')) {
                values[patternSegment.slice(1)] = segments[index];
            } else if (patternSegment !== segments[index]) {
                return null;
            }
        }
        return { segments, values };
    }

    /* ---------------- Header / layout ---------------- */

    async function loadCategories() {
        const bar = document.getElementById('category-bar');
        if (!bar) return;
        try {
            const categories = await Api.catalog.categories();
            bar.innerHTML = [
                '<a class="category-link category-link--all" href="#/san-pham">☰ Tất cả sản phẩm</a>',
                ...categories.map(category => `
                    <a class="category-link" href="#/danh-muc/${category.id}">${UI.escapeHtml(category.name)}</a>`),
                '<a class="category-link" href="#/flash-sale">⚡ Flash sale</a>',
                '<a class="category-link" href="#/tra-gop">Trả góp 0%</a>'
            ].join('');
        } catch {
            bar.innerHTML = '<a class="category-link" href="#/san-pham">Tất cả sản phẩm</a>';
        }
    }

    async function refreshCartBadge() {
        const badge = document.getElementById('cart-count');
        const totalLabel = document.getElementById('cart-total');
        if (!badge || !totalLabel) return;

        if (!Api.isAuthenticated()) {
            badge.hidden = true;
            totalLabel.textContent = '0 đ';
            return;
        }

        try {
            const cart = await Api.cart.get();
            badge.textContent = cart.totalQuantity || 0;
            badge.hidden = !cart.totalQuantity;
            totalLabel.textContent = UI.formatCurrency(cart.subtotal || 0);
        } catch {
            badge.hidden = true;
            totalLabel.textContent = '0 đ';
        }
    }

    function syncAuthUi() {
        const user = Api.getUser();
        document.querySelectorAll('[data-auth-only]').forEach(node => { node.hidden = !user; });
        document.querySelectorAll('[data-guest-only]').forEach(node => { node.hidden = Boolean(user); });
        document.querySelectorAll('[data-user-name]').forEach(node => { node.textContent = user ? (user.fullName || user.email) : ''; });
        const label = document.getElementById('header-account-label');
        const account = document.getElementById('header-account');
        if (label) label.textContent = user ? 'Tài khoản' : 'Đăng nhập';
        if (account) account.setAttribute('href', user ? '#/tai-khoan' : '#/dang-nhap');
    }

    async function logout() {
        try {
            await Api.auth.logout();
        } catch (error) {
            // Dù server lỗi, phiên phía trình duyệt vẫn đã bị xoá (finally trong Api.auth.logout).
            console.error(error);
        }
        UI.toast('Bạn đã đăng xuất.', 'success');
        if (window.location.hash === '#/' || window.location.hash === '') {
            await route();
        } else {
            window.location.hash = '#/';
        }
    }

    /* ---------------- Quick login ---------------- */

    function openQuickLogin() {
        UI.openModal('quick-login-modal');
    }

    function bindQuickLogin() {
        const modal = document.getElementById('quick-login-modal');
        if (!modal) return;

        modal.querySelectorAll('[data-close-modal]').forEach(node => node.addEventListener('click', () => UI.closeModal('quick-login-modal')));
        modal.querySelectorAll('[data-demo-email]').forEach(button => button.addEventListener('click', () => {
            const form = document.getElementById('quick-login-form');
            form.email.value = button.dataset.demoEmail;
            form.password.value = button.dataset.demoPassword;
        }));

        document.getElementById('quick-login-form').addEventListener('submit', async event => {
            event.preventDefault();
            const form = event.target;
            try {
                await Api.auth.login(form.email.value.trim(), form.password.value);
                UI.closeModal('quick-login-modal');
                UI.toast('Đăng nhập thành công.', 'success');
                form.reset();
                await refreshCartBadge();
                route();
            } catch (error) {
                UI.toast(error.message, 'error');
            }
        });
    }

    /* ---------------- Search ---------------- */

    function bindSearch() {
        const input = document.getElementById('global-search');
        const suggest = document.getElementById('search-suggest');
        if (!input || !suggest) return;

        let timer = null;

        async function renderSuggestions() {
            const keyword = input.value.trim();
            if (keyword.length < 2) {
                suggest.hidden = true;
                suggest.innerHTML = '';
                return;
            }
            try {
                const result = await Api.catalog.products({ search: keyword, page: 1, pageSize: 6 });
                const items = result.items || [];
                if (items.length === 0) {
                    suggest.hidden = true;
                    return;
                }
                suggest.innerHTML = items.map(product => `
                    <a href="#/san-pham/${product.id}">
                        <span>${UI.escapeHtml(product.name)}</span>
                        <small>${UI.formatCurrency(product.minPrice)}</small>
                    </a>`).join('');
                suggest.hidden = false;
            } catch {
                suggest.hidden = true;
            }
        }

        input.addEventListener('input', () => {
            clearTimeout(timer);
            timer = setTimeout(renderSuggestions, 250);
        });

        input.addEventListener('keydown', event => {
            if (event.key === 'Enter') {
                event.preventDefault();
                suggest.hidden = true;
                const keyword = input.value.trim();
                window.location.hash = keyword ? `#/san-pham?search=${encodeURIComponent(keyword)}` : '#/san-pham';
            }
        });

        document.addEventListener('click', event => {
            if (!suggest.contains(event.target) && event.target !== input) {
                suggest.hidden = true;
            }
        });
    }

    /* ---------------- Global events ---------------- */

    function bindGlobalEvents() {
        const scrollTopButton = document.getElementById('btn-scroll-top');
        if (scrollTopButton) {
            scrollTopButton.addEventListener('click', () => {
                window.scrollTo({ top: 0, behavior: 'smooth' });
            });
        }

        // Add-to-cart từ card sản phẩm
        document.addEventListener('click', async event => {
            if (event.target.closest('[data-logout]')) {
                event.preventDefault();
                await logout();
                return;
            }
            const addButton = event.target.closest('[data-add-to-cart]');
            if (addButton) {
                event.preventDefault();
                const productId = Number(addButton.dataset.addToCart);
                let variants = [];
                try {
                    variants = JSON.parse(addButton.dataset.variants || '[]');
                } catch {
                    variants = [];
                }
                if (addButton.disabled) return;
                const originalLabel = addButton.textContent;
                addButton.disabled = true;
                addButton.textContent = 'Đang thêm...';
                try {
                    const product = await Api.catalog.product(productId).catch(() => null);
                    const variant = variants[0] || (product && product.variants && product.variants[0]);
                    await Pages.addToCart(variant, product || { id: productId, name: '' }, 1);
                } finally {
                    addButton.disabled = false;
                    addButton.textContent = originalLabel;
                }
                return;
            }

            const compareButton = event.target.closest('[data-compare]');
            if (compareButton) {
                event.preventDefault();
                const productId = Number(compareButton.dataset.compare);
                compareProducts({ id: productId });
                return;
            }

            const wishlistButton = event.target.closest('[data-wishlist]');
            if (wishlistButton) {
                event.preventDefault();
                const productId = Number(wishlistButton.dataset.wishlist);
                const list = Mock.toggleWishlist(productId);
                const isActive = list.includes(productId);
                wishlistButton.classList.toggle('is-active', isActive);
                wishlistButton.textContent = isActive ? '♥' : '♡';
                UI.toast(isActive ? 'Đã thêm vào danh sách yêu thích.' : 'Đã bỏ khỏi danh sách yêu thích.', 'success');
            }
        });

        document.addEventListener('phonestore:auth-changed', () => {
            syncAuthUi();
            refreshCartBadge();
        });
    }

    function compareProducts(product) {
        const productId = product.id;
        if (!compareList.includes(productId)) {
            compareList.push(productId);
        }
        renderCompareBar();
        window.location.hash = `#/so-sanh?ids=${compareList.join(',')}`;
        return compareList;
    }

    function renderCompareBar() {
        const existing = document.getElementById('compare-bar');
        if (existing) existing.remove();
        if (compareList.length === 0) return;

        const bar = document.createElement('div');
        bar.className = 'compare-bar';
        bar.id = 'compare-bar';
        bar.innerHTML = `
            <span>Đã chọn <b>${compareList.length}</b> sản phẩm để so sánh</span>
            <span class="compare-bar__dots">${compareList.map(id => `<span>#${id}</span>`).join('')}</span>
            <a class="btn btn--primary btn--sm" href="#/so-sanh?ids=${compareList.join(',')}">So sánh ngay</a>
            <button class="btn btn--ghost btn--sm" type="button" data-compare-clear>×</button>`;
        document.body.appendChild(bar);

        bar.querySelector('[data-compare-clear]').addEventListener('click', () => {
            compareList.length = 0;
            renderCompareBar();
        });
    }

    /* ---------------- Init ---------------- */

    async function init() {
        await Api.restoreSession();
        syncAuthUi();
        bindSearch();
        bindQuickLogin();
        bindGlobalEvents();
        loadCategories();
        refreshCartBadge();

        window.addEventListener('hashchange', route);
        await route();
    }

    document.addEventListener('DOMContentLoaded', init);

    return { init, route, refreshCartBadge, openQuickLogin, compareProducts, afterLoginHash, logout };
})();
