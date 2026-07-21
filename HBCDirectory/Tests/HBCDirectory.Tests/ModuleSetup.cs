using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace HBCDirectory.Tests
{
    /*  QuestPDF.Settings.License has to be set before any Document.GeneratePdf()
        call happens anywhere in the process, same as Program.cs does at startup.
        A module initializer runs once when this test assembly loads, before any
        test executes, regardless of run order.*/
    internal static class ModuleSetup
    {
        [ModuleInitializer]
        internal static void Init()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }
    }
}
