# Script để test seed data sau khi sửa lỗi
Write-Host "=== TEST SEED DATA SCRIPT ===" -ForegroundColor Green
Write-Host "Đang test seed data sau khi sửa lỗi FOREIGN KEY..." -ForegroundColor Yellow

# Kiểm tra xem có đang ở đúng thư mục không
if (-not (Test-Path "API_ThiTracNghiem.csproj")) {
    Write-Host "Lỗi: Không tìm thấy file API_ThiTracNghiem.csproj" -ForegroundColor Red
    Write-Host "Vui lòng chạy script này từ thư mục gốc của project" -ForegroundColor Red
    exit 1
}

# Xóa database cũ để test từ đầu
Write-Host "Đang xóa database cũ..." -ForegroundColor Yellow
dotnet ef database drop --force

if ($LASTEXITCODE -ne 0) {
    Write-Host "Lỗi xóa database!" -ForegroundColor Red
    exit 1
}

# Tạo lại database và migration
Write-Host "Đang tạo lại database..." -ForegroundColor Yellow
dotnet ef database update

if ($LASTEXITCODE -ne 0) {
    Write-Host "Lỗi tạo database!" -ForegroundColor Red
    exit 1
}

# Build project
Write-Host "Đang build project..." -ForegroundColor Yellow
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Lỗi build project!" -ForegroundColor Red
    exit 1
}

# Chạy ứng dụng để test seed data
Write-Host "Đang chạy ứng dụng để test seed data..." -ForegroundColor Yellow
Write-Host "Ứng dụng sẽ tự động seed dữ liệu khi khởi động" -ForegroundColor Cyan
Write-Host "Nhấn Ctrl+C để dừng ứng dụng sau khi thấy 'Application started'" -ForegroundColor Cyan

dotnet run

Write-Host "=== HOÀN THÀNH TEST ===" -ForegroundColor Green
Write-Host "Nếu không có lỗi, seed data đã hoạt động thành công!" -ForegroundColor Green
