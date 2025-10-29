# Admin Endpoints Documentation

## Tổng quan
Các endpoint admin được thêm vào AuthService để quản lý người dùng. Chỉ có admin mới có thể truy cập các endpoint này.

**Danh sách endpoints**:
1. `GET /api/admin/users` - Lấy danh sách toàn bộ người dùng
2. `PUT /api/admin/users/{id}` - Cập nhật thông tin user
3. `PUT /api/admin/users/{id}/lock` - Khóa user
4. `PUT /api/admin/users/{id}/unlock` - Mở khóa user
5. `DELETE /api/admin/users/{id}` - Xóa user (soft delete)

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

# Kết hợp nhiều filter
GET /api/admin/users?page=1&pageSize=10&status=active&roleId=2&search=nguyen
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