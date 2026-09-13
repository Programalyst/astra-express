import { createHash } from "node:crypto";
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, unlinkSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { gzipSync, gunzipSync } from "node:zlib";

const repository = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const build = join(repository, "Builds", "Web");
const output = join(repository, "Builds", "SitesDeploy");
const destination = join(output, "dist");
const html = readFileSync(join(build, "index.html"), "utf8");
const chunkLimit = 2_000_000;
const asset = key => {
  const match = html.match(new RegExp(key + ': assetUrl\\("([^"\\\\]+)"\\)'));
  if (!match || !/^Build\/[A-Za-z0-9_.-]+$/.test(match[1])) throw new Error(`Cannot locate ${key} in the Web export.`);
  return match[1];
};
const loaderMatch = html.match(/loader.src = assetUrl\("(Build\/[A-Za-z0-9_.-]+)"\)/);
if (!loaderMatch) throw new Error("Rebuild Web with the current Astra template first.");
const metadata = key => JSON.parse(html.match(new RegExp(key + ': ("[^"\\\\]*")'))[1]);
const hash = bytes => createHash("sha256").update(bytes).digest("hex");
mkdirSync(join(destination, "Build"), { recursive: true });

function packagePayload(filename, mime) {
  const bytes = readFileSync(join(build, filename));
  const compressed = gzipSync(bytes, { level: 9 });
  if (!gunzipSync(compressed).equals(bytes)) throw new Error("Compressed payload verification failed.");
  const release = hash(compressed).slice(0, 16);
  const parts = [];
  for (let offset = 0; offset < compressed.length; offset += chunkLimit) {
    const part = `${filename}.${release}.${parts.length.toString().padStart(2, "0")}.bin`;
    writeFileSync(join(destination, part), compressed.subarray(offset, offset + chunkLimit));
    parts.push(part);
  }
  return { parts, bytes: bytes.length, compressedBytes: compressed.length, sha256: hash(bytes), mime };
}

const config = {
  data: packagePayload(asset("dataUrl"), "application/octet-stream"),
  wasm: packagePayload(asset("codeUrl"), "application/wasm"),
  framework: asset("frameworkUrl"),
  loader: loaderMatch[1],
  companyName: metadata("companyName"),
  productName: metadata("productName"),
  productVersion: metadata("productVersion")
};
config.downloadBytes = config.data.compressedBytes + config.wasm.compressedBytes;
copyFileSync(join(build, config.loader), join(destination, config.loader));
copyFileSync(join(build, config.framework), join(destination, config.framework));
const versionedAssets = new Map();
function versionAsset(filename, source) {
  const bytes = readFileSync(source);
  const versioned = filename.replace(/\.(js|css)$/, `.${hash(bytes).slice(0, 16)}.$1`);
  writeFileSync(join(destination, versioned), bytes);
  versionedAssets.set(filename, versioned);
  return versioned;
}
const boot = versionAsset("boot.js", join(repository, "scripts", "sites-loader.js"));
for (const match of html.matchAll(/(?:src|href)="([A-Za-z0-9_.-]+\.(?:js|css))"/g)) {
  versionAsset(match[1], join(build, match[1]));
}
if (existsSync(join(build, "icons"))) {
  mkdirSync(join(destination, "icons"), { recursive: true });
  for (const filename of readdirSync(join(build, "icons"))) {
    if (/^[A-Za-z0-9_-]+\.png$/.test(filename)) copyFileSync(join(build, "icons", filename), join(destination, "icons", filename));
  }
}
const scripts = `<script id="build-config" type="application/json">${JSON.stringify(config).replaceAll("<", "\\u003c")}</script>\n  <script src="${boot}"></script>`;
const replaced = html.replace(/<script>[\s\S]*?<\/script>/, scripts);
if (replaced === html) throw new Error("Cannot find the original Unity loader script.");
const page = replaced.replace(/(src|href)="([A-Za-z0-9_.-]+\.(?:js|css))"/g, (match, attribute, filename) => `${attribute}="${versionedAssets.get(filename) ?? filename}"`);
writeFileSync(join(destination, "index.html"), page);
const releasePage = `play-${hash(Buffer.from(page)).slice(0, 16)}.html`;
writeFileSync(join(destination, releasePage), page);
const retained = new Set([...config.data.parts, ...config.wasm.parts, config.framework, config.loader]);
for (const filename of readdirSync(join(destination, "Build"))) {
  if (!retained.has("Build/" + filename)) unlinkSync(join(destination, "Build", filename));
}
const hostingPath = join(output, ".openai", "hosting.json");
const hosting = existsSync(hostingPath) ? JSON.parse(readFileSync(hostingPath, "utf8")) : {};
hosting.static = { directory: "dist" };
mkdirSync(dirname(hostingPath), { recursive: true });
writeFileSync(hostingPath, JSON.stringify(hosting, null, 2) + "\n");
console.log(`Prepared ${config.data.parts.length + config.wasm.parts.length} payload files, each at most ${chunkLimit} bytes.`);
console.log(`Compressed payload download: ${config.downloadBytes} bytes; original: ${config.data.bytes + config.wasm.bytes} bytes.`);
console.log(`Fresh release entry: ${releasePage}`);
console.log("Preserved the existing Sites project identity. No upload or deployment performed.");
