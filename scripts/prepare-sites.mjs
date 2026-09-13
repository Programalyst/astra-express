import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repository = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const build = join(repository, "Builds", "Web");
const output = join(repository, "Builds", "SitesDeploy");
const assetBase = new URL(process.argv[2] || "https://invalid.example/");
const local = assetBase.protocol === "http:" && ["127.0.0.1", "localhost"].includes(assetBase.hostname);
if (!process.argv[2] || (assetBase.protocol !== "https:" && !local) || assetBase.username || assetBase.password || assetBase.search || assetBase.hash || !assetBase.pathname.endsWith("/")) {
  throw new Error("Usage: node scripts/prepare-sites.mjs https://storage.googleapis.com/BUCKET/RELEASE/ [https://SITES-ORIGIN]. Asset URL must end in / and contain no credentials, query, or fragment. HTTP is allowed only for localhost testing.");
}

const indexPath = join(build, "index.html");
const source = readFileSync(indexPath, "utf8");
const baseDeclaration = 'const assetBaseUrl = new URL("./", window.location.href);';
if (source.split(baseDeclaration).length !== 2) throw new Error("Rebuild Web with the current Astra template before preparing Sites.");
const index = source.replace(baseDeclaration, `const assetBaseUrl = new URL(${JSON.stringify(assetBase.href)});`);
const referenced = [...index.matchAll(/assetUrl\("(Build\/[^"\\]+)"\)/g)].map(match => match[1]);
if (new Set(referenced).size !== 4 || referenced.some(filename => !existsSync(join(build, filename)))) throw new Error("The Web build must contain its referenced loader, framework, data, and WebAssembly files.");

const staticRoot = join(output, "dist");
mkdirSync(staticRoot, { recursive: true });
for (const filename of readdirSync(staticRoot)) {
  if (filename !== "index.html" && filename !== "screenshot.jpeg") throw new Error(`Unexpected existing Sites output: ${filename}. Review it before preparing a new package.`);
}
if (Buffer.byteLength(index) >= 3_000_000) throw new Error("Sites entry page exceeds our conservative 3 MB packaging budget.");
writeFileSync(join(staticRoot, "index.html"), index);
const hostingPath = join(output, ".openai", "hosting.json");
const hosting = existsSync(hostingPath) ? JSON.parse(readFileSync(hostingPath, "utf8")) : {};
hosting.static = { directory: "dist" };
mkdirSync(dirname(hostingPath), { recursive: true });
writeFileSync(hostingPath, JSON.stringify(hosting, null, 2) + "\n");

const files = referenced.map(filename => ({
  path: filename,
  bytes: statSync(join(build, filename)).size,
  sha256: createHash("sha256").update(readFileSync(join(build, filename))).digest("hex"),
  url: new URL(filename, assetBase).href
}));
writeFileSync(join(output, "release-manifest.json"), JSON.stringify({ assetBase: assetBase.href, localOnly: local, files }, null, 2) + "\n");
if (process.argv[3]) {
  const site = new URL(process.argv[3]);
  if ((site.protocol !== "https:" && !(site.protocol === "http:" && ["127.0.0.1", "localhost"].includes(site.hostname))) || site.username || site.password || site.pathname !== "/" || site.search || site.hash) throw new Error("Supply the Sites origin only, without a path, query, credentials, or fragment.");
  writeFileSync(join(output, "gcp-cors.json"), JSON.stringify([{ origin: [site.origin], method: ["GET", "HEAD"], responseHeader: ["Content-Type", "Content-Length", "Content-Encoding"], maxAgeSeconds: 3600 }], null, 2) + "\n");
}
console.log(`Sites page: ${join(staticRoot, "index.html")} (${Buffer.byteLength(index)} bytes)`);
console.log(`External build: ${files.reduce((total, file) => total + file.bytes, 0)} bytes at ${assetBase.href}`);
console.log(local ? "LOCAL TEST ONLY: regenerate with the final HTTPS asset URL before publishing." : "Prepared locally. No cloud resources, permissions, uploads, or Sites deployments were changed.");
