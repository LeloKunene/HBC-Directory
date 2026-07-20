# HBCDirectory.Tests

Run with:
```
dotnet test Tests/HBCDirectory.Tests/HBCDirectory.Tests.csproj
```

## What's covered

These are unit tests against the pure-logic pieces flagged in the pre-ship
review, no database, no R2, no HTTP calls happen in this suite:

- **`PdfSettingsTests`** : `PdfSettings.GetPages()`: default pages when
  nothing's saved, camelCase JSON with missing labels, corrupt JSON falling
  back to defaults, and that a saved page order is preserved.
- **`MemberTests`** : `Member`'s computed properties (`IsAdult`, `IsChild`,
  `IsLeadership`, `DisplayName`), including that a `Child` with a null
  `MemberStatus`/`ChurchOffice` behaves correctly (children have no status
  by design, see the comment on the `Member` class).
- **`PdfPasswordHelperTests`** : the encrypted PDF actually requires the
  password to open, and the owner password is no longer deterministically
  derived from the open password (regression test for the `password + "_o"`
  issue).
- **`DirectoryPdfServiceTests`** : `GenerateAsync` doesn't throw with no
  members, with families that have no photos, with a member whose
  `MemberStatus`/`ChurchOffice` are both null (regression test for the
  `.ToUpper()` null-safety concern, every call site already guards with
  `IsNullOrEmpty`, this just locks it in), and with a page disabled via
  `PdfSettings`.

## What's intentionally not covered here

The original review also called for authentication flow tests (login
success/failure, unauthenticated redirects, member-vs-admin access),
member/family CRUD tests, the approval queue, and photo upload validation.
Those all go through Razor Page handlers that need a real HTTP pipeline and
a database — `WebApplicationFactory<Program>` plus either a Postgres test
container or a temporary SQLite/Npgsql-compatible database.

That's a reasonable next slice of work, but it's a different shape of test
(integration, not unit) and needs infrastructure this sandbox doesn't have
available to actually run and verify. Rather than hand you untested
integration test code, this commit sticks to what could actually be
exercised. A reasonable next step:

1. Add `Microsoft.AspNetCore.Mvc.Testing` to this project.
2. Add a `public partial class Program { }` marker at the bottom of
   `Program.cs` (needed because it uses top-level statements —
   `WebApplicationFactory<Program>` can't see the implicit `Program` class
   without it).
3. Point the factory's `DirectoryContext` registration at a throwaway
   database per test run (a local Postgres via Docker, or Testcontainers).
4. Work through the "Authentication", "Member management", and "Approval
   queue" bullets from the review as `WebApplicationFactory`-based tests.
