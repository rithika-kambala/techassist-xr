# Validation status — 20 September 2026

- Backend: 18 automated tests passed. Provider responses are mocked in these tests; no paid AI calls are required.
- Current Hybrid C# sources, including the macOS build helper: compiled against Unity 6000.4.11f1 assemblies with both new and legacy input symbols. Only deprecated Editor search API warnings.
- Pure session engine: 37 assertions passed against current source.
- Earlier Unity Editor integration evidence: Generated/BackendIntegrationTest.txt records a successful four-step local HTTP session using the mock provider, including target selection and server acknowledgments.
- Public `/readyz`: returned `{"status":"ready","ai_mode":"groq"}` on 20 September 2026. Readiness does not validate a complete provider-generated diagnosis.
- Latest production source-index validation fix is deployed, but an authenticated live Groq diagnosis through Unity remains unverified.
- macOS standalone batch build attempted; Unity licensing rejected the headless entitlement. No verified standalone binary is supplied. Use the build menu from a licensed Editor.
- Quest rig, passthrough, Android build and headset acceptance testing remain pending. Do not label this release a completed Quest application.
- Current project is Unity 6.4 (6000.4.11f1), not a validated Unity 2022.3 project.

Render Free uses ephemeral SQLite storage. Server confirmations are real, but history is not durable across service replacement/restart. Training machine and evidence are synthetic; this is not an approved repair system.
