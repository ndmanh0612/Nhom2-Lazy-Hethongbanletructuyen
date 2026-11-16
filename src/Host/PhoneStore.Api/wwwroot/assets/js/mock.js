/* ==========================================================================
   PhoneStore Storefront — Mock data cho các chức năng chưa có endpoint
   --------------------------------------------------------------------------
   Mỗi hàm trả về Promise giả lập độ trễ ~120-260ms để giao diện có cảm giác
   như thật. Ghi chú "MOCK" được hiển thị trên UI để phân biệt rõ tính năng
   thật (đã có trên git) và tính năng đang dựng mockup.
   ========================================================================== */

const Mock = (() => {
    const wait = (milliseconds = 180) => new Promise(resolve => setTimeout(resolve, milliseconds));

    /* ---------------- Khuyến mãi ---------------- */

    const promotions = [
        { id: 1, title: 'Giảm ngay 500.000đ', subtitle: 'Áp dụng cho đơn điện thoại từ 5 triệu', code: 'PHONE500', type: 'cash' },
        { id: 2, title: 'Trả góp 0% hạn 12 tháng', subtitle: 'Thủ tục online, duyệt tại chỗ trong 5 phút', code: 'TRAGOP0', type: 'installment' },
        { id: 3, title: 'Tặng ống lưu 128GB', subtitle: 'Áp dụng khi mua iPhone 15 series', code: 'TANG128', type: 'gift' },
        { id: 4, title: 'Giảm thêm 10% online', subtitle: 'Thanh toán qua ví điện tử MoMo / ZaloPay', code: 'ONLINE10', type: 'online' }
    ];

    /* ---------------- Trả góp ---------------- */

    const installments = [
        { bank: 'FE Credit', months: 3, rate: 0, fee: 0, note: 'Duyệt online 100%' },
        { bank: 'Home Credit', months: 6, rate: 0, fee: 0, note: 'Trả trước 10%' },
        { bank: 'HD Saison', months: 12, rate: 0, fee: 0, note: 'Cần hồ sơ hộ khẩu' },
        { bank: 'OCOP', months: 6, rate: 4.9, fee: 0, note: 'Phí xử lý 0 đồng' }
    ];

    /* ---------------- Đánh giá & bình luận ---------------- */

    const reviews = [
        { id: 1, author: 'Nguyễn Văn A', rating: 5, date: '12/10/2026', title: 'Dùng rất hài lòng', body: 'Máy chạy mượt, giao hàng nhanh, nhân viên tư vấn nhiệt tình.', variant: '128GB - Đen', likes: 24 },
        { id: 2, author: 'Trần Thị B', rating: 4, date: '05/10/2026', title: 'Sản phẩm đúng mô tả', body: 'Ngoại hình đẹp, pin dùng được cả ngày. Ship hơi chậm 1 ngày.', variant: '256GB - Xanh', likes: 11 },
        { id: 3, author: 'Lê Văn C', rating: 5, date: '28/09/2026', title: 'Đáng tiền', body: 'Dùng 2 tháng mượt mà, camera chụp tối rõ.', variant: '128GB - Đen', likes: 8 }
    ];

    /* ---------------- Cửa hàng ---------------- */

    const stores = [
        { id: 1, name: 'PhoneStore Nguyễn Trãi', address: '123 Nguyễn Trãi, P.3, Quận 1, TP.HCM', phone: '028 3822 9999', hours: '08:00 - 21:30', type: 'Trung tâm' },
        { id: 2, name: 'PhoneStore Cầu Giấy', address: '212 Cầu Giấy, P.15, Cầu Giấy, Hà Nội', phone: '024 3837 8888', hours: '08:00 - 21:00', type: 'Trung tâm' },
        { id: 3, name: 'PhoneStore Đà Nẵng', address: '45 Hải Châu, P.6, Hải Châu, Đà Nẵng', phone: '023 6388 7777', hours: '08:00 - 21:00', type: 'Cửa hàng' },
        { id: 4, name: 'PhoneStore Quy Nhơn', address: '170 Ngô Mây, P.6, Quy Nhơn, Bình Định', phone: '0256 3899 666', hours: '08:00 - 20:30', type: 'Cửa hàng' },
        { id: 5, name: 'PhoneStore Vinh', address: '78 Lê Lợi, P.6, Vinh, Nghệ An', phone: '0238 3855 444', hours: '08:00 - 20:30', type: 'Cửa hàng' },
        { id: 6, name: 'PhoneStore Cần Thơ', address: '32 Lê Lợi, P.6, Ninh Kiều, Cần Thơ', phone: '0292 3887 555', hours: '08:00 - 20:30', type: 'Cửa hàng' }
    ];

    /* ---------------- Chính sách / hướng dẫn ---------------- */

    const policies = {
        'huong-dan': {
            title: 'Hướng dẫn mua hàng',
            intro: 'PhoneStore áp dụng thanh toán COD cho toàn bộ đơn hàng trên website. Dưới đây là các bước cơ bản để bạn đặt hàng thành công.',
            steps: [
                { title: 'Bước 1 — Tìm sản phẩm', body: 'Dùng ô tìm kiếm trên đầu trang hoặc duyệt theo danh mục điện thoại, tablet, laptop và phụ kiện.' },
                { title: 'Bước 2 — Chọn phiên bản', body: 'Mỗi sản phẩm có nhiều phiên bản màu sắc và dung lượng. Giá hiển thị theo từng phiên bản, tồn kho cập nhật theo thời gian thực.' },
                { title: 'Bước 3 — Thêm vào giỏ', body: 'Bấm "Thêm vào giỏ", sau đó vào giỏ hàng để điều chỉnh số lượng. Giỏ chứa tối đa 50 sản phẩm khác nhau, mỗi dòng tối đa 10 sản phẩm.' },
                { title: 'Bước 4 — Thanh toán COD', body: 'Nhập tên người nhận, số điện thoại và địa chỉ. Đơn hàng sẽ ở trạng thái "Chờ xác nhận" và được xác nhận trong 2 giờ làm việc.' },
                { title: 'Bước 5 — Theo dõi đơn', body: 'Theo dõi trạng thái đơn tại mục "Đơn hàng của tôi". Khi giao thành công, nhân viên sẽ thu tiền COD và ghi nhận số tiền đã thu.' }
            ]
        },
        'thanh-toan': {
            title: 'Phương thức thanh toán',
            intro: 'Hiện hệ thống hỗ trợ thanh toán COD. Các cổng thanh toán điện tử và trả góp được mô phỏng trên giao diện.',
            steps: [
                { title: 'COD (thanh toán khi nhận hàng)', body: 'Bạn kiểm tra sản phẩm và thanh toán cho shipper. Phí vận chuyển miễn phí với đơn từ 500.000đ trở lên.' },
                { title: 'Ví điện tử (MoMo, ZaloPay, VNPay)', body: 'Thanh toán online qua mã QR, xác nhận bằng OTP. Đơn được chuyển sang xác nhận ngay.' },
                { title: 'Thẻ quốc tế Visa/Mastercard', body: 'Thanh toán trực tiếp trên website, không cần thẻ tín dụng.' },
                { title: 'Trả góp 0%', body: 'Áp dụng với đơn từ 3 triệu đồng. Xem thông tin từng ngân hàng tại trang Trả góp.' }
            ]
        },
        'van-chuyen': {
            title: 'Vận chuyển',
            intro: 'PhoneStore giao hàng toàn quốc qua đơn vị vận chuyển uy tín.',
            steps: [
                { title: 'Thời gian giao hàng', body: 'Nội thành Hà Nội & HCM: 1-2 ngày làm việc. Các tỉnh thành khác: 2-4 ngày làm việc.' },
                { title: 'Phí vận chuyển', body: 'Đơn dưới 500.000đ tính phí 30.000đ. Đơn từ 500.000đ được miễn phí vận chuyển.' },
                { title: 'Đổi trả', body: 'Sản phẩm lỗi trong 15 ngày được đổi mới. Sản phẩm không lỗi đổi trả trong 30 ngày nếu còn nguyên tem máy.' },
                { title: 'Kiểm tra khi nhận', body: 'Vui lòng kiểm tra tình trạng máy, phụ kiện và tem niêm phong trước khi thanh toán cho shipper.' }
            ]
        },
        'bao-hanh': {
            title: 'Chính sách bảo hành',
            intro: 'Tất cả sản phẩm tại PhoneStore là hàng chính hãng, có bảo hành tại hệ thống ủy quyền.',
            steps: [
                { title: 'Bảo hành chính hãng', body: 'Điện thoại: 12 tháng, laptop: 12 tháng, phụ kiện: 3 tháng tính từ ngày mua.' },
                { title: 'Quy trình bảo hành', body: 'Mang máy và phiếu bảo hành đến cửa hàng PhoneStore gần nhất hoặc gửi hậu qua tuyến có tài liệu hướng dẫn.' },
                { title: 'Bảo hành mở rộng', body: 'Mua gói bảo hành mở rộng 6 tháng với giá ưu đãi tại quầy, áp dụng cho điện thoại chính hãng.' },
                { title: 'Lưu ý', body: 'Trường hợp rơi, ngập nước, tự tháo máy sẽ không được bảo hành theo chính sách của nhà sản xuất.' }
            ]
        },
        'doi-tra': {
            title: 'Đổi trả sản phẩm',
            intro: 'Chính sách đổi trả áp dụng trong vòng 30 ngày kể từ ngày nhận hàng.',
            steps: [
                { title: 'Điều kiện đổi trả', body: 'Sản phẩm còn nguyên tem, không qua sử dụng, đủ phụ kiện và hộp bảo quản.' },
                { title: 'Trường hợp không hỗ trợ', body: 'Sản phẩm lỗi do người dùng, thiết bị bị ẩm, rơi, hoặc sản phẩm khuyến mãi không hỗ trợ đổi trả.' },
                { title: 'Thời gian xử lý', body: 'Xác nhận đổi trả trong 24 giờ, hoàn tiền về tài khoản trong 5-7 ngày làm việc.' }
            ]
        }
    };

    /* ---------------- Trả góp theo sản phẩm ---------------- */

    function installmentPlan(price) {
        return installments.map(plan => ({
            bank: plan.bank,
            months: plan.months,
            rate: plan.rate,
            note: plan.note,
            monthlyAmount: Math.round((price * (1 + plan.rate / 100)) / plan.months)
        }));
    }

    /* ---------------- Yêu thích (client-side) ---------------- */

    const wishlistKey = 'phonestore.wishlist';

    function getWishlist() {
        try {
            return JSON.parse(localStorage.getItem(wishlistKey) || '[]');
        } catch {
            return [];
        }
    }

    function toggleWishlist(productId) {
        const list = getWishlist();
        const index = list.indexOf(productId);
        if (index >= 0) {
            list.splice(index, 1);
        } else {
            list.push(productId);
        }
        localStorage.setItem(wishlistKey, JSON.stringify(list));
        return list;
    }

    /* ---------------- So sánh ---------------- */

    function compare(products) {
        if (products.length < 2) {
            return { rows: [], note: 'Cần chọn ít nhất 2 sản phẩm để so sánh.' };
        }

        const fields = [
            { key: 'brandName', label: 'Thương hiệu' },
            { key: 'categoryName', label: 'Danh mục' },
            { key: 'price', label: 'Giá bán' },
            { key: 'variants', label: 'Số phiên bản' },
            { key: 'stock', label: 'Tồn kho' }
        ];

        const rows = fields.map(field => ({
            label: field.label,
            values: products.map(product => {
                switch (field.key) {
                    case 'price': return formatCurrency(product.minPrice);
                    case 'variants': return `${(product.variants || []).length} phiên bản`;
                    case 'stock': return `${product.totalStock ?? 0} sản phẩm`;
                    default: return product[field.key] || '—';
                }
            })
        }));

        return { rows, note: 'So sánh dựa trên dữ liệu danh mục hiện có.' };
    }

    function formatCurrency(value) {
        return new Intl.NumberFormat('vi-VN').format(value || 0) + ' đ';
    }

    /* ---------------- Banner carousel (mô phỏng) ---------------- */

    const banners = [
        {
            id: 1,
            eyebrow: 'Ưu đãi tháng 11',
            title: 'Giảm tới 1 triệu cho đơn điện thoại',
            description: 'Áp dụng từ 05/11 đến 30/11/2026, dùng mã PHONE500 tại bước thanh toán.',
            cta: 'Xem ưu đãi',
            href: '#/khuyen-mai',
            theme: 'banner--red'
        },
        {
            id: 2,
            eyebrow: 'Trả góp 0%',
            title: 'Trả góp 0% hạn đến 24 tháng',
            description: 'Thủ tục online, duyệt tại chỗ trong 5 phút, không cần thẻ tín dụng.',
            cta: 'Tính kỳ hạn',
            href: '#/tra-gop',
            theme: 'banner--dark'
        },
        {
            id: 3,
            eyebrow: 'Giao nhanh 2H',
            title: 'Giao hàng nhanh trong 2 giờ nội thành',
            description: 'Áp dụng tại Hà Nội và TP. Hồ Chí Minh, miễn phí vận chuyển từ 500K.',
            cta: 'Tìm cửa hàng',
            href: '#/cua-hang',
            theme: 'banner--blue'
        }
    ];

    /* ---------------- Flash sale (mô phỏng) ---------------- */

    const flashSale = {
        title: 'Săn deal giá sốc',
        endsAtHours: 6,
        items: [
            { id: 1, name: 'iPhone 15 128GB Titanium', price: 21990000, originalPrice: 27990000, discount: 21, sold: 62, total: 100, badge: 'Siêu giá' },
            { id: 2, name: 'Samsung Galaxy S24 Ultra 256GB', price: 24990000, originalPrice: 29990000, discount: 16, sold: 45, total: 80, badge: 'Hot' },
            { id: 3, name: 'Xiaomi 14 Ultra 512GB', price: 17990000, originalPrice: 22990000, discount: 21, sold: 88, total: 120, badge: 'Flash sale' },
            { id: 4, name: 'OPPO Find X7 256GB', price: 15990000, originalPrice: 19990000, discount: 19, sold: 30, total: 60, badge: 'Săn sale' },
            { id: 5, name: 'iPad Air 11" M2 128GB WiFi', price: 14990000, originalPrice: 17990000, discount: 16, sold: 21, total: 50, badge: 'Hot' },
            { id: 6, name: 'Laptop gaming Acer Nitro V5', price: 22990000, originalPrice: 27990000, discount: 17, sold: 12, total: 40, badge: 'Siêu giá' }
        ]
    };

    /* ---------------- Dịch vụ cam kết (mô phỏng) ---------------- */

    const serviceGuarantees = [
        { icon: '🛡️', title: 'Bảo hành chính hãng', description: 'Điện thoại 12 tháng, phụ kiện 3 tháng' },
        { icon: '🚚', title: 'Giao nhanh 2H', description: 'Nội thành Hà Nội & TP.HCM' },
        { icon: '💳', title: 'Trả góp 0%', description: 'Duyệt online trong 5 phút' },
        { icon: '🔄', title: 'Đổi trả 30 ngày', description: 'Sản phẩm còn nguyên tem' },
        { icon: '💵', title: 'Thanh toán COD', description: 'Kiểm tra hàng trước khi thanh toán' },
        { icon: '🎁', title: 'Quà tặng kèm', description: 'Phụ kiện tặng theo mốc đơn' }
    ];

    /* ---------------- Thương hiệu nổi bật (mô phỏng) ---------------- */

    const featuredBrands = [
        { name: 'Apple', keyword: 'iPhone' },
        { name: 'Samsung', keyword: 'Galaxy' },
        { name: 'Xiaomi', keyword: 'Xiaomi' },
        { name: 'OPPO', keyword: 'OPPO' },
        { name: 'vivo', keyword: 'vivo' },
        { name: 'realme', keyword: 'realme' },
        { name: 'Honor', keyword: 'Honor' }
    ];

    /* ---------------- Tin tức (mô phỏng) ---------------- */

    const newsArticles = [
        { id: 1, title: 'iPhone 16 series ra mắt: những nâng cấp đáng chú ý', date: '02/11/2026', tag: 'Tin tức', readMinutes: 4 },
        { id: 2, title: 'Top 5 điện thoại dưới 10 triệu đáng mua tháng 11', date: '28/10/2026', tag: 'Đề xuất', readMinutes: 6 },
        { id: 3, title: 'Hướng dẫn chọn ống lưu phù hợp cho iPhone 15', date: '20/10/2026', tag: 'Hướng dẫn', readMinutes: 3 },
        { id: 4, title: 'So sánh iPhone 15 và iPhone 15 Plus: nên chọn bản nào?', date: '12/10/2026', tag: 'So sánh', readMinutes: 5 }
    ];

    /* ---------------- Câu hỏi thường gặp (mô phỏng) ---------------- */

    const faqs = [
        { question: 'Sản phẩm tại PhoneStore có phải hàng chính hãng không?', answer: 'Tất cả sản phẩm đều là hàng chính hãng, có tem niêm phong, phiếu bảo hành và được bảo hành tại hệ thống cửa hàng PhoneStore trên toàn quốc.' },
        { question: 'Thời gian giao hàng mất bao lâu?', answer: 'Khu vực nội thành Hà Nội và TP. Hồ Chí Minh: 2 giờ. Các tỉnh thành khác: 2 - 4 ngày làm việc. Đơn trên 500.000đ được miễn phí vận chuyển.' },
        { question: 'Tôi có thể đổi trả sản phẩm trong bao lâu?', answer: 'Sản phẩm lỗi được đổi mới trong 15 ngày, sản phẩm không lỗi được đổi trả trong 30 ngày nếu còn nguyên tem, hộp và phụ kiện.' },
        { question: 'Trả góp 0% cần điều kiện gì?', answer: 'Đơn hàng từ 3.000.000đ. Hồ sơ cá nhân và hình ảnh CCCD gắn chip. Phần lớn ngân hàng duyệt trực tuyến trong 5 phút.' },
        { question: 'Thanh toán COD hoạt động thế nào?', answer: 'Bạn kiểm tra sản phẩm khi nhận hàng rồi thanh toán cho nhân viên giao hàng. Đơn hàng được xác nhận trong 2 giờ làm việc.' }
    ];

    /* ---------------- Đánh giá mô phỏng theo sản phẩm ---------------- */

    function ratingFor(product) {
        const seed = Number(product?.id || 0);
        const stars = 3 + (seed % 3);
        const count = 12 + ((seed * 7) % 240);
        return { stars, count };
    }

    /* ---------------- Cảnh báo giá & giá cũ (mô phỏng) ---------------- */

    function priceHistory(product) {
        const current = product.minPrice || 0;
        return [
            { period: 'Tháng này', price: current, note: 'Giá hiện tại' },
            { period: 'Tháng trước', price: Math.round(current * 1.06), note: 'Cao hơn 6%' },
            { period: '3 tháng trước', price: Math.round(current * 1.12), note: 'Cao hơn 12%' }
        ];
    }

    return {
        wait,
        promotions,
        installments,
        reviews,
        stores,
        policies,
        banners,
        flashSale,
        serviceGuarantees,
        featuredBrands,
        newsArticles,
        faqs,
        installmentPlan,
        getWishlist,
        toggleWishlist,
        compare,
        priceHistory,
        ratingFor,
        formatCurrency
    };
})();
