# Script để seed dữ liệu mẫu vào database
# Chạy script này để thêm dữ liệu test cho endpoint GET /api/materials/{id}

Write-Host "=== SEED DATA SCRIPT ===" -ForegroundColor Green
Write-Host "Đang seed dữ liệu mẫu cho API Thi Trắc Nghiệm..." -ForegroundColor Yellow

# Kiểm tra xem có đang ở đúng thư mục không
if (-not (Test-Path "API_ThiTracNghiem.csproj")) {
    Write-Host "Lỗi: Không tìm thấy file API_ThiTracNghiem.csproj" -ForegroundColor Red
    Write-Host "Vui lòng chạy script này từ thư mục gốc của project" -ForegroundColor Red
    exit 1
}

# Build project
Write-Host "Đang build project..." -ForegroundColor Yellow
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Lỗi build project!" -ForegroundColor Red
    exit 1
}

# Chạy migration để đảm bảo database schema đã được tạo
Write-Host "Đang chạy migration..." -ForegroundColor Yellow
dotnet ef database update

if ($LASTEXITCODE -ne 0) {
    Write-Host "Lỗi migration!" -ForegroundColor Red
    exit 1
}

# Tạo một console app tạm thời để chạy seed data
Write-Host "Đang tạo script seed data..." -ForegroundColor Yellow

$seedScript = @"
using Microsoft.EntityFrameworkCore;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Models;

// Tạo DbContext
var connectionString = "Server=(localdb)\\mssqllocaldb;Database=API_ThiTracNghiem;Trusted_Connection=true;MultipleActiveResultSets=true";
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString)
    .Options;

using var context = new ApplicationDbContext(options);

try 
{
    Console.WriteLine("Đang seed dữ liệu...");
    await SeedData.SeedAsync(context);
    Console.WriteLine("✅ Seed dữ liệu thành công!");
    Console.WriteLine("📊 Dữ liệu đã được thêm:");
    Console.WriteLine($"   - {context.Subjects.Count()} Subjects");
    Console.WriteLine($"   - {context.ClassCohorts.Count()} ClassCohorts");
    Console.WriteLine($"   - {context.Courses.Count()} Courses");
    Console.WriteLine($"   - {context.Materials.Count()} Materials");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Lỗi seed dữ liệu: {ex.Message}");
    Environment.Exit(1);
}
"@

# Lưu script tạm thời
$tempScriptPath = "temp_seed.cs"
$seedScript | Out-File -FilePath $tempScriptPath -Encoding UTF8

# Chạy script
Write-Host "Đang chạy seed data..." -ForegroundColor Yellow
dotnet run --project . -- $tempScriptPath

# Xóa file tạm thời
Remove-Item $tempScriptPath -ErrorAction SilentlyContinue

Write-Host "=== HOÀN THÀNH ===" -ForegroundColor Green
Write-Host "Dữ liệu mẫu đã được thêm vào database!" -ForegroundColor Green
Write-Host "Bạn có thể test endpoint GET /api/materials/{id} với các ID từ 1-25" -ForegroundColor Cyan
