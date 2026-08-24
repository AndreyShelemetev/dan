# ADR-001: Reuse the NaidiAI stack instead of a fresh Node/Prisma stack

## Status

Accepted — 2026-08-21

## Context

Pamyat Ryadom is a new MVP built by a small team on a tight timeline. A sibling project,
NaidiAI, already runs a battle-tested ASP.NET Core + EF Core/Npgsql + PostgreSQL backend paired
with a Next.js + TypeScript + Tailwind frontend, deployed via Docker Compose to a single VPS
behind nginx. NaidiAI also already has a working passwordless OTP auth flow, a session-cookie
model, and the beginnings of a payment integration, none of which Pamyat Ryadom has yet.

The alternative under consideration was a fresh Node.js + Prisma + PostgreSQL stack, which is a
common default for a new small-team web app in 2026.

## Decision

Adopt NaidiAI's actual stack — ASP.NET Core (.NET 10) + EF Core/Npgsql + PostgreSQL 17 on the
backend, Next.js 14 App Router + TypeScript strict + Tailwind CSS 3.4 on the frontend, Docker
Compose with the same service shape (postgres/api/frontend + adminer in dev) — instead of
building a new Node/Prisma stack from scratch.

Rationale:

- It lets us **port**, not rebuild, NaidiAI's OTP auth flow, session-cookie handling, and
  payment-integration scaffolding — the highest-risk, highest-effort parts of any new backend.
- The two projects can share engineering conventions, tooling, and institutional knowledge
  (the same team maintains both), which lowers the ongoing maintenance cost of running two
  stacks.
- The deploy shape (single VPS, Docker Compose, nginx) is already proven in production for
  NaidiAI and needs no new ops work to stand up.
- A fresh Node/Prisma stack would have meant re-solving auth, sessions, and payments from zero
  with no code to port, for no clear benefit at MVP stage.

## Deviations from the NaidiAI pattern

Not everything is copied as-is — Pamyat Ryadom's domain has different requirements:

- **bigint identity primary keys, not UUID.** NaidiAI already uses `bigint identity` PKs named
  `id`; Pamyat Ryadom keeps this rather than switching to UUIDs, since the domain has no
  multi-tenant/offline-generation requirement that would justify UUIDs, and bigint keys are
  smaller and faster to index.
- **`numeric(12,2)` decimal money, not integer minor units.** Orders, estimates, and payouts are
  modeled as `decimal` mapped to `numeric(12,2)`, matching NaidiAI's convention, rather than
  storing money as integer kopecks — this keeps the money-handling convention identical across
  both codebases instead of introducing a second convention to remember.
- **Private S3-compatible object storage, not local disk.** NaidiAI stores AI-generated images
  on local disk with short retention. Pamyat Ryadom's photo/video evidence is a permanent private
  record of work performed, so it uses a private S3-compatible bucket in a Russian region
  (Yandex Object Storage / Selectel / VK Cloud) with presigned upload/download URLs instead.
- **YooKassa with refunds, not NaidiAI's payment scaffolding as-is.** Payments are one-off
  redirect-confirmation payments (not Safe Deal/escrow), and refunds are implemented from the
  start — NaidiAI has no refund flow to port, so this part is built new, following the same
  never-trust-the-webhook-body / idempotent-write discipline as the rest of the stack.
- **A wider role set.** NaidiAI's MVP has a single `admin` role; Pamyat Ryadom needs
  `client` / `executor` / `dispatcher` / `qa` / `support` / `finance` / `admin` / `superadmin`,
  with MFA required for every role except client/executor, because dispatch and payouts are
  core to the product rather than an admin-only afterthought.
