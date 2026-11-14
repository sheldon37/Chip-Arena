# Signed-in player invitations

Unity's main menu / lobby / pause menu has an **Online players** panel. Sign-in sends a presence heartbeat every 4 seconds after a successful response; players disappear after 60 seconds without one. The list shows up to 100 signed-in usernames, without account privileges, profiles, tokens or room addresses. No friendship system is required.

Host a normal online room (including the unlocked Expedition station), then select **Invite** beside a player. The recipient's panel offers **Accept** / **Decline**. Acceptance is available only outside a room or match and feeds the code directly into the existing trusted-directory join path. It never automatically disconnects a player or joins from a notification. Normal room capacity, started-run and version checks still apply; invitations do not reserve seats or bypass those checks. LAN and custom managed-relay endpoints are not included in this first integration.

Invitations expire after two minutes. The service derives the sender from the authenticated account session, checks fresh recipient presence and an unexpired room-directory entry, limits sends to one per five seconds, prevents duplicate pending invitations from the same sender, and caps pending invitations at 20 per recipient. Only the recipient can consume an invitation; acceptance/decline is a conditional database update and cannot be replayed. An invitation is a referral from the named sender, not proof that sender owns the room. No session token or arbitrary connection endpoint is stored in an invite.

## Deployment order

1. Apply generated migration `drizzle/0006_player_invites.sql` (two additive tables and indexes), with the matching journal/snapshot and `db/schema.ts`.
2. Deploy the account worker built from `src/relay/worker.ts`, including `account-social.ts` and the accounts router. Preserve the current account database and signing identity. No secrets change. The normal relay build packages the migration directory.
3. Publish the corresponding Unity test build through the existing Windows builder. Hosts and recipients both need this client version.

The source branch alone does not activate the hosted service. Until steps 1–2 are deployed, the new client displays that updated account service is required and existing room-code joining continues to work. The owner subsequently authorized the update on 2026-11-02: account-service Sites version 69 deployed successfully from exact source `95a77ab986812d21f4dd75e5d8ea4537f8abd698`; database overview confirmed `player_presence` and `player_invites`. Deployment `appgdep_6ae8ecefc794819199dfcc2c93fd6dcf`. TypeScript, relay build and the focused SQLite invitation test passed before deploy. No real-player invite/join smoke test was run. Rollback: revert the client/service changes; leave the additive tables in place until a separately authorized cleanup. No destructive migration is required.

## Verification and remaining acceptance

Account API integration tests cover authentication, online expiry, private inbox isolation, invalid destinations, self-invite rejection, server-derived identity, send throttling/duplicates, recipient-only acceptance, one-use consumption, decline and expiration using the real SQLite migration/schema. Native assemblies are compiled against the pinned Unity references. Native GUI/network execution remains a Windows acceptance step: two signed-in clients, host and invite, accept/join, decline, expiry, full/closed room, logout/account switch, and invitations received during an active match.

Claude presentation handoff: the functional panel uses existing IMGUI controls. It needs the normal visual polish and controller navigation review; no existing screen has been redesigned. Presence remains active during gameplay, but invitation actions are exposed only in menus.

Presentation done 2026-11-03 (owner: "can we fix the online players button? (Used for inviting) Style it a lot better and apply animations"; `Runtime/NativeSocialPanelUI.cs`, `NativeAccountSocial.cs` now logic only). The old button sat at a fixed physical-pixel spot under the home screen's hexagons, clipped by their labels. Now kit-drawn on the virtual canvas in the cyber palette: an **ONLINE** chip under the hexagon row at home, left of the connection pill in a room's lobby, else in the top-right corner (the panel screens and the pause menu); a breathing presence light with a sonar ring, the count, a magenta invitations badge that beats while any wait (a burst when one arrives), a chevron turning as the panel opens; hover lift and light, press. The panel drops open under it (glass, a caret to the chip): the service's status line, invitation cards (initials, a countdown draining to the two-minute expiry, Accept / Decline; a new card slides in with a flash), the online players as staggered rows (initials with a live light, Invite turning to SENT with a pop), the "host an online room" hint, a busy spinner, wheel scrolling. Escape, its close button or a click outside close it; the menu under it takes no hover or click while the pointer is on it. Actions, who may invite and who may accept are unchanged. Controller: the kit's buttons take the controller pointer's click as before; a focus order for the panel is not added.

Invitation pop-up, 2026-11-07 (owner: "Can we make another button on the screen? This will show a notification when players get invited", then "Pop-up with Accept"; `Runtime/NativeSocialPanelUI.cs`): a card appears under the chip's corner when an invitation waits, with who, the two-minute countdown, Accept and Decline and a chime. It shows on the menus (the pause menu too) and at a match's or an Expedition's end, never mid-fight; an invitation sent during a fight pops up at the next of those while it lasts. Accept and Decline are the panel's own actions. Accept stays available only outside a room or match, so inside one the card says "Leave your room or match to accept". Its close button only puts it away: the invitation stays on the chip's badge. Invitations were initially polled every 20 seconds; the 2026-11-08 client reduces that interval to 4 seconds after each successful response. Network latency and retry delays are additional. No service or rule changed.

INVITES chip, 2026-11-07 (owner: "Lets make a separate button for invites, place it next to the "Online" Button"; `Runtime/NativeSocialPanelUI.cs`): the invitations left the ONLINE chip's badge for a chip of their own, just left of it wherever ONLINE sits (home, a room's lobby, the panel screens and the pause menu). It shows the envelope, **INVITES** and the count in a pill: quiet with none waiting, magenta while any wait (the pill beats with a ring off each beat, and a burst plays when one arrives). Each chip opens its own panel under the pair. ONLINE shows the players with Invite and the "host an online room" hint. INVITES shows the invitation cards with Accept / Decline, or with none "No invitations right now". Clicking the open panel's chip closes it, and clicking the other chip switches the panel over (its rows stagger in again and the caret slides across). The pop-up's "+N MORE" opens the invitations. While either panel is open the pop-up steps aside; once the invitations panel has shown an invitation it doesn't pop up again, otherwise the pop-up returns when the panel closes (without a second chime). Actions, who may invite or accept, and the service are unchanged.

## Unity invitation directory fix (2026-11-07)

Unity registers a visible room such as `7KMR4P` under the directory key `UN7KMR4P`.
Invitation sends now use that registered key. Accept strips exactly the `UN` namespace
before handing the six-character code to JoinRoom; unrelated eight-character rooms
are rejected. The former raw-code send was rejected as unavailable by the service.
SENT now appears only after the service confirms `ok:true`, and action errors survive
background refreshes long enough to read. Temporary request failures keep the cached
inbox instead of silently erasing it. Both players need this client fix. No worker or
migration change is required; the current API already accepts the directory key.

Regression: `node --test tests/invite-directory.test.mjs` runs the shipped social
handler against SQLite with the invite migration, reproducing the old rejection and
checking send, private inbox, accept and one-use consumption with the real namespace.
CloudflareChecks covers decoding accepted keys, including visible codes beginning UN.

## Faster inbox checks (2026-11-08)

The client refreshes presence and invitations four seconds after each successful response, including with both social panels closed. Requests never overlap; failures retain the existing five-second retry and cached inbox. This allows at most 75 periodic polls per five minutes, below the existing 100-per-action limit, without changing the hosted service. The recipient needs the updated client. Popup visibility still follows the existing menus/end-screen rule; active combat can delay presentation beyond delivery.
