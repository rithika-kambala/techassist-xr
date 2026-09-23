// test-harness.js (v2)
// Run:  node test-harness.js   (against a running `node server.js`)

const WebSocket = require('ws');
const assert = require('assert');
const crypto = require('crypto');

const URL = process.env.WS_URL || 'ws://localhost:8080';
let passed = 0;
let failed = 0;

function makeClient(sessionId, role, { autoJoin = true } = {}) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(URL);
    const received = [];
    ws.on('message', (raw) => received.push(JSON.parse(raw)));
    ws.on('open', () => {
      if (autoJoin) ws.send(JSON.stringify({ type: 'JOIN', sessionId, role }));
      resolve({ ws, received });
    });
    ws.on('error', reject);
  });
}

function waitForMessage(received, predicate, timeoutMs = 2000) {
  return new Promise((resolve, reject) => {
    const start = Date.now();
    const check = () => {
      const found = received.find(predicate);
      if (found) return resolve(found);
      if (Date.now() - start > timeoutMs) return reject(new Error('timeout waiting for message'));
      setTimeout(check, 20);
    };
    check();
  });
}

async function test(name, fn) {
  try {
    await fn();
    console.log(`PASS: ${name}`);
    passed++;
  } catch (err) {
    console.log(`FAIL: ${name}`);
    console.log(`   ${err.message}`);
    failed++;
  }
}

async function main() {
  await test('JOIN gets PROJECT_CONTEXT then SESSION_JOINED', async () => {
    const sessionId = `t1-${Date.now()}`;
    const { received, ws } = await makeClient(sessionId, 'technician');
    await waitForMessage(received, (m) => m.type === 'PROJECT_CONTEXT');
    const joined = await waitForMessage(received, (m) => m.type === 'SESSION_JOINED');
    assert.strictEqual(joined.payload.role, 'technician');
    assert.strictEqual(joined.payload.otherConnected, false);
    ws.close();
  });

  await test('expert JOIN notifies already-connected technician', async () => {
    const sessionId = `t2-${Date.now()}`;
    const tech = await makeClient(sessionId, 'technician');
    await waitForMessage(tech.received, (m) => m.type === 'SESSION_JOINED');
    const expert = await makeClient(sessionId, 'expert');
    await waitForMessage(expert.received, (m) => m.type === 'SESSION_JOINED' && m.payload.otherConnected === true);
    const notice = await waitForMessage(tech.received, (m) => m.type === 'SESSION_JOINED' && m.payload.role === 'expert');
    assert.strictEqual(notice.payload.otherConnected, true);
    tech.ws.close(); expert.ws.close();
  });

  await test('STEP_STARTED relays expert -> technician with eventId and ts', async () => {
    const sessionId = `t3-${Date.now()}`;
    const tech = await makeClient(sessionId, 'technician');
    const expert = await makeClient(sessionId, 'expert');
    await waitForMessage(expert.received, (m) => m.type === 'SESSION_JOINED');

    const eventId = crypto.randomUUID();
    expert.ws.send(JSON.stringify({ type: 'STEP_STARTED', eventId, payload: { step_number: 1, target_component: 'panel_01' } }));

    const relayed = await waitForMessage(tech.received, (m) => m.type === 'STEP_STARTED');
    assert.strictEqual(relayed.from, 'expert');
    assert.strictEqual(relayed.eventId, eventId);
    assert.strictEqual(relayed.payload.target_component, 'panel_01');
    assert.ok(typeof relayed.ts === 'number');

    const ack = await waitForMessage(expert.received, (m) => m.type === 'ACK' && m.payload.eventId === eventId);
    assert.strictEqual(ack.payload.delivered, true);
    assert.strictEqual(ack.payload.duplicate, false);

    tech.ws.close(); expert.ws.close();
  });

  await test('resending the same eventId is ACKed as duplicate and not re-relayed', async () => {
    const sessionId = `t4-${Date.now()}`;
    const tech = await makeClient(sessionId, 'technician');
    const expert = await makeClient(sessionId, 'expert');
    await waitForMessage(expert.received, (m) => m.type === 'SESSION_JOINED');

    const eventId = crypto.randomUUID();
    const msg = { type: 'ANNOTATION_CREATED', eventId, payload: { target_component: 'valve_02' } };
    expert.ws.send(JSON.stringify(msg));
    await waitForMessage(tech.received, (m) => m.type === 'ANNOTATION_CREATED' && m.eventId === eventId);

    // Simulate a client that resent before seeing the ACK.
    expert.ws.send(JSON.stringify(msg));
    const dupAck = await waitForMessage(expert.received, (m) => m.type === 'ACK' && m.payload.duplicate === true);
    assert.strictEqual(dupAck.payload.eventId, eventId);

    // Technician should have received exactly one copy, not two.
    const copies = tech.received.filter((m) => m.type === 'ANNOTATION_CREATED' && m.eventId === eventId);
    assert.strictEqual(copies.length, 1);

    tech.ws.close(); expert.ws.close();
  });

  await test('a client that JOINs late receives prior events via SNAPSHOT', async () => {
    const sessionId = `t5-${Date.now()}`;
    const expert = await makeClient(sessionId, 'expert');
    await waitForMessage(expert.received, (m) => m.type === 'SESSION_JOINED');

    // Fire an event before the technician ever connects (no one to relay it to live).
    const eventId = crypto.randomUUID();
    expert.ws.send(JSON.stringify({ type: 'STEP_STARTED', eventId, payload: { step_number: 2, target_component: 'isolation_valve' } }));
    await waitForMessage(expert.received, (m) => m.type === 'ACK' && m.payload.eventId === eventId);

    // Now the technician joins late.
    const tech = await makeClient(sessionId, 'technician');
    const snapshot = await waitForMessage(tech.received, (m) => m.type === 'SNAPSHOT');
    const found = snapshot.payload.events.find((e) => e.eventId === eventId);
    assert.ok(found, 'expected the earlier STEP_STARTED in the snapshot history');
    assert.strictEqual(found.payload.target_component, 'isolation_valve');

    tech.ws.close(); expert.ws.close();
  });

  await test('event before JOIN returns ERROR', async () => {
    const { ws, received } = await makeClient('unused', 'technician', { autoJoin: false });
    ws.send(JSON.stringify({ type: 'STEP_STARTED', payload: {} }));
    const err = await waitForMessage(received, (m) => m.type === 'ERROR');
    assert.ok(err.payload.message.includes('JOIN'));
    ws.close();
  });

  await test('peer disconnect notifies the remaining client', async () => {
    const sessionId = `t7-${Date.now()}`;
    const tech = await makeClient(sessionId, 'technician');
    const expert = await makeClient(sessionId, 'expert');
    await waitForMessage(tech.received, (m) => m.type === 'SESSION_JOINED' && m.payload.otherConnected === true);
    expert.ws.close();
    const notice = await waitForMessage(tech.received, (m) => m.type === 'SESSION_ENDED' && m.payload?.reason === 'peer_disconnected');
    assert.strictEqual(notice.payload.role, 'expert');
    tech.ws.close();
  });

  await test('unknown event type returns ERROR, does not crash server', async () => {
    const sessionId = `t8-${Date.now()}`;
    const { ws, received } = await makeClient(sessionId, 'technician');
    ws.send(JSON.stringify({ type: 'NOT_A_REAL_TYPE' }));
    const err = await waitForMessage(received, (m) => m.type === 'ERROR');
    assert.ok(err.payload.message.includes('Unknown'));
    ws.close();
  });

  await test('sessions are isolated — event in session A never reaches session B', async () => {
    const sessionA = `iso-a-${Date.now()}`;
    const sessionB = `iso-b-${Date.now()}`;
    const techA = await makeClient(sessionA, 'technician');
    const expertA = await makeClient(sessionA, 'expert');
    const techB = await makeClient(sessionB, 'technician');
    await waitForMessage(expertA.received, (m) => m.type === 'SESSION_JOINED');

    expertA.ws.send(JSON.stringify({ type: 'STEP_STARTED', eventId: crypto.randomUUID(), payload: { step_number: 1 } }));
    await waitForMessage(techA.received, (m) => m.type === 'STEP_STARTED');

    await new Promise((r) => setTimeout(r, 200));
    const leaked = techB.received.find((m) => m.type === 'STEP_STARTED');
    assert.strictEqual(leaked, undefined, 'session B should not see session A events');

    techA.ws.close(); expertA.ws.close(); techB.ws.close();
  });

  console.log(`\n${passed} passed, ${failed} failed`);
  process.exit(failed === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error('Harness crashed:', err);
  process.exit(1);
});
