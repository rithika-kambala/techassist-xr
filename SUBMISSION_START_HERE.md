# TechAssist XR submission guide

## Open the desktop demo
1. Add this project folder in Unity Hub. Use Unity 6000.4.11f1, allow package import and compilation to finish.
2. Open `Assets/TechAssistXRHybrid/Generated/TechnicianAIWorkbench 1.unity`.
3. Press Play. Use Offline demo for the deterministic zero-API-cost presentation.
4. Enter `Oil leaking near pump`, select Analyze report, inspect the machine information and cited training sources, then accept the review to start guidance.
5. Click the highlighted machine component and select Complete Step. Repeat until the session ends.

## Connect the deployed backend
1. In Play mode, select Backend.
2. Enter `https://techassist-xr-api.onrender.com` as the service URL. Do not append `/api`.
3. In Render, open the existing `techassist-xr-api` service, then Environment. Privately copy the value of `TECHNICIAN_TOKEN` into Unity's Technician token field.
4. Select Connect. The first request can take up to 90 seconds while the free service wakes.
5. Analyze one report. The Groq API key stays in Render; it is never the Unity technician token.
6. If a plan is rejected, retain the exact error and inspect Render logs. Do not remove component/source validation to make it pass. Offline demo remains available.

## Build macOS
Stop Play mode. Select `TechAssist XR > Build Desktop macOS`. The output is `Builds/macOS/TechAssistXR.app` and `BuildResult.txt`. A licensed Editor and macOS Build Support are required. The automated build attempt was blocked by Unity headless licensing, so this archive includes source, not a verified player binary.

## Free backend setup
Render service root directory: `backend`; build: `npm ci`; start: `npm start`; health check: `/readyz`; instance: Free. Environment: `AI_MODE=groq`, `GROQ_MODEL=openai/gpt-oss-20b`, private `GROQ_API_KEY`, private `TECHNICIAN_TOKEN`, `DB_PATH=/tmp/techassist.sqlite`. Check `backend/.env.example` for exact supported quota/configuration names. Do not commit secrets. Keep the Groq account on its free plan. SQLite data on Render Free is temporary.

## Verification and remaining work
Run `cd backend`, `npm ci`, then `npm test` with Node 24 or later. Read VALIDATION.md for the evidence and limits. Quest deployment requires an XR Origin, configured controller input and passthrough, Android support and device testing; the Desktop/Quest adapter alone does not configure these.
