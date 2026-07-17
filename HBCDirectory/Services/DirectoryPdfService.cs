using HBCDirectory.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HBCDirectory.Services
{
    public class DirectoryPdfService
    {
        private readonly PhotoService _photos;
        private readonly HttpClient _http;

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
            var sortedFamilies = families.OrderBy(f => f.FamilyName).ToList();

            var allMembers = sortedFamilies.SelectMany(f => f.Members).Concat(unassigned).ToList();

            var memberPhotoTasks = allMembers.ToDictionary(
                m => m.Id,
                m => FetchPhotoAsync(m.PhotoFileName));

            var familyPhotoTasks = sortedFamilies
                .Where(f => !string.IsNullOrEmpty(f.PhotoFileName))
                .ToDictionary(f => f.Id, f => FetchPhotoAsync(f.PhotoFileName));

            await Task.WhenAll(memberPhotoTasks.Values.Concat(familyPhotoTasks.Values));

            var memberPhotos = memberPhotoTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);
            var familyPhotos = familyPhotoTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);

            return Document.Create(container =>
            {
                //  Cover page 
                container.Page(cover =>
                {
                    cover.Size(PageSizes.A4);
                    cover.Margin(0);

                    cover.Content().Column(col =>
                    {
                        col.Item().Height(380).Background(Dark).Column(top =>
                        {
                            top.Item().PaddingTop(80).PaddingHorizontal(50).Column(inner =>
                            {
                                inner.Item()
                                    .Text("HERITAGE BAPTIST CHURCH")
                                    .FontFamily("Arial").FontSize(13)
                                    .FontColor(Mustard).Bold().LetterSpacing(0.15f);

                                inner.Item().PaddingTop(12)
                                    .Text("Member\nDirectory")
                                    .FontFamily("Arial").FontSize(52)
                                    .FontColor(White).Bold().LineHeight(1.1f);

                                inner.Item().PaddingTop(16).Width(60).Height(3).Background(Mustard);

                                inner.Item().PaddingTop(16)
                                    .Text($"Generated {DateTime.Today:MMMM d, yyyy}")
                                    .FontFamily("Arial").FontSize(11).FontColor(Gold);
                            });
                        });

                        col.Item().Extend().Background(Light).Column(bottom =>
                        {
                            bottom.Item().PaddingTop(50).PaddingHorizontal(50).Column(stats =>
                            {
                                var totalMembers  = sortedFamilies.Sum(f => f.Members.Count) + unassigned.Count;
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
                                    row.RelativeItem().PaddingLeft(30).Column(c =>
                                    {
                                        c.Item().Text(totalFamilies.ToString())
                                            .FontFamily("Arial").FontSize(36).Bold().FontColor(Dark);
                                        c.Item().Text("Families")
                                            .FontFamily("Arial").FontSize(10).FontColor(Gold);
                                    });
                                    row.ConstantItem(1).Background(Light2);
                                    row.RelativeItem().PaddingLeft(30).Column(c =>
                                    {
                                        c.Item().Text(unassigned.Count.ToString())
                                            .FontFamily("Arial").FontSize(36).Bold().FontColor(Dark);
                                        c.Item().Text("Individual Members")
                                            .FontFamily("Arial").FontSize(10).FontColor(Gold);
                                    });
                                });

                                stats.Item().PaddingTop(40)
                                    .Text("Families listed alphabetically")
                                    .FontFamily("Arial").FontSize(9).FontColor(Grey).Italic();
                            });
                        });
                    });
                });

                //  Directory pages 
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginTop(14, Unit.Millimetre);
                    page.MarginBottom(14, Unit.Millimetre);
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
                        h.Item().PaddingTop(4).Height(1.5f).Background(Mustard);
                        h.Item().Height(8);
                    });

                    page.Content().Column(col =>
                    {
                        foreach (var family in sortedFamilies)
                        {
                            var members = family.Members
                                .OrderBy(m => m.Surname)
                                .ThenBy(m => m.Name)
                                .ToList();

                            if (!members.Any()) continue;

                            RenderFamilySection(col, family, members, memberPhotos, familyPhotos);
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

                container.Page(bdPage =>
                {
                    bdPage.Size(PageSizes.A4);
                    bdPage.MarginHorizontal(14, Unit.Millimetre);
                    bdPage.MarginVertical(14, Unit.Millimetre);
                    bdPage.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10).FontColor("#202222"));

                    bdPage.Header().Column(h =>
                    {
                        h.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Heritage Baptist Church — Birthdays")
                                .Bold().FontSize(10).FontColor("#202222");
                            row.ConstantItem(100).AlignRight()
                                .Text(DateTime.Today.ToString("d MMM yyyy"))
                                .FontSize(9).FontColor("#9a865f");
                        });
                        h.Item().PaddingTop(4).Height(1.5f).Background("#c69760");
                        h.Item().Height(8);
                    });

                    bdPage.Content().Column(col =>
                    {
                        var byBirthday = allMembers
                            .Where(m => m.Birthdate.HasValue && m.ShowBirthdate)
                            .OrderBy(m => m.Birthdate!.Value.Month)
                            .ThenBy(m => m.Birthdate!.Value.Day)
                            .ToList();

                        string? currentMonth = null;
                        foreach (var m in byBirthday)
                        {
                            var month = m.Birthdate!.Value.ToString("MMMM");
                            if (month != currentMonth)
                            {
                                if (currentMonth != null) col.Item().Height(8);
                                col.Item().Text(month.ToUpper())
                                    .Bold().FontSize(9).FontColor("#c69760")
                                    .LetterSpacing(0.1f);
                                col.Item().Height(1.5f).Background("#ede8df");
                                col.Item().Height(4);
                                currentMonth = month;
                            }

                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{m.Name} {m.Surname}")
                                    .FontSize(10).FontColor("#202222");
                                row.ConstantItem(60).AlignRight()
                                    .Text(m.Birthdate!.Value.ToString("MMMM d"))
                                    .FontSize(10).FontColor("#9a865f");
                            });
                            col.Item().Height(0.5f).Background("#f5f1eb");
                        }

                        if (!byBirthday.Any())
                            col.Item().Text("No birthdays on file.").FontSize(10).FontColor("#888");
                    });

                    bdPage.Footer().Column(f =>
                    {
                        f.Item().Height(1).Background("#ede8df");
                        f.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text("Heritage Baptist Church Johannesburg")
                                .FontSize(8).FontColor("#9a865f");
                            row.ConstantItem(80).AlignRight().Text(t =>
                            {
                                t.Span("Page ").FontSize(8).FontColor("#888");
                                t.CurrentPageNumber().FontSize(8).FontColor("#202222").Bold();
                            });
                        });
                    });
                });

                // ── Anniversary page ────────────────────────────────────────────────────────
                container.Page(annivPage =>
                {
                    annivPage.Size(PageSizes.A4);
                    annivPage.MarginHorizontal(14, Unit.Millimetre);
                    annivPage.MarginVertical(14, Unit.Millimetre);
                    annivPage.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10).FontColor("#202222"));

                    annivPage.Header().Column(h =>
                    {
                        h.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Heritage Baptist Church — Anniversaries")
                                .Bold().FontSize(10).FontColor("#202222");
                            row.ConstantItem(100).AlignRight()
                                .Text(DateTime.Today.ToString("d MMM yyyy"))
                                .FontSize(9).FontColor("#9a865f");
                        });
                        h.Item().PaddingTop(4).Height(1.5f).Background("#c69760");
                        h.Item().Height(8);
                    });

                    annivPage.Content().Column(col =>
                    {
                        var byAnniversary = allMembers
                            .Where(m => m.Anniversary.HasValue && m.ShowAnniversary)
                            .OrderBy(m => m.Anniversary!.Value.Month)
                            .ThenBy(m => m.Anniversary!.Value.Day)
                            .ToList();

                        string? currentMonth = null;
                        foreach (var m in byAnniversary)
                        {
                            var month = m.Anniversary!.Value.ToString("MMMM");
                            if (month != currentMonth)
                            {
                                if (currentMonth != null) col.Item().Height(8);
                                col.Item().Text(month.ToUpper())
                                    .Bold().FontSize(9).FontColor("#c69760")
                                    .LetterSpacing(0.1f);
                                col.Item().Height(1.5f).Background("#ede8df");
                                col.Item().Height(4);
                                currentMonth = month;
                            }

                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{m.Name} {m.Surname}")
                                    .FontSize(10).FontColor("#202222");
                                row.ConstantItem(60).AlignRight()
                                    .Text(m.Anniversary!.Value.ToString("MMMM d"))
                                    .FontSize(10).FontColor("#9a865f");
                            });
                            col.Item().Height(0.5f).Background("#f5f1eb");
                        }

                        if (!byAnniversary.Any())
                            col.Item().Text("No anniversaries on file.").FontSize(10).FontColor("#888");
                    });

                    annivPage.Footer().Column(f =>
                    {
                        f.Item().Height(1).Background("#ede8df");
                        f.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text("Heritage Baptist Church Johannesburg")
                                .FontSize(8).FontColor("#9a865f");
                            row.ConstantItem(80).AlignRight().Text(t =>
                            {
                                t.Span("Page ").FontSize(8).FontColor("#888");
                                t.CurrentPageNumber().FontSize(8).FontColor("#202222").Bold();
                            });
                        });
                    });
                });
            }).GeneratePdf();
        }

        private void RenderFamilySection(
            ColumnDescriptor col,
            Family family,
            List<Member> members,
            Dictionary<int, byte[]?> memberPhotos,
            Dictionary<int, byte[]?> familyPhotos)
        {
            col.Item().Column(section =>
            {
                section.Item().Row(row =>
                {
                    row.ConstantItem(4).Background(Mustard);
                    row.RelativeItem().Background(Light).PaddingVertical(8).PaddingLeft(12).Row(inner =>
                    {
                        inner.RelativeItem().Column(c =>
                        {
                            c.Item()
                                .Text(family.FamilyName.ToUpper())
                                .Bold().FontSize(11).FontColor(Dark).LetterSpacing(0.08f);
                            c.Item()
                                .Text($"{members.Count} {(members.Count == 1 ? "member" : "members")}")
                                .FontSize(8).FontColor(Gold);
                        });
                    });
                });

                if (familyPhotos.TryGetValue(family.Id, out var familyPhoto) && familyPhoto != null)
                    section.Item().PaddingTop(6).MaxHeight(100).Image(familyPhoto).FitWidth();

                section.Item().PaddingTop(8).Grid(grid =>
                {
                    grid.Columns(4);
                    grid.Spacing(8);
                    foreach (var member in members)
                    {
                        memberPhotos.TryGetValue(member.Id, out var photo);
                        RenderMemberTile(grid.Item(), member, photo);
                    }
                });
            });
        }

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
                    row.RelativeItem().Background(Light).PaddingVertical(8).PaddingLeft(12).Column(c =>
                    {
                        c.Item().Text("INDIVIDUAL MEMBERS").Bold().FontSize(11).FontColor(Dark).LetterSpacing(0.08f);
                        c.Item().Text($"{members.Count} {(members.Count == 1 ? "member" : "members")}").FontSize(8).FontColor(Gold);
                    });
                });

                section.Item().PaddingTop(8).Grid(grid =>
                {
                    grid.Columns(4);
                    grid.Spacing(8);
                    foreach (var member in members.OrderBy(m => m.Surname).ThenBy(m => m.Name))
                    {
                        memberPhotos.TryGetValue(member.Id, out var photo);
                        RenderMemberTile(grid.Item(), member, photo);
                    }
                });
            });
        }

        private void RenderMemberTile(IContainer container, Member member, byte[]? photo)
        {
            container
                .Border(0.5f).BorderColor(Light2)
                .Background(White)
                .Column(tile =>
                {
                    tile.Item()
                        .Height(90)
                        .Background(Light)
                        .AlignCenter()
                        .AlignMiddle()
                        .Element(img =>
                        {
                            if (photo != null)
                            {
                                img.Width(90).Height(90).Image(photo).FitArea();
                            }
                            else
                            {
                                img.Width(90).Height(90)
                                    .AlignCenter().AlignMiddle()
                                    .Text($"{member.Name[0]}{member.Surname[0]}")
                                    .Bold().FontSize(26).FontColor(Mustard);
                            }
                        });

                    tile.Item().Height(1).Background(Light2);

                    tile.Item().Padding(7).Column(info =>
                    {
                        info.Item()
                            .Text($"{member.Name} {member.Surname}")
                            .Bold().FontSize(8.5f).FontColor(Dark).LineHeight(1.2f);

                        if (!string.IsNullOrEmpty(member.ChurchOffice))
                            info.Item().PaddingTop(2)
                                .Text(member.ChurchOffice.ToUpper())
                                .FontSize(7).FontColor(Mustard).Bold().LetterSpacing(0.06f);

                        if (!string.IsNullOrEmpty(member.PhoneNumber))
                            info.Item().PaddingTop(2)
                                .Text(member.PhoneNumber)
                                .FontSize(7.5f).FontColor(Grey);
                    });
                });
        }
    }
}