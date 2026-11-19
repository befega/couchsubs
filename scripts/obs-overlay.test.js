// SPDX-License-Identifier: AGPL-3.0-only
// OBS katmanı (/obs) gerçek Chromium'da: "Ekranı çevir" sonuçları yayında yazının yerinde görünür. node --test scripts/obs-overlay.test.js
// Tarayıcı yolu: COUCHSUBS_TEST_BROWSER; uygulama, ağ ve OBS gerekmez (olay akışı sahte).
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { pathToFileURL } = require('node:url');
const overlay = path.join(__dirname, '..', 'core', 'Sources', 'CouchSubsCore', 'Resources', 'obs-overlay.html');
const browser = [process.env.COUCHSUBS_TEST_BROWSER, process.env.CHROME_BIN,
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser',
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'].find(p => p && fs.existsSync(p));

const item = (text, left, top, right, bottom, line = .03) => ({ text, box: { left, top, right, bottom }, line });
const menu = [item('Yeni oyun', .10, .30, .25, .34), item('Ayarlar', .10, .36, .25, .40),
  Object.assign(item('Görev: köprüyü geç.\nBelgeleri muhafıza teslim et.', .60, .10, .90, .20, .025), { paragraphs: true }),
  null, { text: 'Geçersiz', box: { left: .5, top: .5, right: .4, bottom: .6 } }, item('', .1, .5, .2, .55)];

/// Sayfaya sahte EventSource konur; olaylar sırayla verilir, her adımın ölçümü sayfaya yazılır.
function render(query, width = 1920, height = 1080) {
  const fake = `<script>
    window.__on = {};
    window.EventSource = function (url) { window.__url = url; this.addEventListener = function (n, f) { (__on[n] = __on[n] || []).push(f); }; };
    function fire(name, data) { (__on[name] || []).forEach(function (f) { f({ data: JSON.stringify(data) }); }); }
  </script>`;
  const probe = `<pre id="result"></pre><script>
    function measure() {
      var host = document.getElementById('screen');
      return { hidden: host.className, z: [getComputedStyle(document.getElementById('regions')).zIndex,
          getComputedStyle(host).zIndex, getComputedStyle(document.getElementById('wrap')).zIndex],
        items: Array.from(host.children).map(function (el) {
          var b = el.getBoundingClientRect(), s = getComputedStyle(el);
          return { text: el.textContent, left: b.left, top: b.top, right: b.right, bottom: b.bottom, font: parseFloat(s.fontSize),
            align: s.textAlign, background: s.backgroundColor, scroll: el.scrollHeight, client: el.clientHeight };
        }) };
    }
    var result = { viewport: { width: innerWidth, height: innerHeight }, url: window.__url };
    fire('screen', { duration: 15000, items: ${JSON.stringify(menu)} });
    result.shown = measure();
    fire('subtitle', { text: 'Arabaya bin, gidiyoruz.', duration: 4000 });
    result.subtitle = document.getElementById('line').className;
    result.afterSubtitle = measure().items.length;
    fire('screen', { duration: 15000, items: [${JSON.stringify(item('Kaydet', .4, .8, .5, .83))}] });
    result.replaced = measure();
    fire('screen-clear', {});
    result.cleared = measure();
    fire('style', { textColor: 'cyan', fontSize: 60 });
    result.color = getComputedStyle(document.getElementById('line')).color;
    document.getElementById('result').textContent = btoa(unescape(encodeURIComponent(JSON.stringify(result))));
  </script>`;
  const html = fs.readFileSync(overlay, 'utf8').replace('<head>', '<head>' + fake).replace('</body>', probe + '</body>');
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'couchsubs-obs-test-'));
  try {
    const file = path.join(temp, 'obs.html');
    fs.writeFileSync(file, html);
    const run = spawnSync(browser, ['--headless=new', '--disable-gpu', '--disable-background-networking',
      '--no-first-run', '--no-default-browser-check', '--hide-scrollbars', '--force-device-scale-factor=1',
      '--window-size=' + width + ',' + height, ...(process.platform === 'linux' ? ['--no-sandbox'] : []),
      '--user-data-dir=' + path.join(temp, 'profile'), '--dump-dom', pathToFileURL(file).href + query],
      { encoding: 'utf8', timeout: 30000, maxBuffer: 2 * 1024 * 1024, windowsHide: true });
    assert.equal(run.status, 0, String(run.error || run.stderr));
    const match = run.stdout.match(/<pre id="result">([A-Za-z0-9+/=]+)<\/pre>/);
    assert.ok(match, 'Tarayıcı ölçüm üretmedi: ' + run.stdout.slice(-1000));
    return JSON.parse(Buffer.from(match[1], 'base64').toString('utf8'));
  } finally {
    // Yalnız bu testin sistem geçici dizininde oluşturduğu klasör.
    assert.equal(path.dirname(temp), path.resolve(os.tmpdir()));
    fs.rmSync(temp, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
  }
}

if (process.env.CI) assert.ok(browser, 'CI: gerçek yerleşim testi için Chrome/Edge gerekli');

test('OBS katmanı: ekran çevirisi yazının yerinde görünür', { skip: !browser && 'Chrome/Edge bulunamadı' }, async t => {
  const r = render('');
  const { width, height } = r.viewport;
  assert.equal(r.url, '/events?viewer=obs');
  await t.test('geçerli her çeviri bir kutu; boş ve bozuk olanlar atlanır', () => {
    assert.equal(r.shown.hidden, '');
    assert.deepEqual(r.shown.items.map(i => i.text), [menu[0].text, menu[1].text, menu[2].text]);
  });
  await t.test('kutu İngilizce yazının yerini örter', () => {
    const first = r.shown.items[0];
    assert.ok(Math.abs(first.left - .096 * width) < 2, JSON.stringify(first));
    assert.ok(Math.abs(first.top - .296 * height) < 2, JSON.stringify(first));
    assert.ok(first.right >= .25 * width, JSON.stringify(first));
    assert.equal(first.font, Math.round(.03 * height * .95));
    assert.equal(first.background, 'rgba(0, 0, 0, 0.97)');
  });
  await t.test('üstteki kutu alttakinin üstüne taşmaz, paragraf sola yaslı ve tam', () => {
    assert.ok(r.shown.items[0].bottom <= r.shown.items[1].top + 1, JSON.stringify(r.shown.items));
    assert.equal(r.shown.items[2].align, 'left');
    assert.ok(r.shown.items[2].scroll <= r.shown.items[2].client + 1);
  });
  await t.test('ek alanların üstünde, altyazının altında', () => {
    assert.deepEqual(r.shown.z, ['1', '2', '3']);
  });
  await t.test('altyazı gelince ekran çevirisi kalkmaz', () => {
    assert.equal(r.subtitle, 'on');
    assert.equal(r.afterSubtitle, 3);
  });
  await t.test('yeni sonuç öncekinin yerine geçer, temizleyince kalkar', () => {
    assert.deepEqual(r.replaced.items.map(i => i.text), ['Kaydet']);
    assert.equal(r.cleared.hidden, 'hidden');
    assert.equal(r.cleared.items.length, 0);
  });
});

test('OBS katmanı: yayın görünümünün renkleri tanınır', { skip: !browser && 'Chrome/Edge bulunamadı' }, () => {
  assert.equal(render('').color, 'rgb(92, 225, 255)');
});

test('OBS katmanı: renkli zeminde (chroma key) ekran çevirisi tam koyu', { skip: !browser && 'Chrome/Edge bulunamadı' }, () => {
  const r = render('?bg=green');
  assert.ok(r.shown.items.length === 3);
  assert.ok(r.shown.items.every(i => i.background === 'rgb(0, 0, 0)'), JSON.stringify(r.shown.items));
});
