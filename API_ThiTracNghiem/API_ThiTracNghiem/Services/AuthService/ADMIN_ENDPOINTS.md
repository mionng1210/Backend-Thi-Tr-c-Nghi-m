# Admin Endpoints Documentation

## Tổng quan
Các endpoint admin được thêm vào AuthService để quản lý người dùng. Chỉ có admin mới có thể truy cập các endpoint này.

**Danh sách endpoints**:
1. `GET /api/admin/users` - Lấy danh sách toàn bộ người dùng
2. `PUT /api/admin/users/{id}` - Cập nhật thông tin user
3. `PUT /api/admin/users/{id}/lock` - Khóa user
4. `PUT /api/admin/users/{id}/unlock` - Mở khóa user
5. `DELETE /api/admin/users/{id}` - Xóa user (soft delete)
6. `GET /api/admin/permissions/requests` - Lấy danh sách yêu cầu phân quyền
7. `PUT /api/admin/permissions/approve/{id}` - Duyệt yêu cầu và cập nhật role = Teacher
8. `PUT /api/admin/permissions/reject/{id}` - Từ chối yêu cầu, ghi lý do và gửi email
9. `PUT /api/admin/users/{userId}/role` - Đổi vai trò theo tên (vd: "student")

## Authentication
Tất cả các endpoint admin yêu cầu:
- Header `Authorization: Bearer <JWT_TOKEN>`
- User phải có role "admin"

## Endpoints

### 1. GET /api/admin/users
**Mô tả**: Lấy danh sách toàn bộ người dùng với phân trang và bộ lọc

**Query Parameters**:
- `page` (int, optional): Trang hiện tại (mặc định: 1)
- `pageSize` (int, optional): Số lượng user mỗi trang (mặc định: 10, tối đa: 100)
- `search` (string, optional): Tìm kiếm theo email, tên hoặc số điện thoại
- `status` (string, optional): Lọc theo trạng thái (active, inactive, banned)
- `roleId` (int, optional): Lọc theo role ID
- `role` (string, optional): Lọc theo tên vai trò (không phân biệt hoa thường), ví dụ `teacher`, `student`, `admin`

**Response**:
```json
{
  "users": [
    {
      "userId": 1,
      "email": "user@example.com",
      "phoneNumber": "0123456789",
      "fullName": "Nguyễn Văn A",
      "roleId": 2,
      "roleName": "User",
      "gender": "Nam",
      "dateOfBirth": "1990-01-01T00:00:00",
      "avatarUrl": "https://example.com/avatar.jpg",
      "status": "active",
      "isEmailVerified": true,
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-02T00:00:00Z",
      "lastLoginAt": "2024-01-03T00:00:00Z"
    }
  ],
  "totalCount": 100,
  "page": 1,
  "pageSize": 10,
  "totalPages": 10
}
```

**Ví dụ sử dụng**:
```bash
# Lấy trang đầu tiên với 20 users
GET /api/admin/users?page=1&pageSize=20

# Tìm kiếm user theo email
GET /api/admin/users?search=john@example.com

# Lọc user theo trạng thái active
GET /api/admin/users?status=active

# Lọc user theo role ID
GET /api/admin/users?roleId=2

# Lọc user theo tên vai trò
GET /api/admin/users?role=teacher

# Kết hợp nhiều filter
GET /api/admin/users?page=1&pageSize=10&status=active&role=teacher&search=nguyen
```

### 2. PUT /api/admin/users/{id}
**Mô tả**: Cập nhật thông tin user (chỉ admin)

**Path Parameters**:
- `id` (int): ID của user cần cập nhật

**Request Body**:
```json
{
  "email": "newemail@example.com",
  "phoneNumber": "0987654321",
  "fullName": "Nguyễn Văn B",
  "roleId": 2,
  "gender": "Nam",
  "dateOfBirth": "01/01/1990",
  "status": "active",
  "isEmailVerified": true
}
```

**Validation Rules**:
- `email`: Tối đa 256 ký tự, phải là email hợp lệ, không trùng với user khác
- `phoneNumber`: Tối đa 30 ký tự
- `fullName`: Tối đa 150 ký tự
- `roleId`: Phải tồn tại trong bảng Roles
- `gender`: Chỉ nhận "Nam" hoặc "Nữ"
- `dateOfBirth`: Định dạng dd/MM/yyyy, không được là ngày tương lai, không quá 120 tuổi
- `status`: Chỉ nhận "active", "inactive" hoặc "banned"
- `isEmailVerified`: Boolean

**Response Success**:
```json
{
  "message": "Cập nhật thông tin người dùng thành công"
}
```

**Response Error Examples**:
```json
// User không tồn tại
{
  "message": "Không tìm thấy người dùng"
}

// Email đã được sử dụng
{
  "message": "Email đã được sử dụng bởi người dùng khác"
}

// Role không tồn tại
{
  "message": "Role không tồn tại"
}

// Validation errors
{
  "errors": {
    "DateOfBirth": ["Ngày sinh phải có định dạng dd/MM/yyyy"],
    "Gender": ["Giới tính chỉ nhận 'Nam' hoặc 'Nữ'"]
  }
}
```

**Ví dụ sử dụng**:
```bash
# Cập nhật email và trạng thái
PUT /api/admin/users/123
Content-Type: application/json
Authorization: Bearer <admin_token>

{
  "email": "newemail@example.com",
  "status": "Banned"
}

# Cập nhật role của user
PUT /api/admin/users/123
Content-Type: application/json
Authorization: Bearer <admin_token>

{
  "roleId": 1
}
```

### 3. PUT /api/admin/users/{id}/lock
**Mô tả**: Khóa user (chỉ admin)

**Path Parameters**:
- `id` (int): ID của user cần khóa

**Request Body**: Không cần

**Response Success**:
```json
{
  "message": "Đã khóa người dùng thành công"
}
```

**Response Error Examples**:
```json
// User không tồn tại
{
  "message": "Không tìm thấy người dùng"
}

// Cố gắng khóa admin
{
  "message": "Không thể khóa tài khoản admin"
}
```

**Ví dụ sử dụng**:
```bash
# Khóa user có ID 123
PUT /api/admin/users/123/lock
Authorization: Bearer <admin_token>
```

### 4. PUT /api/admin/users/{id}/unlock
**Mô tả**: Mở khóa user (chỉ admin)

**Path Parameters**:
- `id` (int): ID của user cần mở khóa

**Request Body**: Không cần

**Response Success**:
```json
{
  "message": "Đã mở khóa người dùng thành công"
}
```

**Response Error Examples**:
```json
// User không tồn tại
{
  "message": "Không tìm thấy người dùng"
}
```

**Ví dụ sử dụng**:
```bash
# Mở khóa user có ID 123
PUT /api/admin/users/123/unlock
Authorization: Bearer <admin_token>
```

### 5. DELETE `/api/admin/users/{id}` - Xóa user (soft delete)

Xóa user khỏi hệ thống (soft delete) và ghi log hoạt động.

**Method**: DELETE  
**URL**: `/api/admin/users/{id}`  
**Authentication**: Required (Admin only)

**Path Parameters**:
- `id` (int): ID của user cần xóa

**Request Body**: Không cần

**Response Success**:
```json
{
  "message": "Đã xóa người dùng thành công",
  "deletedUser": {
    "userId": 123,
    "email": "user@example.com",
    "fullName": "Nguyễn Văn A",
    "deletedAt": "2024-01-15T10:30:00.000Z",
    "deletedBy": "Admin Name"
  }
}
```

**Response Error Examples**:
```json
// User không tồn tại
{
  "message": "Không tìm thấy người dùng"
}

// Cố gắng xóa admin
{
  "message": "Không thể xóa tài khoản admin"
}

// Lỗi server
{
  "message": "Có lỗi xảy ra khi xóa người dùng",
  "error": "Detailed error message"
}
```

**Ví dụ sử dụng**:
```bash
# Xóa user có ID 123
DELETE /api/admin/users/123
Authorization: Bearer <admin_token>
```

**Logging**: 
- Mọi hoạt động xóa user đều được ghi log chi tiết
- Log bao gồm: admin thực hiện, user bị xóa, thời gian
- Log được ghi vào console và có thể mở rộng để ghi vào database

**Bảo mật**:
- Không cho phép xóa tài khoản admin
- Chỉ thực hiện soft delete (đánh dấu HasDelete = true)
- Ghi log đầy đủ để audit trail

## Error Responses

### 401 Unauthorized
```json
{
  "message": "Token không hợp lệ"
}
```

### 403 Forbidden
```json
{
  "message": "Chỉ admin mới có thể truy cập endpoint này"
}
```

### 404 Not Found
```json
{
  "message": "Không tìm thấy người dùng"
}
```

### 400 Bad Request
```json
{
  "errors": {
    "field": ["Error message"]
  }
}
```

### 500 Internal Server Error
```json
{
  "message": "Có lỗi xảy ra khi cập nhật thông tin",
  "error": "Detailed error message"
}
```

## Security Notes

1. **Authorization**: Tất cả endpoint đều yêu cầu JWT token hợp lệ
2. **Role Check**: Chỉ user có role "admin" mới có thể truy cập
3. **Data Validation**: Tất cả input đều được validate kỹ lưỡng
4. **Email Uniqueness**: Kiểm tra email không trùng lặp khi cập nhật
5. **Soft Delete**: Chỉ hiển thị user chưa bị xóa (HasDelete = false)

## Testing

Để test các endpoint này:

1. Đăng nhập với tài khoản admin để lấy JWT token
2. Sử dụng token trong header Authorization
3. Test các trường hợp success và error
4. Kiểm tra pagination và filtering
5. Verify data validation rules
### 6. GET /api/admin/permissions/requests
**Mô tả**: Lấy danh sách yêu cầu phân quyền (mặc định lọc theo `pending`).

**Query Parameters**:
- `status` (string, optional): Lọc theo trạng thái `pending|approved|rejected`. Mặc định: `pending`.

**Response**:
```json
{
  "requests": [
    {
      "id": 1,
      "userId": 123,
      "email": "user@example.com",
      "fullName": "Nguyễn Văn A",
      "requestedRoleId": 2,
      "status": "pending",
      "submittedAt": "2025-10-20T08:00:00Z",
      "reviewedAt": null,
      "reviewedById": null,
      "rejectReason": null
    }
  ],
  "count": 1
}
```

### 7. PUT /api/admin/permissions/approve/{id}
**Mô tả**: Duyệt yêu cầu phân quyền và cập nhật role của user thành Teacher.

**Path Parameters**:
- `id` (int): ID của yêu cầu.

**Response**:
```json
{
  "message": "Đã duyệt yêu cầu và cập nhật role = Teacher",
  "userId": 123
}
```

### 8. PUT /api/admin/permissions/reject/{id}
**Mô tả**: Từ chối yêu cầu phân quyền, ghi lý do và gửi email thông báo tới user.

**Path Parameters**:
- `id` (int): ID của yêu cầu.

**Body**:
```json
{
  "reason": "Hồ sơ chưa đủ thông tin chứng minh kinh nghiệm giảng dạy"
}
```

**Response**:
```json
{
  "message": "Đã từ chối yêu cầu và gửi email thông báo",
  "requestId": 1
}
```

### 9. PUT /api/admin/users/{userId}/role
**Mô tả**: Admin chủ động đổi/thu hồi quyền bằng cách đặt vai trò theo tên (không phân biệt hoa thường).

**Path Parameters**:
- `userId` (int): ID người dùng cần đổi vai trò

**Request Body**:
```json
{
  "role": "student"
}
```

**Response Success**: Trả về thông tin user sau cập nhật
```json
{
  "userId": 123,
  "email": "user@example.com",
  "fullName": "Nguyễn Văn A",
  "roleId": 1,
  "roleName": "Student",
  "status": "active",
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-11-06T04:30:00Z"
}
```

**Response Error Examples**:
```json
// User không tồn tại
{
  "message": "Không tìm thấy người dùng"
}

// Role không tồn tại
{
  "message": "Role không tồn tại"
}

// Không cho phép đổi vai trò của admin sang non-admin
{
  "message": "Không thể thay đổi vai trò của admin"
}
```

**Ví dụ sử dụng**:
```bash
# Thu hồi quyền giáo viên về student
PUT /api/admin/users/123/role
Content-Type: application/json
Authorization: Bearer <admin_token>

{
  "role": "student"
}

# Cấp quyền teacher
PUT /api/admin/users/123/role
Content-Type: application/json
Authorization: Bearer <admin_token>

{
  "role": "teacher"
}
```