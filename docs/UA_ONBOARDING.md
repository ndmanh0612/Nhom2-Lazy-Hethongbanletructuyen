# PhoneStore Backend — Hướng dẫn hiểu dự án từ FR-01 đến FR-26

> Sinh tự động bằng **Understand Anything** từ knowledge graph của codebase.
> Commit: `916bc2e` · Phân tích lúc: 23:25:34 2/11/2026

## Tổng quan

**PhoneStore** — Backend thuong mai dien tu ban le dien thoai - ASP.NET Core Modular Monolith kem Clean Architecture, 5 module (Identity, Catalog, Cart, Order, Inventory) voi CSDL PostgreSQL tach schema rieng cho tung module.

- **Ngôn ngữ**: csharp, javascript, html, css, markdown, yaml, json
- **Framework**: ASP.NET Core, Entity Framework Core, xUnit, PostgreSQL, React
- **Quy mô graph**: 1175 node · 2540 edge · 9 tầng kiến trúc
- **Phân tích nghiệp vụ**: 6 domain · 16 flow · 73 step

## Bản đồ yêu cầu chức năng → nơi triển khai

Mỗi FR được ánh xạ tới flow nghiệp vụ tương ứng trong domain graph. Mở dashboard ở chế độ **Nghiệp vụ** để xem trực quan.

| FR | Flow nghiệp vụ | Domain | Số bước |
|---|---|---|---|
| FR-01 | Đăng ký tài khoản khách hàng | Danh tính và tài khoản khách hàng | 5 |
| FR-02 | Đăng nhập và quản lý phiên | Danh tính và tài khoản khách hàng | 5 |
| FR-03 | Quản lý hồ sơ và địa chỉ | Danh tính và tài khoản khách hàng | 4 |
| FR-15 | Quản trị tài khoản nhân viên | Quản trị tài khoản nhân viên và phân quyền | 5 |
| — | Phân quyền và bảo vệ API | Quản trị tài khoản nhân viên và phân quyền | 4 |
| FR-04 | Tìm kiếm và duyệt sản phẩm | Danh mục sản phẩm và quản lý Catalog | 4 |
| FR-05 | Xem chi tiết sản phẩm và SKU | Danh mục sản phẩm và quản lý Catalog | 3 |
| FR-12 | Quản lý danh mục và thương hiệu | Danh mục sản phẩm và quản lý Catalog | 4 |
| FR-13 | Quản lý sản phẩm, SKU và giá | Danh mục sản phẩm và quản lý Catalog | 5 |
| FR-06 | Quản lý giỏ hàng | Giỏ hàng | 5 |
| FR-07 | Checkout COD tạo đơn hàng | Đơn hàng và giao hàng COD | 8 |
| FR-08 | Xem danh sách và lịch sử đơn hàng | Đơn hàng và giao hàng COD | 4 |
| FR-10, FR-11, FR-16 | Nhân viên xác nhận, giao hàng và thu tiền COD | Đơn hàng và giao hàng COD | 5 |
| FR-09 | Huỷ đơn và hoàn trả tồn kho | Đơn hàng và giao hàng COD | 5 |
| FR-14 | Nhập kho và điều chỉnh tồn kho | Kho và tồn kho | 4 |
| FR-16 | Theo dõi tồn kho và báo cáo | Kho và tồn kho | 3 |

## Phân tích theo nghiệp vụ

### Danh tính và tài khoản khách hàng

Module Identity quản lý vòng đời tài khoản của khách hàng: đăng ký, đăng nhập, quản lý phiên token (JWT access token và refresh token xoay vòng), cập nhật hồ sơ, đổi mật khẩu và địa chỉ nhận hàng. Mật khẩu được băm bằng BCrypt và chỉ lưu hash SHA-256 của refresh token trong database.

**Entity chính**: User, Address, RefreshToken, AuthResponseDto

**Quy tắc nghiệp vụ**:

- Email được chuẩn hoá về chữ thường và là duy nhất trong schema identity
- Mật khẩu tối thiểu 8 ký tự, phải có chữ và số; BCrypt cost 11
- Refresh token chỉ lưu bản hash SHA-256, mỗi lần dùng sẽ bị tiêu thụ và xoay vòng
- Tài khoản bị khoá hoặc đổi mật khẩu sẽ thu hồi toàn bộ phiên đang mở
- Thông báo lỗi đăng nhập không tiết lộ sự tồn tại của email

#### Đăng ký tài khoản khách hàng

Khách hàng tạo tài khoản mới, hệ thống kiểm tra dữ liệu, băm mật khẩu, tạo User và cấp ngay phiên đăng nhập đầu tiên.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Tiếp nhận yêu cầu đăng ký khách hàng | AuthController tiếp nhận POST /api/v1/auth/register với email, mật khẩu, họ tên và số điện thoại. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Endpoints/AuthController.cs:Register` |
| 2 | Kiểm tra dữ liệu đăng ký | FluentValidation kiểm tra định dạng email, độ mạnh mật khẩu, họ tên và mẫu số điện thoại Việt Nam trước khi xử lý. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Validators/IdentityValidators.cs:RegisterRequestValidator` |
| 3 | Tạo tài khoản CUSTOMER | IdentityService chuẩn hoá email, kiểm tra trùng email và tạo User với vai trò CUSTOMER qua factory CreateCustomer. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:RegisterAsync` |
| 4 | Băm mật khẩu bằng BCrypt | Mật khẩu được băm bằng BCrypt EnhancedHashPassword cost 11 trước khi lưu vào bảng users. | `class:src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Services/BCryptPasswordHasher.cs:BCryptPasswordHasher` |
| 5 | Cấp cặp token và cookie phiên | Sinh access token JWT kèm claim sub/email/role và refresh token opaque được lưu vào kho, sau đó trả cookie HttpOnly cho client. | `function:src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Identity/JwtTokenService.cs:CreateTokenPairAsync` |

#### Đăng nhập và quản lý phiên

Khách hàng đăng nhập bằng email và mật khẩu, nhận access token JWT cùng cookie refresh token, làm mới token bằng cơ chế xoay vòng và đăng xuất để thu hồi phiên.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Xác thực email và mật khẩu | IdentityService đăng nhập với khoá dòng FOR UPDATE trên bảng users, kiểm tra mật khẩu bằng BCrypt và trạng thái tài khoản. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:LoginAsync` |
| 2 | Phát access token và refresh token | Sinh JWT access token ký HMAC-SHA256 kèm session id và lưu hash refresh token vào bảng refresh_tokens. | `function:src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Identity/JwtTokenService.cs:CreateTokenPairAsync` |
| 3 | Đặt cookie phiên HttpOnly | Refresh token được đặt trong cookie HttpOnly SameSite Strict, kèm cookie CSRF để bảo vệ các request dựa trên cookie. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Endpoints/AuthController.cs:AppendAuthCookies` |
| 4 | Xoay vòng token khi access token hết hạn | POST /api/v1/auth/refresh tiêu thụ refresh token cũ, kiểm tra hạn và cấp cặp token mới; request phải kèm CSRF token. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:RefreshAsync` |
| 5 | Đăng xuất và thu hồi phiên | Thu hồi refresh token hoặc toàn bộ phiên theo session id rồi xoá cookie phiên, vẫn cho phép khi JWT đã hết hạn. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:LogoutAsync` |

#### Quản lý hồ sơ và địa chỉ

Khách hàng xem và cập nhật hồ sơ, đổi mật khẩu kèm thu hồi phiên, đồng thời quản lý danh sách địa chỉ nhận hàng phục vụ checkout.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Xem hồ sơ cá nhân | MeController lấy user id từ claim JWT qua ICurrentUser rồi trả hồ sơ gồm thông tin cơ bản, vai trò và trạng thái tài khoản. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Endpoints/MeController.cs:GetProfile` |
| 2 | Cập nhật hồ tên và số điện thoại | Cập nhật thông tin cá nhân qua User.UpdateProfile rồi lưu thay đổi vào schema identity. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:UpdateProfileAsync` |
| 3 | Đổi mật khẩu và thu hồi phiên | Đổi mật khẩu trong transaction kèm khoá dòng và thu hồi toàn bộ refresh token đang mở của tài khoản. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:ChangePasswordAsync` |
| 4 | Quản lý địa chỉ nhận hàng | Thêm và xem danh sách địa chỉ nhận hàng của khách, mỗi địa chỉ giữ tên người nhận, số điện thoại và nội dung địa chỉ. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:AddAddressAsync` |

### Quản trị tài khoản nhân viên và phân quyền

Module Identity quản lý vòng đời tài khoản nhân viên và quản trị viên: tạo tài khoản STAFF/ADMIN, lọc danh sách, đổi vai trò, khoá/mở khoá và đặt lại mật khẩu. Song song, hệ thống phân quyền dựa trên policy theo vai trò được đăng ký trong BuildingBlocks và kiểm tra ở mọi endpoint quản trị.

**Entity chính**: User, UserRole, AuthorizationPolicies, ICurrentUser

**Quy tắc nghiệp vụ**:

- Chỉ ADMIN được gọi các endpoint /api/v1/admin/staff
- Không cho phép tự đổi vai trò hoặc tự khoá chính tài khoản đang thao tác
- Vai trò chỉ nhận giá trị CUSTOMER, STAFF hoặc ADMIN và không thể hạ về CUSTOMER
- Khoá tài khoản chấm dứt mọi phiên đang mở của tài khoản đó
- Bốn policy chuẩn: RequireAuthenticated, RequireCustomer, RequireStaff, RequireAdmin

#### Quản trị tài khoản nhân viên

Quản trị viên tạo, tra cứu, đổi vai trò, khoá/mở khoá và đặt lại mật khẩu cho các tài khoản nhân viên trong hệ thống.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Tạo tài khoản nhân viên | ADMIN tạo tài khoản nhân viên với vai trò STAFF hoặc ADMIN, từ chối email trùng và băm mật khẩu. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:CreateStaffAsync` |
| 2 | Lọc và phân trang danh sách nhân viên | Truy vấn danh sách tài khoản nhân viên theo từ khóa, vai trò và trạng thái kèm phân trang. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:GetStaffAsync` |
| 3 | Đổi vai trò tài khoản | Đổi vai trò trong transaction, chặn tự đổi vai trò của chính mình và thu hồi phiên cũ của tài khoản bị đổi vai trò. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Domain/Entities/User.cs:ChangeRole` |
| 4 | Khoá và mở khoá tài khoản | Khoá tài khoản bằng cách đảo trạng thái kích hoạt và chấm dứt mọi phiên đang mở. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:LockAccountAsync` |
| 5 | Đặt lại mật khẩu | Đặt lại mật khẩu cho tài khoản và thu hồi toàn bộ phiên đang mở. | `function:src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs:ResetPasswordAsync` |

#### Phân quyền và bảo vệ API

Hệ thống phân quyền theo vai trò CUSTOMER/STAFF/ADMIN thông qua policy dùng chung, đọc danh tính từ claim JWT và chuẩn hoá lỗi nghiệp vụ trả về client.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Đăng ký policy theo vai trò | Đăng ký bốn authorization policy dựa trên authentication và role của người dùng vào DI container. | `function:src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Identity/AuthorizationRegistration.cs:AddAppAuthorization` |
| 2 | Đọc danh tính từ claim | HttpContextCurrentUser lấy user id, vai trò và cờ IsAuthenticated từ claim của principal đã xác thực. | `class:src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Identity/HttpContextCurrentUser.cs:HttpContextCurrentUser` |
| 3 | Kiểm tra vai trò tại endpoint | Các controller quản trị áp policy RequireStaff/RequireAdmin để chặn khách hàng truy cập API quản lý. | `file:src/BuildingBlocks/PhoneStore.BuildingBlocks/Common/AuthorizationPolicies.cs` |
| 4 | Chuẩn hoá lỗi nghiệp vụ | AppExceptionMiddleware chuyển AppException và lỗi không lường trước thành ErrorResponse JSON thống nhất kèm mã lỗi. | `file:src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Web/AppExceptionMiddleware.cs` |

### Danh mục sản phẩm và quản lý Catalog

Module Catalog sở hữu toàn bộ dữ liệu sản phẩm bán lẻ điện thoại: danh mục, thương hiệu, sản phẩm, phiên bản SKU và ảnh sản phẩm. Module cung cấp API công khai để khách tìm kiếm và xem chi tiết, API quản trị cho nhân viên, đồng thời phục vụ giá cho Cart và Order qua IProductPricingContract và phát Domain Event tạo phiên bản cho Inventory.

**Entity chính**: Category, Brand, Product, ProductVariant, ProductImage

**Quy tắc nghiệp vụ**:

- Tên danh mục và thương hiệu là duy nhất, tối đa 100 ký tự
- SKU được chuẩn hoá chữ hoa và duy nhất trong toàn hệ thống
- Một sản phẩm có tối đa 20 phiên bản SKU
- Không xoá danh mục/thương hiệu khi còn sản phẩm tham chiếu, chỉ ẩn đi
- Giá và tồn kho của phiên bản phải lớn hơn 0, tồn kho không được âm
- Chỉ có biến thể trùng tổ hợp màu và dung lượng mới được thêm

#### Tìm kiếm và duyệt sản phẩm

Khách hàng tìm kiếm sản phẩm theo từ khoa, danh mục, thương hiệu và khoảng giá, có sắp xếp và phân trang trả về kết quả đóng gói chuẩn.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Tra cứu danh sách sản phẩm | GET /api/v1/products trả về danh sách sản phẩm công khai cho khách, không yêu cầu đăng nhập. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Endpoints/ProductsController.cs:GetProducts` |
| 2 | Lọc sản phẩm theo điều kiện | Lọc theo từ khoa, danh mục, thương hiệu, khoảng giá MinPrice/MaxPrice kèm kiểu sắp xếp được chấp nhận. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:GetProductsAsync` |
| 3 | Đóng gói kết quả phân trang | Kết quả trả về dưới dạng PagedResult gồm items, page, pageSize, totalCount và cờ hasNextPage/hasPreviousPage. | `file:src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/PagedResult.cs` |
| 4 | Tra cứu danh mục và thương hiệu | Khách đọc danh sách danh mục và thương hiệu đang hoạt động để lọc danh sách sản phẩm. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Endpoints/CategoriesController.cs:GetCategories` |

#### Xem chi tiết sản phẩm và SKU

Khách xem chi tiết sản phẩm cùng các phiên bản SKU (màu, dung lượng, giá, tồn kho) và ảnh sản phẩm trước khi đưa vào giỏ.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Xem chi tiết sản phẩm | GET /api/v1/products/{id} trả mô tả, thông số kỹ thuật, danh mục, thương hiệu và ảnh của sản phẩm. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Endpoints/ProductsController.cs:GetProductById` |
| 2 | Nạp phiên bản SKU kèm ảnh | CatalogService nạp sản phẩm cùng toàn bộ phiên bản và ảnh bằng eager loading rồi chuyển sang ProductDetailDto. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:GetProductByIdAsync` |
| 3 | Danh sách và lọc phiên bản SKU | GET /api/v1/admin/products/variants lọc danh sách phiên bản theo SKU, sản phẩm và trạng thái kèm phân trang. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Endpoints/ProductsController.cs:GetVariants` |

#### Quản lý danh mục và thương hiệu

Nhân viên quản trị danh mục và thương hiệu sản phẩm: tạo, cập nhật trạng thái, xoá hoặc ẩn khi đã có sản phẩm tham chiếu.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Tạo danh mục sản phẩm | Nhân viên tạo danh mục mới, kiểm tra trùng tên trước khi lưu vào schema catalog. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:CreateCategoryAsync` |
| 2 | Cập nhật danh mục | Cập nhật tên và trạng thái danh mục, chặn đổi tên khi danh mục đang được sử dụng. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:UpdateCategoryAsync` |
| 3 | Xoá hoặc ẩn danh mục | Từ chối xoá danh mục còn sản phẩm tham chiếu, thay vào đó chuyển sang trạng thái ẩn. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:DeleteCategoryAsync` |
| 4 | Quản lý thương hiệu | Tạo, cập nhật và xoá thương hiệu với cùng bộ quy tắc trùng tên và không xoá khi còn sản phẩm. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:CreateBrandAsync` |

#### Quản lý sản phẩm, SKU và giá

Nhân viên thực hiện CRUD sản phẩm, phiên bản SKU, giá bán và ảnh sản phẩm, đồng thời phát Domain Event khởi tạo tồn kho cho phiên bản mới.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Tạo sản phẩm kèm SKU ban đầu | Tạo sản phẩm với danh sách phiên bản SKU ban đầu, kiểm tra trùng SKU và tổ hợp màu dung lượng. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:CreateProductAsync` |
| 2 | Thêm phiên bản SKU mới | Thêm phiên bản SKU cho sản phẩm, kiểm tra trùng SKU và giới hạn tối đa 20 phiên bản mỗi sản phẩm. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:CreateVariantAsync` |
| 3 | Cập nhật giá và thông số phiên bản | Cập nhật SKU, màu, dung lượng, giá và trạng thái phiên bản; giá phải lớn hơn 0. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:UpdateVariantAsync` |
| 4 | Thêm và xoá ảnh sản phẩm | Thêm ảnh với URL và thứ tự sắp xếp, hoặc xoá ảnh theo id. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs:AddProductImageAsync` |
| 5 | Phát Domain Event tạo phiên bản | Sau khi tạo phiên bản, Catalog phát ProductVariantCreated để Inventory khởi tạo bản ghi tồn kho tương ứng. | `class:src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Catalog/ProductVariantCreated.cs:ProductVariantCreated` |

### Giỏ hàng

Module Cart quản lý giỏ hàng của khách hàng với trạng thái ACTIVE, CONVERTED và ABANDONED. Giỏ hàng là aggregate root chứa các dòng sản phẩm kèm ảnh chụp giá, tổng số lượng và tổng tiền; giá luôn được lấy từ Catalog qua IProductPricingContract chứ không nhận từ client.

**Entity chính**: Cart, CartItem, CartStatus, CartSnapshot

**Quy tắc nghiệp vụ**:

- Tối đa 50 sản phẩm khác nhau trong một giỏ hàng
- Mỗi dòng sản phẩm giới hạn số lượng từ 1 đến 10
- Chỉ giỏ hàng ở trạng thái ACTIVE mới được thêm, sửa hoặc xoá sản phẩm
- Dòng sản phẩm phải thuộc đúng giỏ hàng hiện tại mới được cập nhật hoặc xoá
- Thêm sản phẩm đã có sẽ gộp số lượng thay vì tạo dòng mới
- Giá sản phẩm luôn lấy từ Catalog, không tin giá do client gửi lên

#### Quản lý giỏ hàng

Khách thêm, sửa số lượng và xoá sản phẩm trong giỏ hàng; giá và tổng tiền luôn được server tính lại từ Catalog.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Xem giỏ hàng hiện tại | GET /api/v1/cart trả giỏ hàng ACTIVE của khách, tự tạo mới nếu khách chưa có giỏ hàng. | `function:src/Modules/Cart/PhoneStore.Modules.Cart/CartModuleRegistration.cs:GetCart` |
| 2 | Lấy giá sản phẩm từ Catalog | CartService gọi IProductPricingContract.GetVariantPricingAsync để lấy giá chuẩn của phiên bản SKU. | `function:src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/ProductPricingContract.cs:GetVariantPricingAsync` |
| 3 | Thêm sản phẩm vào giỏ hàng | Thêm sản phẩm, tự tạo giỏ hàng nếu cần và gộp số lượng nếu sản phẩm đã có trong giỏ. | `function:src/Modules/Cart/PhoneStore.Modules.Cart/Application/Services/CartService.cs:AddItemAsync` |
| 4 | Cập nhật số lượng sản phẩm | Cập nhật số lượng của dòng sản phẩm sau khi kiểm tra dòng đó thuộc giỏ hàng hiện tại. | `function:src/Modules/Cart/PhoneStore.Modules.Cart/Application/Services/CartService.cs:UpdateItemAsync` |
| 5 | Xoá sản phẩm khỏi giỏ hàng | Xoá một dòng sản phẩm khỏi giỏ hàng và lưu thay đổi xuống schema cart. | `function:src/Modules/Cart/PhoneStore.Modules.Cart/Application/Services/CartService.cs:RemoveItemAsync` |

### Đơn hàng và giao hàng COD

Module Order quản lý toàn bộ vòng đời đơn hàng từ checkout COD đến giao hàng và thu tiền: PENDING, CONFIRMED, SHIPPING, DELIVERED, DELIVERY_FAILED, CANCELLED. Order là aggregate root giữ chi tiết đơn, lịch sử trạng thái và điều khiển các chuyển trạng thái hợp lệ, đồng thời điều phối Cart, Catalog và Inventory qua các Contract.

**Entity chính**: Order, OrderItem, OrderStatusHistory, OrderStatus, CheckoutRequest

**Quy tắc nghiệp vụ**:

- Chỉ đơn PENDING mới được chuyển CONFIRMED, CONFIRMED mới được SHIPPING, SHIPPING mới được DELIVERED
- Đơn đã DELIVERED hoặc CANCELLED không được chuyển trạng thái
- Chỉ PENDING và CONFIRMED mới được huỷ
- Tổng tiền = tổng tiền hàng + phí vận chuyển, đơn đạt ngưỡng giá trị sẽ được miễn phí vận chuyển
- Mỗi đơn hàng gắn với một giỏ hàng, không cho checkout trùng giỏ hàng đã tạo đơn
- Số tiền thu COD phải dương và không nhỏ hơn tổng tiền đơn

#### Checkout COD tạo đơn hàng

Khách thanh toán COD: hệ thống kiểm tra giỏ hàng, chống đặt trùng đơn, lấy lại giá, đặt giữ tồn kho, tạo đơn PENDING kèm phí vận chuyển và đánh dấu giỏ hàng đã chuyển thành đơn.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Lấy giỏ hàng ACTIVE | Đọc snapshot giỏ hàng ACTIVE của khách qua ICartContract, trả lỗi nếu giỏ hàng trống. | `function:src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Cart/ICartContract.cs:GetActiveCartAsync` |
| 2 | Chống checkout trùng giỏ hàng | Tra cứu đơn theo CartId để chặn tạo trùng đơn từ cùng một giỏ hàng. | `function:src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Repositories/OrderRepository.cs:GetByCartIdAsync` |
| 3 | Lấy lại giá từ Catalog | Đọc giá hiện tại của từng phiên bản SKU qua IProductPricingContract thay vì tin giá lưu trong giỏ hàng. | `function:src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Catalog/IProductPricingContract.cs:GetVariantPricingAsync` |
| 4 | Đặt giữ tồn kho | Gọi IInventoryContract.TryReserveStockAsync để đặt giữ tồn kho theo từng phiên bản, dùng khoá dòng FOR UPDATE; thiếu hàng thì trả false. | `function:src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryContract.cs:TryReserveStockAsync` |
| 5 | Tạo đơn hàng PENDING | Order.Create khởi tạo đơn ở trạng thái PENDING, tạo chi tiết chụp giá và ghi lịch sử trạng thái ban đầu. | `function:src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs:Create` |
| 6 | Tính phí vận chuyển và tổng tiền | RecalculateTotals tính tổng tiền hàng và phí vận chuyển theo ngưỡng miễn phí vận chuyển. | `function:src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs:RecalculateTotals` |
| 7 | Sinh mã đơn hàng duy nhất | Sinh mã đơn với tiền tố ngày kèm hậu tố ngẫu nhiên, có kiểm tra trùng lặp và thử lại. | `function:src/Modules/Order/PhoneStore.Modules.Order/Application/Services/OrderService.cs:GenerateOrderNumberAsync` |
| 8 | Đánh dấu giỏ hàng CONVERTED | Sau khi tạo đơn thành công, giỏ hàng được chuyển sang trạng thái CONVERTED qua ICartContract. | `function:src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Cart/ICartContract.cs:MarkAsConvertedAsync` |

#### Xem danh sách và lịch sử đơn hàng

Khách hàng xem danh sách đơn đã đặt, chi tiết đơn và lịch sử chuyển trạng thái; dữ liệu được cô lập theo từng khách.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Xem danh sách đơn hàng | GET /api/v1/orders trả danh sách đơn của khách đang đăng nhập, có lọc theo trạng thái và khoảng thời gian kèm phân trang. | `function:src/Modules/Order/PhoneStore.Modules.Order/OrderModuleRegistration.cs:GetOrders` |
| 2 | Xem chi tiết đơn hàng | Đọc đơn theo id kèm chi tiết và lịch sử trạng thái, kiểm tra đơn thuộc về khách hoặc khách có quyền nhân viên. | `function:src/Modules/Order/PhoneStore.Modules.Order/Application/Services/OrderService.cs:GetOrderAsync` |
| 3 | Tra cứu lịch sử trạng thái | Lịch sử trạng thái ghi lại từng lần chuyển đổi kèm ghi chú và thời điểm thay đổi để đối chiếu. | `class:src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/OrderStatusHistory.cs:OrderStatusHistory` |
| 4 | Truy vấn đơn phân trang theo bộ lọc | Repository lọc theo trạng thái, khoảng thời gian và trả về tổng số bản ghi cùng tổng số trang. | `function:src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Repositories/OrderRepository.cs:GetByCustomerPagedAsync` |

#### Nhân viên xác nhận, giao hàng và thu tiền COD

Nhân viên điều hành vòng đời giao hàng: xác nhận đơn, giao cho vận chuyển, giao thành công kèm thu tiền COD hoặc ghi nhận giao thất bại, kèm thống kê doanh thu COD.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Nhân viên xác nhận đơn | Nhân viên xác nhận đơn, chuyển trạng thái PENDING sang CONFIRMED kèm ghi chú tùy chọn. | `function:src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs:Confirm` |
| 2 | Giao hàng cho đơn vị vận chuyển | Ghi nhận đơn vị vận chuyển và mã vận đơn, chuyển trạng thái CONFIRMED sang SHIPPING. | `function:src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs:Ship` |
| 3 | Giao thành công và thu tiền COD | Ghi nhận số tiền COD thực thu, kiểm tra không nhỏ hơn tổng tiền đơn và chuyển sang DELIVERED. | `function:src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs:Deliver` |
| 4 | Giao thất bại | Đánh dấu giao thất bại DELIVERY_FAILED hoặc huỷ đơn trực tiếp kèm lý do. | `function:src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs:MarkDeliveryFailed` |
| 5 | Thống kê COD đã thu | Truy vấn các đơn đã giao thành công để lập báo cáo doanh thu COD theo khoảng thời gian và phân trang. | `function:src/Modules/Order/PhoneStore.Modules.Order/Application/Services/OrderService.cs:GetCodCollectionsAsync` |

#### Huỷ đơn và hoàn trả tồn kho

Khách hoặc nhân viên huỷ đơn hợp lệ; đơn chuyển sang CANCELLED và phát Domain Event OrderCancelled để Inventory hoàn trả số lượng đã đặt giữ.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Kiểm tra quyền huỷ đơn | Xác minh đơn thuộc khách hiện tại hoặc do nhân viên xử lý và trạng thái còn cho phép huỷ. | `function:src/Modules/Order/PhoneStore.Modules.Order/Application/Services/OrderService.cs:CancelOrderAsync` |
| 2 | Chuyển đơn sang CANCELLED | Order.Cancel ghi nhận lý do huỷ, ghi lịch sử trạng thái và phát Domain Event OrderCancelled. | `function:src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs:Cancel` |
| 3 | Phát Domain Event OrderCancelled | Event chứa id đơn, id khách, số đơn và danh sách sản phẩm bị huỷ để module Inventory xử lý. | `class:src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Order/OrderCancelled.cs:OrderCancelled` |
| 4 | Event handler hoàn trả tồn kho | OrderCancelledHandler nhận event qua Event Bus, chuyển chi tiết đơn thành StockChangeItem và gọi RestoreStockAsync. | `function:src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/EventHandlers/OrderCancelledHandler.cs:Handle` |
| 5 | Hoàn trả tồn kho đã đặt giữ | InventoryContract cộng lại số lượng đã đặt giữ vào tồn kho, ghi log RESTORE để truy vết. | `function:src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Inventory/IInventoryContract.cs:RestoreStockAsync` |

### Kho và tồn kho

Module Inventory quản lý tồn kho của từng phiên bản sản phẩm với hai bảng product_stocks và stock_movements trong schema inventory. Mọi biến động tồn kho đều được ghi log với số lượng trước/sau, loại biến động và đơn hàng liên quan; module cũng cung cấp Contract đặt giữ và hoàn trả tồn kho cho Order.

**Entity chính**: ProductStock, StockMovement, StockMovementType, IInventoryContract

**Quy tắc nghiệp vụ**:

- Tồn kho không được âm trong mọi thao tác đặt giữ, nhập và điều chỉnh
- Mỗi biến động tồn kho đều phải có log với số lượng trước và sau
- Đặt giữ tồn kho dùng khoá dòng FOR UPDATE theo thứ tự id biến bản để tránh deadlock
- Lô nhập tối đa 200 dòng và mỗi dòng phải có số lượng lớn hơn 0
- Điều chỉnh tay phải khác 0 và kết quả cuối cùng không được âm

#### Nhập kho và điều chỉnh tồn kho

Nhân viên kho nhập hàng đơn lẻ hoặc lô, điều chỉnh tồn kho thủ công và khởi tạo tồn kho cho SKU mới, mọi thay đổi đều được ghi log.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Nhập kho cho một phiên bản | InventoryService tạo hoặc lấy bản ghi tồn kho, cộng thêm số lượng nhập và ghi log IMPORT. | `function:src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryService.cs:ImportStockAsync` |
| 2 | Nhập kho nhiều phiên bản | Xử lý lô nhập tối đa 200 dòng, tái sử dụng logic nhập đơn lẻ và tổng hợp kết quả từng dòng. | `function:src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryService.cs:ImportStockBatchAsync` |
| 3 | Điều chỉnh tồn kho thủ công | Điều chỉnh tăng hoặc giảm số lượng tồn kho, bảo đảm kết quả không âm và ghi log ADJUSTMENT. | `function:src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryService.cs:AdjustStockAsync` |
| 4 | Khởi tạo tồn kho cho SKU mới | Handler ProductVariantCreated tạo bản ghi ProductStock một cách idempotent kèm số lượng tồn ban đầu. | `function:src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/EventHandlers/ProductVariantCreatedHandler.cs:Handle` |

#### Theo dõi tồn kho và báo cáo

Nhân viên theo dõi tồn kho hiện tại, tra cứu log biến động có phân trang và xem danh sách cảnh báo tồn kho thấp.

| # | Bước | Mô tả | Nơi thực hiện |
|---|---|---|---|
| 1 | Xem tồn kho hiện tại | Tra cứu danh sách tồn kho toàn bộ phiên bản hoặc tồn kho của một phiên bản cụ thể. | `function:src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryService.cs:GetStocksAsync` |
| 2 | Tra cứu log biến động tồn kho | Lọc log theo biến bản, loại biến động, đơn hàng và khoảng thời gian kèm phân trang. | `function:src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryService.cs:GetMovementsAsync` |
| 3 | Cảnh báo tồn kho thấp | Trả về danh sách phiên bản có tồn kho dưới ngưỡng để nhân viên kho bổ sung hàng. | `function:src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryService.cs:GetLowStockAsync` |

## Kiến trúc phân tầng

### Lớp API Host

Điểm vào HTTP của dịch vụ: Program.cs khởi tạo ASP.NET Core, seeding dữ liệu, đăng ký DI cho 5 module, cùng toàn bộ Controller (Endpoints) cho Auth, Me, Staff, Products, Categories, Brands.

| File | Mô tả | Độ phức tạp |
|---|---|---|
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/BuildingBlocksRegistration.cs` | Điểm đăng ký DI của BuildingBlocks: MediatR, event bus, dispatcher, ICurrentUser, JWT bearer authentication, policy theo vai trò và antiforgery CSRF. | complex |
| `src/Host/PhoneStore.Api/PhoneStore.Api.http` | File .http của Visual Studio/REST Client khai báo biến host address và một request mẫu tới endpoint weatherforecast. | simple |
| `src/Host/PhoneStore.Api/Program.cs` | Entry point cua API PhoneStore: khoi dong WebApplication, dang ky Swagger, validation, exception handling, CORS, CSRF va toan bo 5 module (Identity, Catalog, Cart, Order, Inventory). | moderate |
| `src/Host/PhoneStore.Api/Seeding/DatabaseSeeder.cs` | Seed du lieu toi thieu cho moi truong local: tai khoan Identity (khach/nhan vien/admin), danh muc - thuong hieu - san pham Catalog va ton kho ban dau cho Inventory; thao tac idempotent. | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/CartModuleRegistration.cs` | Dang ky module Cart vao DI (DbContext PostgreSQL, repository, service, contract) va cung cap CartController cho cac endpoint gio hang. | moderate |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/CatalogModuleRegistration.cs` | Dang ky module Catalog vao DI: CatalogDbContext PostgreSQL, ICatalogService va IProductPricingContract. | simple |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Endpoints/BrandsController.cs` | REST controller cho quản lý thương hiệu sản phẩm (FR-12): đọc mở cho khách, ghi yêu cầu vai trò STAFF/ADMIN. | moderate |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Endpoints/CategoriesController.cs` | REST controller cho quản lý danh mục sản phẩm (FR-12): đọc mở cho khách, ghi yêu cầu vai trò STAFF/ADMIN. | moderate |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Endpoints/ProductsController.cs` | REST controller cho sản phẩm, SKU và ảnh sản phẩm: tra cứu mở cho khách và CRUD quản trị yêu cầu vai trò STAFF. | complex |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Endpoints/AuthController.cs` | Controller xác thực (FR-01/FR-02): đăng ký, đăng nhập, làm mới token, đăng xuất và phát CSRF token, refresh token được đặt trong cookie HttpOnly. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Endpoints/MeController.cs` | Controller cho hồ sơ cá nhân (FR-03): xem/cập nhật thông tin, đổi mật khẩu và quản lý địa chỉ nhận hàng của người đang đăng nhập. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Endpoints/StaffController.cs` | Controller quản trị tài khoản nhân viên (FR-15): tạo, danh sách, đổi vai trò, khoá/mở khoá và đặt lại mật khẩu, chỉ ADMIN được gọi. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/IdentityModuleRegistration.cs` | Điểm đăng ký DI của module Identity: DbContext Npgsql, BCrypt hasher, dịch vụ JWT, kho refresh token và IIdentityService. | simple |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/InventoryModuleRegistration.cs` | 库存模块注册入口：配置 DbContext 与服务依赖，并定义需 STAFF/ADMIN 角色的库存管理 REST 控制器。 | moderate |
| `src/Modules/Order/PhoneStore.Modules.Order/OrderModuleRegistration.cs` | Composition root of the Order module: registers the DbContext, repository and application service, and hosts the OrdersController exposing checkout, cancel, confirm, ship, deliver and COD collection endpoints. | complex |

### Lớp Application Services

Use case và logic ứng dụng của các module Identity, Catalog, Cart, Order, Inventory: service nghiệp vụ, contract liên module, DTO và FluentValidator.

| File | Mô tả | Độ phức tạp |
|---|---|---|
| `src/Modules/Cart/PhoneStore.Modules.Cart/Application/DTOs/DTOs.cs` | Cac record DTO cua module Cart: CartDto, CartItemDto va cac request them/sua san pham trong gio hang (gia khong nhan tu client). | simple |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Application/Services/CartContract.cs` | Implement ICartContract cua module Cart, cung cap snapshot gio hang va chuyen gio hang sang trang thai CONVERTED de module Order su dung ma khong can reference truc tiep DbContext. | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Application/Services/CartService.cs` | Dich vu nghiep vu gio hang: them / sua / xoa san pham, tu dong tao gio hang neu chua co va luon lay gia san pham tu Catalog qua IProductPricingContract. | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Application/Validators/CartValidators.cs` | Validator FluentValidation cho request them va cap nhat san pham trong gio hang: id phien ban > 0 va so luong tu 1 den 10. | simple |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/DTOs/DTOs.cs` | Tap DTO cua module Catalog: danh muc, thuong hieu, san pham, phien ban SKU, anh san pham va cac request/filter phan trang dung chung. | moderate |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs` | Dich vu nghiep vu lon nhat cua module Catalog: quan ly danh muc, thuong hieu, san pham, phien ban SKU, anh san pham, loc phan trang va publish su kien tao phien ban. | complex |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/ProductPricingContract.cs` | Implement IProductPricingContract cua module Catalog, tra ve gia va thong tin phien ban de module Cart lay gia chuan khong phai truy van truc tiep Catalog. | simple |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Validators/CatalogValidators.cs` | Tap validator FluentValidation cho module Catalog: danh muc, thuong hieu, san pham, phien ban SKU, anh san pham va cac bo loc phan trang. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Application/DTOs/DTOs.cs` | Tập hợp record DTO cho module Identity: đăng ký/đăng nhập, hồ sơ người dùng, địa chỉ và quản trị tài khoản nhân viên. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs` | Dịch vụ ứng dụng của module Identity xử lý đăng ký, đăng nhập, xoay vòng refresh token, hồ sơ, địa chỉ và quản trị tài khoản nhân viên. | complex |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Application/Validators/IdentityValidators.cs` | Bộ validator FluentValidation cho toàn bộ DTO của module Identity: đăng ký, đăng nhập, hồ sơ, mật khẩu, địa chỉ và quản trị nhân viên. | complex |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/DTOs/DTOs.cs` | 库存模块的请求/响应数据传输对象集合，涵盖入库、调整、批量入库与库存变动日志查询。 | simple |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/EventHandlers/OrderCancelledHandler.cs` | 处理 Order 模块发布的 OrderCancelled 领域事件，调用库存契约把已预留库存返还。 | simple |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/EventHandlers/ProductVariantCreatedHandler.cs` | 监听 Catalog 模块的 ProductVariantCreated 事件，为新建商品变体幂等地初始化库存记录。 | moderate |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryContract.cs` | 库存对外契约实现，提供基于行锁（FOR UPDATE）的库存预留与回滚能力，供订单模块跨模块调用。 | moderate |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryService.cs` | 库存应用服务，实现库存查询、入库、批量入库、手工调整与变动日志分页查询等管理端用例。 | complex |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Validators/InventoryValidators.cs` | 库存管理接口的 FluentValidation 校验器集合，约束入库、批量入库、调整与日志查询参数。 | moderate |
| `src/Modules/Order/PhoneStore.Modules.Order/Application/DTOs/DTOs.cs` | 订单模块的数据传输对象集合，包含订单列表/详情、结账、取消、确认、发货、交付与 COD 收款相关契约。 | moderate |
| `src/Modules/Order/PhoneStore.Modules.Order/Application/Services/OrderService.cs` | 订单应用服务，实现 COD 结账、订单查询、取消、确认、发货、交付与 COD 收款统计等用例，并协调购物车、定价与库存契约。 | complex |
| `src/Modules/Order/PhoneStore.Modules.Order/Application/Validators/OrderValidators.cs` | 订单接口的 FluentValidation 校验器集合，覆盖结账、取消、过滤、确认、发货与交付失败请求。 | moderate |

### Lớp Domain Model

Mô hình nghiệp vụ cốt lõi: Aggregate Root, Entity và Enum của các module, cùng các khái niệm Domain dùng chung (BaseEntity, AggregateRoot, Result, IDomainEvent).

| File | Mô tả | Độ phức tạp |
|---|---|---|
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Domain/AggregateRoot.cs` | Lớp cơ sở aggregate root gom các domain event phát sinh trong quá trình thay đổi trạng thái để dispatch sau khi commit. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Domain/BaseEntity.cs` | Lớp cơ sở cho entity với id generic, dấu thời gian tạo/cập nhật và phương thức Touch để đánh dấu thay đổi. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Domain/IDomainEvent.cs` | Interface đánh dấu đối tượng là domain event, yêu cầu mốc thời gian phát sinh. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Domain/Result.cs` | Định nghĩa mẫu Result/Error để trả về lỗi nghiệp vụ có mã và phân loại thay vì ném exception. | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Domain/Entities/Cart.cs` | Aggregate root Gio hang: giu danh sach dong san pham, tinh tong so luong / tong tien va ap dung quy tac nghiep vu (gio toi da 50 san pham, chi gio ACTIVE moi duoc them). | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Domain/Entities/CartItem.cs` | Entity dong san pham trong gio hang: luu id phien ban, ten, SKU, don gia va so luong, ap dung gioi han 10 don vi moi san pham. | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Domain/Enums/CartStatus.cs` | Enum trang thai gio hang: ACTIVE, CONVERTED (da thanh toan) va ABANDONED (bo qua). | simple |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Domain/Entities/CategoryAndBrand.cs` | Entity danh muc va thuong hieu cua module Catalog, moi entity co factory Create va phuong thuc Update cho ten va trang thai. | simple |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Domain/Entities/Product.cs` | Entity san pham cua module Catalog: aggregate root Product kem phien ban SKU (ProductVariant) va anh (ProductImage), moi phien ban giu gia va ton kho rieng. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Domain/Entities/Address.cs` | Thực thể địa chỉ nhận hàng thuộc về người dùng, với factory Create và phương thức Update giữ bất biến của domain. | simple |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Domain/Entities/RefreshToken.cs` | Thực thể lưu hash SHA-256 của refresh token kèm thời hạn và trạng thái thu hồi, hỗ trợ cơ chế xoay vòng token. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Domain/Entities/User.cs` | Thực thể người dùng (khách hàng và nhân viên) kế thừa AggregateRoot, đóng gói quy tắc vai trò, khoá tài khoản và cập nhật hồ sơ. | complex |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Domain/Entities/UserRole.cs` | Enum vai trò người dùng gồm CUSTOMER, STAFF và ADMIN, dùng cho chính sách phân quyền và lưu trữ dạng chuỗi. | simple |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Domain/Entities/ProductStock.cs` | 库存领域实体，维护单个商品变体的库存数量并封装预留、归还、入库与调整等不变式。 | moderate |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Domain/Entities/StockMovement.cs` | 库存变动日志领域模型，定义变动类型枚举并记录每次库存变化的变更前后数量与关联订单。 | moderate |
| `src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs` | 订单聚合根，管理订单状态机、金额计算、明细与状态历史，并在取消/交付失败时发布 OrderCancelled 领域事件。 | complex |
| `src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/OrderItem.cs` | Domain entity representing a purchased product line inside an order, snapshotting product name, SKU, unit price and quantity at checkout time. | simple |
| `src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/OrderItemDraft.cs` | Lightweight record used to carry order line data from the application layer into the domain entity factory without persistence concerns. | simple |
| `src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/OrderStatusHistory.cs` | Domain entity recording every order status transition with optional note and timestamp, providing an audit trail for the fulfillment workflow. | simple |
| `src/Modules/Order/PhoneStore.Modules.Order/Domain/Enums/OrderStatus.cs` | Enumeration of the order lifecycle states (PENDING, CONFIRMED, SHIPPING, DELIVERED, DELIVERY_FAILED, CANCELLED) persisted as string values. | simple |

### Lớp Infrastructure & Persistence

Hiện thực hạ tầng dữ liệu: DbContext riêng cho từng module, EF Core Migrations theo PostgreSQL schema, Repository và các service hạ tầng (BCrypt, RefreshTokenStore).

| File | Mô tả | Độ phức tạp |
|---|---|---|
| `src/Modules/Cart/PhoneStore.Modules.Cart/Infrastructure/Persistence/CartDbContext.cs` | DbContext EF Core cua module Cart tren schema "cart", cau hinh bang carts va cart_items kem chi muc duy nhat theo (CartId, ProductVariantId). | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Infrastructure/Persistence/Migrations/20261025173409_InitialCart.Designer.cs` | File Designer do EF Core sinh cho migration InitialCart, mo ta model dich cho dung khi tao hoac rollback migration. | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Infrastructure/Persistence/Migrations/20261025173409_InitialCart.cs` | Migration EF Core tao schema cart voi bang carts va cart_items, chi muc gan va rang buoc cascade. | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Infrastructure/Persistence/Migrations/CartDbContextModelSnapshot.cs` | Snapshot model hien tai cua CartDbContext do EF Core sinh, dung lam co so de tao migration moi cho module Cart. | moderate |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Infrastructure/Repositories/CartRepository.cs` | Repository EF Core cho gio hang: truy van gio hang theo nguoi dung / id, them xoa va luu thay doi. | simple |
| `src/Modules/Cart/PhoneStore.Modules.Cart/Infrastructure/Repositories/ICartRepository.cs` | Hop dong repository cho gio hang: truy van theo nguoi dung / id, them xoa va luu thay doi. | simple |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Infrastructure/Persistence/CatalogDbContext.cs` | DbContext EF Core của module Catalog định nghĩa mapping cho danh mục, thương hiệu, sản phẩm, SKU và ảnh trong schema catalog. | moderate |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Infrastructure/Persistence/Migrations/20261025173356_InitialCatalog.Designer.cs` | File Designer do EF Core sinh cho migration InitialCatalog, mô tả mô hình đích của schema catalog tại thời điểm sinh. | complex |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Infrastructure/Persistence/Migrations/20261025173356_InitialCatalog.cs` | Migration khởi tạo schema catalog: tạo các bảng brands, categories, products, product_variants và product_images cùng index. | complex |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/Infrastructure/Persistence/Migrations/CatalogDbContextModelSnapshot.cs` | Snapshot mô hình hiện tại của CatalogDbContext, dùng để EF Core sinh migration tiếp theo. | complex |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Persistence/IdentityDbContext.cs` | DbContext EF Core của module Identity định nghĩa mapping cho bảng users, addresses và refresh_tokens trong schema identity. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Persistence/IdentityDbContextFactory.cs` | Factory DbContext thiết kế thời cho module Identity, cho phép chạy dotnet ef migrations mà không cần khởi động Web API host. | simple |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Persistence/Migrations/20261025174244_InitialIdentity.Designer.cs` | File Designer do EF Core sinh cho migration InitialIdentity, mô tả mô hình đích gồm bảng users và addresses trong schema identity. | complex |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Persistence/Migrations/20261025174244_InitialIdentity.cs` | Migration khởi tạo schema identity: tạo bảng users và addresses cùng index duy nhất email và liên kết khóa ngoại. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Persistence/Migrations/20261026154105_AddRefreshTokens_OrderFulfillment_StockMovements.Designer.cs` | File Designer do EF Core sinh cho migration bổ sung bảng refresh_tokens, mô tả mô hình đích của schema identity sau thay đổi. | complex |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Persistence/Migrations/20261026154105_AddRefreshTokens_OrderFulfillment_StockMovements.cs` | Migration bổ sung bảng refresh_tokens trong schema identity với index duy nhất trên token hash và index theo user. | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Persistence/Migrations/IdentityDbContextModelSnapshot.cs` | EF Core 模型快照，记录 Identity 模块当前完整的实体映射结构，供后续迁移生成差异使用。 | moderate |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Services/BCryptPasswordHasher.cs` | 基于 BCrypt 的密码哈希实现，封装身份模块的口令散列与校验。 | simple |
| `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Services/RefreshTokenStore.cs` | 刷新令牌持久化服务，基于 identity schema 的 refresh_tokens 表实现存储、一次性消费、吊销与有效性校验。 | moderate |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Infrastructure/Persistence/InventoryDbContext.cs` | 库存模块的 EF Core DbContext，映射 inventory schema 下的 product_stocks 与 stock_movements 两张表。 | simple |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Infrastructure/Persistence/InventoryDbContextFactory.cs` | 设计时 DbContext 工厂，使 dotnet ef migrations 命令无需启动整个 Web API 即可生成迁移。 | simple |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Infrastructure/Persistence/Migrations/20261025173434_InitialInventory.Designer.cs` | InitialInventory 迁移的 Designer 伴随文件，描述迁移的目标模型快照（EF 自动生成）。 | simple |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Infrastructure/Persistence/Migrations/20261025173434_InitialInventory.cs` | 库存模块的初始迁移：创建 inventory schema 与 product_stocks 表及其唯一索引。 | moderate |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Infrastructure/Persistence/Migrations/20261026154118_AddStockMovements.Designer.cs` | AddStockMovements 迁移的 Designer 伴随文件，包含新增日志表后的目标模型（EF 自动生成）。 | moderate |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Infrastructure/Persistence/Migrations/20261026154118_AddStockMovements.cs` | 库存模块第二个迁移：创建 stock_movements 日志表并按变体、订单、类型与时间建立索引。 | moderate |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/Infrastructure/Persistence/Migrations/InventoryDbContextModelSnapshot.cs` | 库存模块当前模型快照，记录 product_stocks 与 stock_movements 的最新映射，供后续迁移比对。 | moderate |
| `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/Migrations/20261025173421_InitialOrder.Designer.cs` | Auto-generated EF Core designer snapshot for the initial Order migration, describing the full target model (orders, order_items, order_status_histories) of the ordering schema. | complex |
| `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/Migrations/20261025173421_InitialOrder.cs` | Initial EF Core migration creating the ordering schema with orders, order_items and order_status_histories tables plus their indexes and foreign keys. | moderate |
| `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/Migrations/20261026154131_AddOrderFulfillmentFields.Designer.cs` | Auto-generated EF Core designer file capturing the Order model after fulfillment fields (carrier, tracking number, COD amount, cart reference) were added. | complex |
| `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/Migrations/20261026154131_AddOrderFulfillmentFields.cs` | EF Core migration adding carrier, tracking number, collected amount, collected-at and cart-id columns to the orders table for the fulfillment workflow. | moderate |
| `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/Migrations/OrderDbContextModelSnapshot.cs` | Current EF Core model snapshot of the Order module, used as the baseline for generating the next migration. | complex |
| `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/OrderDbContext.cs` | EF Core DbContext for the Order module mapping orders, order items and status histories into the "ordering" PostgreSQL schema with indexes and cascade deletes. | moderate |
| `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/OrderDbContextFactory.cs` | Design-time DbContext factory enabling `dotnet ef migrations` to run for the Order module without booting the whole Web API host. | simple |
| `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Repositories/IOrderRepository.cs` | Repository contract for order persistence covering lookup by id, order number and cart, filtered paged customer listings, delivered-order queries and write operations. | simple |
| `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Repositories/OrderRepository.cs` | EF Core implementation of IOrderRepository providing eager-loaded order reads, filtered paged queries and unit-of-work style write operations. | moderate |

### Lớp Cross-Cutting BuildingBlocks

Shared kernel dùng chung cho mọi module: CQRS/Result wrapper, ApiResponse, middleware (exception, CSRF, filter), EventBus, JwtTokenService, contract tích hợp và cơ chế validation.

| File | Mô tả | Độ phức tạp |
|---|---|---|
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/CQRS.cs` | Định nghĩa các abstraction CQRS dùng chung: ICommand/IQuery kế thừa MediatR IRequest và các handler tương ứng trả về Result. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/ICurrentUser.cs` | Interface đọc danh tính người dùng đang đăng nhập từ claim trong access token: UserId, Role, IsAuthenticated và hàm kiểm tra vai trò. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/IDomainEventDispatcher.cs` | Interface phát tán các domain event đã gom trên aggregate, mặc định gọi sau SaveChanges để bảo đảm dữ liệu đã commit. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/IEventBus.cs` | Interface event bus nội bộ cho phát sự kiện miền nghiệp vụ giữa các module qua MediatR. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/IRefreshTokenStore.cs` | Interface lưu trữ refresh token hỗ trợ xoay vòng token và đăng xuất (thu hồi), đặt ở BuildingBlocks để module Identity không rò rỉ DbContext. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/ITokenService.cs` | Định nghĩa TokenPair (access token, refresh token, thời điểm hết hạn) và interface phát cặp token, tách biệt nghiệp vụ làm mới token của module Identity. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/PagedResult.cs` | Kiểu kết quả phân trang dùng chung: items, page, pageSize, totalCount kèm các thuộc tính dẫn xuất totalPages, hasPreviousPage, hasNextPage. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Common/ApiResponse.cs` | Các envelope phản hồi HTTP chuẩn: ApiResponse<T> cho thành công (success, message, data, timestamp) và ErrorResponse cho lỗi (code, message, fieldErrors, requestId). | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Common/AuthorizationPolicies.cs` | Hằng số dùng chung cho phân quyền: vai trò CUSTOMER/STAFF/ADMIN, tên 4 authorization policy, tên cookie refresh/CSRF và header CSRF. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Common/JwtOptions.cs` | Cấu hình JWT đọc từ mục "Authentication" của appsettings: issuer, audience, khoá ký, thời hạn access/refresh token và cơ chế khoá ký phát triển. | moderate |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Cart/ICartContract.cs` | Định nghĩa contract đọc và đánh dấu giỏ hàng để module Order không cần tham chiếu trực tiếp CartDbContext. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Catalog/IProductPricingContract.cs` | Contract truy vấn giá hiện tại của phiên bản sản phẩm do Catalog sở hữu, dùng chung cho Cart và Order. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Catalog/ProductVariantCreated.cs` | Domain event thông báo Catalog tạo phiên bản sản phẩm mới, dùng để Inventory khởi tạo tồn kho. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Inventory/IInventoryContract.cs` | Contract đặt giữ và hoàn trả tồn kho cho module Order, tránh tham chiếu trực tiếp InventoryDbContext. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Order/OrderCancelled.cs` | Domain event phát ra khi đơn hàng bị hủy, để Inventory hoàn trả tồn kho đã đặt giữ. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Exceptions/AppException.cs` | Định nghĩa các exception nghiệp vụ (NotFound, Conflict, Unauthorized, Forbidden, Validation) mang mã lỗi và HTTP status. | moderate |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/EventBus/DomainEventDispatcher.cs` | Dispatcher gom domain event của aggregate và đẩy từng event lên event bus kèm log. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/EventBus/InMemoryEventBus.cs` | Event bus in-memory publish domain event qua Mediator INotification, không cần broker ngoài. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Identity/AuthorizationRegistration.cs` | Đăng ký các policy phân quyền dùng chung (RequireAuthenticated, RequireCustomer, RequireStaff, RequireAdmin). | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Identity/HttpContextCurrentUser.cs` | Đọc danh tính người dùng (id, vai trò) từ claim của principal đã xác thực trong HttpContext. | moderate |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Identity/JwtTokenService.cs` | Phát cặp access token JWT kèm refresh token opaque, chỉ lưu bản SHA-256 của refresh token trong kho. | moderate |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Web/AppExceptionMiddleware.cs` | Middleware chuyển AppException và lỗi không lường trước thành ErrorResponse chuẩn JSON. | moderate |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Web/CsrfProtectionMiddleware.cs` | Middleware bảo vệ CSRF cho endpoint dùng cookie xác thực, đối chiếu token trong cookie và header. | complex |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Web/FluentValidationFilter.cs` | Action filter tự động chạy FluentValidation cho mọi tham số action và trả ErrorResponse khi không hợp lệ. | moderate |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Web/StandardModelStateFilter.cs` | Chuẩn hóa lỗi ModelState của ASP.NET về cùng định dạng ErrorResponse mà AppExceptionMiddleware sử dụng. | moderate |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Web/StandardResponseOperationFilter.cs` | Operation filter bổ sung mô tả response chuẩn tiếng Việt cho các status code trong Swagger. | simple |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Web/SwaggerRegistration.cs` | Cấu hình Swagger/OpenAPI dùng chung: XML comments, security scheme Bearer và operation filter chuẩn. | moderate |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Validation/AppValidator.cs` | Lớp validator dùng chung kế thừa AbstractValidator, kèm extension chuyển lỗi sang ValidationException của BuildingBlocks. | moderate |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/Validation/ValidationRegistration.cs` | Đăng ký FluentValidation, các filter validate request và cấu hình JSON camelCase cho toàn hệ thống. | simple |

### Lớp Kiểm thử

Bộ test xUnit gồm UnitTests, IntegrationTests chạy PostgreSQL thật và ArchitectureTests kiểm tra ranh giới Clean Architecture giữa các module.

| File | Mô tả | Độ phức tạp |
|---|---|---|
| `tests/PhoneStore.ArchitectureTests/ModuleArchitectureTests.cs` | Architecture tests using NetArchTest to assert that each module assembly references only BuildingBlocks and never another business module, enforcing the modular monolith boundaries. | moderate |
| `tests/PhoneStore.ArchitectureTests/PhoneStore.ArchitectureTests.csproj` | MSBuild project file for the architecture test project referencing xUnit, FluentAssertions, NetArchTest and every module assembly under test. | simple |
| `tests/PhoneStore.IntegrationTests/ApiTestHelper.cs` | Shared integration-test helper wrapping HTTP calls, response envelope assertions, variant lookups and checkout flows so individual tests focus on behaviour only. | moderate |
| `tests/PhoneStore.IntegrationTests/AssemblyInfo.cs` | Assembly-level configuration disabling xUnit test parallelization because integration tests share the same environment database connection string. | simple |
| `tests/PhoneStore.IntegrationTests/AuthAndStaffApiTests.cs` | Integration tests covering registration, JWT access and refresh token rotation, logout revocation, locked accounts and the staff administration endpoints. | complex |
| `tests/PhoneStore.IntegrationTests/AuthTestHelper.cs` | Integration-test helper that performs real registration/login flows and returns HttpClient instances carrying genuine Bearer JWT tokens plus CSRF headers. | moderate |
| `tests/PhoneStore.IntegrationTests/BusinessFlowRegressionTests.cs` | Broad end-to-end regression suite covering cart isolation and limits, checkout to order creation, order numbering, shipping fee rules, cancellation history, catalog paging and customer isolation. | complex |
| `tests/PhoneStore.IntegrationTests/CartApiTests.cs` | Integration tests for the cart API covering cart creation, item merging, quantity updates and limits, item removal, not-found cases and unauthenticated access. | moderate |
| `tests/PhoneStore.IntegrationTests/CatalogAdminApiTests.cs` | Integration tests for catalog administration APIs: staff CRUD on categories, brands, products and variants, duplicate-SKU and unknown-reference conflicts, and role restrictions. | complex |
| `tests/PhoneStore.IntegrationTests/CatalogAndIdentityRegressionTests.cs` | Regression suite for catalog filtering, sorting, paging and validation errors plus identity registration and login rules such as email normalization and password policies. | complex |
| `tests/PhoneStore.IntegrationTests/CatalogQueryTests.cs` | Integration tests for the catalog product query API covering search, price filtering, sorting and pagination against a real PostgreSQL database. | moderate |
| `tests/PhoneStore.IntegrationTests/CheckoutFlowTests.cs` | Integration tests for the checkout and cart endpoints, verifying health, seeded products, server-side pricing and COD order creation. | moderate |
| `tests/PhoneStore.IntegrationTests/DatabaseMigrator.cs` | Test helper that applies EF Core migrations for every module DbContext (identity, catalog, cart, ordering, inventory) into a single test database. | simple |
| `tests/PhoneStore.IntegrationTests/EndToEndOrderJourneyTests.cs` | End-to-end journey tests walking a customer from browsing products through checkout, order lifecycle transitions, cancellation and stock restoration. | complex |
| `tests/PhoneStore.IntegrationTests/IdentityApiTests.cs` | Integration tests for the identity API covering registration, login, profile updates, password changes and per-user address management. | complex |
| `tests/PhoneStore.IntegrationTests/IdentitySessionRegressionTests.cs` | Regression tests for session and token behaviour: logout invalidation, refresh-token rotation, concurrency, CSRF enforcement and account-security invalidation. | complex |
| `tests/PhoneStore.IntegrationTests/InventoryApiTests.cs` | Integration tests for the inventory API covering stock import, adjustment, movement logging, low-stock reporting and staff-only authorization. | complex |
| `tests/PhoneStore.IntegrationTests/OrderFulfillmentApiTests.cs` | Integration tests for staff order fulfilment: confirm, ship, deliver with COD collection, failed delivery handling and status history. | moderate |
| `tests/PhoneStore.IntegrationTests/OrderLifecycleTests.cs` | Integration tests for the order lifecycle API covering listing, filtering, detail retrieval, cancellation rules and cross-user access protection. | moderate |
| `tests/PhoneStore.IntegrationTests/PhoneStoreApiFactory.cs` | WebApplicationFactory-based test host that provisions an isolated PostgreSQL database, runs migrations before startup and drops the database afterwards. | moderate |
| `tests/PhoneStore.IntegrationTests/StockRestoreOnCancelTests.cs` | Integration test asserting that cancelling an order restores the reserved stock quantity of the ordered product variants. | moderate |
| `tests/PhoneStore.IntegrationTests/PhoneStore.IntegrationTests.csproj` | MSBuild project file for the integration test suite referencing the API host project plus xUnit, FluentAssertions, TestHost and Npgsql packages. | simple |
| `tests/PhoneStore.UnitTests/CartServiceTests.cs` | Unit tests for CartService using an in-memory fake repository to verify server-side pricing, not-found handling and implicit cart creation. | simple |
| `tests/PhoneStore.UnitTests/CartTests.cs` | Unit tests for the Cart aggregate covering creation, item merge and quantity limits, item removal and converted-cart rules. | moderate |
| `tests/PhoneStore.UnitTests/CatalogCategoryAndBrandTests.cs` | Extensive unit tests for catalog category and brand management: listing, lookup, create, update, validation, name-conflict and soft-versus-hard delete rules. | complex |
| `tests/PhoneStore.UnitTests/FakeCartRepository.cs` | In-memory test double implementing ICartRepository so cart service logic can be exercised without a database. | simple |
| `tests/PhoneStore.UnitTests/InventoryTests.cs` | Unit tests for the ProductStock entity covering initialization, negative-stock rejection, reserve and restore quantity behaviour. | simple |
| `tests/PhoneStore.UnitTests/JwtOptionsTests.cs` | Unit tests for JWT signing-key resolution: missing-key rejection, shared development fallback key and invalid token lifetime validation. | simple |
| `tests/PhoneStore.UnitTests/OrderStatusTransitionTests.cs` | Unit tests for the Order aggregate status machine, covering confirmation, shipping, delivery, failed delivery, cancellation and illegal transitions. | moderate |
| `tests/PhoneStore.UnitTests/OrderTests.cs` | Unit tests for Order creation totals, free-shipping thresholds, status-history recording, cancellation rules and domain event handling. | moderate |
| `tests/PhoneStore.UnitTests/PersistenceModelTests.cs` | Smoke tests that build the Cart and Order EF Core models to catch mapping or configuration errors at test time. | simple |
| `tests/PhoneStore.UnitTests/ProductFilterSortPaginationTests.cs` | Largest unit test suite for the catalog product query, validating filter parameters, sorting options, pagination metadata and DTO mapping. | complex |
| `tests/PhoneStore.UnitTests/ProductStockTests.cs` | Unit tests for ProductStock reserve, restore, import and adjust operations plus StockMovement quantity bookkeeping. | moderate |
| `tests/PhoneStore.UnitTests/ResultTests.cs` | Unit tests for the shared Result type verifying success and failure factory results and error code propagation. | simple |
| `tests/PhoneStore.UnitTests/PhoneStore.UnitTests.csproj` | MSBuild project file for the unit test suite referencing the BuildingBlocks and all domain modules plus xUnit and the EF Core in-memory provider. | simple |
| `tests/PhoneStore.UnitTests/UserAccountManagementTests.cs` | Unit test cho nguoi dung domain trong Identity module, phu bi quan ly tai khoan nhan vien, doi vai tro va khoa/mo khoa tai khoan theo FR-15 (N2LHTBLTT-16). | moderate |

### Lớp Tài liệu

Tài liệu thiết kế và báo cáo: README, báo cáo phân tích thiết kế, tài liệu yêu cầu/kiểm thử, review báo lỗi cùng các trang HTML tĩnh cho thiết kế hệ thống và học liệu.

| File | Mô tả | Độ phức tạp |
|---|---|---|
| `GIT_WORKFLOW.md` | Cẩm nang quy chuẩn Git cho cả nhóm: chiến lược phân nhánh main/develop/feature/bugfix, định dạng commit Conventional Commits kèm mã Jira, và quy trình làm việc hằng ngày từ pull đến mở Pull Request. | simple |
| `README.md` | Tài liệu tổng quan dự án PhoneStore Backend kiến trúc Modular Monolith + Clean Architecture, gồm phân công nhóm, cấu trúc thư mục, hướng dẫn cài đặt/chạy Docker, cơ chế JWT, tình trạng và cách chạy test, ví dụ gọi API bằng curl. | complex |
| `docs/auth/FR-01-FR-02.md` | Tài liệu đặc tả nghiệp vụ và thiết kế kỹ thuật cho FR-01 (đăng ký) và FR-02 (đăng nhập/đăng xuất/phiên) của module Identity, mô tả API contract, luồng transaction, chính sách CSRF/cookie và các trade-off đã biết. | moderate |
| `docs/auth/TEST-POSTMAN.md` | Hướng dẫn kiểm thử thủ công FR-01/FR-02 bằng Postman theo từng bước: khởi động backend, áp dụng migration, import collection 18 request và các kỳ vọng về status/cookie/CSRF cho đăng ký, đăng nhập, refresh, logout. | moderate |
| `docs/auth/validation.md` | Bằng chứng kiểm thử cho FR-01/FR-02 gồm kết quả 7 regression test trước/sau sửa, bảng tổng hợp 275 test đạt trên .NET 10 + PostgreSQL 17.9, cùng danh sách build warning và các giới hạn chưa kiểm chứng. | simple |
| `docs/design/index.html` | Trang chỉ mục của bộ tài liệu thiết kế hệ thống PhoneStore, liên kết tới hai tài liệu chính (phân rã chức năng và thiết kế hệ thống) và tóm tắt trạng thái hiện thực của dự án. | moderate |
| `docs/design/phan-ra-chuc-nang.html` | Tài liệu phân tích yêu cầu và phân rã chức năng: 16 yêu cầu chức năng, 9 yêu cầu phi chức năng, cây FBD 5 cấp, đặc tả use case, vòng đời trạng thái đơn hàng, 25 quy tắc nghiệp vụ và ma trận truy vết FR ↔ Task ↔ Endpoint. | complex |
| `docs/design/style.css` | Bảng định kiểu dùng chung cho cả ba trang tài liệu thiết kế, định nghĩa biến CSS cho theme tối, cùng các component như card, grid, badge, bảng, khối pre ASCII và lớp module. | moderate |
| `docs/design/thiet-ke-he-thong.html` | Tài liệu thiết kế hệ thống: kiến trúc Modular Monolith + Clean Architecture 6 tầng, ranh giới và ma trận phụ thuộc giữa 5 module, thiết kế CSDL 13 bảng trên 5 schema, ERD, các luồng kỹ thuật, cơ chế Contract/Domain Event, xử lý lỗi và hạ tầng/CI. | complex |
| `docs/reviews/ASK-MANH-N2LHTBLTT-12.md` | Tài liệu hỏi đáp giữa các thành viên về task N2LHTBLTT-12 (Cart/Order domain), nêu 3 điểm cần chốt: nguồn giá sản phẩm qua contract, transaction xuyên nhiều DbContext và event hoàn tồn kho khi hủy đơn; kèm phần ghi nhận giả định đã implement và báo cáo bug tìm được qua integration test. | complex |
| `docs/reviews/ASK-NGHIA-CATALOG-SORT-FILTER-BUG.md` | Báo cáo lỗi trong API danh mục sản phẩm FR-04: tham số SortBy, MinPrice/MaxPrice được validate nhưng bị bỏ qua khi truy vấn, kèm lỗi phân trang trả về toàn bộ kết quả và danh sách 10 test E2E đã bổ sung. | moderate |
| `docs/reviews/ASK-TRI-AUTH-TOKEN-ISSUE.md` | Tài liệu hỏi đáp về xác thực API Identity (FR-01/02/03): token đăng ký/đăng nhập là GUID giả không dùng để xác thực, fallback userId ?? 1 khiến endpoint hồ sơ không yêu cầu đăng nhập, và hành vi chưa rõ của endpoint logout. | moderate |
| `docs/reviews/N2LHTBLTT-12-review.html` | Trang HTML tĩnh trình bày review chi tiết task N2LHTBLTT-12 với 12 mục: danh sách file thay đổi, kiến trúc dữ liệu Cart/Order, review entity, repository, EF Core mapping, API contract DTO, sơ đồ quan hệ schema và checklist review. | complex |
| `.github/pull_request_template.md` | Mẫu Pull Request của repo yêu cầu mô tả thay đổi, checklist kiểm tra trước khi review và ảnh minh chứng kết quả kiểm thử. | simple |
| `docs/BaoCao_PhanTichThietKe_PhoneStore_CSharp_NET.docx` | Báo cáo phân tích thiết kế hệ thống PhoneStore (Word, ~4400 dòng nội dung) mô tả kiến trúc modular monolith, nghiệp vụ và cơ sở dữ liệu. | complex |
| `docs/JIRA_TASKS.md` | Bảng tra cứu Jira project N2LHTBLTT: thành viên, board theo team, issue theo từng người, ánh xạ FR → Issue → Board và trạng thái triển khai. | moderate |
| `docs/learning/app.js` | Logic của trang ôn tập: quản lý tiến độ trong localStorage, tính điểm tổng thể và render các mục dashboard, flashcard, quiz, code lab, cheat sheet, checklist. | complex |
| `docs/learning/data-cards.js` | Bộ dữ liệu flashcard (mảng CARDS) với câu hỏi, đáp án và giải thích về domain model, hạ tầng chung, giỏ hàng, checkout và kiểm thử. | moderate |
| `docs/learning/data-quiz.js` | Bộ dữ liệu câu hỏi trắc nghiệm (mảng QUIZ) dạng 4 lựa chọn kèm đáp án đúng và giải thích cho 7 task Jira. | complex |
| `docs/learning/data.js` | Dữ liệu nội dung ôn tập: danh sách task, dòng thời gian triển khai, sơ đồ kiến trúc và các luồng nghiệp vụ giỏ hàng / checkout / đơn hàng. | complex |
| `docs/learning/index.html` | Trang HTML tĩnh làm giao diện ôn tập toàn: header thống kê tiến độ, thanh điều hướng 9 mục và các section nội dung cho Cart, Order và Testing. | complex |
| `docs/learning/style.css` | Stylesheet của trang ôn tập: hệ biến CSS cho chế độ sáng/tối, layout responsive lưới card, thanh tiến độ vòng tròn và style cho quiz/lab. | moderate |

### Lớp Cấu hình & Build

Cấu hình build và môi trường của solution: file .csproj của các project, appsettings, launchSettings, PhoneStore.slnx, Directory.Build.props, .env.example và collection Postman.

| File | Mô tả | Độ phức tạp |
|---|---|---|
| `.env.example` | Mẫu biến môi trường cho môi trường local, chứa tài khoản test đã seed (ADMIN/STAFF/CUSTOMER/DEMO), đường dẫn API/Swagger/health check, thông số kết nối PostgreSQL và cấu hình JWT. | moderate |
| `src/Host/PhoneStore.Api/PhoneStore.Api.csproj` | Project file for the ASP.NET Core API host that references BuildingBlocks plus all six feature modules and enables Swagger and XML documentation generation for endpoint descriptions. | simple |
| `src/Host/PhoneStore.Api/appsettings.json` | Base application configuration defining log levels, allowed hosts, the PostgreSQL connection string for phonestore_db, and JWT authentication parameters such as issuer, audience and token lifetimes. | simple |
| `src/Host/PhoneStore.Api/appsettings.Development.json` | Development-environment overrides that raise log verbosity and supply a local HMAC signing key with a longer access-token lifetime for local debugging. | simple |
| `.gitattributes` | Cấu hình .gitattributes chuẩn hoá line-ending (LF/CRLF) và chế độ diff cho từng loại file trong repo, tránh nhiễu thay đổi giữa Windows và Linux. | simple |
| `Directory.Build.props` | File props cấp solution áp dụng chung cho mọi project: .NET 10, nullable bật, implicit usings và ngôn ngữ C# mới nhất. | simple |
| `PhoneStore.slnx` | Solution mới dạng XML (.slnx) liệt kê BuildingBlocks, Host API, 5 module nghiệp vụ và 3 project kiểm thử của hệ thống PhoneStore. | simple |
| `postman/PhoneStore-FR01-FR02.postman_collection.json` | Bộ Postman collection kiểm thử luồng đăng nhập FR-01/FR-02 với 18 request tuần tự (00–17), script lưu JWT/CSRF vào collection variable và cookie jar. | complex |
| `src/BuildingBlocks/PhoneStore.BuildingBlocks/PhoneStore.BuildingBlocks.csproj` | Project file của thư viện BuildingBlocks: sinh XML documentation cho Swagger và khai báo các package dùng chung (MediatR, EF Core, Swashbuckle, FluentValidation). | simple |
| `src/Host/PhoneStore.Api/Properties/launchSettings.json` | Cau hinh profile chay local cua API (http tren cong 5122, https tren 7168) va bien moi truong ASPNETCORE_ENVIRONMENT=Development. | simple |
| `src/Modules/Cart/PhoneStore.Modules.Cart/PhoneStore.Modules.Cart.csproj` | Tep project cua module Cart: tham chieu ASP.NET Core, EF Core 10, Npgsql PostgreSQL va project BuildingBlocks, kem sinh XML doc cho Swagger. | simple |
| `src/Modules/Catalog/PhoneStore.Modules.Catalog/PhoneStore.Modules.Catalog.csproj` | Tệp project MSBuild của module Catalog: khai báo framework ASP.NET Core, package EF Core/Npgsql và tham chiếu tới BuildingBlocks. | simple |
| `src/Modules/Identity/PhoneStore.Modules.Identity/PhoneStore.Modules.Identity.csproj` | Identity 模块的 MSBuild 项目文件，声明 ASP.NET Core 框架引用、BCrypt 与 EF Core PostgreSQL 包，并引用 BuildingBlocks 项目。 | simple |
| `src/Modules/Inventory/PhoneStore.Modules.Inventory/PhoneStore.Modules.Inventory.csproj` | 库存模块的 MSBuild 项目文件，声明 ASP.NET Core 框架引用、EF Core PostgreSQL 包并引用 BuildingBlocks 项目。 | simple |
| `src/Modules/Order/PhoneStore.Modules.Order/PhoneStore.Modules.Order.csproj` | MSBuild project file for the Order module declaring the ASP.NET Core framework reference, EF Core/Npgsql packages and the BuildingBlocks project dependency. | simple |

### Lớp Triển khai & CI/CD

Hạ tầng triển khai và tự động hóa: docker-compose cho PostgreSQL và MinIO, cùng pipeline GitHub Actions build và chạy các bộ test.

| File | Mô tả | Độ phức tạp |
|---|---|---|
| `.github/workflows/ci.yml` | GitHub Actions CI pipeline for the .NET 10 PhoneStore solution: restores and builds PhoneStore.slnx, then runs unit, architecture and PostgreSQL-backed integration test suites and uploads test result artifacts. | moderate |
| `docker/docker-compose.yml` | Compose file dựng hạ tầng phát triển: PostgreSQL 16 (port 5433) cho database chính và MinIO (port 9000/9001) cho lưu trữ object. | simple |
| `docker/docker-compose.yml` | Service PostgreSQL 16-alpine với volume `pgdata`, healthcheck `pg_isready` và cổng 5433:5432 cho database `phonestore_db`. | simple |
| `docker/docker-compose.yml` | Service MinIO lưu trữ object với console quản trị ở cổng 9001, dữ liệu nằm trong volume `miniodata`. | simple |

## Lộ trình học đề xuất

Thứ tự đọc code được đề xuất, bám theo thứ tự dependency:

### 1. Tổng quan dự án PhoneStore

Bắt đầu bằng README.md để nắm mục tiêu dự án: hệ thống backend thương mại điện tử bán lẻ điện thoại, kiến trúc Modular Monolith kết hợp Clean Architecture. File solution liệt kê toàn bộ các project gồm BuildingBlocks, Host API, 5 module nghiệp vụ và 3 project kiểm thử, cho bạn bản đồ thư mục trước khi đi sâu vào code. Tài liệu thiết kế hệ thống mô tả kiến trúc 6 tầng, ranh giới giữa 5 module và thiết kế CSDL gồm 13 bảng trên 5 schema.

**Xem:** `README.md`, `PhoneStore.slnx`, `docs/design/thiet-ke-he-thong.html`

### 2. Shared kernel: Result và Error

Trước khi đọc bất kỳ module nào, hãy hiểu ngôn ngữ lỗi chung của dự án: mẫu Result/Error trả về lỗi nghiệp vụ có mã và phân loại thay vì ném exception, và AppException mang theo mã lỗi cùng HTTP status. ApiResponse định nghĩa envelope phản hồi HTTP chuẩn (success, message, data, timestamp cho thành công; code, message, fieldErrors, requestId cho lỗi). Ba file này có fan-in cao nhất trong toàn bộ đồ thị, nghĩa là mọi module đều phụ thuộc vào chúng và bạn sẽ gặp lại chúng ở mọi bước sau.

**Xem:** `src/BuildingBlocks/PhoneStore.BuildingBlocks/Domain/Result.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Exceptions/AppException.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Common/ApiResponse.cs`

> 💡 Trong C#, mẫu Result<T> cùng với các static factory method (Ok/Fail) giúp lỗi nghiệp vụ trở thành dữ liệu bình thường trong luồng điều khiển, buộc caller phải xử lý lỗi tường minh thay vì bỏ sót exception.

### 3. Base class Domain và Domain Event

BuildingBlocks cung cấp nền tảng cho mọi Entity: BaseEntity với id generic và dấu thời gian tạo/cập nhật, AggregateRoot gom domain event phát sinh trong quá trình thay đổi trạng thái để dispatch sau khi commit. IDomainEventDispatcher mặc định phát tán event sau SaveChanges để bảo đảm dữ liệu đã được commit, còn InMemoryEventBus publish event nội bộ qua MediatR nên không cần broker ngoài. Đây là cơ chế nền cho toàn bộ giao tiếp bất đồng bộ giữa các module mà bạn sẽ thấy ở bước checkout.

**Xem:** `src/BuildingBlocks/PhoneStore.BuildingBlocks/Domain/BaseEntity.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Domain/AggregateRoot.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/IDomainEventDispatcher.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/EventBus/InMemoryEventBus.cs`

### 4. Module Order: Domain model

Bắt đầu với một module end-to-end và đi từ Domain trở lên. Order là aggregate root quản lý máy trạng thái đơn hàng, cách tính tiền, chi tiết đơn và lịch sử trạng thái, đồng thời phát OrderCancelled khi hủy hoặc giao thất bại. OrderItem snapshot tên sản phẩm, SKU, đơn giá và số lượng tại thời điểm checkout, còn OrderStatus liệt kê vòng đời PENDING → CONFIRMED → SHIPPING → DELIVERED cùng nhánh CANCELLED/DELIVERY_FAILED. Entity này sử dụng đúng các base class đã học ở Step 3, cho thấy cách áp dụng shared kernel trong thực tế.

**Xem:** `src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs`, `src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/OrderItem.cs`, `src/Modules/Order/PhoneStore.Modules.Order/Domain/Enums/OrderStatus.cs`, `src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/OrderItemDraft.cs`

### 5. Module Order: Application layer

OrderService là tầng Application hiện thực các use case COD checkout, tra cứu đơn, hủy, xác nhận, giao hàng, giao thành công và thống kê thu tiền COD, đồng thời điều phối với contract của Cart, Catalog và Inventory. Các abstraction CQRS dùng chung (ICommand/IQuery kế thừa MediatR IRequest, handler trả về Result) cùng PagedResult cho phân trang giúp các module chia sẻ cùng một quy ước xử lý request. OrderRepository là lớp hạ tầng EF Core cung cấp truy vấn eager-loading, lọc và phân trang theo unit-of-work. Đọc tầng này giúp bạn thấy ranh giới rõ ràng: service chỉ nói chuyện với repository và contract, không chạm DbContext của module khác.

**Xem:** `src/Modules/Order/PhoneStore.Modules.Order/Application/Services/OrderService.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/CQRS.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Application/PagedResult.cs`, `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Repositories/OrderRepository.cs`

### 6. Module Order: Persistence và Controller

OrderDbContext ánh xạ orders, order_items và order_status_histories vào schema PostgreSQL "ordering" với index và cascade delete, cho thấy mỗi module sở hữu schema riêng thay vì dùng chung bảng. Migration InitialOrder tạo schema cùng bộ index và foreign key, còn migration AddOrderFulfillmentFields bổ sung cột carrier, tracking number, số tiền đã thu và cart-id cho luồng giao hàng COD. OrderModuleRegistration là composition root của module: đăng ký DbContext, repository, service và cung cấp OrdersController với các endpoint checkout, cancel, confirm, ship, deliver. Đây là mẫu lặp lại ở cả 5 module, giúp bạn tự tin đọc module khác mà không cần tài liệu thêm.

**Xem:** `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/OrderDbContext.cs`, `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/Migrations/20261025173421_InitialOrder.cs`, `src/Modules/Order/PhoneStore.Modules.Order/OrderModuleRegistration.cs`

> 💡 EF Core migration ghi lại từng bước thay đổi schema theo thời gian, nên migration luôn idpotent và có thể rollback an toàn; luôn sinh migration cùng lúc với thay đổi model để tránh lệch schema production.

### 7. Contract giữa các module

Nguyên tắc quan trọng nhất của Modular Monolith nằm ở các contract trong BuildingBlocks: ICartContract cho phép Order đọc và chuyển trạng thái giỏ hàng, IProductPricingContract cho Cart/Order lấy giá chuẩn từ Catalog, IInventoryContract cho Order đặt giữ và hoàn trả tồn kho — mà không module nào tham chiếu trực tiếp DbContext của module khác. Song song đó, các domain event như OrderCancelled và ProductVariantCreated cho phép Inventory phản ứng với thay đổi ở module khác. Đây chính là ranh giới phụ thuộc được kiểm soát bằng Architecture Tests mà bạn sẽ xem ở bước testing.

**Xem:** `src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Cart/ICartContract.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Catalog/IProductPricingContract.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Inventory/IInventoryContract.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Order/OrderCancelled.cs`

### 8. Luồng checkout xuyên module

Bước này ghép mọi thứ lại: khách thêm sản phẩm vào giỏ qua CartService (luôn lấy giá server-side từ Catalog qua IProductPricingContract), rồi checkout sẽ đọc snapshot giỏ, chuyển giỏ sang trạng thái CONVERTED qua CartContract và tạo đơn COD trong OrderService. Song song, Order đặt giữ tồn kho qua InventoryContract với cơ chế row lock FOR UPDATE; nếu đơn bị hủy, OrderCancelledHandler trong Inventory hoàn trả lượng tồn đã đặt giữ và ghi vào StockMovement. Đây là luồng xuyên ba module đầy rủi ro transaction nhất, nên nó được bao phủ bởi nhiều integration test như CheckoutFlowTests và StockRestoreOnCancelTests.

**Xem:** `src/Modules/Cart/PhoneStore.Modules.Cart/Application/Services/CartService.cs`, `src/Modules/Cart/PhoneStore.Modules.Cart/Application/Services/CartContract.cs`, `src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryContract.cs`, `src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/EventHandlers/OrderCancelledHandler.cs`, `src/Modules/Inventory/PhoneStore.Modules.Inventory/Domain/Entities/StockMovement.cs`

### 9. Xác thực JWT và phân quyền

Module Identity xử lý đăng ký, đăng nhập, xoay vòng refresh token và quản trị tài khoản qua IdentityService, với User và RefreshToken làm entity Domain. JwtTokenService phát cặp access token JWT kèm refresh token opaque và chỉ lưu bản băm SHA-256 của refresh token trong kho, còn AuthController đặt refresh token vào cookie HttpOnly và phát CSRF token. AuthorizationPolicies tập trung các hằng số vai trò CUSTOMER/STAFF/ADMIN và tên policy, HttpContextCurrentUser đọc danh tính từ claim qua interface ICurrentUser, còn CsrfProtectionMiddleware đối chiếu token cookie với header cho các endpoint dùng cookie. Đặc tả nghiệp vụ chi tiết của FR-01/FR-02 nằm trong tài liệu auth.

**Xem:** `src/Modules/Identity/PhoneStore.Modules.Identity/Endpoints/AuthController.cs`, `src/Modules/Identity/PhoneStore.Modules.Identity/Domain/Entities/User.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Identity/JwtTokenService.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Common/AuthorizationPolicies.cs`, `docs/auth/FR-01-FR-02.md`

### 10. Host API và wiring middleware

Program.cs là entry point của API: khởi động WebApplication, đăng ký Swagger, validation, exception handling, CORS, CSRF và toàn bộ 5 module. BuildingBlocksRegistration là điểm đăng ký DI dùng chung cho MediatR, event bus, dispatcher, JWT bearer authentication, policy theo vai trò và antiforgery; ValidationRegistration cấu hình FluentValidation, filter validate request và JSON camelCase xuyên hệ thống. AppExceptionMiddleware biến AppException cùng lỗi không lường trước thành ErrorResponse chuẩn, DatabaseSeeder tạo dữ liệu tối thiểu idempotent cho môi trường local, còn docker-compose dựng PostgreSQL 16 và MinIO cho việc phát triển.

**Xem:** `src/Host/PhoneStore.Api/Program.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/BuildingBlocksRegistration.cs`, `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Web/AppExceptionMiddleware.cs`, `src/Host/PhoneStore.Api/Seeding/DatabaseSeeder.cs`, `docker/docker-compose.yml`

### 11. Chiến lược kiểm thử

Dự án có ba tầng kiểm thử. Unit test cô lập domain và service với fake repository, ví dụ OrderTests kiểm tra tính tổng tiền, ngưỡng miễn phí vận chuyển và ghi lịch sử trạng thái. Architecture test dùng NetArchTest để khẳng định mỗi module chỉ reference BuildingBlocks và không bao giờ tham chiếu module nghiệp vụ khác — đây chính là cơ chế bảo vệ ranh giới đã học ở Step 7. Integration test dựng host thật qua PhoneStoreFactory với PostgreSQL cô lập, chạy migration trước khi start và drop database sau khi kết thúc, phủ các luồng checkout, vòng đời đơn và hoàn trả tồn kho; CI pipeline build solution rồi chạy cả ba bộ test này.

**Xem:** `tests/PhoneStore.ArchitectureTests/ModuleArchitectureTests.cs`, `tests/PhoneStore.UnitTests/OrderTests.cs`, `tests/PhoneStore.IntegrationTests/PhoneStoreApiFactory.cs`, `tests/PhoneStore.IntegrationTests/CheckoutFlowTests.cs`, `.github/workflows/ci.yml`

> 💡 NetArchTest kiểm tra quy tắc phụ thuộc ở mức assembly bằng cách phân tích IL/metadata, cho phép viết ràng buộc kiến trúc như "Domain không được reference Infrastructure" dưới dạng assertion chạy trong CI.

### 12. Tài liệu và quy trình nhóm

Cuối cùng, hãy nắm các tài liệu đi kèm để làm việc nhất quán với cả team. Tài liệu hỏi đáp ASK-MANH-N2LHTBLTT-12 ghi lại ba điểm kiến trúc then chốt của module Cart/Order: nguồn giá qua contract, transaction xuyên nhiều DbContext và event hoàn tồn kho khi hủy đơn. GIT_WORKFLOW.md quy chuẩn nhánh main/develop/feature/bugfix, commit theo Conventional Commits kèm mã Jira, còn JIRA_TASKS.md tra cứu ánh xạ FR → Issue → Board. Sau khi đọc xong các bước trên, bạn đã có đủ bối cảnh để nhận việc đầu tiên trong hệ thống.

**Xem:** `docs/reviews/ASK-MANH-N2LHTBLTT-12.md`, `GIT_WORKFLOW.md`, `docs/JIRA_TASKS.md`

## Điểm cần thận trọng khi đọc code

40 file được đánh dấu là **complex** — nên đọc chậm và có test bao phủ:

- `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Persistence/Migrations/20261026154105_AddRefreshTokens_OrderFulfillment_StockMovements.Designer.cs` — File Designer do EF Core sinh cho migration bổ sung bảng refresh_tokens, mô tả mô hình đích của schema identity sau thay đổi.
- `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/Migrations/20261026154131_AddOrderFulfillmentFields.Designer.cs` — Auto-generated EF Core designer file capturing the Order model after fulfillment fields (carrier, tracking number, COD amount, cart reference) were added.
- `src/Modules/Identity/PhoneStore.Modules.Identity/Infrastructure/Persistence/Migrations/20261025174244_InitialIdentity.Designer.cs` — File Designer do EF Core sinh cho migration InitialIdentity, mô tả mô hình đích gồm bảng users và addresses trong schema identity.
- `src/Modules/Catalog/PhoneStore.Modules.Catalog/Infrastructure/Persistence/Migrations/20261025173356_InitialCatalog.Designer.cs` — File Designer do EF Core sinh cho migration InitialCatalog, mô tả mô hình đích của schema catalog tại thời điểm sinh.
- `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/Migrations/20261025173421_InitialOrder.Designer.cs` — Auto-generated EF Core designer snapshot for the initial Order migration, describing the full target model (orders, order_items, order_status_histories) of the ordering schema.
- `src/Modules/Catalog/PhoneStore.Modules.Catalog/Infrastructure/Persistence/Migrations/20261025173356_InitialCatalog.cs` — Migration khởi tạo schema catalog: tạo các bảng brands, categories, products, product_variants và product_images cùng index.
- `src/Modules/Catalog/PhoneStore.Modules.Catalog/Infrastructure/Persistence/Migrations/CatalogDbContextModelSnapshot.cs` — Snapshot mô hình hiện tại của CatalogDbContext, dùng để EF Core sinh migration tiếp theo.
- `src/Modules/Order/PhoneStore.Modules.Order/Infrastructure/Persistence/Migrations/OrderDbContextModelSnapshot.cs` — Current EF Core model snapshot of the Order module, used as the baseline for generating the next migration.
- `src/Modules/Identity/PhoneStore.Modules.Identity/Application/Validators/IdentityValidators.cs` — Bộ validator FluentValidation cho toàn bộ DTO của module Identity: đăng ký, đăng nhập, hồ sơ, mật khẩu, địa chỉ và quản trị nhân viên.
- `src/BuildingBlocks/PhoneStore.BuildingBlocks/Infrastructure/Web/CsrfProtectionMiddleware.cs` — Middleware bảo vệ CSRF cho endpoint dùng cookie xác thực, đối chiếu token trong cookie và header.
- `src/Modules/Inventory/PhoneStore.Modules.Inventory/Application/Services/InventoryService.cs` — 库存应用服务，实现库存查询、入库、批量入库、手工调整与变动日志分页查询等管理端用例。
- `src/Modules/Identity/PhoneStore.Modules.Identity/Application/Services/IdentityService.cs` — Dịch vụ ứng dụng của module Identity xử lý đăng ký, đăng nhập, xoay vòng refresh token, hồ sơ, địa chỉ và quản trị tài khoản nhân viên.
- `src/Modules/Catalog/PhoneStore.Modules.Catalog/Application/Services/CatalogService.cs` — Dich vu nghiep vu lon nhat cua module Catalog: quan ly danh muc, thuong hieu, san pham, phien ban SKU, anh san pham, loc phan trang va publish su kien tao phien ban.
- `src/Modules/Order/PhoneStore.Modules.Order/Application/Services/OrderService.cs` — 订单应用服务，实现 COD 结账、订单查询、取消、确认、发货、交付与 COD 收款统计等用例，并协调购物车、定价与库存契约。
- `src/Modules/Catalog/PhoneStore.Modules.Catalog/Endpoints/ProductsController.cs` — REST controller cho sản phẩm, SKU và ảnh sản phẩm: tra cứu mở cho khách và CRUD quản trị yêu cầu vai trò STAFF.
- `src/BuildingBlocks/PhoneStore.BuildingBlocks/BuildingBlocksRegistration.cs` — Điểm đăng ký DI của BuildingBlocks: MediatR, event bus, dispatcher, ICurrentUser, JWT bearer authentication, policy theo vai trò và antiforgery CSRF.
- `src/Modules/Identity/PhoneStore.Modules.Identity/Domain/Entities/User.cs` — Thực thể người dùng (khách hàng và nhân viên) kế thừa AggregateRoot, đóng gói quy tắc vai trò, khoá tài khoản và cập nhật hồ sơ.
- `tests/PhoneStore.IntegrationTests/CatalogAndIdentityRegressionTests.cs` — Regression suite for catalog filtering, sorting, paging and validation errors plus identity registration and login rules such as email normalization and password policies.
- `src/Modules/Order/PhoneStore.Modules.Order/OrderModuleRegistration.cs` — Composition root of the Order module: registers the DbContext, repository and application service, and hosts the OrdersController exposing checkout, cancel, confirm, ship, deliver and COD collection endpoints.
- `src/Modules/Order/PhoneStore.Modules.Order/Domain/Entities/Order.cs` — 订单聚合根，管理订单状态机、金额计算、明细与状态历史，并在取消/交付失败时发布 OrderCancelled 领域事件。
- `tests/PhoneStore.IntegrationTests/IdentitySessionRegressionTests.cs` — Regression tests for session and token behaviour: logout invalidation, refresh-token rotation, concurrency, CSRF enforcement and account-security invalidation.
- `tests/PhoneStore.IntegrationTests/BusinessFlowRegressionTests.cs` — Broad end-to-end regression suite covering cart isolation and limits, checkout to order creation, order numbering, shipping fee rules, cancellation history, catalog paging and customer isolation.
- `tests/PhoneStore.IntegrationTests/EndToEndOrderJourneyTests.cs` — End-to-end journey tests walking a customer from browsing products through checkout, order lifecycle transitions, cancellation and stock restoration.
- `tests/PhoneStore.UnitTests/ProductFilterSortPaginationTests.cs` — Largest unit test suite for the catalog product query, validating filter parameters, sorting options, pagination metadata and DTO mapping.
- `tests/PhoneStore.UnitTests/CatalogCategoryAndBrandTests.cs` — Extensive unit tests for catalog category and brand management: listing, lookup, create, update, validation, name-conflict and soft-versus-hard delete rules.
- `tests/PhoneStore.IntegrationTests/AuthAndStaffApiTests.cs` — Integration tests covering registration, JWT access and refresh token rotation, logout revocation, locked accounts and the staff administration endpoints.
- `tests/PhoneStore.IntegrationTests/CatalogAdminApiTests.cs` — Integration tests for catalog administration APIs: staff CRUD on categories, brands, products and variants, duplicate-SKU and unknown-reference conflicts, and role restrictions.
- `docs/BaoCao_PhanTichThietKe_PhoneStore_CSharp_NET.docx` — Báo cáo phân tích thiết kế hệ thống PhoneStore (Word, ~4400 dòng nội dung) mô tả kiến trúc modular monolith, nghiệp vụ và cơ sở dữ liệu.
- `tests/PhoneStore.IntegrationTests/InventoryApiTests.cs` — Integration tests for the inventory API covering stock import, adjustment, movement logging, low-stock reporting and staff-only authorization.
- `tests/PhoneStore.IntegrationTests/IdentityApiTests.cs` — Integration tests for the identity API covering registration, login, profile updates, password changes and per-user address management.
- `postman/PhoneStore-FR01-FR02.postman_collection.json` — Bộ Postman collection kiểm thử luồng đăng nhập FR-01/FR-02 với 18 request tuần tự (00–17), script lưu JWT/CSRF vào collection variable và cookie jar.
- `docs/reviews/ASK-MANH-N2LHTBLTT-12.md` — Tài liệu hỏi đáp giữa các thành viên về task N2LHTBLTT-12 (Cart/Order domain), nêu 3 điểm cần chốt: nguồn giá sản phẩm qua contract, transaction xuyên nhiều DbContext và event hoàn tồn kho khi hủy đơn; kèm phần ghi nhận giả định đã implement và báo cáo bug tìm được qua integration test.
- `docs/reviews/N2LHTBLTT-12-review.html` — Trang HTML tĩnh trình bày review chi tiết task N2LHTBLTT-12 với 12 mục: danh sách file thay đổi, kiến trúc dữ liệu Cart/Order, review entity, repository, EF Core mapping, API contract DTO, sơ đồ quan hệ schema và checklist review.
- `docs/design/phan-ra-chuc-nang.html` — Tài liệu phân tích yêu cầu và phân rã chức năng: 16 yêu cầu chức năng, 9 yêu cầu phi chức năng, cây FBD 5 cấp, đặc tả use case, vòng đời trạng thái đơn hàng, 25 quy tắc nghiệp vụ và ma trận truy vết FR ↔ Task ↔ Endpoint.
- `docs/design/thiet-ke-he-thong.html` — Tài liệu thiết kế hệ thống: kiến trúc Modular Monolith + Clean Architecture 6 tầng, ranh giới và ma trận phụ thuộc giữa 5 module, thiết kế CSDL 13 bảng trên 5 schema, ERD, các luồng kỹ thuật, cơ chế Contract/Domain Event, xử lý lỗi và hạ tầng/CI.
- `docs/learning/data-quiz.js` — Bộ dữ liệu câu hỏi trắc nghiệm (mảng QUIZ) dạng 4 lựa chọn kèm đáp án đúng và giải thích cho 7 task Jira.
- `docs/learning/index.html` — Trang HTML tĩnh làm giao diện ôn tập toàn: header thống kê tiến độ, thanh điều hướng 9 mục và các section nội dung cho Cart, Order và Testing.
- `docs/learning/data.js` — Dữ liệu nội dung ôn tập: danh sách task, dòng thời gian triển khai, sơ đồ kiến trúc và các luồng nghiệp vụ giỏ hàng / checkout / đơn hàng.
- `docs/learning/app.js` — Logic của trang ôn tập: quản lý tiến độ trong localStorage, tính điểm tổng thể và render các mục dashboard, flashcard, quiz, code lab, cheat sheet, checklist.
- `README.md` — Tài liệu tổng quan dự án PhoneStore Backend kiến trúc Modular Monolith + Clean Architecture, gồm phân công nhóm, cấu trúc thư mục, hướng dẫn cài đặt/chạy Docker, cơ chế JWT, tình trạng và cách chạy test, ví dụ gọi API bằng curl.

## Cách khám phá trong Cursor

```
Skill đã cài: /understand, /understand-dashboard, /understand-chat, /understand-explain,
           /understand-diff, /understand-onboard, /understand-domain, /understand-knowledge
```

- `/understand-dashboard` — mở lại dashboard tương tác (đồ thị, tìm kiếm, phân tích diff)
- `/understand-chat "Hỏi câu hỏi về codebase"` — hỏi đáp bằng ngôn ngữ tự nhiên
- `/understand-explain src/path/File.cs` — giải thích sâu một file cụ thể
- `/understand-diff` — xem thay đổi hiện tại ảnh hưởng tới đâu trong hệ thống
- `/understand` — chạy lại pipeline (mặc định incremental, chỉ phân tích file thay đổi)
