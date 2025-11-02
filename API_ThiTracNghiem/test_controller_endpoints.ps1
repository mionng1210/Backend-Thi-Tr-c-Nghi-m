#!/usr/bin/env pwsh

Write-Host "=== Testing ExamsService TestController Endpoints ===" -ForegroundColor Green
Write-Host ""

$baseUrl = "http://localhost:5002"

# Test 1: Ping endpoint
Write-Host "1. Testing Ping endpoint..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/ping" -Method Get
    Write-Host "✓ Ping successful: $($response | ConvertTo-Json)" -ForegroundColor Green
} catch {
    Write-Host "✗ Ping failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 2: Create Subject
Write-Host "2. Testing Create Subject..." -ForegroundColor Yellow
$subjectData = @{
    Name = "Toán học"
    Description = "Môn học Toán học cơ bản"
} | ConvertTo-Json

try {
    $subjectResponse = Invoke-RestMethod -Uri "$baseUrl/api/test/subjects" -Method Post -Body $subjectData -ContentType "application/json"
    Write-Host "✓ Subject created successfully:" -ForegroundColor Green
    Write-Host "  SubjectId: $($subjectResponse.subjectId)" -ForegroundColor Cyan
    Write-Host "  Name: $($subjectResponse.name)" -ForegroundColor Cyan
    $createdSubjectId = $subjectResponse.subjectId
} catch {
    Write-Host "✗ Create Subject failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 3: Get Subjects
Write-Host "3. Testing Get Subjects..." -ForegroundColor Yellow
try {
    $subjects = Invoke-RestMethod -Uri "$baseUrl/api/test/subjects" -Method Get
    Write-Host "✓ Get Subjects successful. Found $($subjects.Count) subjects:" -ForegroundColor Green
    foreach ($subject in $subjects) {
        Write-Host "  - ID: $($subject.SubjectId), Name: $($subject.Name)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "✗ Get Subjects failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: Create Question with SubjectId
Write-Host "4. Testing Create Question with SubjectId..." -ForegroundColor Yellow
$questionData = @{
    Content = "2 + 2 = ?"
    QuestionType = "MultipleChoice"
    Difficulty = "Easy"
    Marks = 1.0
    SubjectId = $createdSubjectId
    Tags = @("toán học", "cộng")
    Options = @(
        @{ Content = "3"; IsCorrect = $false }
        @{ Content = "4"; IsCorrect = $true }
        @{ Content = "5"; IsCorrect = $false }
        @{ Content = "6"; IsCorrect = $false }
    )
} | ConvertTo-Json -Depth 3

try {
    $questionResponse = Invoke-RestMethod -Uri "$baseUrl/api/test/questions" -Method Post -Body $questionData -ContentType "application/json"
    Write-Host "✓ Question created successfully:" -ForegroundColor Green
    Write-Host "  QuestionId: $($questionResponse.questionId)" -ForegroundColor Cyan
    Write-Host "  SubjectId: $($questionResponse.subjectId)" -ForegroundColor Cyan
    Write-Host "  SubjectName: $($questionResponse.subjectName)" -ForegroundColor Cyan
    Write-Host "  BankId: $($questionResponse.bankId)" -ForegroundColor Cyan
    $createdQuestionId = $questionResponse.questionId
} catch {
    Write-Host "✗ Create Question failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $errorStream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($errorStream)
        $errorBody = $reader.ReadToEnd()
        Write-Host "Error details: $errorBody" -ForegroundColor Red
    }
}

Write-Host ""

# Test 5: Get Questions without filter
Write-Host "5. Testing Get Questions (no filter)..." -ForegroundColor Yellow
try {
    $questions = Invoke-RestMethod -Uri "$baseUrl/api/test/questions" -Method Get
    Write-Host "✓ Get Questions successful. Found $($questions.Count) questions:" -ForegroundColor Green
    foreach ($question in $questions) {
        Write-Host "  - ID: $($question.QuestionId), Content: $($question.Content), SubjectId: $($question.SubjectId)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "✗ Get Questions failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 6: Get Questions with SubjectId filter
Write-Host "6. Testing Get Questions with SubjectId filter..." -ForegroundColor Yellow
try {
    $filteredQuestions = Invoke-RestMethod -Uri "$baseUrl/api/test/questions?subjectId=$createdSubjectId" -Method Get
    Write-Host "✓ Get Questions with filter successful. Found $($filteredQuestions.Count) questions for SubjectId ${createdSubjectId}:" -ForegroundColor Green
    foreach ($question in $filteredQuestions) {
        Write-Host "  - ID: $($question.QuestionId), Content: $($question.Content), SubjectId: $($question.SubjectId)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "✗ Get Questions with filter failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 7: Create another question with different SubjectId (should fail)
Write-Host "7. Testing Create Question with invalid SubjectId..." -ForegroundColor Yellow
$invalidQuestionData = @{
    Content = "Invalid question"
    QuestionType = "MultipleChoice"
    Difficulty = "Easy"
    Marks = 1.0
    SubjectId = 99999  # Non-existent SubjectId
    Tags = @("test")
    Options = @(
        @{ Content = "A"; IsCorrect = $true }
        @{ Content = "B"; IsCorrect = $false }
    )
} | ConvertTo-Json -Depth 3

try {
    $invalidResponse = Invoke-RestMethod -Uri "$baseUrl/api/test/questions" -Method Post -Body $invalidQuestionData -ContentType "application/json"
    Write-Host "✗ Expected failure but got success: $($invalidResponse | ConvertTo-Json)" -ForegroundColor Red
} catch {
    Write-Host "✓ Expected failure occurred: $($_.Exception.Message)" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Test completed ===" -ForegroundColor Green