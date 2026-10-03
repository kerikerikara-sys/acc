// ---------------------------------------------------------------------------
//  Panel de administracion: login seguro + gestion del enlace de descarga.
//
//  La contraseña NUNCA se guarda en texto plano: se almacena su hash bcrypt
//  en la tabla `settings`. El primer admin se siembra desde variables de
//  entorno (ADMIN_USER / ADMIN_PASSWORD) la primera vez que arranca el
//  servidor; despues puedes cambiar la contraseña desde el propio panel.
// ---------------------------------------------------------------------------
const crypto = require('crypto');
const path = require('path');

const TOKEN_TTL_MS = 8 * 60 * 60 * 1000;      // 8 horas
const LOGIN_WINDOW_MS = 15 * 60 * 1000;        // ventana de rate limit
const LOGIN_MAX_ATTEMPTS = 8;                  // intentos fallidos por ventana

function mountAdmin(app, db) {
  const { getSetting, setSetting, hashPassword, verifyPassword } = db;

  // --- Credenciales del admin desde el entorno ---
  // El .env manda: si defines ADMIN_USER y ADMIN_PASSWORD, esos son SIEMPRE
  // tus datos de acceso al panel (se re-aplican en cada arranque). Asi evitamos
  // confusiones si cambias la contraseña en el .env. Para gestionar la
  // contraseña solo desde el panel, quita ADMIN_PASSWORD del .env tras el
  // primer arranque.
  const envUser = (process.env.ADMIN_USER || '').trim();
  const envPass = process.env.ADMIN_PASSWORD || '';
  if (envUser && envPass) {
    setSetting('admin_user', envUser);
    setSetting('admin_pass_hash', hashPassword(envPass));
    console.log(`[admin] Credenciales de admin aplicadas desde el entorno: "${envUser}"`);
  } else if (!getSetting('admin_user')) {
    console.warn('[admin] Admin no configurado. Define ADMIN_USER y ADMIN_PASSWORD en el .env y reinicia.');
  }

  // --- Sesiones en memoria (se reinician al reiniciar el servidor) ---
  const tokens = new Map();   // token -> expiry (ms)
  const attempts = new Map(); // ip -> { count, resetAt }

  function issueToken() {
    const t = crypto.randomBytes(32).toString('hex');
    tokens.set(t, Date.now() + TOKEN_TTL_MS);
    return t;
  }
  function tokenValid(t) {
    if (!t) return false;
    const exp = tokens.get(t);
    if (!exp) return false;
    if (Date.now() > exp) { tokens.delete(t); return false; }
    return true;
  }
  function readToken(req) {
    const h = req.headers['authorization'] || '';
    if (h.startsWith('Bearer ')) return h.slice(7).trim();
    return (req.body && req.body.token) || null;
  }
  function clientIp(req) {
    const fwd = req.headers['x-forwarded-for'] || req.headers['x-real-ip'];
    if (fwd) return String(fwd).split(',')[0].trim();
    return (req.socket && req.socket.remoteAddress) || 'unknown';
  }
  function requireAdmin(req, res, next) {
    if (!tokenValid(readToken(req))) return res.status(401).json({ ok: false, error: 'No autorizado' });
    next();
  }

  function sanitizeUrl(raw) {
    if (typeof raw !== 'string') return null;
    const s = raw.trim();
    if (!s) return '';                    // vacio = sin enlace configurado
    if (s.length > 2048) return null;
    let u;
    try { u = new URL(s); } catch { return null; }
    if (u.protocol !== 'http:' && u.protocol !== 'https:') return null;
    return u.toString();
  }

  // --- Login ---
  app.post('/api/admin/login', (req, res) => {
    const ip = clientIp(req);
    const now = Date.now();
    let a = attempts.get(ip);
    if (a && now > a.resetAt) { a = null; attempts.delete(ip); }
    if (a && a.count >= LOGIN_MAX_ATTEMPTS) {
      const mins = Math.ceil((a.resetAt - now) / 60000);
      return res.status(429).json({ ok: false, error: `Demasiados intentos. Espera ${mins} min.` });
    }

    const { username, password } = req.body || {};
    const user = getSetting('admin_user');
    const hash = getSetting('admin_pass_hash');
    const ok = user && hash && typeof username === 'string' && typeof password === 'string'
      && username.trim() === user && verifyPassword(password, hash);

    if (!ok) {
      const rec = a || { count: 0, resetAt: now + LOGIN_WINDOW_MS };
      rec.count += 1;
      attempts.set(ip, rec);
      return res.status(401).json({ ok: false, error: 'Usuario o contraseña incorrectos' });
    }

    attempts.delete(ip);
    return res.json({ ok: true, token: issueToken(), username: user });
  });

  app.post('/api/admin/logout', (req, res) => {
    const t = readToken(req);
    if (t) tokens.delete(t);
    return res.json({ ok: true });
  });

  app.get('/api/admin/me', requireAdmin, (req, res) => {
    return res.json({ ok: true, username: getSetting('admin_user') });
  });

  // --- Enlace de descarga ---
  // Publico: lo usa la web para el boton "Descargar".
  app.get('/api/download', (req, res) => {
    return res.json({ ok: true, url: getSetting('download_url', '') || '' });
  });

  // Admin: ver / cambiar el enlace.
  app.get('/api/admin/download', requireAdmin, (req, res) => {
    return res.json({ ok: true, url: getSetting('download_url', '') || '' });
  });

  app.post('/api/admin/download', requireAdmin, (req, res) => {
    const url = sanitizeUrl((req.body || {}).url);
    if (url === null) return res.status(400).json({ ok: false, error: 'URL invalida (usa http:// o https://)' });
    setSetting('download_url', url);
    return res.json({ ok: true, url });
  });

  // --- Cambio de contraseña ---
  app.post('/api/admin/password', requireAdmin, (req, res) => {
    const { currentPassword, newPassword } = req.body || {};
    const hash = getSetting('admin_pass_hash');
    if (!hash || typeof currentPassword !== 'string' || !verifyPassword(currentPassword, hash))
      return res.status(401).json({ ok: false, error: 'La contraseña actual no es correcta' });
    if (typeof newPassword !== 'string' || newPassword.length < 8)
      return res.status(400).json({ ok: false, error: 'La nueva contraseña debe tener al menos 8 caracteres' });
    setSetting('admin_pass_hash', hashPassword(newPassword));
    // Invalida todas las sesiones abiertas tras el cambio.
    tokens.clear();
    return res.json({ ok: true });
  });

  // --- Pagina del panel ---
  app.get('/admin', (req, res) => {
    res.sendFile(path.join(__dirname, 'admin.html'));
  });
}

module.exports = mountAdmin;
