# Story intake

- Folder: `.squad/stories/09-customer-portal/CRM-40/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `09-customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-40`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Customer portal: login
```

---

## Description

```
As a customer, I want to sign in to the customer portal with my email (one-time code), so that I can follow my requests securely.
```

---

## Acceptance criteria

```
1. Entering an email sends a one-time code; the correct code signs the customer in.
2. A wrong code returns 401; a code expires after 10 minutes.
3. A customer token calling staff APIs gets 403.
4. The portal account is linked to the existing customer with the same email.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-2 (JWT auth), CRM-8/9 (customers, contacts), CRM-23 (email channel sender).
- **Depends on code areas or other stories:** `IChannelSender`, `ICustomerRepository`, `JwtAccessTokenGenerator`, `AuthenticationExtensions`.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/35-submit-tickets-portal/`.
- Customers are not staff users: token uses role `Customer` (no permissions) and `sub` = customer id, so staff APIs answer 403; portal endpoints use a policy that requires the `Customer` role.
- Code: 6 digits, stored hashed, expires after 10 minutes (`TimeProvider`), 5 wrong tries burn the code, one use only. Requesting a code never reveals whether the email is known.

## Out of scope

- Passwords, social login, remember-me, portal profile editing.
