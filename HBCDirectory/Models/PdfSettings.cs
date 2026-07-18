using System.Text.Json;
using System.Text.Json.Serialization;

namespace HBCDirectory.Models
{
    public class PdfSettings
    {
        public int Id { get; set; } = 1;
        public DateTime? LastGenerated { get; set; }
        public string? R2Key    { get; set; }   // R2 object key for the cached PDF
        public string? Password { get; set; }   // null = no protection
        [JsonIgnore] public bool HasPassword => !string.IsNullOrEmpty(Password);
        public string PagesJson { get; set; } = DefaultPagesJson;
                public static readonly Dictionary<string, string> KeyLabels = new()
        {
            ["cover"]         = "Cover Page",
            ["directory"]     = "Member Directory",
            ["birthdays"]     = "Birthday List",
            ["anniversaries"] = "Anniversary List",
        };

        public List<PdfPageConfig> GetPages()
        {
            try
            {
                var opts  = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var pages = JsonSerializer.Deserialize<List<PdfPageConfig>>(PagesJson, opts)
                            ?? DefaultPages();
                foreach (var p in pages)
                    if (string.IsNullOrEmpty(p.Label) && KeyLabels.TryGetValue(p.Key, out var lbl))
                        p.Label = lbl;

                return pages;
            }
            catch { return DefaultPages(); }
        }

        public static List<PdfPageConfig> DefaultPages() => new()
        {
            new() { Key = "cover",         Label = "Cover Page",       Enabled = true, Order = 1 },
            new() { Key = "directory",     Label = "Member Directory", Enabled = true, Order = 2 },
            new() { Key = "birthdays",     Label = "Birthday List",    Enabled = true, Order = 3 },
            new() { Key = "anniversaries", Label = "Anniversary List", Enabled = true, Order = 4 },
        };

        public static readonly string DefaultPagesJson =
            JsonSerializer.Serialize(DefaultPages());
    }

    public class PdfPageConfig
    {
        public string Key     { get; set; } = string.Empty;
        public string Label   { get; set; } = string.Empty;
        public bool   Enabled { get; set; } = true;
        public int    Order   { get; set; } = 1;
    }
}
