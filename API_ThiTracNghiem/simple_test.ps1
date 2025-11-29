# Simple test script for ExamsService endpoints
$baseUrl = "http://localhost:5002"

Write-Host "=== SIMPLE ENDPOINT TESTING ===" -ForegroundColor Green

# Test 1: Ping endpoint
Write-Host "`n1. Testing Ping endpoint" -ForegroundColor Yellow
try {
    $pingResponse = Invoke-RestMethod -Uri "$baseUrl/api/ping" -Method GET
    Write-Host "Ping successful: $($pingResponse | ConvertTo-Json)" -ForegroundColor Green
} catch {
    Write-Host "Ping failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Test CreateQuestion endpoint (expect 401 Unauthorized)
Write-Host "`n2. Testing CreateQuestion endpoint (should return 401)" -ForegroundColor Yellow
$createQuestionBody = @{
    SubjectId = 1
    Content = "Test question - What is 2+2?"
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
} | ConvertTo-Json -Depth 3

try {
    $headers = @{ "Content-Type" = "application/json" }
    $createResponse = Invoke-RestMethod -Uri "$baseUrl/api/question-bank" -Method POST -Body $createQuestionBody -Headers $headers
    Write-Host "Unexpected success: $($createResponse | ConvertTo-Json)" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "Expected 401 Unauthorized - Authentication working correctly" -ForegroundColor Green
    } else {
        Write-Host "Unexpected error: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Yellow
    }
}

# Test 3: Test GetQuestions endpoint (expect 401 Unauthorized)
Write-Host "`n3. Testing GetQuestions endpoint (should return 401)" -ForegroundColor Yellow
try {
    $getResponse = Invoke-RestMethod -Uri "$baseUrl/api/question-bank?SubjectId=1" -Method GET
    Write-Host "Unexpected success: $($getResponse | ConvertTo-Json)" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "Expected 401 Unauthorized - Authentication working correctly" -ForegroundColor Green
    } else {
        Write-Host "Unexpected error: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Yellow
    }
}

Write-Host "`n=== TESTING COMPLETED ===" -ForegroundColor Green
Write-Host "Note: All endpoints require authentication. 401 errors are expected." -ForegroundColor Cyan