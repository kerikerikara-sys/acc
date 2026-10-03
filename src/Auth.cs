using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Noxxer
{
    public class AuthSession
    {
        public string Username;
        public string LicenseKey;
        public string Hwid;
        public int RemainingDays;
        public string ExpiresAt;
    }

    public class PinSession
    {
        public string RequestId;   // identifies the request; the PIN itself only reaches Discord
        public string Token;       // single-use scan token returned after the PIN is verified
        public string Hwid;
        public string Username;
    }

    public class PinResult
    {
        public bool Ok;
        public string Error;
        public string Code;        // wrong / expired / locked / used / revoked / hwid / not_found
        public int AttemptsLeft = -1;
        public int ExpiresIn;
        public PinSession Session;
    }

    public class AuthResult
    {
        public bool Ok;
        public string Error;
        public AuthSession Session;
    }

    public static class Auth
    {
        static readonly object lk = new object();
        static string cachedHwid;

        static string _cachedServerUrl = null;
        static string _cachedServerUrlSource = null;

        public static string ServerUrl
        {
            get
            {
                if (_cachedServerUrl != null) return _cachedServerUrl;
                try
                {
                    string f = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "noxxer_server.txt");
                    if (File.Exists(f))
                    {
                        string[] lines = File.ReadAllLines(f);
                        foreach (string raw in lines)
                        {
                            string s = (raw ?? "").Trim();
                            if (s.Length == 0) continue;
                            if (s.StartsWith("#") || s.StartsWith("//")) continue;
                            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            {
                                string clean = s.TrimEnd('/');
                                if (Uri.IsWellFormedUriString(clean, UriKind.Absolute))
                                {
                                    _cachedServerUrl = clean;
                                    _cachedServerUrlSource = f;
                                    return clean;
                                }
                            }
                        }
                    }
                }
                catch { }
                _cachedServerUrl = "http://localhost:3000";
                _cachedServerUrlSource = "(default - noxxer_server.txt not found or invalid)";
                return _cachedServerUrl;
            }
        }

        public static string ServerUrlDebugInfo
        {
            get
            {
                string u = ServerUrl;
                return "URL: " + u + " | Config: " + (_cachedServerUrlSource ?? "unknown");
            }
        }

        public static string GetHwid()
        {
            lock (lk)
            {
                if (!string.IsNullOrEmpty(cachedHwid)) return cachedHwid;
                try
                {
                    StringBuilder sb = new StringBuilder();
                    using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                    using (ManagementObjectCollection col = s.Get())
                        foreach (ManagementBaseObject o in col) { sb.Append((o["ProcessorId"] ?? "").ToString()); break; }
                    using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                    using (ManagementObjectCollection col = s.Get())
                        foreach (ManagementBaseObject o in col) { sb.Append("|"); sb.Append((o["UUID"] ?? "").ToString()); break; }
                    using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='C:'"))
                    using (ManagementObjectCollection col = s.Get())
                        foreach (ManagementBaseObject o in col) { sb.Append("|"); sb.Append((o["VolumeSerialNumber"] ?? "").ToString()); break; }
                    using (SHA1 sha = SHA1.Create())
                    {
                        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                        StringBuilder hex = new StringBuilder();
                        foreach (byte b in hash) hex.Append(b.ToString("X2"));
                        cachedHwid = hex.ToString();
                    }
                }
                catch { cachedHwid = "UNKNOWN-" + Environment.UserName; }
                return cachedHwid;
            }
        }

        // ---------- tiny JSON helpers (no refs needed) ----------
        static string JStr(string s)
        {
            if (s == null) return "null";
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        static string ExtractJson(string json, string key)
        {
            string k = "\"" + key + "\"";
            int i = json.IndexOf(k, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i += k.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == '\r' || json[i] == '\n' || json[i] == ':')) i++;
            if (i >= json.Length) return null;
            if (json[i] == '"')
            {
                i++;
                StringBuilder sb = new StringBuilder();
                while (i < json.Length && json[i] != '"')
                {
                    if (json[i] == '\\' && i + 1 < json.Length)
                    {
                        i++;
                        switch (json[i])
                        {
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'u':
                                if (i + 4 < json.Length)
                                {
                                    try { sb.Append((char)Convert.ToInt32(json.Substring(i + 1, 4), 16)); i += 4; } catch { }
                                }
                                break;
                            default: sb.Append(json[i]); break;
                        }
                    }
                    else sb.Append(json[i]);
                    i++;
                }
                return sb.ToString();
            }
            int start = i;
            while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']' && json[i] != ' ' && json[i] != '\t' && json[i] != '\r' && json[i] != '\n') i++;
            return json.Substring(start, i - start);
        }

        static bool ExtractBool(string json, string key)
        {
            string v = ExtractJson(json, key);
            if (v == null) return false;
            return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        static int ExtractInt(string json, string key, int def = 0)
        {
            string v = ExtractJson(json, key);
            int r; if (int.TryParse(v, out r)) return r;
            return def;
        }

        static string Request(string path, string method, Dictionary<string, object> data)
        {
            string baseUrl = ServerUrl;
            string url = baseUrl + path;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.ContentType = "application/json";
            req.Timeout = 15000;
            req.UserAgent = "ACNoxxer/1.0";
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            if (data != null)
            {
                StringBuilder sb = new StringBuilder("{");
                bool first = true;
                foreach (var kv in data)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append(JStr(kv.Key)).Append(":");
                    if (kv.Value == null) sb.Append("null");
                    else if (kv.Value is bool) sb.Append(((bool)kv.Value) ? "true" : "false");
                    else if (kv.Value is int || kv.Value is long || kv.Value is double || kv.Value is float) sb.Append(kv.Value.ToString());
                    else sb.Append(JStr(kv.Value.ToString()));
                }
                sb.Append("}");
                byte[] buf = Encoding.UTF8.GetBytes(sb.ToString());
                req.ContentLength = buf.Length;
                using (Stream s = req.GetRequestStream()) s.Write(buf, 0, buf.Length);
            }
            try
            {
                using (WebResponse resp = req.GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                    return r.ReadToEnd();
            }
            catch (WebException wex)
            {
                if (wex.Response != null)
                    using (Stream s = wex.Response.GetResponseStream())
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                        return r.ReadToEnd();
                throw new Exception(wex.Message + " [Server: " + baseUrl + "]");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + " [Server: " + baseUrl + "]");
            }
        }

        static AuthResult ParseAuth(string json)
        {
            AuthResult r = new AuthResult();
            r.Ok = ExtractBool(json, "ok");
            r.Error = ExtractJson(json, "error");
            if (!r.Ok) return r;
            r.Session = new AuthSession();
            r.Session.Username = ExtractJson(json, "username");
            r.Session.LicenseKey = ExtractJson(json, "license");
            r.Session.RemainingDays = ExtractInt(json, "remainingDays");
            r.Session.ExpiresAt = ExtractJson(json, "expiresAt");
            r.Session.Hwid = GetHwid();
            if (string.IsNullOrEmpty(r.Session.Username)) r.Session.Username = ExtractJson(json, "user_username");
            return r;
        }

        public static AuthResult SignUp(string username, string password, string licenseKey)
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["username"] = username;
                d["password"] = password;
                d["licenseKey"] = licenseKey;
                return ParseAuth(Request("/api/auth/signup", "POST", d));
            }
            catch (Exception ex)
            {
                return new AuthResult { Ok = false, Error = "Error de conexión: " + ex.Message };
            }
        }

        public static AuthResult Login(string username, string password, string licenseKey)
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["username"] = username;
                d["password"] = password;
                d["licenseKey"] = licenseKey;
                d["hwid"] = GetHwid();
                return ParseAuth(Request("/api/auth/login", "POST", d));
            }
            catch (Exception ex)
            {
                return new AuthResult { Ok = false, Error = "Error de conexión: " + ex.Message };
            }
        }

        public static AuthResult Verify(AuthSession s)
        {
            if (s == null) return new AuthResult { Ok = false, Error = "Sin sesión" };
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["username"] = s.Username;
                d["licenseKey"] = s.LicenseKey;
                d["hwid"] = s.Hwid;
                string json = Request("/api/auth/verify", "POST", d);
                AuthResult r = new AuthResult();
                r.Ok = ExtractBool(json, "ok");
                r.Error = ExtractJson(json, "error");
                if (r.Ok) { s.RemainingDays = ExtractInt(json, "remainingDays"); s.ExpiresAt = ExtractJson(json, "expiresAt"); r.Session = s; }
                return r;
            }
            catch (Exception ex) { return new AuthResult { Ok = false, Error = "Error de conexión: " + ex.Message }; }
        }

        // Uploads the findings with the single-use token obtained from VerifyPin.
        public static AuthResult SubmitScan(PinSession pin, Engine eng)
        {
            if (pin == null || string.IsNullOrEmpty(pin.Token) || eng == null)
                return new AuthResult { Ok = false, Error = "Sin autorización de escaneo" };
            try
            {
                List<Finding> findings = eng.Snapshot();
                List<object> items = new List<object>();
                foreach (Finding f in findings)
                {
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d["Sev"] = f.Sev; d["Type"] = f.Type; d["Source"] = f.Source;
                    d["Match"] = f.Match; d["Detail"] = f.Detail; d["Time"] = f.Time.ToString("o");
                    items.Add(d);
                }
                Dictionary<string, object> body = new Dictionary<string, object>();
                body["token"] = pin.Token;
                body["hwid"] = string.IsNullOrEmpty(pin.Hwid) ? GetHwid() : pin.Hwid;
                body["startedAt"] = eng.Started.ToString("o");
                body["endedAt"] = eng.Ended.ToString("o");
                body["durationSec"] = (int)(eng.Ended - eng.Started).TotalSeconds;
                body["totalHigh"] = eng.TotalHigh;
                body["totalMed"] = eng.TotalMed;
                body["totalLow"] = eng.TotalLow;
                int v = eng.Verdict();
                body["verdict"] = v == 2 ? "FLAGGED" : v == 1 ? "SUSPICIOUS" : "CLEAN";
                body["findings"] = items;
                string json = Request("/api/scans/submit", "POST", body);
                AuthResult r = new AuthResult();
                r.Ok = ExtractBool(json, "ok");
                r.Error = ExtractJson(json, "error");
                return r;
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "noxxer_scan.log"), DateTime.Now + "\r\n" + ex + "\r\n\r\n"); } catch { }
                return new AuthResult { Ok = false, Error = "Error de conexión: " + ex.Message };
            }
        }

        // Asks the server to generate a PIN. The PIN is sent to the administrator on Discord, never to this client.
        public static PinResult RequestPin(string optionalUsername)
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["hwid"] = GetHwid();
                if (!string.IsNullOrWhiteSpace(optionalUsername)) d["username"] = optionalUsername.Trim();
                string json = Request("/api/auth/requestpin", "POST", d);
                PinResult r = new PinResult();
                r.Ok = ExtractBool(json, "ok");
                r.Error = ExtractJson(json, "error");
                r.ExpiresIn = ExtractInt(json, "expiresIn", 600);
                if (r.Ok)
                {
                    r.Session = new PinSession();
                    r.Session.RequestId = ExtractJson(json, "requestId");
                    r.Session.Hwid = GetHwid();
                    if (!string.IsNullOrWhiteSpace(optionalUsername)) r.Session.Username = optionalUsername.Trim();
                    if (string.IsNullOrEmpty(r.Session.RequestId)) { r.Ok = false; r.Error = "Respuesta del servidor no válida"; }
                }
                return r;
            }
            catch (Exception ex) { return new PinResult { Ok = false, Error = "Error de conexión: " + ex.Message }; }
        }

        // Exchanges the PIN the administrator dictated for a single-use scan token.
        public static PinResult VerifyPin(PinSession request, string pin)
        {
            if (request == null || string.IsNullOrEmpty(request.RequestId))
                return new PinResult { Ok = false, Code = "not_found", Error = "Pide un PIN primero" };
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["requestId"] = request.RequestId;
                d["pin"] = (pin ?? "").Trim().ToUpperInvariant();
                d["hwid"] = string.IsNullOrEmpty(request.Hwid) ? GetHwid() : request.Hwid;
                string json = Request("/api/auth/verifypin", "POST", d);
                PinResult r = new PinResult();
                r.Ok = ExtractBool(json, "ok");
                r.Error = ExtractJson(json, "error");
                r.Code = ExtractJson(json, "code");
                r.AttemptsLeft = ExtractInt(json, "attemptsLeft", -1);
                string token = ExtractJson(json, "token");
                if (r.Ok && !string.IsNullOrEmpty(token))
                {
                    r.Session = new PinSession { RequestId = request.RequestId, Token = token, Hwid = d["hwid"].ToString(), Username = request.Username };
                }
                else if (r.Ok) { r.Ok = false; r.Error = "Respuesta del servidor no válida"; }
                return r;
            }
            catch (Exception ex) { return new PinResult { Ok = false, Error = "Error de conexión: " + ex.Message }; }
        }

        public static AuthResult PingServer()
        {
            try
            {
                string json = Request("/", "GET", null);
                bool ok = ExtractBool(json, "ok");
                string service = ExtractJson(json, "service");
                return new AuthResult
                {
                    Ok = ok,
                    Error = ok ? ("Conectado a: " + (service ?? "Noxxer API") + " | " + ServerUrlDebugInfo) : ("Fallo en respuesta del servidor. " + ServerUrlDebugInfo)
                };
            }
            catch (Exception ex)
            {
                return new AuthResult
                {
                    Ok = false,
                    Error = "No se puede conectar al servidor: " + ex.Message + ". " + ServerUrlDebugInfo +
                            " | Soluciones: 1) Asegurate de que el servidor este corriendo. 2) Revisa noxxer_server.txt con la IP publica de la VPS y el puerto correcto (ej: http://203.0.113.45:3000). 3) Abre el puerto 3000 en el firewall de tu VPS (Windows Firewall / UFW / iptables) y en el panel de tu proveedor cloud (AWS/GCP/OVH/Hetzner Security Groups)."
                };
            }
        }
    }
}
