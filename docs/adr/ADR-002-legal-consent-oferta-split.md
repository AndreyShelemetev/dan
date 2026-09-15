# ADR-002: Split the consent and the client offer into separate registry entries

## Status

Accepted — 2026-09-14

## Context

`LegalDocumentRegistry` declared a single `oferta_client` v1.0 entry whose `Path` pointed at
`/legal/consent/` — the consent-to-processing page, not an offer document. Registration required
accepting `privacy` + `oferta_client`, which in practice meant a new client only ever saw and
accepted the privacy policy and the consent to processing; no offer/contract text existed or was
required, but the type on the acceptance row said `oferta_client`.

Under 152-ФЗ the privacy policy (what the operator publishes about its processing) and the
consent (the legal ground the person gives) are two different instruments, and a public offer for
the service itself (`/api/v1/legal/...` per CLAUDE.md's legal-contour rules, D08 on the delivery
plan) is a third, separate document again — an order/acceptance/payment/warranty contract, not a
data-processing instrument. Filing all of that under one `oferta_client` type meant the type name
promised something the content never delivered, and there was nowhere to file the actual offer
once it got written without either reusing the same misleading type or breaking the existing
acceptance trail.

A published version is never edited (BR-016): the fix could not be "correct the existing
`oferta_client` 1.0 row's path", since that would silently redefine what every past acceptance of
it already means.

## Decision

- Add a new `consent` document type (v1.0, path `/legal/consent/`) carrying exactly the same
  content and path the mislabeled `oferta_client` v1.0 pointed at. `RequiredForRegistration`
  switches from `oferta_client` to `consent`.
- Bump `oferta_client` to v2.0 with its own path, `/legal/oferta/` — the actual public offer for
  the service (subject, ordering and acceptance, price and payment, warranty, returns, claims —
  see D08). It is no longer part of the registration gate: accepting the data-processing consent
  is what 152-ФЗ requires to create an account; agreeing to the service offer is a separate
  contractual step already covered by ordering.
- `oferta_client` v1.0 is left exactly as published. Every `LegalAcceptance` row pointing at it
  keeps meaning what it meant when recorded — a person who registered before this change is on
  record as having accepted document 1.0 of that type, and that fact does not change.

## Consequences

- The registry now has three distinct instruments where there were effectively two: `privacy`,
  `consent` (both required at registration) and `oferta_client` (the service contract, reachable
  from the footer, not gating account creation).
- `legal_documents.type`'s CHECK constraint gains `consent` via an EF Core migration
  (`LegalConsentOfertaSplit`); no other schema change.
- The `/legal/oferta/` page itself (real offer text) is a separate task (D08) — this change only
  reserves the type, version and path for it.
