namespace HBCDirectory.Services.EmailTemplates
{
    public static class WelcomeEmailTemplate
    {
        public const string Html = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta name=""color-scheme"" content=""light only"">
  <meta name=""supported-color-schemes"" content=""light only"">
  <title>Welcome to the HBC Member Directory</title>
  <style>
    * { box-sizing: border-box; }
    body, table, td, p, a, li { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
    table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; border-collapse: collapse; }
    img { -ms-interpolation-mode: bicubic; border: 0; outline: none; text-decoration: none; display: block; }
    body { margin: 0; padding: 0; background-color: #EDE5D8; font-family: 'Open Sans', Arial, sans-serif; }
    .email-wrapper { width: 100%; background-color: #EDE5D8; padding: 32px 16px; }
    .email-card { max-width: 580px; margin: 0 auto; background-color: #FDFAF5; border-radius: 4px; border: 1px solid rgba(154,134,95,0.3); overflow: hidden; }
    .email-header { background-color: #202222; padding: 32px 40px 32px; text-align: center; }
    .email-logo { width: 48px; height: 48px; margin: 0 auto 16px; border-radius: 50%; background-color: #202222; }
    .email-title { font-family: 'Montserrat', 'Arial Black', sans-serif; font-size: 18px; font-weight: 700; letter-spacing: 0.28em; text-transform: uppercase; color: #c6b08d; margin: 0 0 16px; }
    .header-rule { width: 100%; height: 1px; background: linear-gradient(90deg, transparent, #847153 30%, #c6b08d 50%, #847153 70%, transparent); border: none; margin: 0; }
    .email-subtitle { font-family: 'Open Sans', Arial, sans-serif; font-size: 13px; font-style: italic; font-weight: 300; color: rgba(255,255,255,0.42); line-height: 1.8; margin: 14px 0 0; }
    .email-body { padding: 36px 40px 28px; }
    .greeting { font-size: 15px; color: #202222; line-height: 1.7; margin: 0 0 14px; }
    .body-text { font-size: 14px; color: #3d3f3f; line-height: 1.75; margin: 0 0 14px; }
    .credentials-box { background-color: #F5EFE4; border-left: 3px solid #9a865f; border-radius: 3px; padding: 18px 20px; margin: 24px 0; }
    .credentials-box p { font-size: 14px; color: #202222; margin: 6px 0; line-height: 1.5; }
    .credentials-box strong { font-weight: 700; font-size: 11px; letter-spacing: 0.1em; text-transform: uppercase; }
    .btn-wrap { text-align: center; margin: 28px 0; }
    .btn { display: inline-block; background-color: #9a865f; color: #202222 !important; font-family: 'Montserrat', Arial, sans-serif; font-size: 11px; font-weight: 700; letter-spacing: 0.26em; text-transform: uppercase; text-decoration: none; padding: 14px 36px; border-radius: 3px; }
    .info-box { background-color: #F5EFE4; border-left: 3px solid #c69760; border-radius: 3px; padding: 14px 18px; margin: 0 0 24px; }
    .info-box p { font-family: 'Montserrat', Arial, sans-serif; font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: #202222; margin: 0; }
    .email-footer { border-top: 1px solid rgba(154,134,95,0.2); padding: 20px 40px 28px; text-align: center; }
    .email-footer p { font-size: 12px; font-style: italic; color: #9a9b9b; margin: 4px 0; line-height: 1.6; }

    /* Belt-and-suspenders against email clients that auto-dark-mode
       regardless of the color-scheme meta tags above (Gmail app,
       some webmail clients, Outlook.com) — re-assert every color
       used above so nothing gets auto-inverted or reflowed. */
    @media (prefers-color-scheme: dark) {
      body, .email-wrapper { background-color: #EDE5D8 !important; }
      .email-card { background-color: #FDFAF5 !important; }
      .email-header, .email-logo { background-color: #202222 !important; }
      .email-title { color: #c6b08d !important; }
      .email-subtitle { color: rgba(255,255,255,0.42) !important; }
      .greeting, .body-text { color: #202222 !important; }
      .body-text { color: #3d3f3f !important; }
      .credentials-box, .info-box { background-color: #F5EFE4 !important; }
      .credentials-box p, .info-box p { color: #202222 !important; }
      .btn { background-color: #9a865f !important; color: #202222 !important; }
      .email-footer p { color: #9a9b9b !important; }
    }

    @media screen and (max-width: 480px) {
      .email-header { padding-left: 24px !important; padding-right: 24px !important; }
      .email-title { font-size: 15px !important; letter-spacing: 0.08em !important; }
    }
  </style>
</head>
<body>
<div class=""email-wrapper"">
  <div class=""email-card"">
    <div class=""email-header"">
      <img src=""https://www.heritagebaptistlibrary.co.za/file.png"" alt=""Heritage Baptist Church""
           class=""email-logo"" width=""48"" height=""48""
           style=""width:48px;height:48px;margin:0 auto 16px;border-radius:50%;background-color:#202222;-webkit-filter:none !important;filter:none !important;"">
      <h1 class=""email-title"">Heritage Baptist Church</h1>
      <hr class=""header-rule"">
      <p class=""email-subtitle"">Member Directory</p>
    </div>
    <div class=""email-body"">
      <p class=""greeting"">Dear <strong>{memberName}</strong>,</p>
      <p class=""body-text"">Welcome to the Heritage Baptist Church Member Directory. Your account has been set up by the admin. You can now log in to view the full church directory, find contact details for fellow members, and update your own profile.</p>
      <p class=""body-text"">Your login credentials are below:</p>
      <div class=""credentials-box"">
        <p><strong>Username (Email)</strong><br>{username}</p>
        <p style=""margin-top:12px;""><strong>Temporary Password</strong><br>{temporaryPassword}</p>
      </div>
      <p class=""body-text"">For security, please set a new password using the button below before logging in.</p>
      <div class=""btn-wrap"">
        <a href=""{resetPasswordLink}"" class=""btn"">Set My Password</a>
      </div>
      <div class=""info-box"">
        <p>Reset link expires in 3 days</p>
      </div>
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
