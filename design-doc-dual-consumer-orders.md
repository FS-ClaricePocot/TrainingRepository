# Design Doc: Orders Endpoint - Internal Admin + External Partner Access

## Context
One Orders endpoint needs to serve two very different consumers - an internal admin tool
and an external partner. Both internal admin and external partner access the same Order data. 
Rather than creating two parallel implementations that would be harder to maintain, we expose 
one Orders endpoint to both.

In this document we define the auth mechanism, rate limits, contract versioning,
deprecation policy, and bad input handling for the two users. 


## Authentication

### Internal admin tool — cookie auth
ASP.NET Core Cookie Authentication middleware will be used as cookie auth mechanism
for the internal admin tool. 

### External partner — API key
API key (X-Api-Key) in the request header will be used for the external partner.
API keys will be revokable and will be stored in a partner registry table. 

#### API Key Lifecycle
**Provisioning.** Keys are admin-generated (not self-service) through the internal admin tool,
one key per partner. The raw key is shown to the admin exactly once at creation time — only its
hash is persisted in the partner registry table, following the same principle as password
storage, so a database read alone can never leak a usable key.
 
**Rotation.** A partner can be issued a new key without immediately breaking their existing
integration: the old key is marked `Rotating` (not revoked) and both the old and new key validate
successfully for a grace period (proposed: 48 hours). After the grace period elapses, the old key is
automatically revoked. This gives the partner a window to update their stored credential without
a hard cutover.
 
**Revocation.** Revocation is immediate, not eventual — the partner registry table is the
single source of truth that is checked on every request (no local caching
of key validity), so a revoked key stops authenticating on its very next request rather than
propagating on some delay. Revocation can be triggered manually (support/security action) or
automatically at the end of a rotation grace period.
 
**States a key can be in:** `Active` → `Rotating` (optional, only during a rotation) →
`Revoked`. A request presenting a `Revoked` key is treated the same as a missing/invalid key: `401`.

### Telling them apart
We register both schemes and use a policy scheme that inspects each incoming request. 
If X-Api-Key is present, we route it to the API key handler; otherwise we route it to the
cookie handler and forward it to the appropriate authentication handler. The controller
and business logic remain identical for both; only the identity resolution differs. 
Request only reaches the business logic after passing the authentication. 
If neither auth succeeds due to missing auth or failure, 401 (unauthorized) is returned.
If the resolved identity lacks permission for the requested resource, 403 (forbidden) is returned.


## Rate Limiting
Rate limiting is partitioned by consumer identity - API key for external partners
and user/tenantID for internal admin. Two policies named via Microsoft.AspNetCore.RateLimiting:
* PartnerPolicy - uses fixed window with limits set appropriate to the partner. 
Should be specified in the config and not hardcoded so it's flexible to changes on partner policy.
* InternalPolicy - fully exempt. Internal admin traffic is not subject to rate limiting.

Throttled requests return 429 with a Retry-After header. 

## Versioning (v1 / v2)

### Approach chosen
URL segment will be used for versioning (e.g. /api/v1/orders, /api/v2/orders).
It is explicit, version is clearly visible, and eradicates ambiguity especially for an integrating partner. 

Version-specific response DTOs (e.g. OrderV1Response, OrderV2Response) are mapped at the controller boundary 
and stay separate from the internal Order domain model, so a contract change never forces a change to OrderService or vice versa.

### v1 compatibility guarantee
v2 will only extend what is already in v1. No removal will be made to not introduce breaking changes to existing integrations with v1.
Consumers of the "updates" can directly use v2 without losing what is in v1. 

## Deprecation Policy
Deprecated versions are marked with the Deprecation header and the removal date is communicated via the Sunset header. 
After the sunset date, the old version returns 410 Gone with a pointer to the current version — never a silent break or unexplained failure.

## Malformed Payload Handling
Bad requests return 400 with an error field on the message that the integrating parties can act on.
Verbose and explicit message for the internal admin and generic message for the external partner for security. 


