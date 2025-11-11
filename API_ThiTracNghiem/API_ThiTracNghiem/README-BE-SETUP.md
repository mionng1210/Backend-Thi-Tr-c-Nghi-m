# Hướng dẫn cài đặt Backend (BE) cho người mới

Tài liệu này giúp bạn chuẩn bị môi trường, cấu hình và chạy các dịch vụ backend trong repo. Các dịch vụ chính và cổng mặc định:

- AuthService: `http://localhost:5001`
- ExamsService: `http://localhost:5002`
- MaterialsService: `http://localhost:5003`
- ChatService: `http://localhost:5004`

## 1) Cần cài những gì (Prerequisites)

- .NET SDK `8.0`
  - Tải tại: https://dotnet.microsoft.com/download/dotnet/8.0
  - Kiểm tra cài đặt: `dotnet --version` (kỳ vọng 8.x)

- SQL Server (Express/Developer) + SSMS (khuyến nghị)
  - Tải SQL Server: https://www.microsoft.com/sql-server/sql-server-downloads
  - Tải SSMS: https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms
  - Có thể dùng `LocalDB` hoặc `SQLEXPRESS`. Kết nối mẫu bên dưới.

- Redis (dùng để lưu tiến trình làm bài tạm thời)
  - Khuyến nghị dùng Docker: `docker run -d --name redis -p 6379:6379 redis:7`
  - Hoặc dùng WSL2 (cài redis trên Ubuntu), hoặc Memurai trên Windows.

- IDE/Editor (tùy chọn)
  - Visual Studio 2022 (khuyến nghị) hoặc VS Code + C# extension.

## 2) Cấu hình cần chỉnh (appsettings.json)

Mỗi service có `appsettings.json`. Vui lòng cập nhật các mục dưới đây cho phù hợp môi trường local.

### 2.1) JWT (dùng chung cho tất cả dịch vụ)

- `Jwt.Key`: chuỗi bí mật NGẪU NHIÊN, đủ dài (>= 32 ký tự). PHẢI giống nhau ở tất cả services để xác thực token.
- `Jwt.Issuer`: mặc định `AuthService`
- `Jwt.Audience`: mặc định `AuthService_Client`
- `Jwt.ExpireMinutes`: thời gian sống của token (vd `120`)

Ví dụ:
```json
"Jwt": {
  "Key": "CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32+_CHARS",
  "Issuer": "AuthService",
  "Audience": "AuthService_Client",
  "ExpireMinutes": 120
}
```

### 2.2) ConnectionStrings (SQL Server)

Thay `Server=...` theo máy bạn. Ví dụ dùng `localhost` hoặc `.\SQLEXPRESS`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=ThiTrucTuyenDb-AuthServiceDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```
Mỗi service có DB riêng (tên mẫu):
- AuthService: `ThiTrucTuyenDb-AuthServiceDb`
- ExamsService: `ThiTrucTuyenDb-ExamsServiceDb`
- MaterialsService: `ThiTrucTuyenDb-MaterialsServiceDb`
- ChatService: `ThiTrucTuyenDb-ChatServiceDb`

### 2.3) Redis (chỉ cần cho ExamsService)
```json
"Redis": {
  "ConnectionString": "localhost:6379",
  "AttemptTtlMinutes": 180
}
```
- `ConnectionString`: địa chỉ Redis (mặc định docker map `6379` về `localhost`).
- `AttemptTtlMinutes`: TTL tiến trình làm bài lưu trong Redis.

### 2.4) AutoSubmit (ExamsService)
```json
"AutoSubmit": {
  "PollSeconds": 30
}
```
- Khoảng thời gian background service kiểm tra và tự động nộp bài khi hết giờ.

### 2.5) Cloudinary & Supabase (MaterialsService)

Dùng để lưu trữ tài liệu/ảnh. Bạn có thể dùng một hoặc cả hai.
```json
"Cloudinary": {
  "CloudName": "<cloud_name>",
  "ApiKey": "<api_key>",
  "ApiSecret": "<api_secret>"
},
"Supabase": {
  "ProjectUrl": "https://<your-project>.supabase.co",
  "AnonKey": "<anon_key>",
  "ServiceKey": "<service_key>",
  "Bucket": "Materials"
}
```
- Cloudinary: tạo tài khoản, lấy `CloudName`, `ApiKey`, `ApiSecret` (Dashboard).
- Supabase: tạo project, bật Storage, tạo bucket `Materials`, lấy `AnonKey` và `ServiceKey` (chỉ dùng `ServiceKey` ở backend).

### 2.6) SMTP (AuthService – gửi email)
```json
"Smtp": {
  "SenderEmail": "<email@domain>",
  "SenderPassword": "<app_password>",
  "Host": "smtp.gmail.com",
  "Port": 587,
  "EnableSsl": true
}
```
- Nếu dùng Gmail: bật 2FA và tạo App Password, dùng thay cho mật khẩu thường.
- Có thể dùng SMTP khác (SendGrid, Mailgun…), cập nhật `Host/Port/SSL` tương ứng.

### 2.7) BaseUrl giữa các dịch vụ (Services)

Đảm bảo các `BaseUrl` trỏ đúng cổng local.
Ví dụ trong `AuthService/appsettings.json`:
```json
"Services": {
  "ChatService": { "BaseUrl": "http://localhost:5004" },
  "ExamsService": { "BaseUrl": "http://localhost:5002" }
}
```
Các dịch vụ khác cũng có phần `Services` tương tự, hãy kiểm tra và cập nhật nếu bạn đổi cổng.

## 3) Chạy dự án (Development)

- Khôi phục package:
  ```bash
  dotnet restore
  ```

- Chạy từng service (mở 4 terminal hoặc dùng VS set multiple startup):
  ```bash
  cd Services/AuthService && dotnet run
  cd Services/ExamsService && dotnet run
  cd Services/MaterialsService && dotnet run
  cd Services/ChatService && dotnet run
  ```

- Swagger:
  - AuthService: `http://localhost:5001/swagger`
  - ExamsService: `http://localhost:5002/swagger`
  - MaterialsService: `http://localhost:5003/swagger`
  - ChatService: `http://localhost:5004/swagger`

### 3.1) Database

- AuthService gọi `db.Database.Migrate()` khi khởi động để áp dụng migration.
- ExamsService và MaterialsService gọi `Database.EnsureCreated()` và seed dữ liệu mẫu lần đầu.
- Bạn có thể quản lý DB bằng SSMS.

### 3.2) Redis

- Đảm bảo container Redis đã chạy: `docker ps` thấy `redis` đang `Up`.
- Nếu không dùng Docker: cài Redis qua WSL2 hoặc Memurai và cập nhật `ConnectionString` cho phù hợp.

## 4) Kiểm tra nhanh (Smoke test)

1) Vào `http://localhost:5001/swagger` (AuthService), thử đăng nhập/đăng ký để lấy JWT.
2) Dùng JWT đó gọi API ở `http://localhost:5002/swagger` (ExamsService), ví dụ bắt đầu bài thi và lưu tiến trình.
3) Vào `http://localhost:5003/swagger` (MaterialsService), thử upload tài liệu/ảnh (nếu đã cấu hình Cloudinary/Supabase).

## 5) Lưu ý & Gợi ý bảo mật

- KHÔNG commit bí mật (`Jwt.Key`, `ApiSecret`, `ServiceKey`, mật khẩu email) lên repo công khai.
- Nên dùng `appsettings.Development.json` hoặc biến môi trường/Secret Manager cho giá trị nhạy cảm.
- `Jwt.Key` phải giống nhau ở tất cả dịch vụ.
- Nếu Redis không có dữ liệu tiến trình, `submit` có thể yêu cầu gửi kèm `answers` trong body.
- Nếu tự đổi cổng, hãy cập nhật lại `Services.BaseUrl` giữa các service.

## 6) Sự cố thường gặp

- 404 khi gọi `GET /attempts/{id}/progress`: phiên thi có thể đã `Completed` hoặc JWT không khớp `userId`.
- Không kết nối được SQL Server: kiểm tra `Server=...` và quyền truy cập.
- Không kết nối được Redis: kiểm tra Docker/WSL và `ConnectionString`.
- Upload thất bại: kiểm tra Cloudinary/Supabase keys và mạng.

---

Nếu bạn muốn mình tạo file cấu hình mẫu `appsettings.Development.json` riêng cho từng service, nói mình biết để thêm.