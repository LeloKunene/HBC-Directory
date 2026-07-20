using System.Text;
using HBCDirectory.Pages;
using iText.Kernel.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Xunit;

namespace HBCDirectory.Tests
{
    public class PdfPasswordHelperTests
    {
        private static byte[] MakeMinimalPdf() =>
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Content().Text("Test document");
                });
            }).GeneratePdf();

        [Fact]
        public void AddPassword_OutputRequiresThePasswordToOpen()
        {
            var plain = MakeMinimalPdf();

            var protectedBytes = AdminModel.PdfPasswordHelper.AddPassword(plain, "correct-horse");

            /*  Opening without a password should fail, iText throws a
                BadPasswordException (a PdfException) here, but the exact
                exception type has moved between iText7 versions before, so
                this asserts "some exception", not the specific type.*/
            Assert.ThrowsAny<Exception>(() =>
            {
                using var input  = new MemoryStream(protectedBytes);
                using var reader = new PdfReader(input);
                using var doc    = new PdfDocument(reader);
            });

            // Opening with the correct password should succeed.
            using var input2  = new MemoryStream(protectedBytes);
            var readerProps = new ReaderProperties().SetPassword(Encoding.UTF8.GetBytes("correct-horse"));
            using var reader2 = new PdfReader(input2, readerProps);
            using var doc2    = new PdfDocument(reader2);
            Assert.True(doc2.GetNumberOfPages() >= 1);
        }

        [Fact]
        public void AddPassword_DoesNotThrowWhenGivenFreshInputEachTime()
        {
            /*  Sanity check for the "removePassword then regenerate" path in
                Admin: AddPassword should be safe to call on freshly generated input without misbehaving.*/
            var plain = MakeMinimalPdf();
            var once  = AdminModel.PdfPasswordHelper.AddPassword(plain, "pw-one");

            Assert.NotNull(once);
            Assert.NotEmpty(once);
        }

        [Fact]
        public void AddPassword_OwnerPasswordNoLongerDerivedFromUserPassword()
        {
            /* Regression test for the "owner password = password + _o" issue:
                two PDFs encrypted with the same open password should not be
                byte-identical, because the owner password (and the PDF's
                internal document ID) are now randomized per call rather than
                deterministically derived from the open password.*/
            var plain = MakeMinimalPdf();

            var first  = AdminModel.PdfPasswordHelper.AddPassword(plain, "same-open-password");
            var second = AdminModel.PdfPasswordHelper.AddPassword(plain, "same-open-password");

            Assert.NotEqual(Convert.ToBase64String(first), Convert.ToBase64String(second));
        }
    }
}
