# BoardApp — .NET Traditional

A traditional .NET app with a JS frontend. NOT aspirified — used as eval for `aspire init`.

## Running manually

1. Start Postgres on localhost:5432
2. Start Redis on localhost:6379
3. Copy `.env` and set values
4. `cd src/MigrationRunner && dotnet run` (run migrations)
5. `cd src/BoardApi && dotnet run` (start API on :5220)
6. `cd src/AdminDashboard && dotnet run` (start admin on :5230)
7. `cd frontend && npm install && npm run dev` (start frontend on :5173)
