import http from 'node:http';
import { randomUUID } from 'node:crypto';
import { WebSocketServer, WebSocket } from 'ws';
import { ApiError, assert, authenticate, hash, identifier } from './domain.js';

export function createApp({config,store,machines,diagnose}) {
  const peers=new Set(), inflight=new Set(), rates=new Map(); let closing=false;
  const userOnly=u=>assert(u.role==='technician',403,'forbidden','A technician account is required.');
  const json=(res,status,body)=>{res.writeHead(status,{'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store','X-Content-Type-Options':'nosniff'});res.end(JSON.stringify(body));};
  function limit(key,max=120) {const now=Date.now();let row=rates.get(key);if(!row||row.until<=now){row={count:0,until:now+60000};rates.set(key,row);} assert(++row.count<=max,429,'rate_limit','Too many requests. Try again shortly.');}
  const cleanup=setInterval(()=>{for(const [key,row] of rates) if(row.until<Date.now()) rates.delete(key);},60000);cleanup.unref();
  function send(ws,obj) {if(ws.readyState!==WebSocket.OPEN)return false;if(ws.bufferedAmount>1024*1024){ws.close(1013,'Slow consumer');return false;}ws.send(JSON.stringify(obj));return true;}
  function broadcast(event) {for(const ws of peers) if(ws.sessionId===event.sessionId) send(ws,event);}
  const snapshot=s=>({session_id:s.id,diagnosis_id:s.diagnosis_id,completed_steps:s.completed,status:s.status,steps:s.plan.steps,sessionId:s.id});
  function createSession(body,user) {
    userOnly(user);identifier(body.session_id,'session_id');identifier(body.diagnosis_id,'diagnosis_id');
    assert(body.reviewed===true,400,'review_required','Review the plan before starting.');
    return store.transaction(()=>{
      const existing=store.db.prepare('SELECT * FROM sessions WHERE id=?').get(body.session_id);
      if(existing){assert(existing.owner===user.id && existing.diagnosis_id===body.diagnosis_id,409,'session_conflict','Session ID already exists.');return {result:snapshot(store.session(body.session_id,user)),events:[]};}
      const plan=store.diagnosis(body.diagnosis_id,user.id);
      assert(!plan.needs_expert && plan.steps.length>0,409,'expert_required','Expert review is needed; no executable plan exists.');
      store.db.prepare('INSERT INTO sessions(id,owner,diagnosis_id,plan,created) VALUES(?,?,?,?,?)').run(body.session_id,user.id,body.diagnosis_id,JSON.stringify(plan),Date.now());
      const event=store.event(body.session_id,'STEP_STARTED',plan.steps[0],randomUUID());
      return {result:snapshot(store.session(body.session_id,user)),events:[event]};
    });
  }
  function complete(body,user) {
    userOnly(user);identifier(body.session_id);identifier(body.event_id);
    return store.transaction(()=>{
      const s=store.session(body.session_id,user);
      const old=store.db.prepare('SELECT body FROM events WHERE event_id=?').get(body.event_id);
      if(old){const e=JSON.parse(old.body);assert(e.sessionId===s.id && e.type==='STEP_COMPLETED' && e.payload.step_number===body.step_number && e.payload.target_component===body.target_component,409,'idempotency_conflict','Event ID already used for different data.');return {result:{session_id:s.id,event_id:body.event_id,accepted:true,duplicate:true},events:[]};}
      assert(s.status==='active',409,'session_closed','Session is no longer active.');
      const expected=s.plan.steps[s.completed];
      assert(expected && expected.step_number===body.step_number && expected.target_component===body.target_component,409,'unexpected_step','Completion does not match the current step.');
      const events=[store.event(s.id,'STEP_COMPLETED',{step_number:body.step_number,target_component:body.target_component},body.event_id,'technician')];
      const done=s.completed+1===s.plan.steps.length;
      store.db.prepare('UPDATE sessions SET completed=completed+1,status=? WHERE id=?').run(done?'completed':'active',s.id);
      events.push(store.event(s.id,done?'SESSION_ENDED':'STEP_STARTED',done?{reason:'procedure_complete'}:s.plan.steps[s.completed+1],randomUUID()));
      return {result:{session_id:s.id,event_id:body.event_id,accepted:true,duplicate:false},events};
    });
  }
  function endSession(id,user) {
    userOnly(user);
    return store.transaction(()=>{const s=store.session(id,user);if(s.status!=='active')return [];
      store.db.prepare("UPDATE sessions SET status='ended' WHERE id=?").run(id);
      return [store.event(id,'SESSION_ENDED',{reason:'technician_cancelled'},randomUUID(),'technician')];});
  }
  async function readBody(req) {
    assert((req.headers['content-type']||'').split(';')[0]==='application/json',415,'content_type','Use application/json.');
    const chunks=[];let bytes=0;for await(const chunk of req){bytes+=chunk.length;assert(bytes<=32*1024,413,'body_too_large','Request exceeds 32 KiB.');chunks.push(chunk);} const text=Buffer.concat(chunks).toString('utf8');
    let body;try{body=JSON.parse(text);}catch{throw new ApiError(400,'invalid_json','Invalid JSON body.');}
    assert(body && typeof body==='object' && !Array.isArray(body),400,'invalid_json','Expected a JSON object.');return body;
  }
  const server=http.createServer(async(req,res)=>{
    const requestId=randomUUID();res.setHeader('X-Request-ID',requestId);
    try {
      const path=new URL(req.url,'http://localhost').pathname;
      if(req.method==='GET' && path==='/healthz') return json(res,200,{status:'ok'});
      if(req.method==='GET' && path==='/readyz') {store.db.prepare('SELECT 1').get();return json(res,closing?503:200,{status:closing?'draining':'ready',ai_mode:config.aiMode});}
      assert(!closing,503,'draining','Service is restarting.');
      const origin=req.headers.origin;if(origin) {assert(config.origins.includes(origin),403,'origin_denied','Origin is not allowed.');res.setHeader('Access-Control-Allow-Origin',origin);res.setHeader('Vary','Origin');}
      if(req.method==='OPTIONS'){res.setHeader('Access-Control-Allow-Methods','GET, POST, OPTIONS');res.setHeader('Access-Control-Allow-Headers','Authorization, Content-Type, Idempotency-Key');res.writeHead(204);return res.end();}
      const user=authenticate(req.headers.authorization,config.users);limit(user.id);
      if(req.method==='GET' && path==='/api/machines')return json(res,200,{machines:[...machines.values()]});
      if(req.method==='GET' && path.startsWith('/api/machines/')){const machine=machines.get(path.split('/').pop());assert(machine,404,'not_found','Machine not found.');return json(res,200,machine);}
      if(req.method==='POST' && path==='/api/diagnoses') {
        userOnly(user);const body=await readBody(req);const machine=machines.get(body.machine_id);
        assert(machine,404,'not_found','Machine not found.');identifier(body.request_id,'request_id');
        assert(typeof body.technician_report==='string' && body.technician_report.trim().length>=3 && body.technician_report.length<=1000,400,'invalid_report','Enter a report of 3–1000 characters.');
        assert(inflight.size<config.maxConcurrent && !inflight.has(user.id),429,'ai_busy','An AI request is already running. Try again shortly.');
        const report=body.technician_report.trim(),id=randomUUID();
        const fingerprint=hash(JSON.stringify({machine,report,model:config.model,mode:config.aiMode}));
        const cached=store.reserveDiagnosis({id,owner:user.id,requestId:body.request_id,fingerprint},config.dailyLimit,config.globalDailyLimit);
        if(cached)return json(res,200,cached);
        inflight.add(user.id);
        try {
          const plan=await diagnose(report,machine);
          const result={...plan,diagnosis_id:id,machine_id:machine.machine_id,mode:config.aiMode,training_only:machine.training_only!==false,
            source_documents:machine.sources.filter(s=>plan.source_ids.includes(s.id)),created_at:new Date().toISOString()};
          store.saveDiagnosis(id,result);return json(res,200,result);
        } catch(e){store.failDiagnosis(id);throw e;} finally {inflight.delete(user.id);}
      }
      if(req.method==='POST' && path==='/api/sessions') {const {result,events}=createSession(await readBody(req),user);events.forEach(broadcast);return json(res,200,result);}
      if(req.method==='POST' && path==='/api/step-completions') {const {result,events}=complete(await readBody(req),user);events.forEach(broadcast);return json(res,200,result);}
      const match=path.match(/^\/api\/sessions\/([a-zA-Z0-9_-]{1,96})(\/end)?$/);
      if(match && req.method==='GET' && !match[2]){const s=store.session(match[1],user);return json(res,200,{...snapshot(s),events:store.history(s.id)});}
      if(match && req.method==='POST' && match[2]){endSession(match[1],user).forEach(broadcast);return json(res,200,{ended:true});}
      throw new ApiError(404,'not_found','Endpoint not found.');
    } catch(e){
      // Do not log report bodies, tokens, provider messages or prompts.
      if(!e.status)console.error(JSON.stringify({request_id:requestId,error:'internal_error',name:e.name}));
      if(!res.headersSent)json(res,e.status||500,{error:{code:e.code||'internal_error',message:e.status?e.message:'Internal server error.',request_id:requestId}});
      else res.end();
    }
  });
  server.requestTimeout=15000;server.headersTimeout=10000;
  const wss=new WebSocketServer({noServer:true,maxPayload:16384,perMessageDeflate:false});
  server.on('upgrade',(req,socket,head)=>{
    try {
      assert(!closing && new URL(req.url,'http://localhost').pathname==='/ws',404,'not_found','Not found.');
      const user=authenticate(req.headers.authorization,config.users);limit('ws:'+user.id,12);
      assert(!req.headers.origin||config.origins.includes(req.headers.origin),403,'origin_denied','Origin denied.');
      assert([...peers].filter(w=>w.user.id===user.id).length<4,429,'socket_limit','Too many sockets.');
      wss.handleUpgrade(req,socket,head,ws=>{ws.user=user;wss.emit('connection',ws,req);});
    }catch{socket.end('HTTP/1.1 401 Unauthorized\r\nConnection: close\r\n\r\n');}
  });
  wss.on('connection',ws=>{
    peers.add(ws);ws.alive=true;
    const joinTimer=setTimeout(()=>{if(!ws.sessionId)ws.close(1008,'JOIN required');},10000);joinTimer.unref();
    ws.on('pong',()=>{ws.alive=true;});ws.on('error',()=>{});ws.on('close',()=>{clearTimeout(joinTimer);peers.delete(ws);});
    ws.on('message',raw=>{try {
      limit('messages:'+ws.user.id,180);let msg;try{msg=JSON.parse(raw.toString());}catch{throw new ApiError(400,'invalid_json','Invalid JSON.');}
      assert(msg && typeof msg==='object',400,'invalid_json','Expected an object.');
      if(msg.type==='JOIN') {
        assert(msg.role===ws.user.role,403,'forbidden','Role does not match credentials.');const s=store.session(identifier(msg.sessionId),ws.user);
        for(const other of peers)if(other!==ws && other.sessionId===s.id && other.user.role===ws.user.role)other.close(4000,'replaced');
        ws.sessionId=s.id;clearTimeout(joinTimer);
        send(ws,{type:'PROJECT_CONTEXT',payload:{machine:machines.get(s.plan.machine_id),procedure:{steps:s.plan.steps},component_naming_rule:'Exact component IDs only.'}});
        send(ws,{type:'SESSION_JOINED',sessionId:s.id,payload:{role:ws.user.role,otherConnected:[...peers].some(p=>p!==ws && p.sessionId===s.id)}});
        send(ws,{type:'SNAPSHOT',sessionId:s.id,payload:{events:store.history(s.id)}});return;
      }
      assert(ws.sessionId,400,'join_required','JOIN first.');
      if(msg.type==='STEP_COMPLETED') {
        const {result,events}=complete({...msg.payload,session_id:ws.sessionId,event_id:msg.eventId},ws.user);events.forEach(broadcast);
        send(ws,{type:'ACK',payload:{forType:msg.type,eventId:result.event_id,duplicate:result.duplicate,accepted:true}});return;
      }
      if(msg.type==='SESSION_ENDED'){endSession(ws.sessionId,ws.user).forEach(broadcast);return;}
      throw new ApiError(400,'unsupported_event','This service publishes approved steps automatically; only completion and end are writable.');
    }catch(e){send(ws,{type:'ERROR',payload:{code:e.code||'internal_error',message:e.status?e.message:'Internal server error.'}});}});
  });
  const heartbeat=setInterval(()=>{for(const ws of peers){if(!ws.alive){ws.terminate();continue;}ws.alive=false;ws.ping();}},20000);heartbeat.unref();
  return {server,wss,async close(){closing=true;clearInterval(cleanup);clearInterval(heartbeat);for(const ws of peers)ws.terminate();await new Promise(resolve=>wss.close(resolve));await new Promise(resolve=>server.close(resolve));store.close();}};
}
