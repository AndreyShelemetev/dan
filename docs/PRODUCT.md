# PRODUCT.md — Память рядом (Pamyat Ryadom)

## Who the customer is

People who want a relative's grave kept tended but cannot do it themselves — because they live in
another city or country, are elderly, or simply cannot make the trip regularly. Today their only
options are an occasional favor from someone local, or an unmanaged, unverifiable arrangement with
an individual cemetery caretaker. Pamyat Ryadom is the managed alternative: order the care online,
have a vetted executor perform it, and see proof it was done.

## The core promise

**Бережно организуем уход и покажем результат.** We carefully arrange the care and show you the
result. The client never has to negotiate with, verify, or trust an unknown person directly —
Pamyat Ryadom stands behind the visit and the evidence.

## In scope for MVP

- A managed service, not an open marketplace: the client orders from a catalog, dispatch assigns
  the work to a vetted executor — the client never picks or contacts an executor directly.
- A catalog of care packages and one-off services (cleaning, flowers, small tidying) tied to a
  known burial site.
- Orders and estimates: place an order against a burial site and a catalog selection, see the
  price up front.
- Dispatch and visits: an order becomes a scheduled visit assigned to an executor, with visible
  status through completion.
- Photo/video evidence: every completed visit produces a report the client can view.
- One-off online payment via YooKassa, with refunds.
- A dispute flow for when a client is unsatisfied with a completed visit.
- Web app only, mobile-first — most clients are expected to use it from a phone.

## Out of scope for MVP

- **No open marketplace.** Clients do not browse, choose, message, or rate individual executors;
  Pamyat Ryadom controls assignment and quality end to end.
- **No native mobile apps.** Mobile-first responsive web only — no iOS/Android app store
  presence in MVP.
- **No capital repair or construction work.** Monument restoration, fencing/foundation work, and
  other major physical repairs are not offered — the catalog covers light, recurring upkeep only.
- **No burial arrangement or plot sales.** The service assumes the burial site already exists;
  Pamyat Ryadom does not arrange funerals, burials, or cemetery plot purchases.
- **No recurring subscriptions this month.** Ordering is one-off per visit. A `SubscriptionPlan`
  exists in the catalog admin screen, but there is no subscription lifecycle, scheduler, or
  client UI behind it yet — moved to right after launch, once payments, disputes and
  notifications exist for it to build on (see `docs/plan/PLAN.md`, decision D04).
- **No SMS login this month.** The API accepts an `sms` channel in its OTP request shape but
  returns `501` for it — no SMS provider is wired up — and the login form only ever offers email.
  Not a scoped-out decision, just not this month's task; see `CLAUDE.md`.
