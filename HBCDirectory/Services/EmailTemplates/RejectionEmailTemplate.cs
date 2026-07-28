namespace HBCDirectory.Services.EmailTemplates
{
    // Sent when Admin rejects a pending submission (a member's profile
    // photo/update, or a family's photo) — see
    // EmailService.SendRejectionEmailAsync. Placeholders:
    // {subject}, {memberName}, {requestType} (e.g. "profile photo
    // update"), {submissionDetail}, {submittedDate}, {rejectionReason}
    // (already HTML-encoded by the caller), {ctaUrl}, {ctaLabel}.
    public static class RejectionEmailTemplate
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

    .detail-card { background-color: #F5EFE4; border-radius: 6px; padding: 18px 20px; margin: 4px 0 20px; }
    .detail-row { margin-bottom: 14px; }
    .detail-row:last-child { margin-bottom: 0; }
    .detail-label { font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: #847153; margin: 0 0 3px; }
    .detail-value { font-size: 14px; color: #202222; margin: 0; }

    .reason-card { background-color: #FBEAEA; border-left: 4px solid #a3392f; border-radius: 4px; padding: 16px 20px; margin: 0 0 20px; }
    .reason-label { font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: #a3392f; margin: 0 0 6px; }
    .reason-text { font-size: 14px; color: #202222; margin: 0; }

    .cta-wrap { text-align: center; margin: 26px 0 4px; }
    .cta-button { display: inline-block; background-color: #9a865f; color: #ffffff !important; text-decoration: none; font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; padding: 13px 30px; border-radius: 6px; }

    .email-footer { border-top: 1px solid rgba(154,134,95,0.2); padding: 20px 40px 28px; text-align: center; }
    .email-footer p { font-size: 12px; font-style: italic; color: #9a9b9b; margin: 4px 0; line-height: 1.6; }

    @media (prefers-color-scheme: dark) {
      body, .email-wrapper { background-color: #EDE5D8 !important; }
      .email-card { background-color: #FDFAF5 !important; }
      .email-header, .email-logo { background-color: #202222 !important; }
      .email-title { color: #c6b08d !important; }
      .greeting, .body-text, .detail-value, .reason-text { color: #202222 !important; }
      .body-text { color: #3d3f3f !important; }
      .detail-card { background-color: #F5EFE4 !important; }
      .reason-card { background-color: #FBEAEA !important; }
      .cta-button { background-color: #9a865f !important; color: #ffffff !important; }
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
      <p class=""body-text"">Thank you for your recent submission. Unfortunately, your {requestType} could not be approved at this time.</p>

      <div class=""detail-card"">
        <div class=""detail-row"">
          <p class=""detail-label"">Submission</p>
          <p class=""detail-value"">{submissionDetail}</p>
        </div>
        <div class=""detail-row"">
          <p class=""detail-label"">Submitted</p>
          <p class=""detail-value"">{submittedDate}</p>
        </div>
      </div>

      <div class=""reason-card"">
        <p class=""reason-label"">Reason</p>
        <p class=""reason-text"">{rejectionReason}</p>
      </div>

      <p class=""body-text"">If you believe this was made in error or would like further assistance, please use the ""Report an Issue"" button in the web app.</p>

      <div class=""cta-wrap"">
        <a href=""{ctaUrl}"" class=""cta-button"">{ctaLabel}</a>
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
