// server.js (v2)
// Same job as v1: relay session events between exactly one "technician" and
// one "expert" per sessionId. v2 adds two reliability features borrowed from
// a reviewed ChatGPT prototype, reimplemented on the `ws` library instead of
// a hand-rolled socket parser:
//   1. Session history + a SNAPSHOT sent on JOIN, so a client that reconnects
//      mid-session catches up on what it missed.
//   2. event_id on every event + ACK{duplicate} + server-side dedup, so a
//      client that resends an unacknowledged event after reconnecting does
//      not cause it to be processed twice.

const WebSocket = require('ws');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const PORT = process.env.PORT || 8080;
const HISTORY_LIMIT = 50; // per session; bounded so memory doesn't grow across a long demo

const CONTRACT = JSON.parse(
  fs.readFileSync(path.join(__dirname, 'project_contract.json'), 'utf8')
);

const wss = new WebSocket.Server({ port: PORT });

// sessionId -> { technician: ws|null, expert: ws|null, history: Event[] }
const sessions = new Map();

const VALID_TYPES = new Set([
  'JOIN',               // client -> server, not relayed
  'SESSION_JOINED',
  'STEP_STARTED',
  'STEP_COMPLETED',
  'ANNOTATION_CREATED',
  'SESSION_ENDED',
]);

function log(...args) {
  console.log(new Date().toISOString(), ...args);
}

function getSession(sessionId) {
  if (!sessions.has(sessionId)) {
    sessions.set(sessionId, { technician: null, expert: null, history: [] });
  }
  return sessions.get(sessionId);
}

function otherRole(role) {
  return role === 'technician' ? 'expert' : 'technician';
}

function safeSend(ws, obj) {
  if (ws && ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(obj));
    return true;
  }
  return false;
}

wss.on('connection', (ws) => {
  ws.isAlive = true;
  ws.sessionId = null;
  ws.role = null;

  log('client connected (unjoined)');

  // Contract is static and role-agnostic — send it immediately, before JOIN.
  safeSend(ws, { type: 'PROJECT_CONTEXT', payload: CONTRACT });

  ws.on('pong', () => { ws.isAlive = true; });

  ws.on('message', (raw) => {
    let msg;
    try {
      msg = JSON.parse(raw);
    } catch (err) {
      safeSend(ws, { type: 'ERROR', payload: { message: 'Invalid JSON' } });
      return;
    }

    if (!msg || typeof msg.type !== 'string' || !VALID_TYPES.has(msg.type)) {
      safeSend(ws, { type: 'ERROR', payload: { message: `Unknown or missing type: ${msg && msg.type}` } });
      return;
    }

    // --- JOIN: bind this socket to a session + role, then catch it up ---
    if (msg.type === 'JOIN') {
      const { sessionId, role } = msg;
      if (!sessionId || (role !== 'technician' && role !== 'expert')) {
        safeSend(ws, { type: 'ERROR', payload: { message: 'JOIN requires sessionId and role in ["technician","expert"]' } });
        return;
      }

      const session = getSession(sessionId);
      if (session[role]) {
        // Seat occupied (e.g. a refresh/reconnect) — replace it, tell the old socket why.
        safeSend(session[role], { type: 'ERROR', payload: { message: 'Replaced by a new connection for this role' } });
        try { session[role].close(4000, 'replaced'); } catch (_) {}
      }

      session[role] = ws;
      ws.sessionId = sessionId;
      ws.role = role;

      log(`JOIN sessionId=${sessionId} role=${role}`);

      const otherWs = session[otherRole(role)];
      const otherConnected = !!(otherWs && otherWs.readyState === WebSocket.OPEN);

      safeSend(ws, {
        type: 'SESSION_JOINED',
        sessionId,
        payload: { role, otherConnected },
        ts: Date.now(),
      });

      if (otherConnected) {
        safeSend(otherWs, {
          type: 'SESSION_JOINED',
          sessionId,
          payload: { role, otherConnected: true },
          ts: Date.now(),
        });
      }

      // Catch-up: replay this session's recent event history to the joining
      // client. Covers the case where a client reconnected after missing a
      // STEP_STARTED / ANNOTATION_CREATED that was relayed while it was down.
      safeSend(ws, { type: 'SNAPSHOT', sessionId, payload: { events: session.history } });
      return;
    }

    // --- All other event types: dedup, store, relay ---
    if (!ws.sessionId || !ws.role) {
      safeSend(ws, { type: 'ERROR', payload: { message: 'Send JOIN before other events' } });
      return;
    }

    const session = getSession(ws.sessionId);
    const eventId = msg.eventId || crypto.randomUUID();

    const existing = session.history.find((e) => e.eventId === eventId);
    if (existing) {
      // Already processed this exact event (client resent after a reconnect
      // before it saw the ACK). Tell the sender, but don't relay it again —
      // the other side already got it the first time.
      safeSend(ws, { type: 'ACK', payload: { forType: msg.type, eventId, duplicate: true } });
      return;
    }

    const target = session[otherRole(ws.role)];
    const envelope = {
      type: msg.type,
      sessionId: ws.sessionId,
      from: ws.role,
      eventId,
      payload: msg.payload ?? null,
      ts: Date.now(),
    };

    session.history.push(envelope);
    if (session.history.length > HISTORY_LIMIT) session.history.shift();

    log(`RELAY ${envelope.type} session=${ws.sessionId} from=${ws.role} eventId=${eventId} -> ${otherRole(ws.role)}`);

    const delivered = safeSend(target, envelope);
    safeSend(ws, { type: 'ACK', payload: { forType: envelope.type, eventId, delivered, duplicate: false } });
  });

  ws.on('close', (code, reason) => {
    log(`client disconnected sessionId=${ws.sessionId} role=${ws.role} code=${code} reason=${reason}`);
    if (ws.sessionId && ws.role) {
      const session = sessions.get(ws.sessionId);
      if (session && session[ws.role] === ws) {
        session[ws.role] = null;
        const otherWs = session[otherRole(ws.role)];
        safeSend(otherWs, {
          type: 'SESSION_ENDED', // reused as a "peer disconnected" signal
          sessionId: ws.sessionId,
          payload: { reason: 'peer_disconnected', role: ws.role },
          ts: Date.now(),
        });
      }
    }
  });

  ws.on('error', (err) => {
    log('socket error', err.message);
  });
});

// --- Heartbeat: detect dead connections without waiting on TCP timeouts ---
const HEARTBEAT_MS = 15000;
const heartbeatInterval = setInterval(() => {
  wss.clients.forEach((ws) => {
    if (ws.isAlive === false) {
      log(`terminating dead connection sessionId=${ws.sessionId} role=${ws.role}`);
      return ws.terminate();
    }
    ws.isAlive = false;
    ws.ping();
  });
}, HEARTBEAT_MS);

wss.on('close', () => clearInterval(heartbeatInterval));

log(`WebSocket server listening on ws://localhost:${PORT}`);
log(`Serving project contract: ${CONTRACT.project} / ${CONTRACT.machine.machine_id}`);
