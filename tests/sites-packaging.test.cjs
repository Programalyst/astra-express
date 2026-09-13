const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const {execFileSync} = require('node:child_process');
const {createHash} = require('node:crypto');
const {gunzipSync} = require('node:zlib');

test('Sites packaging fingerprints scripts/styles and emits a matching release entry', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'astra-sites-test-'));
  try {
    const scripts = path.join(root, 'scripts'), build = path.join(root, 'Builds/Web');
    const output = path.join(root, 'Builds/SitesDeploy/dist');
    fs.mkdirSync(scripts, {recursive:true}); fs.mkdirSync(path.join(build, 'Build'), {recursive:true});
    fs.copyFileSync(path.join(__dirname, '../scripts/prepare-sites-direct.mjs'), path.join(scripts, 'prepare-sites-direct.mjs'));
    fs.writeFileSync(path.join(scripts, 'sites-loader.js'), 'window.bootReady = true;');
    fs.writeFileSync(path.join(build, 'client.js'), 'window.direct = true;');
    fs.writeFileSync(path.join(build, 'style.css'), 'body { color: white; }');
    for (const filename of ['game.data','game.wasm','game.framework.js','game.loader.js']) fs.writeFileSync(path.join(build, 'Build', filename), filename.repeat(50));
    fs.writeFileSync(path.join(build, 'index.html'), `<link href="style.css"><script src="client.js"></script><script>
loader.src = assetUrl("Build/game.loader.js");
const config = { dataUrl: assetUrl("Build/game.data"), codeUrl: assetUrl("Build/game.wasm"), frameworkUrl: assetUrl("Build/game.framework.js"), companyName: "Astra", productName: "Express", productVersion: "1" };
</script>`);
    const run = () => execFileSync(process.execPath, [path.join(scripts, 'prepare-sites-direct.mjs')], {encoding:'utf8'});
    const first = run(), html = fs.readFileSync(path.join(output, 'index.html'), 'utf8');
    const entry = first.match(/Fresh release entry: (\S+)/)[1];
    assert.equal(fs.readFileSync(path.join(output, entry), 'utf8'), html);
    for (const match of html.matchAll(/(?:src|href)="([^"]+)"/g)) {
      assert.match(match[1], /\.[a-f0-9]{16}\.(js|css)$/);
      const bytes = fs.readFileSync(path.join(output, match[1]));
      assert.ok(match[1].includes(createHash('sha256').update(bytes).digest('hex').slice(0,16)));
    }
    const config = JSON.parse(html.match(/type="application\/json">([^<]+)/)[1]);
    for (const name of ['data','wasm']) {
      const parts = config[name].parts.map(filename => fs.readFileSync(path.join(output, filename)));
      assert.ok(parts.every(bytes => bytes.length <= 2000000));
      const bytes = gunzipSync(Buffer.concat(parts));
      assert.equal(createHash('sha256').update(bytes).digest('hex'), config[name].sha256);
    }
    assert.equal(run(), first);
    fs.writeFileSync(path.join(build, 'client.js'), 'window.direct = "updated";');
    const next = run(), changed = fs.readFileSync(path.join(output, 'index.html'), 'utf8');
    assert.notEqual(next.match(/Fresh release entry: (\S+)/)[1], entry);
    assert.notEqual(changed.match(/src="(client[^"]+)/)[1], html.match(/src="(client[^"]+)/)[1]);
    assert.equal(changed.match(/href="([^"]+)/)[1], html.match(/href="([^"]+)/)[1]);
  } finally { fs.rmSync(root, {recursive:true, force:true}); }
});
