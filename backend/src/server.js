import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { parseUsers, validateMachine } from './domain.js';
import { Store } from './store.js';
import { makeDiagnoser, checkGeminiAccess } from './ai.js';
import { createApp } from './app.js';
const integer=(name,value,defaultValue)=>{const n=Number(value??defaultValue);if(!Number.isSafeInteger(n)||n<1)throw new Error(name+' must be a positive integer.');return n;};
const config={
  users:parseUsers(process.env.API_TOKENS_JSON || JSON.stringify({technician:{token:process.env.TECHNICIAN_TOKEN,role:"technician"}})),aiMode:process.env.AI_MODE||'gemini',openaiKey:process.env.OPENAI_API_KEY,
  groqKey:process.env.GROQ_API_KEY,groqModel:process.env.GROQ_MODEL||'openai/gpt-oss-20b',
  geminiKey:process.env.GEMINI_API_KEY,geminiModel:process.env.GEMINI_MODEL||'gemini-2.5-flash-lite',
  model:process.env.OPENAI_MODEL||'gpt-5.4-mini',aiTimeoutMs:45000,
  dailyLimit:integer('DAILY_LIMIT',process.env.DAILY_LIMIT,20),globalDailyLimit:integer('GLOBAL_DAILY_LIMIT',process.env.GLOBAL_DAILY_LIMIT,100),
  maxConcurrent:2,origins:(process.env.ALLOWED_ORIGINS||'').split(',').filter(Boolean)
};
if(!['openai','gemini','groq','mock'].includes(config.aiMode))throw new Error('AI_MODE must be openai, gemini, groq or mock.');
if(config.aiMode==='openai'&&!config.openaiKey)throw new Error('OPENAI_API_KEY is required in openai mode.');
if(config.aiMode==='gemini'&&!config.geminiKey)throw new Error('GEMINI_API_KEY is required in gemini mode.');
if(config.aiMode==='groq'&&!config.groqKey)throw new Error('GROQ_API_KEY is required in groq mode.');
const dataDir=fileURLToPath(new URL('../data/',import.meta.url));
const machines=new Map(readdirSync(dataDir).filter(n=>n.endsWith('.json')).map(name=>{const machine=validateMachine(JSON.parse(readFileSync(join(dataDir,name),'utf8')));return [machine.machine_id,machine];}));
const store=new Store(process.env.DB_PATH||'./storage/techassist.sqlite');
const app=createApp({config,store,machines,diagnose:makeDiagnoser(config)});
const port=integer('PORT',process.env.PORT,8080);
app.server.listen(port,process.env.HOST||'0.0.0.0',()=>console.log(JSON.stringify({event:'listening',port,ai_mode:config.aiMode,model:config.aiMode==='groq'?config.groqModel:config.aiMode==='gemini'?config.geminiModel:config.model})));
for(const signal of ['SIGINT','SIGTERM']) process.once(signal,()=>{const deadline=setTimeout(()=>process.exit(1),15000);deadline.unref();app.close().then(()=>process.exit(0));});

if(config.aiMode==='gemini') checkGeminiAccess(config).then(result=>console.log(JSON.stringify({event:'gemini_access_check',...result})));
