# TechAssist XR deployment

This update adds a real authenticated backend, Gemini/OpenAI diagnosis integration, persisted sessions/completions, and a Unity connection form. It is a deployable **training pilot**, not an approved machinery maintenance system. The supplied A102 profile and manual excerpts are synthetic, explicitly marked `training_only`. Replace them with reviewed machine documents for any real training rollout.

## What's included

- `backend/`: Node 24 service, pinned dependency/lockfile, SQLite persistence, tests and Dockerfile.
- `render.yaml`: Render Blueprint, single paid Starter service with a 1 GiB persistent disk.
- `.github/workflows/backend.yml`: tests on Node 24 and Docker build on GitHub Actions.
- `Assets/TechAssistXRHybrid`: Unity model, workbench, authenticated connection form and integration test.

The existing repository root `server.js` is a legacy relay. Deploy **backend/src/server.js**, not the old relay. The new service preserves the camel-case WebSocket envelope but enforces authentication, session ownership and stored-plan ordering. Approved steps are emitted automatically; arbitrary expert-authored STEP_STARTED and ANNOTATION_CREATED writes are intentionally not accepted by this pilot endpoint.

## Public deployment on Render

1. Commit the new backend, Render Blueprint, CI workflow and changed `Assets/TechAssistXRHybrid` files to a branch in your GitHub repository. Review unrelated local changes separately. Do not commit `.env`, tokens, node_modules, SQLite files or Library.
2. Sign in to Render, choose **New > Blueprint**, select `rithika-kambala/techassist-xr`, and choose the branch containing these files.
3. Review the selected service and disk pricing. The Blueprint requires a paid service because SQLite data must survive restarts. It is not a free-tier configuration.
4. Set **GEMINI_API_KEY** privately when prompted, from your own Google AI Studio free-tier project with billing disabled. Leave `AI_MODE=gemini` and `GEMINI_MODEL=gemini-2.5-flash-lite` for real AI. There is no automatic fallback to mock AI.
5. Render generates **TECHNICIAN_TOKEN** as a secret. After deploying, obtain its value privately from the service Environment page. Do not put it in GitHub or a Unity scene.
6. Wait until `https://YOUR-SERVICE.onrender.com/readyz` returns `status: ready`. This checks service/storage readiness; it does not spend tokens to verify an AI completion.
7. Open the Unity `TechnicianAIWorkbench 1` scene. Press Play, choose **Backend**, paste the Render service base URL and technician token, and select **Connect**. Do not append `/api` to the base URL. The password field clears after submission; the token remains in memory only.
8. Close the connection panel, enter `Oil leaking near pump`, choose **Analyze report**, review the source-linked plan, and start guidance. Successful live completions are now persisted by the server before Unity advances.
9. Confirm a real diagnosis response reports `mode: gemini`; use an authenticated GET session request to verify completed steps. A successful `/readyz` alone does not verify provider credentials/model access.

No Render resource or public endpoint has been created by this delivery. Live deployment needs the authenticated GitHub/Render account and a valid Gemini or OpenAI key. Docker execution and a real provider call also require those respective environments/credentials; local tests use an injected provider or explicitly labeled mock mode.

## Local development

Use Node 24 LTS (the service also runs on Node 26).

```sh
cd backend
npm ci --ignore-scripts
cp .env.example .env
# Privately fill TECHNICIAN_TOKEN with a random 32+ character value.
# For a zero-cost local test set AI_MODE=mock; for real AI also set GEMINI_API_KEY.
node --env-file=.env src/server.js
```

Generate a technician token locally with `node -e "console.log(require('crypto').randomBytes(32).toString('base64url'))"`. Treat the output as a secret. The server loads environment variables; `.env` is read only when the `--env-file` flag is supplied.

Unity Editor permits `http://127.0.0.1:8080` for local testing. Player builds require HTTPS. A Quest headset's localhost is the headset; use the public Render URL on-device.

`npm test` runs offline integration tests without OpenAI charges. `TechAssist XR > Run Backend Integration Test` in Unity expects the service at `127.0.0.1:8089` with `AI_MODE=mock` and the clearly test-only token found in that Editor test. Never reuse that test token on Render. The test traverses the real HTTP client/server path and persistent acknowledgments, with a mock AI provider.

## API contract

Every `/api/*` endpoint and `/ws` upgrade requires `Authorization: Bearer <technician-token>`. `/healthz` and `/readyz` are public. Do not put tokens in URLs.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/healthz` | Process liveness |
| GET | `/readyz` | Service/storage readiness and configured AI mode |
| GET | `/api/machines` | Server-owned machine catalog |
| GET | `/api/machines/A102` | A102 components and source documents |
| POST | `/api/diagnoses` | Generate and persist a source-linked plan |
| POST | `/api/sessions` | Start a reviewed diagnosis as a session |
| GET | `/api/sessions/{session_id}` | Read persisted progress and event history |
| POST | `/api/step-completions` | Idempotent, ordered completion |
| POST | `/api/sessions/{session_id}/end` | Explicitly end an active session |
| WebSocket | `/ws` | Authenticated JOIN, SNAPSHOT, event stream and completions |

Diagnosis request:

```json
{"machine_id":"A102","technician_report":"Oil leaking near pump","request_id":"unique-client-request-id"}
```

The server ignores client-supplied manuals/components. It loads the reviewed record from `backend/data/A102.json`, sends it and the report to OpenAI Responses using a strict JSON schema, validates returned IDs/order/field lengths, and stores the result. A successful response includes `diagnosis_id`, `mode`, `training_only`, `summary`, `needs_expert`, `source_ids`, `source_documents`, and `steps`. Each step also carries its supporting `source_ids`. When documents are insufficient, `needs_expert=true` and `steps=[]`; no session can be started.

Session request:

```json
{"session_id":"unique-session-id","diagnosis_id":"returned-diagnosis-id","reviewed":true}
```

Completion request:

```json
{"session_id":"unique-session-id","event_id":"unique-completion-id","step_number":1,"target_component":"control_panel"}
```

Successful completion response:

```json
{"session_id":"unique-session-id","event_id":"unique-completion-id","accepted":true,"duplicate":false}
```

Reuse the same request_id for diagnosis retries and the same event_id for completion retries. Reusing an ID with different content returns 409. The final accepted step sets the stored session status to `completed`. GET session returns `completed_steps`, `status`, `steps` and `events` and can support a future client resume screen. Current Unity UI does not automatically restore a session after app termination; in-session failed completions can be retried.

Errors use `{"error":{"code":"...","message":"...","request_id":"..."}}`. The service returns 401 for invalid credentials, 404 for unknown/inaccessible objects, 409 for ordering/idempotency conflicts, 429 for quotas/rate limits, and 502/504 for provider failures. No prompt, report or credential is written to application logs.

## Credentials, limits and persistence

- Default secret `TECHNICIAN_TOKEN` maps to one pilot technician account. For separate users, configure `API_TOKENS_JSON` with a map such as `{"alice":{"token":"a-long-random-secret","role":"technician"},"mentor":{"token":"a-different-long-random-secret","role":"expert"}}`. That map replaces the default token configuration.
- Expert accounts may inspect sessions and subscribe to events; technician accounts can only read/write their own sessions. Do not distribute one token to users who need isolated histories.
- Rotate/revoke a token through Render environment settings and redeploy. End-user login/SSO and expiring access tokens are future production identity work; static pilot tokens are not a general enterprise identity system.
- Defaults: 20 AI attempts per user per UTC day, 100 globally, maximum two concurrent AI requests, one per user. Failed provider attempts count toward the limits. Completed diagnosis retries reuse stored output and do not spend another request.
- OpenAI model is configurable through `OPENAI_MODEL`; default `gpt-5.4-mini`. Provider calls request at most 4000 output tokens and have a 45-second timeout. Also set provider-side spend limits. `store:false` is sent to Responses; this is not a claim of zero data retention by the provider.
- SQLite runs with WAL and FULL synchronous mode. Keep `DB_PATH` on `/var/data` on Render. A persistent disk limits the service to one instance; do not enable horizontal scaling. Move to managed Postgres before multi-instance deployment.
- Session records include report-derived content and model output. Add a retention policy, backups and access review for a real rollout. For a consistent SQLite backup use SQLite's online backup facility or stop writes before copying the database and WAL; do not assume copying only the active `.sqlite` file is consistent.
- `ALLOWED_ORIGINS` is an explicit comma-separated browser CORS allowlist. Native Unity does not require it. WebSocket clients must send Authorization as an upgrade header; browser clients need a trusted proxy/token-exchange design rather than putting a bearer token in a query string.
- Render terminates HTTPS. Never bypass TLS validation in Unity.

## Deployment verification

1. Verify `/readyz`, then authenticated catalog access; an unauthenticated catalog request must be 401.
2. Request one live oil-leak diagnosis; check `mode=openai`, referenced sources and correct component IDs.
3. Review/start the plan, complete every step from Unity, and check persisted session status.
4. Retry a completion with the same event_id; verify duplicate=true and no extra progress.
5. Restart the service and verify the session/history remains available.
6. Verify a second technician token cannot read the first technician's session.

No live provider inference, public Render deployment or hardware Quest build has been verified yet. The existing local Unity project is Unity 6000.4.11f1. The new live-ready scene is `Assets/TechAssistXRHybrid/Generated/TechnicianAIWorkbench 1.unity`; the earlier workbench scene is retained. Quest passthrough still requires device-specific setup and testing; this backend deployment does not configure headset hardware. The machine model and sources remain training fixtures.

Official references: [OpenAI Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs), [Render Blueprint](https://render.com/docs/blueprint-spec), [Render persistent disks](https://render.com/docs/disks).

## Free AI option

The default provider is Gemini 2.5 Flash-Lite. Obtain your own key at https://aistudio.google.com/api-keys and keep the Google project on the Free tier without enabling billing. Never paste API keys into chat, GitHub or Unity. Set GEMINI_API_KEY only in the private server environment. Free quotas depend on the project and availability; a 429 is displayed as a quota error with no automatic paid fallback. The offline Unity demo remains available without a key.

Google's free tier may use submitted content to improve its products. Use only the supplied synthetic training data for the demo, not confidential machine documents. See https://ai.google.dev/gemini-api/docs/pricing and https://ai.google.dev/gemini-api/docs/rate-limits . Provider integration follows https://ai.google.dev/api/generate-content .

OpenAI remains optional: set AI_MODE=openai, OPENAI_API_KEY and OPENAI_MODEL. An OpenAI key is not assumed to include free usage. Free Gemini API usage does not make Render's persistent disk hosting free. No paid resources have been provisioned.

## Unity version and assets

The existing repository uses Unity 6000.4.11f1 (Unity 6.4), matching the installed editor and validation environment. Unity 2022.3 and Quest hardware have not been validated. TextMesh Pro essential resources are included so the saved workbench scene resolves its fonts in a fresh checkout.
