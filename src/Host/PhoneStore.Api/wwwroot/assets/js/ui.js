/* ==========================================================================
   PhoneStore Storefront — UI helpers
   ========================================================================== */

const UI = (() => {
    const app = () => document.getElementById('app');

    /* ---------------- Formatting ---------------- */

    function formatCurrency(value) {
        return new Intl.NumberFormat('vi-VN').format(Math.round(Number(value) || 0)) + ' đ';
    }

    function formatDate(value) {
        if (!value) return '';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return '';
        return date.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    /* ---------------- Toast ---------------- */

    function toast(message, type = 'info') {
        const stack = document.getElementById('toast-stack');
        if (!stack) return;
        const node = document.createElement('div');
        node.className = `toast toast--${type}`;
        node.textContent = message;
        stack.appendChild(node);
        setTimeout(() => {
            node.style.opacity = '0';
            node.style.transform = 'translateY(8px)';
            node.style.transition = 'all .2s';
            setTimeout(() => node.remove(), 220);
        }, 3200);
    }

    /* ---------------- Rendering helpers ---------------- */

    function render(html) {
        app().innerHTML = html;
        window.scrollTo({ top: 0, behavior: 'instant' in window ? 'instant' : 'auto' });
    }

    function loading(message = 'Đang tải dữ liệu...') {
        const card = `<div class="skeleton-card"><div class="skeleton skeleton--media"></div><div class="skeleton skeleton--line"></div><div class="skeleton skeleton--line skeleton--short"></div><div class="skeleton skeleton--price"></div></div>`;
        render(`<div class="container" role="status" aria-live="polite">
            <span class="sr-only">${escapeHtml(message)}</span>
            <div class="skeleton skeleton--title"></div>
            <div class="product-grid">${card.repeat(8)}</div>
        </div>`);
    }

    function errorState(message) {
        render(`
            <div class="container">
                <div class="info-page" style="text-align:center">
                    <h2>Rất tiếc, đã có lỗi xảy ra</h2>
                    <p>${escapeHtml(message)}</p>
                    <a class="btn btn--primary" href="#/">Về trang chủ</a>
                </div>
            </div>`);
    }

    function breadcrumb(items) {
        const parts = items.map((item, index) => {
            const isLast = index === items.length - 1;
            return isLast
                ? `<span>${escapeHtml(item.label)}</span>`
                : `<a href="${item.href}">${escapeHtml(item.label)}</a><span>/</span>`;
        });
        return `<nav class="container breadcrumb">${parts.join(' ')}</nav>`;
    }

    function emptyState(icon, title, message, actionHtml = '') {
        return `
            <div class="empty-state">
                <div class="empty-state__icon">${icon}</div>
                <h3>${escapeHtml(title)}</h3>
                <p>${escapeHtml(message)}</p>
                ${actionHtml}
            </div>`;
    }

    function mockBanner(text = 'Tính năng này đang được dựng mockup, chưa có endpoint tương ứng trên backend.') {
        return `<div class="mock-banner"><span>⚠️</span><span><b>Mockup:</b> ${escapeHtml(text)}</span></div>`;
    }

    function statusChip(status) {
        const map = {
            PENDING: 'pending',
            CONFIRMED: 'confirmed',
            SHIPPING: 'shipping',
            DELIVERED: 'delivered',
            CANCELLED: 'cancelled',
            DELIVERY_FAILED: 'failed'
        };
        const labels = {
            PENDING: 'Chờ xác nhận',
            CONFIRMED: 'Đã xác nhận',
            SHIPPING: 'Đang giao',
            DELIVERED: 'Đã giao',
            CANCELLED: 'Đã hủy',
            DELIVERY_FAILED: 'Giao thất bại'
        };
        return `<span class="status-chip status-chip--${map[status] || 'pending'}">${labels[status] || status}</span>`;
    }

    function placeholderThumb(product, large = false) {
        const name = (product.name || '?').trim();
        let hash = 0;
        for (let i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) % 360;
        const brand = escapeHtml(product.brandName || '');
        return `<div class="product-card__thumb${large ? ' product-card__thumb--lg' : ''}" style="--hue:${hash}" role="img" aria-label="${escapeHtml(name)}">
            <svg viewBox="0 0 48 48" aria-hidden="true"><rect x="14" y="5" width="20" height="38" rx="4.5" fill="none" stroke="currentColor" stroke-width="2.4"/><path d="M21 9h6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/><circle cx="24" cy="37.5" r="1.6" fill="currentColor"/></svg>
            ${brand ? `<span>${brand}</span>` : ''}
        </div>`;
    }

    function productThumb(product, size = '') {
        if (product.mainImageUrl) {
            return `<img src="${escapeHtml(product.mainImageUrl)}" alt="${escapeHtml(product.name)}" loading="lazy" decoding="async"
                data-fallback-name="${escapeHtml(product.name || '')}" data-fallback-brand="${escapeHtml(product.brandName || '')}" ${size}>`;
        }
        return placeholderThumb(product);
    }

    // Ảnh lỗi (404, hỏng) → thay bằng placeholder thay vì icon ảnh vỡ.
    document.addEventListener('error', (event) => {
        const img = event.target;
        if (!(img instanceof HTMLImageElement) || !img.dataset.fallbackName) return;
        const holder = document.createElement('div');
        holder.innerHTML = placeholderThumb({ name: img.dataset.fallbackName, brandName: img.dataset.fallbackBrand });
        img.replaceWith(holder.firstElementChild);
    }, true);

    function productCard(product) {
        const price = product.minPrice || 0;
        const maxPrice = product.maxPrice || price;
        const hasRange = maxPrice > price;
        const discount = Math.round((1 - price / Math.max(maxPrice, price)) * 100);
        // API danh sách không trả tồn kho → stock = null (không rõ), tránh hiện sai "Tạm hết hàng".
        const hasStockInfo = product.totalStock != null || (product.variants || []).length > 0;
        const stock = hasStockInfo
            ? (product.totalStock ?? product.variants.reduce((sum, variant) => sum + (variant.stockQuantity || 0), 0))
            : null;
        const rating = Mock.ratingFor(product);

        const badges = [];
        if (discount > 0) badges.push(`<span class="badge">-${discount}%</span>`);
        if (product.isNew) badges.push('<span class="badge badge--new">Mới</span>');
        if (stock !== null && stock > 0 && stock < 20) badges.push('<span class="badge badge--hot">Sắp hết</span>');

        const isWishlisted = Mock.getWishlist().includes(product.id);

        return `
            <article class="product-card">
                <a class="product-card__media" href="#/san-pham/${product.id}">
                    ${productThumb(product)}
                    ${badges.length ? `<div class="product-card__badges">${badges.join('')}</div>` : ''}
                </a>
                <button class="product-card__wish ${isWishlisted ? 'is-active' : ''}" type="button"
                        data-wishlist="${product.id}" aria-label="Yêu thích sản phẩm">${isWishlisted ? '♥' : '♡'}</button>
                <div class="product-card__body">
                    <a class="product-card__name" href="#/san-pham/${product.id}">${escapeHtml(product.name)}</a>
                    <div class="product-card__meta">${escapeHtml(product.brandName || '')} · ${escapeHtml(product.categoryName || '')}</div>
                    <div class="product-card__rating">
                        <span class="stars">${'★'.repeat(rating.stars)}${'☆'.repeat(5 - rating.stars)}</span>
                        <span>${rating.count} đánh giá</span>
                    </div>
                    <div class="product-card__price">
                        ${formatCurrency(price)}
                        ${hasRange ? `<del>${formatCurrency(maxPrice)}</del>` : ''}
                    </div>
                    ${stock === null ? '' : `<div class="product-card__note${stock > 0 ? '' : ' product-card__note--out'}">${stock > 0 ? `Còn ${stock} sản phẩm` : 'Tạm hết hàng'}</div>`}
                    <div class="product-card__actions">
                        <button class="btn btn--primary btn--sm" data-add-to-cart="${product.id}" data-variants='${escapeHtml(JSON.stringify(product.variants || []))}'>Thêm vào giỏ</button>
                        <button class="btn btn--ghost btn--sm" data-compare="${product.id}">So sánh</button>
                    </div>
                </div>
            </article>`;
    }

    function productGrid(products) {
        if (!products || products.length === 0) {
            return emptyState('🔍', 'Không tìm thấy sản phẩm', 'Hãy thử thay đổi bộ lọc hoặc từ khóa tìm kiếm.');
        }
        return `<div class="product-grid">${products.map(productCard).join('')}</div>`;
    }

    /* ---------------- Banner carousel ---------------- */

    function bannerCarousel(banners) {
        const slides = banners.map(banner => `
            <div class="carousel__slide">
                <div class="banner ${banner.theme}">
                    <span class="banner__eyebrow">${escapeHtml(banner.eyebrow)}</span>
                    <h2 class="banner__title">${escapeHtml(banner.title)}</h2>
                    <p class="banner__desc">${escapeHtml(banner.description)}</p>
                    <a class="btn btn--primary" href="${banner.href}" style="align-self:flex-start;padding:11px 22px;font-size:14.5px">${escapeHtml(banner.cta)}</a>
                </div>
            </div>`).join('');

        const dots = banners.map((banner, index) => `
            <button type="button" class="${index === 0 ? 'is-active' : ''}" data-slide="${index}" aria-label="Banner ${index + 1}"></button>`).join('');

        return `
            <div class="carousel" id="banner-carousel">
                <div class="carousel__track" id="banner-track">${slides}</div>
                <div class="carousel__dots">${dots}</div>
                <div class="carousel__arrows">
                    <button type="button" data-slide-prev aria-label="Banner trước">‹</button>
                    <button type="button" data-slide-next aria-label="Banner sau">›</button>
                </div>
            </div>`;
    }

    function bindCarousel(carouselId, intervalMs = 6000) {
        const carousel = document.getElementById(carouselId);
        if (!carousel) return;

        const track = carousel.querySelector('.carousel__track');
        const slides = Array.from(carousel.querySelectorAll('.carousel__slide'));
        const dots = Array.from(carousel.querySelectorAll('[data-slide]'));
        if (slides.length === 0) return;

        let currentIndex = 0;
        let timer = null;

        function goTo(index) {
            currentIndex = (index + slides.length) % slides.length;
            track.style.transform = `translateX(-${currentIndex * 100}%)`;
            dots.forEach((dot, dotIndex) => dot.classList.toggle('is-active', dotIndex === currentIndex));
        }

        function restartTimer() {
            if (timer) clearInterval(timer);
            timer = setInterval(() => goTo(currentIndex + 1), intervalMs);
        }

        dots.forEach(dot => dot.addEventListener('click', () => {
            goTo(Number(dot.dataset.slide));
            restartTimer();
        }));
        carousel.querySelector('[data-slide-next]').addEventListener('click', () => { goTo(currentIndex + 1); restartTimer(); });
        carousel.querySelector('[data-slide-prev]').addEventListener('click', () => { goTo(currentIndex - 1); restartTimer(); });

        goTo(0);
        restartTimer();
    }

    /* ---------------- Countdown flash sale ---------------- */

    function countdownMarkup() {
        return `
            <span class="countdown">
                <span class="countdown__box" data-countdown="hours">00</span>:
                <span class="countdown__box" data-countdown="minutes">00</span>:
                <span class="countdown__box countdown__box--brand" data-countdown="seconds">00</span>
            </span>`;
    }

    function bindCountdown(target, endTime) {
        const nodes = target.querySelectorAll('[data-countdown]');
        if (nodes.length === 0) return;

        function tick() {
            const remaining = Math.max(0, endTime - Date.now());
            const totalSeconds = Math.floor(remaining / 1000);
            const hours = Math.floor(totalSeconds / 3600);
            const minutes = Math.floor((totalSeconds % 3600) / 60);
            const seconds = totalSeconds % 60;
            const values = {
                hours: String(hours).padStart(2, '0'),
                minutes: String(minutes).padStart(2, '0'),
                seconds: String(seconds).padStart(2, '0')
            };
            nodes.forEach(node => { node.textContent = values[node.dataset.countdown]; });
        }

        tick();
        setInterval(tick, 1000);
    }

    /* ---------------- Flash sale card ---------------- */

    function flashCard(item) {
        const soldPercent = Math.min(100, Math.round((item.sold / item.total) * 100));
        return `
            <article class="flash-card">
                <span class="flash-card__badge">${escapeHtml(item.badge)}</span>
                <div class="flash-card__media">${placeholderThumb({ name: item.name, brandName: item.name.split(' ')[0] })}</div>
                <div class="flash-card__body">
                    <div class="flash-card__name">${escapeHtml(item.name)}</div>
                    <div class="flash-card__price">
                        <span>${formatCurrency(item.price)}</span>
                        <del>${formatCurrency(item.originalPrice)}</del>
                    </div>
                    <div class="flash-card__progress"><span style="width:${soldPercent}%"></span></div>
                    <div class="flash-card__sold">Đã bán ${item.sold}/${item.total}</div>
                </div>
            </article>`;
    }

    function flashSaleSection(options = {}) {
        const sale = Mock.flashSale;
        const endTime = Date.now() + sale.endsAtHours * 3600 * 1000;

        return `
            <section class="flash-sale" id="flash-sale">
                <div class="flash-sale__head">
                    <h3 class="flash-sale__title">⚡ ${escapeHtml(sale.title)}</h3>
                    ${countdownMarkup(endTime)}
                    ${options.showLink !== false ? '<a href="#/khuyen-mai" style="margin-left:auto;color:var(--brand);font-size:13px;font-weight:600">Xem tất cả →</a>' : ''}
                </div>
                <div class="flash-sale__track">
                    ${sale.items.map(flashCard).join('')}
                </div>
            </section>`;
    }

    /* ---------------- Service guarantee strip ---------------- */

    function serviceStrip() {
        return `
            <section class="service-strip">
                ${Mock.serviceGuarantees.map(item => `
                    <div class="service-tile">
                        <span class="service-tile__icon">${item.icon}</span>
                        <span>
                            <b>${escapeHtml(item.title)}</b>
                            <span>${escapeHtml(item.description)}</span>
                        </span>
                    </div>`).join('')}
            </section>`;
    }

    /* ---------------- Brand strip ---------------- */

    function brandStrip() {
        return `
            <section class="brand-strip">
                ${Mock.featuredBrands.map(brand => `
                    <a class="brand-tile" href="#/san-pham?search=${encodeURIComponent(brand.keyword)}">
                        ${escapeHtml(brand.name)}
                        <span>${escapeHtml(brand.keyword)}</span>
                    </a>`).join('')}
            </section>`;
    }

    /* ---------------- Modal ---------------- */

    function openModal(id) {
        const modal = document.getElementById(id);
        if (modal) modal.hidden = false;
    }

    function closeModal(id) {
        const modal = document.getElementById(id);
        if (modal) modal.hidden = true;
    }

    /* ---------------- Address helper ---------------- */

    async function pickAddress(addresses, onPick) {
        if (!addresses || addresses.length === 0) {
            onPick(null);
            return;
        }
        if (addresses.length === 1) {
            onPick(addresses[0]);
            return;
        }

        const modal = document.createElement('div');
        modal.className = 'modal';
        modal.innerHTML = `
            <div class="modal__backdrop" data-close></div>
            <div class="modal__panel" role="dialog" aria-modal="true">
                <button class="modal__close" type="button" data-close aria-label="Đóng">&times;</button>
                <h3 class="modal__title">Chọn địa chỉ nhận hàng</h3>
                <div class="form" style="margin-top:12px">
                    ${addresses.map((address, index) => `
                        <label class="option" style="display:flex;gap:10px;align-items:flex-start;padding:12px">
                            <input type="radio" name="address-choice" value="${index}" ${index === 0 ? 'checked' : ''} style="margin-top:3px">
                            <span>
                                <b>${escapeHtml(address.receiverName)}</b> · ${escapeHtml(address.receiverPhone)}<br>
                                <small style="color:var(--ink-soft)">${escapeHtml(address.addressText)}</small>
                            </span>
                        </label>`).join('')}
                    <button class="btn btn--primary btn--block" type="button" data-confirm>Tiếp tục</button>
                </div>`;

        document.body.appendChild(modal);
        modal.querySelectorAll('[data-close]').forEach(node => node.addEventListener('click', () => modal.remove()));
        modal.querySelector('[data-confirm]').addEventListener('click', () => {
            const selected = modal.querySelector('input[name="address-choice"]:checked');
            modal.remove();
            onPick(addresses[Number(selected.value)]);
        });
    }

    /* ---------------- Prompt ---------------- */

    function prompt(title, label, defaultValue = '') {
        return new Promise(resolve => {
            const modal = document.createElement('div');
            modal.className = 'modal';
            modal.innerHTML = `
                <div class="modal__backdrop" data-close></div>
                <div class="modal__panel modal__panel--sm" role="dialog" aria-modal="true">
                    <button class="modal__close" type="button" data-close aria-label="Đóng">&times;</button>
                    <h3 class="modal__title">${escapeHtml(title)}</h3>
                    <form class="form" style="margin-top:12px">
                        <label class="field">
                            <span class="field__label">${escapeHtml(label)}</span>
                            <textarea name="value" rows="3" required>${escapeHtml(defaultValue)}</textarea>
                        </label>
                        <button class="btn btn--primary btn--block" type="submit">Xác nhận</button>
                    </form>
                </div>`;

            document.body.appendChild(modal);
            const close = () => { modal.remove(); resolve(null); };
            modal.querySelectorAll('[data-close]').forEach(node => node.addEventListener('click', close));
            modal.querySelector('form').addEventListener('submit', event => {
                event.preventDefault();
                const value = modal.querySelector('textarea[name="value"]').value.trim();
                modal.remove();
                resolve(value || null);
            });
        });
    }

    return {
        formatCurrency,
        formatDate,
        escapeHtml,
        toast,
        render,
        loading,
        errorState,
        breadcrumb,
        emptyState,
        mockBanner,
        statusChip,
        productThumb,
        placeholderThumb,
        productCard,
        productGrid,
        bannerCarousel,
        bindCarousel,
        countdownMarkup,
        bindCountdown,
        flashCard,
        flashSaleSection,
        serviceStrip,
        brandStrip,
        openModal,
        closeModal,
        pickAddress,
        prompt
    };
})();
