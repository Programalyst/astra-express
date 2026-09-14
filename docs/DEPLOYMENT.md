# OpenAI Sites deployment

> **Current source after the Astra migration:** local coaching and planning require `server/coach_server.py`, hosted Agents API sessions, and `gpt-6-astra`. The published static Sites release documented below still contains the earlier direct Responses transport. Static Sites hosting cannot run the Python server or safely contain a shared API key; publishing this migration requires a separately hosted backend or a supported server-side proxy. No new Sites release has been published for this change.

## Live site

Production URL: https://astra-express.leonard-lin-2003.chatgpt.site

Latest release: version 11, reported succeeded at 23:05 Singapore time on 13 September 2026 (deployment `appgdep_6aa6bb943dc48191a7938782477f7a73`). Deployment source: `d3b1659ceb5f5fc4e72edcf7436a408994bec220`; Unity checkpoint: `372c900`, with a clean source checkout at export. Adds the Martian splash-art loading backdrop and automatic train dispatch for connected mining services, retaining the previous visual/gameplay upgrades. Web export succeeded in 23 seconds. Compressed payload: 27,637,185 bytes in 15 pieces; the separate backdrop JPEG is 465,587 bytes and content-fingerprinted. Release entry: `play-ddaf3641eb4215b5.html`. The loading screen was visually checked locally and its packaging regression test passed before this deployment. Existing public access and browser-provided API keys are unchanged. Publication is confirmed by Sites; no additional live browser playtest was performed for this release.

Previous release: version 10, reported succeeded at 22:21 Singapore time on 13 September 2026 (deployment `appgdep_6aa6b158784c8191ac7f24cdb53a0eb1`). Deployment source: `68832649630d928ad39d7e4f436c84fcc9dba499`; Unity checkpoint: `f25940f`, with a clean source checkout at export. Includes the research pod colony, animated drill extractors, resource visuals retained beneath extractors, raised resource labels, removed ramp labels, disabled focus-loss auto-pause, and lighting tuning. Web export succeeded in 33 seconds; the packaging regression test passed. Compressed payload: 27,631,718 bytes in 15 pieces. Release entry: `play-5e5ac7ebb342823b.html`. Existing public access and browser-provided API keys are unchanged. Publication is confirmed by Sites; no additional browser playtest was performed for this release.

Previous release: version 9, reported succeeded at 21:36 Singapore time on 13 September 2026 (deployment `appgdep_6aa6a6ae463081919da4399ac1085a0d`). Deployment source: `bd0deb7204acdb319b75d919fa9c6b1259bda2cb`; Unity checkpoint: `878deea`, with local lighting tuning present during this workflow. Includes continuous windswept Martian terrain, the 32×32 grid, Synty APC rover, Single Crystal Fluxite deposits, and accumulated post-processing. Web export succeeded in 45 seconds; 33 focused packaging/browser API tests passed. Compressed payload: 24,462,138 bytes in 13 pieces. Release entry: `play-2a7a181ebee89caa.html`. Public access and browser-provided API keys are unchanged; no backend or secret was uploaded. The live root page's build configuration and script references match the new release; whole-HTML byte comparison differs because Sites transforms the served document.

Previous release: version 8, reported succeeded at 16:21 Singapore time on 13 September 2026 from `aca01dfc08d250cb106c32c935d9149a529cd98b` (deployment `appgdep_6aa65ce5b6b081918c5370b79ca55acf`). It adds content-hashed JavaScript/CSS filenames and a release-specific entry, `play-b0252fbd556aa500.html`. Packaging determinism, references, and payload integrity are covered by a new test; all 113 JavaScript tests pass. Public access is unchanged. Do not assume the new entry is available until public content verification passes: Sites propagation has lagged behind its succeeded status.

Follow-up verification after 16:27: both the main URL and release entry now reference version 8's hashed contract, planning, and API scripts. The live `astrabot-api.cf1d32d0691f0313.js` matches the local deployed file byte-for-byte, and the canonical settings script also matches. Version 8 has now propagated; reload the old browser page to replace its in-memory backend-based client.

Verified public content at 16:22: the main URL serves the version 7 page and the correct direct-OpenAI transport, contract, and planning scripts (all three byte-for-byte equal to the local build). This fixes the old backend-dependent key flow after a browser reload. At 16:15, despite version 7's earlier succeeded status, the public site still served version 6 scripts; `/api/coach/config` returned fallback HTML, producing the reported unexpected `<` JSON error. Query-string cache busting and no-cache headers did not fix it. Reissuing version 7 deployment (`appgdep_6aa65c0b19c08191afaf28ce3d78a4f8`, succeeded 16:17) also did not immediately change public content. Verify actual public files when investigating deployment problems, not status alone.

Version 7 was reported published at 16:13 Singapore time from `cacce7a53de5590a4dcbdb87f2a02a835c0e7540` (deployment `appgdep_6aa65b14d2d08191818d5252596559be`). It adds direct browser-to-OpenAI Responses coaching and planning with player-provided, tab-only keys; no game backend is required. Unity Web build and 112 JavaScript tests passed. Compressed payload: 21,032,804 bytes in 12 pieces. A successful real-key inference still needs owner verification. The exact deployed static files are committed in the deployment-only checkout.

Version 6 was published at 15:51 Singapore time on 13 September 2026 from `576006d6e084867722ff839cdc0408b2f23bbdcc` (deployment `appgdep_6aa655f76c7c81919757992efb174e16`). Its Unity source checkpoint is `75da91c`. It introduced the latest AstraBot guidance, routing, and temporary API-key settings UI, which still required the separate backend. Compressed payload: 21,032,804 bytes in 12 pieces.

Version 5 was published at 14:58 Singapore time on 13 September 2026 from `d1ab7ba33c702cdace6edabb8ea61cb4e8231dc7` (deployment `appgdep_6aa64973024481918d6c062db1a11593`). It includes the merged copilot UI and rule-based tips, fog and power-flow visuals, paired cliff prefab, camera updates, and power-flow initialization fix. The Unity source checkpoint is `1b56e00`; packaging includes the copilot scripts/styles/icons and calls both copilot startup hooks after Unity loads. Compressed payload: 21,019,906 bytes in 12 pieces.

The owner explicitly chose to publish earlier releases without the AI backend due to time constraints. Those releases supported local tips but not hosted AI. Version 7 removes that backend dependency: players can connect their own OpenAI key in settings. No Python server, API key, or other secret was uploaded.

Version 4 was published at 14:11 Singapore time from `b976723e520496fcc47f0ba483110fb3121bd5a1` (deployment `appgdep_6aa63e7bc34c8191b897bfac4724ead1`). It introduced both raised plateaus and the rover-follow/35° camera. Compressed payload: 20,160,910 bytes.

Version 3 was published at 13:08 Singapore time from `1430220a4cee7d9e853ab155f275bab7059a4392` (deployment `appgdep_6aa62fd9c24c8191bf27591a7ff5faed`). It introduced the flat Kenney Space Kit floor. Compressed payload: 20,152,353 bytes.

Version 1 deployed successfully on 13 September 2026 at 11:55 Singapore time. Deployment ID: `appgdep_6aa61e5129dc819194b19e3913608245`.

Access changed to public with the owner's authorization on 13 September 2026 at 12:00 Singapore time (access policy revision 2). Anyone with the URL can visit. The owner confirmed that the hosted game loaded after switching from the venue Wi-Fi (0.28 Mbps download) to a mobile hotspot.

Version 2 deployed successfully at 12:37 Singapore time from `711f151ae3f185768071678c467e0b7f86d5ac4b` (deployment `appgdep_6aa6285db1808191904929fc85f45a71`). It adds streamed byte progress, a slow-connection message, 60-second inactivity timeouts with retries, and limits asset downloads to four concurrent requests. Focused loader checks cover progress before completion, retry accounting, byte assembly, and terminal failures.

The reported splash-screen stall was investigated in Chrome: both scripts loaded, asset requests returned HTTP 200, and downloads advanced extremely slowly (one 1.76 MB piece took 157 seconds). The original progress indicator only advanced after a whole piece finished. No Unity startup error was observed at that point; the initial downloads had not completed. A separate 2 MB request completed successfully in 17.8 seconds. Version 2 improves feedback and recovery but cannot repair the underlying network throughput.

## Packaging

The current Web template requires the same-origin Agents server. Keep the prompts, planning validator, transport, and settings scripts together; the packaging script copies all referenced template scripts but cannot package the Python service. Never add an API key to static files or hosting configuration. The currently published static release retains its historical direct Responses client until a server-side deployment architecture is added. See [COACH.md](COACH.md) for local setup and credential boundaries.

The packager fingerprints referenced JavaScript/CSS, JPEG artwork, and the loader using file-content hashes. It writes both `index.html` and a deterministic `play-<content-hash>.html` entry. This avoids reusing asset cache keys between releases; a fresh entry is useful when the root document remains cached, but cannot bypass delayed platform deployment propagation. `node --test tests/sites-packaging.test.cjs` checks this behavior, including matching backdrop preload/image references. Preserve prior entries/assets for existing open pages.

The game remains a Unity Web build, hosted entirely on OpenAI Sites. No iframe, external asset host, cloud bucket, backend API, or runtime secret is required.

The original export contains a 52,718,487-byte WebAssembly file and a 12,784,941-byte data file. A direct source upload failed with `artifacts_git_receive_pack_object_too_large`. The response did not identify the exact file-size limit; do not claim that it confirmed a 3 MB limit. An earlier HTTP/2 transport failure was a separate problem, resolved for subsequent upload attempts with per-command HTTP/1.1 and a 32 MiB Git request buffer.

`scripts/prepare-sites-direct.mjs` compresses the two large assets with gzip and splits the compressed bytes into files of at most 2,000,000 bytes. It preserves the Unity loader/framework files and builds a small static entry page. The current build uses 12 payload files totalling 21,032,804 compressed bytes rather than 68,600,510 uncompressed payload bytes.

`scripts/sites-loader.js` downloads the pieces in order, decompresses them using the browser's `DecompressionStream`, verifies each complete asset's size and SHA-256, and supplies Blob URLs to Unity. The Unity application and resource accounting code are unchanged. The custom loader reports progress, retries failed piece downloads, and displays a reload message on errors. It requires a modern browser with gzip `DecompressionStream`, WebAssembly, and WebGL support; the packaged game has been exercised in local Chrome, not all browsers or mobile devices.

## Local workflow

1. Exit Play Mode and export through **Astra Express > Build Web** to `Builds/Web`.
2. Run `node scripts/prepare-sites-direct.mjs` from the Unity repository root.
3. Preview with `python3 -m http.server 8093 --bind 127.0.0.1 --directory Builds/SitesDeploy/dist`.
4. Open `http://127.0.0.1:8093/`. The compressed package has been verified to reach the game and respond to rover movement, including battery consumption and fog exploration.

`Builds/SitesDeploy` is a separate, deployment-only Git checkout. It contains the complete static output under `dist` and the hosting identity under `.openai/hosting.json`. Keep this checkout between deployments. The Unity repository ignores `Builds/`, so game source commits do not contain exported binaries or this checkout's Git history.

The existing Sites project ID is `appgprj_6aa61b09b620819190cd5e343eb9007a`. Reuse it; never create another Site just because a build folder or local credential is missing. If preparing from another machine, recover this Site's existing source checkout or restore this exact ID before publishing. The packaging helper preserves an existing ID but cannot discover a missing one.

## Publishing

Current owner preference: batch gameplay upgrades and validate locally. Do not publish a new Sites release after each change; wait for an explicit deployment request. The 14:11 release publishes the accumulated terrain and camera upgrades following the owner's request.

Use the Sites connector and its hosting skill. Push the exact deployment checkout state to the source repository returned for the existing Site. Obtain temporary credentials through Sites when required; never save credentials in a remote URL, Git configuration, files, or this document. Use per-command authentication and wait for a successful push before recording the full `git rev-parse --verify HEAD` value.

Package the static output using the Sites hosting helper, save a version associated with that pushed commit, and deploy the returned saved version. Keep the archive unchanged through saving. The accepted first small-file snapshot is `9630433463183e00f31203c82f792913d28014dc`; the deployment archive was accepted with 16 files and 21,032,960 bytes. Subsequent changes should create normal deployment commits, not amend already published history.

The first uncompressed snapshot was rejected and never published. Only that unpublished snapshot was amended to remove the oversized Git objects; merely deleting oversized files in a later commit would still leave those files in the history sent on an initial push.

New Sites start owner-private. Making the game available to judges is a separate audience change; preserve the current audience until the owner authorizes that change. Never treat a private URL as anonymously accessible or as a completed hackathon submission.

## Fallback

The uncommitted `scripts/prepare-sites.mjs` is an earlier external-asset packaging option, not the active deployment path. If needed later, a GCP bucket could serve assets over HTTPS with CORS limited to the Sites origin. No external host has been provisioned or configured for this deployment.
