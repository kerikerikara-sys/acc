const path = require('path');
const fs = require('fs');
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
      user_id INTEGER NOT NULL,
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
      hwid TEXT,
      ip TEXT,
      username TEXT,
      status TEXT DEFAULT 'pending',
      approved_by TEXT,
      rejected_by TEXT,
      reject_reason TEXT,
      scan_id INTEGER,
      created_at TEXT DEFAULT CURRENT_TIMESTAMP,
      resolved_at TEXT,
      expires_at TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_pins_pin ON pins(pin);
    CREATE INDEX IF NOT EXISTS idx_pins_status ON pins(status);
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

  const PIN_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  const PIN_LENGTH = 8;

  function generatePin() {
    let s = '';
    for (let i = 0; i < PIN_LENGTH; i++) s += PIN_CHARS[Math.floor(Math.random() * PIN_CHARS.length)];
    return s;
  }

  function createPin(hwid, ip, username) {
    const pin = generatePin();
    const now = new Date();
    const expires = new Date(now.getTime() + 15 * 60 * 1000);
    run(
      'INSERT INTO pins (pin, hwid, ip, username, status, expires_at) VALUES (?, ?, ?, ?, ?, ?)',
      [pin, hwid || null, ip || null, username || null, 'pending', expires.toISOString()]
    );
    return getOne('SELECT * FROM pins WHERE pin = ?', [pin]);
  }

  function getPin(pin) {
    return getOne('SELECT * FROM pins WHERE pin = ?', [(pin || '').toUpperCase().trim()]);
  }

  function listPendingPins(limit) {
    const rows = getAll(
      `SELECT * FROM pins WHERE status = 'pending' AND (expires_at IS NULL OR expires_at > ?) ORDER BY created_at DESC LIMIT ?`,
      [new Date().toISOString(), limit || 50]
    );
    return rows;
  }

  function approvePin(pin, by) {
    const now = new Date().toISOString();
    run(
      `UPDATE pins SET status = 'approved', approved_by = ?, resolved_at = ? WHERE pin = ? AND status = 'pending'`,
      [by || null, now, (pin || '').toUpperCase().trim()]
    );
    return getPin(pin);
  }

  function rejectPin(pin, by, reason) {
    const now = new Date().toISOString();
    run(
      `UPDATE pins SET status = 'rejected', rejected_by = ?, reject_reason = ?, resolved_at = ? WHERE pin = ? AND status = 'pending'`,
      [by || null, reason || null, now, (pin || '').toUpperCase().trim()]
    );
    return getPin(pin);
  }

  const ctx = {
    SQL, db, save, run, getOne, getAll,
    hashPassword, verifyPassword, generateKey, generatePin,
    createLicense, getActiveUserFromLicense, isLicenseValid,
    createPin, getPin, listPendingPins, approvePin, rejectPin
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
