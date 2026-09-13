# Astra Express: coached playtest and iteration

Date: 2026-09-13. Tested locally at http://127.0.0.1:8090/ in the Codex browser at 1280 × 720. This is an agent-led interaction test with prior knowledge of the game, not a blind new-player study. All construction and movement were performed through visible game controls, without changing simulation state programmatically. The user's original running colony was preserved in its own tab.

## Baseline: fresh colony with live Pip

Played exploration → extractor → conduits → rails → dispatch → two paid deliveries. Credits: 500 → 350 (extractor) → 338 (12-credit power connection) → 320 (18-credit rails) → 384 after eight ore delivered. This run used the previous Responses backend; actual screenshot-based replies were visibly labelled **Live screen + game state**.

| Observation | Assessment | Change selected |
| --- | --- | --- |
| Pip identified the idle rover and highlighted an eastward frontier tile. Clicking it moved the rover and revealed labelled ore. | Easy to act on. The highlight was more useful than coordinates alone. | Preserve Show me and visible labels. |
| Numbered port labels accepted clicks. Starting a segment spent nothing; clicking its endpoint charged the displayed cost. | Clear and predictable. | Preserve transparent suggestions and explicit preview cost. |
| Power completion removed the ghost and displayed POWER CONNECTED with continuous cyan cabling. Rail completion enabled Dispatch Train. | Successful actions were visible. | Preserve these state transitions. |
| Dispatch changed the button to TRAIN RUNNING; the train moved and deliveries increased the top-bar counters. | Main dispatch path succeeded. | Test reassignment and pause separately. |
| A long live reply filled the panel; question field and live-screen switch/status required scrolling offscreen. | Difficult to ask, inspect status, or dismiss after scrolling. | Keep header, actions, form and status fixed; scroll only explanation. |
| One model reply said placing an extractor could immediately start production, with power described as a later hookup. | Incorrect dependency explanation. | State explicitly in canonical guidance and server instructions that power is required before production. |
| First-delivery fallback said “tomine”. | Internal state name was unclear. | Use “travelling to the mine”. |
| Paid deliveries repeatedly replaced live explanations with local tips, even though the recommended action remained unchanged. | Hard to finish reading an answer. | Invalidate on strategic changes and affordability, not every income tick. |
| Asking about switching mines while a screen request was in progress cleared the field without showing a queued state; the visible advice remained a capacity upgrade. | Unclear whether the question was accepted. | Queue explicit questions visibly, retry after stale context, preserve text on failure; test supported switch-mine guidance. |

## Iteration 1: coach readability and reply continuity

Changes: fixed action/header/footer areas, scrollable explanation, visible question queue, explicit-question retry after a changing frame, readable train phase, correct mine-power dependency, and strategic context signatures that preserve relevant replies across deliveries while still rejecting purchases, construction, route edits and affordability changes.

Automated check: 17 guidance tests passed in the first pass (20 after the second-pass regressions), including income continuity, affordability transitions and mine dependency wording. Browser retest confirmed that long live replies can scroll while Dismiss, Show connection, What next, the question field and live status stay visible. Asking during a request displayed “Question queued”, followed by “Reading the screen to answer you”.

After the coordinated Agents backend restart, two actual unpowered-mine screenshots completed through the hosted session. The live answer to “Does a new extractor need power before it can produce ore?” was: “Yes. A new extractor must have power and be connected before it can produce ore…” This corrects the previously observed dependency error. Following its exact port steps powered the mine; rails and dispatch succeeded again.

## Iteration 2: gameplay and button interface

Second-mine baseline: travelled north from the first route, revealed another labelled 1×1 ore patch, placed the extractor, and extended power and rails from the existing corner for 12 and 18 credits. The partial-route guide correctly started at marker 2 and finished at marker 3, reusing the built corridor.

New problems reproduced: Pip still suggested train capacity after discovering the second deposit. Once the second mine had power and rails, the sidebar showed a disabled **PARK CURRENT TRAIN FIRST** button, the completion banner still said to dispatch, and Pip suggested more power. The required train reassignment was not explained where the player was working.

Selected fixes: recognize additional revealed ore, show an actionable **Park to switch mine** control on the selected unserved extractor, wait for parking, then offer Dispatch Train. Explain that only one train exists and the previous mine retains its stored ore. Keep the completion banner and coach steps consistent. Add visible Power → Rails → Train readiness and readable train status. Custom generated button artwork and a clearer toolbar were requested during this pass and are being integrated. Final build/retest results follow when complete.

## API verification and visual analysis

Separate implementation and review agents are auditing/migrating the hosted OpenAI Agents API. See `AGENTS-INTEGRATION.md` when complete. A separate user-visible task is analysing shader, emissive resource and asset improvements; shader changes are not assumed implemented by this playtest.

The separate Agents review and integration completed successfully. Before the final instruction refresh, actual in-game health counters recorded five completed frames, zero failures, one created session and four reused turns. The selected second mine subsequently filled to 24/24 while its ore was not being collected; the old candidate set let the model recommend capacity, incorrectly treating track connectivity as assignment. The final policy and prompt explicitly separate `railConnected` from `served`.

## Rebuilt interface retest

Unity Web build succeeded: 67,360,887 bytes; reported build step 26 seconds (`Logs/playtest-interface-web-build.log`). At 1280×720, all 15 generated icon concepts loaded, the dark panel palette rendered correctly, toolbar labels/costs/key badges fit, and the extractor readiness cards and bottom focus buttons did not overlap. The same first route again reached Dispatch train and Train running. Pausing changed the primary action to Resume colony; clicking it resumed the service.

The second discovery now produced “You found another ore patch”, including the 150-credit cost, power/rail requirements and single-train rule. A real Agents reply described the newly revealed patch and current paying route. The new second-mine readiness display showed Power Linked / Rails Linked / Train Busy, with an enabled Park to switch mine action. The completion banner and local coach gave the same order: park, wait, dispatch.

Two additional issues found during the rebuilt retest were fixed in the Web template and served copy: flex-based icon buttons could override the HTML hidden attribute; and Show me left pointer-based input capture over Pip, swallowing a number-key shortcut. Hidden controls now stay hidden. Show me collapses Pip while retaining its target highlight for seven seconds, returning game controls immediately. Fresh-tab verification is recorded below once complete.

Generated-art originals, 256px Unity copies, 128px browser copies, exact prompts, provenance and contact sheet are preserved in the project. See `Art/ButtonIcons/README.md`. The available built-in image tool has no model selector; Image 2.5 is not verified. The icons augment readable labels rather than replacing them.

## Final verification

- Park to switch mine changed to Parking at colony, then Dispatch train after arrival. Dispatching assigned the train to the second, full (24/24) mine. It travelled up the branch and completed two paid deliveries: 92 → 100 ore, 23 → 25 trips, 876 → 940 credits. Full storage did not block dispatch. The capacity button then increased the displayed capacity from 4 to 8 and advanced its next price from 100 to 200 credits.
- A real live answer to “Why is the train not collecting ore from this second mine?” correctly explained that the single train was assigned to the other mine and must park before reassignment. It preserved the exact three local steps. Relevant live advice remained visible as freight credits increased.
- The final build succeeded: 67,361,091 bytes; build step 23 seconds (`Logs/playtest-final-web-build.log`). It includes the shortened payment label and the last Web-template fixes.
- Fresh-tab test confirmed Show me collapses Pip, keeps Look here visible, and permits the immediate 2 shortcut to select Extractor without moving the rover or spending credits. In a paused-state tip, the unavailable Show me control was absent while What next, Ask, Dismiss and live-screen status remained visible.
- 20 deterministic coach-policy tests passed. The backend implementation and independent reviewer both passed the 15 backend tests; these use a mocked upstream and real local HTTP. Unity compilation/build and visible gameplay are separate evidence.

Reliability limit: in the longer final gameplay run, one aggregate snapshot recorded 6 received frames, 5 completed and 1 failed, with 2 sessions created, 4 reuses, 2 deletions and 1 historical cleanup failure. Subsequent live answers succeeded. The aggregate counters do not establish the failed request's cause, so it is not attributed to a timeout or model error. Local game-state tips remained available throughout play. Remote session deletion is best effort with retries; see the Agents integration document. Earlier five-frame/zero-failure measurements above describe the earlier run, not an all-session success rate.

Remaining design work belongs to the separate visual-analysis task: more distinct rail/power preview patterns, persistent ore identity around extractors, selective emissive materials and optional bloom. Its shader is a review prototype, not an imported or validated game shader. No Blender MCP capability was callable in this session.

Handoff: final fresh-tab inspection confirmed the payment label reads “8 credits / ore” without clipping. A fresh, unpaused colony with Explore selected and Pip collapsed is left open on the final build; the original player tab is preserved separately. The disposable two-mine test tab was closed after its successful deliveries and upgrade checks.


## 2026-09-13: next-click guidance and contextual panels

The player reported invisible numbered markers, long advice and panels obscuring the board. Three subagents split the crosshair overlay, advice policy and contextual Unity panels. The bottom toolbar method was compared against its pre-change snapshot and preserved.

Changes: one unnumbered next-click instruction; a bright, click-through crosshair over the actual tile, port or control; optional collapsed details; dismissible cues; and an explicit Show target control for hidden targets. Only active context panels are displayed. Building mode clears the sidebars, connecting shows compact connection status, and tile picking clears the panels. Selecting Solar, Extractor or Plant now takes priority over optional fleet upgrades while keeping affordability and placement checks.

Live playtests at 1280 x 720 and 1028 x 940 followed the crosshair through revealing ore, placing an extractor, connecting power and rails, and dispatching a train. The latter run confirmed a paid delivery (320 to 352 credits, 4 ore, 1 trip). It also placed a suggested solar footprint, connected it through the colony port, and visibly increased solar generation from 2 to 4 power/second. Picking a task tile hid the panels and selected tile (11, 7) without moving the rover. Dismissing a cue kept it hidden for the current step; switching the copilot off hid its UI.

The loop caught and fixed three specific issues: Cancel text wrapping in the compact connection strip; missing Explore guidance between completed rails and the now-hidden Dispatch control; and generic train-upgrade advice overriding a manually selected Solar tool. A final overlay pass added nearby panel-edge positions so labels remain near their crosshairs.

Validation: 55 deterministic policy tests pass, including compact-sidebar dispatch handoff, construction intent, missing anchors, unavailable routes and credit limits. The backend subagent passed 42 tests with mocked upstream calls. Unity WebGL compilation succeeded in Logs/next-click-guidance-final-build.log. Served coach.js, coach.css and coach-policy.js match their source templates. These checks complement the visible playtests; they do not constitute a new end-to-end evaluation of remote planner reliability.
