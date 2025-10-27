# Mẫu Test Data cho API Tạo Bài Thi

## 1. Endpoint Information
- **URL**: `POST http://localhost:5208/api/Exams`
- **Content-Type**: `application/json`
- **Authorization**: Bearer Token (Role: Teacher hoặc Admin)

## 2. Mẫu Request JSON

### Mẫu 1: Bài kiểm tra C# cơ bản
```json
{
  "title": "Kiểm tra C# cơ bản",
  "description": "Bài kiểm tra kiến thức C# cho người mới bắt đầu",
  "courseId": 1,
  "durationMinutes": 60,
  "totalQuestions": 5,
  "totalMarks": 50,
  "passingMark": 30,
  "examType": "Quiz",
  "startAt": "2025-01-20T08:00:00.000Z",
  "endAt": "2025-01-20T09:00:00.000Z",
  "randomizeQuestions": true,
  "allowMultipleAttempts": false,
  "status": "Draft",
  "questions": [
    {"questionId": 1, "marks": 10, "sequenceIndex": 1},
    {"questionId": 2, "marks": 10, "sequenceIndex": 2},
    {"questionId": 3, "marks": 10, "sequenceIndex": 3},
    {"questionId": 4, "marks": 10, "sequenceIndex": 4},
    {"questionId": 5, "marks": 10, "sequenceIndex": 5}
  ]
}
```

### Mẫu 2: Bài thi cuối kỳ
```json
{
  "title": "Thi cuối kỳ - Lập trình Web",
  "description": "Bài thi cuối kỳ môn Lập trình Web với ASP.NET Core",
  "courseId": 2,
  "durationMinutes": 120,
  "totalQuestions": 10,
  "totalMarks": 100,
  "passingMark": 60,
  "examType": "Final",
  "startAt": "2025-01-25T14:00:00.000Z",
  "endAt": "2025-01-25T16:00:00.000Z",
  "randomizeQuestions": false,
  "allowMultipleAttempts": false,
  "status": "Published",
  "questions": [
    {"questionId": 1, "marks": 10, "sequenceIndex": 1},
    {"questionId": 2, "marks": 10, "sequenceIndex": 2},
    {"questionId": 3, "marks": 10, "sequenceIndex": 3},
    {"questionId": 4, "marks": 10, "sequenceIndex": 4},
    {"questionId": 5, "marks": 10, "sequenceIndex": 5},
    {"questionId": 6, "marks": 10, "sequenceIndex": 6},
    {"questionId": 7, "marks": 10, "sequenceIndex": 7},
    {"questionId": 8, "marks": 10, "sequenceIndex": 8},
    {"questionId": 9, "marks": 10, "sequenceIndex": 9},
    {"questionId": 10, "marks": 10, "sequenceIndex": 10}
  ]
}
```

### Mẫu 3: Bài tập nhỏ
```json
{
  "title": "Bài tập tuần 3 - OOP",
  "description": "Bài tập về lập trình hướng đối tượng",
  "courseId": 1,
  "durationMinutes": 30,
  "totalQuestions": 3,
  "totalMarks": 30,
  "passingMark": 18,
  "examType": "Assignment",
  "startAt": "2025-01-18T10:00:00.000Z",
  "endAt": "2025-01-18T10:30:00.000Z",
  "randomizeQuestions": true,
  "allowMultipleAttempts": true,
  "status": "Draft",
  "questions": [
    {"questionId": 1, "marks": 10, "sequenceIndex": 1},
    {"questionId": 2, "marks": 10, "sequenceIndex": 2},
    {"questionId": 3, "marks": 10, "sequenceIndex": 3}
  ]
}
```

## 3. Cách test bằng PowerShell

### Bước 1: Lấy Bearer Token (nếu cần authentication)
```powershell
# Login để lấy token
$loginResponse = Invoke-WebRequest -Uri "http://localhost:5208/api/Auth/login" -Method POST -ContentType "application/json" -Body '{"email": "teacher@example.com", "password": "password123"}'
$token = ($loginResponse.Content | ConvertFrom-Json).data.token
```

### Bước 2: Tạo bài thi với token
```powershell
# Tạo header với Bearer token
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Gửi request tạo bài thi
$examData = @'
{
  "title": "Test Exam",
  "description": "This is a test exam",
  "courseId": 1,
  "durationMinutes": 60,
  "totalQuestions": 2,
  "totalMarks": 20,
  "passingMark": 12,
  "examType": "Quiz",
  "startAt": "2025-01-20T08:00:00.000Z",
  "endAt": "2025-01-20T09:00:00.000Z",
  "randomizeQuestions": true,
  "allowMultipleAttempts": true,
  "status": "Draft",
  "questions": [
    {"questionId": 1, "marks": 10, "sequenceIndex": 1},
    {"questionId": 2, "marks": 10, "sequenceIndex": 2}
  ]
}
'@

$response = Invoke-WebRequest -Uri "http://localhost:5208/api/Exams" -Method POST -Headers $headers -Body $examData
Write-Host "Response: $($response.Content)"
```

### Bước 3: Test không cần authentication (nếu đã tắt authorize)
```powershell
$examData = '{"title": "Simple Test", "description": "Basic test", "courseId": 1, "durationMinutes": 30, "totalQuestions": 1, "totalMarks": 10, "passingMark": 6, "examType": "Quiz", "startAt": "2025-01-20T08:00:00.000Z", "endAt": "2025-01-20T08:30:00.000Z", "randomizeQuestions": false, "allowMultipleAttempts": true, "status": "Draft", "questions": [{"questionId": 1, "marks": 10, "sequenceIndex": 1}]}'

$response = Invoke-WebRequest -Uri "http://localhost:5208/api/Exams" -Method POST -ContentType "application/json" -Body $examData
Write-Host "Success: $($response.StatusCode)"
Write-Host $response.Content
```

## 4. Các trường bắt buộc

- **title**: Tên bài thi (string, required)
- **description**: Mô tả bài thi (string, required)
- **courseId**: ID khóa học (int, required)
- **durationMinutes**: Thời gian làm bài (int, required)
- **totalQuestions**: Tổng số câu hỏi (int, required)
- **totalMarks**: Tổng điểm (int, required)
- **passingMark**: Điểm đậu (int, required)
- **examType**: Loại bài thi (string: "Quiz", "Midterm", "Final", "Assignment")
- **startAt**: Thời gian bắt đầu (datetime, ISO format)
- **endAt**: Thời gian kết thúc (datetime, ISO format)
- **status**: Trạng thái ("Draft", "Published", "Archived")

## 5. Response mẫu khi thành công

```json
{
  "statusCode": 200,
  "message": "OK",
  "data": {
    "id": 11,
    "title": "Kiểm tra C# cơ bản",
    "description": "Bài kiểm tra kiến thức C# cho người mới bắt đầu",
    "courseId": 1,
    "courseName": null,
    "teacherId": null,
    "teacherName": null,
    "subjectId": null,
    "subjectName": null,
    "durationMinutes": 60,
    "totalQuestions": 5,
    "totalMarks": 50,
    "passingMark": 30,
    "examType": "Quiz",
    "startAt": "2025-01-20T08:00:00Z",
    "endAt": "2025-01-20T09:00:00Z",
    "randomizeQuestions": true,
    "allowMultipleAttempts": false,
    "status": "Draft",
    "createdAt": "2025-10-17T07:45:00.000Z",
    "updatedAt": null,
    "questions": []
  }
}
```

## 6. Lưu ý quan trọng

1. **CourseId**: Đảm bảo courseId tồn tại trong database
2. **QuestionId**: Các questionId trong mảng questions phải tồn tại trong database
3. **DateTime format**: Sử dụng ISO 8601 format (YYYY-MM-DDTHH:mm:ss.sssZ)
4. **Authorization**: Cần đăng nhập với role Teacher hoặc Admin
5. **Validation**: Kiểm tra startAt < endAt, passingMark <= totalMarks

## 7. Troubleshooting

- **401 Unauthorized**: Cần đăng nhập hoặc token hết hạn
- **400 Bad Request**: Kiểm tra format JSON và các trường required
- **500 Internal Server Error**: Kiểm tra courseId, questionId có tồn tại không