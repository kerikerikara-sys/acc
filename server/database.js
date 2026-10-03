const path = require('path');
const fs = require('fs');
const crypto = require('crypto');
const bcrypt = require('bcryptjs');
const initSqlJs = require('sql.js');

const dbPath = path.join(__dirname, 'noxxer.db');

const hashPassword = (pass) => bcrypt.hashSync(pass, 10);
const verifyPassword = (pass, hash) => bcrypt.compareSync(pass, hash);

const generateKey = () => {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let s = '';
  for (let i = 0; i < 4; i++) {
    for (let j = 0; j < 4; j++) s += chars[Math.floor(Math.random() * chars.length)];
    if (i < 3) s += '-';
  }
  return s;
};

function columnsOf(db, table) {
  const r = db.exec(`PRAGMA table_info(${table})`);
  if (!r.length) return {};
  const out = {};
  for (const v of r[0].values) out[v[1]] = { notnull: v[3] };
  return out;
}

// Bases creadas con versiones anteriores: CREATE TABLE IF NOT EXISTS no las toca.
function migrate(db) {
  const gc = columnsOf(db, 'guild_config');
  if (!gc.pin_channel) db.exec('ALTER TABLE guild_config ADD COLUMN pin_channel TEXT');

  const pins = columnsOf(db, 'pins');
  const pinCols = {
    request_id: 'TEXT', attempts: 'INTEGER DEFAULT 0', token: 'TEXT', token_expires_at: 'TEXT',
    verified_at: 'TEXT', used_at: 'TEXT'
  };
  for (const c of Object.keys(pinCols)) {
    if (!pins[c]) db.exec(`ALTER TABLE pins ADD COLUMN ${c} ${pinCols[c]}`);
  }

  // Los escaneos con PIN no tienen usuario: user_id debe admitir NULL.
  const scans = columnsOf(db, 'scans');
  if (scans.user_id && scans.user_id.notnull) {
    const cols = 'id, user_id, username, hwid, started_at, ended_at, duration_sec, total_high, total_med, total_low, verdict, findings_json, reported_to_discord, created_at';
    db.exec(`
      BEGIN;
      CREATE TABLE scans_new (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        user_id INTEGER,
        username TEXT NOT NULL,
        hwid TEXT,
        started_at TEXT,
        ended_at TEXT,
        duration_sec INTEGER,
        total_high INTEGER DEFAULT 0,
        total_med INTEGER DEFAULT 0,
        total_low INTEGER DEFAULT 0,
        verdict TEXT,
        findings_json TEXT,
        reported_to_discord INTEGER DEFAULT 0,
        created_at TEXT DEFAULT CURRENT_TIMESTAMP
      );
      INSERT INTO scans_new (${cols}) SELECT ${cols} FROM scans;
      DROP TABLE scans;
      ALTER TABLE scans_new RENAME TO scans;
      COMMIT;
    `);
    console.log('[db] Migrada tabla scans: user_id ahora admite NULL');
  }
}

async function init() {
  const SQL = await initSqlJs();
  let db;
  let dirty = false;

  try {
    if (fs.existsSync(dbPath)) {
      const buf = fs.readFileSync(dbPath);
      db = new SQL.Database(buf);
    } else {
      db = new SQL.Database();
      dirty = true;
    }
  } catch (e) {
    console.warn('DB corrupt or missing, creating new:', e.message);
    db = new SQL.Database();
    dirty = true;
  }

  db.exec(`
    CREATE TABLE IF NOT EXISTS users (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      username TEXT UNIQUE NOT NULL,
      password_hash TEXT NOT NULL,
      discord_id TEXT,
      created_at TEXT DEFAULT CURRENT_TIMESTAMP,
      banned INTEGER DEFAULT 0,
      ban_reason TEXT
    );
    CREATE TABLE IF NOT EXISTS licenses (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      key TEXT UNIQUE NOT NULL,
      days INTEGER NOT NULL,
      created_by TEXT NOT NULL,
      created_at TEXT DEFAULT CURRENT_TIMESTAMP,
      activated_at TEXT,
      expires_at TEXT,
      user_id INTEGER,
      hwid TEXT,
      status TEXT DEFAULT 'unused',
      banned INTEGER DEFAULT 0,
      ban_reason TEXT
    );
    CREATE TABLE IF NOT EXISTS scans (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      user_id INTEGER,
      username TEXT NOT NULL,
      hwid TEXT,
      started_at TEXT,
      ended_at TEXT,
      duration_sec INTEGER,
      total_high INTEGER DEFAULT 0,
      total_med INTEGER DEFAULT 0,
      total_low INTEGER DEFAULT 0,
      verdict TEXT,
      findings_json TEXT,
      reported_to_discord INTEGER DEFAULT 0,
      created_at TEXT DEFAULT CURRENT_TIMESTAMP
    );
    CREATE TABLE IF NOT EXISTS guild_config (
      guild_id TEXT PRIMARY KEY,
      scan_logs_channel TEXT,
      admin_role TEXT,
      pin_channel TEXT,
      set_by TEXT,
      updated_at TEXT DEFAULT CURRENT_TIMESTAMP
    );
    CREATE TABLE IF NOT EXISTS pins (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      pin TEXT UNIQUE NOT NULL,
      request_id TEXT,
      hwid TEXT,
      ip TEXT,
      username TEXT,
      status TEXT DEFAULT 'issued',
      attempts INTEGER DEFAULT 0,
      token TEXT,
      token_expires_at TEXT,
      approved_by TEXT,
      rejected_by TEXT,
      reject_reason TEXT,
      scan_id INTEGER,
      created_at TEXT DEFAULT CURRENT_TIMESTAMP,
      verified_at TEXT,
      used_at TEXT,
      resolved_at TEXT,
      expires_at TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_pins_pin ON pins(pin);
    CREATE INDEX IF NOT EXISTS idx_pins_status ON pins(status);
  `);
  migrate(db);
  db.exec(`
    CREATE INDEX IF NOT EXISTS idx_pins_request ON pins(request_id);
    CREATE INDEX IF NOT EXISTS idx_pins_token ON pins(token);
  `);
  dirty = true;

  function save() {
    if (!dirty) return;
    try {
      const data = db.export();
      fs.writeFileSync(dbPath, Buffer.from(data));
      dirty = false;
    } catch (e) {
      console.error('DB save error:', e.message);
    }
  }
  setInterval(save, 5000);

  function lastId() {
    const r = db.exec('SELECT last_insert_rowid() AS id');
    return r && r[0] && r[0].values && r[0].values[0] ? r[0].values[0][0] : 0;
  }

  function rowOf(stmt) {
    const cols = stmt.getColumnNames();
    const vals = stmt.get();
    const o = {};
    for (let i = 0; i < cols.length; i++) o[cols[i]] = vals[i];
    return o;
  }

  function bindArr(stmt, params) {
    if (!params || params.length === 0) return;
    stmt.bind(params);
  }

  function getOne(sql, params) {
    try {
      const stmt = db.prepare(sql);
      bindArr(stmt, params || []);
      let o;
      if (stmt.step()) o = rowOf(stmt);
      stmt.free();
      return o;
    } catch (e) {
      console.error('DB getOne error:', e.message, sql);
      return undefined;
    }
  }

  function getAll(sql, params) {
    try {
      const stmt = db.prepare(sql);
      bindArr(stmt, params || []);
      const rows = [];
      while (stmt.step()) rows.push(rowOf(stmt));
      stmt.free();
      return rows;
    } catch (e) {
      console.error('DB getAll error:', e.message, sql);
      return [];
    }
  }

  function run(sql, params) {
    try {
      const p = params || [];
      db.run(sql, p);
      dirty = true;
      return { changes: db.getRowsModified(), lastInsertRowid: lastId() };
    } catch (e) {
      console.error('DB run error:', e.message, sql);
      return { changes: 0, lastInsertRowid: 0, error: e.message };
    }
  }

  function createLicense(days, createdBy) {
    const key = generateKey();
    run('INSERT INTO licenses (key, days, created_by, status) VALUES (?, ?, ?, ?)', [key, days, createdBy, 'unused']);
    return getOne('SELECT * FROM licenses WHERE key = ?', [key]);
  }

  function getActiveUserFromLicense(key) {
    return getOne(`
      SELECT u.*, l.id as license_id, l.key, l.days, l.activated_at, l.expires_at, l.hwid, l.status as lic_status, l.banned as lic_banned
      FROM licenses l
      LEFT JOIN users u ON u.id = l.user_id
      WHERE l.key = ?
    `, [key]);
  }

  function isLicenseValid(row) {
    if (!row) return { ok: false, reason: 'Licencia no encontrada' };
    if (row.lic_banned == 1) return { ok: false, reason: 'Licencia baneada: ' + (row.ban_reason || '') };
    if (row.banned == 1) return { ok: false, reason: 'Cuenta baneada' };
    if (row.lic_status === 'unused') return { ok: true, needActivation: true };
    if (row.lic_status !== 'active') return { ok: false, reason: 'Licencia no activa' };
    if (!row.expires_at) return { ok: false, reason: 'Fecha de expiracion invalida' };
    const now = new Date();
    const exp = new Date(row.expires_at);
    if (now > exp) return { ok: false, reason: 'Licencia expirada el ' + exp.toLocaleString() };
    return { ok: true, remaining: Math.max(0, Math.ceil((exp - now) / (1000 * 60 * 60 * 24))) };
  }

  // ------------------------------------------------------------------
  //  PINs de escaneo: el cliente pide uno, el PIN llega SOLO a Discord,
  //  el admin se lo dicta a la persona y el cliente lo canjea por un token
  //  de un solo uso con el que sube el resultado del escaneo.
  // ------------------------------------------------------------------
  const PIN_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  const PIN_LENGTH = 8;
  const PIN_TTL_MS = 10 * 60 * 1000;
  const PIN_MAX_ATTEMPTS = 5;
  const TOKEN_TTL_MS = 3 * 60 * 60 * 1000;

  function generatePin() {
    let s = '';
    for (let i = 0; i < PIN_LENGTH; i++) s += PIN_CHARS[crypto.randomInt(PIN_CHARS.length)];
    return s;
  }

  function sameText(a, b) {
    const x = Buffer.from(String(a));
    const y = Buffer.from(String(b));
    return x.length === y.length && crypto.timingSafeEqual(x, y);
  }

  function isExpired(iso) {
    if (!iso) return false;
    const t = new Date(iso).getTime();
    return !isNaN(t) && Date.now() > t;
  }

  function createPinRequest(hwid, ip, username) {
    const requestId = crypto.randomBytes(16).toString('hex');
    const expires = new Date(Date.now() + PIN_TTL_MS).toISOString();
    for (let tries = 0; tries < 5; tries++) {
      const r = run(
        'INSERT INTO pins (pin, request_id, hwid, ip, username, status, attempts, expires_at) VALUES (?, ?, ?, ?, ?, ?, 0, ?)',
        [generatePin(), requestId, hwid || null, ip || null, username || null, 'issued', expires]
      );
      if (!r.error) return getOne('SELECT * FROM pins WHERE request_id = ?', [requestId]);
    }
    return null;
  }

  function deletePin(id) {
    run('DELETE FROM pins WHERE id = ?', [id]);
  }

  function getPin(pin) {
    return getOne('SELECT * FROM pins WHERE pin = ?', [(pin || '').toUpperCase().trim()]);
  }

  // Devuelve { ok, token, row } o { ok:false, code, error, attemptsLeft? }
  function verifyPin(requestId, pin, hwid) {
    const row = getOne('SELECT * FROM pins WHERE request_id = ?', [String(requestId || '')]);
    if (!row) return { ok: false, code: 'not_found', error: 'Solicitud no encontrada. Pide un PIN nuevo.' };
    if (row.status === 'issued' && isExpired(row.expires_at)) {
      run("UPDATE pins SET status = 'expired', resolved_at = ? WHERE id = ?", [new Date().toISOString(), row.id]);
      row.status = 'expired';
    }
    if (row.status === 'expired') return { ok: false, code: 'expired', error: 'El PIN ha caducado. Pide uno nuevo.' };
    if (row.status === 'revoked') return { ok: false, code: 'revoked', error: 'El administrador ha anulado este PIN.' };
    if (row.status === 'locked') return { ok: false, code: 'locked', error: 'Demasiados intentos fallidos. Pide un PIN nuevo.' };
    if (row.status !== 'issued') return { ok: false, code: 'used', error: 'Este PIN ya se ha usado. Pide uno nuevo.' };
    if (row.hwid && hwid !== row.hwid) return { ok: false, code: 'hwid', error: 'Este PIN se pidio desde otro equipo.' };

    const typed = String(pin || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
    if (!sameText(typed, row.pin)) {
      const attempts = (row.attempts || 0) + 1;
      const locked = attempts >= PIN_MAX_ATTEMPTS;
      run('UPDATE pins SET attempts = ?, status = ?, resolved_at = ? WHERE id = ?',
        [attempts, locked ? 'locked' : 'issued', locked ? new Date().toISOString() : null, row.id]);
      if (locked) return { ok: false, code: 'locked', error: 'PIN incorrecto. Demasiados intentos: pide un PIN nuevo.', row };
      return { ok: false, code: 'wrong', error: 'PIN incorrecto.', attemptsLeft: PIN_MAX_ATTEMPTS - attempts };
    }

    const token = crypto.randomBytes(32).toString('hex');
    const now = new Date();
    run("UPDATE pins SET status = 'verified', token = ?, token_expires_at = ?, verified_at = ? WHERE id = ?",
      [token, new Date(now.getTime() + TOKEN_TTL_MS).toISOString(), now.toISOString(), row.id]);
    return { ok: true, token, expiresIn: Math.floor(TOKEN_TTL_MS / 1000), row: getOne('SELECT * FROM pins WHERE id = ?', [row.id]) };
  }

  // Token valido y sin usar -> fila del PIN; si no, { error }
  function findScanToken(token, hwid) {
    if (!token || typeof token !== 'string' || !/^[a-f0-9]{64}$/.test(token)) return { error: 'Token invalido' };
    const row = getOne('SELECT * FROM pins WHERE token = ?', [token]);
    if (!row) return { error: 'Token invalido' };
    if (row.status !== 'verified') return { error: 'Este token ya se ha usado o fue anulado' };
    if (isExpired(row.token_expires_at)) return { error: 'El token ha caducado' };
    if (row.hwid && hwid && hwid !== row.hwid) return { error: 'El token pertenece a otro equipo' };
    return { row };
  }

  function markPinUsed(id, scanId) {
    run("UPDATE pins SET status = 'used', used_at = ?, scan_id = ? WHERE id = ?", [new Date().toISOString(), scanId, id]);
  }

  function listActivePins(limit) {
    return getAll(
      `SELECT * FROM pins WHERE (status = 'issued' AND expires_at > ?) OR (status = 'verified' AND token_expires_at > ?)
       ORDER BY id DESC LIMIT ?`,
      [new Date().toISOString(), new Date().toISOString(), limit || 25]
    );
  }

  function revokePin(pin, by, reason) {
    const now = new Date().toISOString();
    const r = run(
      `UPDATE pins SET status = 'revoked', rejected_by = ?, reject_reason = ?, resolved_at = ? WHERE pin = ? AND status IN ('issued', 'verified')`,
      [by || null, reason || null, now, (pin || '').toUpperCase().trim()]
    );
    return r.changes > 0 ? getPin(pin) : null;
  }

  const ctx = {
    SQL, db, save, run, getOne, getAll,
    hashPassword, verifyPassword, generateKey, generatePin,
    createLicense, getActiveUserFromLicense, isLicenseValid,
    createPinRequest, deletePin, getPin, verifyPin, findScanToken, markPinUsed, listActivePins, revokePin
  };
  save();
  return ctx;
}

module.exports = {
  init,
  hashPassword,
  verifyPassword,
  generateKey
};
