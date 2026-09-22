#!/usr/bin/env node
/*
 * Offline checks for the download buttons on the project site (docs/). No network and no dependencies: every page
 * in docs/ is parsed into a small stub DOM, the scripts it loads from docs/ run in a vm against it, and fetch answers
 * like GitHub's API from a fixed list of releases (fakeGitHub below).
 *
 * The rule every timbermods site follows: offer GitHub's Latest release (the newest one that is not a draft or a
 * pre-release), and only when there is none, the newest pre-release. With no answer from GitHub the page keeps what
 * it was written with, and its download buttons lead to the Latest release page.
 *
 *   node tests/test-site.mjs [repo root]      (default: this checkout)
 *
 * Prints one line per check and exits 1 if any check fails.
 */
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = path.resolve(process.argv[2] || path.join(path.dirname(fileURLToPath(import.meta.url)), '..'));
const docs = path.join(root, 'docs');
const REPO = 'timbermods/HungryPathing';
const RELEASES = `https://github.com/${REPO}/releases`;

// ---------- fake GitHub ----------

// A release as GitHub's API returns it. The zip is named after the version, as build.ps1 names it.
function release(version, { tag = `v${version}`, prerelease = false, draft = false } = {}) {
  const zip = `HungryPathing-${version}.zip`;
  return {
    tag_name: tag, name: tag, draft, prerelease, html_url: `${RELEASES}/tag/${tag}`,
    assets: [{ name: zip, size: 40960, digest: `sha256:${'ab'.repeat(32)}`, browser_download_url: `${RELEASES}/download/${tag}/${zip}` }],
  };
}

// A fetch that answers like api.github.com for this repository, `list` newest first. /releases?per_page=N returns
// the first N; /releases/latest returns the newest release that is not a draft or a pre-release, or 404 when there
// is none. `limited` answers 403 to everything, as GitHub does once an address has used its 60 lookups an hour.
function fakeGitHub({ list = [], limited = false }, log) {
  return async (url) => {
    log.push(String(url));
    const reply = (status, body) => ({ ok: status === 200, status, json: async () => JSON.parse(JSON.stringify(body)) });
    const m = /^https:\/\/api\.github\.com\/repos\/([\w.-]+\/[\w.-]+)\/releases(\/latest)?(?:\?per_page=(\d+))?$/.exec(String(url));
    if (!m || m[1] !== REPO) return reply(404, { message: 'Not Found' });
    if (limited) return reply(403, { message: 'API rate limit exceeded' });
    if (!m[2]) return reply(200, list.slice(0, Number(m[3] || 30)));
    const latest = list.find((r) => !r.draft && !r.prerelease);
    return latest ? reply(200, latest) : reply(404, { message: 'Not Found' });
  };
}

// ---------- stub DOM ----------

// Just enough of the DOM for small release scripts: a parsed tree, attribute and class selectors, text, hidden.
class Text {
  constructor(data) { this.nodeType = 3; this.data = data; this.parentNode = null; }
  get textContent() { return this.data; }
}

class Element {
  constructor(tag, attributes = {}) {
    this.nodeType = 1;
    this.tagName = tag.toUpperCase();
    this.attributes = { ...attributes };
    this.childNodes = [];
    this.parentNode = null;
  }
  get children() { return this.childNodes.filter((n) => n.nodeType === 1); }
  getAttribute(name) { return Object.hasOwn(this.attributes, name) ? this.attributes[name] : null; }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  hasAttribute(name) { return Object.hasOwn(this.attributes, name); }
  removeAttribute(name) { delete this.attributes[name]; }
  get id() { return this.getAttribute('id') || ''; }
  get className() { return this.getAttribute('class') || ''; }
  set className(value) { this.setAttribute('class', value); }
  get href() { return this.getAttribute('href') || ''; }
  set href(value) { this.setAttribute('href', value); }
  get hidden() { return this.hasAttribute('hidden'); }
  set hidden(value) { if (value) this.setAttribute('hidden', ''); else this.removeAttribute('hidden'); }
  get textContent() { return this.childNodes.map((n) => n.textContent).join(''); }
  set textContent(value) {
    for (const n of this.childNodes) n.parentNode = null;
    this.childNodes = [];
    if (String(value) !== '') this.appendChild(new Text(String(value)));
  }
  appendChild(node) {
    if (node.parentNode) node.parentNode.removeChild(node);
    node.parentNode = this;
    this.childNodes.push(node);
    return node;
  }
  removeChild(node) {
    const i = this.childNodes.indexOf(node);
    if (i < 0) throw new Error('stub DOM: removeChild of a node that is not a child');
    this.childNodes.splice(i, 1);
    node.parentNode = null;
    return node;
  }
  querySelectorAll(selector) {
    const tests = compile(selector);
    const found = [];
    const walk = (el) => {
      for (const child of el.children) {
        if (tests.some((t) => t(child))) found.push(child);
        walk(child);
      }
    };
    walk(this);
    return found;
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
}

// Compound selectors only: an optional tag name, then any number of .class and [attr] or [attr="value"], in a
// comma-separated list. Anything else throws, so a script that needs more shows up as a failure, not a quiet pass.
function compile(selector) {
  return String(selector).split(',').map((part) => {
    const s = part.trim();
    const m = /^([a-zA-Z][\w-]*)?((?:\.[\w-]+|\[[\w-]+(?:=(?:"[^"]*"|'[^']*'|[\w-]+))?\])*)$/.exec(s);
    if (!s || !m) throw new Error(`stub DOM: unsupported selector "${s}"`);
    const tag = m[1] ? m[1].toUpperCase() : null;
    const checks = [];
    for (const t of m[2].matchAll(/\.([\w-]+)|\[([\w-]+)(?:=(?:"([^"]*)"|'([^']*)'|([\w-]+)))?\]/g)) {
      if (t[1]) checks.push((el) => el.className.split(/\s+/).includes(t[1]));
      else if (t[3] === undefined && t[4] === undefined && t[5] === undefined) checks.push((el) => el.hasAttribute(t[2]));
      else { const want = t[3] ?? t[4] ?? t[5]; checks.push((el) => el.getAttribute(t[2]) === want); }
    }
    return (el) => (!tag || el.tagName === tag) && checks.every((c) => c(el));
  });
}

const VOID = new Set(['area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'source', 'track', 'wbr']);
const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ', middot: '·', rarr: '→', larr: '←', hellip: '…', mdash: '—', ndash: '–', times: '×' };
const decode = (s) => s.replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, (all, e) => {
  if (e[0] === '#') return String.fromCodePoint(e[1] === 'x' || e[1] === 'X' ? parseInt(e.slice(2), 16) : Number(e.slice(1)));
  return ENTITIES[e.toLowerCase()] ?? all;
});

function attributesOf(source) {
  const attributes = {};
  for (const a of source.matchAll(/([^\s=>\/]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'>]+)))?/g)) {
    attributes[a[1].toLowerCase()] = decode(a[2] ?? a[3] ?? a[4] ?? '');
  }
  return attributes;
}

// The site's pages are hand-written and well-formed, so a tokenizer with a stack is enough. Script and style bodies
// are kept as raw text.
function parse(html) {
  const doc = new Element('#document');
  const stack = [doc];
  const re = /<!--[\s\S]*?-->|<![^>]*>|<\/([a-zA-Z][\w-]*)\s*>|<([a-zA-Z][\w-]*)((?:\s+[^\s=>\/]+(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s"'>]+))?)*)\s*(\/?)>|[^<]+|</g;
  let m;
  while ((m = re.exec(html))) {
    const top = stack[stack.length - 1];
    if (m[1]) {
      const tag = m[1].toUpperCase();
      for (let i = stack.length - 1; i > 0; i--) if (stack[i].tagName === tag) { stack.length = i; break; }
    } else if (m[2]) {
      const el = top.appendChild(new Element(m[2], attributesOf(m[3])));
      const tag = m[2].toLowerCase();
      if (tag === 'script' || tag === 'style') {
        const end = html.toLowerCase().indexOf(`</${tag}`, re.lastIndex);
        const stop = end < 0 ? html.length : end;
        if (stop > re.lastIndex) el.appendChild(new Text(html.slice(re.lastIndex, stop)));
        re.lastIndex = stop;
      } else if (!VOID.has(tag) && !m[4]) stack.push(el);
    } else if (!m[0].startsWith('<!')) {
      top.appendChild(new Text(decode(m[0])));
    }
  }
  return doc;
}

class Document {
  constructor(html) {
    this.root = parse(html);
    this.documentElement = this.root.children.find((el) => el.tagName === 'HTML') || this.root.appendChild(new Element('html'));
    this.readyState = 'interactive'; // what a deferred script sees
    this.currentScript = null;
    this.listeners = [];
  }
  get body() { return this.documentElement.querySelector('body'); }
  querySelectorAll(selector) { return this.root.querySelectorAll(selector); }
  querySelector(selector) { return this.root.querySelector(selector); }
  getElementById(id) { return this.root.querySelectorAll('[id]').find((el) => el.id === id) || null; }
  createElement(tag) { return new Element(tag); }
  createTextNode(data) { return new Text(String(data)); }
  addEventListener(type, fn) { this.listeners.push({ type, fn }); }
  removeEventListener() {}
  dispatchEvent(event) { for (const l of this.listeners) if (l.type === event.type) l.fn(event); return true; }
}

function memoryStorage() {
  const items = new Map();
  return {
    getItem: (k) => (items.has(k) ? items.get(k) : null),
    setItem: (k, v) => { items.set(k, String(v)); },
    removeItem: (k) => { items.delete(k); },
  };
}

// What a visitor reads: text outside the head, scripts, styles and hidden elements.
function visibleText(node) {
  if (node.nodeType === 3) return node.data;
  if (node.hidden || ['HEAD', 'SCRIPT', 'STYLE', 'TEMPLATE'].includes(node.tagName)) return '';
  return node.childNodes.map(visibleText).join('');
}

// ---------- running a page ----------

const pages = readdirSync(docs).filter((f) => f.endsWith('.html')).sort();

// Loads one page and runs the scripts it loads from docs/ (inline scripts are page behavior, not releases), with
// fresh storage, then lets the fetches settle.
async function load(page, github) {
  const file = path.join(docs, page);
  const document = new Document(readFileSync(file, 'utf8'));
  const log = [];
  const sandbox = {
    document, fetch: fakeGitHub(github, log), localStorage: memoryStorage(), sessionStorage: memoryStorage(),
    CustomEvent: class CustomEvent { constructor(type, init = {}) { this.type = type; this.detail = init.detail; } },
    location: { hash: '', href: `https://timbermods.github.io/HungryPathing/${page}` },
    console, setTimeout, clearTimeout,
  };
  sandbox.window = sandbox;
  sandbox.self = sandbox;
  const context = vm.createContext(sandbox);
  for (const script of document.querySelectorAll('script')) {
    const src = script.getAttribute('src');
    if (!src || /^([a-z]+:)?\/\//i.test(src)) continue;
    document.currentScript = script;
    vm.runInContext(readFileSync(path.join(path.dirname(file), src), 'utf8'), context, { filename: src });
    document.currentScript = null;
  }
  for (let i = 0; i < 20; i++) await new Promise((resolve) => setTimeout(resolve, 0));
  return { document, log };
}

// The page's download buttons: links to the releases whose text offers a download.
function downloadButtons(document) {
  return document.querySelectorAll('a').filter((a) => a.href.startsWith(RELEASES) && /\bdownload\b/i.test(a.textContent));
}

// ---------- checks ----------

let passed = 0;
let failed = 0;
function check(ok, what, detail) {
  if (ok) { passed++; console.log(`ok   ${what}`); } else { failed++; console.log(`FAIL ${what}${detail ? ` (${detail})` : ''}`); }
}

const mentions = (text, word) => new RegExp(`(^|[^\\w.-])${word.replace(/[.+]/g, '\\$&')}(?![\\w.-]*\\w)`).test(text);

const pre = (version, options = {}) => release(version, { ...options, prerelease: true });
const scenarios = [
  {
    name: 'a pre-release newer than the Latest (the releases as of 0.2.2)',
    list: [pre('0.2.2'), release('0.2.1'), release('0.2.0'), pre('0.1.0', { tag: 'v0.1.0-alpha' })],
    offer: 'v0.2.1',
  },
  {
    name: 'more pre-releases after the Latest than one page of the list',
    list: [...Array.from({ length: 12 }, (_, i) => pre(`0.3.${12 - i}`)), release('0.2.1')],
    offer: 'v0.2.1',
  },
  { name: 'a newer draft', list: [release('0.3.0', { draft: true }), release('0.2.1')], offer: 'v0.2.1' },
  { name: 'only stable releases', list: [release('0.2.1'), release('0.2.0')], offer: 'v0.2.1' },
  { name: 'only pre-releases', list: [pre('0.2.0'), pre('0.1.0', { tag: 'v0.1.0-alpha' })], offer: 'v0.2.0' },
  { name: 'GitHub refuses the lookup', list: [release('0.2.1')], limited: true, offer: null },
  { name: 'no releases yet', list: [], offer: null },
];

const written = new Map(pages.map((page) => [page, new Document(readFileSync(path.join(docs, page), 'utf8'))]));
const buttonCount = [...written.values()].reduce((n, d) => n + downloadButtons(d).length, 0);
check(['index.html', 'install.html'].every((page) => written.has(page) && downloadButtons(written.get(page)).length > 0),
  `the overview and the install guide have download buttons to check (${buttonCount} on the site)`);
const fallbacks = [...written.values()].flatMap((d) => downloadButtons(d).map((a) => a.href));
check(fallbacks.every((href) => href === `${RELEASES}/latest`),
  'as written, before any script runs, every download button leads to the Latest release page',
  fallbacks.filter((href) => href !== `${RELEASES}/latest`).join(', '));

for (const scenario of scenarios) {
  const offered = scenario.list.find((r) => r.tag_name === scenario.offer) || null;
  const others = scenario.list.filter((r) => r !== offered);
  const hrefs = [];
  const unnamed = [];
  let text = '';
  let threw = null;
  for (const page of pages) {
    try {
      const { document } = await load(page, scenario);
      const buttons = downloadButtons(document);
      const pageText = visibleText(document.root);
      hrefs.push(...buttons.map((a) => a.href));
      text += `\n${pageText}`;
      if (offered && buttons.length && !mentions(pageText, offered.tag_name) && !mentions(pageText, offered.assets[0].name)) unnamed.push(page);
    } catch (e) {
      threw = `${page}: ${e && e.message}`;
    }
  }
  check(!threw, `${scenario.name}: every page's scripts run`, threw);
  const wrong = others.filter((r) => mentions(text, r.tag_name) || mentions(text, r.assets[0].name)).map((r) => r.tag_name);
  if (offered) {
    const zip = offered.assets[0].browser_download_url;
    const off = hrefs.filter((href) => href !== zip);
    check(hrefs.length > 0 && off.length === 0, `${scenario.name}: every download button fetches ${offered.assets[0].name}`,
      off.length ? `got ${[...new Set(off)].join(', ')}` : '');
    check(unnamed.length === 0 && wrong.length === 0,
      `${scenario.name}: every page with a download button names ${offered.tag_name} or its file, and no page names another release`,
      wrong.length ? `also names ${wrong.join(', ')}` : `nothing named on ${unnamed.join(', ')}`);
    const says = /\bpreview\b/i.test(text);
    check(says === offered.prerelease, `${scenario.name}: "preview" is shown ${offered.prerelease ? 'for a pre-release' : 'only for a pre-release'}`,
      says ? 'the page says preview' : 'the page does not say preview');
  } else {
    const moved = hrefs.filter((href) => !href.startsWith(RELEASES) || href.includes('/download/'));
    check(moved.length === 0, `${scenario.name}: the download buttons keep the links the page was written with`, moved.join(', '));
    check(wrong.length === 0 && !/\bpreview\b/i.test(text), `${scenario.name}: the site names no release it did not get`,
      wrong.length ? wrong.join(', ') : 'the page says preview');
  }
}

console.log(failed ? `FAILED: ${failed} of ${passed + failed} site checks` : `${passed}/${passed} site checks passed`);
process.exit(failed ? 1 : 0);
