/* ==========================================================================
   PhoneStore Storefront — Page views
   Mỗi hàm render trả về HTML string; app.js điều phối việc render + bind event.
   ========================================================================== */

const Pages = (() => {
    const escapeHtml = UI.escapeHtml;
    const formatCurrency = UI.formatCurrency;
    const formatDate = UI.formatDate;

    /* ================= HOME ================= */

    async function home() {
        UI.loading('Đang tải trang chủ...');

        const [productResult, categories, brands] = await Promise.all([
            Api.catalog.products({ page: 1, pageSize: 10, sortBy: 'newest' }).catch(() => ({ items: [] })),
            Api.catalog.categories().catch(() => []),
            Api.catalog.brands().catch(() => [])
        ]);

        const products = productResult.items || [];

        const categoryTiles = categories.length
            ? categories.slice(0, 6).map((category, index) => `
                <a class="promo-tile ${index % 4 === 1 ? 'promo-tile--blue' : index % 4 === 2 ? 'promo-tile--green' : index % 4 === 3 ? 'promo-tile--amber' : ''}"
                   href="#/danh-muc/${category.id}">
                    <b>${escapeHtml(category.name)}</b>
                    <span>Xem sản phẩm ${escapeHtml(category.name).toLowerCase()}</span>
                </a>`).join('')
            : `
                <a class="promo-tile" href="#/san-pham"><b>Điện thoại</b><span>Smartphone chính hãng</span></a>
                <a class="promo-tile promo-tile--blue" href="#/san-pham"><b>Tablet</b><span>iPad và máy tính bảng</span></a>
                <a class="promo-tile promo-tile--green" href="#/san-pham"><b>Laptop</b><span>Máy tính xách tay</span></a>
                <a class="promo-tile promo-tile--amber" href="#/san-pham"><b>Phụ kiện</b><span>Sạc, ốp lưu, tai nghe</span></a>`;

        const hotProducts = products.slice(0, 4);
        const newProducts = products.length > 4 ? products.slice(4, 10) : products;

        UI.render(`
            <section class="section" style="padding-top:18px">
                <div class="container">
                    ${UI.bannerCarousel(Mock.banners)}
                </div>
            </section>

            <section class="section" style="padding-top:0">
                <div class="container">
                    ${UI.flashSaleSection()}
                </div>
            </section>

            <section class="section" style="padding-top:0">
                <div class="container">
                    ${UI.serviceStrip()}
                </div>
            </section>

            <section class="section" style="padding-top:0">
                <div class="container">
                    <div class="section__head">
                        <h2>Danh mục sản phẩm</h2>
                        <p>Chọn nhanh theo nhóm sản phẩm bạn quan tâm</p>
                        <a href="#/san-pham">Xem tất cả →</a>
                    </div>
                    <div class="promo-grid">${categoryTiles}</div>
                </div>
            </section>

            <section class="section" style="padding-top:0">
                <div class="container">
                    <div class="section__head">
                        <h2>Thương hiệu nổi bật</h2>
                        <p>Tìm nhanh theo thương hiệu bạn yêu thích</p>
                    </div>
                    ${UI.brandStrip()}
                </div>
            </section>

            ${hotProducts.length ? `
            <section class="section" style="padding-top:0">
                <div class="container">
                    <div class="section__head">
                        <h2>Sản phẩm bán chạy</h2>
                        <p>Được quan tâm nhiều nhất tuần này</p>
                        <a href="#/san-pham">Xem tất cả →</a>
                    </div>
                    ${UI.productGrid(hotProducts)}
                </div>
            </section>` : ''}

            <section class="section" style="padding-top:0">
                <div class="container">
                    <div class="section__head">
                        <h2>Sản phẩm mới về</h2>
                        <p>Cập nhật mỗi ngày</p>
                        <a href="#/san-pham?sortBy=newest">Xem tất cả →</a>
                    </div>
                    ${UI.productGrid(newProducts)}
                </div>
            </section>

            <section class="section" style="padding-bottom:44px">
                <div class="container">
                    <div class="section__head">
                        <h2>Tiện ích dành cho bạn</h2>
                        <p>Các chức năng hỗ trợ mua sắm</p>
                    </div>
                    <div class="promo-grid">
                        <a class="promo-tile" href="#/tra-gop"><b>💳 Trả góp 0%</b><span>Trả trước 0 đồng, duyệt online</span></a>
                        <a class="promo-tile promo-tile--blue" href="#/khuyen-mai"><b>🎁 Mã giảm giá</b><span>Nhập mã ưu đãi tại bước thanh toán</span></a>
                        <a class="promo-tile promo-tile--green" href="#/cua-hang"><b>📍 Cửa hàng gần bạn</b><span>40 chi nhánh trên toàn quốc</span></a>
                        <a class="promo-tile promo-tile--amber" href="#/tra-cuu-don-hang"><b>🚚 Theo dõi đơn</b><span>Tra cứu trạng thái vận chuyển</span></a>
                    </div>
                </div>
            </section>

            <section class="section" style="padding-top:0;padding-bottom:44px">
                <div class="container">
                    <div class="section__head">
                        <h2>Tin tức &amp; kinh nghiệm</h2>
                        <p>Cập nhật mẹo chọn máy và tin công nghệ</p>
                    </div>
                    ${UI.mockBanner('Trang tin tức chưa có endpoint và bảng dữ liệu riêng, nội dung dưới đây là mô phỏng.')}
                    <div class="news-grid">
                        ${Mock.newsArticles.map(article => `
                            <a class="news-card" href="#/huong-dan">
                                <span class="news-card__tag">${escapeHtml(article.tag)}</span>
                                <b>${escapeHtml(article.title)}</b>
                                <span class="news-card__meta">${escapeHtml(article.date)} · ${article.readMinutes} phút đọc</span>
                            </a>`).join('')}
                    </div>
                </div>
            </section>

            <section class="section" style="padding-top:0;padding-bottom:52px">
                <div class="container">
                    <div class="section__head">
                        <h2>Câu hỏi thường gặp</h2>
                        <p>Giải đáp nhanh cho khách hàng</p>
                    </div>
                    ${UI.mockBanner('Câu hỏi thường gặp chưa có endpoint, dữ liệu dưới đây là mô phỏng.')}
                    <div class="faq-list">
                        ${Mock.faqs.map(faq => `
                            <details class="faq-item">
                                <summary>${escapeHtml(faq.question)}</summary>
                                <p>${escapeHtml(faq.answer)}</p>
                            </details>`).join('')}
                    </div>
                </div>
            </section>`);

        UI.bindCarousel('banner-carousel');
        UI.bindCountdown(document.getElementById('flash-sale'), Date.now() + Mock.flashSale.endsAtHours * 3600 * 1000);
    }

    /* ================= PRODUCT LIST ================= */

    async function productList(params) {
        UI.loading('Đang tải danh sách sản phẩm...');

        const query = {
            search: params.get('search') || '',
            categoryId: params.get('categoryId') || '',
            brandId: params.get('brandId') || '',
            minPrice: params.get('minPrice') || '',
            maxPrice: params.get('maxPrice') || '',
            sortBy: params.get('sortBy') || 'newest',
            page: params.get('page') || '1',
            pageSize: params.get('pageSize') || '12'
        };

        const [result, categories, brands] = await Promise.all([
            Api.catalog.products(query),
            Api.catalog.categories().catch(() => []),
            Api.catalog.brands().catch(() => [])
        ]);

        const items = result.items || [];
        const totalPages = result.totalPages || 1;
        const currentPage = result.page || 1;

        const buildListUrl = overrides => {
            const merged = Object.assign({}, query, overrides);
            const search = new URLSearchParams();
            Object.entries(merged).forEach(([key, value]) => {
                if (value !== '' && value !== null && value !== undefined) search.set(key, value);
            });
            return `#/san-pham?${search.toString()}`;
        };

        const categoryLinks = categories.map(category => `
            <li>
                <button class="${String(query.categoryId) === String(category.id) ? 'is-active' : ''}"
                        data-filter-category="${category.id}">
                    <span>${escapeHtml(category.name)}</span><em>${category.isActive ? '' : 'Ẩn'}</em>
                </button>
            </li>`).join('');

        const brandLinks = brands.map(brand => `
            <li>
                <button class="${String(query.brandId) === String(brand.id) ? 'is-active' : ''}"
                        data-filter-brand="${brand.id}">
                    <span>${escapeHtml(brand.name)}</span>
                </button>
            </li>`).join('');

        const sortTabs = [
            { key: 'newest', label: 'Mới nhất' },
            { key: 'price_asc', label: 'Giá tăng dần' },
            { key: 'price_desc', label: 'Giá giảm dần' }
        ].map(tab => `
            <button class="${query.sortBy === tab.key ? 'is-active' : ''}" data-sort="${tab.key}">${tab.label}</button>`).join('');

        const brandChips = brands.map(brand => `
            <button class="chip ${String(query.brandId) === String(brand.id) ? 'is-active' : ''}"
                    data-filter-brand="${brand.id}">${escapeHtml(brand.name)}</button>`).join('');

        const categoryChips = categories.map(category => `
            <button class="chip ${String(query.categoryId) === String(category.id) ? 'is-active' : ''}"
                    data-filter-category="${category.id}">${escapeHtml(category.name)}</button>`).join('');

        const pagination = totalPages > 1 ? `
            <div class="pagination">
                <button ${currentPage <= 1 ? 'disabled' : ''} data-page="${currentPage - 1}">‹</button>
                ${Array.from({ length: totalPages }, (_, index) => index + 1).map(page => `
                    <button class="${page === currentPage ? 'is-active' : ''}" data-page="${page}">${page}</button>`).join('')}
                <button ${currentPage >= totalPages ? 'disabled' : ''} data-page="${currentPage + 1}">›</button>
            </div>` : '';

        UI.render(`
            ${UI.breadcrumb([
                { label: 'Trang chủ', href: '#/' },
                { label: 'Sản phẩm', href: '#/san-pham' }
            ])}
            <div class="container list-layout">
                <aside>
                    <div class="panel">
                        <div class="panel__head">Bộ lọc</div>
                        <div class="filter-group">
                            <h4>Danh mục</h4>
                            <ul class="filter-list">
                                <li><button class="${!query.categoryId ? 'is-active' : ''}" data-filter-category=""><span>Tất cả</span></button></li>
                                ${categoryLinks}
                            </ul>
                        </div>
                        <div class="filter-group">
                            <h4>Thương hiệu</h4>
                            <ul class="filter-list">
                                <li><button class="${!query.brandId ? 'is-active' : ''}" data-filter-brand=""><span>Tất cả</span></button></li>
                                ${brandLinks}
                            </ul>
                        </div>
                        <div class="filter-group">
                            <h4>Khoảng giá</h4>
                            <div class="price-inputs">
                                <input type="number" id="min-price" placeholder="Từ" value="${escapeHtml(query.minPrice)}">
                                <span>—</span>
                                <input type="number" id="max-price" placeholder="Đến" value="${escapeHtml(query.maxPrice)}">
                            </div>
                            <button class="btn btn--outline btn--sm btn--block" style="margin-top:10px" data-apply-price>Lọc</button>
                        </div>
                    </div>
                </aside>

                <div>
                    ${categoryChips ? `<div class="chip-row">${categoryChips}</div>` : ''}
                    ${brandChips ? `<div class="chip-row">${brandChips}</div>` : ''}
                    <div class="list-toolbar">
                        <span class="list-toolbar__count">Tìm thấy <b>${result.totalCount || 0}</b> sản phẩm</span>
                        ${query.search ? `<span class="list-toolbar__count">từ khóa: "${escapeHtml(query.search)}"</span>` : ''}
                        <div class="sort-tabs">${sortTabs}</div>
                    </div>
                    ${UI.productGrid(items)}
                    ${pagination}
                </div>
            </div>`);

        // Bind filter events
        document.querySelectorAll('[data-filter-category]').forEach(node => node.addEventListener('click', () => {
            window.location.hash = buildListUrl({ categoryId: node.dataset.filterCategory, page: 1 });
        }));
        document.querySelectorAll('[data-filter-brand]').forEach(node => node.addEventListener('click', () => {
            window.location.hash = buildListUrl({ brandId: node.dataset.filterBrand, page: 1 });
        }));
        document.querySelectorAll('[data-sort]').forEach(node => node.addEventListener('click', () => {
            window.location.hash = buildListUrl({ sortBy: node.dataset.sort, page: 1 });
        }));
        document.querySelectorAll('[data-page]').forEach(node => node.addEventListener('click', () => {
            window.location.hash = buildListUrl({ page: node.dataset.page });
        }));
        const applyPrice = document.querySelector('[data-apply-price]');
        if (applyPrice) {
            applyPrice.addEventListener('click', () => {
                window.location.hash = buildListUrl({
                    minPrice: document.getElementById('min-price').value,
                    maxPrice: document.getElementById('max-price').value,
                    page: 1
                });
            });
        }
    }

    /* ================= PRODUCT DETAIL ================= */

    async function productDetail(params) {
        UI.loading('Đang tải chi tiết sản phẩm...');

        const productId = params.get('id');
        const product = await Api.catalog.product(productId);
        const variants = product.variants || [];
        const images = product.images || [];

        let selectedVariant = variants.find(variant => variant.isActive) || variants[0] || null;

        const variantButtons = variants.map(variant => `
            <button class="option" data-variant="${variant.id}" data-price="${variant.price}" data-stock="${variant.stockQuantity}"
                    data-sku="${escapeHtml(variant.sku)}" data-color="${escapeHtml(variant.color)}" data-capacity="${escapeHtml(variant.capacity)}">
                ${escapeHtml(variant.color)} · ${escapeHtml(variant.capacity)}
            </button>`).join('');

        const imagesHtml = images.length
            ? images.map((image, index) => `
                <button class="${index === 0 ? 'is-active' : ''}" data-image="${escapeHtml(image.imageUrl)}">
                    <img src="${escapeHtml(image.imageUrl)}" alt="${escapeHtml(product.name)}">
                </button>`).join('')
            : '';

        const mainImage = product.mainImageUrl || (images[0] && images[0].imageUrl) || '';

        const specs = parseSpecifications(product.specificationsJson);

        const installmentRows = Mock.installmentPlan(selectedVariant ? selectedVariant.price : product.minPrice).map(plan => `
            <tr>
                <td>${escapeHtml(plan.bank)}</td>
                <td>${plan.months} tháng · ${plan.rate}%</td>
                <td><b>${formatCurrency(plan.monthlyAmount)}</b>/tháng</td>
                <td>${escapeHtml(plan.note)}</td>
            </tr>`).join('');

        const reviews = Mock.reviews.map(review => `
            <div class="order-item-row" style="align-items:flex-start;padding:12px 0;border-bottom:1px solid var(--line)">
                <div style="flex:1">
                    <b>${escapeHtml(review.author)}</b>
                    <span style="margin-left:8px;color:var(--amber);font-size:12px">${'★'.repeat(review.rating)}${'☆'.repeat(5 - review.rating)}</span>
                    <span style="display:block;color:var(--ink-soft);font-size:12px">${escapeHtml(review.date)} · ${escapeHtml(review.variant)}</span>
                    <p style="margin:6px 0 0;font-size:13.5px"><b>${escapeHtml(review.title)}</b> — ${escapeHtml(review.body)}</p>
                </div>
                <span class="badge badge--muted">👍 ${review.likes}</span>
            </div>`).join('');

        UI.render(`
            ${UI.breadcrumb([
                { label: 'Trang chủ', href: '#/' },
                { label: 'Sản phẩm', href: '#/san-pham' },
                { label: product.categoryName || '', href: `#/danh-muc/${product.categoryId}` },
                { label: product.name, href: `#/san-pham/${product.id}` }
            ])}
            <div class="container">
                <div class="detail">
                    <div class="detail__gallery">
                        <div class="detail__main-image" id="main-image">
                            ${mainImage
                                ? `<img src="${escapeHtml(mainImage)}" alt="${escapeHtml(product.name)}" style="max-height:100%;object-fit:contain">`
                                : `<div class="product-card__thumb" style="font-size:64px">${escapeHtml((product.name || '?').charAt(0).toUpperCase())}</div>`}
                        </div>
                        <div class="detail__thumbs">${imagesHtml}</div>
                    </div>

                    <div>
                        <h1 class="detail__title">${escapeHtml(product.name)}</h1>
                        <div class="detail__sub">
                            Thương hiệu: <b>${escapeHtml(product.brandName || '')}</b> ·
                            Danh mục: <b>${escapeHtml(product.categoryName || '')}</b> ·
                            Mã sản phẩm: <span id="sku-label">${selectedVariant ? escapeHtml(selectedVariant.sku) : '—'}</span>
                        </div>

                        <div class="detail__price-box">
                            <div class="detail__price" id="detail-price">${formatCurrency(selectedVariant ? selectedVariant.price : product.minPrice)}</div>
                            <div class="detail__save" id="detail-stock">${selectedVariant ? (selectedVariant.stockQuantity > 0 ? `Còn ${selectedVariant.stockQuantity} sản phẩm` : 'Tạm hết hàng') : ''}</div>
                        </div>

                        <div class="detail__row">
                            <h4>Màu sắc & dung lượng</h4>
                            <div class="option-list" id="variant-options">${variantButtons}</div>
                        </div>

                        <div class="detail__promo">
                            🎁 <b>Ưu đãi đang có:</b> Giảm ngay 500.000đ cho đơn từ 5 triệu (mã <b>PHONE500</b>) · Trả góp 0% hạn 12 tháng.
                        </div>

                        <div class="detail__actions">
                            <button class="btn btn--primary" id="btn-add-cart" ${selectedVariant ? '' : 'disabled'}>Thêm vào giỏ hàng</button>
                            <button class="btn btn--outline" id="btn-buy-now" ${selectedVariant ? '' : 'disabled'}>Mua ngay</button>
                            <button class="btn btn--ghost" id="btn-wishlist">♡ Yêu thích</button>
                            <button class="btn btn--ghost" id="btn-compare">⇄ So sánh</button>
                        </div>

                        <div class="detail__row" style="margin-top:20px">
                            <h4>Bảo hành &amp; vận chuyển</h4>
                            <p style="margin:0;font-size:13.5px;color:var(--ink-soft)">
                                ✔ Bảo hành chính hãng 12 tháng · ✔ Giao hàng miễn phí từ 500.000đ · ✔ Đổi trả trong 30 ngày · ✔ Thanh toán COD
                            </p>
                        </div>
                    </div>
                </div>

                <div class="info-page">
                    <h2>Mô tả sản phẩm</h2>
                    <p>${escapeHtml(product.description || 'Chưa có mô tả cho sản phẩm này.')}</p>

                    ${specs.length ? `
                        <h3>Thông số kỹ thuật</h3>
                        <table class="spec-table">
                            <tbody>
                                ${specs.map(spec => `<tr><td>${escapeHtml(spec.key)}</td><td>${escapeHtml(spec.value)}</td></tr>`).join('')}
                            </tbody>
                        </table>` : ''}

                    <h3>Trả góp không tốn phí</h3>
                    ${UI.mockBanner('Dữ liệu trả góp được mô phỏng, backend chưa có endpoint hỗ trợ.')}
                    <table class="spec-table">
                        <thead><tr><th>Ngân hàng</th><th>Kỳ hạn</th><th>Trả hàng tháng</th><th>Ghi chú</th></tr></thead>
                        <tbody>${installmentRows}</tbody>
                    </table>

                    <h3>Đánh giá sản phẩm</h3>
                    ${UI.mockBanner('Đánh giá và bình luận chưa có endpoint, dữ liệu dưới đây là mô phỏng.')}
                    ${reviews}

                    <h3>Lịch sử giá</h3>
                    ${UI.mockBanner('Lịch sử giá được mô phỏng từ giá hiện tại.')}
                    <table class="spec-table">
                        <tbody>
                            ${Mock.priceHistory(product).map(row => `
                                <tr><td>${escapeHtml(row.period)}</td><td><b>${formatCurrency(row.price)}</b></td><td>${escapeHtml(row.note)}</td></tr>`).join('')}
                        </tbody>
                    </table>
                </div>
            </div>`);

        // Variant selection
        document.querySelectorAll('[data-variant]').forEach(node => node.addEventListener('click', () => {
            document.querySelectorAll('[data-variant]').forEach(item => item.classList.remove('is-active'));
            node.classList.add('is-active');
            selectedVariant = {
                id: Number(node.dataset.variant),
                sku: node.dataset.sku,
                color: node.dataset.color,
                capacity: node.dataset.capacity,
                price: Number(node.dataset.price),
                stockQuantity: Number(node.dataset.stock)
            };
            document.getElementById('detail-price').textContent = formatCurrency(selectedVariant.price);
            document.getElementById('detail-stock').textContent = selectedVariant.stockQuantity > 0
                ? `Còn ${selectedVariant.stockQuantity} sản phẩm`
                : 'Tạm hết hàng';
            document.getElementById('sku-label').textContent = selectedVariant.sku;
        }));

        // Image gallery
        document.querySelectorAll('[data-image]').forEach(node => node.addEventListener('click', () => {
            document.querySelectorAll('[data-image]').forEach(item => item.classList.remove('is-active'));
            node.classList.add('is-active');
            document.getElementById('main-image').innerHTML =
                `<img src="${escapeHtml(node.dataset.image)}" alt="${escapeHtml(product.name)}" style="max-height:100%;object-fit:contain">`;
        }));

        document.getElementById('btn-add-cart').addEventListener('click', () => addToCart(selectedVariant, product, 1));
        document.getElementById('btn-buy-now').addEventListener('click', async () => {
            const added = await addToCart(selectedVariant, product, 1, true);
            if (added) {
                window.location.hash = '#/thanh-toan';
            }
        });
        document.getElementById('btn-wishlist').addEventListener('click', () => {
            const list = Mock.toggleWishlist(product.id);
            UI.toast(list.includes(product.id) ? 'Đã thêm vào danh sách yêu thích.' : 'Đã bỏ khỏi danh sách yêu thích.', 'success');
        });
        document.getElementById('btn-compare').addEventListener('click', () => {
            const compared = App.compareProducts(product);
            window.location.hash = `#/so-sanh?ids=${compared.join(',')}`;
        });
    }

    function parseSpecifications(json) {
        if (!json) return [];
        try {
            const parsed = JSON.parse(json);
            if (Array.isArray(parsed)) {
                return parsed.map(item => ({ key: item.key || item.name || '', value: item.value || '' }));
            }
            return Object.entries(parsed).map(([key, value]) => ({ key, value: String(value) }));
        } catch {
            return [];
        }
    }

    async function addToCart(variant, product, quantity = 1, silent = false) {
        if (!variant) {
            UI.toast('Vui lòng chọn phiên bản sản phẩm.', 'error');
            return false;
        }
        if (!Api.isAuthenticated()) {
            UI.toast('Vui lòng đăng nhập để thêm vào giỏ hàng.', 'error');
            App.openQuickLogin();
            return false;
        }
        try {
            await Api.cart.addItem(variant.id, quantity);
            await App.refreshCartBadge();
            if (!silent) {
                UI.toast(`Đã thêm ${variant.color} ${variant.capacity} vào giỏ hàng.`, 'success');
            }
            return true;
        } catch (error) {
            UI.toast(error.message, 'error');
            return false;
        }
    }

    /* ================= CART ================= */

    async function cart() {
        if (!Api.isAuthenticated()) {
            UI.render(`
                <div class="container">
                    ${UI.emptyState('🛒', 'Bạn chưa đăng nhập', 'Đăng nhập để xem giỏ hàng của bạn.',
                        '<button class="btn btn--primary" id="btn-login-redirect">Đăng nhập</button>')}
                </div>`);
            document.getElementById('btn-login-redirect').addEventListener('click', () => App.openQuickLogin());
            return;
        }

        UI.loading('Đang tải giỏ hàng...');
        const cartData = await Api.cart.get();

        if (!cartData.items || cartData.items.length === 0) {
            UI.render(`
                ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Giỏ hàng', href: '#/gio-hang' }])}
                <div class="container">
                    ${UI.emptyState('🛒', 'Giỏ hàng đang trống', 'Hãy thêm sản phẩm bạn quan tâm vào giỏ hàng.',
                        '<a class="btn btn--primary" href="#/san-pham">Tiếp tục mua sắm</a>')}
                </div>`);
            return;
        }

        const rows = cartData.items.map(item => `
            <tr data-cart-item="${item.id}">
                <td style="width:64px">
                    <div class="order-item-row__thumb">${escapeHtml((item.productName || '?').charAt(0).toUpperCase())}</div>
                </td>
                <td>
                    <b>${escapeHtml(item.productName)}</b><br>
                    <small style="color:var(--ink-soft)">SKU: ${escapeHtml(item.sku)}</small>
                </td>
                <td>${formatCurrency(item.unitPrice)}</td>
                <td>
                    <span class="qty">
                        <button data-qty-decrease="${item.id}">−</button>
                        <span>${item.quantity}</span>
                        <button data-qty-increase="${item.id}">+</button>
                    </span>
                </td>
                <td><b>${formatCurrency(item.subtotal)}</b></td>
                <td><button class="btn btn--ghost btn--sm" data-remove-item="${item.id}">Xóa</button></td>
            </tr>`).join('');

        const shippingFee = cartData.subtotal >= 500000 ? 0 : 30000;
        const total = cartData.subtotal + shippingFee;

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Giỏ hàng', href: '#/gio-hang' }])}
            <div class="container cart-layout">
                <div class="panel">
                    <div class="panel__head">Giỏ hàng của bạn <span class="badge badge--muted">${cartData.totalQuantity} sản phẩm</span></div>
                    <div style="overflow-x:auto">
                        <table class="cart-table">
                            <thead><tr><th></th><th>Sản phẩm</th><th>Đơn giá</th><th>Số lượng</th><th>Tổng</th><th></th></tr></thead>
                            <tbody>${rows}</tbody>
                        </table>
                    </div>
                    <div style="padding:14px 16px;display:flex;gap:10px;flex-wrap:wrap">
                        <a class="btn btn--ghost" href="#/san-pham">← Tiếp tục mua sắm</a>
                    </div>
                </div>

                <aside class="panel">
                    <div class="panel__head">Tóm tắt đơn hàng</div>
                    <div class="panel__body">
                        <div class="summary-row"><span>Tổng tiền hàng (${cartData.totalQuantity} sản phẩm)</span><b>${formatCurrency(cartData.subtotal)}</b></div>
                        <div class="summary-row"><span>Phí vận chuyển</span><b>${shippingFee === 0 ? 'Miễn phí' : formatCurrency(shippingFee)}</b></div>
                        ${shippingFee > 0 ? '<p style="font-size:12px;color:var(--ink-soft);margin:4px 0 0">Mua thêm ' + formatCurrency(500000 - cartData.subtotal) + ' để được miễn phí vận chuyển.</p>' : ''}
                        <div class="summary-row summary-row--total"><span>Tổng cộng</span><b>${formatCurrency(total)}</b></div>
                        <a class="btn btn--primary btn--block" href="#/thanh-toan" style="margin-top:14px">Tiến hành thanh toán</a>
                        <p style="font-size:12px;color:var(--ink-soft);text-align:center;margin:10px 0 0">Thanh toán khi nhận hàng (COD)</p>
                    </div>
                </aside>
            </div>`);

        document.querySelectorAll('[data-qty-increase]').forEach(node => node.addEventListener('click', () => changeQuantity(node.dataset.qtyIncrease, 1)));
        document.querySelectorAll('[data-qty-decrease]').forEach(node => node.addEventListener('click', () => changeQuantity(node.dataset.qtyDecrease, -1)));
        document.querySelectorAll('[data-remove-item]').forEach(node => node.addEventListener('click', async () => {
            try {
                await Api.cart.removeItem(node.dataset.removeItem);
                UI.toast('Đã xóa sản phẩm khỏi giỏ hàng.', 'success');
                await App.refreshCartBadge();
                cart();
            } catch (error) {
                UI.toast(error.message, 'error');
            }
        }));
    }

    async function changeQuantity(cartItemId, delta) {
        const row = document.querySelector(`[data-cart-item="${cartItemId}"]`);
        const quantityCell = row ? row.querySelector('.qty span') : null;
        const nextQuantity = Number(quantityCell.textContent) + delta;
        if (nextQuantity < 1) return;
        if (nextQuantity > 10) {
            UI.toast('Mỗi sản phẩm tối đa 10 đơn vị.', 'error');
            return;
        }
        try {
            await Api.cart.updateItem(cartItemId, nextQuantity);
            await App.refreshCartBadge();
            cart();
        } catch (error) {
            UI.toast(error.message, 'error');
        }
    }

    /* ================= CHECKOUT ================= */

    async function checkout() {
        if (!Api.isAuthenticated()) {
            window.location.hash = '#/dang-nhap';
            return;
        }

        UI.loading('Đang chuẩn bị thanh toán...');

        const [cartData, addresses] = await Promise.all([
            Api.cart.get(),
            Api.auth.addresses().catch(() => [])
        ]);

        if (!cartData.items || cartData.items.length === 0) {
            window.location.hash = '#/gio-hang';
            return;
        }

        const selected = addresses[0] || { receiverName: '', receiverPhone: '', addressText: '' };
        const shippingFee = cartData.subtotal >= 500000 ? 0 : 30000;

        UI.render(`
            ${UI.breadcrumb([
                { label: 'Trang chủ', href: '#/' },
                { label: 'Giỏ hàng', href: '#/gio-hang' },
                { label: 'Thanh toán', href: '#/thanh-toan' }
            ])}
            <div class="container cart-layout">
                <div>
                    <div class="panel" style="margin-bottom:16px">
                        <div class="panel__head">Thông tin người nhận</div>
                        <div class="panel__body">
                            ${addresses.length > 1 ? '<button class="btn btn--outline btn--sm" id="btn-pick-address" style="margin-bottom:14px">Chọn địa chỉ khác</button>' : ''}
                            <form class="form" id="checkout-form">
                                <div class="form__grid-2">
                                    <label class="field">
                                        <span class="field__label">Họ và tên người nhận *</span>
                                        <input name="receiverName" required value="${escapeHtml(selected.receiverName)}" placeholder="Nguyễn Văn A">
                                    </label>
                                    <label class="field">
                                        <span class="field__label">Số điện thoại *</span>
                                        <input name="receiverPhone" required value="${escapeHtml(selected.receiverPhone)}" placeholder="0912345678">
                                    </label>
                                </div>
                                <label class="field">
                                    <span class="field__label">Địa chỉ nhận hàng *</span>
                                    <textarea name="shippingAddress" rows="3" required placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành">${escapeHtml(selected.addressText)}</textarea>
                                </label>
                                <label class="field">
                                    <span class="field__label">Ghi chú cho đơn hàng</span>
                                    <textarea name="note" rows="2" placeholder="Ví dụ: gọi trước khi giao"></textarea>
                                </label>
                                <label class="field">
                                    <span class="field__label">Mã ưu đãi</span>
                                    <input name="discountCode" placeholder="PHONE500">
                                    ${UI.mockBanner('Mã ưu đãi chưa được backend xử lý, hiện chỉ mang tính minh hoạ.')}
                                </label>
                                <div class="form__error" id="checkout-error" hidden></div>
                                <button class="btn btn--primary btn--block" type="submit" style="padding:13px">Đặt hàng (COD)</button>
                            </form>
                        </div>
                    </div>
                </div>

                <aside class="panel">
                    <div class="panel__head">Đơn hàng</div>
                    <div class="panel__body">
                        ${cartData.items.map(item => `
                            <div class="order-item-row" style="margin-bottom:10px">
                                <div class="order-item-row__thumb">${escapeHtml((item.productName || '?').charAt(0).toUpperCase())}</div>
                                <div class="order-item-row__info">
                                    <b>${escapeHtml(item.productName)}</b>
                                    <span>${escapeHtml(item.sku)} · x${item.quantity}</span>
                                </div>
                                <b>${formatCurrency(item.subtotal)}</b>
                            </div>`).join('')}
                        <div class="summary-row"><span>Tổng tiền hàng</span><b>${formatCurrency(cartData.subtotal)}</b></div>
                        <div class="summary-row"><span>Phí vận chuyển</span><b>${shippingFee === 0 ? 'Miễn phí' : formatCurrency(shippingFee)}</b></div>
                        <div class="summary-row summary-row--total"><span>Tổng cộng</span><b>${formatCurrency(cartData.subtotal + shippingFee)}</b></div>
                    </div>
                </aside>
            </div>`);

        const pickAddressButton = document.getElementById('btn-pick-address');
        if (pickAddressButton) {
            pickAddressButton.addEventListener('click', async () => {
                const chosen = await new Promise(resolve => UI.pickAddress(addresses, resolve));
                if (chosen) {
                    const form = document.getElementById('checkout-form');
                    form.receiverName.value = chosen.receiverName;
                    form.receiverPhone.value = chosen.receiverPhone;
                    form.shippingAddress.value = chosen.addressText;
                }
            });
        }

        document.getElementById('checkout-form').addEventListener('submit', async event => {
            event.preventDefault();
            const errorBox = document.getElementById('checkout-error');
            const form = event.target;
            const submitButton = form.querySelector('button[type="submit"]');
            submitButton.disabled = true;

            try {
                const created = await Api.order.checkout({
                    receiverName: form.receiverName.value.trim(),
                    receiverPhone: form.receiverPhone.value.trim(),
                    shippingAddress: form.shippingAddress.value.trim(),
                    note: form.note.value.trim() || null
                });
                await App.refreshCartBadge();
                UI.toast('Đặt hàng thành công!', 'success');
                window.location.hash = `#/don-hang/${created.id}`;
            } catch (error) {
                errorBox.hidden = false;
                errorBox.textContent = error.message;
                submitButton.disabled = false;
            }
        });
    }

    /* ================= ORDERS ================= */

    async function orders() {
        if (!Api.isAuthenticated()) {
            window.location.hash = '#/dang-nhap';
            return;
        }

        UI.loading('Đang tải đơn hàng...');
        const result = await Api.order.list({ page: 1, pageSize: 20 });
        const items = result.items || [];

        if (items.length === 0) {
            UI.render(`
                ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Đơn hàng', href: '#/don-hang' }])}
                <div class="container">
                    ${UI.emptyState('📦', 'Bạn chưa có đơn hàng nào', 'Các đơn hàng đã đặt sẽ hiển thị tại đây.',
                        '<a class="btn btn--primary" href="#/san-pham">Tiếp tục mua sắm</a>')}
                </div>`);
            return;
        }

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Đơn hàng', href: '#/don-hang' }])}
            <div class="container" style="padding-bottom:40px">
                <div class="section__head"><h2>Đơn hàng của tôi</h2><p>${result.totalCount} đơn hàng</p></div>
                ${items.map(orderCard).join('')}
            </div>`);

        bindOrderActions();
    }

    function orderCard(order) {
        const canCancel = order.status === 'PENDING' || order.status === 'CONFIRMED';
        return `
            <div class="order-card">
                <div class="order-card__head">
                    <span class="order-card__no">Đơn #${escapeHtml(order.orderNumber)}</span>
                    ${UI.statusChip(order.status)}
                    <span class="order-card__date">${formatDate(order.createdAt)}</span>
                    <span class="order-card__total">${formatCurrency(order.totalAmount)}</span>
                </div>
                <div class="order-card__items">
                    ${(order.items || []).map(item => `
                        <div class="order-item-row">
                            <div class="order-item-row__thumb">${escapeHtml((item.productName || '?').charAt(0).toUpperCase())}</div>
                            <div class="order-item-row__info">
                                <b>${escapeHtml(item.productName)}</b>
                                <span>${escapeHtml(item.sku)} · x${item.quantity} · ${formatCurrency(item.unitPrice)}</span>
                            </div>
                        </div>`).join('')}
                </div>
                <div class="order-card__foot">
                    <span style="margin-right:auto;font-size:12.5px;color:var(--ink-soft)">
                        ${escapeHtml(order.shippingAddress || '')}
                    </span>
                    ${canCancel ? `<button class="btn btn--ghost btn--sm" data-cancel-order="${order.id}">Hủy đơn</button>` : ''}
                    <a class="btn btn--outline btn--sm" href="#/don-hang/${order.id}">Chi tiết</a>
                </div>
            </div>`;
    }

    function bindOrderActions() {
        document.querySelectorAll('[data-cancel-order]').forEach(node => node.addEventListener('click', async () => {
            const reason = await UI.prompt('Hủy đơn hàng', 'Lý do hủy đơn (bắt buộc):', 'Không còn nhu cầu');
            if (!reason) return;
            try {
                await Api.order.cancel(node.dataset.cancelOrder, reason);
                UI.toast('Đã hủy đơn hàng và hoàn trả tồn kho.', 'success');
                orders();
            } catch (error) {
                UI.toast(error.message, 'error');
            }
        }));
    }

    async function orderDetail(params) {
        if (!Api.isAuthenticated()) {
            window.location.hash = '#/dang-nhap';
            return;
        }

        UI.loading('Đang tải chi tiết đơn hàng...');
        const order = await Api.order.detail(params.get('id'));

        const timeline = (order.statusHistories || []).map(history => `
            <div class="order-item-row" style="align-items:flex-start;padding:10px 0;border-bottom:1px solid var(--line)">
                <div style="flex:1">
                    <b>${escapeHtml(history.fromStatus || 'Tạo đơn')} → ${escapeHtml(history.toStatus)}</b>
                    ${history.note ? `<span style="display:block;color:var(--ink-soft);font-size:12.5px">${escapeHtml(history.note)}</span>` : ''}
                </div>
                <span style="font-size:12.5px;color:var(--ink-soft)">${formatDate(history.changedAt)}</span>
            </div>`).join('');

        const canCancel = order.status === 'PENDING' || order.status === 'CONFIRMED';

        UI.render(`
            ${UI.breadcrumb([
                { label: 'Trang chủ', href: '#/' },
                { label: 'Đơn hàng', href: '#/don-hang' },
                { label: order.orderNumber, href: `#/don-hang/${order.id}` }
            ])}
            <div class="container" style="padding-bottom:40px">
                <div class="order-card">
                    <div class="order-card__head">
                        <span class="order-card__no">Đơn #${escapeHtml(order.orderNumber)}</span>
                        ${UI.statusChip(order.status)}
                        <span class="order-card__date">Đặt lúc ${formatDate(order.createdAt)}</span>
                        <span class="order-card__total">${formatCurrency(order.totalAmount)}</span>
                    </div>

                    <div class="info-grid" style="margin:16px 0">
                        <div class="info-tile"><b>Người nhận</b><span>${escapeHtml(order.receiverName)}<br>${escapeHtml(order.receiverPhone)}</span></div>
                        <div class="info-tile"><b>Địa chỉ</b><span>${escapeHtml(order.shippingAddress)}</span></div>
                        <div class="info-tile"><b>Thanh toán</b><span>${escapeHtml(order.paymentMethod)} · ${order.collectedAmount ? `Đã thu ${formatCurrency(order.collectedAmount)}` : 'Chưa thu'}</span></div>
                    </div>

                    ${order.note ? `<p style="font-size:13.5px;color:var(--ink-soft)"><b>Ghi chú:</b> ${escapeHtml(order.note)}</p>` : ''}

                    <h3 style="font-size:16px;margin:18px 0 8px">Sản phẩm</h3>
                    ${(order.items || []).map(item => `
                        <div class="order-item-row" style="padding:9px 0">
                            <div class="order-item-row__thumb">${escapeHtml((item.productName || '?').charAt(0).toUpperCase())}</div>
                            <div class="order-item-row__info">
                                <b>${escapeHtml(item.productName)}</b>
                                <span>${escapeHtml(item.sku)} · x${item.quantity} · ${formatCurrency(item.unitPrice)}</span>
                            </div>
                            <b>${formatCurrency(item.subtotal)}</b>
                        </div>`).join('')}

                    <div style="margin-top:14px;border-top:1px solid var(--line);padding-top:12px;max-width:320px;margin-left:auto">
                        <div class="summary-row"><span>Tổng tiền hàng</span><b>${formatCurrency(order.subtotal)}</b></div>
                        <div class="summary-row"><span>Phí vận chuyển</span><b>${formatCurrency(order.shippingFee)}</b></div>
                        <div class="summary-row summary-row--total"><span>Tổng cộng</span><b>${formatCurrency(order.totalAmount)}</b></div>
                    </div>

                    <div class="order-card__foot" style="margin-top:16px">
                        ${canCancel ? '<button class="btn btn--ghost btn--sm" data-cancel-order="' + order.id + '">Hủy đơn</button>' : ''}
                        <a class="btn btn--outline btn--sm" href="#/don-hang">← Về danh sách đơn</a>
                    </div>
                </div>

                <div class="order-card">
                    <h3 style="font-size:16px;margin:0 0 8px">Lịch sử trạng thái</h3>
                    ${timeline || '<p style="font-size:13.5px;color:var(--ink-soft)">Chưa có lịch sử.</p>'}
                </div>

                <div class="order-card">
                    <h3 style="font-size:16px;margin:0 0 8px">Theo dõi vận chuyển</h3>
                    ${UI.mockBanner('Chưa có endpoint tra cứu vận chuyển từ đơn vị vận chuyển.')}
                    <p style="font-size:13.5px;color:var(--ink-soft);margin:0">
                        Đơn vị vận chuyển: <b>${escapeHtml(order.carrier || 'Chưa cập nhật')}</b> ·
                        Mã vận đơn: <b>${escapeHtml(order.trackingNumber || 'Chưa cập nhật')}</b>
                    </p>
                </div>
            </div>`);

        bindOrderActions();
    }

    /* ================= ACCOUNT ================= */

    async function account() {
        if (!Api.isAuthenticated()) {
            window.location.hash = '#/dang-nhap';
            return;
        }

        UI.loading('Đang tải thông tin tài khoản...');

        const [profile, addresses] = await Promise.all([
            Api.auth.profile(),
            Api.auth.addresses().catch(() => [])
        ]);

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Tài khoản', href: '#/tai-khoan' }])}
            <div class="container" style="padding-bottom:40px">
                <div class="tabs">
                    <button class="is-active" data-tab="profile">Thông tin cá nhân</button>
                    <button data-tab="addresses">Địa chỉ nhận hàng</button>
                    <button data-tab="password">Đổi mật khẩu</button>
                    ${Api.isStaff() ? '<button data-tab="staff">Quản trị</button>' : ''}
                </div>

                <div id="tab-profile" class="tab-panel">
                    <div class="panel">
                        <div class="panel__head">Thông tin tài khoản</div>
                        <div class="panel__body">
                            <form class="form" id="profile-form">
                                <div class="form__grid-2">
                                    <label class="field"><span class="field__label">Email</span><input value="${escapeHtml(profile.email)}" disabled></label>
                                    <label class="field"><span class="field__label">Vai trò</span><input value="${escapeHtml(profile.role)}" disabled></label>
                                </div>
                                <label class="field"><span class="field__label">Họ và tên</span><input name="fullName" required value="${escapeHtml(profile.fullName || '')}"></label>
                                <label class="field"><span class="field__label">Số điện thoại</span><input name="phone" value="${escapeHtml(profile.phone || '')}" placeholder="0912345678"></label>
                                <div class="form__error" id="profile-error" hidden></div>
                                <button class="btn btn--primary" type="submit" style="align-self:flex-start">Lưu thay đổi</button>
                            </form>
                        </div>
                    </div>
                </div>

                <div id="tab-addresses" class="tab-panel" hidden>
                    <div class="panel">
                        <div class="panel__head">Địa chỉ nhận hàng</div>
                        <div class="panel__body">
                            ${addresses.length
                                ? `<div class="info-grid">${addresses.map(address => `
                                    <div class="info-tile">
                                        <b>${escapeHtml(address.receiverName)}</b>
                                        <span>${escapeHtml(address.receiverPhone)}</span>
                                        <span>${escapeHtml(address.addressText)}</span>
                                    </div>`).join('')}</div>`
                                : '<p style="font-size:13.5px;color:var(--ink-soft)">Bạn chưa có địa chỉ nào. Thêm địa chỉ để checkout nhanh hơn.</p>'}
                            <form class="form" id="address-form" style="margin-top:16px">
                                <div class="form__grid-2">
                                    <label class="field"><span class="field__label">Người nhận *</span><input name="receiverName" required></label>
                                    <label class="field"><span class="field__label">Số điện thoại *</span><input name="receiverPhone" required></label>
                                </div>
                                <label class="field"><span class="field__label">Địa chỉ *</span><textarea name="addressText" rows="2" required></textarea></label>
                                <button class="btn btn--primary" type="submit" style="align-self:flex-start">Thêm địa chỉ</button>
                            </form>
                        </div>
                    </div>
                </div>

                <div id="tab-password" class="tab-panel" hidden>
                    <div class="panel" style="max-width:460px">
                        <div class="panel__head">Đổi mật khẩu</div>
                        <div class="panel__body">
                            <form class="form" id="password-form">
                                <label class="field"><span class="field__label">Mật khẩu hiện tại *</span><input type="password" name="currentPassword" required autocomplete="current-password"></label>
                                <label class="field">
                                    <span class="field__label">Mật khẩu mới *</span>
                                    <input type="password" name="newPassword" required minlength="8" autocomplete="new-password">
                                    <span class="field__hint">Tối thiểu 8 ký tự, gồm chữ và số.</span>
                                </label>
                                <div class="form__error" id="password-error" hidden></div>
                                <button class="btn btn--primary" type="submit" style="align-self:flex-start">Đổi mật khẩu</button>
                            </form>
                        </div>
                    </div>
                </div>

                ${Api.isStaff() ? `
                <div id="tab-staff" class="tab-panel" hidden>
                    <div class="panel">
                        <div class="panel__head">Bảng điều khiển nhân viên</div>
                        <div class="panel__body" id="staff-dashboard">
                            <div class="loading"><div class="spinner"></div><span>Đang tải dữ liệu...</span></div>
                        </div>
                    </div>
                </div>` : ''}
            </div>`);

        bindTabs();

        document.getElementById('profile-form').addEventListener('submit', async event => {
            event.preventDefault();
            const form = event.target;
            const errorBox = document.getElementById('profile-error');
            try {
                await Api.auth.updateProfile({ fullName: form.fullName.value.trim(), phone: form.phone.value.trim() || null });
                UI.toast('Cập nhật hồ sơ thành công.', 'success');
                errorBox.hidden = true;
            } catch (error) {
                errorBox.hidden = false;
                errorBox.textContent = error.message;
            }
        });

        document.getElementById('address-form').addEventListener('submit', async event => {
            event.preventDefault();
            const form = event.target;
            try {
                await Api.auth.addAddress({
                    receiverName: form.receiverName.value.trim(),
                    receiverPhone: form.receiverPhone.value.trim(),
                    addressText: form.addressText.value.trim()
                });
                UI.toast('Thêm địa chỉ thành công.', 'success');
                account();
            } catch (error) {
                UI.toast(error.message, 'error');
            }
        });

        document.getElementById('password-form').addEventListener('submit', async event => {
            event.preventDefault();
            const form = event.target;
            const errorBox = document.getElementById('password-error');
            try {
                await Api.auth.changePassword(form.currentPassword.value, form.newPassword.value);
                errorBox.hidden = true;
                form.reset();
                UI.toast('Đổi mật khẩu thành công. Các phiên khác đã bị thu hồi.', 'success');
            } catch (error) {
                errorBox.hidden = false;
                errorBox.textContent = error.message;
            }
        });

        if (Api.isStaff()) {
            loadStaffDashboard();
        }
    }

    function bindTabs() {
        document.querySelectorAll('.tabs button').forEach(button => button.addEventListener('click', () => {
            document.querySelectorAll('.tabs button').forEach(item => item.classList.remove('is-active'));
            document.querySelectorAll('.tab-panel').forEach(panel => { panel.hidden = true; });
            button.classList.add('is-active');
            const panel = document.getElementById(`tab-${button.dataset.tab}`);
            if (panel) panel.hidden = false;
        }));
    }

    async function loadStaffDashboard() {
        const container = document.getElementById('staff-dashboard');
        if (!container) return;

        try {
            const [lowStock, cod] = await Promise.all([
                Api.admin.lowStock(10).catch(() => []),
                Api.admin.codCollections({ page: 1, pageSize: 5 }).catch(() => ({ items: [] }))
            ]);

            const codItems = cod.items || [];
            const codTotal = codItems.reduce((sum, item) => sum + (item.collectedAmount || 0), 0);

            container.innerHTML = `
                <div class="admin-stats">
                    <div class="admin-stat"><b>${(lowStock || []).length}</b><span>SKU tồn kho thấp</span></div>
                    <div class="admin-stat"><b>${codItems.length}</b><span>Đơn COD đã thu (gần nhất)</span></div>
                    <div class="admin-stat"><b>${formatCurrency(codTotal)}</b><span>Tiền COD đã thu</span></div>
                    <div class="admin-stat"><b>${Mock.stores.length}</b><span>Cửa hàng trên hệ thống</span></div>
                </div>
                <h4 style="font-size:15px">Đơn hàng COD đã thu tiền</h4>
                ${codItems.length ? `
                    <table class="data-table">
                        <thead><tr><th>Mã đơn</th><th>Tổng tiền</th><th>Đã thu</th><th>Thời điểm</th></tr></thead>
                        <tbody>
                            ${codItems.map(item => `
                                <tr>
                                    <td>${escapeHtml(item.orderNumber)}</td>
                                    <td>${formatCurrency(item.totalAmount)}</td>
                                    <td><b>${formatCurrency(item.collectedAmount)}</b></td>
                                    <td>${formatDate(item.collectedAt)}</td>
                                </tr>`).join('')}
                        </tbody>
                    </table>` : '<p style="font-size:13.5px;color:var(--ink-soft)">Chưa có dữ liệu thu COD.</p>'}

                <h4 style="font-size:15px;margin-top:20px">SKU tồn kho thấp</h4>
                ${(lowStock || []).length ? `
                    <table class="data-table">
                        <thead><tr><th>Phiên bản</th><th>Tồn kho</th></tr></thead>
                        <tbody>
                            ${(lowStock || []).map(stock => `
                                <tr><td>#${stock.productVariantId}</td><td><b style="color:var(--brand)">${stock.availableQuantity ?? stock.quantity ?? 0}</b></td></tr>`).join('')}
                        </tbody>
                    </table>` : '<p style="font-size:13.5px;color:var(--ink-soft)">Không có SKU nào tồn kho thấp.</p>'}`;
        } catch (error) {
            container.innerHTML = `<p style="color:var(--brand);font-size:13.5px">${escapeHtml(error.message)}</p>`;
        }
    }

    /* ================= AUTH PAGES ================= */

    function login() {
        UI.render(`
            <div class="container">
                <div class="auth-card">
                    <h2>Đăng nhập</h2>
                    <p>Đăng nhập để theo dõi đơn hàng và sử dụng giỏ hàng.</p>
                    <form class="form" id="login-form">
                        <label class="field"><span class="field__label">Email</span><input type="email" name="email" required placeholder="ban@email.com" autocomplete="username"></label>
                        <label class="field"><span class="field__label">Mật khẩu</span><input type="password" name="password" required placeholder="••••••••" autocomplete="current-password"></label>
                        <div class="form__error" id="login-error" hidden></div>
                        <button class="btn btn--primary btn--block" type="submit">Đăng nhập</button>
                        <p class="form__hint">Chưa có tài khoản? <a href="#/dang-ky">Đăng ký ngay</a></p>
                    </form>
                    <div style="margin-top:18px;border-top:1px solid var(--line);padding-top:14px">
                        <p class="field__hint" style="margin:0 0 8px">Tài khoản demo (mật khẩu: Customer@123 / Staff@123 / Admin@123)</p>
                        <div class="demo-accounts">
                            <button class="demo-account" type="button" data-demo-email="customer@phonestore.vn" data-demo-password="Customer@123"><strong>Khách</strong><span>customer@phonestore.vn</span></button>
                            <button class="demo-account" type="button" data-demo-email="staff@phonestore.vn" data-demo-password="Staff@123"><strong>Nhân viên</strong><span>staff@phonestore.vn</span></button>
                            <button class="demo-account" type="button" data-demo-email="admin@phonestore.vn" data-demo-password="Admin@123"><strong>Quản trị</strong><span>admin@phonestore.vn</span></button>
                        </div>
                    </div>
                </div>
            </div>`);

        bindDemoButtons(document.getElementById('login-form'));

        document.getElementById('login-form').addEventListener('submit', async event => {
            event.preventDefault();
            const form = event.target;
            const errorBox = document.getElementById('login-error');
            const button = form.querySelector('button[type="submit"]');
            button.disabled = true;
            try {
                await Api.auth.login(form.email.value.trim(), form.password.value);
                UI.toast('Đăng nhập thành công.', 'success');
                window.location.hash = '#/';
            } catch (error) {
                errorBox.hidden = false;
                errorBox.textContent = error.message;
                button.disabled = false;
            }
        });
    }

    function bindDemoButtons(form) {
        if (!form) return;
        document.querySelectorAll('[data-demo-email]').forEach(button => button.addEventListener('click', () => {
            form.email.value = button.dataset.demoEmail;
            form.password.value = button.dataset.demoPassword;
            form.email.dispatchEvent(new Event('input'));
        }));
    }

    function register() {
        UI.render(`
            <div class="container">
                <div class="auth-card">
                    <h2>Đăng ký tài khoản</h2>
                    <p>Tạo tài khoản để mua sắm nhanh hơn và theo dõi đơn hàng.</p>
                    <form class="form" id="register-form">
                        <label class="field"><span class="field__label">Họ và tên *</span><input name="fullName" required placeholder="Nguyễn Văn A"></label>
                        <label class="field"><span class="field__label">Email *</span><input type="email" name="email" required placeholder="ban@email.com" autocomplete="username"></label>
                        <label class="field"><span class="field__label">Số điện thoại</span><input name="phone" placeholder="0912345678"></label>
                        <label class="field">
                            <span class="field__label">Mật khẩu *</span>
                            <input type="password" name="password" required minlength="8" placeholder="••••••••" autocomplete="new-password">
                            <span class="field__hint">Tối thiểu 8 ký tự, gồm chữ và số.</span>
                        </label>
                        <div class="form__error" id="register-error" hidden></div>
                        <button class="btn btn--primary btn--block" type="submit">Đăng ký</button>
                        <p class="form__hint">Đã có tài khoản? <a href="#/dang-nhap">Đăng nhập</a></p>
                    </form>
                </div>
            </div>`);

        document.getElementById('register-form').addEventListener('submit', async event => {
            event.preventDefault();
            const form = event.target;
            const errorBox = document.getElementById('register-error');
            const button = form.querySelector('button[type="submit"]');
            button.disabled = true;
            try {
                await Api.auth.register({
                    fullName: form.fullName.value.trim(),
                    email: form.email.value.trim(),
                    password: form.password.value,
                    phone: form.phone.value.trim() || null
                });
                UI.toast('Đăng ký tài khoản thành công.', 'success');
                window.location.hash = '#/';
            } catch (error) {
                errorBox.hidden = false;
                errorBox.textContent = error.message;
                button.disabled = false;
            }
        });
    }

    /* ================= FLASH SALE (MOCK) ================= */

    async function flashSalePage() {
        UI.loading('Đang tải chương trình flash sale...');
        await Mock.wait();

        const sale = Mock.flashSale;
        const endTime = Date.now() + sale.endsAtHours * 3600 * 1000;

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Flash sale', href: '#/flash-sale' }])}
            <div class="container" style="padding-bottom:44px">
                ${UI.mockBanner('Chương trình flash sale chưa có endpoint và bảng dữ liệu riêng trên backend, sản phẩm bên dưới là dữ liệu mô phỏng.')}
                <div class="flash-sale" style="box-shadow:none;border:1px solid var(--line);margin-bottom:22px">
                    <div class="flash-sale__head">
                        <h3 class="flash-sale__title">⚡ ${escapeHtml(sale.title)}</h3>
                        ${UI.countdownMarkup(endTime)}
                    </div>
                </div>
                <div class="flash-sale__track" style="grid-auto-columns:minmax(220px,1fr)">
                    ${sale.items.map(UI.flashCard).join('')}
                </div>
                <div class="info-page" style="margin-top:22px">
                    <h2>Luật chương trình</h2>
                    <ul>
                        <li>Mỗi tài khoản được mua tối đa 01 sản phẩm trong mỗi đợt flash sale.</li>
                        <li>Số lượng mỗi đợt giới hạn theo lượt còn hiển thị trên thẻ sản phẩm.</li>
                        <li>Không áp dụng đồng thời với mã giảm giá khác trong cùng đơn hàng.</li>
                        <li>Đơn hàng flash sale vẫn áp dụng chính sách đổi trả 30 ngày.</li>
                    </ul>
                </div>
            </div>`);

        UI.bindCountdown(document.querySelector('.container'), endTime);
    }

    /* ================= NEWS (MOCK) ================= */

    async function newsPage() {
        UI.loading('Đang tải tin tức...');
        await Mock.wait();

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Tin tức', href: '#/tin-tuc' }])}
            <div class="container" style="padding-bottom:44px">
                ${UI.mockBanner('Tin tức chưa có endpoint và bảng dữ liệu riêng, nội dung dưới đây là mô phỏng.')}
                <div class="section__head"><h2>Tin tức &amp; kinh nghiệm</h2><p>${Mock.newsArticles.length} bài viết</p></div>
                <div class="news-grid">
                    ${Mock.newsArticles.map(article => `
                        <a class="news-card" href="#/huong-dan">
                            <span class="news-card__tag">${escapeHtml(article.tag)}</span>
                            <b>${escapeHtml(article.title)}</b>
                            <span class="news-card__meta">${escapeHtml(article.date)} · ${article.readMinutes} phút đọc</span>
                        </a>`).join('')}
                </div>
            </div>`);
    }

    /* ================= FAQ (MOCK) ================= */

    async function faqPage() {
        UI.loading('Đang tải câu hỏi thường gặp...');
        await Mock.wait();

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Câu hỏi thường gặp', href: '#/cau-hoi' }])}
            <div class="container" style="padding-bottom:44px">
                ${UI.mockBanner('Câu hỏi thường gặp chưa có endpoint, dữ liệu dưới đây là mô phỏng.')}
                <div class="section__head"><h2>Câu hỏi thường gặp</h2><p>${Mock.faqs.length} câu hỏi</p></div>
                <div class="faq-list">
                    ${Mock.faqs.map(faq => `
                        <details class="faq-item">
                            <summary>${escapeHtml(faq.question)}</summary>
                            <p>${escapeHtml(faq.answer)}</p>
                        </details>`).join('')}
                </div>
            </div>`);
    }

    /* ================= STATIC / MOCK PAGES ================= */

    async function promotionsPage() {
        UI.loading('Đang tải khuyến mãi...');
        await Mock.wait();

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Khuyến mãi', href: '#/khuyen-mai' }])}
            <div class="container" style="padding-bottom:40px">
                ${UI.mockBanner('Chương trình khuyến mãi chưa có endpoint và bảng dữ liệu riêng trên backend.')}
                <div class="section__head"><h2>Khuyến mãi đang chạy</h2><p>${Mock.promotions.length} chương trình</p></div>
                <div class="promo-grid">
                    ${Mock.promotions.map(promotion => `
                        <div class="promo-tile">
                            <b>${escapeHtml(promotion.title)}</b>
                            <span>${escapeHtml(promotion.subtitle)}</span>
                            <div style="margin-top:10px;font-size:12px;color:var(--brand);font-weight:700">Mã: ${escapeHtml(promotion.code)}</div>
                        </div>`).join('')}
                </div>

                <div class="info-page" style="margin-top:20px">
                    <h2>Cách áp dụng mã ưu đãi</h2>
                    <ul>
                        <li>Nhập mã tại bước thanh toán, sau khi chọn địa chỉ nhận hàng.</li>
                        <li>Mỗi đơn hàng chỉ áp dụng được một mã ưu đãi tại một thời điểm.</li>
                        <li>Mã không áp dụng cùng chương trình giảm giá khác trừ khi có thông báo riêng.</li>
                    </ul>
                </div>
            </div>`);
    }

    async function installmentPage() {
        UI.loading('Đang tải thông tin trả góp...');
        await Mock.wait();

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Trả góp', href: '#/tra-gop' }])}
            <div class="container" style="padding-bottom:40px">
                ${UI.mockBanner('Tính toán trả góp chưa có endpoint, số liệu minh hoạ theo mức giá trung bình.')}
                <div class="section__head"><h2>Trả góp không tốn phí</h2><p>Duyệt online trong 5 phút</p></div>
                <div class="info-page">
                    <table class="spec-table">
                        <thead><tr><th>Ngân hàng</th><th>Kỳ hạn</th><th>Lãi suất</th><th>Ghi chú</th></tr></thead>
                        <tbody>
                            ${Mock.installments.map(plan => `
                                <tr>
                                    <td><b>${escapeHtml(plan.bank)}</b></td>
                                    <td>${plan.months} tháng</td>
                                    <td>${plan.rate}%</td>
                                    <td>${escapeHtml(plan.note)}</td>
                                </tr>`).join('')}
                        </tbody>
                    </table>
                    <h3>Ví dụ thanh toán</h3>
                    <p>Sản phẩm giá 23.000.000đ trả góp 12 tháng 0% → mỗi tháng trả khoảng <b>1.916.667đ</b>.</p>
                </div>
            </div>`);
    }

    async function storesPage() {
        UI.loading('Đang tải hệ thống cửa hàng...');
        await Mock.wait();

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Cửa hàng', href: '#/cua-hang' }])}
            <div class="container" style="padding-bottom:40px">
                ${UI.mockBanner('Danh sách cửa hàng chưa được lưu trong database, đang hiển thị dữ liệu mô phỏng.')}
                <div class="section__head"><h2>Hệ thống cửa hàng</h2><p>${Mock.stores.length} điểm bán</p></div>
                <div class="store-grid">
                    ${Mock.stores.map(store => `
                        <div class="store-card">
                            <b>${escapeHtml(store.name)}</b>
                            <span>${escapeHtml(store.address)}</span>
                            <span>☎ ${escapeHtml(store.phone)}</span>
                            <span>🕘 ${escapeHtml(store.hours)} · ${escapeHtml(store.type)}</span>
                        </div>`).join('')}
                </div>
            </div>`);
    }

    async function comparePage(params) {
        UI.loading('Đang so sánh sản phẩm...');

        const ids = (params.get('ids') || '').split(',').filter(Boolean).map(Number);
        const products = await Promise.all(ids.map(id => Api.catalog.product(id).catch(() => null)));
        const validProducts = products.filter(Boolean);

        const comparison = Mock.compare(validProducts.map(product => ({
            brandName: product.brandName,
            categoryName: product.categoryName,
            minPrice: product.minPrice,
            variants: product.variants || [],
            totalStock: (product.variants || []).reduce((sum, variant) => sum + (variant.stockQuantity || 0), 0)
        })));

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'So sánh', href: '#/so-sanh' }])}
            <div class="container" style="padding-bottom:40px">
                ${UI.mockBanner('Tính năng so sánh chưa có endpoint riêng, dữ liệu lấy từ API danh mục.')}
                <div class="section__head"><h2>So sánh sản phẩm</h2><p>${validProducts.length} sản phẩm</p></div>
                <div class="info-page" style="overflow-x:auto">
                    <table class="spec-table" style="min-width:520px">
                        <thead>
                            <tr>
                                <th>Tiêu chí</th>
                                ${validProducts.map(product => `<th>${escapeHtml(product.name)}</th>`).join('')}
                            </tr>
                        </thead>
                        <tbody>
                            ${comparison.rows.map(row => `
                                <tr>
                                    <td>${escapeHtml(row.label)}</td>
                                    ${row.values.map(value => `<td>${escapeHtml(value)}</td>`).join('')}
                                </tr>`).join('')}
                            <tr>
                                <td>Hành động</td>
                                ${validProducts.map(product => `<td><a class="btn btn--primary btn--sm" href="#/san-pham/${product.id}">Mua ngay</a></td>`).join('')}
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>`);
    }

    async function policyPage(params) {
        const key = params.get('id');
        const policy = Mock.policies[key];
        if (!policy) {
            window.location.hash = '#/huong-dan';
            return;
        }
        await Mock.wait();

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: policy.title, href: `#/chinh-sach/${key}` }])}
            <div class="container" style="padding-bottom:40px">
                <div class="info-page">
                    <h2>${escapeHtml(policy.title)}</h2>
                    <p>${escapeHtml(policy.intro)}</p>
                    ${policy.steps.map(step => `
                        <h3>${escapeHtml(step.title)}</h3>
                        <p>${escapeHtml(step.body)}</p>`).join('')}
                </div>
            </div>`);
    }

    async function orderLookup() {
        UI.loading('Đang tra cứu...');
        await Mock.wait();

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Tra cứu đơn hàng', href: '#/tra-cuu-don-hang' }])}
            <div class="container" style="padding-bottom:40px">
                <div class="info-page" style="max-width:640px;margin:0 auto">
                    <h2>Tra cứu đơn hàng</h2>
                    <p>Nhập mã đơn hàng (ví dụ <code>20261104ABC123</code>) để xem trạng thái.</p>
                    ${UI.mockBanner('Tra cứu bằng mã đơn chưa có endpoint; khách đã đăng nhập dùng được mục "Đơn hàng của tôi".')}
                    <form class="form" id="lookup-form" style="margin-top:14px">
                        <label class="field"><span class="field__label">Mã đơn hàng</span><input name="orderNumber" required placeholder="20261104ABC123"></label>
                        <button class="btn btn--primary" type="submit">Tra cứu</button>
                    </form>
                    <p style="margin-top:14px"><a href="#/don-hang">Xem đơn hàng của tôi →</a></p>
                </div>
            </div>`);

        document.getElementById('lookup-form').addEventListener('submit', event => {
            event.preventDefault();
            UI.toast('Chức năng tra cứu bằng mã đơn đang được phát triển.', 'info');
        });
    }

    async function simpleInfoPage(title, intro, extraHtml = '') {
        await Mock.wait();
        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: title, href: '#/' }])}
            <div class="container" style="padding-bottom:40px">
                <div class="info-page">
                    <h2>${escapeHtml(title)}</h2>
                    <p>${escapeHtml(intro)}</p>
                    ${extraHtml}
                </div>
            </div>`);
    }

    async function wishlistPage() {
        UI.loading('Đang tải danh sách yêu thích...');
        const ids = Mock.getWishlist();
        const products = await Promise.all(ids.map(id => Api.catalog.product(id).catch(() => null)));
        const validProducts = products.filter(Boolean);

        UI.render(`
            ${UI.breadcrumb([{ label: 'Trang chủ', href: '#/' }, { label: 'Yêu thích', href: '#/yeu-thich' }])}
            <div class="container" style="padding-bottom:40px">
                ${UI.mockBanner('Danh sách yêu thích được lưu cục bộ trên trình duyệt, chưa đồng bộ tài khoản.')}
                <div class="section__head"><h2>Sản phẩm yêu thích</h2><p>${validProducts.length} sản phẩm</p></div>
                ${UI.productGrid(validProducts)}
            </div>`);
    }

    return {
        home,
        productList,
        productDetail,
        cart,
        checkout,
        orders,
        orderDetail,
        account,
        login,
        register,
        promotionsPage,
        installmentPage,
        storesPage,
        comparePage,
        policyPage,
        orderLookup,
        wishlistPage,
        flashSalePage,
        newsPage,
        faqPage,
        simpleInfoPage,
        addToCart
    };
})();
