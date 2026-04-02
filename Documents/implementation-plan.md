# Implementation Plan

The goal is working software from the earliest possible point. The approach is a two-slice walking skeleton before any match domain work begins.

---

## Slice 1 — Login and Exist ✓ Complete

Everything needed for a user to authenticate and be reachable.

- Seed `InvitedUsers` directly in the database (your email address, any test devices). This is not a hack — it is the legitimate initial state of an invite-only system, and these seed entries will remain as admin/owner accounts.
- Auth0 email passwordless login
- Pre-auth gate: check `InvitedUsers` before handing off to Auth0
- `UserRegistered` event → creates the Users read model record with display name
- `FcmTokenRegistered` event → stores device push token via async Polecat projection

**Deliverable:** A real user can log in with their email address, set a display name, and be reachable by push notification.

---

## Slice 2 — Send a Message

Everything needed for one authenticated user to send a push notification to another.

- Temporary "all registered users except me" query endpoint — a scaffold, clearly marked, to be replaced by the contacts/invite discovery flow
- `MessageSent` event sourced via Polecat
- Polecat async projection handles the FCM send as a side effect
- Minimal send UI (no match context yet)

**Deliverable:** Full end-to-end — authenticated user sends a message, recipient receives an OS push notification. Functionally equivalent to the POC but with real identity and event sourcing.

---

## After the Walking Skeleton

Match creation and the full invite/contacts flow follow once the above is solid. The temporary "all registered users" endpoint is removed when proper recipient discovery is in place.
