using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using HBCDirectory.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HBCDirectory.Services
{
    public class DirectoryPdfService
    {
        private readonly PhotoService      _photos;
        private readonly HttpClient        _http;
        private readonly IConfiguration   _config;

        private const string Dark    = "#202222";
        private const string Gold    = "#9a865f";
        private const string Mustard = "#c69760";
        private const string Light   = "#f5f1eb";
        private const string Light2  = "#ede8df";
        private const string White   = "#ffffff";
        private const string Grey    = "#888888";

        public DirectoryPdfService(
            PhotoService photos,
            IHttpClientFactory httpClientFactory,
            IConfiguration config)
        {
            _photos = photos;
            _http   = httpClientFactory.CreateClient();
            _config = config;
        }

        // ── R2 helpers ────────────────────────────────────────────────────────
        private AmazonS3Client R2Client()
        {
            var cred = new BasicAWSCredentials(
                _config["R2:AccessKeyId"], _config["R2:SecretAccessKey"]);
            var cfg  = new AmazonS3Config
                { ServiceURL = _config["R2:Endpoint"], ForcePathStyle = true };
            return new AmazonS3Client(cred, cfg);
        }

        public async Task<string> UploadToR2Async(byte[] pdfBytes, string key)
        {
            using var client = R2Client();
            using var stream = new MemoryStream(pdfBytes);
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName            = _config["R2:BucketName"],
                Key                   = key,
                InputStream           = stream,
                ContentType           = "application/pdf",
                DisablePayloadSigning = true
            });
            return key;
        }

        public async Task<byte[]?> DownloadFromR2Async(string key)
        {
            try
            {
                using var client   = R2Client();
                var response = await client.GetObjectAsync(_config["R2:BucketName"], key);
                using var ms = new MemoryStream();
                await response.ResponseStream.CopyToAsync(ms);
                return ms.ToArray();
            }
            catch { return null; }
        }

        // ── Photo fetch from R2 ───────────────────────────────────────────────
        private async Task<byte[]?> FetchPhotoAsync(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            try
            {
                var response = await _http.GetAsync(_photos.Url(fileName));
                return response.IsSuccessStatusCode
                    ? await response.Content.ReadAsByteArrayAsync()
                    : null;
            }
            catch { return null; }
        }

        // ── Main generation ───────────────────────────────────────────────────
        public async Task<byte[]> GenerateAsync(
            List<Family>  families,
            List<Member>  unassigned,
            List<PdfPageConfig>? pageConfig = null)
        {
            var pages = (pageConfig ?? PdfSettings.DefaultPages())
                .Where(p => p.Enabled)
                .OrderBy(p => p.Order)
                .Select(p => p.Key)
                .ToList();

            var sortedFamilies = families.OrderBy(f => f.FamilyName).ToList();
            var allMembers = sortedFamilies.SelectMany(f => f.Members)
                                           .Concat(unassigned).ToList();

            // Fetch only the photos we need based on which pages are included
            var familyPhotoTasks = new Dictionary<int, Task<byte[]?>>();
            var memberPhotoTasks = new Dictionary<int, Task<byte[]?>>();

            if (pages.Contains("directory"))
            {
                foreach (var f in sortedFamilies.Where(f => !string.IsNullOrEmpty(f.PhotoFileName)))
                    familyPhotoTasks[f.Id] = FetchPhotoAsync(f.PhotoFileName);

                foreach (var m in unassigned.Where(m => !string.IsNullOrEmpty(m.PhotoFileName)))
                    memberPhotoTasks[m.Id] = FetchPhotoAsync(m.PhotoFileName);
            }

            if (familyPhotoTasks.Any() || memberPhotoTasks.Any())
                await Task.WhenAll(familyPhotoTasks.Values.Concat(memberPhotoTasks.Values));

            var familyPhotos = familyPhotoTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);
            var memberPhotos = memberPhotoTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);

            var byBirthday = allMembers
                .Where(m => m.Birthdate.HasValue && m.ShowBirthdate)
                .OrderBy(m => m.Birthdate!.Value.Month)
                .ThenBy(m => m.Birthdate!.Value.Day).ToList();

            var byAnniversary = allMembers
                .Where(m => m.Anniversary.HasValue && m.ShowAnniversary)
                .OrderBy(m => m.Anniversary!.Value.Month)
                .ThenBy(m => m.Anniversary!.Value.Day).ToList();

            return Document.Create(container =>
            {
                foreach (var key in pages)
                {
                    switch (key)
                    {
                        case "cover":
                            AddCoverPage(container, allMembers, sortedFamilies, unassigned);
                            break;
                        case "directory":
                            AddDirectoryPage(container, sortedFamilies, unassigned,
                                familyPhotos, memberPhotos);
                            break;
                        case "birthdays":
                            if (byBirthday.Any())
                                AddListPage(container, "Birthdays", byBirthday,
                                    m => m.Birthdate!.Value.ToString("MMMM d"),
                                    m => m.Birthdate!.Value.Month);
                            break;
                        case "anniversaries":
                            if (byAnniversary.Any())
                                AddListPage(container, "Anniversaries", byAnniversary,
                                    m => m.Anniversary!.Value.ToString("MMMM d"),
                                    m => m.Anniversary!.Value.Month);
                            break;
                    }
                }
            }).GeneratePdf();
        }

        // ══════════════════════════════════════════════════════════════════════
        // COVER PAGE
        // ══════════════════════════════════════════════════════════════════════
        private static void AddCoverPage(
            IDocumentContainer container,
            List<Member>        allMembers,
            List<Family>        families,
            List<Member>        unassigned)
        {
            container.Page(cover =>
            {
                cover.Size(PageSizes.A4);
                cover.Margin(0);
                cover.Background(Light);

                cover.Content().Column(col =>
                {
                    col.Item().Background(Dark)
                        .PaddingTop(80).PaddingBottom(60).PaddingHorizontal(50)
                        .Column(inner =>
                        {
                            inner.Item()
                                .Text("HERITAGE BAPTIST CHURCH")
                                .FontFamily("Arial").FontSize(13)
                                .FontColor(Mustard).Bold().LetterSpacing(0.15f);

                            inner.Item().PaddingTop(12)
                                .Text("Member\nDirectory")
                                .FontFamily("Arial").FontSize(48)
                                .FontColor(White).Bold().LineHeight(1.1f);

                            inner.Item().PaddingTop(20).Row(bar =>
                            {
                                bar.ConstantItem(60).Height(3).Background(Mustard);
                                bar.RelativeItem();
                            });

                            inner.Item().PaddingTop(16)
                                .Text($"Generated {DateTime.Today:MMMM d, yyyy}")
                                .FontFamily("Arial").FontSize(11).FontColor(Gold);
                        });

                    col.Item()
                        .PaddingTop(48).PaddingHorizontal(50).PaddingBottom(48)
                        .Column(stats =>
                        {
                            stats.Item()
                                .Text("DIRECTORY CONTENTS")
                                .FontFamily("Arial").FontSize(9)
                                .FontColor(Gold).Bold().LetterSpacing(0.12f);

                            stats.Item().PaddingTop(16).Row(row =>
                            {
                                StatBlock(row, allMembers.Count.ToString(),    "Members");
                                row.ConstantItem(1).Background(Light2);
                                StatBlock(row, families.Count(f => f.Members.Any()).ToString(), "Families");
                                row.ConstantItem(1).Background(Light2);
                                StatBlock(row, unassigned.Count.ToString(), "Individual Members");
                            });

                            stats.Item().PaddingTop(32)
                                .Text("Families listed alphabetically · Confidential — for members only")
                                .FontFamily("Arial").FontSize(9).FontColor(Grey).Italic();
                        });
                });
            });
        }

        private static void StatBlock(RowDescriptor row, string value, string label)
        {
            row.RelativeItem().PaddingLeft(0).Column(c =>
            {
                c.Item().Text(value)
                    .FontFamily("Arial").FontSize(36).Bold().FontColor(Dark);
                c.Item().Text(label)
                    .FontFamily("Arial").FontSize(10).FontColor(Gold);
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // DIRECTORY PAGE
        // Families: one card per family (group photo + member list)
        // Individuals: one card per person (photo + details)
        // ══════════════════════════════════════════════════════════════════════
        private static void AddDirectoryPage(
            IDocumentContainer container,
            List<Family>  families,
            List<Member>  unassigned,
            Dictionary<int, byte[]?> familyPhotos,
            Dictionary<int, byte[]?> memberPhotos)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(14,        Unit.Millimetre);
                page.MarginBottom(14,     Unit.Millimetre);
                page.MarginHorizontal(14, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9).FontColor(Dark));

                page.Header().Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        row.RelativeItem()
                            .Text("Heritage Baptist Church — Member Directory")
                            .Bold().FontSize(10).FontColor(Dark);
                        row.ConstantItem(100).AlignRight()
                            .Text(DateTime.Today.ToString("d MMM yyyy"))
                            .FontSize(9).FontColor(Gold);
                    });
                    h.Item().PaddingTop(4).Height(2).Background(Mustard);
                    h.Item().Height(8);
                });

                page.Content().Column(col =>
                {
                    if (!families.Any() && !unassigned.Any())
                    {
                        col.Item().PaddingTop(40).AlignCenter()
                            .Text("No members in directory yet.")
                            .FontSize(12).FontColor(Grey).Italic();
                        return;
                    }

                    // ── Families ───────────────────────────────────────────────
                    foreach (var family in families)
                    {
                        var adults   = family.Adults.ToList();
                        var children = family.Children.ToList();
                        if (!adults.Any() && !children.Any()) continue;

                        familyPhotos.TryGetValue(family.Id, out var photo);
                        RenderFamilyCard(col, family, adults, children, photo);
                        col.Item().Height(10);
                    }

                    // ── Individuals ────────────────────────────────────────────
                    if (unassigned.Any())
                    {
                        col.Item().Row(row =>
                        {
                            row.ConstantItem(4).Background(Gold);
                            row.RelativeItem().Background(Light)
                                .PaddingVertical(6).PaddingLeft(10)
                                .Text("INDIVIDUAL MEMBERS")
                                .Bold().FontSize(10).FontColor(Dark)
                                .LetterSpacing(0.08f);
                        });
                        col.Item().Height(8);

                        foreach (var m in unassigned.OrderBy(m => m.Surname).ThenBy(m => m.Name))
                        {
                            memberPhotos.TryGetValue(m.Id, out var mPhoto);
                            RenderIndividualCard(col, m, mPhoto);
                            col.Item().Height(6);
                        }
                    }
                });

                page.Footer().Column(f =>
                {
                    f.Item().Height(1).Background(Light2);
                    f.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem()
                            .Text("Heritage Baptist Church Johannesburg")
                            .FontSize(8).FontColor(Gold);
                        row.ConstantItem(80).AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(8).FontColor(Grey);
                            t.CurrentPageNumber().FontSize(8).FontColor(Dark).Bold();
                            t.Span(" of ").FontSize(8).FontColor(Grey);
                            t.TotalPages().FontSize(8).FontColor(Dark).Bold();
                        });
                    });
                });
            });
        }

        // ── Family card: ICD-style photo + member list ─────────────────────
        private static void RenderFamilyCard(
            ColumnDescriptor col,
            Family       family,
            List<Member> adults,
            List<Member> children,
            byte[]?      photo)
        {
            col.Item().Column(card =>
            {
                // Coloured header bar
                card.Item().Row(row =>
                {
                    row.ConstantItem(4).Background(Mustard);
                    row.RelativeItem().Background(Light)
                        .PaddingVertical(6).PaddingLeft(10)
                        .Row(inner =>
                        {
                            inner.RelativeItem().Column(c =>
                            {
                                c.Item().Text(family.FamilyName.ToUpper())
                                    .Bold().FontSize(11).FontColor(Dark).LetterSpacing(0.08f);
                                var memberCount = adults.Count + children.Count;
                                c.Item().Text($"{memberCount} {(memberCount == 1 ? "member" : "members")}")
                                    .FontSize(8).FontColor(Gold);
                            });
                        });
                });

                // Photo + member list in a row
                card.Item().Row(row =>
                {
                    // LEFT: family photo or initial placeholder
                    row.ConstantItem(130).Column(photoCol =>
                    {
                        var photoSlot = photoCol.Item().Height(130).Background(Dark);
                        if (photo != null)
                            photoSlot.Image(photo).FitArea();
                        else
                        {
                            var initial = family.FamilyName.Length > 0
                                ? family.FamilyName[0].ToString() : "?";
                            photoSlot.AlignCenter().AlignMiddle()
                                .Text(initial)
                                .Bold().FontSize(48).FontColor(Mustard);
                        }
                    });

                    // RIGHT: member list
                    row.RelativeItem()
                        .Border(0.5f).BorderColor(Light2).Background(White)
                        .Padding(10).Column(info =>
                        {
                            // Address
                            if (!string.IsNullOrEmpty(family.Address))
                            {
                                info.Item()
                                    .Text(family.Address)
                                    .FontSize(8).FontColor(Grey).Italic();
                                info.Item().Height(4);
                            }

                            // Adults
                            foreach (var a in adults)
                            {
                                info.Item().Row(mRow =>
                                {
                                    mRow.RelativeItem().Column(mc =>
                                    {
                                        mc.Item().Text(a.DisplayName)
                                            .Bold().FontSize(9).FontColor(Dark);
                                        if (!string.IsNullOrEmpty(a.ChurchOffice))
                                            mc.Item().Text(a.ChurchOffice.ToUpper())
                                                .FontSize(7).FontColor(Mustard).Bold();
                                        else if (!string.IsNullOrEmpty(a.MemberStatus))
                                            mc.Item().Text(a.MemberStatus.ToUpper())
                                                .FontSize(7).FontColor(Gold);
                                    });
                                    if (a.ShowPhone && !string.IsNullOrEmpty(a.PhoneNumber))
                                        mRow.ConstantItem(90).AlignRight()
                                            .Text(a.PhoneNumber).FontSize(8).FontColor(Grey);
                                });
                                info.Item().Height(3);
                            }

                            // Children (smaller, italic)
                            if (children.Any())
                            {
                                info.Item().Height(2).Background(Light2);
                                info.Item().PaddingTop(4)
                                    .Text("Children: " + string.Join(", ", children.Select(c => c.Name)))
                                    .FontSize(8).FontColor(Grey).Italic();
                            }

                            // Family phone (if set and different from adults' phones)
                            if (!string.IsNullOrEmpty(family.FamilyPhone))
                            {
                                info.Item().PaddingTop(4)
                                    .Text($"Family: {family.FamilyPhone}")
                                    .FontSize(8).FontColor(Grey);
                            }

                            // Anniversary
                            var annivAdult = adults.FirstOrDefault(a =>
                                a.ShowAnniversary && a.Anniversary.HasValue);
                            if (annivAdult != null)
                            {
                                info.Item().PaddingTop(2)
                                    .Text($"Anniversary: {annivAdult.Anniversary!.Value:MMMM d}")
                                    .FontSize(8).FontColor(Grey);
                            }
                        });
                });
            });
        }

        // ── Individual card: photo on left, details on right ───────────────
        private static void RenderIndividualCard(
            ColumnDescriptor col, Member member, byte[]? photo)
        {
            col.Item().Row(row =>
            {
                // Photo slot
                row.ConstantItem(70).Column(photoCol =>
                {
                    var slot = photoCol.Item().Height(70).Background(Light);
                    if (photo != null)
                        slot.Image(photo).FitArea();
                    else
                    {
                        var initials =
                            (member.Name.Length    > 0 ? member.Name[0].ToString()    : "?") +
                            (member.Surname.Length > 0 ? member.Surname[0].ToString() : "");
                        slot.AlignCenter().AlignMiddle()
                            .Text(initials).Bold().FontSize(20).FontColor(Gold);
                    }
                });

                // Details
                row.RelativeItem()
                    .Border(0.5f).BorderColor(Light2).Background(White)
                    .Padding(8).Column(info =>
                    {
                        info.Item().Text(member.DisplayName)
                            .Bold().FontSize(10).FontColor(Dark);

                        if (!string.IsNullOrEmpty(member.ChurchOffice))
                            info.Item().PaddingTop(2)
                                .Text(member.ChurchOffice.ToUpper())
                                .FontSize(7).FontColor(Mustard).Bold().LetterSpacing(0.06f);
                        else if (!string.IsNullOrEmpty(member.MemberStatus))
                            info.Item().PaddingTop(2)
                                .Text(member.MemberStatus.ToUpper())
                                .FontSize(7).FontColor(Gold).LetterSpacing(0.06f);

                        if (member.ShowPhone && !string.IsNullOrEmpty(member.PhoneNumber))
                            info.Item().PaddingTop(3)
                                .Text(member.PhoneNumber).FontSize(8.5f).FontColor(Grey);

                        if (member.ShowAddress && !string.IsNullOrEmpty(member.Address))
                            info.Item().PaddingTop(2)
                                .Text(member.Address).FontSize(8).FontColor(Grey).Italic();
                    });
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // BIRTHDAY / ANNIVERSARY PAGE
        // ══════════════════════════════════════════════════════════════════════
        private static void AddListPage(
            IDocumentContainer container,
            string        title,
            List<Member>  members,
            Func<Member, string> getDate,
            Func<Member, int>    getMonth)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(14, Unit.Millimetre);
                page.MarginVertical(14,   Unit.Millimetre);
                page.DefaultTextStyle(t =>
                    t.FontFamily("Arial").FontSize(10).FontColor(Dark));

                page.Header().Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        row.RelativeItem()
                            .Text($"Heritage Baptist Church — {title}")
                            .Bold().FontSize(10).FontColor(Dark);
                        row.ConstantItem(100).AlignRight()
                            .Text(DateTime.Today.ToString("d MMM yyyy"))
                            .FontSize(9).FontColor(Gold);
                    });
                    h.Item().PaddingTop(4).Height(2).Background(Mustard);
                    h.Item().Height(8);
                });

                page.Content().Column(col =>
                {
                    int? currentMonth = null;
                    foreach (var m in members)
                    {
                        var month = getMonth(m);
                        if (month != currentMonth)
                        {
                            if (currentMonth != null) col.Item().Height(8);
                            var monthName = new DateTime(2000, month, 1).ToString("MMMM");
                            col.Item().Text(monthName.ToUpper())
                                .Bold().FontSize(9).FontColor(Mustard).LetterSpacing(0.1f);
                            col.Item().Height(2).Background(Light2);
                            col.Item().Height(4);
                            currentMonth = month;
                        }

                        col.Item().Row(row =>
                        {
                            row.RelativeItem()
                                .Text($"{m.Name} {m.Surname}")
                                .FontSize(10).FontColor(Dark);
                            row.ConstantItem(80).AlignRight()
                                .Text(getDate(m))
                                .FontSize(10).FontColor(Gold);
                        });
                        col.Item().Height(1).Background(Light2);
                    }
                });

                page.Footer().Column(f =>
                {
                    f.Item().Height(1).Background(Light2);
                    f.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem()
                            .Text("Heritage Baptist Church Johannesburg")
                            .FontSize(8).FontColor(Gold);
                        row.ConstantItem(80).AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(8).FontColor(Grey);
                            t.CurrentPageNumber().FontSize(8).FontColor(Dark).Bold();
                        });
                    });
                });
            });
        }
    }
}
