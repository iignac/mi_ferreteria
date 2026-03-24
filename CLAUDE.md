# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Mi Ferretería** is an ASP.NET Core 9.0 MVC web application for managing a hardware store — sales, inventory, clients, users, and permissions.

## Tech Stack

- **Backend:** ASP.NET Core 9.0 (C#), MVC pattern, Razor views
- **Database:** PostgreSQL via Npgsql (raw ADO.NET — NO Entity Framework)
- **Frontend:** Tailwind CSS v3, Alpine.js 3.x, Bootstrap (legacy)
- **Auth:** Cookie-based authentication with claims-based RBAC

## Commands

```bash
# Run (development)
dotnet run --configuration Development
# App runs on http://localhost:5215 / https://localhost:7186

# Build
dotnet build

# Build Tailwind CSS (also runs automatically before dotnet build)
npm run build:css
```

## Hard Rules

- **No Entity Framework** — use raw SQL with Npgsql only
- **No Identity Framework** — custom auth in `Security/`
- Use C#, .NET 9, ASP.NET Core MVC, Razor, ViewModels, and DI
- All repositories registered as `Transient` in `Program.cs`

## Architecture

### Layered Structure

```
Controllers/     → HTTP request handling, thin orchestration
ViewModels/      → Data shaped for views (separate from domain Models)
Models/          → Domain entities (map to DB tables)
Data/            → Repository interfaces + Npgsql implementations (raw SQL)
Security/        → Auth service, permission definitions, authorization filters
Helpers/         → Shared utilities
Dtos/            → Data Transfer Objects (for API endpoints)
Views/           → Razor .cshtml templates, organized by controller
wwwroot/         → Static assets; css/tailwind.css is compiled output
Scripts/         → SQL migration scripts
```

### Repository Pattern

Every entity has an interface in `Data/` and a concrete implementation using raw `NpgsqlConnection`. Repositories are injected into controllers. Never instantiate repositories directly.

### Permission System (RBAC)

Three default roles: `Administrador`, `Vendedor`, `Stock`. Permissions are claims stored in the auth cookie. There are 18 permissions across 7 domains (Ventas, Stock, Productos, Categorías, Clientes, Reportes, Usuarios). Authorization uses dynamically generated policies based on permission claim names. See `Security/` for `PermisosDefinicion`, `AuthService`, and authorization attributes.

### Audit Logging

`AuditoriaActionFilter` (registered globally) logs every controller action to `auditoria_registro`.

### Frontend Patterns

- **Layout:** `Views/Shared/_Layout.cshtml` — dark sidebar, responsive at 768px breakpoint via Alpine.js
- **Alerts:** TempData keys `SuccessMessage` / `ErrorMessage` displayed globally in layout
- **Interactivity:** Alpine.js for toggles, dropdowns, and form state; avoid jQuery for new code
- **CSS:** Edit `wwwroot/css/app.css` as the source; `tailwind.css` is the compiled output (do not edit directly)

### API Endpoints

`ProductoApiController` and `UsuarioApiController` expose REST endpoints. Swagger available at `/swagger` in Development.
