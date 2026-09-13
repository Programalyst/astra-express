# Astra Express Hackathon Preparation

Initial checks 12 September 2026; latest update 13 September 2026, Singapore time.

## Current status: 13 September

Latest result: the first paying railway loop has been verified in Unity Play Mode and the local Chrome Web build. The initial Web export exposed a bridge runtime reference to an Editor-only API; the user fixed the bridge, and the retry succeeded at 10:34 SGT. The original pre-event deadline was missed. See [Playable handoff](PLAYABLE_HANDOFF.md) for launch instructions, validation, current limitations, and remaining deployment work. This result supersedes the earlier readiness statements below.

Implementation is authorized for the [90-minute pre-event milestone](PRE_EVENT_MILESTONE.md), with a separate 30-minute buffer. The Unity MCP tools are now exposed in this session. A native `refresh_assets` call succeeded and polling reached `NO_COMPILATION`: the refresh completed, but that no-op check does not certify newly edited C# or a domain-reload cycle. The previous malformed-arguments issue below is historical, not a current blocker. The supplied Kenney packs and exact solar model are recorded in the asset brief. The current Git status reports only untracked design documents; earlier manifest/lockfile changes are no longer reported. A playable scene, real script-reload validation, Web export, browser performance, and public hosting remain to be proved.

The following readiness notes retain the earlier setup evidence; this current-status section supersedes their stale configuration and planning-only statements.

## Readiness assessment

The available tools cover the proposed Unity browser game. Blender is responding, Web Build Support is installed for Unity 6000.3.24f1, and the Unity editor now responds to health and project-settings requests. Native MCP argument repair and an actual browser export remain separate checks.

The user has pushed a Unity 6000.3.24f1 URP project with Git LFS configuration and added the local bridge package. The manifest and package lockfile contain uncommitted user changes, and the design documents remain uncommitted. These checks do not establish that a game or hosted browser build works.

| Capability | Evidence from this session | Assessment |
| --- | --- | --- |
| Blender MCP | Status call succeeded; Blender reported 5.2.1 LTS, addon 1.6, protocol 5 matching the server | Ready for later mesh creation and inspection |
| Unity Semantic Bridge | Configured and enabled, but arguments are malformed; no Unity tools exposed in this session | Repair configuration before relying on it |
| Unity editor listener | Health and get_project_settings requests succeeded after package setup | Basic connection verified; edits, reloads, and screenshots still need testing |
| Unity editors | Project and responding editor both use 6000.3.24f1 | Keep this version across the team |
| Web Build Support | Unity 6000.3.24f1 has PlaybackEngines/WebGLSupport alongside Unity.app, including build tools and player variations; Hub metadata selects the webgl module | Installed; actual browser export remains untested |
| Browser automation | cua_repl configured and a computer/browser control tool exposed | Available, but no game or hosted build exists to test yet |
| Shell and Git | Available; repository inspection succeeded | Sufficient for C# files, version control, and later build commands |
| Image generation | Available as a tool independently of an MCP server | Optional concept art or bitmap assets; not used during planning |
| codex_app and computer-use | Listed as disabled | No reason identified to enable them for the proposed slice |
| Astra role | Published brief requires Astra throughout development and an explanation of its use in the video | No in-game AI feature specified |

The local Unity bridge implements hierarchy and component inspection, object authoring, ScriptableObject creation and updates, asset discovery, compilation status, console logs, screenshots, Play Mode control, and recent editor events. Those are enough for the proposed workflow alongside C# editing through the shell. No dedicated Web build or deployment tool appeared in the inspected tool definitions; use Unity's normal build workflow when implementation is authorized.

MCP tools assist development. They do not become services that players need in order to run the browser game. A backend is only necessary if the chosen event requirement or gameplay feature needs one.

## Unity configuration issue

The configured command is `uv`, but its argument array currently has two strings. One string contains the quoted text for both `--directory` and the path; the other contains the quoted text for both `run` and `main.py`. These must be four separate arguments.

The proposed replacement for the existing server block is:

```toml
[mcp_servers.unity-semantic-bridge]
command = "uv"
args = ["--directory", "/Users/leonardlin/workspace/unity-semantic-bridge/mcp-editor-bridge", "run", "main.py"]
```

This is a proposed configuration, not an applied change. Preserve any additional settings when correcting the existing block. Reconnect the server or restart the Codex session afterwards and confirm the Unity tools are exposed.

The user has added the package at `/Users/leonardlin/workspace/unity-semantic-bridge/com.gamenami.unity-semantic-bridge`, and the listener responds. Its current source uses port 1073, with GET `/health` and POST `/rpc`; the README also contains older `/mcp` wording. The absolute package path needs a matching checkout or a portable reference on another development machine.

The bridge README flags an object-ID compatibility uncertainty around 6.3. The successful project-settings request establishes basic connectivity with 6000.3.24f1, but hierarchy edits and reloads still need verification. The bridge belongs to the user, so specific fixes or additional operations can be implemented when needed.

Minimal connection proof once project setup is allowed:

1. Health endpoint responds.
2. Project settings and a shallow hierarchy query succeed.
3. A game or scene screenshot returns correctly.
4. Compilation status and console logs are readable.
5. A simple authorized edit survives a script recompile and a fresh hierarchy query.

Unity instance IDs change across reloads, so reacquire them after compilation. The bridge handles editor requests one at a time; avoid overlapping Unity calls. Only one editor should own the listener port during the hackathon.

## Browser build prerequisite

Web Build Support is confirmed at `/Applications/Unity/Hub/Editor/6000.3.24f1/PlaybackEngines/WebGLSupport`. The directory contains the editor extension, build tools, and player variations; the installation's `modules.json` also marks the `webgl` module as selected. No module installation is needed for this editor.

The earlier missing-module assessment checked only `Unity.app/Contents/PlaybackEngines` and missed the sibling `PlaybackEngines` directory. That assessment was incorrect. If the team changes editor versions, verify the matching module separately; Unity documents module management in [Add modules to a Unity Editor installation](https://docs.unity.com/en-us/hub/add-modules).

Do the first real browser smoke test as the first implementation milestone, once building is authorized. A scene working in the editor does not prove that the browser output works. Test the host's WebAssembly MIME type and build compression settings early; [Unity's deployment instructions](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-deploying.html) describe the required configuration.

Agree on one host the team already knows, then verify a shareable release URL during the event. Host and account access are unverified. Browser automation can inspect the loaded page and interact with the game canvas; editor inspection remains the better tool for internal scene and component state.

## Team and event

The team has two people. The published on-site build window is 10:30am to 3:30pm on 13 September; a deployed prototype and a 90-second video explaining Astra's contribution are due at 3:30pm. The user has authorized overnight development, with planning completed first. [Event details](https://luma.com/fdzbrq5b)

The user and teammate refine gameplay and art direction, may supply asset packs, and review lighting and shaders. The assistant owns implementation, integration, debugging, and validation. Use playable checkpoints rather than the original eight-hour schedule.

The current game centres on rover exploration, fog, a shared power battery, solar generation, extractors, conduits, and railway deliveries. The square grid has low-yield 1 by 1 deposits near the colony, higher-yield 2 by 2 deposits farther away, and the highest-yield 3 by 3 deposits in the distant frontier. All deposits are infinite; the incentive to explore is higher output. The old refinery, food chain, and gate are deferred.

## Preparation order

These are recommended next actions. The user created the project and installed the bridge. The assistant has not written gameplay code, downloaded game assets, or built the game; planning remains active.

- Finish reviewing proposed extractor footprints, fog, charging, shortage, and recovery rules in the [game design](GAME_DESIGN.md).
- Correct Unity MCP arguments and verify hierarchy edits and recompilation.
- Make the bridge package reference portable if another machine will open the project, preserving existing user edits.
- When implementation resumes, prove the existing sample scene exports and runs in a browser.
- Review supplied packs and prepare the rover, fog, solar, extractor, conduit, and train assets in the [asset brief](ASSET_BRIEF.md).
- Confirm a submission host and test a release on it early.
- Record actual Astra contributions and reserve time for the 90-second video.

If there is only an hour for preparation, spend it on event requirements, Unity configuration, browser export and hosting verification once authorized, and choosing the art kit. Those remove more schedule risk than producing a large pile of assets or adding new MCP integrations.

## Evidence and remaining uncertainty

Local evidence came from `codex mcp list`, `codex mcp get unity-semantic-bridge --json`, Blender's addon status, the editor installation directories, and the bridge checkout's `README.md`, `mcp_tools.py`, `state_manager.py`, and `EditorBridge.cs`. The corrected Web module finding additionally checks Unity 6000.3.24f1's sibling `PlaybackEngines/WebGLSupport` directory and `modules.json`.

The earlier Codex Doctor network failures occurred in a restricted network sandbox and do not by themselves prove the Mac's normal internet connection is broken. The initial Unity localhost checks also failed, but later health and project-settings requests succeeded after project setup. The readiness table reflects those later results.

Remaining decisions include proposed extractor footprints, fog, charging and recovery rules, tier balance, chosen assets, and hosting. Broader editor-operation compatibility, portable package setup, and browser performance still need testing. The event link, team size, responsibilities, shared battery model, infinite deposit progression, and permission to work earlier are now supplied.
