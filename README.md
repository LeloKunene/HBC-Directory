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
dotnet run / dotnet watch (Enables HotReload)

- Public directory https://localhost:5272 (or displayed URL)
- Admin login: https://.../Login (required to access admin features)
- Admin dashboard: https://.../Admin
```

### Admin Credentials

Set your local admin login credentials using the .NET User Secrets tool (these are stored outside the repo and never committed):

```bash
cd HBCDirectory
dotnet user-secrets init
dotnet user-secrets set "AdminCredentials:Username" "<your_username>"
dotnet user-secrets set "AdminCredentials:Password" "<your_password>"
```

Replace `<your_username>` and `<your_password>` with your own values, then use them to log into the admin panel locally.

Verify it worked:

```bash
dotnet user-secrets list
```

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
