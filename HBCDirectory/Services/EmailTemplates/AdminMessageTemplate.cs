namespace HBCDirectory.Services.EmailTemplates
{
    // A plain announcement/message email, composed freely by Admin and sent to selected members
    public static class AdminMessageTemplate
    {
        public const string Html = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta name=""color-scheme"" content=""light only"">
  <meta name=""supported-color-schemes"" content=""light only"">
  <title>{subject}</title>
  <style>
    * { box-sizing: border-box; }
    body { margin: 0; padding: 0; background-color: #EDE5D8; font-family: 'Open Sans', Arial, sans-serif; }
    .email-wrapper { width: 100%; background-color: #EDE5D8; padding: 32px 16px; }
    .email-card { max-width: 580px; margin: 0 auto; background-color: #FDFAF5; border-radius: 4px; border: 1px solid rgba(154,134,95,0.3); overflow: hidden; }
    .email-header { background-color: #202222; padding: 32px 40px 32px; text-align: center; }
    .email-logo { width: 48px; height: 48px; margin: 0 auto 16px; border-radius: 50%; background-color: #202222; }
    .email-title { font-family: 'Montserrat', 'Arial Black', sans-serif; font-size: 17px; font-weight: 700; letter-spacing: 0.26em; text-transform: uppercase; color: #c6b08d; margin: 0 0 16px; }
    .header-rule { width: 100%; height: 1px; background: linear-gradient(90deg, transparent, #847153 30%, #c6b08d 50%, #847153 70%, transparent); border: none; margin: 0; }
    .email-body { padding: 36px 40px 28px; }
    .greeting { font-size: 15px; color: #202222; line-height: 1.7; margin: 0 0 14px; }
    .body-text { font-size: 14px; color: #3d3f3f; line-height: 1.75; margin: 0 0 14px; }
    .email-footer { border-top: 1px solid rgba(154,134,95,0.2); padding: 20px 40px 28px; text-align: center; }
    .email-footer p { font-size: 12px; font-style: italic; color: #9a9b9b; margin: 4px 0; line-height: 1.6; }

    @media (prefers-color-scheme: dark) {
      body, .email-wrapper { background-color: #EDE5D8 !important; }
      .email-card { background-color: #FDFAF5 !important; }
      .email-header, .email-logo { background-color: #202222 !important; }
      .email-title { color: #c6b08d !important; }
      .greeting, .body-text { color: #202222 !important; }
      .body-text { color: #3d3f3f !important; }
      .email-footer p { color: #9a9b9b !important; }
    }

    @media screen and (max-width: 480px) {
      .email-header { padding-left: 24px !important; padding-right: 24px !important; }
      .email-title { font-size: 14px !important; letter-spacing: 0.08em !important; }
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
    </div>
    <div class=""email-body"">
      <p class=""greeting"">Dear <strong>{memberName}</strong>,</p>
      <p class=""body-text"">{messageBody}</p>
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
