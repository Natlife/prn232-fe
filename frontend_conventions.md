# QUY ƯỚC LẬP TRÌNH FRONTEND 🎨 (FRONTEND CODING CONVENTIONS)

Tài liệu này quy định các quy chuẩn xây dựng giao diện, viết mã Javascript, CSS, gọi AJAX và quản lý luồng tương tác phía **Frontend (ASP.NET Core MVC Client)**.

---

## I. Xác Thực & Phụ Thuộc Tương Tác API

### 1. Đính kèm Token JWT từ Cookie
Hệ thống sử dụng đồng thời Cookie Authentication ở Client và JWT Bearer ở API. Do đó, khi Client gọi bất kỳ API bảo mật nào (như CRUD Xe, Quản lý yêu cầu mua xe), bắt buộc phải trích xuất Token JWT từ Claims và đính kèm vào Header của `HttpClient`:
```csharp
protected void AppendAuthorizationHeader()
{
    var token = User.FindFirst("jwt_token")?.Value;
    if (!string.IsNullOrEmpty(token))
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}
```
*Các Controller của Client cần gọi hàm này trước khi thực thi gửi yêu cầu lên Web API.*

### 2. Phân Quyền Phía Client (Role-based Views)
*   **Ở tầng Controller**: Sử dụng `[Authorize(Roles = "Admin")]` để chặn truy cập trái phép.
*   **Ở tầng View (.cshtml)**: Kiểm tra vai trò của User để ẩn/hiển thị các nút chức năng nhạy cảm (như nút Xóa hoặc nút vào khu vực quản trị):
    ```html
    @if (User.IsInRole("Admin"))
    {
        <button class="btn btn-danger" onclick="confirmDelete(@item.CarId)">Xóa xe</button>
    }
    ```

---

## II. Quy Chuẩn Tương Tác Giao Diện (UI/UX Guidelines)

Để đảm bảo điểm số giao diện tối đa và mang lại trải nghiệm mượt mà, dự án thống nhất các quy tắc sau:

### 1. Tạo và Cập Nhật bằng Popup Modal (Bootstrap)
*   **Không chuyển hướng trang** khi Thêm mới hoặc Cập nhật thông tin xe. Tất cả các hành động này phải được thực hiện trên cùng một trang danh sách thông qua **Bootstrap Modal**.
*   **Thêm mới**: Click nút $\rightarrow$ Mở Modal trống $\rightarrow$ Nhập dữ liệu $\rightarrow$ Gửi AJAX.
*   **Cập nhật**: Click nút Sửa $\rightarrow$ Gửi AJAX lấy thông tin chi tiết xe từ API $\rightarrow$ Đổ dữ liệu vào form trong Modal $\rightarrow$ Hiển thị Modal $\rightarrow$ Gửi AJAX lưu thay đổi.

### 2. Xóa Kết Hợp Xác Nhận Trực Quan (SweetAlert2)
*   Không sử dụng hộp thoại `confirm()` mặc định của trình duyệt. 
*   Bắt buộc tích hợp thư viện **SweetAlert2** để hiển thị hộp thoại xác nhận xóa dạng popup đẹp mắt trước khi gửi yêu cầu `DELETE` qua AJAX lên Server.

---

## III. Quy Ước Viết Code Frontend

### 1. Quy tắc Đặt tên (Naming Conventions)
*   **Biến và Hàm Javascript**: camelCase (ví dụ: `carId`, `loadCarData()`, `submitRequestForm()`).
*   **CSS Class**: kebab-case (ví dụ: `car-detail-card`, `btn-deposit-action`).
*   **ID HTML**: camelCase hoặc kebab-case, nhưng phải mang tính mô tả và độc nhất (ví dụ: `carModal`, `input-car-name`).

### 2. Viết AJAX Chuẩn Hóa
Sử dụng jQuery AJAX để gửi và nhận dữ liệu nhằm tránh tải lại trang:
```javascript
function saveCar() {
    var form = $('#carForm');
    if (!form.valid()) return; // Validation Client-side

    var formData = new FormData(form[0]);

    $.ajax({
        url: '/Admin/Cars/Save',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function(res) {
            if (res.success) {
                $('#carModal').modal('hide');
                Swal.fire('Thành công!', res.message, 'success').then(() => location.reload());
            } else {
                Swal.fire('Thất bại!', res.message, 'error');
            }
        },
        error: function() {
            Swal.fire('Lỗi!', 'Đã xảy ra lỗi kết nối mạng.', 'error');
        }
    });
}
```

### 3. Đồng Bộ Hóa Styling (CSS)
*   Sử dụng CSS variables trong `/css/site.css` để đồng bộ các màu sắc chính của dự án, đảm bảo thiết kế premium và nhất quán:
    ```css
    :root {
        --primary-color: #ffc107;   /* Màu vàng chủ đạo */
        --secondary-color: #262626; /* Màu đen nền */
        --light-bg: #f8f9fa;
        --card-shadow: 0 4px 6px rgba(0,0,0,0.1);
    }
    ```
*   Tất cả các thẻ Card xe, bảng, nút bấm cần sử dụng các class định nghĩa sẵn hoặc tận dụng CSS variables để đổi màu nhanh chóng khi cần.

---

## IV. Quy Trình Phối Hợp Nhóm (Git Workflow)

1.  **Tách nhánh tính năng**: Đặt tên dạng `feature/fe-tên-chức-năng` (ví dụ: `feature/fe-car-modal`).
2.  **Commit Message**: Viết ngắn gọn kèm tiền tố như `feat:`, `fix:`, `refactor:`.
3.  **Tải lại code trước khi Push**: Luôn chạy `git pull origin main` để resolve xung đột ở máy cục bộ và kiểm tra build thành công trước khi gửi Pull Request.
4.  **Bảo toàn comment**: Không được xóa các comment/hướng dẫn code của các thành viên khác đang làm việc trên cùng một view hay file script.
