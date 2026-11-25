// Emberclad website Worker: serves the static site (website/) and the speedrun leaderboard API.
//
// API (all JSON). A nickname is a knight: entering one starts a run that belongs to that name until the
// Bone Warden falls. The game keeps {runId, token} and comes back to the same knight after deaths, exits
// and closed tabs; progress (level, XP, felled, deaths, play time) is saved on the server as it plays.
//   POST /api/runs/start    { "nickname": "Ash" }
//        -> 200 { "runId", "token", "startedAt" }   400 bad nickname   409 name taken   429 too many starts
//   POST /api/runs/resume   { "runId", "token" }
//        -> 200 { "nickname", "xp", "level", "kills", "deaths", "playMs", "finished" }   403 bad token   404 unknown
//   POST /api/runs/progress { "runId", "token", "xp", "level", "kills", "deaths", "playMs" }
//        -> 200 { "ok": true }   (sent every few seconds while playing, and on deaths, level-ups and exits)
//   POST /api/runs/finish   { "runId", "token", "deaths", "kills", "xp", "level", "clientTimeMs" }
//        -> 200 { "runMs", "deaths", "rank" }   409 already finished
//   GET  /api/leaderboard?limit=50
//        -> 200 { "riding": 2, "knights": 31, "entries": [ { "rank", "nickname", "level", "kills", "deaths",
//                 "runMs" (null until finished), "finished", "riding" } ] }
//
// Run time is play time, not wall time: the server credits the gap between progress pings, capped at
// PING_CREDIT_MS each, so time spent away (closed tab, menus, overnight) does not count against a knight.
// The game's own play clock is accepted only up to what the server has seen pass.


import { nameRefusal } from './name-filter.js';
// The home page is bundled with the worker (served below) rather than uploaded as a static asset,
// so a page-only change can be deployed with a plain worker upload. website/.assetsignore keeps it
// out of the asset upload.
import indexHtml from '../website/index.html';

const NICK_RE = /^[A-Za-z0-9][A-Za-z0-9 _.\-]{1,15}$/;   // 2-16 chars
const MIN_RUN_MS = 60 * 1000;          // a real full run can't be faster than this
const MAX_DEATHS = 100000;
const CLOCK_SLACK_MS = 15 * 1000;       // allowed gap between client and server timing (network, loading)
const PING_CREDIT_MS = 20 * 1000;      // the game pings every 10 s while playing; a longer gap only counts this much
const RIDING_MS = 90 * 1000;           // seen this recently = on the trail right now
const IDLE_RELEASE_MS = 30 * 86400000;  // an unfinished knight untouched this long gives its name back
const MAX_KILLS = 1000000, MAX_XP = 100000000, MAX_LEVEL = 1000;
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
  // One row per nickname. Replaces 'runs' (kept, read once to carry finished runs across).
  await db.batch([
    db.prepare(`CREATE TABLE IF NOT EXISTS knights (
      id TEXT PRIMARY KEY,
      token_hash TEXT NOT NULL,
      nickname TEXT NOT NULL,
      nickname_key TEXT NOT NULL UNIQUE,
      ip_hash TEXT NOT NULL,
      user_id TEXT,
      started_at INTEGER NOT NULL,
      last_seen INTEGER NOT NULL,
      play_ms INTEGER NOT NULL DEFAULT 0,
      xp INTEGER NOT NULL DEFAULT 0,
      level INTEGER NOT NULL DEFAULT 1,
      kills INTEGER,
      deaths INTEGER NOT NULL DEFAULT 0,
      finished_at INTEGER,
      run_ms INTEGER
    )`),
    db.prepare(`CREATE INDEX IF NOT EXISTS knights_seen ON knights (last_seen)`),
    db.prepare(`CREATE INDEX IF NOT EXISTS knights_ip ON knights (ip_hash, started_at)`),
    db.prepare(`CREATE INDEX IF NOT EXISTS knights_user ON knights (user_id)`),
  ]);
  const any = await db.prepare(`SELECT 1 FROM knights LIMIT 1`).first();
  if (!any) {
    // Finished runs from before knights existed: no kill count or level was recorded, so kills stays NULL.
    await db.prepare(`INSERT OR IGNORE INTO knights (id, token_hash, nickname, nickname_key, ip_hash, user_id, started_at, last_seen, play_ms, deaths, finished_at, run_ms)
      SELECT id, token_hash, nickname, nickname_key, ip_hash, user_id, started_at, finished_at, run_ms, deaths, finished_at, run_ms FROM runs WHERE finished_at IS NOT NULL`).run();
  }
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

function wholeNumber(v, max) { return Number.isInteger(v) && v >= 0 && v <= max; }

async function knightFor(env, body) {
  if (!body || typeof body.runId !== 'string' || typeof body.token !== 'string' || body.runId.length > 64) return { error: err(400, 'runId and token are required') };
  const k = await env.DB.prepare('SELECT * FROM knights WHERE id = ?').bind(body.runId).first();
  if (!k) return { error: err(404, 'Unknown run') };
  if (k.token_hash !== await sha256(body.token)) return { error: err(403, 'Bad token') };
  return { k };
}

// Play time the server will vouch for since the last ping, and the clock it allows the game to claim.
function creditedMs(k, now) { return k.play_ms + Math.min(Math.max(0, now - k.last_seen), PING_CREDIT_MS); }

async function startRun(request, env) {
  const body = await readJson(request);
  const nickname = cleanNickname(body && body.nickname);
  if (!nickname) return err(400, 'Nickname must be 2-16 letters, numbers, spaces, _ . or -');
  const key = nickKey(nickname);
  if (key.length < 2) return err(400, 'Nickname is too short');
  const refusal = nameRefusal(nickname);
  if (refusal) return err(400, refusal);
  const now = Date.now();

  // A name held by a knight nobody has ridden for a month is given back.
  await env.DB.prepare('DELETE FROM knights WHERE nickname_key = ? AND finished_at IS NULL AND last_seen < ?').bind(key, now - IDLE_RELEASE_MS).run();
  const taken = await env.DB.prepare('SELECT 1 FROM knights WHERE nickname_key = ?').bind(key).first();
  if (taken) return err(409, 'That name already belongs to another knight. Pick another.');

  const ipHash = await ipHashOf(request, env);
  const recent = await env.DB.prepare('SELECT COUNT(*) AS n FROM knights WHERE ip_hash = ? AND started_at > ?')
    .bind(ipHash, now - 3600 * 1000).first();
  if (recent && recent.n >= STARTS_PER_HOUR_PER_IP) return err(429, 'Too many runs started from here. Try again later.');

  const runId = randomHex(12), token = randomHex(24);
  const user = await currentUser(request, env);
  try {
    await env.DB.prepare('INSERT INTO knights (id, token_hash, nickname, nickname_key, ip_hash, user_id, started_at, last_seen, kills) VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0)')
      .bind(runId, await sha256(token), nickname, key, ipHash, user ? user.id : null, now, now).run();
  } catch (e) {
    if (String(e).includes('UNIQUE')) return err(409, 'That name already belongs to another knight. Pick another.');
    throw e;
  }
  return json({ runId, token, startedAt: now });
}

async function resumeRun(request, env) {
  const { k, error } = await knightFor(env, await readJson(request));
  if (error) return error;
  const now = Date.now();
  // Coming back after time away: that gap is not play time, so just mark the knight as seen.
  if (!k.finished_at) await env.DB.prepare('UPDATE knights SET last_seen = ? WHERE id = ?').bind(now, k.id).run();
  return json({ nickname: k.nickname, xp: k.xp, level: k.level, kills: k.kills || 0, deaths: k.deaths, playMs: k.play_ms, finished: !!k.finished_at },
    200, { 'Cache-Control': 'no-store' });
}

// Shared by progress and finish: validate the counters and bring the row up to date. Counters only ever go up.
function readStats(body) {
  const st = { xp: body.xp, level: body.level, kills: body.kills, deaths: body.deaths };
  if (!wholeNumber(st.xp, MAX_XP) || !wholeNumber(st.level, MAX_LEVEL) || !wholeNumber(st.kills, MAX_KILLS) || !wholeNumber(st.deaths, MAX_DEATHS)) return null;
  return st;
}

async function progressRun(request, env) {
  const body = await readJson(request);
  const { k, error } = await knightFor(env, body);
  if (error) return error;
  if (k.finished_at) return err(409, 'This run is already finished');
  const st = readStats(body);
  if (!st) return err(400, 'xp, level, kills and deaths must be whole numbers');
  const now = Date.now();
  await env.DB.prepare(`UPDATE knights SET last_seen = ?, play_ms = ?, xp = MAX(xp, ?), level = MAX(level, ?), kills = MAX(COALESCE(kills, 0), ?), deaths = MAX(deaths, ?)
    WHERE id = ? AND finished_at IS NULL`).bind(now, creditedMs(k, now), st.xp, st.level, st.kills, st.deaths, k.id).run();
  return json({ ok: true }, 200, { 'Cache-Control': 'no-store' });
}

async function finishRun(request, env) {
  const body = await readJson(request);
  const { k, error } = await knightFor(env, body);
  if (error) return error;
  if (k.finished_at) return err(409, 'This run was already submitted');
  const st = readStats({ xp: body.xp ?? k.xp, level: body.level ?? k.level, kills: body.kills ?? (k.kills || 0), deaths: body.deaths });
  if (!st) return err(400, 'deaths must be a whole number');
  const clientMs = body.clientTimeMs;
  if (clientMs !== undefined && (!Number.isFinite(clientMs) || clientMs < 0)) return err(400, 'clientTimeMs must be a positive number');

  const now = Date.now();
  
  // Play time: what the server has seen pass in pings. The game's clock may add the last few seconds,
  // but never more than real time allows.
  const seen = creditedMs(k, now);
  let runMs = seen;
  if (clientMs !== undefined) runMs = Math.max(seen, Math.min(Math.round(clientMs), seen + CLOCK_SLACK_MS, now - k.started_at));
  if (runMs < MIN_RUN_MS) return err(400, 'Run too short to be a full run');

  const res = await env.DB.prepare(`UPDATE knights SET finished_at = ?, run_ms = ?, last_seen = ?, play_ms = ?, xp = MAX(xp, ?), level = MAX(level, ?),
      kills = MAX(COALESCE(kills, 0), ?), deaths = MAX(deaths, ?) WHERE id = ? AND finished_at IS NULL`)
    .bind(now, runMs, now, runMs, st.xp, st.level, st.kills, st.deaths, k.id).run();
  if (!res.meta || res.meta.changes !== 1) return err(409, 'This run was already submitted');
  const deaths = Math.max(k.deaths, st.deaths);
  const ahead = await env.DB.prepare('SELECT COUNT(*) AS n FROM knights WHERE finished_at IS NOT NULL AND (run_ms < ? OR (run_ms = ? AND finished_at < ?))')
    .bind(runMs, runMs, now).first();
  return json({ runMs, deaths, rank: (ahead ? ahead.n : 0) + 1 });
}

// Finished knights first, fastest first. Then everyone still on the trail, furthest along (level, XP) first.
async function leaderboard(url, env) {
  const limit = Math.min(Math.max(parseInt(url.searchParams.get('limit') || '50', 10) || 50, 1), 200);
  const now = Date.now();
  const { results } = await env.DB.prepare(
    `SELECT nickname, level, xp, kills, deaths, run_ms, finished_at, last_seen FROM knights
      ORDER BY (finished_at IS NULL), run_ms ASC, level DESC, xp DESC, kills DESC, started_at ASC LIMIT ?`
  ).bind(limit).all();
  const counts = await env.DB.prepare('SELECT COUNT(*) AS knights, SUM(CASE WHEN finished_at IS NULL AND last_seen > ? THEN 1 ELSE 0 END) AS riding FROM knights')
    .bind(now - RIDING_MS).first();
  const entries = results.map((r, i) => ({
    rank: i + 1, nickname: r.nickname, level: r.kills == null ? null : r.level, kills: r.kills,   // runs from before knights: no level or kills recorded
    deaths: r.deaths,
    runMs: r.finished_at ? r.run_ms : null, finished: !!r.finished_at, riding: !r.finished_at && r.last_seen > now - RIDING_MS,
  }));
  return json({ riding: (counts && counts.riding) || 0, knights: (counts && counts.knights) || 0, entries }, 200, { 'Cache-Control': 'public, max-age=10' });
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
            (SELECT COUNT(*) FROM knights o WHERE o.finished_at IS NOT NULL AND (o.run_ms < r.run_ms OR (o.run_ms = r.run_ms AND o.finished_at < r.finished_at))) + 1 AS rank
       FROM knights r WHERE r.user_id = ? AND r.finished_at IS NOT NULL ORDER BY r.run_ms ASC LIMIT 200`
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
    if (url.pathname === '/' || url.pathname === '/index.html') {
      return new Response(request.method === 'HEAD' ? null : indexHtml, { headers: { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'public, max-age=0, must-revalidate' } });
    }
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
      if (url.pathname === '/api/runs/resume' && request.method === 'POST') return await resumeRun(request, env);
      if (url.pathname === '/api/runs/progress' && request.method === 'POST') return await progressRun(request, env);
      if (url.pathname === '/api/runs/finish' && request.method === 'POST') return await finishRun(request, env);
      if (url.pathname === '/api/leaderboard' && request.method === 'GET') return await leaderboard(url, env);
      return err(404, 'Not found');
    } catch (e) {
      console.error(e);
      return err(500, 'Server error');
    }
  },
};
