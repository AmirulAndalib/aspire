# Aspirify Eval Apps

These are **pre-aspirification** playground apps used to evaluate the `aspire-init` skill.
They are intentionally NOT wired up with Aspire — the goal is to run `aspire init` on them
and have the agent use the `aspire-init` skill to fully aspirify them.

## Apps

### dotnet-traditional/

A traditional .NET solution with a JS frontend, similar to a real-world LOB app:

- **Vue/Vite frontend** (`frontend/`) — talks to the API via `API_URL` env var
- **ASP.NET Web API** (`src/BoardApi/`) — REST API with EF Core + Postgres, Redis caching
- **Blazor admin dashboard** (`src/AdminDashboard/`) — server-side Blazor, shares DB
- **EF Core migrations worker** (`src/MigrationRunner/`) — runs DB migrations on startup
- **Shared data library** (`src/BoardData/`) — EF Core models and DbContext
- **Solution file** (`BoardApp.slnx`) — ties it all together

Config is via `.env` files and hardcoded connection strings. No Aspire.

### polyglot/

A polyglot microservices app with multiple languages, no solution file:

- **React/Vite frontend** (`frontend/`) — calls all backend APIs
- **Python FastAPI service** (`api-weather/`) — weather data with Redis caching
- **Go HTTP service** (`api-geo/`) — geocoding stub with external API key
- **C# minimal API** (`api-events/`) — events endpoint, single-file .cs
- **Redis** — referenced via `REDIS_URL` env var in multiple services

Config is via `.env` file at root. No Aspire, no apphost.

## Eval process

1. `cd` into either app directory
2. Run `aspire init`
3. Let the agent execute the `aspire-init` skill
4. Verify with `aspire start` — all services should appear in the dashboard
