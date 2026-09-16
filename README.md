# TechAssist XR — Pavithra Work Package

Deadline: 10 September 2026

## Scope
Pavithra owns:
1. AI manual → structured procedure generation
2. Expert dashboard
3. Basic backend/database
4. API/event contract documentation
5. Session report

## MVP
Manual PDF is processed BEFORE the demo. One LLM call produces 3–4 steps:
step_number, title, instruction, target_component.

The live demo must not depend on a live AI call.

## Run order
1. Put the demo manual in `data/manual.pdf`.
2. Run `scripts/extract_manual.py`.
3. Generate/validate `mock_data/procedure.json`.
4. Start backend.
5. Open dashboard.
6. Test session create → join → step advance → annotation → complete → report.

## Important contract
`target_component` must exactly match Shreya's Unity object/component names.
Do not invent a second naming convention.
