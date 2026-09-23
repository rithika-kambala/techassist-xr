# TechAssist XR — Real-Time Event Layer (v2)

Owner: Macario. This is v2 of the layer we built together, updated after
reviewing a ChatGPT-generated prototype you were given. Short version:
that prototype had two genuinely good ideas worth stealing and one real
bug worth avoiding. This doc explains what changed and gives you the
exact tests to re-verify everything.

---

## What changed from v1, and why

| Change | Why |
|---|---|
| **Session history + `SNAPSHOT` on JOIN** | v1 lost any event sent while a client was disconnected. If the technician's Quest drops Wi-Fi mid-step and reconnects, it now gets caught up on what it missed instead of silently sitting on stale state. |
| **`eventId` + `ACK{duplicate}` + server-side dedup** | Pairs with the client-side resend queue: if a client resends an event because it never saw the ACK (e.g. reconnect happened first), the server recognizes the same `eventId` and won't relay it twice. |
| **Server loads and serves `project_contract.json`** | The test client now drives real procedure steps (`pressure_gauge`, `isolation_valve`, `control_panel`) instead of generic placeholder text, so a naming mismatch with Shreya's Unity objects would show up in local testing, not at integration. |
| **Client-side pending/resend queue** | On reconnect, anything still unacknowledged gets resent automatically — closes the gap where a dropped connection could silently eat an annotation. |

**What I deliberately did *not* take from the ChatGPT version, and why:**

- **Hand-rolled WebSocket parsing in pure Python.** I found a concrete bug
  while reviewing it: the frame parser reads the opcode but never checks
  the FIN bit, so it silently assumes every frame is final. It'll work
  fine for the short JSON messages this demo sends, but it's exactly the
  kind of protocol-compliance gap that's expensive to debug blind five
  days before a deadline. `ws` (the library both versions of my server
  use) handles fragmentation, masking, and the closing handshake
  correctly because it's had years of real-world edge cases thrown at
  it — not a place to reinvent the wheel under time pressure.
- **Broadcasting to every client in a session.** Your system is strictly
  one technician + one expert per session. The ChatGPT version's hub
  broadcasts to *all* connected clients sharing a `session_id` with no
  role check, so a stray second tab in the same session would silently
  start receiving (and could send) events for a role it doesn't hold.
  v2 keeps v1's two-seat model: JOIN locks a socket to exactly one role,
  and a same-role reconnect cleanly replaces the old socket.
- **`PROCEDURE_COMPLETED` as a sixth event type.** Your execution plan
  lists five events. `SESSION_ENDED` with `payload.reason:
  "procedure_complete"` already carries that information without
  growing the contract. If the team later decides they genuinely want a
  distinct "all steps done, session still open" signal, that's a real
  design conversation to have with Pavithra and Rithika — not something
  to fork silently in the transport layer.
- **Duplicated `target_component`.** The ChatGPT annotation payload put
  `target_component` both at the top level and nested inside a `marker`
  object. Two fields that must always agree are two fields that
  eventually won't. v2 keeps it flat: one `target_component` field.

---

## 1. WebSockets recap (unchanged from v1)

Still the same fundamentals: persistent full-duplex connection,
`readyState` drives your CONNECTING/CONNECTED/DISCONNECTED states, no
built-in reconnect or message structure — you define both. See v1's
README if you want the longer version. What's new in v2 is entirely
about what happens *around* the socket (history, acknowledgment,
dedup), not the socket mechanics themselves.

---

## 2. Event contract

Same five event types as the execution plan. Envelope now includes
`eventId`:

```json
{ "type": "STEP_STARTED", "sessionId": "A102-local-demo", "from": "expert",
  "eventId": "9c1e...", "payload": { "step_number": 1, "title": "Inspect the pressure gauge",
  "instruction": "...", "target_component": "pressure_gauge" }, "ts": 1737000000000 }
```

| type | sent by | payload |
|---|---|---|
| `SESSION_JOINED` | server, on JOIN | `{ role, otherConnected }` |
| `STEP_STARTED` | expert | `{ step_number, title, instruction, target_component }` |
| `STEP_COMPLETED` | technician | `{ step_number, target_component }` |
| `ANNOTATION_CREATED` | expert | `{ step_number, target_component, marker_type, space, label }` |
| `SESSION_ENDED` | either, or server on peer-disconnect | `{ reason }` |
| `PROJECT_CONTEXT` | server, on connect | `{ project, machine, procedure, component_naming_rule }` |
| `SNAPSHOT` | server, on JOIN | `{ events: [...] }` — recent history for this session |
| `ACK` | server, after any relay | `{ forType, eventId, delivered, duplicate }` |
| `ERROR` | server | `{ message }` |

A client should always generate its own `eventId` (e.g.
`crypto.randomUUID()`) **before** sending, and reuse that same id if it
resends the message after a reconnect — that's what makes server-side
dedup work.

---

## 3. Run it locally

```bash
cd realtime-layer-v2
npm install
node server.js
```

Open `test-client.html` in two tabs. Set Session ID to
`A102-local-demo` in both, Role `technician` in one and `expert` in the
other, click Connect in each.

### Manual tests

1. **Contract loads** — both tabs should show "Machine A102" and the
   same three steps immediately, before either clicks Connect's JOIN
   step even resolves the other side.
2. **Join notification** — connect the expert tab first, then the
   technician tab. The expert tab's log should show a second
   `SESSION_JOINED` with `role: "technician"` and `otherConnected: true`.
3. **Step relay** — in the expert tab, pick Step 1, click **Send
   STEP_STARTED**. Confirm the technician tab's log shows it, the step
   card for Step 1 turns "current" in *both* tabs, and the expert tab's
   Unacked counter returns to 0 after the ACK.
4. **Annotation relay** — click **Send ANNOTATION_CREATED** in the
   expert tab, confirm `target_component: "pressure_gauge"` arrives at
   the technician tab.
5. **Reverse direction** — click **Send STEP_COMPLETED** in the
   technician tab, confirm it reaches the expert tab and the step card
   turns "done" in both.
6. **Snapshot catch-up** — with the expert tab connected and the
   technician tab *not yet* connected, click **Send STEP_STARTED** for
   Step 2 in the expert tab. Now connect the technician tab. Its log
   should show a `SNAPSHOT` message containing that STEP_STARTED event,
   and its step list should reflect Step 2 as current — even though it
   wasn't connected when the event was originally sent.
7. **Resend after reconnect** — with both tabs connected, stop the
   server (`Ctrl+C`). Click **Send ANNOTATION_CREATED** in the expert
   tab while offline — it should show as "QUEUED (offline)" and the
   Unacked counter should go to 1. Restart the server. The expert tab
   should reconnect, automatically resend the queued message (visible
   as a `RESEND` log line), and the technician tab should receive it
   exactly once.
8. **Peer disconnect** — close the technician tab entirely. Within
   ~15s the expert tab's log should show `SESSION_ENDED` with
   `reason: "peer_disconnected"`.

### Automated tests

```bash
node server.js &
node test-harness.js
```

Nine assertions, ending in `9 passed, 0 failed`. Beyond the v1 checks
(join handshake, bidirectional relay, error handling, disconnect
signaling), this version adds: duplicate-eventId suppression, late-join
snapshot catch-up, and session isolation (an event in session A never
reaching session B).

---

## 4. Integration notes (unchanged guidance from v1)

Unity/Quest: use **NativeWebSocket**
(`https://github.com/endel/NativeWebSocket`), not
`System.Net.WebSockets` — same reasoning as before, it isn't reliably
available on the Quest runtime target. The event contract and field
names haven't changed shape (still `type`/`sessionId`/`payload`, still
`target_component` as the cross-team join key with Shreya), so if
Rithika already started against the v1 contract, the only new thing to
wire up is reading `eventId` off incoming events and handling
`SNAPSHOT` on connect — both optional to consume if you want to keep
Unity simpler and just rely on the connection rarely dropping in the
demo room.

Same debugging table from v1 still applies. One addition:

| Symptom | Likely cause | Check |
|---|---|---|
| Technician sees a step twice after reconnecting | Client isn't tracking `seenEventIds` before applying `SNAPSHOT` events | Confirm the receiving client dedups by `eventId`, not just by relying on the server (server dedup only stops double-*sending*, not double-*applying* on a client that reprocesses its own history) |
