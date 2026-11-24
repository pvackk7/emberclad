// Emberclad website Worker: serves the static site (website/) and the speedrun leaderboard API.
//
// API (all JSON):
//   POST /api/runs/start   { "nickname": "Ash" }
//        -> 200 { "runId": "...", "token": "...", "startedAt": 1732345678901 }
//        -> 400 { "error": "..." } bad nickname   409 nickname already on the board   429 too many starts
//   POST /api/runs/finish  { "runId": "...", "token": "...", "deaths": 3, "clientTimeMs": 1234567 }
//        -> 200 { "runMs": 1234000, "deaths": 3, "rank": 7 }
//        -> 400 invalid   403 bad token   404 unknown run   409 already finished / expired
//   GET  /api/leaderboard?limit=50
//        -> 200 { "entries": [ { "rank": 1, "nickname": "Ash", "runMs": 1234000, "deaths": 3, "finishedAt": 1732345678901 } ] }
//
// Run time is measured by the server (start request -> finish request), so a client can't report a faster
// time than it actually took. clientTimeMs is only used as a sanity check.

import { nameRefusal } from './name-filter.js';

const NICK_RE = /^[A-Za-z0-9][A-Za-z0-9 _.\-]{1,15}$/;   // 2-16 chars
const MIN_RUN_MS = 60 * 1000;          // a real full run can't be faster than this
const MAX_RUN_MS = 12 * 60 * 60 * 1000; // runs older than this expire
const MAX_DEATHS = 100000;
const CLOCK_SLACK_MS = 15 * 1000;       // allowed gap between client and server timing (network, loading)
const STARTS_PER_HOUR_PER_IP = 40;

let schemaReady = false;
async function ensureSchema(db) {
  if (schemaReady) return;
  await db.batch([
    db.prepare(`CREATE TABLE IF NOT EXISTS runs (
      id TEXT PRIMARY KEY,
      token_hash TEXT NOT NULL,
      nickname TEXT NOT NULL,
      nickname_key TEXT NOT NULL,
      ip_hash TEXT NOT NULL,
      started_at INTEGER NOT NULL,
      finished_at INTEGER,
      run_ms INTEGER,
      deaths INTEGER
    )`),
    db.prepare(`CREATE INDEX IF NOT EXISTS runs_board ON runs (run_ms) WHERE finished_at IS NOT NULL`),
    db.prepare(`CREATE UNIQUE INDEX IF NOT EXISTS runs_nick_done ON runs (nickname_key) WHERE finished_at IS NOT NULL`),
    db.prepare(`CREATE INDEX IF NOT EXISTS runs_ip_start ON runs (ip_hash, started_at)`),
    db.prepare(`CREATE TABLE IF NOT EXISTS users (
      id TEXT PRIMARY KEY,
      email TEXT NOT NULL UNIQUE,
      pass_hash TEXT NOT NULL,
      salt TEXT NOT NULL,
      created_at INTEGER NOT NULL
    )`),
    db.prepare(`CREATE TABLE IF NOT EXISTS sessions (
      token_hash TEXT PRIMARY KEY,
      user_id TEXT NOT NULL,
      expires_at INTEGER NOT NULL
    )`),
    db.prepare(`CREATE TABLE IF NOT EXISTS auth_failures (ip_hash TEXT NOT NULL, at INTEGER NOT NULL)`),
    db.prepare(`CREATE INDEX IF NOT EXISTS auth_failures_ip ON auth_failures (ip_hash, at)`),
  ]);
  const cols = await db.prepare(`PRAGMA table_info(runs)`).all();
  if (!cols.results.some(c => c.name === 'user_id')) {
    await db.prepare(`ALTER TABLE runs ADD COLUMN user_id TEXT`).run();
  }
  await db.prepare(`CREATE INDEX IF NOT EXISTS runs_user ON runs (user_id, finished_at)`).run();
  schemaReady = true;
}

const CORS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type',
};
function json(body, status = 200, extra = {}) {
  return new Response(JSON.stringify(body), {
    status, headers: { 'Content-Type': 'application/json; charset=utf-8', ...CORS, ...extra },
  });
}
const err = (status, error) => json({ error }, status);

async function sha256(text) {
  const buf = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
  return [...new Uint8Array(buf)].map(b => b.toString(16).padStart(2, '0')).join('');
}
function randomHex(bytes) {
  const a = new Uint8Array(bytes); crypto.getRandomValues(a);
  return [...a].map(b => b.toString(16).padStart(2, '0')).join('');
}
async function readJson(request) {
  if (Number(request.headers.get('content-length') || 0) > 2048) return null;
  try { const t = await request.text(); return t.length > 2048 ? null : JSON.parse(t); } catch { return null; }
}
function cleanNickname(raw) {
  if (typeof raw !== 'string') return null;
  const nick = raw.normalize('NFKC').replace(/\s+/g, ' ').trim();
  return NICK_RE.test(nick) ? nick : null;
}
const nickKey = nick => nick.toLowerCase().replace(/[\s_.\-]/g, '');

async function startRun(request, env) {
  const body = await readJson(request);
  const nickname = cleanNickname(body && body.nickname);
  if (!nickname) return err(400, 'Nickname must be 2-16 letters, numbers, spaces, _ . or -');
  const key = nickKey(nickname);
  if (key.length < 2) return err(400, 'Nickname is too short');
  const refusal = nameRefusal(nickname);
  if (refusal) return err(400, refusal);

  const taken = await env.DB.prepare('SELECT 1 FROM runs WHERE nickname_key = ? AND finished_at IS NOT NULL').bind(key).first();
  if (taken) return err(409, 'That nickname is already on the leaderboard');

  const now = Date.now();
  const ipHash = await sha256((request.headers.get('cf-connecting-ip') || 'local') + '|' + (env.IP_SALT || 'emberclad'));
  const recent = await env.DB.prepare('SELECT COUNT(*) AS n FROM runs WHERE ip_hash = ? AND started_at > ?')
    .bind(ipHash, now - 3600 * 1000).first();
  if (recent && recent.n >= STARTS_PER_HOUR_PER_IP) return err(429, 'Too many runs started from here. Try again later.');

  const runId = randomHex(12), token = randomHex(24);
  const user = await currentUser(request, env);
  await env.DB.prepare('INSERT INTO runs (id, token_hash, nickname, nickname_key, ip_hash, started_at, user_id) VALUES (?, ?, ?, ?, ?, ?, ?)')
    .bind(runId, await sha256(token), nickname, key, ipHash, now, user ? user.id : null).run();
  return json({ runId, token, startedAt: now });
}

async function finishRun(request, env) {
  const body = await readJson(request);
  if (!body || typeof body.runId !== 'string' || typeof body.token !== 'string') return err(400, 'runId and token are required');
  const deaths = body.deaths;
  if (!Number.isInteger(deaths) || deaths < 0 || deaths > MAX_DEATHS) return err(400, 'deaths must be a whole number');
  const clientMs = body.clientTimeMs;
  if (clientMs !== undefined && (!Number.isFinite(clientMs) || clientMs < 0)) return err(400, 'clientTimeMs must be a positive number');

  const run = await env.DB.prepare('SELECT * FROM runs WHERE id = ?').bind(body.runId).first();
  if (!run) return err(404, 'Unknown run');
  if (run.token_hash !== await sha256(body.token)) return err(403, 'Bad token');
  if (run.finished_at) return err(409, 'This run was already submitted');

  const now = Date.now();
  const runMs = now - run.started_at;
  if (runMs > MAX_RUN_MS) return err(409, 'This run expired');
  if (runMs < MIN_RUN_MS) return err(400, 'Run too short to be a full run');
  // The game's own timer can't be longer than real time, and shouldn't be far shorter either.
  if (clientMs !== undefined && (clientMs > runMs + CLOCK_SLACK_MS || clientMs < runMs - Math.max(CLOCK_SLACK_MS, runMs * 0.1)))
    return err(400, 'Run time does not match the server clock');

  try {
    const res = await env.DB.prepare('UPDATE runs SET finished_at = ?, run_ms = ?, deaths = ? WHERE id = ? AND finished_at IS NULL')
      .bind(now, runMs, deaths, run.id).run();
    if (!res.meta || res.meta.changes !== 1) return err(409, 'This run was already submitted');
  } catch (e) {
    if (String(e).includes('UNIQUE')) return err(409, 'That nickname is already on the leaderboard');
    throw e;
  }
  const ahead = await env.DB.prepare('SELECT COUNT(*) AS n FROM runs WHERE finished_at IS NOT NULL AND (run_ms < ? OR (run_ms = ? AND finished_at < ?))')
    .bind(runMs, runMs, now).first();
  return json({ runMs, deaths, rank: (ahead ? ahead.n : 0) + 1 });
}

async function leaderboard(url, env) {
  const limit = Math.min(Math.max(parseInt(url.searchParams.get('limit') || '50', 10) || 50, 1), 200);
  const { results } = await env.DB.prepare(
    'SELECT nickname, run_ms, deaths, finished_at FROM runs WHERE finished_at IS NOT NULL ORDER BY run_ms ASC, finished_at ASC LIMIT ?'
  ).bind(limit).all();
  const entries = results.map((r, i) => ({ rank: i + 1, nickname: r.nickname, runMs: r.run_ms, deaths: r.deaths, finishedAt: r.finished_at }));
  return json({ entries }, 200, { 'Cache-Control': 'public, max-age=15' });
}

// ---- Accounts (email + password, session cookie) ----
const SESSION_COOKIE = 'ec_session';
const SESSION_DAYS = 30;
const PBKDF2_ITER = 100000;
const EMAIL_RE = /^[^\s@]{1,64}@[^\s@]{1,190}\.[^\s@]{2,24}$/;

async function hashPassword(password, saltHex) {
  const salt = new Uint8Array(saltHex.match(/../g).map(h => parseInt(h, 16)));
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(password), 'PBKDF2', false, ['deriveBits']);
  const bits = await crypto.subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-256', salt, iterations: PBKDF2_ITER }, key, 256);
  return [...new Uint8Array(bits)].map(b => b.toString(16).padStart(2, '0')).join('');
}
function safeEqual(a, b) {
  if (a.length !== b.length) return false;
  let d = 0; for (let i = 0; i < a.length; i++) d |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return d === 0;
}
function getCookie(request, name) {
  const m = (request.headers.get('cookie') || '').match(new RegExp('(?:^|;\\s*)' + name + '=([^;]+)'));
  return m ? m[1] : null;
}
function sessionCookie(request, value, maxAge) {
  const secure = new URL(request.url).protocol === 'https:' ? '; Secure' : '';
  return `${SESSION_COOKIE}=${value}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${maxAge}${secure}`;
}
async function currentUser(request, env) {
  const token = getCookie(request, SESSION_COOKIE);
  if (!token || !/^[0-9a-f]{64}$/.test(token)) return null;
  return await env.DB.prepare(
    'SELECT u.id, u.email FROM sessions s JOIN users u ON u.id = s.user_id WHERE s.token_hash = ? AND s.expires_at > ?'
  ).bind(await sha256(token), Date.now()).first();
}
async function ipHashOf(request, env) {
  return sha256((request.headers.get('cf-connecting-ip') || 'local') + '|' + (env.IP_SALT || 'emberclad'));
}
async function tooManyFailures(request, env) {
  const r = await env.DB.prepare('SELECT COUNT(*) AS n FROM auth_failures WHERE ip_hash = ? AND at > ?')
    .bind(await ipHashOf(request, env), Date.now() - 15 * 60 * 1000).first();
  return r && r.n >= 10;
}
async function noteFailure(request, env) {
  const now = Date.now();
  await env.DB.batch([
    env.DB.prepare('INSERT INTO auth_failures (ip_hash, at) VALUES (?, ?)').bind(await ipHashOf(request, env), now),
    env.DB.prepare('DELETE FROM auth_failures WHERE at < ?').bind(now - 24 * 3600 * 1000),
  ]);
}
async function startSession(request, env, user) {
  const token = randomHex(32), now = Date.now();
  await env.DB.batch([
    env.DB.prepare('INSERT INTO sessions (token_hash, user_id, expires_at) VALUES (?, ?, ?)').bind(await sha256(token), user.id, now + SESSION_DAYS * 86400000),
    env.DB.prepare('DELETE FROM sessions WHERE expires_at < ?').bind(now),
  ]);
  return json({ user: { email: user.email } }, 200, { 'Set-Cookie': sessionCookie(request, token, SESSION_DAYS * 86400) });
}
function readCreds(body) {
  if (!body) return null;
  const email = typeof body.email === 'string' ? body.email.trim().toLowerCase() : '';
  const password = typeof body.password === 'string' ? body.password : '';
  return { email, password };
}

async function signup(request, env) {
  if (await tooManyFailures(request, env)) return err(429, 'Too many attempts. Try again in a few minutes.');
  const c = readCreds(await readJson(request));
  if (!c || !EMAIL_RE.test(c.email) || c.email.length > 254) return err(400, 'Enter a valid email address');
  if (c.password.length < 8 || c.password.length > 200) return err(400, 'Password must be at least 8 characters');
  const exists = await env.DB.prepare('SELECT 1 FROM users WHERE email = ?').bind(c.email).first();
  if (exists) { await noteFailure(request, env); return err(409, 'An account with that email already exists'); }
  const user = { id: randomHex(12), email: c.email };
  const salt = randomHex(16);
  try {
    await env.DB.prepare('INSERT INTO users (id, email, pass_hash, salt, created_at) VALUES (?, ?, ?, ?, ?)')
      .bind(user.id, c.email, await hashPassword(c.password, salt), salt, Date.now()).run();
  } catch (e) {
    if (String(e).includes('UNIQUE')) return err(409, 'An account with that email already exists');
    throw e;
  }
  return startSession(request, env, user);
}

async function login(request, env) {
  if (await tooManyFailures(request, env)) return err(429, 'Too many attempts. Try again in a few minutes.');
  const c = readCreds(await readJson(request));
  if (!c || !c.email || !c.password) return err(400, 'Enter your email and password');
  const row = await env.DB.prepare('SELECT id, email, pass_hash, salt FROM users WHERE email = ?').bind(c.email).first();
  const hash = await hashPassword(c.password, row ? row.salt : '00'.repeat(16));
  if (!row || !safeEqual(hash, row.pass_hash)) { await noteFailure(request, env); return err(401, 'Wrong email or password'); }
  return startSession(request, env, row);
}

async function logout(request, env) {
  const token = getCookie(request, SESSION_COOKIE);
  if (token && /^[0-9a-f]{64}$/.test(token)) await env.DB.prepare('DELETE FROM sessions WHERE token_hash = ?').bind(await sha256(token)).run();
  return json({ ok: true }, 200, { 'Set-Cookie': sessionCookie(request, '', 0) });
}

async function me(request, env) {
  const user = await currentUser(request, env);
  return json({ user: user ? { email: user.email } : null }, 200, { 'Cache-Control': 'no-store' });
}

async function myRuns(request, env) {
  const user = await currentUser(request, env);
  if (!user) return err(401, 'Log in to see your runs');
  const { results } = await env.DB.prepare(
    `SELECT r.nickname, r.run_ms, r.deaths, r.finished_at,
            (SELECT COUNT(*) FROM runs o WHERE o.finished_at IS NOT NULL AND (o.run_ms < r.run_ms OR (o.run_ms = r.run_ms AND o.finished_at < r.finished_at))) + 1 AS rank
       FROM runs r WHERE r.user_id = ? AND r.finished_at IS NOT NULL ORDER BY r.run_ms ASC LIMIT 200`
  ).bind(user.id).all();
  return json({ user: { email: user.email }, runs: results.map(r => ({ nickname: r.nickname, runMs: r.run_ms, deaths: r.deaths, finishedAt: r.finished_at, rank: r.rank })) },
    200, { 'Cache-Control': 'no-store' });
}

// ---- Browser game files (website/play/Build) ----
// Unity's Brotli files need Content-Encoding: br. Files over Cloudflare's 25 MiB asset limit are stored
// as name.part0, name.part1, ... and streamed back together here.
const GAME_TYPES = { '.data': 'application/octet-stream', '.wasm': 'application/wasm', '.js': 'application/javascript' };

async function assetOrNull(env, request, pathname) {
  const u = new URL(request.url); u.pathname = pathname; u.search = '';
  const res = await env.ASSETS.fetch(new Request(u.toString(), { method: 'GET' }));
  return res.ok ? res : null;
}

async function serveGameFile(request, env, url) {
  const path = url.pathname;
  if (!path.endsWith('.br')) return env.ASSETS.fetch(request);
  const inner = path.slice(0, -3);
  const type = GAME_TYPES[inner.slice(inner.lastIndexOf('.'))] || 'application/octet-stream';
  const headers = { 'Content-Type': type, 'Content-Encoding': 'br', 'Cache-Control': url.searchParams.has('v') ? 'public, max-age=31536000, immutable' : 'no-cache' };

  const whole = await assetOrNull(env, request, path);
  if (whole) {
    const len = whole.headers.get('content-length'); if (len) headers['Content-Length'] = len;
    return new Response(request.method === 'HEAD' ? null : whole.body, { headers, encodeBody: 'manual' });
  }
  const parts = [];
  for (let i = 0; i < 20; i++) {
    const part = await assetOrNull(env, request, path + '.part' + i);
    if (!part) break;
    parts.push(part);
  }
  if (!parts.length) return new Response('Not found', { status: 404 });
  const lens = parts.map(p => Number(p.headers.get('content-length')));
  if (lens.every(n => n > 0)) headers['Content-Length'] = String(lens.reduce((a, b) => a + b, 0));
  if (request.method === 'HEAD') { parts.forEach(p => p.body && p.body.cancel()); return new Response(null, { headers }); }
  const { readable, writable } = new TransformStream();
  (async () => {
    for (const p of parts) await p.body.pipeTo(writable, { preventClose: true });
    await writable.close();
  })().catch(e => console.error('game stream', e));
  return new Response(readable, { headers, encodeBody: 'manual' });
}

async function routePrivate(url, request, env) {
  if (url.pathname === '/api/auth/signup' && request.method === 'POST') return signup(request, env);
  if (url.pathname === '/api/auth/login' && request.method === 'POST') return login(request, env);
  if (url.pathname === '/api/auth/logout' && request.method === 'POST') return logout(request, env);
  if (url.pathname === '/api/auth/me' && request.method === 'GET') return me(request, env);
  if (url.pathname === '/api/me/runs' && request.method === 'GET') return myRuns(request, env);
  return err(404, 'Not found');
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname.startsWith('/play/Build/')) return serveGameFile(request, env, url);
    if (!url.pathname.startsWith('/api/')) return env.ASSETS.fetch(request);
    const privateApi = url.pathname.startsWith('/api/auth/') || url.pathname.startsWith('/api/me/');
    if (request.method === 'OPTIONS') return privateApi ? new Response(null, { status: 204 }) : new Response(null, { status: 204, headers: CORS });
    if (privateApi && request.method === 'POST' && !(request.headers.get('content-type') || '').startsWith('application/json'))
      return err(415, 'JSON required');
    try {
      await ensureSchema(env.DB);
      if (privateApi) {
        const res = await routePrivate(url, request, env);
        const h = new Headers(res.headers); Object.keys(CORS).forEach(k => h.delete(k));
        return new Response(res.body, { status: res.status, headers: h });
      }
      if (url.pathname === '/api/runs/start' && request.method === 'POST') return await startRun(request, env);
      if (url.pathname === '/api/runs/finish' && request.method === 'POST') return await finishRun(request, env);
      if (url.pathname === '/api/leaderboard' && request.method === 'GET') return await leaderboard(url, env);
      return err(404, 'Not found');
    } catch (e) {
      console.error(e);
      return err(500, 'Server error');
    }
  },
};
