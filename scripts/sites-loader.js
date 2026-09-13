const config = JSON.parse(document.getElementById("build-config").textContent);
const canvas = document.getElementById("unity-canvas");
const progressBar = document.getElementById("progress");
const statusLabel = document.getElementById("load-status");
const payloadUrls = [];
let downloadedBytes = 0;
const downloadStarted = performance.now();

function updateDownloadProgress() {
  const fraction = Math.min(downloadedBytes / config.downloadBytes, 1);
  const elapsedSeconds = (performance.now() - downloadStarted) / 1000;
  const speed = downloadedBytes / Math.max(elapsedSeconds, 1);
  progressBar.value = 0.75 * fraction;
  statusLabel.textContent = `Downloading colony assets… ${Math.floor(fraction * 100)}% (${(downloadedBytes / 1_000_000).toFixed(1)} / ${(config.downloadBytes / 1_000_000).toFixed(1)} MB)`;
  if (elapsedSeconds > 15 && speed < 200_000) statusLabel.textContent += " — slow connection; please keep this tab open.";
}

function showError(error) {
  const panel = document.getElementById("error");
  panel.style.display = "block";
  panel.textContent = "The colony could not start.\n" + error.message + "\nReload to retry.";
  payloadUrls.forEach(url => URL.revokeObjectURL(url));
}

async function downloadPart(filename) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const controller = new AbortController();
    let receivedBytes = 0;
    let timeout;
    const resetTimeout = () => {
      clearTimeout(timeout);
      timeout = setTimeout(() => controller.abort(), 60_000);
    };
    try {
      resetTimeout();
      const response = await fetch(filename, { credentials: "same-origin", signal: controller.signal });
      if (!response.ok) throw new Error(`Download failed (${response.status}): ${filename}`);
      const reader = response.body.getReader();
      const chunks = [];
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        chunks.push(value);
        receivedBytes += value.byteLength;
        downloadedBytes += value.byteLength;
        resetTimeout();
        updateDownloadProgress();
      }
      return new Blob(chunks).arrayBuffer();
    } catch (error) {
      downloadedBytes -= receivedBytes;
      updateDownloadProgress();
      if (attempt === 2) throw new Error(`Asset download failed after three attempts: ${filename}. ${error.message}`);
      statusLabel.textContent += " Retrying an interrupted download…";
      await new Promise(resolve => setTimeout(resolve, 500 * (attempt + 1)));
    } finally {
      clearTimeout(timeout);
    }
  }
}

async function loadPayload(payload) {
  const parts = [];
  for (let offset = 0; offset < payload.parts.length; offset += 2) {
    parts.push(...await Promise.all(payload.parts.slice(offset, offset + 2).map(downloadPart)));
  }
  const compressed = new Blob(parts);
  const stream = compressed.stream().pipeThrough(new DecompressionStream("gzip"));
  const bytes = await new Response(stream).arrayBuffer();
  if (bytes.byteLength !== payload.bytes) throw new Error("Incomplete game asset. Please reload.");
  const hash = await crypto.subtle.digest("SHA-256", bytes);
  const digest = Array.from(new Uint8Array(hash), byte => byte.toString(16).padStart(2, "0")).join("");
  if (digest !== payload.sha256) throw new Error("Game asset verification failed. Please reload.");
  const url = URL.createObjectURL(new Blob([bytes], { type: payload.mime }));
  payloadUrls.push(url);
  return url;
}

async function startGame() {
  try {
    if (typeof DecompressionStream === "undefined") throw new Error("This game needs a browser with gzip decompression support. Please use an up-to-date Chrome, Edge, Firefox, or Safari.");
    updateDownloadProgress();
    const [dataUrl, codeUrl] = await Promise.all([loadPayload(config.data), loadPayload(config.wasm)]);
    statusLabel.textContent = "Starting your colony…";
    const instance = await createUnityInstance(canvas, {
      dataUrl,
      codeUrl,
      frameworkUrl: config.framework,
      streamingAssetsUrl: "StreamingAssets",
      companyName: config.companyName,
      productName: config.productName,
      productVersion: config.productVersion,
      devicePixelRatio: Math.min(window.devicePixelRatio || 1, 1.5)
    }, progress => { progressBar.value = 0.75 + progress * 0.25; });
    window.unityInstance = instance;
    window.astraCoach?.ready(instance);
    window.astraBotControl?.ready(instance);
    document.getElementById("loading").remove();
    canvas.focus();
    payloadUrls.forEach(url => URL.revokeObjectURL(url));
    payloadUrls.length = 0;
  } catch (error) { showError(error); }
}

const loader = document.createElement("script");
statusLabel.textContent = "Connecting to the game loader…";
const loaderTimeout = setTimeout(() => {
  statusLabel.textContent = "Still connecting to the game loader… Your connection is taking longer than usual.";
}, 15_000);
loader.src = config.loader;
loader.onload = () => {
  clearTimeout(loaderTimeout);
  startGame();
};
loader.onerror = () => {
  clearTimeout(loaderTimeout);
  showError(new Error("The game loader could not download."));
};
document.body.appendChild(loader);
