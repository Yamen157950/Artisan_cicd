# Artisan AI chat (home page)

The home page hero input talks to **Artisan AI** through the .NET API. Answers come from the **SQL Server database** (`ArtisanDb`) — real provider profiles, ratings, and prices.

## Run (2 terminals)

**Quick start:**

```powershell
cd "C:\Users\NTC\OneDrive\Desktop\ARTISAN 2\scripts"
.\start-all.ps1
```

**1. SQL Server LocalDB** (if not already running):

```powershell
sqllocaldb start MSSQLLocalDB
```

**2. Artisan API**

```powershell
cd "C:\Users\NTC\OneDrive\Desktop\ARTISAN 2\Backend\ArtisanApi"
dotnet run --launch-profile http
```

**3. Frontend** (Live Server on `frontend/` folder, port 5500)

Open: http://127.0.0.1:5500/index.html

> **Note:** The old Python Herafi chatbot (`python app.py`) is no longer required. Chat is handled inside the API using `HerafiChatService`.

## Database connection

`appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ArtisanDb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

## API

`POST /api/herafi/chat` with body `{ "message": "plumber in Amman" }` returns live matches from `ProviderProfiles` + `ProviderRatings`:

```json
{
  "reply": "🔍 Live results from Artisan (Plumbers · Amman)\n\n...",
  "navigate": { "type": "browse", "trade": "Plumbing", "city": "Amman", "q": "Amman", "sort": "rating" },
  "redirectDelayMs": 1600
}
```

The home page then opens **Browse** (`customer-services.html`) with matching trade, city, and sort.

## Example queries

- `electrician in Amman`
- `cheapest plumber in Irbid`
- `best painter`
- `details demo-plumber-amman`
- `كهربائي في عمان`
