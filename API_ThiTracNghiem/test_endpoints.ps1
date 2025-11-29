# Test script cho các endpoint đã sửa đổi trong ExamsService
$baseUrl = "http://localhost:5002"

Write-Host "=== TESTING EXAMS SERVICE ENDPOINTS ===" -ForegroundColor Green

# Test 1: Lấy danh sách subjects để có SubjectId
Write-Host "`n1. Testing GET /api/subjects" -ForegroundColor Yellow
try {
    $subjectsResponse = Invoke-RestMethod -Uri "$baseUrl/api/subjects" -Method GET
    Write-Host "Subjects Response:" -ForegroundColor Cyan
    $subjectsResponse | ConvertTo-Json -Depth 3
    
    if ($subjectsResponse -and $subjectsResponse.Count -gt 0) {
        $subjectId = $subjectsResponse[0].Id
        Write-Host "Using SubjectId: $subjectId for testing" -ForegroundColor Green
    } else {
        Write-Host "No subjects found. Creating a test subject..." -ForegroundColor Yellow
        # Tạo subject mới nếu không có
        $newSubject = @{
            Name = "Test Subject"
            Description = "Subject for testing"
        }
        $createSubjectResponse = Invoke-RestMethod -Uri "$baseUrl/api/subjects" -Method POST -Body ($newSubject | ConvertTo-Json) -ContentType "application/json"
        $subjectId = $createSubjectResponse.Id
        Write-Host "Created new subject with ID: $subjectId" -ForegroundColor Green
    }
} catch {
    Write-Host "Error getting subjects: $($_.Exception.Message)" -ForegroundColor Red
    # Sử dụng SubjectId mặc định
    $subjectId = 1
    Write-Host "Using default SubjectId: $subjectId" -ForegroundColor Yellow
}

# Test 2: Test CreateQuestion endpoint với SubjectId
Write-Host "`n2. Testing POST /api/questionbank/questions (CreateQuestion with SubjectId)" -ForegroundColor Yellow
$createQuestionRequest = @{
    SubjectId = $subjectId
    Content = "Test question with SubjectId - What is 2+2?"
    QuestionType = "MultipleChoice"
    Difficulty = "Easy"
    Marks = 1
    Tags = @("math", "basic")
    AnswerOptions = @(
        @{ Content = "3"; IsCorrect = $false }
        @{ Content = "4"; IsCorrect = $true }
        @{ Content = "5"; IsCorrect = $false }
        @{ Content = "6"; IsCorrect = $false }
    )
}

try {
    $createResponse = Invoke-RestMethod -Uri "$baseUrl/api/questionbank/questions" -Method POST -Body ($createQuestionRequest | ConvertTo-Json -Depth 3) -ContentType "application/json"
    Write-Host "Create Question Response:" -ForegroundColor Cyan
    $createResponse | ConvertTo-Json -Depth 3
    $createdQuestionId = $createResponse.Id
} catch {
    Write-Host "Error creating question: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Response: $($_.Exception.Response)" -ForegroundColor Red
}

# Test 3: Test GetQuestions endpoint với filter SubjectId
Write-Host "`n3. Testing GET /api/questionbank/questions with SubjectId filter" -ForegroundColor Yellow
try {
    $getQuestionsUrl = "$baseUrl/api/questionbank/questions?SubjectId=$subjectId&PageNumber=1&PageSize=10"
    $getResponse = Invoke-RestMethod -Uri $getQuestionsUrl -Method GET
    Write-Host "Get Questions Response:" -ForegroundColor Cyan
    $getResponse | ConvertTo-Json -Depth 4
} catch {
    Write-Host "Error getting questions: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Tạo một exam để test AddQuestionsFromBank
Write-Host "`n4. Creating test exam for AddQuestionsFromBank test" -ForegroundColor Yellow
$createExamRequest = @{
    Title = "Test Exam for Question Bank"
    Description = "Test exam to verify AddQuestionsFromBank functionality"
    SubjectId = $subjectId
    Duration = 60
    TotalQuestions = 0
    TotalMarks = 0
    IsActive = $true
    CreatedBy = "test-user"
}

try {
    $examResponse = Invoke-RestMethod -Uri "$baseUrl/api/exams" -Method POST -Body ($createExamRequest | ConvertTo-Json -Depth 3) -ContentType "application/json"
    Write-Host "Create Exam Response:" -ForegroundColor Cyan
    $examResponse | ConvertTo-Json -Depth 3
    $examId = $examResponse.Id
} catch {
    Write-Host "Error creating exam: $($_.Exception.Message)" -ForegroundColor Red
    # Sử dụng exam ID mặc định nếu tạo thất bại
    $examId = 1
}

# Test 5: Test AddQuestionsFromBank endpoint
Write-Host "`n5. Testing POST /api/exams/{examId}/questions/from-bank (AddQuestionsFromBank)" -ForegroundColor Yellow
if ($createdQuestionId) {
    $addQuestionsRequest = @{
        SubjectId = $subjectId
        QuestionIds = @($createdQuestionId)
    }

    try {
        $addQuestionsUrl = "$baseUrl/api/exams/$examId/questions/from-bank"
        $addResponse = Invoke-RestMethod -Uri $addQuestionsUrl -Method POST -Body ($addQuestionsRequest | ConvertTo-Json -Depth 3) -ContentType "application/json"
        Write-Host "Add Questions From Bank Response:" -ForegroundColor Cyan
        $addResponse | ConvertTo-Json -Depth 3
    } catch {
        Write-Host "Error adding questions from bank: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "Skipping AddQuestionsFromBank test - no question was created" -ForegroundColor Yellow
}

# Test 6: Test với SubjectId không tương thích
Write-Host "`n6. Testing AddQuestionsFromBank with incompatible SubjectId" -ForegroundColor Yellow
if ($createdQuestionId) {
    $incompatibleRequest = @{
        SubjectId = 999  # SubjectId không tồn tại
        QuestionIds = @($createdQuestionId)
    }

    try {
        $addQuestionsUrl = "$baseUrl/api/exams/$examId/questions/from-bank"
        $incompatibleResponse = Invoke-RestMethod -Uri $addQuestionsUrl -Method POST -Body ($incompatibleRequest | ConvertTo-Json -Depth 3) -ContentType "application/json"
        Write-Host "Unexpected success with incompatible SubjectId:" -ForegroundColor Red
        $incompatibleResponse | ConvertTo-Json -Depth 3
    } catch {
        Write-Host "Expected error with incompatible SubjectId: $($_.Exception.Message)" -ForegroundColor Green
    }
}

Write-Host "`n=== TESTING COMPLETED ===" -ForegroundColor Green