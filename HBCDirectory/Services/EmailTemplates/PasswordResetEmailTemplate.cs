namespace HBCDirectory.Services.EmailTemplates
{
    public static class PasswordResetEmailTemplate
    {
        public const string Html = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Reset Your HBC Directory Password</title>
  <style>
    * { box-sizing: border-box; }
    body { margin: 0; padding: 0; background-color: #EDE5D8; font-family: 'Open Sans', Arial, sans-serif; }
    .email-wrapper { width: 100%; background-color: #EDE5D8; padding: 32px 16px; }
    .email-card { max-width: 580px; margin: 0 auto; background-color: #FDFAF5; border-radius: 4px; border: 1px solid rgba(154,134,95,0.3); overflow: hidden; }
    .email-header { background-color: #202222; padding: 40px 40px 32px; text-align: center; }
    .email-title { font-family: 'Montserrat', 'Arial Black', sans-serif; font-size: 17px; font-weight: 700; letter-spacing: 0.26em; text-transform: uppercase; color: #c6b08d; margin: 0 0 16px; }
    .header-rule { width: 100%; height: 1px; background: linear-gradient(90deg, transparent, #847153 30%, #c6b08d 50%, #847153 70%, transparent); border: none; margin: 0; }
    .email-body { padding: 36px 40px 28px; }
    .greeting { font-size: 15px; color: #202222; line-height: 1.7; margin: 0 0 14px; }
    .body-text { font-size: 14px; color: #3d3f3f; line-height: 1.75; margin: 0 0 14px; }
    .btn-wrap { text-align: center; margin: 28px 0; }
    .btn { display: inline-block; background-color: #9a865f; color: #202222 !important; font-family: 'Montserrat', Arial, sans-serif; font-size: 11px; font-weight: 700; letter-spacing: 0.26em; text-transform: uppercase; text-decoration: none; padding: 14px 36px; border-radius: 3px; }
    .warning-box { background-color: #F5EFE4; border-left: 3px solid #c69760; border-radius: 3px; padding: 14px 18px; margin: 22px 0; }
    .warning-box p { font-family: 'Montserrat', Arial, sans-serif; font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: #202222; margin: 0; }
    .fallback-label { font-size: 12px; color: rgba(32,34,34,0.5); font-style: italic; margin: 18px 0 6px; }
    .fallback-link { font-family: 'Courier New', Courier, monospace; font-size: 12px; color: #847153; word-break: break-all; background-color: #F5EFE4; padding: 10px 14px; border-radius: 3px; border: 1px solid rgba(154,134,95,0.2); display: block; margin: 0; line-height: 1.6; }
    .email-footer { border-top: 1px solid rgba(154,134,95,0.2); padding: 20px 40px 28px; text-align: center; }
    .email-footer p { font-size: 12px; font-style: italic; color: #9a9b9b; margin: 4px 0; line-height: 1.6; }
  </style>
</head>
<body>
<div class=""email-wrapper"">
  <div class=""email-card"">
    <div class=""email-header"">
      <h1 class=""email-title"">Heritage Baptist Church</h1>
      <hr class=""header-rule"">
    </div>
    <div class=""email-body"">
      <p class=""greeting"">Dear <strong>{memberName}</strong>,</p>
      <p class=""body-text"">We received a request to reset your password for the HBC Member Directory. If you did not make this request, you can safely ignore this email.</p>
      <p class=""body-text"">Click the button below to reset your password:</p>
      <div class=""btn-wrap"">
        <a href=""{resetPasswordLink}"" class=""btn"">Reset My Password</a>
      </div>
      <div class=""warning-box"">
        <p>This link expires in 3 days</p>
      </div>
      <p class=""fallback-label"">If the button doesn't work, copy and paste this link into your browser:</p>
      <p class=""fallback-link"">{resetPasswordLink}</p>
    </div>
    <div class=""email-footer"">
      <p>Heritage Baptist Church Johannesburg</p>
      <p>Soli Deo Gloria</p>
    </div>
  </div>
</div>
</body>
</html>";
    }
}
