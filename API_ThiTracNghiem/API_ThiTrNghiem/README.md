Supabase Storage notes:

- Bucket `materials` nên để Public để dùng URL `/object/public/...`.
- Nếu để Private, cần tạo Signed URL từ server (chưa triển khai ở đây).
- API yêu cầu header: `Authorization: Bearer <Anon or Service Role>` và `apikey: <same key>`.
