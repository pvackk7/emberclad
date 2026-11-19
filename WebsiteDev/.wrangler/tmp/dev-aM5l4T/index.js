var __defProp = Object.defineProperty;
var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// worker/name-filter.js
var LEET = {
  "0": "o",
  "1": "i",
  "!": "i",
  "|": "i",
  "\xA1": "i",
  "3": "e",
  "\u20AC": "e",
  "4": "a",
  "@": "a",
  "5": "s",
  "$": "s",
  "6": "g",
  "9": "g",
  "7": "t",
  "+": "t",
  "8": "b",
  "2": "z",
  "\xA3": "l"
};
var BANNED = [
  // racial and ethnic slurs
  "nigger",
  "nigga",
  "niger",
  "niglet",
  "wigger",
  "negro",
  "coon",
  "jigaboo",
  "groid",
  "chink",
  "gook",
  "jap",
  "slanteye",
  "zipperhead",
  "spic",
  "beaner",
  "wetback",
  "kike",
  "heeb",
  "paki",
  "pajeet",
  "currymuncher",
  "towelhead",
  "raghead",
  "mudslime",
  "redskin",
  "injun",
  "tarbaby",
  // homophobic and transphobic slurs
  "faggot",
  "fag",
  "dyke",
  "tranny",
  "shemale",
  "homo",
  // ableist slurs
  "retard",
  "mongoloid",
  // hate movements
  "hitler",
  "nazi",
  "heil",
  "kkk",
  "klansman",
  "whitepower",
  "swastika",
  // sexual content
  "fuck",
  "shit",
  "cunt",
  "bitch",
  "bastard",
  "whore",
  "slut",
  "skank",
  "douche",
  "cock",
  "dick",
  "penis",
  "vagina",
  "pussy",
  "anal",
  "anus",
  "arse",
  "asshole",
  "ass",
  "cum",
  "jizz",
  "semen",
  "boobs",
  "tits",
  "titties",
  "titty",
  "nipple",
  "blowjob",
  "handjob",
  "rimjob",
  "deepthroat",
  "bukkake",
  "cumshot",
  "creampie",
  "gangbang",
  "orgasm",
  "masturbate",
  "wank",
  "jerkoff",
  "dildo",
  "buttplug",
  "fisting",
  "porn",
  "hentai",
  "milf",
  "dilf",
  "bdsm",
  "sex",
  "incest",
  // abuse and exploitation
  "rape",
  "rapist",
  "molest",
  "pedo",
  "pedophile",
  "lolicon",
  "jailbait",
  "bestiality",
  "zoophilia",
  "necrophilia",
  // crude
  "piss",
  "turd",
  "twat",
  "prick",
  "bollocks",
  "wanker",
  // self-harm
  "kys",
  "killyourself",
  // reserved for the site
  "admin",
  "administrator",
  "moderator",
  "emberclad"
];
var ALLOW = [
  // ass
  "assassin",
  "assault",
  "assembly",
  "assemble",
  "assert",
  "assess",
  "asset",
  "assign",
  "assist",
  "associate",
  "assume",
  "assure",
  "ambassador",
  "bass",
  "brass",
  "cassandra",
  "class",
  "classic",
  "compass",
  "embassy",
  "glass",
  "grass",
  "harass",
  "hassle",
  "lass",
  "mass",
  "massive",
  "molasses",
  "morass",
  "pass",
  "passage",
  "passive",
  "potassium",
  "surpass",
  "trespass",
  // anal, anus
  "analysis",
  "analyst",
  "analog",
  "analogue",
  "banal",
  "canal",
  "arsenal",
  "janus",
  "uranus",
  // cum
  "accumulate",
  "circumstance",
  "cucumber",
  "cumberland",
  "cumbria",
  "cumin",
  "cumulative",
  "cumulus",
  "document",
  "incumbent",
  "succumb",
  "vacuum",
  // tits
  "titan",
  "titanic",
  "titanium",
  // rape
  "grape",
  "grapes",
  "grapefruit",
  "drape",
  "drapes",
  "drapery",
  // cock
  "cocktail",
  "cockpit",
  "cockney",
  "cockroach",
  "peacock",
  "shuttlecock",
  "woodcock",
  "hitchcock",
  // dick
  "dickens",
  "dickinson",
  "dickson",
  // shit
  "shiitake",
  "shitake",
  // spic
  "spice",
  "spices",
  "spicy",
  "auspicious",
  "conspicuous",
  "despicable",
  "suspicious",
  // arse
  "coarse",
  "hoarse",
  "parse",
  "sparse",
  "arsenic",
  "marseille",
  // penis, cunt
  "penistone",
  "scunthorpe",
  "clitheroe",
  // negro
  "montenegro",
  "rionegro",
  // niger (the slur folds onto this too, so only the longer forms are rescued)
  "nigeria",
  "nigerian",
  "nigerien",
  // homo
  "homogeneous",
  "homogenize",
  "homonym",
  "homophone",
  "homer",
  // jap, paki
  "japan",
  "japanese",
  "pakistan",
  "pakistani",
  // sex
  "sussex",
  "essex",
  "middlesex",
  "sexton",
  "sextant",
  "sextet",
  // turd
  "saturday",
  // prick
  "prickly",
  "prickle",
  // retard
  "retardant",
  // fag, dyke, coon
  "fagin",
  "vandyke",
  "cocoon"
];
function fold(raw) {
  const stripped = (raw || "").normalize("NFKD").replace(/[̀-ͯ]/g, "").toLowerCase();
  return Array.from(stripped).map((c) => c in LEET ? LEET[c] : c).join("");
}
__name(fold, "fold");
function loose(term, flags) {
  const body = Array.from(term).map((c) => c + "+").join("[^a-z]*");
  return new RegExp(body, flags);
}
__name(loose, "loose");
var BANNED_RE = BANNED.map((term) => ({ term, re: loose(term, "") }));
var ALLOW_RE = ALLOW.slice().sort((a, b) => b.length - a.length).map((term) => loose(term, "g"));
function bannedTerm(nickname) {
  let probe = fold(nickname);
  for (const re of ALLOW_RE) probe = probe.replace(re, " ");
  for (const { term, re } of BANNED_RE) if (re.test(probe)) return term;
  return null;
}
__name(bannedTerm, "bannedTerm");
function nameRefusal(nickname) {
  return bannedTerm(nickname) ? "That name is not allowed. Choose another." : null;
}
__name(nameRefusal, "nameRefusal");

// worker/index.js
var NICK_RE = /^[A-Za-z0-9][A-Za-z0-9 _.\-]{1,15}$/;
var MIN_RUN_MS = 60 * 1e3;
var MAX_RUN_MS = 12 * 60 * 60 * 1e3;
var MAX_DEATHS = 1e5;
var CLOCK_SLACK_MS = 15 * 1e3;
var STARTS_PER_HOUR_PER_IP = 40;
var schemaReady = false;
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
    db.prepare(`CREATE INDEX IF NOT EXISTS auth_failures_ip ON auth_failures (ip_hash, at)`)
  ]);
  const cols = await db.prepare(`PRAGMA table_info(runs)`).all();
  if (!cols.results.some((c) => c.name === "user_id")) {
    await db.prepare(`ALTER TABLE runs ADD COLUMN user_id TEXT`).run();
  }
  await db.prepare(`CREATE INDEX IF NOT EXISTS runs_user ON runs (user_id, finished_at)`).run();
  schemaReady = true;
}
__name(ensureSchema, "ensureSchema");
var CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type"
};
function json(body, status = 200, extra = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json; charset=utf-8", ...CORS, ...extra }
  });
}
__name(json, "json");
var err = /* @__PURE__ */ __name((status, error) => json({ error }, status), "err");
async function sha256(text) {
  const buf = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text));
  return [...new Uint8Array(buf)].map((b) => b.toString(16).padStart(2, "0")).join("");
}
__name(sha256, "sha256");
function randomHex(bytes) {
  const a = new Uint8Array(bytes);
  crypto.getRandomValues(a);
  return [...a].map((b) => b.toString(16).padStart(2, "0")).join("");
}
__name(randomHex, "randomHex");
async function readJson(request) {
  if (Number(request.headers.get("content-length") || 0) > 2048) return null;
  try {
    const t = await request.text();
    return t.length > 2048 ? null : JSON.parse(t);
  } catch {
    return null;
  }
}
__name(readJson, "readJson");
function cleanNickname(raw) {
  if (typeof raw !== "string") return null;
  const nick = raw.normalize("NFKC").replace(/\s+/g, " ").trim();
  return NICK_RE.test(nick) ? nick : null;
}
__name(cleanNickname, "cleanNickname");
var nickKey = /* @__PURE__ */ __name((nick) => nick.toLowerCase().replace(/[\s_.\-]/g, ""), "nickKey");
async function startRun(request, env) {
  const body = await readJson(request);
  const nickname = cleanNickname(body && body.nickname);
  if (!nickname) return err(400, "Nickname must be 2-16 letters, numbers, spaces, _ . or -");
  const key = nickKey(nickname);
  if (key.length < 2) return err(400, "Nickname is too short");
  const refusal = nameRefusal(nickname);
  if (refusal) return err(400, refusal);
  const taken = await env.DB.prepare("SELECT 1 FROM runs WHERE nickname_key = ? AND finished_at IS NOT NULL").bind(key).first();
  if (taken) return err(409, "That nickname is already on the leaderboard");
  const now = Date.now();
  const ipHash = await sha256((request.headers.get("cf-connecting-ip") || "local") + "|" + (env.IP_SALT || "emberclad"));
  const recent = await env.DB.prepare("SELECT COUNT(*) AS n FROM runs WHERE ip_hash = ? AND started_at > ?").bind(ipHash, now - 3600 * 1e3).first();
  if (recent && recent.n >= STARTS_PER_HOUR_PER_IP) return err(429, "Too many runs started from here. Try again later.");
  const runId = randomHex(12), token = randomHex(24);
  const user = await currentUser(request, env);
  await env.DB.prepare("INSERT INTO runs (id, token_hash, nickname, nickname_key, ip_hash, started_at, user_id) VALUES (?, ?, ?, ?, ?, ?, ?)").bind(runId, await sha256(token), nickname, key, ipHash, now, user ? user.id : null).run();
  return json({ runId, token, startedAt: now });
}
__name(startRun, "startRun");
async function finishRun(request, env) {
  const body = await readJson(request);
  if (!body || typeof body.runId !== "string" || typeof body.token !== "string") return err(400, "runId and token are required");
  const deaths = body.deaths;
  if (!Number.isInteger(deaths) || deaths < 0 || deaths > MAX_DEATHS) return err(400, "deaths must be a whole number");
  const clientMs = body.clientTimeMs;
  if (clientMs !== void 0 && (!Number.isFinite(clientMs) || clientMs < 0)) return err(400, "clientTimeMs must be a positive number");
  const run = await env.DB.prepare("SELECT * FROM runs WHERE id = ?").bind(body.runId).first();
  if (!run) return err(404, "Unknown run");
  if (run.token_hash !== await sha256(body.token)) return err(403, "Bad token");
  if (run.finished_at) return err(409, "This run was already submitted");
  const now = Date.now();
  const runMs = now - run.started_at;
  if (runMs > MAX_RUN_MS) return err(409, "This run expired");
  if (runMs < MIN_RUN_MS) return err(400, "Run too short to be a full run");
  if (clientMs !== void 0 && (clientMs > runMs + CLOCK_SLACK_MS || clientMs < runMs - Math.max(CLOCK_SLACK_MS, runMs * 0.1)))
    return err(400, "Run time does not match the server clock");
  try {
    const res = await env.DB.prepare("UPDATE runs SET finished_at = ?, run_ms = ?, deaths = ? WHERE id = ? AND finished_at IS NULL").bind(now, runMs, deaths, run.id).run();
    if (!res.meta || res.meta.changes !== 1) return err(409, "This run was already submitted");
  } catch (e) {
    if (String(e).includes("UNIQUE")) return err(409, "That nickname is already on the leaderboard");
    throw e;
  }
  const ahead = await env.DB.prepare("SELECT COUNT(*) AS n FROM runs WHERE finished_at IS NOT NULL AND (run_ms < ? OR (run_ms = ? AND finished_at < ?))").bind(runMs, runMs, now).first();
  return json({ runMs, deaths, rank: (ahead ? ahead.n : 0) + 1 });
}
__name(finishRun, "finishRun");
async function leaderboard(url, env) {
  const limit = Math.min(Math.max(parseInt(url.searchParams.get("limit") || "50", 10) || 50, 1), 200);
  const { results } = await env.DB.prepare(
    "SELECT nickname, run_ms, deaths, finished_at FROM runs WHERE finished_at IS NOT NULL ORDER BY run_ms ASC, finished_at ASC LIMIT ?"
  ).bind(limit).all();
  const entries = results.map((r, i) => ({ rank: i + 1, nickname: r.nickname, runMs: r.run_ms, deaths: r.deaths, finishedAt: r.finished_at }));
  return json({ entries }, 200, { "Cache-Control": "public, max-age=15" });
}
__name(leaderboard, "leaderboard");
var SESSION_COOKIE = "ec_session";
var SESSION_DAYS = 30;
var PBKDF2_ITER = 1e5;
var EMAIL_RE = /^[^\s@]{1,64}@[^\s@]{1,190}\.[^\s@]{2,24}$/;
async function hashPassword(password, saltHex) {
  const salt = new Uint8Array(saltHex.match(/../g).map((h) => parseInt(h, 16)));
  const key = await crypto.subtle.importKey("raw", new TextEncoder().encode(password), "PBKDF2", false, ["deriveBits"]);
  const bits = await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt, iterations: PBKDF2_ITER }, key, 256);
  return [...new Uint8Array(bits)].map((b) => b.toString(16).padStart(2, "0")).join("");
}
__name(hashPassword, "hashPassword");
function safeEqual(a, b) {
  if (a.length !== b.length) return false;
  let d = 0;
  for (let i = 0; i < a.length; i++) d |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return d === 0;
}
__name(safeEqual, "safeEqual");
function getCookie(request, name) {
  const m = (request.headers.get("cookie") || "").match(new RegExp("(?:^|;\\s*)" + name + "=([^;]+)"));
  return m ? m[1] : null;
}
__name(getCookie, "getCookie");
function sessionCookie(request, value, maxAge) {
  const secure = new URL(request.url).protocol === "https:" ? "; Secure" : "";
  return `${SESSION_COOKIE}=${value}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${maxAge}${secure}`;
}
__name(sessionCookie, "sessionCookie");
async function currentUser(request, env) {
  const token = getCookie(request, SESSION_COOKIE);
  if (!token || !/^[0-9a-f]{64}$/.test(token)) return null;
  return await env.DB.prepare(
    "SELECT u.id, u.email FROM sessions s JOIN users u ON u.id = s.user_id WHERE s.token_hash = ? AND s.expires_at > ?"
  ).bind(await sha256(token), Date.now()).first();
}
__name(currentUser, "currentUser");
async function ipHashOf(request, env) {
  return sha256((request.headers.get("cf-connecting-ip") || "local") + "|" + (env.IP_SALT || "emberclad"));
}
__name(ipHashOf, "ipHashOf");
async function tooManyFailures(request, env) {
  const r = await env.DB.prepare("SELECT COUNT(*) AS n FROM auth_failures WHERE ip_hash = ? AND at > ?").bind(await ipHashOf(request, env), Date.now() - 15 * 60 * 1e3).first();
  return r && r.n >= 10;
}
__name(tooManyFailures, "tooManyFailures");
async function noteFailure(request, env) {
  const now = Date.now();
  await env.DB.batch([
    env.DB.prepare("INSERT INTO auth_failures (ip_hash, at) VALUES (?, ?)").bind(await ipHashOf(request, env), now),
    env.DB.prepare("DELETE FROM auth_failures WHERE at < ?").bind(now - 24 * 3600 * 1e3)
  ]);
}
__name(noteFailure, "noteFailure");
async function startSession(request, env, user) {
  const token = randomHex(32), now = Date.now();
  await env.DB.batch([
    env.DB.prepare("INSERT INTO sessions (token_hash, user_id, expires_at) VALUES (?, ?, ?)").bind(await sha256(token), user.id, now + SESSION_DAYS * 864e5),
    env.DB.prepare("DELETE FROM sessions WHERE expires_at < ?").bind(now)
  ]);
  return json({ user: { email: user.email } }, 200, { "Set-Cookie": sessionCookie(request, token, SESSION_DAYS * 86400) });
}
__name(startSession, "startSession");
function readCreds(body) {
  if (!body) return null;
  const email = typeof body.email === "string" ? body.email.trim().toLowerCase() : "";
  const password = typeof body.password === "string" ? body.password : "";
  return { email, password };
}
__name(readCreds, "readCreds");
async function signup(request, env) {
  if (await tooManyFailures(request, env)) return err(429, "Too many attempts. Try again in a few minutes.");
  const c = readCreds(await readJson(request));
  if (!c || !EMAIL_RE.test(c.email) || c.email.length > 254) return err(400, "Enter a valid email address");
  if (c.password.length < 8 || c.password.length > 200) return err(400, "Password must be at least 8 characters");
  const exists = await env.DB.prepare("SELECT 1 FROM users WHERE email = ?").bind(c.email).first();
  if (exists) {
    await noteFailure(request, env);
    return err(409, "An account with that email already exists");
  }
  const user = { id: randomHex(12), email: c.email };
  const salt = randomHex(16);
  try {
    await env.DB.prepare("INSERT INTO users (id, email, pass_hash, salt, created_at) VALUES (?, ?, ?, ?, ?)").bind(user.id, c.email, await hashPassword(c.password, salt), salt, Date.now()).run();
  } catch (e) {
    if (String(e).includes("UNIQUE")) return err(409, "An account with that email already exists");
    throw e;
  }
  return startSession(request, env, user);
}
__name(signup, "signup");
async function login(request, env) {
  if (await tooManyFailures(request, env)) return err(429, "Too many attempts. Try again in a few minutes.");
  const c = readCreds(await readJson(request));
  if (!c || !c.email || !c.password) return err(400, "Enter your email and password");
  const row = await env.DB.prepare("SELECT id, email, pass_hash, salt FROM users WHERE email = ?").bind(c.email).first();
  const hash = await hashPassword(c.password, row ? row.salt : "00".repeat(16));
  if (!row || !safeEqual(hash, row.pass_hash)) {
    await noteFailure(request, env);
    return err(401, "Wrong email or password");
  }
  return startSession(request, env, row);
}
__name(login, "login");
async function logout(request, env) {
  const token = getCookie(request, SESSION_COOKIE);
  if (token && /^[0-9a-f]{64}$/.test(token)) await env.DB.prepare("DELETE FROM sessions WHERE token_hash = ?").bind(await sha256(token)).run();
  return json({ ok: true }, 200, { "Set-Cookie": sessionCookie(request, "", 0) });
}
__name(logout, "logout");
async function me(request, env) {
  const user = await currentUser(request, env);
  return json({ user: user ? { email: user.email } : null }, 200, { "Cache-Control": "no-store" });
}
__name(me, "me");
async function myRuns(request, env) {
  const user = await currentUser(request, env);
  if (!user) return err(401, "Log in to see your runs");
  const { results } = await env.DB.prepare(
    `SELECT r.nickname, r.run_ms, r.deaths, r.finished_at,
            (SELECT COUNT(*) FROM runs o WHERE o.finished_at IS NOT NULL AND (o.run_ms < r.run_ms OR (o.run_ms = r.run_ms AND o.finished_at < r.finished_at))) + 1 AS rank
       FROM runs r WHERE r.user_id = ? AND r.finished_at IS NOT NULL ORDER BY r.run_ms ASC LIMIT 200`
  ).bind(user.id).all();
  return json(
    { user: { email: user.email }, runs: results.map((r) => ({ nickname: r.nickname, runMs: r.run_ms, deaths: r.deaths, finishedAt: r.finished_at, rank: r.rank })) },
    200,
    { "Cache-Control": "no-store" }
  );
}
__name(myRuns, "myRuns");
var GAME_TYPES = { ".data": "application/octet-stream", ".wasm": "application/wasm", ".js": "application/javascript" };
async function assetOrNull(env, request, pathname) {
  const u = new URL(request.url);
  u.pathname = pathname;
  u.search = "";
  const res = await env.ASSETS.fetch(new Request(u.toString(), { method: "GET" }));
  return res.ok ? res : null;
}
__name(assetOrNull, "assetOrNull");
async function serveGameFile(request, env, url) {
  const path = url.pathname;
  if (!path.endsWith(".br")) return env.ASSETS.fetch(request);
  const inner = path.slice(0, -3);
  const type = GAME_TYPES[inner.slice(inner.lastIndexOf("."))] || "application/octet-stream";
  const headers = { "Content-Type": type, "Content-Encoding": "br", "Cache-Control": url.searchParams.has("v") ? "public, max-age=31536000, immutable" : "no-cache" };
  const whole = await assetOrNull(env, request, path);
  if (whole) {
    const len = whole.headers.get("content-length");
    if (len) headers["Content-Length"] = len;
    return new Response(request.method === "HEAD" ? null : whole.body, { headers, encodeBody: "manual" });
  }
  const parts = [];
  for (let i = 0; i < 20; i++) {
    const part = await assetOrNull(env, request, path + ".part" + i);
    if (!part) break;
    parts.push(part);
  }
  if (!parts.length) return new Response("Not found", { status: 404 });
  const lens = parts.map((p) => Number(p.headers.get("content-length")));
  if (lens.every((n) => n > 0)) headers["Content-Length"] = String(lens.reduce((a, b) => a + b, 0));
  if (request.method === "HEAD") {
    parts.forEach((p) => p.body && p.body.cancel());
    return new Response(null, { headers });
  }
  const { readable, writable } = new TransformStream();
  (async () => {
    for (const p of parts) await p.body.pipeTo(writable, { preventClose: true });
    await writable.close();
  })().catch((e) => console.error("game stream", e));
  return new Response(readable, { headers, encodeBody: "manual" });
}
__name(serveGameFile, "serveGameFile");
async function routePrivate(url, request, env) {
  if (url.pathname === "/api/auth/signup" && request.method === "POST") return signup(request, env);
  if (url.pathname === "/api/auth/login" && request.method === "POST") return login(request, env);
  if (url.pathname === "/api/auth/logout" && request.method === "POST") return logout(request, env);
  if (url.pathname === "/api/auth/me" && request.method === "GET") return me(request, env);
  if (url.pathname === "/api/me/runs" && request.method === "GET") return myRuns(request, env);
  return err(404, "Not found");
}
__name(routePrivate, "routePrivate");
var worker_default = {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname.startsWith("/play/Build/")) return serveGameFile(request, env, url);
    if (!url.pathname.startsWith("/api/")) return env.ASSETS.fetch(request);
    const privateApi = url.pathname.startsWith("/api/auth/") || url.pathname.startsWith("/api/me/");
    if (request.method === "OPTIONS") return privateApi ? new Response(null, { status: 204 }) : new Response(null, { status: 204, headers: CORS });
    if (privateApi && request.method === "POST" && !(request.headers.get("content-type") || "").startsWith("application/json"))
      return err(415, "JSON required");
    try {
      await ensureSchema(env.DB);
      if (privateApi) {
        const res = await routePrivate(url, request, env);
        const h = new Headers(res.headers);
        Object.keys(CORS).forEach((k) => h.delete(k));
        return new Response(res.body, { status: res.status, headers: h });
      }
      if (url.pathname === "/api/runs/start" && request.method === "POST") return await startRun(request, env);
      if (url.pathname === "/api/runs/finish" && request.method === "POST") return await finishRun(request, env);
      if (url.pathname === "/api/leaderboard" && request.method === "GET") return await leaderboard(url, env);
      return err(404, "Not found");
    } catch (e) {
      console.error(e);
      return err(500, "Server error");
    }
  }
};

// ../../.npm/_npx/32026684e21afda6/node_modules/wrangler/templates/middleware/middleware-ensure-req-body-drained.ts
var drainBody = /* @__PURE__ */ __name(async (request, env, _ctx, middlewareCtx) => {
  try {
    return await middlewareCtx.next(request, env);
  } finally {
    try {
      if (request.body !== null && !request.bodyUsed) {
        const reader = request.body.getReader();
        while (!(await reader.read()).done) {
        }
      }
    } catch (e) {
      console.error("Failed to drain the unused request body.", e);
    }
  }
}, "drainBody");
var middleware_ensure_req_body_drained_default = drainBody;

// ../../.npm/_npx/32026684e21afda6/node_modules/wrangler/templates/middleware/middleware-miniflare3-json-error.ts
function reduceError(e) {
  return {
    name: e?.name,
    message: e?.message ?? String(e),
    stack: e?.stack,
    cause: e?.cause === void 0 ? void 0 : reduceError(e.cause)
  };
}
__name(reduceError, "reduceError");
var jsonError = /* @__PURE__ */ __name(async (request, env, _ctx, middlewareCtx) => {
  try {
    return await middlewareCtx.next(request, env);
  } catch (e) {
    const error = reduceError(e);
    const body = JSON.stringify(error);
    const headers = {
      "Content-Type": "application/json",
      "MF-Experimental-Error-Stack": "true"
    };
    const encoded = encodeURIComponent(body);
    if (encoded.length <= 8192) {
      headers["MF-Experimental-Error-Stack-Payload"] = encoded;
    }
    return new Response(body, { status: 500, headers });
  }
}, "jsonError");
var middleware_miniflare3_json_error_default = jsonError;

// .wrangler/tmp/bundle-9h1zDV/middleware-insertion-facade.js
var __INTERNAL_WRANGLER_MIDDLEWARE__ = [
  middleware_ensure_req_body_drained_default,
  middleware_miniflare3_json_error_default
];
var middleware_insertion_facade_default = worker_default;

// ../../.npm/_npx/32026684e21afda6/node_modules/wrangler/templates/middleware/common.ts
var __facade_middleware__ = [];
function __facade_register__(...args) {
  __facade_middleware__.push(...args.flat());
}
__name(__facade_register__, "__facade_register__");
function __facade_invokeChain__(request, env, ctx, dispatch, middlewareChain) {
  const [head, ...tail] = middlewareChain;
  const middlewareCtx = {
    dispatch,
    next(newRequest, newEnv) {
      return __facade_invokeChain__(newRequest, newEnv, ctx, dispatch, tail);
    }
  };
  return head(request, env, ctx, middlewareCtx);
}
__name(__facade_invokeChain__, "__facade_invokeChain__");
function __facade_invoke__(request, env, ctx, dispatch, finalMiddleware) {
  return __facade_invokeChain__(request, env, ctx, dispatch, [
    ...__facade_middleware__,
    finalMiddleware
  ]);
}
__name(__facade_invoke__, "__facade_invoke__");

// .wrangler/tmp/bundle-9h1zDV/middleware-loader.entry.ts
var __Facade_ScheduledController__ = class ___Facade_ScheduledController__ {
  constructor(scheduledTime, cron, noRetry) {
    this.scheduledTime = scheduledTime;
    this.cron = cron;
    this.#noRetry = noRetry;
  }
  scheduledTime;
  cron;
  static {
    __name(this, "__Facade_ScheduledController__");
  }
  #noRetry;
  noRetry() {
    if (!(this instanceof ___Facade_ScheduledController__)) {
      throw new TypeError("Illegal invocation");
    }
    this.#noRetry();
  }
};
function wrapExportedHandler(worker) {
  if (__INTERNAL_WRANGLER_MIDDLEWARE__ === void 0 || __INTERNAL_WRANGLER_MIDDLEWARE__.length === 0) {
    return worker;
  }
  for (const middleware of __INTERNAL_WRANGLER_MIDDLEWARE__) {
    __facade_register__(middleware);
  }
  const fetchDispatcher = /* @__PURE__ */ __name(function(request, env, ctx) {
    if (worker.fetch === void 0) {
      throw new Error("Handler does not export a fetch() function.");
    }
    return worker.fetch(request, env, ctx);
  }, "fetchDispatcher");
  return {
    ...worker,
    fetch(request, env, ctx) {
      const dispatcher = /* @__PURE__ */ __name(function(type, init) {
        if (type === "scheduled" && worker.scheduled !== void 0) {
          const controller = new __Facade_ScheduledController__(
            Date.now(),
            init.cron ?? "",
            () => {
            }
          );
          return worker.scheduled(controller, env, ctx);
        }
      }, "dispatcher");
      return __facade_invoke__(request, env, ctx, dispatcher, fetchDispatcher);
    }
  };
}
__name(wrapExportedHandler, "wrapExportedHandler");
function wrapWorkerEntrypoint(klass) {
  if (__INTERNAL_WRANGLER_MIDDLEWARE__ === void 0 || __INTERNAL_WRANGLER_MIDDLEWARE__.length === 0) {
    return klass;
  }
  for (const middleware of __INTERNAL_WRANGLER_MIDDLEWARE__) {
    __facade_register__(middleware);
  }
  return class extends klass {
    #fetchDispatcher = /* @__PURE__ */ __name((request, env, ctx) => {
      this.env = env;
      this.ctx = ctx;
      if (super.fetch === void 0) {
        throw new Error("Entrypoint class does not define a fetch() function.");
      }
      return super.fetch(request);
    }, "#fetchDispatcher");
    #dispatcher = /* @__PURE__ */ __name((type, init) => {
      if (type === "scheduled" && super.scheduled !== void 0) {
        const controller = new __Facade_ScheduledController__(
          Date.now(),
          init.cron ?? "",
          () => {
          }
        );
        return super.scheduled(controller);
      }
    }, "#dispatcher");
    fetch(request) {
      return __facade_invoke__(
        request,
        this.env,
        this.ctx,
        this.#dispatcher,
        this.#fetchDispatcher
      );
    }
  };
}
__name(wrapWorkerEntrypoint, "wrapWorkerEntrypoint");
var WRAPPED_ENTRY;
if (typeof middleware_insertion_facade_default === "object") {
  WRAPPED_ENTRY = wrapExportedHandler(middleware_insertion_facade_default);
} else if (typeof middleware_insertion_facade_default === "function") {
  WRAPPED_ENTRY = wrapWorkerEntrypoint(middleware_insertion_facade_default);
}
var middleware_loader_entry_default = WRAPPED_ENTRY;
export {
  __INTERNAL_WRANGLER_MIDDLEWARE__,
  middleware_loader_entry_default as default
};
//# sourceMappingURL=index.js.map
