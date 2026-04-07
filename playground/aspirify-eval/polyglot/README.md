# CityServices — Polyglot

A polyglot microservices app. NOT aspirified — used as eval for `aspire init`.

## Services

- **api-weather** (Python/FastAPI) — weather data, caches in Redis
- **api-geo** (Go) — geocoding stub, uses external API key
- **api-events** (C# minimal API) — city events endpoint
- **frontend** (React/Vite) — dashboard calling all APIs

## Running manually

1. Start Redis on localhost:6379
2. Copy `.env` and set values
3. `cd api-weather && pip install -r requirements.txt && uvicorn main:app --port 8001`
4. `cd api-geo && go run .` (listens on PORT env var, default 8002)
5. `cd api-events && dotnet run` (listens on :8003)
6. `cd frontend && npm install && npm run dev` (listens on :5173)
