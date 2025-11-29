-- Script SQL để thêm cột Content vào bảng Lessons
-- Chạy script này trong SQL Server Management Studio hoặc Azure Data Studio

-- Kiểm tra xem cột đã tồn tại chưa
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Lessons]') 
    AND name = 'Content'
)
BEGIN
    ALTER TABLE [dbo].[Lessons]
    ADD [Content] nvarchar(MAX) NULL;
    
    PRINT 'Đã thêm cột Content vào bảng Lessons thành công!';
END
ELSE
BEGIN
    PRINT 'Cột Content đã tồn tại trong bảng Lessons.';
END
GO

