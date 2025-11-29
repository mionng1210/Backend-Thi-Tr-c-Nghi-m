# Script để chèn nội dung vào file Word ở mục 2.2
# Yêu cầu: Microsoft Word phải được cài đặt

param(
    [string]$WordFilePath = "BCDACN.docx",
    [string]$ContentFile = "NOI_DUNG_CHO_MUC_2.2.txt"
)

# Kiểm tra file Word có tồn tại không
if (-not (Test-Path $WordFilePath)) {
    Write-Host "Không tìm thấy file Word: $WordFilePath" -ForegroundColor Red
    exit 1
}

# Kiểm tra file nội dung có tồn tại không
if (-not (Test-Path $ContentFile)) {
    Write-Host "Không tìm thấy file nội dung: $ContentFile" -ForegroundColor Red
    exit 1
}

Write-Host "Đang mở file Word..." -ForegroundColor Yellow

try {
    # Mở Word application
    $word = New-Object -ComObject Word.Application
    $word.Visible = $true
    
    # Mở document
    $doc = $word.Documents.Open((Resolve-Path $WordFilePath).Path)
    
    Write-Host "Đã mở file Word. Vui lòng:" -ForegroundColor Green
    Write-Host "1. Tìm đến mục '2.2 Thiết kế các bảng'" -ForegroundColor Cyan
    Write-Host "2. Đặt con trỏ ở cuối mục 2.2 (sau các bảng đã có)" -ForegroundColor Cyan
    Write-Host "3. Mở file NOI_DUNG_CHO_MUC_2.2.txt và copy nội dung" -ForegroundColor Cyan
    Write-Host "4. Paste vào Word và áp dụng styles:" -ForegroundColor Cyan
    Write-Host "   - Heading 2 cho '2.2 Thiết kế các bảng' (nếu chưa có)" -ForegroundColor Cyan
    Write-Host "   - Heading 3 cho các '2.2.X Bảng...'" -ForegroundColor Cyan
    Write-Host "   - Normal cho mô tả 'Lưu trữ...'" -ForegroundColor Cyan
    Write-Host "   - Bullet list cho các cột" -ForegroundColor Cyan
    
    Write-Host "`nNhấn Enter sau khi hoàn thành để đóng script..." -ForegroundColor Yellow
    Read-Host
    
    # Đóng document (không lưu tự động để tránh ghi đè)
    $doc.Close([Microsoft.Office.Interop.Word.WdSaveOptions]::wdDoNotSaveChanges)
    $word.Quit()
    
    Write-Host "Đã đóng Word." -ForegroundColor Green
}
catch {
    Write-Host "Lỗi: $_" -ForegroundColor Red
    if ($word) {
        $word.Quit()
    }
}



