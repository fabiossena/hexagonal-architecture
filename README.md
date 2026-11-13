# OrderFlow — Order Payment Service (Hexagonal Architecture · .NET 8)

A **payment service for orders**, used to demonstrate **Hexagonal Architecture
(Ports & Adapters)** end-to-end: a rich domain model, a framework-free
application layer, inbound/outbound adapters and a composition root.

It charges an order through **Stripe**, **Mercado Pago** or **Asaas** (cards,
Pix, Boleto, bank transfer), and ships a **browser test console** that exercises
every payment type and shows the raw provider answers.

> Every document, comment and XML doc in this repository is written in English.

```
Place an order → confirm → charge it (card · Pix · Boleto · transfer)
→ follow the status (webhook or refresh) → refund it
```

The service also includes an **offline simulator**, so the whole flow — API,
persistence and test console — works **without any credential**.

---

## Table of contents

1. [Why Hexagonal](#why-hexagonal)
2. [Architecture](#architecture)
3. [Domain model and business rules](#domain-model-and-business-rules)
4. [Payment providers](#payment-providers)
5. [Project layout](#project-layout)
6. [The test console](#the-test-console)
7. [iAppForms compatibility](#iappforms-compatibility)
8. [API reference](#api-reference)
9. [Configuration](#configuration)
10. [Running](#running)
11. [Tests](#tests)
12. [Design decisions](#design-decisions)

---

## Why Hexagonal

The application has **one** reason to touch the outside world on each side:

* **Inbound**: HTTP (REST) — controllers, provider webhooks and the test console.
* **Outbound**: persistence (PostgreSQL), time and the **payment providers**.

Hexagonal Architecture makes those edges *replaceable* and keeps the business
rules independent of them:

* the **domain** never references a framework, a database or a payment SDK;
* the **application** declares the ports it needs (`IPaymentRepository`,
  `IPaymentProvider`, `IPaymentProviderResolver`, `IUnitOfWork`, `IClock`) and
  orchestrates the use cases;
* the **adapters** implement those ports (EF Core, ASP.NET controllers, system
  clock, Stripe/Mercado Pago/Asaas clients);
* the **composition root** (`Program.cs`) is the only place that knows every
  concrete type.

The payoff is visible in the tests: the whole application layer is exercised with
in-memory fakes — no database, no web server and **no payment provider**.

---

## Architecture

```
                        ┌───────────────────────────────────────────────┐
   HTTP request ───────▶│ INBOUND ADAPTER                               │
   (test console)       │ Adapters.Inbound                              │
                        │   PaymentsController · ProvidersController     │
                        │   ProviderWebhookController                    │
                        │   DomainExceptionHandler → RFC 9457           │
                        └───────────────────────┬───────────────────────┘
                                                │ calls
                        ┌───────────────────────▼───────────────────────┐
                        │ APPLICATION                                   │
                        │   Use cases: Create · Get · List · Refund ·    │
                        │              Cancel · Refresh · Callback      │
                        │   Ports: IPaymentRepository · IPaymentProvider │
                        │          IPaymentProviderResolver ·            │
                        │          IOrderRepository · IUnitOfWork · IClock│
                        └───────────────────────┬───────────────────────┘
                                                │ uses
                        ┌───────────────────────▼───────────────────────┐
                        │ DOMAIN                        (no framework)  │
                        │   Payment (aggregate) · Money (value object)  │
                        │   PaymentProvider · PaymentMethod ·            │
                        │   PaymentStatus · PaymentPolicy                │
                        └───────────────────────┬───────────────────────┘
                                                ▲
                                                │ implements ports
                        ┌───────────────────────┴───────────────────────┐
                        │ OUTBOUND ADAPTERS                            │
                        │ Adapters.Outbound                            │
                        │   PaymentRepository · UnitOfWork (EF Core)   │
                        │   OrderFlowDbContext + migrations (Postgres) │
                        │   SystemClock                                │
                        │   Stripe · MercadoPago · Asaas · Simulator   │
                        └───────────────────────────────────────────────┘
```

**Dependency rule:** arrows never point outward. `OrderFlow.Domain` has **zero**
package references; `OrderFlow.Application` references only the domain and the DI
abstractions; the adapters depend on the application ports.

---

## Domain model and business rules

### Aggregates

```
Order (aggregate root)                     Payment (aggregate root)
├── Id, CustomerId                         ├── Id, OrderId
├── Status: Draft → Confirmed → Cancelled  ├── Provider, Method
├── CreatedAtUtc, ConfirmedAtUtc?          ├── Amount: Money
└── Items: OrderItem[]                     ├── Status: Pending → Processing → Paid
                                           │           → Failed | Cancelled | Refunded
                                           ├── ProviderPaymentId, ProviderStatus
                                           ├── CheckoutUrl, PixCopyPasteCode, PixQrCodeBase64
                                           ├── FailureReason, ProviderPayload
                                           └── RefundedAmount, RefundableAmount
```

`Money` is a value object: non-negative, two decimals, ISO-4217 currency, and it
refuses cross-currency arithmetic.

### Rules enforced by the `Payment` aggregate

| Rule | Violation |
|---|---|
| The amount must be strictly positive | `ValidationException` → **400** |
| The provider must accept the chosen method | `ValidationException` → **400** |
| Only pending/processing payments can be paid, failed or cancelled | `ConflictException` → **409** |
| Only a **paid** payment can be refunded | `ConflictException` → **409** |
| The refund cannot exceed the refundable amount | `ValidationException` → **400** |
| Failed, cancelled and refunded payments are terminal | `ConflictException` → **409** |

### Payment status machine

```
Pending ──▶ Processing ──▶ Paid ──▶ Refunded
   │            │
   └──▶ Failed ┘        Cancelled
```

`PaymentPolicy` owns that matrix (and the provider/method matrix below) as pure
domain data — the application layer, the API and the test console all read the
same rules.

### Order rules (unchanged)

| Rule | Violation |
|---|---|
| A customer is required and timestamps must be UTC | `ValidationException` → **400** |
| Items can only be added/removed while **Draft** | `ConflictException` → **409** |
| An order can only be **confirmed** if it has ≥ 1 item | `ValidationException` → **400** |
| Cancel requires a reason (≤ 500 chars) and happens once | `ValidationException` → **400** / `ConflictException` → **409** |

---

## Payment providers

| Provider | Card | Pix | Boleto | Bank transfer | Endpoint |
|---|:---:|:---:|:---:|:---:|---|
| **Stripe** | ✅ | ✅ | ✅ | — | `POST /v1/payment_intents` (card) · `POST /v1/checkout/sessions` (Pix/Boleto) |
| **Mercado Pago** | ✅ | ✅ | ✅ | ✅ | `POST /v1/payments` |
| **Asaas** | ✅ | ✅ | ✅ | — | `POST /v3/payments` |
| **Simulated** | ✅ | ✅ | ✅ | ✅ | in-process (no HTTP) |

* **Stripe** charges cards through a PaymentIntent using Stripe's test payment
  method (`pm_card_visa`), so the sandbox answers immediately. Pix and Boleto
  need an extra confirmation in the browser: a **Checkout Session** is created
  and its URL is returned.
* **Mercado Pago** covers every instrument with one endpoint; the
  `payment_method_id` selects the method. Cards require a token produced by
  Mercado Pago's own SDK.
* **Asaas** requires a customer: the adapter creates one on demand from the
  e-mail and the tax id. Cards require a token produced by Asaas' SDK.
* **Simulated** answers like a provider without calling one: cards are
  confirmed immediately, Pix/Boleto/transfer stay pending and are confirmed
  after `Payments:Simulated:AutoPayAfterSeconds` (the "refresh" button of the
  test console shows that transition). The console can also ask for a
  simulated **failure**, so the error path is reachable too.

---

## Project layout

```
orderflow/
├── OrderFlow.sln
├── Directory.Build.props            # shared compiler settings (Nullable, C# 12, XML docs)
├── Dockerfile                       # multi-stage .NET 8 build
├── Dockerfile.runtime               # runtime-only image (offline builds)
├── docker-compose.yml               # PostgreSQL + API
├── .config/dotnet-tools.json        # pinned dotnet-ef version
├── src/
│   ├── OrderFlow.Domain/            # Payment, Money, policies, domain exceptions
│   ├── OrderFlow.Application/       # use cases, DTOs, outbound ports
│   ├── OrderFlow.Adapters.Inbound/  # controllers, contracts, problem-details mapping
│   ├── OrderFlow.Adapters.Outbound/ # EF Core + PostgreSQL, providers, migrations
│   ├── OrderFlow.Integration.IAppForms/ # iAppForms compatibility surface (anti-corruption)
│   └── OrderFlow.Api/               # composition root (Program.cs), appsettings, test console
└── tests/
    ├── OrderFlow.Domain.Tests/      # 97 tests
    ├── OrderFlow.Application.Tests/ # 44 tests
    └── OrderFlow.Integration.IAppForms.Tests/ # 37 tests
```

| Layer | May depend on | Contains |
|---|---|---|
| Domain | — | `Payment`, `Order`, `OrderItem`, `Money`, `PaymentProvider`, `PaymentMethod`, `PaymentStatus`, `PaymentPolicy`, `VolumeDiscountPolicy`, exceptions |
| Application | Domain | `CreatePaymentUseCase`, `GetPaymentUseCase`, `ListPaymentsUseCase`, `RefundPaymentUseCase`, `CancelPaymentUseCase`, `RefreshPaymentStatusUseCase`, `ListPaymentProvidersUseCase`, `ApplyProviderCallbackUseCase`, `IPaymentRepository`, `IPaymentProvider`, `IPaymentProviderResolver`, `IUnitOfWork`, `IClock`, DTOs |
| Adapters.Inbound | Application, Domain | `PaymentsController`, `ProvidersController`, `ProviderWebhookController`, request contracts, `DomainExceptionHandler` |
| Adapters.Outbound | Application, Domain | `OrderFlowDbContext`, `PaymentRepository`, `OrderRepository`, `UnitOfWork`, `SystemClock`, `StripePaymentProvider`, `MercadoPagoPaymentProvider`, `AsaasPaymentProvider`, `SimulatedPaymentProvider`, `PaymentProviderResolver`, EF migrations |
| Integration.IAppForms | Application, Domain | `CheckoutPaymentsController`, `CheckoutWebhookController`, `IAppFormsVocabulary`, `IAppFormsPaymentSurface`, `CheckoutExceptionHandler` — the anti-corruption layer |
| Api | all | `Program.cs` (composition root), Swagger, health checks, `wwwroot/index.html` (test console) |

---

## The test console

Open **`http://localhost:5080`** (or `http://localhost:8080` with Docker) and
you get the payment test console:

1. **Provider & payment type** — the provider cards come from
   `GET /api/providers`; each card shows whether the provider is configured.
   The method chips change with the selected provider.
2. **Charge details** — amount, installments, customer e-mail/name, tax id,
   card token.
3. **Create & confirm test order** — the console creates and confirms an order
   for you, so you never need to prepare one by hand.
4. **Charge test payment** — calls `POST /api/payments` and renders the
   **result**: status badge, provider id, provider status, checkout URL, the
   **Pix QR code and copy-and-paste code**, the failure reason and the **raw
   provider payload**.
5. **Payments created in this session** — a table with `refresh`, `refund` and
   `cancel` actions per payment, and a live log of every API call.

Nothing is hard-coded in the console: the provider list, the accepted methods
and the "configured" flags all come from the API.

The console has **two tabs**:

* **Native API** — the OrderFlow endpoints (`/api/orders`, `/api/payments`).
* **iAppForms compatibility** — the `/api/checkout/*` surface: create a quote,
  charge it with an `Idempotency-Key`, read the status and simulate a provider
  webhook — all in the iAppForms vocabulary.

---

## iAppForms compatibility

The service can be used as the **payment backend of the iAppForms module**,
without changing a single line of the iAppForms front-end.

### What is exposed

| Method | Route | iAppForms counterpart |
|---|---|---|
| `POST` | `/api/checkout/payments` | `CheckoutPaymentsController` — same route, same body, same answer |
| `GET` | `/api/checkout/payments/{id}` | `PaymentStatusDto` |
| `POST` | `/api/checkout/webhooks/{provider}` | `CheckoutWebhookController` |

The bodies are the ones iAppForms already publishes:

```jsonc
// POST /api/checkout/payments   (header: Idempotency-Key)
{ "quoteId": "01e75ab5-…", "paymentMethodCode": "Pix", "recurring": false }

// 201 Created
{
  "paymentId": "8b9c168b-…",
  "status": "InReview",          // commercial status
  "providerStatus": "Pending",   // settlement status
  "provider": "Simulated",
  "checkoutUrl": null,
  "instructions": {
    "methodCode": "Pix",
    "qrCodeImage": "PHN2ZyB4bWxucz0…",
    "copyPaste": "00020126580014BR.GOV.BCB.PIX0136…",
    "barcode": null,
    "url": null,
    "fields": null
  },
  "reused": false
}
```

Errors keep the iAppForms shape: RFC 9457 `problem+json` with the structured
`code` extension (`checkout.quote_not_found`, `checkout.unsupported_combination`,
`checkout.provider_unavailable`, …).

### The quote is an order

iAppForms works with **quotes**; OrderFlow works with **orders**. The bridge is
the `externalReference` of the order:

1. the integration creates the order with `externalReference = quoteId`;
2. the integration confirms it (a confirmed order is a payable one);
3. iAppForms calls `POST /api/checkout/payments` with that `quoteId`;
4. OrderFlow finds the order back and charges it.

The amount and the currency always come from the order — the quote snapshot —
so the two systems can never disagree about how much was charged.

### The vocabulary bridge

`IAppFormsVocabulary` (in `OrderFlow.Integration.IAppForms`) is the
**anti-corruption layer**: the domain keeps its own single status and never
learns the iAppForms tokens.

| OrderFlow status | iAppForms commercial (`status`) | iAppForms provider (`providerStatus`) |
|---|---|---|
| `Pending` / `Processing` | `InReview` | `Pending` |
| `Paid` | `Approved` | `Paid` |
| `Failed` *(provider answered)* | `Rejected` | `Failed` |
| `Failed` *(provider unreachable)* | `Error` | `Failed` |
| `Cancelled` | `Cancelled` | `Cancelled` |
| `Refunded` | `Approved` | `Refunded` |

The method codes are the same tokens (`Pix`, `CreditCard`, `DebitCard`,
`Boleto`) and the provider names are the same adapter names (`Stripe`,
`MercadoPago`, `Asaas`, `Simulated`).

### Safety

* **Idempotency.** The `Idempotency-Key` header is stored in a **unique**
  column. Replaying it returns the very same payment (`"reused": true`) and
  never charges the customer twice. Reusing a key on another order is a **409**.
* **Shared secret.** When `IAppForms:ApiKey` is set, every call must carry the
  `X-Api-Key` header, otherwise the answer is **401**. Leave it empty only for
  development.
* **Provider routing** follows the iAppForms rules and is configurable
  (`IAppForms:ProviderPreferenceByMethod`). The simulator is the last resort,
  so the surface never fails for lack of credentials.

### Pointing iAppForms at this service (the safe switch)

The iAppForms module already has a provider port (`IPaymentProvider`) with
adapters for Stripe, Mercado Pago, Asaas and a simulator. To move the payments
here, in a safe and simple way:

1. **keep** the current adapters — they are the fallback;
2. add **one** adapter named `OrderFlow` in
   `IAppForms.Authorization.Infrastructure/Services/Checkout/Providers/`
   that speaks the `/api/checkout/*` surface (the DTOs are already mapped by
   `CheckoutSurfaceContracts.cs` here);
3. register it in `DependencyInjection.cs` and add a line to
   `ProviderRouter.RuleProvider` — **only** for the domains you want to move;
4. configure `Payments:Providers:OrderFlow:BaseUrl` with the URL of this
   service and `WebhookSecret` with the `IAppForms:ApiKey` value;
5. roll out **one domain first** and watch the reconciliation job.

Nothing else changes: the plan lifecycle, the 72h confirmation window, the
coupons and the `NotificationQueue` stay exactly where they are.

### Roadmap — the advanced step (RabbitMQ)

When the traffic grows, the HTTP call becomes the message:

```
iAppForms ──publish──▶ [payment.commands] ──▶ OrderFlow consumer
                            │                        │
                            │                   charge the PSP
                            ▼                        │
                       [payment.results] ◀──publish──┘
```

The pieces are already in place for that:

* the command is the same `PaymentSurfaceRequest` (+ the `Idempotency-Key`);
* the consumer would live in the same integration project
  (`OrderFlow.Integration.IAppForms`) — it is an inbound adapter too;
* the answer is the same `PaymentSurfaceDto` / `PaymentStatusDto`;
* the **source of truth stays here**: the payment, the status, the provider
  reference and the idempotency key live in OrderFlow, so the iAppForms side
  only mirrors what it needs for the plan lifecycle.

The HTTP surface can stay as the *synchronous* path while the queue handles
the asynchronous confirmations (Pix/boleto settle later).

---

## API reference

Base URL: `http://localhost:5080` (local) or `http://localhost:8080` (Docker).
JSON uses **camelCase**; enums travel as readable strings.

### `GET /api/providers` — provider catalogue

```json
[
  {
    "provider": "Simulated",
    "displayName": "Simulated",
    "methods": ["CreditCard", "DebitCard", "Pix", "Boleto", "BankTransfer"],
    "isConfigured": true,
    "isSimulation": true
  }
]
```

### `POST /api/payments` — charge an order

```bash
curl -X POST http://localhost:5080/api/payments \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: checkout-123" \
  -d '{
    "orderId": "b74ad1d0-8943-4af9-98c7-e985c6b0092a",
    "provider": "Simulated",
    "method": "Pix",
    "amount": 149.90,
    "description": "Order 1234",
    "customerEmail": "buyer@example.com",
    "customerName": "Test Buyer",
    "customerDocument": "12345678901",
    "cardToken": null,
    "installments": 1,
    "simulateFailure": false
  }'
```

`201 Created`

```json
{
  "id": "02c6bb4a-01e0-4caf-89e0-71ccb50d713a",
  "orderId": "b74ad1d0-8943-4af9-98c7-e985c6b0092a",
  "provider": "Simulated",
  "method": "Pix",
  "amount": { "amount": 149.9, "currency": "BRL" },
  "status": "Processing",
  "description": "Order 1234",
  "customerEmail": "buyer@example.com",
  "providerPaymentId": "sim_b3c7830715f54615854af7d69373a890",
  "providerStatus": "pending",
  "checkoutUrl": null,
  "pixCopyPasteCode": "00020126580014BR.GOV.BCB.PIX0136…",
  "pixQrCodeBase64": "PHN2ZyB4bWxucz0naHR0cDovL3d3dy53My5vcmcv…",
  "failureReason": null,
  "refundedAmount": { "amount": 0, "currency": "BRL" },
  "refundableAmount": { "amount": 149.9, "currency": "BRL" },
  "createdAtUtc": "2026-11-13T00:02:49.5528379Z",
  "updatedAtUtc": "2026-11-13T00:02:49.5528379Z",
  "paidAtUtc": null,
  "cancelledAtUtc": null,
  "providerPayload": "{\"id\":\"sim_b3c7…\",\"status\":\"pending\", …}"
}
```

The **amount defaults to the order total** and the **currency is always the
order currency** — a payment can never be charged in another currency.

### The other payment endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/payments` | Charge an order (`Idempotency-Key` header optional) |
| `GET` | `/api/payments/{id}` | Read a payment |
| `GET` | `/api/payments?orderId=&provider=&status=&page=&size=` | List payments (page size clamped to 1..100) |
| `POST` | `/api/payments/{id}/refund` | Refund (body `{"amount": 50}` optional — full refund by default) |
| `POST` | `/api/payments/{id}/cancel` | Cancel a payment that was not captured (body `{"reason": "…"}`) |
| `POST` | `/api/payments/{id}/refresh` | Ask the provider for the current status and sync it |
| `POST` | `/api/webhooks/{provider}` | Status notification sent by the provider |
| `GET` | `/api/providers` | Provider catalogue |

### Order endpoints (unchanged)

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/orders` | Create a draft order |
| `GET` | `/api/orders/{id}` | Read an order |
| `GET` | `/api/orders?status=&page=&size=` | List orders |
| `POST` | `/api/orders/{id}/confirm` | Confirm an order (required before paying it) |
| `POST` | `/api/orders/{id}/cancel` | Cancel an order |

### Status codes

| Code | Meaning |
|---|---|
| `200` | Success (read, refund, cancel, refresh) |
| `201` | Payment created |
| `400` | Business validation failure (RFC 9457 `problem+json`) |
| `404` | Order or payment not found |
| `409` | State conflict (e.g., cancelling a captured payment) |
| `502` | The provider call failed — the raw provider payload is attached to the problem details as `providerPayload` |

Interactive documentation: **`/swagger`**.

---

## Configuration

Every setting is read from `appsettings.json` (or the environment). The
standard .NET convention applies: `Section__Key=value` overrides a JSON value.

### `appsettings.json`

```json
{
  "ConnectionStrings": {
    "Orders": "Host=localhost;Port=5432;Database=orderflow;Username=orderflow;Password=orderflow"
  },
  "Payments": {
    "CallbackUrl": "http://localhost:5080/api/webhooks",
    "Stripe": {
      "ApiKey": "",
      "BaseUrl": "https://api.stripe.com",
      "Currency": "brl",
      "SuccessUrl": "http://localhost:5080/payments/success",
      "CancelUrl": "http://localhost:5080/payments/cancel",
      "CardPaymentMethod": "pm_card_visa",
      "Enabled": true
    },
    "MercadoPago": {
      "AccessToken": "",
      "BaseUrl": "https://api.mercadopago.com",
      "NotificationUrl": "http://localhost:5080/api/webhooks/mercadopago",
      "StatementDescriptor": "ORDERFLOW",
      "Enabled": true
    },
    "Asaas": {
      "AccessKey": "",
      "BaseUrl": "https://api.asaas.com/v3",
      "DueDays": 3,
      "AutoCreateCustomer": true,
      "Enabled": true
    },
    "Simulated": {
      "AutoPayAfterSeconds": 15,
      "Enabled": true
    }
  }
}
```

### Settings reference

| Setting | Default | Purpose |
|---|---|---|
| `ConnectionStrings:Orders` | `Host=localhost;Port=5432;Database=orderflow;Username=orderflow;Password=orderflow` | PostgreSQL connection string |
| `Payments:CallbackUrl` | `http://localhost:5080/api/webhooks` | Base URL the providers call back (the provider name is appended) |
| `Payments:Stripe:ApiKey` | *(empty)* | Stripe secret key — `sk_test_…` for the sandbox |
| `Payments:Stripe:BaseUrl` | `https://api.stripe.com` | API base address (change it to point at a mock) |
| `Payments:Stripe:Currency` | `brl` | Currency used by Stripe charges (lower case) |
| `Payments:Stripe:SuccessUrl` | `http://localhost:5080/payments/success` | Redirect target after a Checkout |
| `Payments:Stripe:CancelUrl` | `http://localhost:5080/payments/cancel` | Redirect target when the customer abandons a Checkout |
| `Payments:Stripe:CardPaymentMethod` | `pm_card_visa` | Stripe test payment method used by card charges |
| `Payments:Stripe:Enabled` | `true` | Disables the adapter entirely when `false` |
| `Payments:MercadoPago:AccessToken` | *(empty)* | Mercado Pago access token — `APP_USR-…` for the test account |
| `Payments:MercadoPago:BaseUrl` | `https://api.mercadopago.com` | API base address |
| `Payments:MercadoPago:NotificationUrl` | `http://localhost:5080/api/webhooks/mercadopago` | URL Mercado Pago notifies |
| `Payments:MercadoPago:StatementDescriptor` | `ORDERFLOW` | Text shown on the customer's statement |
| `Payments:MercadoPago:Enabled` | `true` | Disables the adapter entirely when `false` |
| `Payments:Asaas:AccessKey` | *(empty)* | Asaas access key — `$sandbox_access_token_…` for the sandbox |
| `Payments:Asaas:BaseUrl` | `https://api.asaas.com/v3` | API base address |
| `Payments:Asaas:DueDays` | `3` | Days until the payment is due (Boleto/Pix) |
| `Payments:Asaas:AutoCreateCustomer` | `true` | Creates the Asaas customer on demand |
| `Payments:Asaas:Enabled` | `true` | Disables the adapter entirely when `false` |
| `Payments:Simulated:AutoPayAfterSeconds` | `15` | Delay before a pending simulated payment is confirmed |
| `Payments:Simulated:Enabled` | `true` | Disables the simulator when `false` |
| `IAppForms:Enabled` | `true` | Exposes the `/api/checkout/*` compatibility surface |
| `IAppForms:ApiKey` | *(empty)* | Shared secret expected in the `X-Api-Key` header (empty = open, development only) |
| `IAppForms:DefaultProvider` | *(empty)* | Provider used when no preferred one is configured |
| `IAppForms:ProviderPreferenceByMethod` | see `appsettings.json` | Provider preference per method (`Pix`, `CreditCard`, `DebitCard`, `Boleto`, `BankTransfer`) |
| `ASPNETCORE_URLS` | `http://localhost:5080` (local) / `http://+:8080` (Docker) | Listening address |
| `ASPNETCORE_ENVIRONMENT` | `Production` (Docker) / `Development` (local) | Enables Swagger + verbose EF logs |

A provider without credentials is **not** an error: it simply shows up as
`isConfigured: false` in the catalogue and answers
`400 — Provider 'Stripe' is not configured.`

### Environment variables

```bash
# Stripe (sandbox)
export Payments__Stripe__ApiKey="sk_test_51..."

# Mercado Pago (test account)
export Payments__MercadoPago__AccessToken="APP_USR-00000000-0000-0000-0000-000000000000"

# Asaas (sandbox)
export Payments__Asaas__AccessKey="$sandbox_access_token_your-key"

# Database
export ConnectionStrings__Orders="Host=localhost;Port=5432;Database=orderflow;Username=orderflow;Password=orderflow"
```

In **Docker Compose** the same variables are passed to the `api` service:

```yaml
environment:
  Payments__Stripe__ApiKey: "${STRIPE_API_KEY:-}"
  Payments__MercadoPago__AccessToken: "${MERCADOPAGO_ACCESS_TOKEN:-}"
  Payments__Asaas__AccessKey: "${ASAAS_ACCESS_KEY:-}"
```

```bash
STRIPE_API_KEY=sk_test_51... docker compose up --build
```

### Getting sandbox credentials

| Provider | Where |
|---|---|
| **Stripe** | Dashboard → *Developers* → *API keys* → copy the **Secret key** (`sk_test_…`). Test cards: `4242 4242 4242 4242` (the adapter uses the `pm_card_visa` token, no card number needed). |
| **Mercado Pago** | Developer panel → *Credentials* → **Test access token** (`APP_USR-…`). Cards need a token from their JS SDK (`MP_TOKEN` in the console). |
| **Asaas** | Dashboard → *API keys* → copy the **sandbox access key** (`$sandbox_access_token_…`). Cards need a token from their SDK. |

### Provider webhooks

Point the provider dashboard at
`http://localhost:5080/api/webhooks/{stripe|mercadopago|asaas}` (use a tunnel
such as `ngrok` when the API runs on your machine). The handler extracts the
payment reference and the raw status, and the **adapter** translates the status
into the domain vocabulary — the HTTP layer never knows the vendor.

---

## Running

### Docker Compose (recommended)

```bash
docker compose up --build
```

| Endpoint | URL |
|---|---|
| **Test console** | http://localhost:8080 |
| API | http://localhost:8080 |
| Swagger | http://localhost:8080/swagger |
| Health | http://localhost:8080/health |
| PostgreSQL | `localhost:5432` (`orderflow` / `orderflow`) |

Use `API_PORT` and `POSTGRES_PORT` to avoid port clashes:

```bash
API_PORT=8090 POSTGRES_PORT=55432 docker compose up --build
```

### Local (`dotnet run`)

```bash
docker compose up -d postgres
dotnet run --project src/OrderFlow.Api
```

The API listens on **http://localhost:5080** (see `launchSettings.json`).
On first start it applies the EF Core migrations and creates the schema.

### Offline / restricted-network image

When the .NET SDK base image cannot be pulled, publish on the host and assemble
the runtime-only image (same binaries and behaviour):

```bash
dotnet publish src/OrderFlow.Api -c Release -o publish
docker build --pull=false -f Dockerfile.runtime -t orderflow/api:1.0.0 .
docker compose up -d --no-build
```

### Database migrations

The `dotnet-ef` version is pinned in `.config/dotnet-tools.json`:

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name> \
  -p src/OrderFlow.Adapters.Outbound \
  -s src/OrderFlow.Api \
  -o Persistence/Migrations
```

The host applies pending migrations automatically on startup.

---

## Tests

```bash
dotnet build OrderFlow.sln
dotnet test  OrderFlow.sln
```

| Suite | Tests | What it covers |
|---|---:|---|
| `OrderFlow.Domain.Tests` | **97** | payment aggregate invariants (transitions, refunds, cancellation), provider/method matrix, status machine, order invariants, `Money` arithmetic, `VolumeDiscountPolicy` |
| `OrderFlow.Application.Tests` | **44** | every payment use case with in-memory repositories, a fake provider and a fixed clock |
| `OrderFlow.Integration.IAppForms.Tests` | **37** | the iAppForms vocabulary translation, the checkout surface (quote lookup, idempotent replay, webhook) and the API-key guard |

The application tests prove the port abstraction: no database, no HTTP server,
**no payment provider** and no test containers are required.

---

## Design decisions

### Ports & adapters without ceremony

* **Ports live where they are needed.** `IPaymentRepository`,
  `IPaymentProvider`, `IPaymentProviderResolver`, `IUnitOfWork` and `IClock` are
  declared in `Application/Abstractions` because the use cases need them. The
  domain has no ports (it needs nothing).
* **One adapter per edge.** HTTP lives in `Adapters.Inbound`, persistence and
  the providers in `Adapters.Outbound`.
* **Errors are translated at the edge.** Domain exceptions carry meaning, not
  HTTP status codes; `DomainExceptionHandler` owns the mapping.
* **Time is a dependency.** Use cases receive `IClock`; tests use a fixed clock.

### The provider port

`IPaymentProvider` is the only thing the application knows about a payment
provider:

```csharp
PaymentProvider Provider { get; }
IReadOnlyCollection<PaymentMethod> SupportedMethods { get; }
bool IsConfigured { get; }
bool IsSimulation { get; }

Task<ProviderChargeResult> ChargeAsync(ProviderChargeRequest request, CancellationToken ct);
Task<ProviderChargeResult> GetChargeAsync(string providerPaymentId, CancellationToken ct);
Task<ProviderRefundResult> RefundAsync(string providerPaymentId, Money amount, CancellationToken ct);
PaymentStatus MapStatus(string providerStatus);
```

* The **vendor vocabulary stays in the adapter** — `MapStatus` is how a raw
  `"approved"` / `"succeeded"` / `"CONFIRMED"` becomes a domain status. The
  webhook handler and the use cases never learn the vendor's words.
* The **domain only knows identifiers** (`PaymentProvider.Stripe`), never a
  vendor SDK: no Stripe/Mercado Pago/Asaas type crosses the port.
* Adding a provider is therefore **additive**: write the adapter, register it
  in `OutboundServiceCollectionExtensions`, add the row to `PaymentPolicy`. No
  use case changes.

### Why the simulator

A payment service cannot be demonstrated without credentials. The **simulator**
answers like a provider (cards confirmed, Pix pending then confirmed, refunds,
failures) so that:

* the test console is useful **out of the box**;
* the CI has no external dependency;
* the error and pending paths are reachable on demand.

It is a first-class adapter (`IsSimulation = true`), never a special case in
the use cases.

### Failed attempts are recorded

When the provider call blows up, the payment is still persisted as **Failed**
(with the raw payload) and the error is rethrown as `502`. The audit trail keeps
the attempt and the caller still learns what happened.

### Cancellation is local

`POST /api/payments/{id}/cancel` abandons a payment that was not captured: the
customer never completes it. Providers that support explicit cancellation would
be called there.

### Persistence details

* The aggregate maps to the `payments` table; the amount is an owned `Money`
  (`amount` + `currency`).
* Enum values are stored as readable strings (`Stripe`, `Pix`, `Paid`).
* The raw provider payload and the Pix QR code use `text` columns.
* Computed values (`RefundableAmount`, `IsTerminal`) are **not persisted**.

---

## License

Sample project for study and portfolio purposes.
