require('dotenv').config();
const { Client, GatewayIntentBits, EmbedBuilder, PermissionsBitField, REST, Routes, SlashCommandBuilder } = require('discord.js');

const TOKEN = process.env.DISCORD_TOKEN;
const GUILD_ID = process.env.GUILD_ID;
const ADMIN_ID = process.env.ADMIN_ID;

module.exports = function start(db, app) {
  if (!TOKEN) {
    console.warn('[!] DISCORD_TOKEN no configurado en el .env -> bot no iniciado');
    return null;
  }

  process.on('uncaughtException', (e) => {
    console.error('[!] Uncaught Exception (bot sigue corriendo):', e && e.message ? e.message : e);
  });
  process.on('unhandledRejection', (r) => {
    console.warn('[!] Unhandled Rejection (bot sigue corriendo):', r && r.message ? r.message : r);
  });

  const { run, getOne, getAll, createLicense, listActivePins, revokePin } = db;

  const client = new Client({
    intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMembers, GatewayIntentBits.GuildMessages]
  });

  const isAdmin = (user, member) => {
    if (ADMIN_ID && String(user.id) === String(ADMIN_ID)) return true;
    if (GUILD_ID) {
      const cfg = getOne('SELECT admin_role FROM guild_config WHERE guild_id = ?', [GUILD_ID]);
      if (cfg && cfg.admin_role && cfg.admin_role !== GUILD_ID && member && member.roles && member.roles.cache.has(cfg.admin_role)) return true;
    }
    if (member && member.permissions && member.permissions.has(PermissionsBitField.Flags.Administrator)) return true;
    return false;
  };

  const commands = [
    new SlashCommandBuilder()
      .setName('license')
      .setDescription('Gestionar licencias')
      .addSubcommand(s => s
        .setName('generate')
        .setDescription('Genera una nueva license key')
        .addIntegerOption(o => o.setName('dias').setDescription('Dias de duracion').setRequired(true)))
      .addSubcommand(s => s
        .setName('info')
        .setDescription('Informacion de una key')
        .addStringOption(o => o.setName('key').setDescription('License key').setRequired(true)))
      .addSubcommand(s => s
        .setName('list')
        .setDescription('Lista licencias (ultimas 25)')
        .addStringOption(o => o.setName('filtro').setDescription('unused / active / banned').setRequired(false).addChoices(
          { name: 'unused', value: 'unused' },
          { name: 'active', value: 'active' },
          { name: 'banned', value: 'banned' }
        )))
      .addSubcommand(s => s
        .setName('reset-hwid')
        .setDescription('Resetea el HWID ligado a una licencia')
        .addStringOption(o => o.setName('key').setDescription('License key').setRequired(true))),

    new SlashCommandBuilder()
      .setName('bankey')
      .setDescription('Banea una license key')
      .addStringOption(o => o.setName('key').setDescription('License key').setRequired(true))
      .addStringOption(o => o.setName('razon').setDescription('Motivo del ban').setRequired(false)),

    new SlashCommandBuilder()
      .setName('unbankey')
      .setDescription('Desbanea una license key')
      .addStringOption(o => o.setName('key').setDescription('License key').setRequired(true)),

    new SlashCommandBuilder()
      .setName('setscanlogs')
      .setDescription('Configura el canal donde llegan los resultados de escaneos')
      .addChannelOption(o => o.setName('canal').setDescription('Canal de texto').setRequired(true)),

    new SlashCommandBuilder()
      .setName('setadminrole')
      .setDescription('Configura el rol de admin para comandos de licencias')
      .addRoleOption(o => o.setName('rol').setDescription('Rol de admin').setRequired(true)),

    new SlashCommandBuilder()
      .setName('setpinchannel')
      .setDescription('[Admin] Canal privado donde tambien llegan los PINs de escaneo (ademas de tu MD)')
      .addChannelOption(o => o.setName('canal').setDescription('Canal de texto (solo admins)').setRequired(true)),

    new SlashCommandBuilder()
      .setName('scanpin')
      .setDescription('[Admin] Ver o anular PINs de escaneo')
      .addSubcommand(s => s
        .setName('list')
        .setDescription('Lista los PINs activos (sin usar o verificados)'))
      .addSubcommand(s => s
        .setName('revoke')
        .setDescription('Anula un PIN para que no se pueda usar')
        .addStringOption(o => o.setName('pin').setDescription('PIN de 8 caracteres').setRequired(true))
        .addStringOption(o => o.setName('razon').setDescription('Motivo (opcional)').setRequired(false)))
  ];

  client.once('ready', async () => {
    console.log(`[+] Bot conectado como ${client.user.tag}`);
    const rest = new REST({ version: '10' }).setToken(TOKEN);
    try {
      console.log(`[i] Registrando ${commands.length} slash commands...`);
      console.log(`[i] Lista de comandos a registrar:`);
      commands.forEach((c, i) => console.log(`    ${i + 1}. /${c.name}`));

      if (GUILD_ID) {
        await rest.put(Routes.applicationGuildCommands(client.user.id, GUILD_ID), { body: commands });
        console.log(`[+] Slash commands registrados en el guild ${GUILD_ID} (inmediato)`);
        console.log(`[!] Si no aparecen, haz esto en Discord: Ajustes del server -> Integraciones -> Bots y Apps -> tu bot -> Comandos. O reinicia Discord (Ctrl+R).`);
      } else {
        await rest.put(Routes.applicationCommands(client.user.id), { body: commands });
        console.log('[!] GUILD_ID no configurado: registrados GLOBALMENTE (puede tardar ~1 hora en propagarse). Añade GUILD_ID al .env para que sea instantáneo.');
      }
    } catch (e) {
      console.error('[✗] Error registrando slash commands:');
      console.error(e && e.message ? e.message : e);
      if (e && e.stack) console.error(e.stack);
    }
    startScanReporter();
  });

  client.on('error', (e) => console.error('[!] Bot error:', e.message));
  client.on('warn', (w) => console.warn('[!] Bot warn:', w));

  const formatDate = (iso) => {
    if (!iso) return '—';
    try { return new Date(iso).toLocaleString('es-ES'); } catch { return iso; }
  };

  client.on('interactionCreate', async (i) => {
    if (!i.isChatInputCommand()) return;
    try {
      if (!GUILD_ID) {
        try { await i.reply({ content: '❌ Falta GUILD_ID en el .env del servidor', ephemeral: true }); } catch {}
        return;
      }

      // Contestar YA a Discord (3s limite) con deferReply ANTES de cualquier check pesado
      const ephemeralCmds = new Set(['setscanlogs', 'setadminrole', 'setpinchannel', 'scanpin']);
      const cmd = i.commandName;
      let sub = null;
      try { if (i.options && typeof i.options.getSubcommand === 'function') sub = i.options.getSubcommand(); } catch {}

      try { await i.deferReply({ ephemeral: ephemeralCmds.has(cmd) }); } catch (de) {
        console.warn('[bot] deferReply falló (continuando):', de && de.message ? de.message : de);
      }

      const member = i.member;
      const adminCmds = new Set(['setscanlogs', 'setadminrole', 'setpinchannel', 'license', 'bankey', 'unbankey', 'scanpin']);
      if (adminCmds.has(cmd)) {
        if (!isAdmin(i.user, member)) {
          safeReply(i, '❌ No tienes permisos para usar este comando', true);
          return;
        }
      }

      // La llamada a deferReply() ya se hizo ARRIBA, no repetirla aqui
      if (cmd === 'setscanlogs') {
        const ch = i.options.getChannel('canal');
        const existing = getOne('SELECT guild_id FROM guild_config WHERE guild_id = ?', [i.guildId]);
        if (existing) {
          run('UPDATE guild_config SET scan_logs_channel = ?, set_by = ?, updated_at = CURRENT_TIMESTAMP WHERE guild_id = ?',
            [ch.id, i.user.id, i.guildId]);
        } else {
          run('INSERT INTO guild_config (guild_id, scan_logs_channel, set_by) VALUES (?, ?, ?)', [i.guildId, ch.id, i.user.id]);
        }
        safeReply(i, `✅ Canal de logs de escaneos configurado en ${ch.toString()}`, true);
        return;
      }

      if (cmd === 'setadminrole') {
        const role = i.options.getRole('rol');
        // @everyone tiene el mismo id que el servidor: daria permisos de admin a todos los miembros
        if (!role || role.id === i.guildId) { safeReply(i, '❌ No puedes usar @everyone como rol de admin. Elige un rol propio.', true); return; }
        const existing = getOne('SELECT guild_id FROM guild_config WHERE guild_id = ?', [i.guildId]);
        if (existing) {
          run('UPDATE guild_config SET admin_role = ?, set_by = ?, updated_at = CURRENT_TIMESTAMP WHERE guild_id = ?',
            [role.id, i.user.id, i.guildId]);
        } else {
          run('INSERT INTO guild_config (guild_id, admin_role, set_by) VALUES (?, ?, ?)', [i.guildId, role.id, i.user.id]);
        }
        safeReply(i, `✅ Rol de admin: ${role.toString()}`, true);
        return;
      }

      if (cmd === 'setpinchannel') {
        const ch = i.options.getChannel('canal');
        const existing = getOne('SELECT guild_id FROM guild_config WHERE guild_id = ?', [i.guildId]);
        if (existing) {
          run('UPDATE guild_config SET pin_channel = ?, set_by = ?, updated_at = CURRENT_TIMESTAMP WHERE guild_id = ?',
            [ch.id, i.user.id, i.guildId]);
        } else {
          run('INSERT INTO guild_config (guild_id, pin_channel, set_by) VALUES (?, ?, ?)', [i.guildId, ch.id, i.user.id]);
        }
        safeReply(i, `✅ Canal de solicitudes PIN configurado en ${ch.toString()}`, true);
        return;
      }

      if (cmd === 'scanpin') {
        if (sub === 'list') {
          const rows = listActivePins(25);
          if (!rows.length) { safeReply(i, '🟢 No hay PINs activos.', true); return; }
          const lines = rows.map(r => {
            const state = r.status === 'verified' ? '✅ verificado' : '⏳ sin usar';
            const who = r.username ? ` • **${r.username}**` : '';
            const exp = r.status === 'verified' ? r.token_expires_at : r.expires_at;
            return `• \`${r.pin}\` ${state}${who} • ${r.ip || 'IP ?'} • caduca ${discordTime(exp)}`;
          });
          const emb = new EmbedBuilder()
            .setTitle(`🔐 PINs activos (${rows.length})`)
            .setColor(0xFFFFFF)
            .setDescription(lines.join('\n').slice(0, 4000))
            .setFooter({ text: '/scanpin revoke <PIN> para anular uno' });
          safeReply(i, null, true, emb);
          return;
        }
        if (sub === 'revoke') {
          const pin = (i.options.getString('pin') || '').toUpperCase().trim();
          const reason = i.options.getString('razon') || null;
          if (!/^[A-Z0-9]{8}$/.test(pin)) { safeReply(i, '❌ Formato de PIN invalido', true); return; }
          const upd = revokePin(pin, String(i.user.id), reason);
          if (!upd) { safeReply(i, `❌ El PIN \`${pin}\` no existe o ya no esta activo`, true); return; }
          safeReply(i, `🚫 PIN \`${pin}\` anulado${reason ? ` — motivo: ${reason}` : ''}`, true);
          return;
        }
        safeReply(i, '❓ Subcomando desconocido. Usa: list o revoke.', true);
        return;
      }

      if (cmd === 'bankey') {
        const key = (i.options.getString('key') || '').trim().toUpperCase();
        const reason = i.options.getString('razon') || 'Sin motivo';
        const lic = getOne('SELECT * FROM licenses WHERE key = ?', [key]);
        if (!lic) { safeReply(i, '❌ Key no encontrada', true); return; }
        if (lic.banned == 1) { safeReply(i, '⚠️ Esa key ya está baneada', true); return; }
        run('UPDATE licenses SET banned = 1, ban_reason = ? WHERE key = ?', [reason, key]);
        safeReply(i, `🔨 **KEY BANEADA**\n\`${key}\`\nMotivo: ${reason}`, false);
        return;
      }

      if (cmd === 'unbankey') {
        const key = (i.options.getString('key') || '').trim().toUpperCase();
        const lic = getOne('SELECT * FROM licenses WHERE key = ?', [key]);
        if (!lic) { safeReply(i, '❌ Key no encontrada', true); return; }
        run('UPDATE licenses SET banned = 0, ban_reason = NULL WHERE key = ?', [null, key]);
        safeReply(i, `✅ **KEY DESBANEADA**\n\`${key}\``, false);
        return;
      }

      if (cmd === 'license') {
        if (sub === 'generate') {
          const days = i.options.getInteger('dias');
          if (days <= 0 || days > 365 * 10) { safeReply(i, '❌ Dias no validos (max 10 años)', true); return; }
          const lic = createLicense(days, i.user.id);
          try {
            const emb = new EmbedBuilder()
              .setTitle('✅ Nueva licencia generada')
              .setColor(0x3DDC84)
              .addFields(
                { name: 'Key', value: `\`\`\`${lic.key}\`\`\``, inline: false },
                { name: 'Duracion', value: `${days} día(s)`, inline: true },
                { name: 'Estado', value: 'Unused', inline: true },
                { name: 'Creada por', value: `<@${i.user.id}>`, inline: true }
              );
            safeReply(i, null, false, emb);
          } catch (e) { safeReply(i, `Error: ${e.message}`, true); }
          return;
        }
        if (sub === 'info') {
          const key = (i.options.getString('key') || '').trim().toUpperCase();
          const row = getOne(`SELECT l.*, u.username as user_name FROM licenses l LEFT JOIN users u ON u.id = l.user_id WHERE l.key = ?`, [key]);
          if (!row) { safeReply(i, '❌ Key no encontrada', true); return; }
          const emb = new EmbedBuilder().setTitle('🔑 Informacion de licencia').setColor(0x5B9DFF);
          let status = (row.status || '').toUpperCase();
          if (row.banned == 1) status = '🔨 BANEADA';
          emb.addFields(
            { name: 'Key', value: `\`${row.key}\`` },
            { name: 'Duracion', value: `${row.days} días`, inline: true },
            { name: 'Estado', value: status, inline: true },
            { name: 'Creada', value: formatDate(row.created_at), inline: true },
            { name: 'Activada', value: formatDate(row.activated_at), inline: true },
            { name: 'Expira', value: formatDate(row.expires_at), inline: true },
            { name: 'Usuario', value: row.user_name ? row.user_name : '—', inline: true },
            { name: 'HWID', value: row.hwid ? '✅ Ligado' : '—', inline: true }
          );
          if (row.banned == 1 && row.ban_reason) emb.addFields({ name: 'Motivo ban', value: row.ban_reason });
          safeReply(i, null, false, emb);
          return;
        }
        if (sub === 'reset-hwid') {
          const key = (i.options.getString('key') || '').trim().toUpperCase();
          const lic = getOne('SELECT * FROM licenses WHERE key = ?', [key]);
          if (!lic) { safeReply(i, '❌ Key no encontrada', true); return; }
          run('UPDATE licenses SET hwid = NULL WHERE key = ?', [key]);
          safeReply(i, `♻️ HWID reseteado para \`${key}\``, false);
          return;
        }
        if (sub === 'list') {
          const filter = i.options.getString('filtro');
          let sql = 'SELECT l.*, u.username as user_name FROM licenses l LEFT JOIN users u ON u.id = l.user_id';
          const args = [];
          if (filter === 'banned') sql += ' WHERE l.banned = 1';
          else if (filter) { sql += ' WHERE l.status = ? AND l.banned = 0'; args.push(filter); }
          else sql += ' WHERE l.banned = 0';
          sql += ' ORDER BY l.id DESC LIMIT 25';
          const rows = getAll(sql, args);
          if (!rows.length) { safeReply(i, '⚠️ No hay licencias para mostrar', true); return; }
          const lines = rows.map(r => {
            const stat = (r.banned ? '🔨' : (r.status === 'unused' ? '🆕' : (r.status === 'active' ? '✅' : '❔')));
            const u = r.user_name ? `@${r.user_name}` : '—';
            const exp = r.expires_at ? ` • exp ${formatDate(r.expires_at).split(',')[0]}` : '';
            return `${stat} \`${r.key}\` ${u}${exp}`;
          });
          const emb = new EmbedBuilder()
            .setTitle(`📋 Licencias (${filter || 'todas'})`)
            .setColor(0xFFFFFF)
            .setDescription(lines.join('\n'));
          safeReply(i, null, false, emb);
          return;
        }
        safeReply(i, '❓ Subcomando desconocido', true);
      }
    } catch (bigErr) {
      console.error('[!] interactionCreate handler error:', bigErr && bigErr.message ? bigErr.message : bigErr);
      try { await i.editReply({ content: '❌ Error: ' + (bigErr && bigErr.message ? bigErr.message : bigErr) }); }
      catch { try { await i.followUp({ content: '❌ Error interno', ephemeral: true }); } catch {} }
    }
  });

  async function safeReply(i, text, ephemeral, embed) {
    try {
      const payload = {};
      if (text != null) payload.content = text;
      if (embed) payload.embeds = [embed];
      payload.ephemeral = !!ephemeral;
      try { await i.editReply(payload); return; } catch {}
      try { await i.followUp(payload); return; } catch {}
      try { await i.reply(payload); return; } catch {}
    } catch {}
  }

  function verdictColor(v) {
    if (v === 'FLAGGED') return 0xFF3B3B;
    if (v === 'SUSPICIOUS') return 0xFFB020;
    if (v === 'CLEAN') return 0x3DDC84;
    return 0x5B9DFF;
  }

  function buildScanEmbed(scan) {
    const e = new EmbedBuilder()
      .setTitle('🔍 Resultado de escaneo')
      .setColor(verdictColor(scan.verdict))
      .setTimestamp(scan.created_at)
      .setFooter({ text: `Scan ID #${scan.id}` })
      .addFields(
        { name: 'Usuario', value: `@${scan.username}`, inline: true },
        { name: 'User ID', value: scan.user_id != null ? String(scan.user_id) : 'PIN', inline: true },
        { name: 'HWID', value: scan.hwid ? `\`${scan.hwid.slice(0, 20)}...\`` : '—', inline: true },
        { name: 'Veredicto', value: `**${scan.verdict || 'N/A'}**`, inline: true },
        { name: 'High', value: String(scan.total_high || 0), inline: true },
        { name: 'Medium', value: String(scan.total_med || 0), inline: true },
        { name: 'Low', value: String(scan.total_low || 0), inline: true },
        { name: 'Duracion', value: scan.duration_sec ? `${scan.duration_sec}s` : '—', inline: true }
      );
    if (scan.started_at) e.addFields({ name: 'Inicio', value: formatDate(scan.started_at), inline: true });
    if (scan.ended_at) e.addFields({ name: 'Fin', value: formatDate(scan.ended_at), inline: true });
    if (scan.findings_json) {
      try {
        const list = JSON.parse(scan.findings_json);
        if (Array.isArray(list) && list.length > 0) {
          const top = list.slice(0, 10).map(f => {
            const sev = f.Sev >= 3 ? '🔴' : f.Sev === 2 ? '🟠' : '🔵';
            return `${sev} **${(f.Match || '').slice(0, 60)}** — ${(f.Source || '').slice(0, 80)}`;
          });
          e.addFields({ name: `Hallazgos (${list.length})`, value: top.join('\n').slice(0, 1023) });
        }
      } catch {}
    }
    return e;
  }

  async function reportPendingScans() {
    const pending = getAll('SELECT * FROM scans WHERE reported_to_discord = 0 ORDER BY id ASC', []);
    if (!pending.length) return;
    const cfg = GUILD_ID ? getOne('SELECT scan_logs_channel FROM guild_config WHERE guild_id = ?', [GUILD_ID]) : null;
    if (!cfg || !cfg.scan_logs_channel) return;
    const channel = client.channels.cache.get(cfg.scan_logs_channel);
    if (!channel) return;
    for (const scan of pending) {
      try {
        await channel.send({ embeds: [buildScanEmbed(scan)] });
      } catch (e) { console.error('Send scan log error:', e.message); }
      run('UPDATE scans SET reported_to_discord = 1 WHERE id = ?', [scan.id]);
    }
  }

  function startScanReporter() {
    setInterval(() => {
      reportPendingScans().catch(err => console.error(err));
    }, 5000);
    console.log('[+] Scan report loop iniciado (c/5s)');
  }

  const discordTime = (iso) => {
    const t = iso ? Math.floor(new Date(iso).getTime() / 1000) : NaN;
    return isNaN(t) ? '—' : `<t:${t}:R>`;
  };

  // Destinos de los PINs: MD al ADMIN_ID y, si esta configurado, el canal de PINs.
  async function pinTargets() {
    const out = [];
    if (ADMIN_ID) {
      try { out.push(await client.users.fetch(ADMIN_ID)); } catch (e) { console.error('[pin] No se pudo obtener ADMIN_ID:', e.message); }
    }
    const cfg = GUILD_ID ? getOne('SELECT pin_channel FROM guild_config WHERE guild_id = ?', [GUILD_ID]) : null;
    if (cfg && cfg.pin_channel) {
      try { out.push(await client.channels.fetch(cfg.pin_channel)); } catch (e) { console.error('[pin] Canal de PINs no accesible:', e.message); }
    }
    return out;
  }

  async function sendToTargets(payload) {
    let sent = 0;
    for (const t of await pinTargets()) {
      try { await t.send(Object.assign({ allowedMentions: { parse: [] } }, payload)); sent++; }
      catch (e) { console.error('[pin] Envio fallido:', e.message); }
    }
    return sent > 0;
  }

  async function sendPinToDiscord(row) {
    if (!client.isReady()) return false;
    const emb = new EmbedBuilder()
      .setTitle('🔐 Nueva solicitud de escaneo')
      .setColor(0xFFFFFF)
      .setDescription(`# \`${row.pin}\`\nDale este PIN a la persona que vas a escanear para que lo escriba en Noxxer.`)
      .addFields(
        { name: 'Nombre', value: row.username || '—', inline: true },
        { name: 'IP', value: row.ip || '—', inline: true },
        { name: 'Caduca', value: discordTime(row.expires_at), inline: true },
        { name: 'HWID', value: row.hwid ? `\`${String(row.hwid).slice(0, 40)}\`` : '—', inline: false }
      )
      .setFooter({ text: 'Un solo uso • 5 intentos • /scanpin revoke <PIN> para anularlo' })
      .setTimestamp();
    return sendToTargets({ embeds: [emb] });
  }

  function notifyPinEvent(row, kind) {
    if (!client.isReady() || !row) return;
    const verified = kind === 'verified';
    const emb = new EmbedBuilder()
      .setColor(verified ? 0x3DDC84 : 0xFF3B3B)
      .setDescription(verified
        ? `✅ PIN \`${row.pin}\` verificado${row.username ? ` por **${row.username}**` : ''}. El escaneo puede empezar.`
        : `🔒 PIN \`${row.pin}\` bloqueado por demasiados intentos fallidos${row.ip ? ` (IP ${row.ip})` : ''}.`)
      .setTimestamp();
    sendToTargets({ embeds: [emb] }).catch(() => {});
  }

  if (app && app.locals) {
    app.locals.sendPinToDiscord = sendPinToDiscord;
    app.locals.notifyPinEvent = notifyPinEvent;
  }

  client.login(TOKEN);
  return client;
};
