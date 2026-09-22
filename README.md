# OrderManagementService

An ASP.NET Core Web API that serves Order data to two consumers through one shared
endpoint surface: an **internal admin tool** (cookie-authenticated,trusted, unlimited rate) and an
**external partner integration** (API-key-authenticated,untrusted, rate-limited). Both go through the
same controllers and business logic — only identity resolution differs.

Full design rationale lives in [`design-doc-dual-consumer-orders.md`](../design-doc-dual-consumer-orders.md)
at the repo root; this README covers how to run the project, the decisions worth knowing
before changing it, and what it doesn't do yet.

## Contents
- [How to Run](#how-to-run)
- [API Overview](#api-overview)
- [Design Decisions](#design-decisions)
- [Caching Strategy](#caching-strategy)
- [Testing](#testing)
- [Project Structure](#project-structure)
- [Known Limitations](#known-limitations)

---

## How to Run

### Prerequisites
- .NET 10 SDK
- SQL Server (local instance is fine — `Server=localhost` is the default everywhere)
- Node.js 18+ (only needed for `client/`)

### 1. Set up the database
Run these against your SQL Server, in order — all are idempotent (`IF NOT EXISTS`), safe to
re-run:
```
database scripts/day-4-repro-script.sql       # creates OrderManagementDb, Customers, Orders + seed data
database scripts/day-5-add-orders-index.sql    # index on Orders(CustomerId, Status)
database scripts/add-partner-registry.sql      # Partners, PartnerApiKeys tables
```
The connection string is `Server=localhost;Database=OrderManagementDb;Trusted_Connection=True;TrustServerCertificate=True;`
(`src/OrderManagement.Api/appsettings.Development.json`) — edit it there if your SQL Server
isn't local/Windows-auth.

### 2. Trust the local HTTPS dev certificate (one-time)
```bash
dotnet dev-certs https --trust
```
Skipping this is the single most common reason the app "doesn't work" locally — Kestrel
silently fails to bind the HTTPS port and requests to it never connect. Always start with
`--launch-profile https` and confirm the console shows **both**:
```
Now listening on: https://localhost:7271
Now listening on: http://localhost:5269
```
If only the `http` line appears, the cert isn't trusted (or something else is already
bound to the port) — fix that before anything else. Use `https://localhost:7271` for every
request; the app redirects `http` → `https`, and that redirect only works if the app is
actually listening on both.

### 3. Run the API
```bash
cd src/OrderManagement.Api
dotnet run --launch-profile https
```

### 4. Provision a partner (needed to call anything as an external consumer)
There's no seeded partner key — provision one through the admin API. In Debug builds only,
`POST /api/test/admin-login` signs you in as a test admin (no real login UI exists yet — see
[Known Limitations](#known-limitations)):
```bash
curl -sk -c cookies.txt -X POST https://localhost:7271/api/test/admin-login
curl -sk -b cookies.txt -X POST https://localhost:7271/api/v1/partners \
  -H "Content-Type: application/json" -d '{"name":"my-partner"}'
```
Save the `apiKey` from the response — it's shown exactly once. Use it as the `X-Api-Key`
header on partner-facing requests.

### 5. Run the client (optional)
```bash
cd client
npm install
echo "VITE_API_KEY=<the apiKey from step 4>" > .env.local
npm run dev
```

### 6. Run the tests
```bash
cd OrderManagementService     # solution root
dotnet test
```
See [Testing](#testing) for what the DB-backed tests need.

---

## API Overview

| Endpoint | Auth | Notes |
|---|---|---|
| `GET /api/v{1,2}/orders?customerId=&status=` | DualConsumer | filtered by status |
| `PATCH /api/v{1,2}/orders/{orderId}/status` | DualConsumer | evicts affected cache entries |
| `GET /api/v{1,2}/customers/{customerId}/total-spend` | DualConsumer | sum of Completed orders |
| `POST /api/v1/reports` | DualConsumer | `{ groupBy: "ByStatus" \| "ByCustomer" }` → `202` + job id, or `503` if the queue is full |
| `GET /api/v1/reports/{jobId}` | DualConsumer | `Pending` / `Completed` / `Failed` |
| `POST /api/v1/partners` | InternalAdmin | provision a partner, returns the raw key once |
| `POST /api/v1/partners/{partnerId}/rotate-key` | InternalAdmin | old key stays valid for a 48h grace period |
| `POST /api/v1/partners/{partnerId}/keys/{keyId}/revoke` | InternalAdmin | immediate; scoped to that partner's key only |
| `POST /api/test/admin-login` | none (`#if DEBUG` only) | dev-only, mints an admin cookie for local testing |

`v2` endpoints exist and are wired up, but currently return the same fields as `v1` — see
[Known Limitations](#known-limitations).

---

## Design Decisions

The full reasoning is in the design doc; the points most likely to matter when extending
this code:

- **One `"Selector"` policy scheme, not two schemes listed on a policy.** Authentication
  dispatches to Cookie or API-key based on whether `X-Api-Key` is present, and authorization
  policies route through that single scheme. Listing both concrete schemes on a policy
  directly is a trap — ASP.NET Core's multi-scheme authorization succeeds if *any* listed
  scheme succeeds, so a stale admin cookie can silently authenticate a request with a
  garbage API key.
- **One rate-limit policy with a branching partitioner**, not separate policies per
  consumer — admin is exempt (`GetNoLimiter`), partner gets a fixed window keyed by partner
  ID, and an unresolved identity falls back to the *stricter* partner-level limit (keyed by
  IP) rather than the admin's unlimited one. Partner limits are configured in
  `appsettings.json` under `RateLimiting:PartnerPolicy` (100 requests / 60s by default).
- **API keys are SHA-256 hashed, not slow-hashed** (bcrypt/PBKDF2). This is deliberate: keys
  are high-entropy, machine-generated strings, not human-chosen passwords, so the
  brute-force-resistance a slow hash buys you doesn't apply here.
- **Auth failures fail closed with a logged `401`, never an unhandled `500`.** A partner
  registry outage or an inconsistent key row (e.g. a `Rotating` key with no timestamp) is
  caught, logged at Error, and returns `401` like any other invalid key. The partner can't
  distinguish "your key is wrong" from "the registry is down" — that's a known trade-off.
- **Key revocation is scoped to `(partnerId, keyId)`**, not `keyId` alone — the endpoint
  takes both, and the `UPDATE` filters on both, so one partner's key can't be revoked through
  another partner's route. Revoking an already-revoked key is idempotent and preserves the
  original `RevokedAt`.
- **URL-segment versioning** (`/api/v1/...`, `/api/v2/...`), DTOs mapped at the controller
  boundary — a contract change never touches the domain model. `v2` only ever extends `v1`.
- **Bounded report queue, not unbounded.** `Channel.CreateBounded(100)` with the default
  `Wait` full-mode; `TryWrite` returns `false` immediately at capacity, which the controller
  turns into a `503`. Don't switch this to a `Drop*` mode — in those, `TryWrite` returns
  `true` while silently discarding the item, which would turn the `503` into a job that's
  lost behind a `202`.

---

## Caching Strategy

`OrderService` caches `(customerId, status)` → orders for 30 seconds via `IMemoryCache`,
behind a repository seam (`IOrderRepository`) that exists specifically so this logic is
unit-testable without a database.

**Cache key is `customerId:status`, not `customerId` alone.** An earlier version keyed by
customer only, which meant a lookup for one status could be served data that had actually
been cached for a *different* status — wrong data, silently, with no error. The compound key
closes that.

**Concurrent misses on the same key only hit the database once**, coordinated by a
self-cleaning `ConcurrentDictionary`:
```csharp
private readonly ConcurrentDictionary<string, Lazy<Task<List<Order>>>> _inFlightLoads = new();

public async Task<List<Order>> GetOrdersForCustomerAsync(int customerId, string status)
{
    var cacheKey = BuildCacheKey(customerId, status);
    if (_cache.TryGetValue(cacheKey, out List<Order>? cached)) return cached!;

    var lazyLoad = _inFlightLoads.GetOrAdd(cacheKey,
        key => new Lazy<Task<List<Order>>>(() => LoadAndCacheAsync(key, customerId, status)));
    try { return await lazyLoad!.Value; }
    finally { _inFlightLoads.TryRemove(cacheKey, out _); }
}
```
`GetOrAdd` guarantees every racing caller gets the *same* `Lazy<T>` instance, and `Lazy<T>`'s
"runs once" guarantee is per-instance — so the actual DB call only ever runs once per miss,
no matter how many requests race in. This matters because an earlier design
(`IMemoryCache.GetOrCreate` wrapped in a bare `Lazy<T>`) looked correct but wasn't: that
guarantee is per-instance, and concurrent racers who all miss before the first write
completes can each construct their *own* `Lazy`. It took a test using genuine `Task.Run`
concurrency to catch — a sequential test gave a false pass.

**Invalidation:** `UpdateOrderStatusAsync` evicts exactly the affected customer's old-status
and new-status cache entries — other customers' cached data, and other statuses for the same
customer, are untouched.

**In-process only** — `IMemoryCache` is per-instance. If this service ever runs on more than
one node, cached data (and the in-flight-load coordination) won't be shared across them; a
distributed cache would be needed at that point.

---

## Testing

```bash
dotnet test              # everything, Debug
dotnet test -c Release    # everything, Release — contract tests don't depend on Debug-only code
```

**21 tests total:**

| Class | Count | What it covers |
|---|---|---|
| `OrderServiceTests` | 9 | caching, stampede protection, spend calculation, status-update eviction |
| `ReportsControllerTests` | 5 | controller behavior against a mocked `IReportService` |
| `SqlInjectionRegressionTests` | 4 | real attack payloads against the repository, parameterization proven safe |
| `OrdersContractTest` | 3 | full pipeline (auth, rate limiting, validation) against a real host |

**The last two need a reachable SQL Server.** They build a uniquely named, throwaway
database per test class (schema + a small deterministic seed), and drop it on teardown —
never the dev database. Point them at a different server with:
```bash
export ORDERMANAGEMENT_TEST_SQL="Server=my-server;Trusted_Connection=True;TrustServerCertificate=True;"
```
Default, if unset: `Server=localhost;Trusted_Connection=True;TrustServerCertificate=True;`.
If no server is reachable, these tests fail loudly with a message naming the variable,
rather than silently skipping.

`OrdersContractTest` mints a real admin cookie from the test host's own cookie options —
it doesn't depend on `/api/test/admin-login`, which is why the suite passes in both Debug
and Release.

---

## Project Structure

```
OrderManagementService/
├── src/
│   ├── OrderManagement.Api/            web/host layer
│   │   ├── Auth/                       cookie + API-key schemes, the Selector, authorization policies
│   │   ├── Controllers/                Orders, Customers, Reports, Partners (v1/v2 where applicable)
│   │   ├── Partners/                   partner registry domain (models, hashing, SQL repository)
│   │   ├── RateLimiting/               the branching partitioner policy
│   │   ├── Reports/                    async report queue, background worker, service
│   │   ├── Validation/                 query/payload validators
│   │   └── Program.cs                  composition root
│   └── OrderManagement.Core/           domain layer — OrderService, repository seam
├── tests/OrderManagement.Tests/
│   └── Infrastructure/                 throwaway-database + test-host fixtures (see Testing)
└── tools/CacheRepro/                   standalone repro of the cache-stampede race
```
`database scripts/` and `client/` (a minimal TypeScript/Vite consumer) live at the repo root,
alongside this project.

---

## Known Limitations

- **`v2` endpoints don't have real v2-only fields yet.** `OrderV2Response` /
  `TotalSpendV2Response` currently just extend their `v1` counterparts, marked
  `// TODO: populate real v2-only fields once scoped`. The versioning *mechanism* is real;
  the `v2` *contract* isn't yet.
- **No real admin login flow.** The only way to authenticate as admin locally is
  `POST /api/test/admin-login`, which is compiled out of Release builds (`#if DEBUG`). A
  production deployment of the admin tool would need a real login page/flow — that hasn't
  been built.
- **A partner-registry outage returns `401`, not `503`.** Documented trade-off in
  [Design Decisions](#design-decisions) — a distinct "service unavailable" response for
  infrastructure failures during auth would need a `HandleChallengeAsync` override.
- **The `RevokeKey` partner-scoping fix has no permanent automated test.** It was verified
  with a temporary integration probe during review (mismatched partner/key → `404`, correct
  pair still `204`, idempotent, `RevokedAt` preserved) but that probe isn't in the repo.
  Promoting it to a permanent test on `ContractTestFixture` is a follow-up.
- **Single-node only.** `IMemoryCache` and the in-flight-load coordination in `OrderService`,
  and the rate limiter's partition state, are all in-process. None of it is shared across
  multiple instances of the API.
- **The TypeScript client is a minimal smoke-test harness, not a real UI.** It authenticates
  with a single partner API key from an env var (`client/.env.local`), has no admin/cookie
  flow, and only exercises `GetOrders` from `main.ts` today (the other client functions exist
  but aren't wired into the page).
- **No ORM or migrations.** Data access is raw parameterized ADO.NET; schema changes are
  hand-written idempotent SQL scripts in `database scripts/`, applied manually.
- **`CustomerService.cs`** in `OrderManagement.Core` is dead code — a leftover from an
  earlier code-review exercise, not registered in DI, not used by anything.
- **Report generation has a hardcoded 5-second simulated delay** (`ReportService`), standing
  in for a production-scale query — noted in-code as a simulation, not a real workload
  measurement beyond the comment that a real 50k-row query took ~126ms.