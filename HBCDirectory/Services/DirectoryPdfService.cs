using HBCDirectory.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HBCDirectory.Services
{
    public class DirectoryPdfService
    {
        private readonly PhotoService _photos;
        private readonly HttpClient  _http;

        private const string Dark    = "#202222";
        private const string Gold    = "#9a865f";
        private const string Mustard = "#c69760";
        private const string Light   = "#f5f1eb";
        private const string Light2  = "#ede8df";
        private const string White   = "#ffffff";
        private const string Grey    = "#888888";

        public DirectoryPdfService(PhotoService photos, IHttpClientFactory httpClientFactory)
        {
            _photos = photos;
            _http   = httpClientFactory.CreateClient();
        }

        private async Task<byte[]?> FetchPhotoAsync(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            try
            {
                var url      = _photos.Url(fileName);
                var response = await _http.GetAsync(url);
                return response.IsSuccessStatusCode
                    ? await response.Content.ReadAsByteArrayAsync()
                    : null;
            }
            catch { return null; }
        }

        public async Task<byte[]> GenerateAsync(List<Family> families, List<Member> unassigned)
        {
            // ── Fetch all photos concurrently ─────────────────────────────────
            var sortedFamilies = families.OrderBy(f => f.FamilyName).ToList();
            var allMembers     = sortedFamilies.SelectMany(f => f.Members)
                                               .Concat(unassigned).ToList();

            var memberPhotoTasks = allMembers.ToDictionary(
                m => m.Id, m => FetchPhotoAsync(m.PhotoFileName));
            var familyPhotoTasks = sortedFamilies
                .Where(f => !string.IsNullOrEmpty(f.PhotoFileName))
                .ToDictionary(f => f.Id, f => FetchPhotoAsync(f.PhotoFileName));

            await Task.WhenAll(
                memberPhotoTasks.Values.Concat(familyPhotoTasks.Values));

            var memberPhotos = memberPhotoTasks
                .ToDictionary(kv => kv.Key, kv => kv.Value.Result);
            var familyPhotos = familyPhotoTasks
                .ToDictionary(kv => kv.Key, kv => kv.Value.Result);

            // Birthday / anniversary lists (built once, reused in two pages)
            var byBirthday = allMembers
                .Where(m => m.Birthdate.HasValue && m.ShowBirthdate)
                .OrderBy(m => m.Birthdate!.Value.Month)
                .ThenBy(m => m.Birthdate!.Value.Day)
                .ToList();

            var byAnniversary = allMembers
                .Where(m => m.Anniversary.HasValue && m.ShowAnniversary)
                .OrderBy(m => m.Anniversary!.Value.Month)
                .ThenBy(m => m.Anniversary!.Value.Day)
                .ToList();

            return Document.Create(container =>
            {
                // ════════════════════════════════════════════════════════════
                // COVER PAGE
                // Two-tone layout without Extend() — page background provides
                // the bottom colour; the top section sizes itself to content.
                // ════════════════════════════════════════════════════════════
                container.Page(cover =>
                {
                    cover.Size(PageSizes.A4);
                    cover.Margin(0);
                    cover.Background(Light); // bottom half colour

                    cover.Content().Column(col =>
                    {
                        // ── Dark top section (content-sized, no fixed height) ──
                        col.Item()
                            .Background(Dark)
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

                                // Decorative bar — use Row with ConstantItem
                                // (Width() on a Column item can conflict in
                                // QuestPDF 2024+; ConstantItem in a Row is safe)
                                inner.Item().PaddingTop(20).Row(bar =>
                                {
                                    bar.ConstantItem(60).Height(3).Background(Mustard);
                                    bar.RelativeItem();
                                });

                                inner.Item().PaddingTop(16)
                                    .Text($"Generated {DateTime.Today:MMMM d, yyyy}")
                                    .FontFamily("Arial").FontSize(11).FontColor(Gold);
                            });

                        // ── Light bottom section ──────────────────────────────
                        col.Item()
                            .PaddingTop(48).PaddingHorizontal(50).PaddingBottom(48)
                            .Column(stats =>
                            {
                                var totalMembers  = allMembers.Count;
                                var totalFamilies = sortedFamilies.Count(f => f.Members.Any());

                                stats.Item()
                                    .Text("DIRECTORY CONTENTS")
                                    .FontFamily("Arial").FontSize(9)
                                    .FontColor(Gold).Bold().LetterSpacing(0.12f);

                                stats.Item().PaddingTop(16).Row(row =>
                                {
                                    row.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text(totalMembers.ToString())
                                            .FontFamily("Arial").FontSize(36).Bold().FontColor(Dark);
                                        c.Item().Text("Members")
                                            .FontFamily("Arial").FontSize(10).FontColor(Gold);
                                    });
                                    row.ConstantItem(1).Background(Light2);
                                    row.RelativeItem().PaddingLeft(28).Column(c =>
                                    {
                                        c.Item().Text(totalFamilies.ToString())
                                            .FontFamily("Arial").FontSize(36).Bold().FontColor(Dark);
                                        c.Item().Text("Families")
                                            .FontFamily("Arial").FontSize(10).FontColor(Gold);
                                    });
                                    row.ConstantItem(1).Background(Light2);
                                    row.RelativeItem().PaddingLeft(28).Column(c =>
                                    {
                                        c.Item().Text(unassigned.Count.ToString())
                                            .FontFamily("Arial").FontSize(36).Bold().FontColor(Dark);
                                        c.Item().Text("Individual Members")
                                            .FontFamily("Arial").FontSize(10).FontColor(Gold);
                                    });
                                });

                                stats.Item().PaddingTop(32)
                                    .Text("Families listed alphabetically")
                                    .FontFamily("Arial").FontSize(9).FontColor(Grey).Italic();
                            });
                    });
                });

                // ════════════════════════════════════════════════════════════
                // DIRECTORY PAGES
                // ════════════════════════════════════════════════════════════
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginTop(14,   Unit.Millimetre);
                    page.MarginBottom(14, Unit.Millimetre);
                    page.MarginHorizontal(14, Unit.Millimetre);
                    page.DefaultTextStyle(t =>
                        t.FontFamily("Arial").FontSize(9).FontColor(Dark));

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
                        if (!sortedFamilies.Any() && !unassigned.Any())
                        {
                            col.Item().PaddingTop(40).AlignCenter()
                                .Text("No members in directory yet.")
                                .FontSize(12).FontColor(Grey).Italic();
                            return;
                        }

                        foreach (var family in sortedFamilies)
                        {
                            var members = family.Members
                                .OrderBy(m => m.MemberType)  // Adults before children
                                .ThenBy(m => m.Surname)
                                .ThenBy(m => m.Name)
                                .ToList();
                            if (!members.Any()) continue;

                            RenderFamilySection(col, family, members,
                                memberPhotos, familyPhotos);
                            col.Item().Height(16);
                        }

                        if (unassigned.Any())
                            RenderUnassignedSection(col, unassigned, memberPhotos);
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

                // ════════════════════════════════════════════════════════════
                // BIRTHDAY PAGE (only when there is data)
                // ════════════════════════════════════════════════════════════
                if (byBirthday.Any())
                    RenderListPage(container, "Birthdays",
                        byBirthday,
                        m => m.Birthdate!.Value.ToString("MMMM d"),
                        m => m.Birthdate!.Value.Month);

                // ════════════════════════════════════════════════════════════
                // ANNIVERSARY PAGE (only when there is data)
                // ════════════════════════════════════════════════════════════
                if (byAnniversary.Any())
                    RenderListPage(container, "Anniversaries",
                        byAnniversary,
                        m => m.Anniversary!.Value.ToString("MMMM d"),
                        m => m.Anniversary!.Value.Month);

            }).GeneratePdf();
        }

        // ── Birthday / Anniversary page ───────────────────────────────────────
        // Extracted so both pages share the same layout template.
        private static void RenderListPage(
            IDocumentContainer container,
            string title,
            List<Member> members,
            Func<Member, string> getDate,
            Func<Member, int>    getMonth)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(14, Unit.Millimetre);
                page.MarginVertical(14,   Unit.Millimetre);
                page.DefaultTextStyle(t =>
                    t.FontFamily("Arial").FontSize(10).FontColor("#202222"));

                page.Header().Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        row.RelativeItem()
                            .Text($"Heritage Baptist Church — {title}")
                            .Bold().FontSize(10).FontColor("#202222");
                        row.ConstantItem(100).AlignRight()
                            .Text(DateTime.Today.ToString("d MMM yyyy"))
                            .FontSize(9).FontColor("#9a865f");
                    });
                    h.Item().PaddingTop(4).Height(2).Background("#c69760");
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
                            col.Item()
                                .Text(monthName.ToUpper())
                                .Bold().FontSize(9).FontColor("#c69760")
                                .LetterSpacing(0.1f);
                            col.Item().Height(2).Background("#ede8df");
                            col.Item().Height(4);
                            currentMonth = month;
                        }

                        col.Item().Row(row =>
                        {
                            row.RelativeItem()
                                .Text($"{m.Name} {m.Surname}")
                                .FontSize(10).FontColor("#202222");
                            row.ConstantItem(80).AlignRight()
                                .Text(getDate(m))
                                .FontSize(10).FontColor("#9a865f");
                        });
                        col.Item().Height(1).Background("#f5f1eb");
                    }
                });

                page.Footer().Column(f =>
                {
                    f.Item().Height(1).Background("#ede8df");
                    f.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem()
                            .Text("Heritage Baptist Church Johannesburg")
                            .FontSize(8).FontColor("#9a865f");
                        row.ConstantItem(80).AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(8).FontColor("#888");
                            t.CurrentPageNumber().FontSize(8).FontColor("#202222").Bold();
                        });
                    });
                });
            });
        }

        // ── Family section ────────────────────────────────────────────────────
        private void RenderFamilySection(
            ColumnDescriptor col,
            Family family,
            List<Member> members,
            Dictionary<int, byte[]?> memberPhotos,
            Dictionary<int, byte[]?> familyPhotos)
        {
            col.Item().Column(section =>
            {
                // Section header — coloured left bar + family name
                section.Item().Row(row =>
                {
                    row.ConstantItem(4).Background(Mustard);
                    row.RelativeItem()
                        .Background(Light)
                        .PaddingVertical(8).PaddingLeft(12)
                        .Column(c =>
                        {
                            c.Item()
                                .Text(family.FamilyName.ToUpper())
                                .Bold().FontSize(11).FontColor(Dark)
                                .LetterSpacing(0.08f);
                            c.Item()
                                .Text($"{members.Count} " +
                                      $"{(members.Count == 1 ? "member" : "members")}")
                                .FontSize(8).FontColor(Gold);
                        });
                });

                // Optional family group photo
                if (familyPhotos.TryGetValue(family.Id, out var familyPhoto)
                    && familyPhoto != null)
                {
                    section.Item().PaddingTop(6)
                        .MaxHeight(100)
                        .Image(familyPhoto).FitArea();
                }

                // Member grid — 4 columns via Table
                section.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(); c.RelativeColumn();
                        c.RelativeColumn(); c.RelativeColumn();
                    });

                    foreach (var member in members)
                    {
                        memberPhotos.TryGetValue(member.Id, out var photo);
                        var m = member;
                        var p = photo;
                        table.Cell().Padding(3)
                            .Element(c => RenderMemberTile(c, m, p));
                    }

                    // Pad to complete the last row — avoids Table layout issues
                    int rem = members.Count % 4;
                    if (rem != 0)
                        for (int i = 0; i < 4 - rem; i++)
                            table.Cell();
                });
            });
        }

        // ── Individual members section ────────────────────────────────────────
        private void RenderUnassignedSection(
            ColumnDescriptor col,
            List<Member> members,
            Dictionary<int, byte[]?> memberPhotos)
        {
            col.Item().Column(section =>
            {
                section.Item().Row(row =>
                {
                    row.ConstantItem(4).Background(Gold);
                    row.RelativeItem()
                        .Background(Light)
                        .PaddingVertical(8).PaddingLeft(12)
                        .Column(c =>
                        {
                            c.Item()
                                .Text("INDIVIDUAL MEMBERS")
                                .Bold().FontSize(11).FontColor(Dark)
                                .LetterSpacing(0.08f);
                            c.Item()
                                .Text($"{members.Count} " +
                                      $"{(members.Count == 1 ? "member" : "members")}")
                                .FontSize(8).FontColor(Gold);
                        });
                });

                var sorted = members.OrderBy(m => m.Surname).ThenBy(m => m.Name).ToList();

                section.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(); c.RelativeColumn();
                        c.RelativeColumn(); c.RelativeColumn();
                    });

                    foreach (var member in sorted)
                    {
                        memberPhotos.TryGetValue(member.Id, out var photo);
                        var m = member;
                        var p = photo;
                        table.Cell().Padding(3)
                            .Element(c => RenderMemberTile(c, m, p));
                    }

                    int rem = sorted.Count % 4;
                    if (rem != 0)
                        for (int i = 0; i < 4 - rem; i++)
                            table.Cell();
                });
            });
        }

        // ── Member tile ───────────────────────────────────────────────────────
        // Rules for modern QuestPDF (2024+):
        //   • Set Width/Height constraints only on the OUTERMOST container for
        //     each dimension. Never set the same axis twice in a chain.
        //   • Prefer AlignCenter/AlignMiddle on the slot BEFORE calling Image/Text.
        private void RenderMemberTile(IContainer container, Member member, byte[]? photo)
        {
            container
                .Border(0.5f).BorderColor(Light2)
                .Background(White)
                .Column(tile =>
                {
                    // Photo / initials slot.
                    // KEY RULE for QuestPDF 2024+:
                    //   Image + FitArea() resolves its own size from the container —
                    //   do NOT wrap it in AlignCenter/AlignMiddle (circular dependency).
                    //   Text has a fixed natural size so AlignCenter/AlignMiddle IS safe.
                    var slotBase = tile.Item().Height(90).Background(Light);

                    if (photo != null)
                    {
                        // Fill the slot — no alignment wrapper needed for images
                        slotBase.Image(photo).FitArea();
                    }
                    else
                    {
                        // Text has a known size → alignment is safe
                        var initials =
                            (member.Name.Length  > 0 ? member.Name[0].ToString()    : "?") +
                            (member.Surname.Length > 0 ? member.Surname[0].ToString() : "");
                        slotBase.AlignCenter().AlignMiddle()
                            .Text(initials)
                            .Bold().FontSize(24).FontColor(Mustard);
                    }

                    // Thin separator
                    tile.Item().Height(1).Background(Light2);

                    // Name / role / phone block — flexible height, no constraints
                    tile.Item().Padding(6).Column(info =>
                    {
                        info.Item()
                            .Text($"{member.Name} {member.Surname}")
                            .Bold().FontSize(8.5f).FontColor(Dark).LineHeight(1.25f);

                        if (!string.IsNullOrEmpty(member.ChurchOffice))
                            info.Item().PaddingTop(2)
                                .Text(member.ChurchOffice.ToUpper())
                                .FontSize(7).FontColor(Mustard).Bold()
                                .LetterSpacing(0.06f);

                        if (!string.IsNullOrEmpty(member.MemberStatus)
                            && string.IsNullOrEmpty(member.ChurchOffice))
                            info.Item().PaddingTop(2)
                                .Text(member.MemberStatus.ToUpper())
                                .FontSize(7).FontColor(Gold).LetterSpacing(0.06f);

                        if (!string.IsNullOrEmpty(member.PhoneNumber))
                            info.Item().PaddingTop(2)
                                .Text(member.PhoneNumber)
                                .FontSize(7.5f).FontColor(Grey);
                    });
                });
        }
    }
}