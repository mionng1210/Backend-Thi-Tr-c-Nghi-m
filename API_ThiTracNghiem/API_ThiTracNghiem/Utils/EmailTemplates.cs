namespace API_ThiTracNghiem.Utils
{
    public static class EmailTemplates
    {
        public static string BuildOtpCard(string? greeting, string title, string otp, int expireMinutes)
        {
            var greet = string.IsNullOrWhiteSpace(greeting) ? string.Empty : $"<p style=\"margin:0 0 12px\">{greeting}</p>";
            return $@"<div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;background:#f5f7fb;padding:24px;'>
  <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 6px 20px rgba(0,0,0,0.08);overflow:hidden'>
    <div style='background:#3b82f6;color:#fff;padding:16px 20px'>
      <h2 style='margin:0;font-weight:600'>Thi Trắc Nghiệm</h2>
    </div>
    <div style='padding:20px 24px;color:#111827'>
      {greet}
      <h3 style='margin:0 0 8px;font-weight:600;color:#111827'>{title}</h3>
      <div style='margin:16px 0;padding:14px 18px;border:1px dashed #3b82f6;border-radius:10px;background:#eff6ff;text-align:center'>
        <div style='font-size:28px;letter-spacing:4px;font-weight:700;color:#1f2937'>{otp}</div>
      </div>
      <p style='margin:0;color:#4b5563'>Mã sẽ hết hạn sau <b>{expireMinutes} phút</b>. Vui lòng không chia sẻ cho bất kỳ ai.</p>
    </div>
    <div style='padding:14px 20px;background:#f9fafb;color:#6b7280;font-size:12px'>
      <p style='margin:0'>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>
    </div>
  </div>
</div>";
        }

        public static string BuildForgotPasswordCard(string fullName, string otp, int expireMinutes)
        {
            return $@"<div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;background:#f5f7fb;padding:24px;'>
  <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 6px 20px rgba(0,0,0,0.08);overflow:hidden'>
    <div style='background:#dc2626;color:#fff;padding:16px 20px'>
      <h2 style='margin:0;font-weight:600'>🔐 Đặt lại mật khẩu</h2>
    </div>
    <div style='padding:20px 24px;color:#111827'>
      <p style='margin:0 0 12px'>Xin chào <strong>{fullName}</strong>,</p>
      <p style='margin:0 0 16px;color:#4b5563'>Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Sử dụng mã OTP bên dưới để tiếp tục:</p>
      <div style='margin:20px 0;padding:16px 20px;border:2px solid #dc2626;border-radius:12px;background:#fef2f2;text-align:center'>
        <div style='font-size:32px;letter-spacing:6px;font-weight:800;color:#dc2626'>{otp}</div>
      </div>
      <p style='margin:16px 0 0;color:#dc2626;font-weight:600'>⚠️ Mã OTP sẽ hết hạn sau {expireMinutes} phút</p>
      <p style='margin:8px 0 0;color:#4b5563;font-size:14px'>Vì lý do bảo mật, vui lòng không chia sẻ mã này với bất kỳ ai.</p>
    </div>
    <div style='padding:16px 20px;background:#f9fafb;border-top:1px solid #e5e7eb'>
      <p style='margin:0;color:#6b7280;font-size:12px'>
        <strong>Lưu ý:</strong> Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này. 
        Tài khoản của bạn vẫn an toàn và không có thay đổi nào được thực hiện.
      </p>
    </div>
  </div>
</div>";
        }
    }
}


