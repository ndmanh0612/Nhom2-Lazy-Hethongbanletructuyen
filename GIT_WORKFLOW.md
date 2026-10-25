# 🌿 Quy Chuẩn Git Workflow Cho Nhóm

Tài liệu hướng dẫn quy trình làm việc với Git cho cả nhóm 5 người nhằm đạt điểm tối đa tiêu chí **CLO2 (Trách nhiệm cá nhân và Quản lý Git Workflow)**.

---

## 1. Mô hình phân nhánh (Branching Strategy)

- **`main`**: Nhánh chính (Production-ready). Chỉ chứa code đã kiểm thử ổn định, deploy được.
- **`develop`**: Nhánh tích hợp (Integration branch). Code các module sau khi review PR sẽ merge vào đây.
- **`feature/N2LHTBLTT-<mã-task>-<tên-ngắn-gọn>`**: Nhánh tính năng của từng thành viên.
  - *Ví dụ*:
    - Trí: `feature/N2LHTBLTT-S2-04-identity-register`
    - Nghĩa: `feature/N2LHTBLTT-S2-06-catalog-category`
    - Toàn: `feature/N2LHTBLTT-S2-08-cart-order-entities`
    - Đạt: `feature/N2LHTBLTT-S2-03-db-migrations`
- **`bugfix/...`**: Nhánh sửa lỗi phát sinh trong sprint.

---

## 2. Quy ước Commit Message (Conventional Commits)

Cấu trúc commit chuẩn:
```text
<type>(<scope>): <mô tả ngắn bằng tiếng Việt hoặc tiếng Anh> [Mã Jira nếu có]
```

**Các type cho phép:**
- `feat`: Tính năng mới (ví dụ: `feat(identity): implement register API [FR-01]`)
- `fix`: Sửa lỗi (ví dụ: `fix(catalog): fix null reference when fetching variant images`)
- `refactor`: Tái cấu trúc mã nguồn nhưng không đổi logic (ví dụ: `refactor(order): extract order status validation logic`)
- `test`: Thêm hoặc sửa test (ví dụ: `test(cart): add unit test for add to cart calculation`)
- `docs`: Cập nhật tài liệu (ví dụ: `docs: update API documentation in README`)
- `chore`: Cấu hình, dependencies, script (ví dụ: `chore(deps): update npgsql package`)

---

## 3. Quy trình làm việc hàng ngày của từng thành viên

1. **Kéo code mới nhất từ nhánh `develop`**:
   ```bash
   git checkout develop
   git pull origin develop
   ```

2. **Tạo nhánh tính năng mới**:
   ```bash
   git checkout -b feature/N2LHTBLTT-<mã-task>-<tên-tính-năng>
   ```

3. **Code và commit thường xuyên**:
   - Không commit dồn cục vào cuối tuần.
   - Commit nhỏ, tường minh theo quy chuẩn Conventional Commits.
   ```bash
   git add .
   git commit -m "feat(<module>): <mô tả> [N2LHTBLTT-XXX]"
   ```

4. **Kiểm tra trước khi đẩy code lên remote**:
   ```bash
   dotnet build PhoneStore.slnx
   dotnet test PhoneStore.slnx
   ```

5. **Đẩy nhánh lên GitHub và Mở Pull Request (PR)**:
   ```bash
   git push origin feature/N2LHTBLTT-<mã-task>-<tên-tính-năng>
   ```
   - Truy cập GitHub, mở Pull Request trỏ vào nhánh **`develop`**.
   - Điền đầy đủ thông tin theo **Pull Request Template**.
   - Chỉ định ít nhất 1 thành viên khác (hoặc Lead Mạnh) làm Reviewer.
   - Khi được Approve và CI Pipeline pass, tiến hành Merge (Squash and Merge hoặc Rebase).
