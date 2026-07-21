using System.Text.Json;
using HBCDirectory.Models;
using Xunit;

namespace HBCDirectory.Tests
{
    public class PdfSettingsTests
    {
        [Fact]
        public void GetPages_NoRowSaved_ReturnsFourDefaultPagesWithLabels()
        {
            var settings = new PdfSettings(); // PagesJson defaults to DefaultPagesJson

            var pages = settings.GetPages();

            Assert.Equal(4, pages.Count);
            Assert.All(pages, p => Assert.False(string.IsNullOrEmpty(p.Label)));
            Assert.Equal(new[] { "cover", "directory", "birthdays", "anniversaries" },
                pages.Select(p => p.Key));
        }

        [Fact]
        public void GetPages_CamelCaseJson_StillPopulatesMissingLabels()
        {
            // Simulates settings saved by a slightly different client/serializer that emits camelCase property names and omits the label.
            var json = "[{\"key\":\"cover\",\"enabled\":true,\"order\":1}]";
            var settings = new PdfSettings { PagesJson = json };

            var pages = settings.GetPages();

            Assert.Single(pages);
            Assert.Equal("Cover Page", pages[0].Label);
        }

        [Fact]
        public void GetPages_CorruptJson_FallsBackToDefaultWithoutThrowing()
        {
            var settings = new PdfSettings { PagesJson = "{ this is not valid json" };

            var pages = settings.GetPages();

            Assert.Equal(PdfSettings.DefaultPages().Select(p => p.Key),
                pages.Select(p => p.Key));
        }

        [Fact]
        public void GetPages_ReorderedJson_PreservesSavedOrder()
        {
            var reordered = new List<PdfPageConfig>
            {
                new() { Key = "anniversaries", Label = "Anniversary List", Enabled = true, Order = 1 },
                new() { Key = "cover",         Label = "Cover Page",       Enabled = true, Order = 2 },
            };
            var settings = new PdfSettings { PagesJson = JsonSerializer.Serialize(reordered) };

            var pages = settings.GetPages();

            Assert.Equal("anniversaries", pages[0].Key);
            Assert.Equal("cover", pages[1].Key);
        }

        [Fact]
        public void HasPassword_ReflectsWhetherPasswordIsSet()
        {
            var settings = new PdfSettings();
            Assert.False(settings.HasPassword);

            settings.Password = "s3cret";
            Assert.True(settings.HasPassword);

            settings.Password = "";
            Assert.False(settings.HasPassword);
        }
    }
}
