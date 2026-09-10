# Caching & Query Performance — Vocabulary Notes

## Cache-Aside (a.k.a. Lazy Loading)

A caching pattern where the application code — not the cache itself — is
responsible for keeping the cache in sync with the source of truth (the DB).

**Flow:**
1. Application checks the cache for the key.
2. **Hit** → return the cached value directly.
3. **Miss** → read from the database, store the result in the cache, then return it.
4. On writes, the application either invalidates (removes) or updates the
   affected cache entry so subsequent reads don't see stale data.

This is the pattern `GetOrdersForCustomerAsync` uses: check `_cache` first,
fall back to calling the DB on a miss, then populate the cache.
The cache is never the source of truth — the database always is.

---

## Cache Stampede (a.k.a. Thundering Herd)

What happens when a cache entry expires or is invalidated while many
concurrent requests are asking for that same key at once. Without a guard,
**every one of those requests sees a miss simultaneously** and each goes to
the database to recompute the same result — causing unnecessary duplicate DB
operations for what should have been a single recomputation.

**Guard pattern used in `OrderService`:** a per-key lock (`SemaphoreSlim`)
ensures only the first thread to see the miss actually queries the
database. Every other concurrent request for that same key blocks briefly,
then re-checks the cache after the lock is released and reads the
now-fresh value — instead of also hitting the DB.

---

## TTL (Time To Live)

The duration a cache entry is considered valid before it's treated as
expired, regardless of whether anything actually invalidated it. After the
TTL elapses, the next read is treated as a cache miss and triggers a
refresh from the source of truth.

TTL is a **safety net against staleness**, independent of explicit
invalidation — even if the application forgets to invalidate a key after a
write, the TTL guarantees the cache eventually self-corrects.

In `OrderService`, this is `CacheEntry.ExpiresAtUtc`, checked against
`DateTime.UtcNow` on every read; a 30-second `_cacheTtl` is used as the default.


---

## Parameter Sniffing

A SQL Server query-optimization behavior: when a parameterized query (or
stored procedure) is executed, SQL Server builds and caches an **execution
plan** based on the specific parameter values passed in on that *first*
execution. That plan is then reused for subsequent calls with *different*
parameter values.

This is usually beneficial — the plan is optimized for real, representative
data — but it becomes a problem when parameter values have very different
data distributions. Example: a query filtering `Status = @Status` might get
a plan optimized for `@Status = 'Pending'` (a small subset of rows) cached
first, and that same plan then gets reused — sub-optimally — for
`@Status = 'Completed'` (a much larger subset), or vice versa.

Symptoms: a query is fast for some parameter values and slow for others,
with no code change in between — just different execution plans being
reused (or not reused) depending on plan cache state.

Common mitigations:
- `OPTION (RECOMPILE)` — forces a fresh plan on every execution (costs
  compile time, avoids stale plans).
- `OPTION (OPTIMIZE FOR ...)` — hints the optimizer to plan for a
  representative or average value.
- Plan guides or query store forcing a known-good plan.

**Note on `OrderService`:** parameterizing the query (`@CustomerId`,
`@Status`) — which we did to fix the SQL injection risk — is exactly what
makes parameter sniffing *possible* in the first place. The original
concatenated-string version had no reusable parameterized plan at all.

---

## Execution Plan

The step-by-step strategy SQL Server's query optimizer decides on to
actually retrieve the requested data — which indexes to use (or not use),
join order and join type (nested loop, hash, merge), whether to scan a
table or seek an index, estimated vs. actual row counts, etc.

SQL Server caches execution plans (the "plan cache") so it doesn't have to
re-optimize identical queries on every execution — this is the mechanism
that both makes parameterized queries efficient *and* is the root cause of
parameter sniffing.


---