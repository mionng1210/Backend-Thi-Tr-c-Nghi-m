# Thiết Kế Các Bảng SQL

Tài liệu này mô tả chi tiết cấu trúc các bảng trong 4 services của hệ thống.

---

## 1. AuthService

### 1.1 Bảng User

Lưu trữ thông tin người dùng trong hệ thống:

•	UserId(PK): mã định danh người dùng.

•	Email: địa chỉ email của người dùng.

•	PhoneNumber: số điện thoại của người dùng.

•	PasswordHash: mật khẩu đã được mã hóa.

•	FullName: họ và tên đầy đủ của người dùng.

•	RoleId: mã vai trò của người dùng.

•	Gender: giới tính của người dùng.

•	DateOfBirth: ngày sinh của người dùng.

•	AvatarUrl: đường dẫn đến ảnh đại diện.

•	Status: trạng thái tài khoản.

•	IsEmailVerified: trạng thái xác thực email (true/false).

•	CreatedAt: thời gian tạo tài khoản.

•	UpdatedAt: thời gian cập nhật thông tin gần nhất.

•	LastLoginAt: thời gian đăng nhập lần cuối.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 1.2 Bảng Role

Lưu trữ thông tin các vai trò trong hệ thống:

•	RoleId(PK): mã định danh vai trò.

•	RoleName: tên vai trò.

•	Description: mô tả về vai trò.

•	CreatedAt: thời gian tạo vai trò.

•	UpdatedAt: thời gian cập nhật vai trò gần nhất.

---

### 1.3 Bảng OTP

Lưu trữ mã OTP (One-Time Password) để xác thực:

•	OtpId(PK): mã định danh OTP.

•	UserId: mã người dùng sở hữu mã OTP này.

•	OtpCode: mã OTP.

•	Purpose: mục đích sử dụng OTP.

•	ExpiresAt: thời gian hết hạn của mã OTP.

•	IsUsed: trạng thái đã sử dụng (true/false).

•	CreatedAt: thời gian tạo mã OTP.

---

### 1.4 Bảng AuthSession

Lưu trữ thông tin các phiên đăng nhập của người dùng:

•	SessionId(PK): mã định danh phiên đăng nhập.

•	UserId: mã người dùng sở hữu phiên này.

•	DeviceInfo: thông tin thiết bị đăng nhập.

•	IpAddress: địa chỉ IP thực hiện đăng nhập.

•	LoginAt: thời gian bắt đầu đăng nhập.

•	ExpiresAt: thời gian hết hạn của phiên đăng nhập.

•	IsActive: trạng thái hoạt động của phiên (true/false).

•	CreatedAt: thời gian tạo phiên.

•	UpdatedAt: thời gian cập nhật phiên gần nhất.

---

### 1.5 Bảng PermissionRequest

Lưu trữ thông tin các yêu cầu quyền từ người dùng:

•	PermissionRequestId(PK): mã định danh yêu cầu quyền.

•	UserId: mã người dùng gửi yêu cầu.

•	RequestedRoleId: mã vai trò được yêu cầu.

•	Status: trạng thái yêu cầu (pending/approved/rejected).

•	RejectReason: lý do từ chối (nếu bị từ chối).

•	SubmittedAt: thời gian gửi yêu cầu.

•	ReviewedAt: thời gian xem xét yêu cầu.

•	ReviewedById: mã người dùng xem xét yêu cầu.

•	CreatedAt: thời gian tạo yêu cầu.

•	UpdatedAt: thời gian cập nhật yêu cầu gần nhất.

•	BankName: tên ngân hàng.

•	BankAccountName: tên chủ tài khoản ngân hàng.

•	BankAccountNumber: số tài khoản ngân hàng.

•	PaymentMethod: phương thức thanh toán.

•	PaymentReference: mã tham chiếu thanh toán.

•	PaymentStatus: trạng thái thanh toán.

•	PaymentAmount: số tiền thanh toán.

---

## 2. ExamsService

### 2.1 Bảng Exam

Lưu trữ thông tin các bài thi trong hệ thống:

•	ExamId(PK): mã định danh bài thi.

•	CourseId: mã khóa học liên quan.

•	SubjectId: mã môn học.

•	Title: tiêu đề bài thi.

•	Description: mô tả bài thi.

•	DurationMinutes: thời gian làm bài (phút).

•	TotalQuestions: tổng số câu hỏi.

•	TotalMarks: tổng điểm.

•	PassingMark: điểm đạt.

•	ExamType: loại bài thi.

•	StartAt: thời gian bắt đầu.

•	EndAt: thời gian kết thúc.

•	RandomizeQuestions: có xáo trộn câu hỏi hay không (true/false).

•	AllowMultipleAttempts: cho phép làm nhiều lần hay không (true/false).

•	Status: trạng thái bài thi.

•	ImageUrl: đường dẫn ảnh bìa bài thi.

•	Price: giá bài thi.

•	OriginalPrice: giá gốc (để hiển thị giảm giá).

•	Level: cấp độ (Entry, Associate, Professional, Expert).

•	Difficulty: độ khó (Cơ bản, Trung bình, Nâng cao).

•	Provider: nhà cung cấp (AWS, Microsoft, Google Cloud, CompTIA, etc.).

•	FeaturesJson: danh sách tính năng dạng JSON.

•	ValidPeriod: thời hạn hiệu lực (ví dụ: "3 năm", "2 years").

•	CreatedBy: mã người tạo bài thi.

•	CreatedAt: thời gian tạo bài thi.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 2.2 Bảng ExamQuestion

Lưu trữ mối quan hệ giữa bài thi và câu hỏi:

•	ExamQuestionId(PK): mã định danh.

•	ExamId: mã bài thi.

•	QuestionId: mã câu hỏi.

•	SequenceIndex: thứ tự câu hỏi trong bài thi.

•	Marks: điểm số của câu hỏi.

•	CreatedAt: thời gian tạo.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 2.3 Bảng Question

Lưu trữ thông tin các câu hỏi:

•	QuestionId(PK): mã định danh câu hỏi.

•	BankId: mã ngân hàng câu hỏi.

•	Content: nội dung câu hỏi.

•	QuestionType: loại câu hỏi.

•	Difficulty: độ khó.

•	Marks: điểm số.

•	TagsJson: các thẻ phân loại dạng JSON.

•	CreatedBy: mã người tạo câu hỏi.

•	CreatedAt: thời gian tạo câu hỏi.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 2.4 Bảng QuestionBank

Lưu trữ thông tin các ngân hàng câu hỏi:

•	BankId(PK): mã định danh ngân hàng câu hỏi.

•	Name: tên ngân hàng câu hỏi.

•	Description: mô tả ngân hàng câu hỏi.

•	SubjectId: mã môn học.

•	CreatedBy: mã người tạo ngân hàng câu hỏi.

•	CreatedAt: thời gian tạo ngân hàng câu hỏi.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 2.5 Bảng AnswerOption

Lưu trữ thông tin các lựa chọn trả lời cho câu hỏi:

•	OptionId(PK): mã định danh lựa chọn.

•	QuestionId: mã câu hỏi.

•	Content: nội dung lựa chọn.

•	IsCorrect: đánh dấu đáp án đúng (true/false).

•	OrderIndex: thứ tự hiển thị.

•	CreatedAt: thời gian tạo.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 2.6 Bảng Course

Lưu trữ thông tin các khóa học:

•	CourseId(PK): mã định danh khóa học.

•	Title: tiêu đề khóa học.

•	Description: mô tả khóa học.

•	TeacherId: mã giáo viên.

•	SubjectId: mã môn học.

•	Price: giá khóa học.

•	IsFree: miễn phí hay không (true/false).

•	ThumbnailUrl: đường dẫn ảnh đại diện.

•	DurationMinutes: thời lượng khóa học (phút).

•	Level: cấp độ khóa học.

•	Status: trạng thái khóa học.

•	CreatedAt: thời gian tạo khóa học.

•	UpdatedAt: thời gian cập nhật khóa học gần nhất.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 2.7 Bảng Subject

Lưu trữ thông tin các môn học:

•	SubjectId(PK): mã định danh môn học.

•	Name: tên môn học.

•	Description: mô tả môn học.

•	CreatedAt: thời gian tạo môn học.

---

### 2.8 Bảng ExamAttempt

Lưu trữ thông tin các lần làm bài thi:

•	ExamAttemptId(PK): mã định danh lần làm bài.

•	ExamId: mã bài thi.

•	UserId: mã người dùng.

•	VariantCode: mã biến thể bài thi.

•	StartTime: thời gian bắt đầu làm bài.

•	EndTime: thời gian kết thúc làm bài.

•	SubmittedAt: thời gian nộp bài.

•	Score: điểm số đạt được.

•	MaxScore: điểm số tối đa.

•	Status: trạng thái (InProgress, Completed, Abandoned).

•	IsSubmitted: đã nộp bài hay chưa (true/false).

•	TimeSpentMinutes: thời gian làm bài (phút).

•	CreatedAt: thời gian tạo.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 2.9 Bảng SubmittedAnswer

Lưu trữ thông tin câu trả lời đã nộp:

•	SubmittedAnswerId(PK): mã định danh câu trả lời.

•	ExamAttemptId: mã lần làm bài.

•	QuestionId: mã câu hỏi.

•	TextAnswer: câu trả lời dạng văn bản (cho câu hỏi tự luận).

•	IsCorrect: đánh dấu trả lời đúng (true/false).

•	EarnedMarks: điểm số đạt được.

•	CreatedAt: thời gian tạo.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 2.10 Bảng SubmittedAnswerOption

Lưu trữ mối quan hệ giữa câu trả lời đã nộp và các lựa chọn:

•	SubmittedAnswerOptionId(PK): mã định danh.

•	SubmittedAnswerId: mã câu trả lời đã nộp.

•	AnswerOptionId: mã lựa chọn.

•	CreatedAt: thời gian tạo.

---

### 2.11 Bảng Enrollment

Lưu trữ thông tin đăng ký khóa học:

•	EnrollmentId(PK): mã định danh đăng ký.

•	UserId: mã người dùng.

•	CourseId: mã khóa học.

•	EnrollmentDate: ngày đăng ký.

•	ExpiryDate: ngày hết hạn.

•	Status: trạng thái đăng ký.

•	ProgressPercent: phần trăm tiến độ hoàn thành.

•	PaymentTransactionId: mã giao dịch thanh toán.

•	CreatedAt: thời gian tạo.

•	UpdatedAt: thời gian cập nhật gần nhất.

---

### 2.12 Bảng Lesson

Lưu trữ thông tin các bài học trong khóa học:

•	LessonId(PK): mã định danh bài học.

•	CourseId: mã khóa học.

•	Title: tiêu đề bài học.

•	Description: mô tả bài học.

•	Type: loại bài học (video, document, quiz, assignment).

•	VideoUrl: đường dẫn video.

•	ContentUrl: đường dẫn nội dung.

•	DurationSeconds: thời lượng (giây).

•	OrderIndex: thứ tự trong khóa học.

•	IsFree: miễn phí hay không (true/false).

•	CreatedAt: thời gian tạo.

•	UpdatedAt: thời gian cập nhật gần nhất.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 2.13 Bảng PaymentTransaction

Lưu trữ thông tin các giao dịch thanh toán:

•	TransactionId(PK): mã định danh giao dịch.

•	OrderId: mã đơn hàng.

•	UserId: mã người dùng.

•	Amount: số tiền.

•	Currency: loại tiền tệ.

•	Gateway: cổng thanh toán.

•	GatewayTransactionId: mã giao dịch từ cổng thanh toán.

•	Status: trạng thái giao dịch.

•	QrCodeData: dữ liệu mã QR.

•	Payload: dữ liệu bổ sung.

•	PaidAt: thời gian thanh toán.

•	CreatedAt: thời gian tạo giao dịch.

---

### 2.14 Bảng ExamEnrollment

Lưu trữ thông tin đăng ký bài thi:

•	EnrollmentId(PK): mã định danh đăng ký.

•	ExamId: mã bài thi.

•	UserId: mã người dùng.

•	Status: trạng thái đăng ký (Active, Pending, Cancelled).

•	CreatedAt: thời gian tạo.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

## 3. MaterialsService

### 3.1 Bảng Material

Lưu trữ thông tin các tài liệu học tập:

•	MaterialId(PK): mã định danh tài liệu.

•	CourseId: mã khóa học.

•	Title: tiêu đề tài liệu.

•	Description: mô tả tài liệu.

•	MediaType: loại phương tiện.

•	FileUrl: đường dẫn file.

•	IsPaid: tài liệu trả phí hay không (true/false).

•	Price: giá tài liệu.

•	ExternalLink: liên kết ngoài.

•	DurationSeconds: thời lượng (giây).

•	OrderIndex: thứ tự hiển thị.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

•	CreatedAt: thời gian tạo.

•	UpdatedAt: thời gian cập nhật gần nhất.

---

### 3.2 Bảng PaymentTransaction

Lưu trữ thông tin các giao dịch thanh toán:

•	TransactionId(PK): mã định danh giao dịch.

•	OrderId: mã đơn hàng.

•	UserId: mã người dùng.

•	Amount: số tiền.

•	Currency: loại tiền tệ (mặc định: VND).

•	Gateway: cổng thanh toán.

•	Status: trạng thái giao dịch (mặc định: Pending).

•	QrCodeData: dữ liệu mã QR.

•	Payload: dữ liệu bổ sung.

•	CreatedAt: thời gian tạo giao dịch.

---

## 4. ChatService

### 4.1 Bảng ChatRoom

Lưu trữ thông tin các phòng chat:

•	RoomId(PK): mã định danh phòng chat.

•	Name: tên phòng chat.

•	Description: mô tả phòng chat.

•	RoomType: loại phòng (general, course, exam, private).

•	CourseId: mã khóa học (nếu là phòng chat khóa học).

•	ExamId: mã bài thi (nếu là phòng chat bài thi).

•	CreatedBy: mã người tạo phòng chat.

•	CreatedAt: thời gian tạo phòng chat.

•	IsActive: trạng thái hoạt động (true/false).

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 4.2 Bảng ChatMessage

Lưu trữ thông tin các tin nhắn trong phòng chat:

•	MessageId(PK): mã định danh tin nhắn.

•	RoomId: mã phòng chat.

•	SenderId: mã người gửi.

•	Content: nội dung tin nhắn.

•	MessageType: loại tin nhắn (text, image, file, system).

•	AttachmentUrl: đường dẫn file đính kèm.

•	AttachmentName: tên file đính kèm.

•	ReplyToMessageId: mã tin nhắn được trả lời.

•	SentAt: thời gian gửi tin nhắn.

•	IsEdited: đã chỉnh sửa hay chưa (true/false).

•	EditedAt: thời gian chỉnh sửa.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 4.3 Bảng ChatRoomMember

Lưu trữ thông tin thành viên trong phòng chat:

•	MemberId(PK): mã định danh thành viên.

•	RoomId: mã phòng chat.

•	UserId: mã người dùng.

•	Role: vai trò trong phòng (admin, moderator, member).

•	JoinedAt: thời gian tham gia phòng.

•	LastSeenAt: thời gian xem lần cuối.

•	IsActive: trạng thái hoạt động (true/false).

---

### 4.4 Bảng Feedback

Lưu trữ thông tin phản hồi từ người dùng:

•	FeedbackId(PK): mã định danh phản hồi.

•	UserId: mã người dùng gửi phản hồi.

•	ExamId: mã bài thi (nếu phản hồi về bài thi).

•	Stars: số sao đánh giá (1-5).

•	Comment: nội dung phản hồi.

•	CreatedAt: thời gian tạo phản hồi.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 4.5 Bảng Notification

Lưu trữ thông tin các thông báo:

•	NotificationId(PK): mã định danh thông báo.

•	UserId: mã người dùng nhận thông báo.

•	Title: tiêu đề thông báo.

•	Message: nội dung thông báo.

•	Type: loại thông báo.

•	IsRead: đã đọc hay chưa (true/false).

•	CreatedAt: thời gian tạo thông báo.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

### 4.6 Bảng NotificationSetting

Lưu trữ cài đặt thông báo của người dùng:

•	SettingId(PK): mã định danh cài đặt.

•	UserId: mã người dùng.

•	EmailEnabled: bật thông báo qua email (true/false).

•	PopupEnabled: bật thông báo popup (true/false).

•	UpdatedAt: thời gian cập nhật cài đặt.

---

### 4.7 Bảng Report

Lưu trữ thông tin các báo cáo từ người dùng:

•	ReportId(PK): mã định danh báo cáo.

•	UserId: mã người dùng gửi báo cáo.

•	Description: mô tả nội dung báo cáo.

•	AttachmentPath: đường dẫn file đính kèm.

•	Status: trạng thái báo cáo (mặc định: Chưa xử lý).

•	CreatedAt: thời gian tạo báo cáo.

•	UpdatedAt: thời gian cập nhật báo cáo gần nhất.

•	HasDelete: cờ đánh dấu đã xóa (true/false).

---

## 5. Bảng Tham Chiếu (Reference Tables)

Các bảng User và Role được sử dụng làm tham chiếu trong các service khác, được đồng bộ từ AuthService thông qua UserSyncMiddleware.

### 5.1 Bảng User (Reference)

Cấu trúc tương tự như bảng User trong AuthService, được sử dụng để tham chiếu trong ExamsService và ChatService.

### 5.2 Bảng Role (Reference)

Cấu trúc tương tự như bảng Role trong AuthService, được sử dụng để tham chiếu trong ExamsService và ChatService.

---

*Tài liệu được tạo tự động dựa trên cấu trúc Entity Framework của các services.*

