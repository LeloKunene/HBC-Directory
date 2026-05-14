# HBC Directory - A private church member directory built with ASP .NET Core Razor Pages and SQLite

## Tech Stack

| Layer     | Technology                                |
| --------- | ----------------------------------------- |
| Framework | ASP.NET Core 10.0 (Razor Pages)           |
| ORM       | Entity Framework Core 8                   |
| Database  | SQLite                                    |
| Auth      | Cookie authentication (Subject to change) |
| UI        | Bootstrap 5                               |

## Prerequisites

- .NET 10 SDK (`dotnet --version` to check)
- Git

## How to run

```bash
git clone <repo-url>
cd HBCDirectory
dotnet run

- Public directory https://localhost:5272 (or displayed URL)
- Admin login: https://.../Login (required to access admin features)
- Admin dashboard: https://.../Admin
```

### Default admin Credentials

- **Username**: `admin`
- **Password**: `password`

> ⚠️ These are hardcoded for development. See [#10](https://github.com/LeloKunene/HBC-Directory/issues/10) for the planned fix.

## Project Structure

```
HBCDirectory/
├── Models/     # EF Core entities — Member and Family
├── Data/       # DirectoryContext — SQLite config and relationships
├── Pages/      # Razor Pages (.cshtml + .cshtml.cs pairs)
└── wwwroot/    # Static files (CSS, JS, uploaded photos)
```

## Features

- Public directory: anyone can browse and search members by name or family
- Admin can add, delete and manage members and families
- Admin can upload a photo for each member
- Members are grouped into family units

## Planned Features

- Member self-registration and profile management
- Members can edit their own info

## Contributing

Found a bug or have a suggestion? Open an issue on the [GitHub Issues](https://github.com/LeloKunene/HBC-Directory/issues) page.
