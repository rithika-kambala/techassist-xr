import { createHash, timingSafeEqual } from 'node:crypto';
export class ApiError extends Error { constructor(status, code, message) { super(message); this.status = status; this.code = code; } }
export const assert = (test, status, code, message) => { if (!test) throw new ApiError(status, code, message); };
export const hash = value => createHash('sha256').update(value).digest('hex');
export function identifier(value, label = 'ID') {
  assert(typeof value === 'string' && /^[a-zA-Z0-9_-]{1,96}$/.test(value), 400, 'invalid_id', `${label} must contain 1–96 letters, digits, dashes or underscores.`);
  return value;
}
export function parseUsers(raw) {
  let users; try { users = JSON.parse(raw || '{}'); } catch { throw new Error('API_TOKENS_JSON must be valid JSON.'); }
  assert(users && typeof users === 'object' && !Array.isArray(users) && Object.keys(users).length, 500, 'config', 'Configure at least one API user.');
  const tokens = new Set();
  return Object.entries(users).map(([id, user]) => {
    identifier(id, 'User');
    if (!user || typeof user.token !== 'string' || user.token.length < 32 || !['technician', 'expert'].includes(user.role) || tokens.has(user.token)) throw new Error('Each user needs a unique token of at least 32 characters and technician/expert role.');
    tokens.add(user.token);
    return { id, role: user.role, digest: Buffer.from(hash(user.token), 'hex') };
  });
}
export function authenticate(header, users) {
  const value = typeof header === 'string' && header.startsWith('Bearer ') ? header.slice(7) : '';
  const digest = Buffer.from(hash(value), 'hex');
  const user = users.find(item => timingSafeEqual(item.digest, digest));
  assert(user, 401, 'unauthorized', 'A valid bearer token is required.');
  return user;
}
export function validateMachine(machine) {
  identifier(machine.machine_id);
  assert(Array.isArray(machine.components) && machine.components.length && new Set(machine.components).size === machine.components.length, 500, 'machine_config', 'Invalid machine component catalog.');
  assert(Array.isArray(machine.sources) && machine.sources.length && new Set(machine.sources.map(x=>x.id)).size===machine.sources.length, 500, 'machine_config', 'Invalid machine source catalog.');
  for (const c of machine.components) identifier(c);
  for (const s of machine.sources) assert(typeof s.id === 'string' && s.title && s.text, 500, 'machine_config', 'Incomplete machine source.');
  return machine;
}
export function schemaFor(machine) {
  const sourceIds = { type: 'array', minItems: 1, items: { type: 'string', enum: machine.sources.map(x => x.id) } };
  return { type: 'object', additionalProperties: false, required: ['summary', 'needs_expert', 'source_ids', 'steps'], properties: {
    summary: { type: 'string' }, needs_expert: { type: 'boolean' }, source_ids: sourceIds,
    steps: { type: 'array', maxItems: 12, items: { type: 'object', additionalProperties: false,
      required: ['step_number', 'title', 'instruction', 'target_component', 'source_ids'], properties: {
        step_number: { type: 'integer', minimum: 1, maximum: 12 }, title: { type: 'string' }, instruction: { type: 'string' },
        target_component: { type: 'string', enum: machine.components }, source_ids: sourceIds
      }
    }}
  }};
}
export function validatePlan(plan, machine) {
  const text = (v, n) => typeof v === 'string' && v.trim().length > 0 && v.length <= n;
  const sources = ids => Array.isArray(ids) && ids.length > 0 && ids.length <= machine.sources.length && new Set(ids).size === ids.length && ids.every(id => machine.sources.some(s => s.id === id));
  assert(plan && text(plan.summary, 1600) && typeof plan.needs_expert === 'boolean' && sources(plan.source_ids) && Array.isArray(plan.steps) && plan.steps.length <= 12, 502, 'invalid_ai_output', 'AI output did not pass validation.');
  assert(plan.needs_expert ? plan.steps.length === 0 : plan.steps.length > 0, 502, 'invalid_ai_output', 'AI must either provide a plan or request expert review.');
  plan.steps.forEach((step, i) => {
    const check = (ok, message) => assert(ok,502,'invalid_ai_output',`AI step ${i+1}: ${message}`);
    check(step && step.step_number === i+1,'numbering must be consecutive from 1.');
    check(text(step.title,160),'title is empty or exceeds 160 characters.');
    check(text(step.instruction,1800),'instruction is empty or exceeds 1800 characters.');
    check(machine.components.includes(step.target_component),'target component is not in the machine catalog.');
    check(sources(step.source_ids),'source citations are missing, duplicated or unknown.');
    check(step.source_ids.every(id=>plan.source_ids.includes(id)),'step citation is missing from the plan source index.');
  });
  return plan;
}

// The source index is redundant metadata. Build it from genuine supplied citations;
// never invent a citation, change a component, reorder steps or alter instructions.
export function validateGeneratedPlan(plan,machine) {
  const known = new Set(machine.sources.map(s=>s.id));
  const ids = value => {
    assert(Array.isArray(value) && value.length>0 && value.length<=64 && value.every(id=>known.has(id)),502,'invalid_ai_output','AI source citations are missing or unknown.');
    return [...new Set(value)];
  };
  assert(plan && Array.isArray(plan.steps) && plan.steps.length<=12,502,'invalid_ai_output','AI output did not pass validation.');
  const top=ids(plan.source_ids);
  const steps=plan.steps.map(step=>{
    assert(step && typeof step==='object',502,'invalid_ai_output','AI step is invalid.');
    return {...step,source_ids:ids(step.source_ids)};
  });
  return validatePlan({...plan,steps,source_ids:[...new Set([...top,...steps.flatMap(s=>s.source_ids)])]},machine);
}
