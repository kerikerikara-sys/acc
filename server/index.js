// Carga el .env de esta carpeta (server/) aunque se arranque desde otro sitio.
require('dotenv').config({ path: require('path').join(__dirname, '.env') });
const os = require('os');
const express = require('express');
const cors = require('cors');
const path = require('path');
const Database = require('./database.js');
const mountAdmin = require('./admin.js');

const app = express();
app.use(cors());
app.use(express.json({ limit: '10mb' }));

// Sitio web de marketing (carpeta web/ en la raiz del repo) servido en /site,
// y sus archivos sueltos. El panel de admin se monta mas abajo en /admin.
app.use('/site', express.static(path.join(__dirname, '..', 'web')));

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
    createPin, getPin, listPendingPins, approvePin, rejectPin
  } = db;

  function getClientIp(req) {
    const fwd = req.headers && (req.headers['x-forwarded-for'] || req.headers['x-real-ip']);
    if (fwd) return String(fwd).split(',')[0].trim();
    if (req.socket && req.socket.remoteAddress) return req.socket.remoteAddress;
    return null;
  }

  app.get('/', (req, res) => {
    // Los navegadores piden HTML: les damos la web. El escaner (ACNoxxer.exe)
    // no manda Accept: text/html y sigue recibiendo el JSON de estado.
    if ((req.headers.accept || '').includes('text/html'))
      return res.sendFile(path.join(__dirname, '..', 'web', 'index.html'));
    res.json({ ok: true, service: 'Noxxer Licensing', time: new Date().toISOString() });
  });

  // Panel de admin + endpoints del enlace de descarga.
  mountAdmin(app, db);

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
    const { username, licenseKey, pin, hwid, startedAt, endedAt, totalHigh, totalMed, totalLow, verdict, durationSec, findings } = req.body || {};
    const hasLicense = !!username && !!licenseKey;
    const hasPin = !!pin;
    if (!hasLicense && !hasPin) return res.status(400).json({ ok: false, error: 'Faltan datos: se requiere licencia o PIN aprobado' });

    let userId = null;
    let finalUser = null;
    let finalHwid = hwid ? String(hwid).trim().slice(0, 512) : null;
    let scanPinId = null;

    if (hasPin) {
      const cleanPin = String(pin).trim().toUpperCase();
      const p = getOne('SELECT * FROM pins WHERE pin = ?', [cleanPin]);
      if (!p) return res.status(404).json({ ok: false, error: 'PIN no encontrado' });
      if (p.status !== 'approved') return res.status(403).json({ ok: false, error: 'PIN no aprobado. Estado: ' + (p.status || 'unknown') });
      // El codigo nunca se usa como nombre: es secreto. Solo vale desde el PC que lo pidio y durante 24 h tras aprobarse.
      if (p.hwid && p.hwid !== finalHwid) return res.status(403).json({ ok: false, error: 'PIN no valido para este equipo' });
      const resolved = p.resolved_at ? new Date(p.resolved_at) : null;
      if (!resolved || isNaN(resolved) || Date.now() - resolved.getTime() > 24 * 60 * 60 * 1000)
        return res.status(403).json({ ok: false, error: 'PIN caducado. Vuelve a pedir un codigo.' });
      finalUser = p.username || ('Escaneo #' + p.id);
      userId = 0;   // scans.user_id es NOT NULL: 0 = escaneo por codigo (sin cuenta)
      scanPinId = p.id;
    } else {
      const cleanUser = String(username).trim();
      const cleanKey = String(licenseKey).trim().toUpperCase();
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
      if (!info || info.error || !info.lastInsertRowid) {
        console.error('Scan submit: no se pudo guardar el escaneo:', info && info.error);
        return res.status(500).json({ ok: false, error: 'No se pudo guardar el escaneo en el servidor' });
      }
      const scanId = info.lastInsertRowid;
      if (scanPinId) {
        try { run('UPDATE pins SET scan_id = ? WHERE id = ?', [scanId, scanPinId]); } catch {}
      }
      return res.json({ ok: true, id: scanId });
    } catch (e) {
      console.error('Scan submit error:', e);
      return res.status(500).json({ ok: false, error: e.message });
    }
  });

  // ------------------------------------------------------------------
  //  SISTEMA DE PINS (NO LOGIN)
  // ------------------------------------------------------------------
  // Limite de solicitudes de codigo (cada una manda un DM al admin): por IP del socket y global.
  const pinReqLog = new Map();
  let pinReqGlobal = [];
  function pinRequestAllowed(key) {
    const now = Date.now(), win = 10 * 60 * 1000;
    pinReqGlobal = pinReqGlobal.filter(t => now - t < win);
    const mine = (pinReqLog.get(key) || []).filter(t => now - t < win);
    if (pinReqLog.size > 5000) pinReqLog.clear();
    if (pinReqGlobal.length >= 30 || mine.length >= 5 || (mine.length && now - mine[mine.length - 1] < 3000)) {
      pinReqLog.set(key, mine);
      return false;
    }
    mine.push(now); pinReqGlobal.push(now); pinReqLog.set(key, mine);
    return true;
  }

  // Flujo del PIN: el cliente pide un codigo, el servidor lo manda por DM privado al admin
  // (el cliente NUNCA recibe el codigo) y la persona escaneada lo escribe en su ventana.
  app.post('/api/auth/requestpin', requireJson, async (req, res) => {
    const { hwid, username } = req.body || {};
    if (hwid && typeof hwid === 'string' && hwid.length > 2000)
      return res.status(400).json({ ok: false, error: 'HWID invalido (demasiado largo)' });

    if (!hwid || typeof hwid !== 'string' || hwid.trim().length < 3)
      return res.status(400).json({ ok: false, error: 'Falta el identificador del equipo' });

    if (!pinRequestAllowed((req.socket && req.socket.remoteAddress) || 'unknown'))
      return res.status(429).json({ ok: false, error: 'Demasiadas solicitudes de codigo. Espera unos minutos y vuelve a probar.' });

    if (typeof app._notifyPinRequest !== 'function')
      return res.status(503).json({ ok: false, error: 'El bot de Discord no esta activo en el servidor, asi que no se puede mandar el codigo al admin.' });

    const ip = getClientIp(req);
    const cleanUser = username && typeof username === 'string' ? username.trim().slice(0, 48) : null;
    const cleanHwid = hwid ? String(hwid).trim().slice(0, 512) : null;

    // Un solo codigo vivo por equipo: los anteriores sin usar dejan de valer.
    try { run(`UPDATE pins SET status = 'rejected', reject_reason = 'superseded', resolved_at = ? WHERE hwid = ? AND status = 'pending'`, [new Date().toISOString(), cleanHwid]); } catch {}

    const p = createPin(cleanHwid, ip, cleanUser);
    if (!p || !p.pin) return res.status(500).json({ ok: false, error: 'No se pudo generar el pin' });

    let sent = false;
    try { sent = await app._notifyPinRequest(p); } catch (e) { console.error('notifyPinRequest error:', e && e.message ? e.message : e); }
    if (!sent) {
      try { rejectPin(p.pin, 'system', 'dm-failed'); } catch {}
      return res.status(502).json({ ok: false, error: 'No se pudo mandar el codigo al DM del admin. Revisa ADMIN_ID en el .env y que el admin tenga los mensajes directos abiertos.' });
    }

    // OJO: no se devuelve el codigo (p.pin) al cliente.
    return res.json({
      ok: true,
      status: p.status,
      created_at: p.created_at,
      expires_at: p.expires_at
    });
  });

  // Intentos fallidos por IP real del socket (X-Forwarded-For se puede falsificar).
  const pinFails = new Map();
  const PIN_MAX_FAILS = 8, PIN_FAIL_WINDOW_MS = 10 * 60 * 1000;
  function pinBlocked(key) {
    const f = pinFails.get(key);
    if (!f) return false;
    if (Date.now() - f.t > PIN_FAIL_WINDOW_MS) { pinFails.delete(key); return false; }
    return f.n >= PIN_MAX_FAILS;
  }
  function pinFail(key) {
    if (pinFails.size > 5000) pinFails.clear();
    const f = pinFails.get(key);
    if (!f || Date.now() - f.t > PIN_FAIL_WINDOW_MS) pinFails.set(key, { n: 1, t: Date.now() });
    else f.n++;
  }

  app.post('/api/auth/verifypin', requireJson, (req, res) => {
    const { hwid, pin } = req.body || {};
    const key = (req.socket && req.socket.remoteAddress) || 'unknown';
    if (pinBlocked(key))
      return res.status(429).json({ ok: false, error: 'Demasiados intentos fallidos. Espera unos minutos y vuelve a probar.' });

    const clean = String(pin || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
    const cleanHwid = hwid ? String(hwid).trim().slice(0, 512) : null;
    const bad = (msg, status) => {
      pinFail(key);
      return res.status(403).json({ ok: false, status: status || 'invalid', error: msg || 'Codigo incorrecto' });
    };

    if (!/^[A-Z0-9]{8}$/.test(clean)) return bad('Codigo incorrecto');
    const p = getPin(clean);
    // Mismo mensaje si no existe o si es de otro PC: no se da pistas.
    if (!p || (p.hwid && p.hwid !== cleanHwid)) return bad('Codigo incorrecto');
    if (p.status === 'rejected') return bad('Codigo incorrecto');
    if (p.expires_at && new Date() > new Date(p.expires_at)) return bad('El codigo ha expirado. Pide uno nuevo.', 'expired');

    if (p.status === 'pending') approvePin(clean, 'code-entry');
    const done = getPin(clean);
    pinFails.delete(key);
    return res.json({ ok: true, pin: clean, status: done ? done.status : 'approved' });
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
