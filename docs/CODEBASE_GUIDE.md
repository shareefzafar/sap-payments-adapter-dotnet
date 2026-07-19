# Codebase Guide — Where to Start, How It All Connects

This is the narrative companion to `docs/architecture.svg` and the
README's "Why this design" section — read this when you (or someone else)
need to actually get oriented in the code, not just see the shape of it.

## The big picture

This project answers a specific question: *"how do I expose SAP's data
and transactions as REST APIs other bank systems (like Payments) can
call?"* Everything here exists to make that flow — a request comes in,
gets routed to real SAP logic, a response goes back out — safe, testable,
and contract-enforced rather than a quick hack.

## Where to start reading (in this order)

1. **`src/SapPaymentsAdapter.Api/openapi/sap-payments-adapter.yaml`** —
   read this first, always. It's the actual source of truth for what the
   API does. Every DTO shape, every endpoint, every status code is
   defined here before any C# exists. This is the "contract-first" part
   of the requirement.

2. **`Program.cs`** — the composition root (≈ Spring Boot's
   `Application.java`). Shows what's wired to what: Vault → SAP
   connector → domain services → controllers → middleware, in about 60
   lines.

3. **`Controllers/SapAdapterController.cs`** — the one entry point for
   all 5 operations. Inherits from `ControllerBaseControllerBase`, which
   doesn't exist in source control — NSwag generates it fresh from the
   YAML spec on every build. That's the actual mechanism behind "if
   someone changes spec, code should be recompiled": change the YAML,
   the abstract method signatures change, this controller stops
   compiling until it's updated to match.

4. **`Services/Sap/SapBapiSimulator.cs`** — the heart of the hands-on SAP
   practice value. Not a generic mock — shaped like real SAP RFC/BAPI
   calls (`BAPI_ACC_DOCUMENT_POST`, the `BAPIRET2` return-table pattern,
   the easy-to-miss `BAPI_TRANSACTION_COMMIT` requirement). Reading this
   teaches what real SAP integration code has to handle, even with no
   real SAP behind it yet.

5. **The three `Services/*/​*Service.cs` files** (Payments, Vendors,
   GlAccounts) — the translation layer between the generated REST DTOs
   (NSwag's choices: `double`, `DateTimeOffset`, enums) and the internal
   SAP-shaped types in `Services/Sap` (this project's choices: `decimal`,
   `DateOnly`). This split exists because REST contracts and SAP's
   native shapes don't naturally agree, and pretending they do is where
   bugs hide.

## How a single request flows, end to end

Using `POST /api/v1/payments` as the concrete example:

1. A caller sends JSON matching `PaymentInitiationRequest` — shape
   defined in the YAML, generated into C# by NSwag.
2. `SapAdapterController.InitiatePayment` receives it, calls
   `PaymentsService.InitiatePaymentAsync`.
3. `PaymentsService` converts the generated DTO into the internal
   `PaymentPostingRequest` (`Sap` namespace), calls
   `ISapConnector.PostPaymentAsync`.
4. `SapBapiSimulator` "calls" `BAPI_ACC_DOCUMENT_POST`, checks if the
   vendor exists and isn't blocked, returns a `BapiResult` with a
   `BAPIRET2`-style return table.
5. `PaymentsService` translates that back into the generated
   `PaymentInitiationResponse` shape, checking `HasErrors` to decide
   POSTED vs REJECTED.
6. Back in the controller, `Response.StatusCode` is set explicitly — 202
   for posted, 400 for rejected — because NSwag's generated method
   signature returns a raw DTO, not `ActionResult<T>`, so this has to be
   done by hand.
7. If anything throws unexpectedly, `GlobalExceptionMiddleware` catches
   it — `KeyNotFoundException` → 404, anything else → 502, both wrapped
   in the `ProblemDetails` shape the spec defines.

Every other endpoint follows this same shape: Controller → domain
Service → `ISapConnector` → simulator, with the service doing the DTO
translation in both directions.

## The three layers of testing, and what each one proves

- **Unit tests** (`SapBapiSimulatorTests`, `PaymentsServiceTests`) — pure
  logic, mocked dependencies. Prove the *mapping* logic is correct
  (BAPIRET2 → REST status) without anything running.
- **Integration tests** (`PaymentsApiIntegrationTests`) — real ASP.NET
  Core pipeline via `WebApplicationFactory`, real `SapBapiSimulator`, no
  Vault/Docker needed (the startup Vault call is wrapped in try/catch).
  Prove routing, DI, and middleware actually work together.
- **Bruno E2E** — the only layer that hits a genuinely running process
  over real HTTP. Proves the whole stack including Kestrel and actual
  serialization/status codes — this is the layer that caught the
  200-vs-202 status code bug, which mocked unit/integration tests never
  would have, since the controller layer itself wasn't exercised by them.
  It also caught a JWT-ordering bug: the CLI's default folder discovery
  order isn't alphabetical the way you'd assume, so the `Auth` folder ran
  *last* and every protected request failed with 401 before a token
  existed — fixed with explicit `folder.bru` sequencing (`Auth` → seq 1).

That layering — logic, wiring, then the real thing — is the answer if
asked "why three kinds of tests?" in an interview.

## Infrastructure pieces and why each exists

- **Vault** — replaces hardcoded/appsettings secrets. `VaultService`
  fetches SAP RFC credentials *and* the JWT signing key at startup;
  `docker-compose.yml` runs a throwaway dev instance locally and seeds
  both secrets.
- **JWT bearer authentication** — added after a review flagged the API
  had none. `[Authorize]` on `SapAdapterController` requires a valid
  token on all 5 operations; `Program.cs` validates issuer, audience,
  lifetime, and signature. The signing key is symmetric (HMAC) and pulled
  from Vault — explicitly a hands-on-practice simplification, not the
  production pattern (real deployment: validate against an IDP's public
  keys via RS256 + JWKS, not a shared secret). `POST /dev/token` mints
  test tokens, gated to `Development` environment only, since it must
  never exist in a real deployment.
- **GitHub Actions CI** — `spec-lint` (catches bad OpenAPI before it
  becomes bad code) → `codegen-diff-check` (catches someone forgetting
  to regenerate) → `build-and-test` → `sonarqube` (quality gate) +
  `claude-pr-review` (agentic first-pass review, human stays the real
  approver) → `bruno-e2e` (the same test run locally, run against a
  fresh instance in CI).

## What's proven vs. still assumed

Proven, on a real machine: build succeeds, app runs, Vault integration
works (both secrets), `dotnet test` passes 11/11, and the full Bruno E2E
suite passes 7/7 requests / 12/12 tests — including the complete JWT
auth flow from token minting through every protected endpoint.

Still open: the CI pipeline has never executed against a real GitHub
Actions runner, the payment-status Bruno test still returns 404 because
it hardcodes a doc number that doesn't match the simulator's randomly
generated ones (a test-data gap, not a bug), there's no RBAC/scope
enforcement beyond "valid token = full access," and the Node.js port
doesn't exist yet.