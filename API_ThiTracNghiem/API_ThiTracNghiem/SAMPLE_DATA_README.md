# Dữ liệu mẫu cho API Thi Trắc Nghiệm

## Tổng quan

Dữ liệu mẫu được tạo để test endpoint `GET /api/materials/{id}` và các endpoint khác. Dữ liệu được thiết kế để có mối liên hệ chặt chẽ giữa các bảng.

## Cấu trúc dữ liệu

### 0. Users (Giáo viên) - 3 records

- **ID tự động**: Nguyễn Văn A (teacher1@example.com)
- **ID tự động**: Trần Thị B (teacher2@example.com)
- **ID tự động**: Lê Văn C (teacher3@example.com)

### 1. Subjects (Môn học) - 5 records

- **ID 1**: Lập trình C#
- **ID 2**: Cơ sở dữ liệu SQL Server
- **ID 3**: Thiết kế Web với ASP.NET Core
- **ID 4**: Kiến trúc phần mềm
- **ID 5**: DevOps và CI/CD

### 2. ClassCohorts (Lớp học) - 5 records

- **ID 1**: C# Cơ bản - K2024A (SubjectId: 1)
- **ID 2**: SQL Server - K2024B (SubjectId: 2)
- **ID 3**: ASP.NET Core - K2024C (SubjectId: 3)
- **ID 4**: Kiến trúc phần mềm - K2024D (SubjectId: 4)
- **ID 5**: DevOps - K2024E (SubjectId: 5)

### 3. Courses (Khóa học) - 5 records

- **ID 1**: Lập trình C# từ cơ bản đến nâng cao (SubjectId: 1, Price: 299,000 VND)
- **ID 2**: SQL Server Database Design & Optimization (SubjectId: 2, Price: 399,000 VND)
- **ID 3**: Xây dựng Web API với ASP.NET Core (SubjectId: 3, Price: 499,000 VND)
- **ID 4**: Clean Architecture & Design Patterns (SubjectId: 4, Price: 599,000 VND)
- **ID 5**: DevOps với Docker & Azure DevOps (SubjectId: 5, Price: 699,000 VND)

### 4. Materials (Tài liệu) - 25 records

#### Course 1 (C#): Materials ID 1-5

- **ID 1**: Giới thiệu về C# và .NET Framework (FREE)
- **ID 2**: Cú pháp cơ bản và biến trong C# (50,000 VND)
- **ID 3**: Lập trình hướng đối tượng với C# (75,000 VND)
- **ID 4**: LINQ và Collection trong C# (100,000 VND)
- **ID 5**: Async/Await và Multithreading (125,000 VND)

#### Course 2 (SQL Server): Materials ID 6-10

- **ID 6**: Giới thiệu SQL Server và Management Studio (FREE)
- **ID 7**: Thiết kế cơ sở dữ liệu và Normalization (80,000 VND)
- **ID 8**: T-SQL Advanced Queries (120,000 VND)
- **ID 9**: Performance Tuning và Indexing (150,000 VND)
- **ID 10**: Backup và Recovery Strategies (100,000 VND)

#### Course 3 (ASP.NET Core): Materials ID 11-15

- **ID 11**: Giới thiệu ASP.NET Core và Web API (FREE)
- **ID 12**: Dependency Injection và Configuration (90,000 VND)
- **ID 13**: Entity Framework Core và Database First (130,000 VND)
- **ID 14**: Authentication và Authorization (160,000 VND)
- **ID 15**: API Documentation với Swagger (70,000 VND)

#### Course 4 (Clean Architecture): Materials ID 16-20

- **ID 16**: Giới thiệu Clean Architecture (FREE)
- **ID 17**: SOLID Principles và Design Patterns (180,000 VND)
- **ID 18**: CQRS và Event Sourcing (200,000 VND)
- **ID 19**: Microservices Architecture (250,000 VND)
- **ID 20**: Testing Strategies và TDD (150,000 VND)

#### Course 5 (DevOps): Materials ID 21-25

- **ID 21**: Giới thiệu DevOps và CI/CD (FREE)
- **ID 22**: Docker Containerization (140,000 VND)
- **ID 23**: Azure DevOps và Git Workflow (120,000 VND)
- **ID 24**: Infrastructure as Code với Terraform (220,000 VND)
- **ID 25**: Monitoring và Logging (160,000 VND)

## Cách sử dụng

### Tự động seed khi khởi động ứng dụng

Dữ liệu sẽ được tự động seed khi chạy ứng dụng lần đầu tiên.

### Seed thủ công

```powershell
# Chạy script PowerShell để seed dữ liệu
.\Scripts\SeedData.ps1

# Hoặc test seed data từ đầu (xóa DB cũ và tạo mới)
.\Scripts\TestSeedData.ps1
```

### Test endpoint

```http
### Test với Material ID hợp lệ (ID sẽ được tự động tạo)
GET https://localhost:7000/api/materials/1
GET https://localhost:7000/api/materials/5
GET https://localhost:7000/api/materials/10
GET https://localhost:7000/api/materials/15
GET https://localhost:7000/api/materials/20
GET https://localhost:7000/api/materials/25

### Test với Material ID không tồn tại
GET https://localhost:7000/api/materials/999

### Test với Material ID không hợp lệ
GET https://localhost:7000/api/materials/0
GET https://localhost:7000/api/materials/-1
```

**Lưu ý**: Các ID sẽ được tự động tạo bởi database, không cố định như trước.

## Đặc điểm dữ liệu

### Liên kết chặt chẽ

- Mỗi Course thuộc về một Subject
- Mỗi Material thuộc về một Course
- Dữ liệu được sắp xếp theo thứ tự logic học tập

### Đa dạng về giá cả

- Có tài liệu miễn phí (FREE) và trả phí
- Giá từ 50,000 VND đến 250,000 VND
- Phản ánh độ khó và giá trị của tài liệu

### Đa dạng về loại media

- PDF documents
- Video tutorials
- External links

### Thời gian thực tế

- CreatedAt được thiết lập trong 30 ngày qua
- Phản ánh timeline học tập hợp lý

## Lưu ý

- Dữ liệu được thiết kế để test đầy đủ các trường hợp của endpoint
- Có thể mở rộng thêm dữ liệu bằng cách chỉnh sửa `SeedData.cs`
- Dữ liệu sẽ không bị duplicate khi chạy lại seed script
