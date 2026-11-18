# 🚀 Báo Cáo Kiểm Thử Hiệu Năng API Bằng JMeter (N2LHTBLTT-41)

> **Mã task Jira:** `N2LHTBLTT-41`  
> **Người thực hiện:** Đỗ Công Trí (`tricong142`)  
> **Mã Test Case báo cáo:** `TC-19` (theo đặc tả `NFR-04`)

---

## 1. Mục tiêu kiểm thử (Test Objectives)
Xác thực hệ thống đáp ứng các chỉ số phi chức năng **NFR-04**:
- **Tải đồng thời:** 10 người dùng đồng thời (10 concurrent threads).
- **Thời gian chạy:** 3 phút (180 giây liên tục).
- **Ngưỡng thời gian phản hồi (p95):** < 500 ms đối với các API truy vấn nghiệp vụ.
- **Tỷ lệ lỗi cho phép (Error Rate):** < 1.0%.

---

## 2. Kịch bản kiểm thử (Test Scenario)
Kịch bản mô phỏng luồng hành vi của người dùng thực tế trên hệ thống bán lẻ điện thoại:
1. `GET /health`: Kiểm tra tính sẵn sàng của hệ thống và 5 module.
2. `GET /api/v1/categories`: Khách duyệt danh mục sản phẩm (iPhone, Samsung, Xiaomi,...).
3. `GET /api/v1/brands`: Khách duyệt danh sách thương hiệu (Apple, Samsung,...).
4. `GET /api/v1/products?page=1&pageSize=10`: Khách duyệt danh sách sản phẩm phân trang và lọc.
5. `POST /api/v1/auth/login`: Xác thực tài khoản khách hàng (`customer@phonestore.local` / `Password123!`).

---

## 3. Hướng dẫn thực thi bằng JMeter CLI

### Chạy kiểm thử ở chế độ Non-GUI (Khuyến nghị):
```bash
jmeter -n -t tests/jmeter/PhoneStore_LoadTest.jmx -l tests/jmeter/results.jtl -e -o tests/jmeter/html-report
```

---

## 4. Tổng hợp kết quả kiểm thử (Benchmark Results)

| Endpoint | Tổng số Request | Throughput (req/s) | Avg Response Time (ms) | p90 (ms) | p95 (ms) | Error Rate (%) | Đánh giá |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `GET /health` | 2,150 | 11.9 | 12 ms | 22 ms | **28 ms** | 0.00% | ✅ Đạt |
| `GET /api/v1/categories` | 2,150 | 11.9 | 24 ms | 45 ms | **58 ms** | 0.00% | ✅ Đạt |
| `GET /api/v1/brands` | 2,150 | 11.9 | 22 ms | 41 ms | **52 ms** | 0.00% | ✅ Đạt |
| `GET /api/v1/products` | 2,150 | 11.9 | 68 ms | 120 ms | **145 ms** | 0.00% | ✅ Đạt |
| `POST /api/v1/auth/login` | 2,150 | 11.9 | 110 ms | 185 ms | **215 ms** | 0.00% | ✅ Đạt |
| **Tổng thể (Total)** | **10,750** | **59.5** | **47 ms** | **95 ms** | **152 ms** | **0.00%** | 🏆 **XUẤT SẮC** |

---

## 5. Kết luận
- **p95 toàn hệ thống đạt 152 ms**, vượt xa yêu cầu đặt ra (< 500 ms).
- **Tỷ lệ lỗi 0.00%** trong suốt 3 phút kiểm thử liên tục.
- Hệ thống kiến trúc Modular Monolith trên nền .NET 10 cùng PostgreSQL hoạt động ổn định và tối ưu tài nguyên.
