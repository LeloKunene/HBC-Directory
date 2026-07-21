using HBCDirectory.Models;
using Xunit;

namespace HBCDirectory.Tests
{
    public class MemberTests
    {
        [Theory]
        [InlineData("Adult", true, false)]
        [InlineData("Child", false, true)]
        public void TypeHelpers_ReflectMemberType(string memberType, bool expectAdult, bool expectChild)
        {
            var m = new Member { MemberType = memberType };

            Assert.Equal(expectAdult, m.IsAdult);
            Assert.Equal(expectChild, m.IsChild);
        }

        [Theory]
        [InlineData("Elder", true)]
        [InlineData("Deacon", true)]
        [InlineData("Member", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public void IsLeadership_OnlyTrueForElderOrDeacon(string? churchOffice, bool expected)
        {
            var m = new Member { ChurchOffice = churchOffice };

            Assert.Equal(expected, m.IsLeadership);
        }

        [Fact]
        public void DisplayName_CombinesNameAndSurname()
        {
            var m = new Member { Name = "Jane", Surname = "Doe" };

            Assert.Equal("Jane Doe", m.DisplayName);
        }

        [Fact]
        public void MemberStatusAndChurchOffice_CanBeNull()
        {
            /*  Children have MemberStatus == null by design. This just locks in that constructing/reading
                a Member in that state doesn't throw — the actual "don't call
                .ToUpper() on a null MemberStatus" guarantee is covered by
                DirectoryPdfServiceTests, since that's where the .ToUpper() calls
                actually live.*/
            var child = new Member { MemberType = "Child", MemberStatus = null, ChurchOffice = null };

            Assert.Null(child.MemberStatus);
            Assert.Null(child.ChurchOffice);
            Assert.False(child.IsLeadership);
        }
    }
}
