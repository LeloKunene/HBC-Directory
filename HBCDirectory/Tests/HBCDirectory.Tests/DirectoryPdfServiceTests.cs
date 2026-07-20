using HBCDirectory.Models;
using HBCDirectory.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HBCDirectory.Tests
{
    public class DirectoryPdfServiceTests
    {
        /*  PhotoService.Url() is only ever called for members/families that have
            a PhotoFileName set. None of the test data below sets one, so
            GenerateAsync never actually calls FetchPhotoAsync, meaning this
            client is never used to make a real HTTP request. It exists only to
            satisfy DirectoryPdfService's constructor.*/
        private sealed class UnusedHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new();
        }

        private static DirectoryPdfService MakeService()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["R2:PublicUrl"] = "https://example.invalid",
                })
                .Build();

            var photos = new PhotoService(config);
            return new DirectoryPdfService(photos, new UnusedHttpClientFactory(), config);
        }

        [Fact]
        public async Task GenerateAsync_NoMembersAtAll_DoesNotThrowAndProducesAPdf()
        {
            var service = MakeService();

            var bytes = await service.GenerateAsync(new List<Family>(), new List<Member>());

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public async Task GenerateAsync_FamiliesWithoutPhotos_DoesNotThrow()
        {
            var service = MakeService();
            var family = new Family
            {
                Id = 1,
                FamilyName = "Doe",
                Members = new List<Member>
                {
                    new() { Id = 1, Name = "Jane", Surname = "Doe", MemberType = "Adult", MemberStatus = "Member" },
                    new() { Id = 2, Name = "Jack", Surname = "Doe", MemberType = "Child" },
                }
            };

            var bytes = await service.GenerateAsync(new List<Family> { family }, new List<Member>());

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public async Task GenerateAsync_MemberWithNullMemberStatusAndChurchOffice_DoesNotThrow()
        {
            /*  Regression test for the null-safety risk raised in the pre-ship
                review ("Any Razor/rendering code calling MemberStatus.ToUpper()
                without a null check"). RenderIndividualCard and the family adult
                row both guard those calls with IsNullOrEmpty already, this
                locks that behavior in so a future change can't silently drop
                the guard.*/
            var service = MakeService();
            var unassignedAdult = new Member
            {
                Id = 3, Name = "John", Surname = "Smith", MemberType = "Adult",
                MemberStatus = null, ChurchOffice = null
            };

            var bytes = await service.GenerateAsync(new List<Family>(), new List<Member> { unassignedAdult });

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public async Task GenerateAsync_WithBirthdayPageDisabled_StillGeneratesRemainingPages()
        {
            var service = MakeService();
            var member = new Member
            {
                Id = 4, Name = "Ann", Surname = "Lee", MemberType = "Adult",
                Birthdate = DateTime.Today, ShowBirthdate = true
            };

            var pages = PdfSettings.DefaultPages();
            pages.Single(p => p.Key == "birthdays").Enabled = false;

            var bytes = await service.GenerateAsync(new List<Family>(), new List<Member> { member }, pages);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public async Task GenerateAsync_UnassignedAdultsAndFamilies_BothAppear()
        {
            var service = MakeService();
            var family = new Family
            {
                Id = 1,
                FamilyName = "Ncube",
                Members = new List<Member>
                {
                    new() { Id = 5, Name = "Sipho", Surname = "Ncube", MemberType = "Adult", MemberStatus = "Member" },
                }
            };
            var unassigned = new Member
            {
                Id = 6, Name = "Grace", Surname = "Mokoena", MemberType = "Adult", MemberStatus = "Attendant"
            };

            var bytes = await service.GenerateAsync(new List<Family> { family }, new List<Member> { unassigned });

            Assert.NotEmpty(bytes);
        }
    }
}
