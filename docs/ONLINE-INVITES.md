# Signed-in player invitations

Unity's main menu / lobby / pause menu has an **Online players** panel. Sign-in sends a presence heartbeat every 20 seconds; players disappear after 60 seconds without one. The list shows up to 100 signed-in usernames, without account privileges, profiles, tokens or room addresses. No friendship system is required.

Host a normal online room (including the unlocked Expedition station), then select **Invite** beside a player. The recipient's panel offers **Accept** / **Decline**. Acceptance is available only outside a room or match and feeds the code directly into the existing trusted-directory join path. It never automatically disconnects a player or joins from a notification. Normal room capacity, started-run and version checks still apply; invitations do not reserve seats or bypass those checks. LAN and custom managed-relay endpoints are not included in this first integration.

Invitations expire after two minutes. The service derives the sender from the authenticated account session, checks fresh recipient presence and an unexpired room-directory entry, limits sends to one per five seconds, prevents duplicate pending invitations from the same sender, and caps pending invitations at 20 per recipient. Only the recipient can consume an invitation; acceptance/decline is a conditional database update and cannot be replayed. An invitation is a referral from the named sender, not proof that sender owns the room. No session token or arbitrary connection endpoint is stored in an invite.

## Deployment order

1. Apply generated migration `drizzle/0006_player_invites.sql` (two additive tables and indexes), with the matching journal/snapshot and `db/schema.ts`.
2. Deploy the account worker built from `src/relay/worker.ts`, including `account-social.ts` and the accounts router. Preserve the current account database and signing identity. No secrets change. The normal relay build packages the migration directory.
3. Publish the corresponding Unity test build through the existing Windows builder. Hosts and recipients both need this client version.

The source branch alone does not activate the hosted service. Until steps 1–2 are deployed, the new client displays that updated account service is required and existing room-code joining continues to work. This task does not authorize a production account-service or database deployment. Rollback: revert the client/service changes; leave the additive tables in place until a separately authorized cleanup. No destructive migration is required.

## Verification and remaining acceptance

Account API integration tests cover authentication, online expiry, private inbox isolation, invalid destinations, self-invite rejection, server-derived identity, send throttling/duplicates, recipient-only acceptance, one-use consumption, decline and expiration using the real SQLite migration/schema. Native assemblies are compiled against the pinned Unity references. Native GUI/network execution remains a Windows acceptance step: two signed-in clients, host and invite, accept/join, decline, expiry, full/closed room, logout/account switch, and invitations received during an active match.

Claude presentation handoff: the functional panel uses existing IMGUI controls. It needs the normal visual polish and controller navigation review; no existing screen has been redesigned. Presence remains active during gameplay, but invitation actions are exposed only in menus.
