# Endpoint GET /api/materials/{id}

## Mô tả

Endpoint này cho phép lấy thông tin chi tiết của một tài liệu theo ID.

## URL

```
GET /api/materials/{id}
```

## Parameters

- `id` (int, required): ID của tài liệu cần lấy (route parameter)

## Response Format

### Success Response (200 OK)

```json
{
  "statusCode": 200,
  "message": "Lấy thông tin tài liệu thành công",
  "data": {
    "id": 1,
    "title": "Giới thiệu về C# và .NET Framework",
    "description": "Tài liệu giới thiệu tổng quan về ngôn ngữ C# và nền tảng .NET Framework",
    "mediaType": "PDF",
    "isPaid": false,
    "price": null,
    "externalLink": null,
    "durationSeconds": 1800,
    "courseId": 1,
    "orderIndex": 1,
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": null
  }
}
```

### Error Responses

#### 400 Bad Request - ID không hợp lệ

```json
{
  "statusCode": 400,
  "message": "ID tài liệu không hợp lệ"
}
```

#### 404 Not Found - Không tìm thấy tài liệu

```json
{
  "statusCode": 404,
  "message": "Không tìm thấy tài liệu với ID đã cho"
}
```

#### 500 Internal Server Error

```json
{
  "statusCode": 500,
  "message": "Lỗi hệ thống khi lấy thông tin tài liệu"
}
```

## Kiến trúc

### Controller Layer

- `MaterialsController.GetById(int id)`
- Xử lý HTTP request/response
- Validation input
- Exception handling
- Logging

### Service Layer

- `IMaterialsService.GetByIdAsync(int id)`
- Business logic layer
- Có thể thêm caching, authorization logic

### Repository Layer

- `IMaterialsRepository.GetByIdAsync(int id)`
- Data access layer
- Entity Framework queries
- Soft delete support (HasDelete = false)

### Data Transfer Object (DTO)

- `MaterialListItemDto`
- Chứa đầy đủ 12 thuộc tính: Id, Title, Description, MediaType, IsPaid, Price, ExternalLink, DurationSeconds, CourseId, OrderIndex, CreatedAt, UpdatedAt
- Không trả thẳng entity để bảo mật

## Tính năng

### Performance

- Sử dụng `AsNoTracking()` để tối ưu performance
- Chỉ select các trường cần thiết
- Soft delete filtering

### Security

- Input validation
- Không expose internal entity structure
- Structured error messages

### Maintainability

- Clean Architecture pattern
- Dependency injection
- Interface-based design
- Easy to extract to microservice

### Monitoring

- Structured logging với correlation ID
- Error tracking
- Performance metrics ready

## Test Cases

1. **Valid ID**: Trả về thông tin tài liệu
2. **Invalid ID (0, negative)**: Trả về 400 Bad Request
3. **Non-existent ID**: Trả về 404 Not Found
4. **Deleted material**: Trả về 404 Not Found (soft delete)
5. **Database error**: Trả về 500 Internal Server Error

## Microservice Ready

Code được thiết kế để dễ dàng tách thành microservice:

- Interface-based dependencies
- DTO-based communication
- Independent business logic
- Standardized error handling
- Logging infrastructure
