import { DatabaseSync } from 'node:sqlite';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { assert, hash } from './domain.js';
export class Store {
  constructor(path) {
    if(path!==':memory:') mkdirSync(dirname(path),{recursive:true});
    this.db=new DatabaseSync(path);
    this.db.exec(`PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;
      CREATE TABLE IF NOT EXISTS diagnoses(id TEXT PRIMARY KEY,owner TEXT NOT NULL,request_id TEXT NOT NULL,fingerprint TEXT NOT NULL,status TEXT NOT NULL,result TEXT,created INTEGER NOT NULL,UNIQUE(owner,request_id));
      CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,owner TEXT NOT NULL,diagnosis_id TEXT NOT NULL,plan TEXT NOT NULL,completed INTEGER NOT NULL DEFAULT 0,status TEXT NOT NULL DEFAULT 'active',created INTEGER NOT NULL);
      CREATE TABLE IF NOT EXISTS events(seq INTEGER PRIMARY KEY AUTOINCREMENT,session_id TEXT NOT NULL REFERENCES sessions(id),event_id TEXT NOT NULL UNIQUE,type TEXT NOT NULL,body TEXT NOT NULL,created INTEGER NOT NULL);
      CREATE TABLE IF NOT EXISTS quotas(day TEXT NOT NULL,owner TEXT NOT NULL,count INTEGER NOT NULL,PRIMARY KEY(day,owner));
      UPDATE diagnoses SET status='failed' WHERE status='pending';`);
  }
  transaction(action) { this.db.exec('BEGIN IMMEDIATE');try {const result=action();this.db.exec('COMMIT');return result;} catch(e){this.db.exec('ROLLBACK');throw e;} }
  reserveDiagnosis({id,owner,requestId,fingerprint},limit,globalLimit) {
    return this.transaction(()=>{
      const old=this.db.prepare('SELECT * FROM diagnoses WHERE owner=? AND request_id=?').get(owner,requestId);
      if(old){ assert(old.fingerprint===fingerprint,409,'idempotency_conflict','Request ID was already used with different input.');
        if(old.status==='done') return JSON.parse(old.result);
        assert(old.status!=='pending',409,'request_pending','This diagnosis request is still running.');
      }
      const day=new Date().toISOString().slice(0,10);
      const count=this.db.prepare('SELECT count FROM quotas WHERE day=? AND owner=?').get(day,owner)?.count||0;
      const total=this.db.prepare('SELECT SUM(count) AS n FROM quotas WHERE day=?').get(day).n||0;
      assert(count<limit && total<globalLimit,429,'daily_limit','Daily diagnosis budget reached.');
      this.db.prepare('INSERT INTO quotas(day,owner,count) VALUES(?,?,1) ON CONFLICT(day,owner) DO UPDATE SET count=count+1').run(day,owner);
      if(old) this.db.prepare('DELETE FROM diagnoses WHERE id=?').run(old.id);
      this.db.prepare("INSERT INTO diagnoses VALUES(?,?,?,?, 'pending',NULL,?)").run(id,owner,requestId,fingerprint,Date.now());
      return null;
    });
  }
  saveDiagnosis(id,result){this.db.prepare("UPDATE diagnoses SET status='done',result=? WHERE id=?").run(JSON.stringify(result),id);}
  failDiagnosis(id){this.db.prepare("UPDATE diagnoses SET status='failed' WHERE id=?").run(id);}
  diagnosis(id,owner){const row=this.db.prepare("SELECT * FROM diagnoses WHERE id=? AND owner=? AND status='done'").get(id,owner);assert(row,404,'not_found','Diagnosis not found.');return JSON.parse(row.result);}
  session(id,user){const row=this.db.prepare('SELECT * FROM sessions WHERE id=?').get(id);assert(row && (row.owner===user.id || user.role==='expert'),404,'not_found','Session not found.');return {...row,plan:JSON.parse(row.plan)};}
  event(sessionId,type,payload,eventId,from='expert') {
    const body={type,sessionId,from,eventId,payload,ts:Date.now()};
    this.db.prepare('INSERT INTO events(session_id,event_id,type,body,created) VALUES(?,?,?,?,?)').run(sessionId,eventId,type,JSON.stringify(body),Date.now());
    return body;
  }
  history(id){return this.db.prepare('SELECT body FROM events WHERE session_id=? ORDER BY seq').all(id).map(x=>JSON.parse(x.body));}
  close(){this.db.close();}
}
