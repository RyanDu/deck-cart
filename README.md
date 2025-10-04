
# Deck API — SQLite Cart Demo

  

A from-scratch, end‑to‑end backend exercise built with **.NET 8**, **ASP.NET Core**, **EF Core + SQLite**, **JWT auth**, **optimistic concurrency with ETag + If‑Match**,  request validation (**FluentValidation**), and observability (Serilog logs + OpenTelemetry traces). Includes **xUnit integration tests** using an in‑memory SQLite database.
![API SWAGER UI](./assets/Screenshot%202025-10-03%20at%205.57.16 PM.png)

  

> Highlights

>  - Cart snapshots (**CartHistory**) stored as JSON for easy playback/auditing.

>  - All write operations protected by **ETag + If‑Match** optimistic concurrency.

>  - Clean local dev → test workflow: the test host boots with **in‑memory SQLite**, schema creation, and seeding.

  

---

  

## Tech Stack

  

-  **Runtime**: .NET 8

-  **Web**: ASP.NET Core Minimal API + Swagger (Swashbuckle)

-  **Data**: EF Core + SQLite

-  **Auth**: JWT Bearer

-  **Validation**: FluentValidation

-  **Observability**: Serilog, OpenTelemetry (console exporter by default)

-  **Tests**: xUnit + `WebApplicationFactory`

  

---

  

## Project Structure

  

```

├── src

│ └── Deck.Api # Web API

│ ├── Data/ # AppDbContext & Migrations

│ ├── Models/ # User / Item / CartItem / CartHistory

│ ├── Services/ # Business services (CartService, etc.)

│ ├── Validators/ # FluentValidation validators

│ ├── Program.cs # DI, middleware, Swagger, Auth, Telemetry

│ └── appsettings*.json

└── tests

| └── Deck.Tests # xUnit integration tests

| ├── Infrastructure/ # CustomWebApplicationFactory (in‑memory DB, JWT config)

| └── CartApiIntegrationTests.cs

```

  

---

  

## Getting Started (Local Dev)

  

1) Restore packages and generate/apply migrations for a **file‑based** dev DB:

```bash

dotnet  restore

# If you don't have migrations yet:

dotnet  ef  migrations  add  InitialCreate  --project  src/Deck.Api/Deck.Api.csproj  -o  Migrations

dotnet  ef  database  update  --project  src/Deck.Api/Deck.Api.csproj

```

  

2) Run the API:

```bash

dotnet  run  --project  src/Deck.Api/Deck.Api.csproj

```

  

3) Open Swagger UI:

```

http://localhost:5202

```

  

---

  

## Configuration (`appsettings*.json`)

  

```json

{

	"ConnectionStrings": {

		"Default": "Data Source=deck.db"

	},

	"Jwt": {

		"Issuer": "deck.local",

		"Audience": "deck.api",

		"Key": "please-change-me-32+chars"

	},

	"Ef": {

		"MigrateOnStartup": true

	},

	"Serilog": {

		"MinimumLevel": "Information"

	}

}

```

  

> In **tests**, the factory injects a dedicated configuration (Issuer/Audience/Key and `Ef:MigrateOnStartup=false`) and uses **in‑memory SQLite**.

> For dev/prod, keeping `Ef:MigrateOnStartup=true` lets the app auto‑migrate on boot.

  

---

  

## Data Model (Key Points)

  

- Core tables: **Users**, **Items**, **CartItems**, **CartHistory**

- Common columns: `CreatedDateTime`, `ModifiedDateTime`, `IsActive` (soft‑delete / availability)

- User cart version: `Users.CartVersion` (integer)

- Cart history: `CartHistory(UserId, SnapshotAt, PayloadJson)` with composite index `(UserId, SnapshotAt)`

- Static seeding values are recommended to avoid EF's `PendingModelChangesWarning` (do not seed with `DateTime.UtcNow`/`Guid.NewGuid()` directly).

  

---

  

## Authentication


-  **JWT Bearer**. In Swagger, click **Authorize**, paste `Bearer <token>`.

- Obtain tokens from `/auth/token` (see examples below).
![authtication image](./assets/Screenshot%202025-10-03%20at%205.47.42 PM.png)
---


## Optimistic Concurrency (ETag + If‑Match)

  
-  **GET** cart returns an **ETag** header (weak or strong depending on implementation).

-  **REPLACE** requires `If-Match`:

- Version mismatch → `409 Conflict` with `{"error":"ETagConflict"}`

- Missing header → `428 Precondition Required`

- Clients should echo back the ETag they received. Tests are robust to weak/strong/number forms.

  

---

  

## API Overview

  

### 1) Issue Token

-  `POST /auth/token`

#### Request

```json

{ "userId": 1 }

```

#### Response

-  `200 OK` → raw JWT string in body
```json
{ "access_token": "<JWT...>", "token_type": "Bearer" }
```
#### Authenticate
Put "<JWT>" got above to authentication code in authentication popup.


### 2) Get Cart (returns ETag)

-  `POST /cart/get`

#### Request

```json

{ "userId": 1 }

```

#### Response

-  `200 OK`

- Headers: `ETag: "0"` (or `W/"0"`)

- Body example:

```json

{ "cart": [ { "itemId": 1 }, { "itemId": 2 } ] }

```

  

### 3) Replace Cart (requires If‑Match)

-  `POST /cart/replace`

#### Headers

-  `Authorization: Bearer <token>`

-  `If-Match: "0"` (or `W/"0"`, depending on what you received)

#### Request

```json

{ "userId": 1, "items": [ { "itemId": 1 }, { "itemId": 2 } ] }

```

#### Response

-  `204 No Content` on success (may return a new `ETag` header)

-  `409 Conflict` when version mismatches (`{"error":"ETagConflict"}`)

-  `422 Unprocessable Entity` on semantic issues (e.g., duplicate items)

-  `428 Precondition Required` if `If-Match` is missing

  

### 4) Cart History

-  `GET /cart/history/{userId}?take=20&before=2025-10-03T12:00:00Z`

#### Response

-  `200 OK`, array of snapshots

```json

[

	{

		"id": 3,

		"userId": 1,

		"snapshotAt": "2025-10-03T11:50:00Z",

		"payloadJson": "{"items":[{"itemId":1},{"itemId":2}]}"

	}

]

```

  

---

  

## Swagger / cURL Examples

  

### Get Token

```bash

curl  -s  -X  POST  http://localhost:5202/auth/token  -H  "Content-Type: application/json"  -d  '{"userId":1}'

```

  

### Get Cart (first call returns ETag)

```bash

TOKEN=...  # paste the token above

curl  -i  -X  POST  http://localhost:5202/cart/get  -H  "Authorization: Bearer $TOKEN"  -H  "Content-Type: application/json"  -d  '{"userId":1}'

# Note the ETag header in the response

```

  

### Replace Cart (send back the ETag you received)

```bash

IFMATCH='W/"0"'  # or ""0"" depending on the previous response

curl  -i  -X  POST  http://localhost:5202/cart/replace  -H  "Authorization: Bearer $TOKEN"  -H  "If-Match: $IFMATCH"  -H  "Content-Type: application/json"  -d  '{"userId":1,"items":[{"itemId":1},{"itemId":2}]}'

```

  

### Get Cart again

```bash

curl  -s  -X  POST  http://localhost:5202/cart/get  -H  "Authorization: Bearer $TOKEN"  -H  "Content-Type: application/json"  -d  '{"userId":1}'

```

  

---

  

## Validation & Error Handling

  

-  **FluentValidation** for request models (e.g., rejects duplicate item IDs, invalid inputs).

-  **Global error middleware** for consistent error responses and status codes.

- Conventions:

-  `422 Unprocessable Entity` for semantic validation failures.

-  `409 Conflict` for concurrency conflicts.

-  `428 Precondition Required` when preconditions (If‑Match) are missing.

  

---

  

## Observability

  

-  **Serilog** for structured logs (written to console by default). View them directly in your terminal/VS Code.

-  **OpenTelemetry** for traces (console exporter). You can wire an OTLP endpoint for collectors if needed.

- Extra spans added in controllers/services to mark start/finish of important operations.

![Telemetry image](./assets/Screenshot%202025-10-03%20at%204.29.21 PM.png)

---

  

## Testing

  

-  **xUnit** + `WebApplicationFactory<Program>`

- In tests:

- Use **in‑memory SQLite** with a shared connection and deterministic seed data.

- Inject test‑only JWT settings and **disable startup migrations** (`Ef:MigrateOnStartup=false`).

- Disable parallelization to avoid contention on in‑memory SQLite.

  

### Run Tests

```bash

dotnet  test  tests/Deck.Tests  -l  "console;verbosity=detailed"

```

  

### Covered Scenarios

- Protected endpoints without a token → `401`

- Happy path: get cart → replace → get cart

- Duplicate items → `422`

- Stale/wrong ETag → `409`

- History endpoint returns recent snapshots

  

---

  

## Common Pitfalls

  

-  **PendingModelChangesWarning**: never seed with dynamic values (e.g., `DateTime.UtcNow`, `Guid.NewGuid()`) in `HasData`. Use fixed values instead.

-  **EnsureCreated vs Migrate**: do **not** mix both in the same run. Tests use `EnsureCreated`; app startup migrations are disabled in the test environment.

-  **`table "X" already exists`** during tests: indicates a clash between migrations and `EnsureCreated`. Ensure the app does not run migrations under the test environment.

  

---

  

## Contribution

  

- Use feature branches and PRs, e.g.: `feat/*`, `fix/*`, `chore/*`, `test/*`.

- Conventional Commits are welcome.

  

---

  

## License

  

MIT (educational/demo usage).