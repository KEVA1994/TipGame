# TipGame – Copilot Instructions

## Project Overview

You are working on **TipGame** — a World Cup betting/tipping web application where users predict match outcomes and compete on a leaderboard. The app integrates with Supabase for data storage and authentication, and uses a football API to sync live match data.

## Tech Stack

- **Frontend**: Blazor WebAssembly (Razor components) with per-component `.css` scoped styling
- **UI Components**: MudBlazor component library (Material Design)
- **Backend/Database**: Supabase (PostgreSQL + Auth)
- **Authentication**: Supabase Auth (email/password)
- **Language**: C# 14.0, .NET 10
- **Hosting**: GitHub Pages (Blazor WASM static files)
- **CI/CD**: GitHub Actions (`deploy-blazor.yml`)

## Solution Structure

The solution follows a layered architecture with five projects:

- **TipGame.Blazor** – Blazor WebAssembly frontend (pages, components, services, layout)
- **TipGame.Shared** – Shared DTOs (`MatchDto`, `PredictionDto`, `LeaderboardDto`) used across projects
- **TipGame.Domain** – Domain entities (`Match`, `Prediction`, `User`)
- **TipGame.Infrastructure** – External integrations (Football API client, match sync service, prediction service)
- **TipGame.DataMiner** – Console app for syncing match data from external football APIs into Supabase

## Frontend Architecture (TipGame.Blazor)

- **Pages**: `Home`, `Matches`, `Leaderboard`, `Stats` — each with `.razor` + `.razor.cs` code-behind
- **Components**: Reusable UI components organized in subfolders:
  - `Components/Account/` – `AccountPopover` (login/signup/logout popover)
  - `Components/Stats/` – Chart components (`AccuracyChart`, `HeadToHeadChart`, `PopularTipsChart`, `PointProgressionChart`)
  - `Components/` – `PredictionInput` (match prediction form)
- **Services**: Scoped services registered in `Program.cs`:
  - `MatchService` – Fetches match data from Supabase
  - `PredictionService` – Manages user predictions
  - `LeaderboardService` – Fetches leaderboard rankings
  - `StatsService` – Fetches statistics data
  - `PlayerState` – Manages authentication state via Supabase Auth
- **Layout**: `MainLayout` with MudBlazor theming (light/dark mode), app bar, mini drawer navigation

## Conventions

- All UI text is in **Danish**
- Use MudBlazor components for all UI elements
- **Avoid scoped `.razor.css` files when possible**. Prefer using MudBlazor's built-in Class, Typo, Color, Variant, Elevation, Spacing (pa-, ma-, mb-, etc.), and other component properties for all styling. Only create a `.razor.css` file as an absolute last resort when MudBlazor provides no way to achieve the desired layout or style.
- Code-behind pattern: `.razor` for markup, `.razor.cs` for logic (partial classes)
- Configuration loaded from `wwwroot/appsettings.json` and `appsettings.Development.json`
- Supabase client registered as singleton; services registered as scoped

