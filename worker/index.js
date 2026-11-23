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
  ]);
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

  const taken = await env.DB.prepare('SELECT 1 FROM runs WHERE nickname_key = ? AND finished_at IS NOT NULL').bind(key).first();
  if (taken) return err(409, 'That nickname is already on the leaderboard');

  const now = Date.now();
  const ipHash = await sha256((request.headers.get('cf-connecting-ip') || 'local') + '|' + (env.IP_SALT || 'emberclad'));
  const recent = await env.DB.prepare('SELECT COUNT(*) AS n FROM runs WHERE ip_hash = ? AND started_at > ?')
    .bind(ipHash, now - 3600 * 1000).first();
  if (recent && recent.n >= STARTS_PER_HOUR_PER_IP) return err(429, 'Too many runs started from here. Try again later.');

  const runId = randomHex(12), token = randomHex(24);
  await env.DB.prepare('INSERT INTO runs (id, token_hash, nickname, nickname_key, ip_hash, started_at) VALUES (?, ?, ?, ?, ?, ?)')
    .bind(runId, await sha256(token), nickname, key, ipHash, now).run();
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
  const headers = { 'Content-Type': type, 'Content-Encoding': 'br', 'Cache-Control': 'public, max-age=3600' };

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

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname.startsWith('/play/Build/')) return serveGameFile(request, env, url);
    if (!url.pathname.startsWith('/api/')) return env.ASSETS.fetch(request);
    if (request.method === 'OPTIONS') return new Response(null, { status: 204, headers: CORS });
    try {
      await ensureSchema(env.DB);
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
