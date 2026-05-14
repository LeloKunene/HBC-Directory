# HBC Directory - A private church member directory built with ASP.Net Core Razor Pages and SQLite

## Tech Stack

| Layer     | Technology                                |
| --------- | ----------------------------------------- |
| Framework | ASP.NET COre 10.0 (Razor Pages)           |
| ORM       | Entitiy Framewoek Core 8                  |
| Database  | SQLite                                    |
| Auth      | Cookie authentication (Subject to change) |
| UI        | Bootstrap 5                               |

## Prerequisites

- .NET 10 SDk (`dotnet --version` to check)
- Git

## How to run

```bash
git clone <repo-url>
cd HBCDirectory
dotnet run

# Public directory https://localhost:5272 (or displayed URL)
# Admin login: https://.../Login
# Admin dashboard: https://.../Admin (requires login)
```

### Default admin Credentials

- Username: admin
- Password: password

These are subject to change see [known issues](https://github.com/LeloKunene/HBC-Directory/issues/10)

## Project Structure

- Models/ # EF Core entities - Member (name, photo, birthdate, etc.) and Family (groups members together)
- Data/ # DirectoryContext - Configures the SQLite database and relationships between models
- Pages/ # Razor Pages- Each page is a .cshtml (UI) + .cshtml.cs (logic) pair
- wwwroot/ # Static files (CSS, JS, Uploaded photos)

## Key features

- User can signup and add user info.
- User can view other memebers and their info.
- User can view and edit their own user info.
- User can can search for specific members or families.
- Admin has all previously mentioned functionality.
- Admin can delete members and families.

## Contributing

Found a bug or have a suggestion? Open an issue on the [GitHub Issues](https://github.com/LeloKunene/HBC-Directory/issues) page.
