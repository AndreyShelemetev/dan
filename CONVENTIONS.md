# CONVENTIONS.md — Pamyat Ryadom

Code, schema, and process conventions. The goal is predictability — for people and for AI agents.

## 1. Language

- Code, class/method/variable names, DTOs, API routes, branches, commits, and code comments —
  English.
- Product-facing text (UI copy, notifications, dispute messages) — Russian, since the audience is
  the Russian market. Keep it out of the backend where possible (frontend strings, not
  hardcoded server responses) so it stays easy to review and translate.
- Project documentation (`*.md`, `docs/`) — English, to keep it consistent with the rest of this
  repo's docs and readable by the whole team.

## 2. Backend (C# / ASP.NET Core)

### Style and naming

- `PascalCase` — classes, methods, properties, public members, types.
- `camelCase` — local variables and parameters.
- `_camelCase` — private fields.
- `IPascalCase` — interfaces (`I` prefix).
- One public type per file, filename matches the type name.
- Nullable reference types enabled.
- Async methods — `Async` suffix, return `Task` / `Task<T>`.

### Architectural rules

- Business logic lives in `Services/<Module>/`, **never** in controllers.
- Controllers are thin: validate input, call a service, map to a DTO.
- The public API accepts and returns **DTOs**, never domain entities.
- Data access goes through the EF Core `AppDbContext`.
- DB schema changes go **only** through EF Core migrations.

### Structure (reference)

```
backend/src/PamyatRyadom.Api/
├── Controllers/
├── Services/<Module>/
├── Models/          # domain models
├── Dtos/<Module>/
├── Data/            # DbContext, IEntityTypeConfiguration<T>, Migrations/
└── Program.cs
```

## 3. Database conventions

### IDs

Every table has a `bigint identity` primary key named `id`. Foreign key columns are named
`<referenced_entity>_id`. All C# property names stay PascalCase; `EFCore.NamingConventions`
maps them to snake_case columns in Postgres automatically.

```csharp
public sealed class Visit
{
    public long Id { get; set; }
    public long OrderId { get; set; }          // -> order_id
    public long? ExecutorId { get; set; }       // -> executor_id
    public string Status { get; set; } = null!; // see "Status" below
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### Constraint naming

Use the `pk_` / `fk_` / `ck_` / `ux_` / `ix_` prefixes consistently:

```csharp
builder.HasKey(v => v.Id).HasName("pk_visits");

builder.HasOne<Order>()
    .WithMany()
    .HasForeignKey(v => v.OrderId)
    .HasConstraintName("fk_visits_orders_order_id");

builder.HasIndex(v => v.ExecutorId).HasDatabaseName("ix_visits_executor_id");

builder.HasIndex(o => o.OrderRef).IsUnique().HasDatabaseName("ux_orders_order_ref");
```

### Money

Money is always `numeric(12,2)` mapped to C# `decimal` — **never** `float`/`double`, on either
side of the wire (DB column, entity property, or DTO).

```csharp
public sealed class Estimate
{
    public long Id { get; set; }
    public decimal TotalAmount { get; set; } // numeric(12,2)
}

builder.Property(e => e.TotalAmount).HasColumnType("numeric(12,2)");
```

### Status

Status columns are `text` plus a named `CHECK` constraint — no Postgres enum type, no separate
status lookup table.

```csharp
builder.Property(v => v.Status).HasColumnType("text").IsRequired();
builder.ToTable(t => t.HasCheckConstraint(
    "ck_visits_status",
    "status IN ('scheduled','en_route','in_progress','completed','missed','cancelled')"));
```

Soft-delete is a `status` value (e.g. `'cancelled'`, `'deleted'`), never a `deleted_at` column —
the row stays queryable through the same status machinery as every other state transition.

### Audit columns

Every mutable table has `created_at` and `updated_at` as `timestamptz`. `AppDbContext` stamps
both automatically in `SaveChanges`/`SaveChangesAsync` — do not set them by hand in application
code.

```csharp
builder.Property(v => v.CreatedAt).HasColumnType("timestamptz");
builder.Property(v => v.UpdatedAt).HasColumnType("timestamptz");
```

## 4. Frontend (Next.js + React + TypeScript)

### Style and naming

- TypeScript required, `strict` mode.
- Components — `PascalCase` (`VisitReportCard.tsx`).
- Hooks — `useCamelCase` (`useOrderStatus.ts`).
- Utilities/variables — `camelCase`.
- One component per file; components stay small and reusable.

### Rules

- Tailwind CSS, **mobile-first** (base styles are mobile, then `sm:`, `md:`, `lg:`).
- Loading / empty / error states are required wherever a page fetches data.
- Read requests come from Server Components; writes (place order, upload evidence, open a
  dispute) go through client components hitting the backend REST API.
- Never render executor payout amounts, margins, or internal pricing on any client-facing page.

### Structure (reference)

```
frontend/
├── app/              # Next.js App Router routes
├── components/
├── lib/              # api client, utilities
└── public/
```

## 5. Git

### Branches

- `main` — stable branch.
- `feature/<short-description>` — new features.
- `fix/<short-description>` — bug fixes.

### Commits

Conventional Commits, in English:

```
feat: add visit dispatch assignment endpoint
fix: correct idempotency key check on payment webhook
docs: document the module boundaries in ARCHITECTURE.md
chore: pin dotnet-ef local tool version
```

- Small, atomic commits.
- The message explains *what* and *why*.

## 6. Database source of truth

- The source of truth for the schema is the EF Core migrations in
  `backend/src/PamyatRyadom.Api/Data/Migrations/`.
- Table/column names follow EF Core + `EFCore.NamingConventions` (see §3).

## 7. Security & PII (short — full rules in CLAUDE.md)

- Auth: email/SMS OTP, no passwords in MVP. Sessions are an opaque token in an HttpOnly cookie;
  only the hash is stored server-side.
- PII (name, phone, email, address, coordinates, OTP codes) never goes into free-form logs.
- Personal data of RF citizens is stored on RF-hosted infrastructure in production.
- Money is `numeric(12,2)` decimal everywhere, never `float`/`double`.
- Executor payout amounts and margins are commercially confidential — never in user-facing
  content.

## 8. Definition of Done

A task is done only when:

- the backend builds (`dotnet build`) and the frontend builds (`npm run build`);
- migrations are created if the DB schema changed, and no hand-written DDL was used;
- mobile layout is not broken;
- loading / empty / error states exist where relevant;
- PII and commercial-confidentiality rules are respected;
- documentation (`ARCHITECTURE.md`, `CONVENTIONS.md`, or a new ADR) is updated if behavior or
  structure changed.
