# TechAssist XR deployment

The public training service is https://techassist-xr-api.onrender.com. It runs in Groq mode. See SUBMISSION_START_HERE.md for Unity connection and demonstration steps, and VALIDATION.md for tested results and pending acceptance work.

## Render configuration

Use the existing techassist-xr-api service. Root directory: backend. Build: npm ci. Start: npm start. Health check: /readyz. Instance: Free. No paid disk is required.

Set these environment variables privately in the service Environment page:
- AI_MODE=groq
- GROQ_MODEL=openai/gpt-oss-20b
- GROQ_API_KEY: your private Groq key
- TECHNICIAN_TOKEN: a random secret of at least 32 characters
- DB_PATH=/tmp/techassist.sqlite
- DAILY_LIMIT=10
- GLOBAL_DAILY_LIMIT=20

A technician token is the client credential; it is not the provider API key. Keep both out of Git and Unity assets. The checked-in Blueprint supports other provider choices; use the existing service environment for this deployment.

The service is configured without automatic deployment. After pushing a server change, use Manual Deploy in Render and check /readyz. Readiness does not verify a live AI completion. A full authenticated diagnosis and session test is still required after the latest validation fix.

## Local development

Use Node 24 or later. In backend, run npm ci, copy .env.example to .env and privately configure TECHNICIAN_TOKEN. Set AI_MODE=mock for a zero-API-cost local run. Start with node --env-file=.env src/server.js. Run npm test for the automated backend suite.

Unity Editor permits http://127.0.0.1:8080 for local development; player builds require HTTPS. Deploy backend/src/server.js rather than the legacy repository-root relay.

## Limits

Render Free storage is ephemeral: SQLite history can disappear on restart or deployment. Free-provider quotas can interrupt generation. No automatic paid fallback is configured. Training content and geometry are synthetic, and generated instructions are not approved machinery repair procedures. A durable production system needs individual identities, reviewed machine documentation, durable storage and operational monitoring.

The current Unity project uses 6000.4.11f1. Quest passthrough and standalone release acceptance remain pending. See the submission guide for the macOS build menu.
