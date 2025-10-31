# Hướng Dẫn Sử Dụng User Sync System

## Tổng Quan

User Sync System cho phép đồng bộ thông tin người dùng (students, teachers, admins) giữa tất cả các microservices trong hệ thống. Điều này đảm bảo rằng mọi service đều có thể truy cập thông tin user một cách nhất quán và an toàn.

## Kiến Trúc Hệ Thống

### Services và Ports
- **AuthService** (Port 5001): Quản lý authentication và user data
- **ExamsService** (Port 5002): Quản lý bài thi và kết quả
- **MaterialsService** (Port 5003): Quản lý tài liệu học tập
- **UsersService** (Port 5004): Quản lý thông tin user chi tiết

### Luồng Đồng Bộ
1. User đăng nhập qua AuthService và nhận JWT token
2. Khi gọi API ở bất kỳ service nào, UserSyncMiddleware sẽ:
   - Trích xuất JWT token từ Authorization header
   - Gọi AuthService để lấy thông tin user
   - Lưu thông tin user vào HttpContext
   - Cho phép controller sử dụng thông tin này

## Cấu Hình

### 1. appsettings.json
Mỗi service cần cấu hình URL của các service khác:

```json
{
  "Services": {
    "AuthService": {
      "BaseUrl": "http://localhost:5001"
    },
    "ExamsService": {
      "BaseUrl": "http://localhost:5002"
    },
    "MaterialsService": {
      "BaseUrl": "http://localhost:5003"
    },
    "UsersService": {
      "BaseUrl": "http://localhost:5004"
    }
  }
}
```

### 2. Program.cs
Đăng ký services và middleware:

```csharp
// Đăng ký User Sync Service
builder.Services.AddHttpClient<IUserSyncService, UserSyncService>();
builder.Services.AddScoped<IUserSyncService, UserSyncService>();

var app = builder.Build();

// Thêm middleware (sau Authentication, trước Authorization)
app.UseAuthentication();
app.UseUserSync();
app.UseAuthorization();
```

## Sử Dụng Trong Controller

### Cách 1: Sử Dụng HttpContext Extensions (Khuyến nghị)

```csharp
[HttpGet]
[Authorize]
public IActionResult GetData()
{
    // Lấy thông tin user đã được sync
    var syncedUser = HttpContext.GetSyncedUser();
    var userId = HttpContext.GetSyncedUserId();
    var userRole = HttpContext.GetSyncedUserRole();

    if (syncedUser == null)
    {
        return Unauthorized("User not found");
    }

    // Kiểm tra quyền
    if (HttpContext.IsAdmin())
    {
        // Logic cho Admin
    }
    else if (HttpContext.IsTeacher())
    {
        // Logic cho Teacher
    }
    else if (HttpContext.IsStudent())
    {
        // Logic cho Student
    }

    return Ok(new { User = syncedUser });
}
```

### Cách 2: Sử Dụng UserSyncService Trực Tiếp

```csharp
[HttpPost]
[Authorize]
public async Task<IActionResult> CreateData([FromBody] CreateRequest request)
{
    var authHeader = Request.Headers["Authorization"].FirstOrDefault();
    var token = authHeader.Substring("Bearer ".Length).Trim();
    
    var user = await _userSyncService.GetUserFromTokenAsync(token);
    if (user == null)
    {
        return Unauthorized("Invalid token");
    }

    // Kiểm tra quyền cụ thể
    var hasPermission = await _userSyncService.ValidateUserPermissionAsync(user.UserId, "Teacher");
    if (!hasPermission)
    {
        return Forbid("Only Teachers can create this data");
    }

    // Logic tạo dữ liệu
    return Ok();
}
```

## API Endpoints Có Sẵn

### AuthService (Port 5001)
- `GET /api/UserSync/{id}` - Lấy user theo ID
- `GET /api/UserSync/by-email/{email}` - Lấy user theo email
- `POST /api/UserSync/from-token` - Lấy user từ JWT token
- `POST /api/UserSync/validate-permission` - Kiểm tra quyền user

### ExamsService (Port 5002)
- `GET /api/Exams/user-sync-demo` - Demo user sync
- `GET /api/Exams/role-check-demo` - Demo kiểm tra quyền

### MaterialsService (Port 5003)
- `GET /api/Materials/user-sync-demo` - Demo user sync
- `GET /api/Materials/access-check-demo/{materialId}` - Demo kiểm tra quyền truy cập tài liệu

## Các Extension Methods Có Sẵn

### HttpContext Extensions
```csharp
// Lấy thông tin user
var user = HttpContext.GetSyncedUser();
var userId = HttpContext.GetSyncedUserId();
var userRole = HttpContext.GetSyncedUserRole();

// Kiểm tra role
bool isAdmin = HttpContext.IsAdmin();
bool isTeacher = HttpContext.IsTeacher();
bool isStudent = HttpContext.IsStudent();
```

## Cấu Trúc Dữ Liệu

### UserSyncDto
```csharp
public class UserSyncDto
{
    public int UserId { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string RoleName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
```

## Roles Trong Hệ Thống

1. **Admin** (RoleId: 1)
   - Có quyền truy cập tất cả chức năng
   - Quản lý toàn bộ hệ thống

2. **Teacher** (RoleId: 2)
   - Tạo và quản lý bài thi
   - Tạo và quản lý tài liệu
   - Xem kết quả học sinh

3. **Student** (RoleId: 3)
   - Tham gia bài thi
   - Xem tài liệu (có thể bị giới hạn theo loại tài liệu)
   - Xem kết quả của mình

## Ví Dụ Thực Tế

### 1. Kiểm Tra Quyền Truy Cập Bài Thi
```csharp
[HttpGet("{examId}/access")]
[Authorize]
public async Task<IActionResult> CheckExamAccess(int examId)
{
    var userRole = HttpContext.GetSyncedUserRole();
    var userId = HttpContext.GetSyncedUserId();

    bool hasAccess = userRole?.ToLower() switch
    {
        "admin" => true,
        "teacher" => true,
        "student" => await CheckStudentExamAccess(userId.Value, examId),
        _ => false
    };

    return Ok(new { HasAccess = hasAccess });
}
```

### 2. Lọc Dữ Liệu Theo Role
```csharp
[HttpGet]
[Authorize]
public async Task<IActionResult> GetMaterials()
{
    var userRole = HttpContext.GetSyncedUserRole();
    var userId = HttpContext.GetSyncedUserId();

    var query = _context.Materials.AsQueryable();

    // Lọc theo role
    if (userRole?.ToLower() == "student")
    {
        // Student chỉ xem tài liệu miễn phí hoặc đã mua
        query = query.Where(m => !m.IsPaid || 
                           _context.Purchases.Any(p => p.UserId == userId && p.MaterialId == m.MaterialId));
    }

    var materials = await query.ToListAsync();
    return Ok(materials);
}
```

## Lưu Ý Quan Trọng

1. **Bảo Mật**: Luôn kiểm tra token hợp lệ trước khi xử lý
2. **Performance**: Middleware cache thông tin user trong request để tránh gọi API nhiều lần
3. **Error Handling**: Xử lý trường hợp AuthService không khả dụng
4. **Logging**: Log các hoạt động sync để debug và monitor

## Troubleshooting

### Lỗi Thường Gặp

1. **"User not found"**: Kiểm tra JWT token có hợp lệ không
2. **"Service unavailable"**: Kiểm tra AuthService có đang chạy không
3. **"Invalid configuration"**: Kiểm tra URL trong appsettings.json

### Debug Tips

1. Kiểm tra logs trong console
2. Sử dụng Swagger để test API endpoints
3. Verify JWT token tại jwt.io
4. Kiểm tra network connectivity giữa các services

## Kết Luận

User Sync System cung cấp một cách thống nhất và an toàn để quản lý thông tin user across microservices. Bằng cách sử dụng middleware và extension methods, việc implement trở nên đơn giản và nhất quán trong toàn bộ hệ thống.