require('dotenv').config();
const os = require('os');
const express = require('express');
const cors = require('cors');
const Database = require('./database.js');

const app = express();
app.use(cors());
app.use(express.json({ limit: '10mb' }));

const PORT = process.env.PORT || 3000;
const HOST = process.env.HOST || '0.0.0.0';

function listLocalIps() {
  const ips = [];
  const nets = os.networkInterfaces();
  for (const name of Object.keys(nets)) {
    for (const net of nets[name]) {
      if (net.family === 'IPv4' && !net.internal) ips.push(net.address);
    }
  }
  return ips;
}

function printListenInfo(port) {
  const ips = listLocalIps();
  console.log('');
  console.log('============================================');
  console.log('   Noxxer Licensing API - En linea');
  console.log('============================================');
  console.log(`  Local:      http://localhost:${port}`);
  if (ips.length) {
    console.log(`  Red local:  http://${ips[0]}:${port}`);
    if (ips.length > 1) console.log(`  Otras IPs:  ${ips.slice(1).map(i => `http://${i}:${port}`).join(', ')}`);
  }
  console.log(`  VPS/Public: usa la IP publica de tu servidor y abre el puerto ${port} en el firewall`);
  console.log('============================================');
  console.log('');
}

Database.init().then((db) => {
  const {
    run, getOne, getAll, hashPassword, verifyPassword, getActiveUserFromLicense, isLicenseValid,
    createPinRequest, deletePin, verifyPin, findScanToken, markPinUsed
  } = db;

  // X-Forwarded-For solo es fiable detras de un proxy propio (nginx, Cloudflare...): TRUST_PROXY=1
  const TRUST_PROXY = process.env.TRUST_PROXY === '1';
  function getClientIp(req) {
    if (TRUST_PROXY) {
      const fwd = req.headers && (req.headers['x-forwarded-for'] || req.headers['x-real-ip']);
      if (fwd) return String(fwd).split(',')[0].trim();
    }
    const a = req.socket && req.socket.remoteAddress;
    return a ? String(a).replace(/^::ffff:/, '') : null;
  }

  // Limite de peticiones en memoria: max hits por clave dentro de la ventana
  const hits = new Map();
  function limited(key, max, windowMs) {
    const now = Date.now();
    const list = (hits.get(key) || []).filter(t => now - t < windowMs);
    if (list.length >= max) { hits.set(key, list); return true; }
    list.push(now);
    hits.set(key, list);
    return false;
  }
  setInterval(() => {
    const now = Date.now();
    for (const [k, list] of hits) if (!list.length || now - list[list.length - 1] > 60 * 60 * 1000) hits.delete(k);
  }, 10 * 60 * 1000).unref();

  const cleanName = (v) => (typeof v === 'string' ? v.replace(/[\u0000-\u001f`*_~|<>@#]/g, '').trim().slice(0, 32) : '') || null;

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
    const { username, licenseKey, token, hwid, startedAt, endedAt, totalHigh, totalMed, totalLow, verdict, durationSec, findings } = req.body || {};
    const hasLicense = typeof username === 'string' && typeof licenseKey === 'string' && !!username && !!licenseKey;
    if (!token && !hasLicense) return res.status(400).json({ ok: false, error: 'Faltan datos: se requiere un PIN verificado' });

    let userId = null;
    let finalUser = null;
    let finalHwid = typeof hwid === 'string' ? hwid.trim().slice(0, 512) : null;
    let pinRow = null;

    if (token) {
      const t = findScanToken(token, finalHwid);
      if (t.error) return res.status(403).json({ ok: false, error: t.error });
      pinRow = t.row;
      finalUser = pinRow.username || 'Sin nombre';
      finalHwid = pinRow.hwid || finalHwid;
    } else {
      const cleanUser = username.trim();
      const cleanKey = licenseKey.trim().toUpperCase();
      finalUser = cleanUser;
      const lic = getOne('SELECT * FROM licenses WHERE key = ?', [cleanKey]);
      if (!lic || !lic.user_id) return res.status(404).json({ ok: false, error: 'Licencia invalida' });
      const user = getOne('SELECT id, username, banned FROM users WHERE id = ?', [lic.user_id]);
      if (!user || user.username !== cleanUser) return res.status(401).json({ ok: false, error: 'Usuario no coincide' });
      userId = user.id;
    }

    try {
      let findingsText = null;
      if (findings) {
        try { findingsText = JSON.stringify(findings).slice(0, 2000000); } catch {}
      }
      const info = run(`
        INSERT INTO scans (user_id, username, hwid, started_at, ended_at, duration_sec, total_high, total_med, total_low, verdict, findings_json)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
      `, [
        userId, finalUser, finalHwid,
        startedAt || null, endedAt || null,
        durationSec || null,
        totalHigh || 0, totalMed || 0, totalLow || 0,
        verdict || null,
        findingsText
      ]);
      if (info.error) {
        console.error('Scan submit error:', info.error);
        return res.status(500).json({ ok: false, error: 'No se pudo guardar el escaneo' });
      }
      const scanId = info.lastInsertRowid;
      if (pinRow) markPinUsed(pinRow.id, scanId);
      return res.json({ ok: true, id: scanId });
    } catch (e) {
      console.error('Scan submit error:', e);
      return res.status(500).json({ ok: false, error: 'No se pudo guardar el escaneo' });
    }
  });

  // ------------------------------------------------------------------
  //  SISTEMA DE PINS
  //  1. El cliente pide un PIN: el servidor lo genera y lo manda SOLO a Discord.
  //  2. El admin le dice el PIN a la persona y esta lo escribe en la app.
  //  3. /verifypin lo canjea por un token de un solo uso para subir el escaneo.
  // ------------------------------------------------------------------
  app.post('/api/auth/requestpin', requireJson, async (req, res) => {
    const { hwid, username } = req.body || {};
    if (typeof hwid !== 'string' || !/^[A-Za-z0-9-]{8,128}$/.test(hwid))
      return res.status(400).json({ ok: false, error: 'HWID invalido' });

    const ip = getClientIp(req);
    if (limited('req-ip:' + ip, 5, 10 * 60 * 1000) || limited('req-hwid:' + hwid, 3, 10 * 60 * 1000))
      return res.status(429).json({ ok: false, error: 'Demasiadas solicitudes. Espera unos minutos.' });

    const send = app.locals.sendPinToDiscord;
    if (typeof send !== 'function')
      return res.status(503).json({ ok: false, error: 'El bot de Discord no esta activo. Avisa al administrador.' });

    const p = createPinRequest(hwid, ip, cleanName(username));
    if (!p) return res.status(500).json({ ok: false, error: 'No se pudo generar el PIN' });

    let delivered = false;
    try { delivered = await send(p); } catch (e) { console.error('sendPinToDiscord:', e && e.message ? e.message : e); }
    if (!delivered) {
      deletePin(p.id);
      return res.status(503).json({ ok: false, error: 'No se pudo enviar el PIN a Discord. Avisa al administrador.' });
    }

    const expiresIn = Math.max(0, Math.round((new Date(p.expires_at).getTime() - Date.now()) / 1000));
    return res.json({ ok: true, requestId: p.request_id, expiresIn, expiresAt: p.expires_at });
  });

  app.post('/api/auth/verifypin', requireJson, (req, res) => {
    const { requestId, pin, hwid } = req.body || {};
    if (typeof requestId !== 'string' || typeof pin !== 'string')
      return res.status(400).json({ ok: false, error: 'Faltan datos' });
    if (limited('verify-ip:' + getClientIp(req), 20, 10 * 60 * 1000))
      return res.status(429).json({ ok: false, error: 'Demasiados intentos. Espera unos minutos.' });

    const r = verifyPin(requestId, pin, typeof hwid === 'string' ? hwid : null);
    const notify = app.locals.notifyPinEvent;
    if (!r.ok) {
      if (r.code === 'locked' && r.row && typeof notify === 'function') notify(r.row, 'locked');
      const status = r.code === 'wrong' ? 401 : r.code === 'not_found' ? 404 : 403;
      return res.status(status).json({ ok: false, code: r.code, error: r.error, attemptsLeft: r.attemptsLeft == null ? null : r.attemptsLeft });
    }
    if (typeof notify === 'function') notify(r.row, 'verified');
    return res.json({ ok: true, token: r.token, expiresIn: r.expiresIn });
  });

  let actualPort = PORT;
  const server = app.listen(PORT, HOST);
  server.once('listening', () => {
    printListenInfo(actualPort);
  });
  server.once('error', (err) => {
    if (err && err.code === 'EADDRINUSE') {
      console.warn(`[!] Puerto ${PORT} ocupado. Probando ${parseInt(PORT) + 1}...`);
      actualPort = parseInt(PORT) + 1;
      const srv2 = app.listen(actualPort, HOST, () => {
        console.log('[*] (fallback: puerto alternativo)');
        printListenInfo(actualPort);
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
    if (typeof startBot === 'function') startBot(db, app);
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
