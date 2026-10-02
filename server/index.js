require('dotenv').config();
const express = require('express');
const cors = require('cors');
const Database = require('./database.js');

const app = express();
app.use(cors());
app.use(express.json({ limit: '10mb' }));

const PORT = process.env.PORT || 3000;

Database.init().then((db) => {
  const { run, getOne, getAll, hashPassword, verifyPassword, getActiveUserFromLicense, isLicenseValid } = db;

  app.get('/', (req, res) => {
    res.json({ ok: true, service: 'Noxxer Licensing', time: new Date().toISOString() });
  });

  const requireJson = (req, res, next) => {
    if (!req.is('json')) return res.status(400).json({ ok: false, error: 'Body JSON requerido' });
    next();
  };

  app.post('/api/auth/signup', requireJson, (req, res) => {
    const { username, password, licenseKey } = req.body || {};
    if (!username || !password || !licenseKey)
      return res.status(400).json({ ok: false, error: 'username, password y licenseKey son requeridos' });
    if (username.length < 3 || username.length > 24)
      return res.status(400).json({ ok: false, error: 'Username debe tener 3-24 caracteres' });
    if (password.length < 6)
      return res.status(400).json({ ok: false, error: 'Password debe tener al menos 6 caracteres' });

    const cleanUser = username.trim();
    const cleanKey = licenseKey.trim().toUpperCase();

    const lic = getOne('SELECT * FROM licenses WHERE key = ?', [cleanKey]);
    if (!lic) return res.status(404).json({ ok: false, error: 'License key no valida' });
    if (lic.banned == 1) return res.status(403).json({ ok: false, error: 'Esta licencia esta baneada' + (lic.ban_reason ? ': ' + lic.ban_reason : '') });
    if (lic.status === 'active') return res.status(409).json({ ok: false, error: 'Esta licencia ya esta activada en otra cuenta' });
    if (lic.status !== 'unused') return res.status(409).json({ ok: false, error: 'Licencia invalida' });

    const existing = getOne('SELECT id FROM users WHERE username = ?', [cleanUser]);
    if (existing) return res.status(409).json({ ok: false, error: 'Username ya existe' });

    try {
      const info = run('INSERT INTO users (username, password_hash) VALUES (?, ?)', [cleanUser, hashPassword(password)]);
      const userId = info.lastInsertRowid;
      const now = new Date();
      const expires = new Date(now.getTime() + lic.days * 24 * 60 * 60 * 1000);
      run('UPDATE licenses SET user_id = ?, status = ?, activated_at = ?, expires_at = ? WHERE id = ?',
        [userId, 'active', now.toISOString(), expires.toISOString(), lic.id]);
      const row = getActiveUserFromLicense(cleanKey);
      const check = isLicenseValid(row);
      const rem = check.ok ? check.remaining : lic.days;
      return res.json({
        ok: true,
        message: 'Cuenta creada y licencia activada',
        username: cleanUser,
        license: cleanKey,
        activatedAt: row ? row.activated_at : null,
        expiresAt: row ? row.expires_at : null,
        remainingDays: rem,
        user: {
          id: userId,
          username: cleanUser,
          license: cleanKey,
          activatedAt: row ? row.activated_at : null,
          expiresAt: row ? row.expires_at : null,
          remainingDays: rem
        }
      });
    } catch (e) {
      console.error(e);
      return res.status(500).json({ ok: false, error: 'Error interno al crear cuenta' });
    }
  });

  app.post('/api/auth/login', requireJson, (req, res) => {
    const { username, password, licenseKey, hwid } = req.body || {};
    if (!username || !password || !licenseKey)
      return res.status(400).json({ ok: false, error: 'username, password y licenseKey son requeridos' });
    const cleanUser = username.trim();
    const cleanKey = licenseKey.trim().toUpperCase();

    const user = getOne('SELECT * FROM users WHERE username = ?', [cleanUser]);
    if (!user) return res.status(401).json({ ok: false, error: 'Credenciales invalidas' });
    if (!verifyPassword(password, user.password_hash)) return res.status(401).json({ ok: false, error: 'Credenciales invalidas' });
    if (user.banned == 1) return res.status(403).json({ ok: false, error: 'Cuenta baneada' + (user.ban_reason ? ': ' + user.ban_reason : '') });

    const lic = getOne('SELECT * FROM licenses WHERE key = ?', [cleanKey]);
    if (!lic) return res.status(404).json({ ok: false, error: 'License key no valida' });
    if (lic.banned == 1) return res.status(403).json({ ok: false, error: 'Licencia baneada' + (lic.ban_reason ? ': ' + lic.ban_reason : '') });
    if (lic.user_id && lic.user_id !== user.id) return res.status(403).json({ ok: false, error: 'Esa licencia pertenece a otra cuenta' });

    if (lic.status === 'unused') {
      const now = new Date();
      const expires = new Date(now.getTime() + lic.days * 24 * 60 * 60 * 1000);
      run('UPDATE licenses SET user_id = ?, status = ?, activated_at = ?, expires_at = ?, hwid = ? WHERE id = ?',
        [user.id, 'active', now.toISOString(), expires.toISOString(), hwid || null, lic.id]);
    } else if (lic.status === 'active') {
      if (!lic.hwid && hwid) {
        run('UPDATE licenses SET hwid = ? WHERE id = ?', [hwid, lic.id]);
      } else if (lic.hwid && hwid && lic.hwid !== hwid) {
        return res.status(403).json({ ok: false, error: 'HWID no coincide. Pide a un admin que resetee tu HWID.', hwidMismatch: true });
      }
    }

    const row = getActiveUserFromLicense(cleanKey);
    const check = isLicenseValid(row);
    if (!check.ok) return res.status(403).json({ ok: false, error: check.reason });

    return res.json({
      ok: true,
      message: 'Login correcto',
      username: user.username,
      license: cleanKey,
      activatedAt: row ? row.activated_at : null,
      expiresAt: row ? row.expires_at : null,
      remainingDays: check.remaining,
      user: {
        id: user.id,
        username: user.username,
        license: cleanKey,
        activatedAt: row ? row.activated_at : null,
        expiresAt: row ? row.expires_at : null,
        remainingDays: check.remaining
      }
    });
  });

  app.post('/api/auth/verify', requireJson, (req, res) => {
    const { username, licenseKey, hwid } = req.body || {};
    if (!username || !licenseKey) return res.status(400).json({ ok: false, error: 'Faltan datos' });
    const cleanUser = username.trim();
    const cleanKey = licenseKey.trim().toUpperCase();

    const row = getActiveUserFromLicense(cleanKey);
    if (!row || row.username !== cleanUser) return res.status(401).json({ ok: false, error: 'Licencia no coincide con usuario' });
    if (row.hwid && hwid && row.hwid !== hwid) return res.status(403).json({ ok: false, error: 'HWID no coincide', hwidMismatch: true });

    const check = isLicenseValid(row);
    return res.json({ ok: check.ok, error: check.reason || null, remainingDays: check.ok ? check.remaining : 0, expiresAt: row ? row.expires_at : null });
  });

  app.post('/api/scans/submit', requireJson, (req, res) => {
    const { username, licenseKey, hwid, startedAt, endedAt, totalHigh, totalMed, totalLow, verdict, durationSec, findings } = req.body || {};
    if (!username || !licenseKey) return res.status(400).json({ ok: false, error: 'Faltan datos' });
    const cleanUser = username.trim();
    const cleanKey = licenseKey.trim().toUpperCase();

    const lic = getOne('SELECT * FROM licenses WHERE key = ?', [cleanKey]);
    if (!lic || !lic.user_id) return res.status(404).json({ ok: false, error: 'Licencia invalida' });
    const user = getOne('SELECT id, username, banned FROM users WHERE id = ?', [lic.user_id]);
    if (!user || user.username !== cleanUser) return res.status(401).json({ ok: false, error: 'Usuario no coincide' });

    try {
      let findingsText = null;
      if (findings) {
        try { findingsText = JSON.stringify(findings).slice(0, 2000000); } catch {}
      }
      const info = run(`
        INSERT INTO scans (user_id, username, hwid, started_at, ended_at, duration_sec, total_high, total_med, total_low, verdict, findings_json)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
      `, [
        user.id, cleanUser, hwid || null,
        startedAt || null, endedAt || null,
        durationSec || null,
        totalHigh || 0, totalMed || 0, totalLow || 0,
        verdict || null,
        findingsText
      ]);
      return res.json({ ok: true, id: info.lastInsertRowid });
    } catch (e) {
      console.error('Scan submit error:', e);
      return res.status(500).json({ ok: false, error: e.message });
    }
  });

  let actualPort = PORT;
  const server = app.listen(PORT);
  server.once('listening', () => {
    console.log(`[+] Noxxer Licensing API running on http://localhost:${actualPort}`);
  });
  server.once('error', (err) => {
    if (err && err.code === 'EADDRINUSE') {
      console.warn(`[!] Puerto ${PORT} ocupado. Probando ${parseInt(PORT) + 1}...`);
      actualPort = parseInt(PORT) + 1;
      const srv2 = app.listen(actualPort, () => {
        console.log(`[+] Noxxer Licensing API running on http://localhost:${actualPort}  (fallback)`);
      });
      srv2.on('error', (e2) => {
        console.error(`[✗] No se puede abrir el servidor: ${e2.message}`);
        console.error('    Cierra otras ventanas del servidor o cambia el PORT en el .env');
        process.exit(1);
      });
      Object.assign(server, srv2);
    } else {
      console.error(`[✗] Error al abrir el servidor: ${err.message}`);
      process.exit(1);
    }
  });

  try {
    const startBot = require('./bot.js');
    if (typeof startBot === 'function') startBot(db);
  } catch (e) {
    console.warn('Bot no disponible:', e.message);
  }

  process.on('SIGINT', () => { try { db.save(); } catch {} process.exit(0); });
  process.on('SIGTERM', () => { try { db.save(); } catch {} process.exit(0); });
  process.on('beforeExit', () => { try { db.save(); } catch {} });

}).catch((e) => {
  console.error('Failed to start server:', e);
  process.exit(1);
});
