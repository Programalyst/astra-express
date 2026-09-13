# OpenAI Sites deployment

## Live site

Production URL: https://astra-express.leonard-lin-2003.chatgpt.site

Current release: version 4, published at 14:11 Singapore time on 13 September 2026 from `b976723e520496fcc47f0ba483110fb3121bd5a1` (deployment `appgdep_6aa63e7bc34c8191b897bfac4724ead1`). It includes the eastern ore plateau, northern 2x2 Fluxite plateau, height-aware ramps and infrastructure, smooth rover-follow, and the 35° camera pitch. Unity compilation and the Web build succeeded. Compressed payload: 20,160,910 bytes. Sites reported deployment success; public access is unchanged. The camera and northern plateau source changes remain uncommitted in the main Unity repository; the deployment-only checkout is committed and pushed.

Version 3 was published at 13:08 Singapore time from `1430220a4cee7d9e853ab155f275bab7059a4392` (deployment `appgdep_6aa62fd9c24c8191bf27591a7ff5faed`). It introduced the flat Kenney Space Kit floor. Compressed payload: 20,152,353 bytes.

Version 1 deployed successfully on 13 September 2026 at 11:55 Singapore time. Deployment ID: `appgdep_6aa61e5129dc819194b19e3913608245`.

Access changed to public with the owner's authorization on 13 September 2026 at 12:00 Singapore time (access policy revision 2). Anyone with the URL can visit. The owner confirmed that the hosted game loaded after switching from the venue Wi-Fi (0.28 Mbps download) to a mobile hotspot.

Version 2 deployed successfully at 12:37 Singapore time from `711f151ae3f185768071678c467e0b7f86d5ac4b` (deployment `appgdep_6aa6285db1808191904929fc85f45a71`). It adds streamed byte progress, a slow-connection message, 60-second inactivity timeouts with retries, and limits asset downloads to four concurrent requests. Focused loader checks cover progress before completion, retry accounting, byte assembly, and terminal failures.

The reported splash-screen stall was investigated in Chrome: both scripts loaded, asset requests returned HTTP 200, and downloads advanced extremely slowly (one 1.76 MB piece took 157 seconds). The original progress indicator only advanced after a whole piece finished. No Unity startup error was observed at that point; the initial downloads had not completed. A separate 2 MB request completed successfully in 17.8 seconds. Version 2 improves feedback and recovery but cannot repair the underlying network throughput.

## Packaging

The game remains a Unity Web build, hosted entirely on OpenAI Sites. No iframe, external asset host, cloud bucket, backend API, or runtime secret is required.

The original export contains a 52,718,487-byte WebAssembly file and a 12,784,941-byte data file. A direct source upload failed with `artifacts_git_receive_pack_object_too_large`. The response did not identify the exact file-size limit; do not claim that it confirmed a 3 MB limit. An earlier HTTP/2 transport failure was a separate problem, resolved for subsequent upload attempts with per-command HTTP/1.1 and a 32 MiB Git request buffer.

`scripts/prepare-sites-direct.mjs` compresses the two large assets with gzip and splits the compressed bytes into files of at most 2,000,000 bytes. It preserves the Unity loader/framework files and builds a small static entry page. The current build uses 11 payload files totalling 20,141,077 compressed bytes rather than 65,503,428 uncompressed payload bytes.

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
