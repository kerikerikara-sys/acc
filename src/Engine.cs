using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Noxxer
{
    public class Finding
    {
        public int Sev;              // 3 high, 2 medium, 1 low, 0 info
        public string Type, Source, Match, Detail;
        public DateTime Time;
    }

    public class ModuleInfo
    {
        public string Name;
        public int State;            // 0 pending, 1 running, 2 done
        public int High, Med, Low;
        public Action Run;
    }

    class ScanCtx
    {
        public string Source;
        public int MinSev = 1;
        public bool U16;
        public int Before = 90, After = 150;
        public Func<string, string> Post;
        public List<Rule> Rules;
        public Dictionary<string, int> Count = new Dictionary<string, int>();
        public HashSet<string> Seen = new HashSet<string>();
        public int Total;
    }

    public class Engine
    {
        public readonly List<ModuleInfo> Modules = new List<ModuleInfo>();
        public readonly List<Finding> Findings = new List<Finding>();
        public event Action<Finding> FindingAdded;
        public event Action ModuleChanged;
        public event Action Finished;

        public volatile bool Cancel;
        public volatile bool Running;
        public volatile string Activity = "";
        public volatile int Current;
        public float Frac;
        public long Files, Bytes;
        public int TotalHigh, TotalMed, TotalLow;
        public DateTime Started, Ended;
        public readonly bool Admin;

        readonly object lk = new object();
        readonly Dictionary<int, List<Rule>> flaggedProcesses = new Dictionary<int, List<Rule>>();
        Thread worker;
        readonly string self;

        public Engine()
        {
            Admin = IsAdmin();
            try { self = Process.GetCurrentProcess().MainModule.FileName; } catch { self = ""; }

            Modules.Add(new ModuleInfo { Name = "System integrity", Run = ModSystem });
            Modules.Add(new ModuleInfo { Name = "Memory integrity", Run = ModMemoryIntegrity });
            Modules.Add(new ModuleInfo { Name = "DMA hardware", Run = ModDma });
            Modules.Add(new ModuleInfo { Name = "Drivers", Run = ModDrivers });
            Modules.Add(new ModuleInfo { Name = "Processes", Run = ModProcesses });
            Modules.Add(new ModuleInfo { Name = "Network connections", Run = ModNetworkConnections });
            Modules.Add(new ModuleInfo { Name = "Memory regions", Run = ModMemoryRegions });
            Modules.Add(new ModuleInfo { Name = "Execution traces", Run = ModTraces });
            Modules.Add(new ModuleInfo { Name = "Recycle Bin", Run = ModRecycle });
            Modules.Add(new ModuleInfo { Name = "File system", Run = ModFiles });
            Modules.Add(new ModuleInfo { Name = "File contents", Run = ModContents });
            Modules.Add(new ModuleInfo { Name = "Browsers", Run = ModBrowsers });
            Modules.Add(new ModuleInfo { Name = "Discord", Run = ModDiscord });
            Modules.Add(new ModuleInfo { Name = "FiveM", Run = ModFiveM });
        }

        public static bool IsAdmin()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }

        // ------------------------------------------------------------------ control
        public void Start()
        {
            if (Running) return;
            lock (lk) { Findings.Clear(); }
            flaggedProcesses.Clear();
            foreach (ModuleInfo m in Modules) { m.State = 0; m.High = m.Med = m.Low = 0; }
            TotalHigh = TotalMed = TotalLow = 0;
            Files = 0; Bytes = 0; Frac = 0; Current = 0; Cancel = false; Running = true;
            Started = DateTime.Now;
            worker = new Thread(RunAll);
            worker.IsBackground = true;
            worker.Start();
        }

        public void Stop() { Cancel = true; }

        public double Progress
        {
            get
            {
                int done = 0;
                foreach (ModuleInfo m in Modules) if (m.State == 2) done++;
                double f = Running ? Frac : 0;
                return Math.Min(1.0, (done + f) / Modules.Count);
            }
        }

        public int Verdict()
        {
            if (TotalHigh > 0) return 2;
            if (TotalMed > 0) return 1;
            return 0;
        }

        void RunAll()
        {
            for (int i = 0; i < Modules.Count; i++)
            {
                if (Cancel) break;
                Current = i;
                ModuleInfo m = Modules[i];
                m.State = 1; Frac = 0;
                Fire(ModuleChanged);
                try { m.Run(); }
                catch (Exception ex) { Add(0, "Error", m.Name, "Module error", ex.Message); }
                m.State = 2; Frac = 1;
                Fire(ModuleChanged);
            }
            Ended = DateTime.Now;
            Running = false;
            Activity = "";
            Fire(Finished);
        }

        static void Fire(Action a) { if (a != null) a(); }

        public void Add(int sev, string type, string source, string match, string detail)
        {
            Finding f = new Finding { Sev = sev, Type = type, Source = source, Match = match, Detail = detail, Time = DateTime.Now };
            lock (lk)
            {
                Findings.Add(f);
                ModuleInfo m = Modules[Math.Min(Current, Modules.Count - 1)];
                if (sev >= 3) { m.High++; TotalHigh++; }
                else if (sev == 2) { m.Med++; TotalMed++; }
                else if (sev == 1) { m.Low++; TotalLow++; }
            }
            Action<Finding> h = FindingAdded;
            if (h != null) h(f);
        }

        public List<Finding> Snapshot()
        {
            lock (lk) { return new List<Finding>(Findings); }
        }

        void Act(string s) { Activity = s; }
        void P(int i, int n) { Frac = n <= 0 ? 1f : Math.Min(1f, i / (float)n); }

        // ------------------------------------------------------------------ helpers
        static string Env(string n) { return Environment.GetEnvironmentVariable(n) ?? ""; }
        static string LAD { get { return Env("LOCALAPPDATA"); } }
        static string AD { get { return Env("APPDATA"); } }
        static string UP { get { return Env("USERPROFILE"); } }

        bool SkipPath(string p)
        {
            if (string.IsNullOrEmpty(p)) return true;
            if (self.Length > 0 && string.Equals(p, self, StringComparison.OrdinalIgnoreCase)) return true;
            string n = Path.GetFileName(p).ToLowerInvariant();
            // Our own binaries (any copy or rename), reports and keyword list carry the signatures by design.
            return n.StartsWith("noxxer_report") || n.StartsWith("noxxer_keywords")
                || n.StartsWith("acnoxxer") || n.StartsWith("ac noxxer") || n.StartsWith("ac_noxxer");
        }

        static string Lat1Lower(byte[] b, int n)
        {
            char[] c = new char[n];
            for (int i = 0; i < n; i++)
            {
                byte x = b[i];
                if (x >= 65 && x <= 90) x += 32;
                else if (x == 43 || x == 45 || x == 95) x = 32;   // + - _  ->  space (see Rules.Norm)
                c[i] = (char)x;
            }
            return new string(c);
        }

        static string Clean(string t)
        {
            StringBuilder sb = new StringBuilder(t.Length);
            bool sp = true;
            foreach (char ch in t)
            {
                if (char.IsControl(ch) || ch == '\uFFFD') { if (!sp) { sb.Append(' '); sp = true; } continue; }
                if (ch == ' ') { if (!sp) { sb.Append(' '); sp = true; } continue; }
                sb.Append(ch); sp = false;
            }
            string s = sb.ToString().Trim();
            if (s.IndexOf('%') >= 0)
            {
                try { s = Uri.UnescapeDataString(s); } catch { }
            }
            if (s.Length > 320) s = s.Substring(0, 320) + "...";
            return s;
        }

        static string Snippet(byte[] buf, int n, int idx, int plen, bool u16, ScanCtx c)
        {
            int before = u16 ? c.Before * 2 : c.Before;
            int after = u16 ? c.After * 2 : c.After;
            int s = Math.Max(0, idx - before);
            int e = Math.Min(n, idx + plen + after);
            if (u16 && ((idx - s) & 1) != 0) s++;
            int cnt = e - s;
            if (u16 && (cnt & 1) != 0) cnt--;
            if (cnt <= 0) return "";
            string t = u16 ? Encoding.Unicode.GetString(buf, s, cnt) : Encoding.UTF8.GetString(buf, s, cnt);
            return Clean(t);
        }

        List<Rule> RulesFor(int minSev)
        {
            List<Rule> l = new List<Rule>();
            foreach (Rule r in Rules.All) if (r.Sev >= minSev) l.Add(r);
            return l;
        }

        ScanCtx Ctx(string source, int minSev, bool u16, Func<string, string> post, int before, int after)
        {
            return new ScanCtx { Source = source, MinSev = minSev, U16 = u16, Post = post, Before = before, After = after, Rules = RulesFor(minSev) };
        }

        void ScanBuffer(byte[] buf, int n, int skipBelow, ScanCtx c)
        {
            string hay = Lat1Lower(buf, n);
            foreach (Rule r in c.Rules)
            {
                if (Cancel || c.Total >= 40) return;
                for (int pass = 0; pass < (c.U16 ? 2 : 1); pass++)
                {
                    bool u = pass == 1;
                    string p = u ? r.Pat16 : r.Pat;
                    int from = 0;
                    while (true)
                    {
                        int idx;
                        if (!Matcher.Find(hay, r, from, u, out idx)) break;
                        from = idx + 1;
                        if (idx + p.Length <= skipBelow) continue;
                        int cnt;
                        c.Count.TryGetValue(r.Pat, out cnt);
                        if (cnt >= 3) break;
                        string snip = Snippet(buf, n, idx, p.Length, u, c);
                        string key = r.Pat + "|" + (snip.Length > 80 ? snip.Substring(0, 80) : snip);
                        if (!c.Seen.Add(key)) continue;
                        c.Count[r.Pat] = cnt + 1;
                        c.Total++;
                        Add(r.Sev, r.Cat, c.Source, r.Pat, c.Post != null ? c.Post(snip) : snip);
                        if (c.Total >= 40) return;
                    }
                }
            }
        }

        // Returns false when the file could not be opened (locked / access denied).
        bool ScanFile(string path, ScanCtx c, long maxLen)
        {
            if (Cancel) return true;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16))
                {
                    long len = fs.Length;
                    if (len == 0 || len > maxLen) return true;
                    const int CH = 16 << 20, OV = 1024;
                    byte[] buf = new byte[(int)Math.Min(len, CH) + OV + 8];
                    int carry = 0;
                    bool first = true;
                    while (true)
                    {
                        int got = 0;
                        while (got < CH)
                        {
                            int r = fs.Read(buf, carry + got, Math.Min(CH - got, buf.Length - carry - got));
                            if (r <= 0) break;
                            got += r;
                        }
                        if (got == 0) break;
                        int n = carry + got;
                        ScanBuffer(buf, n, first ? 0 : carry, c);
                        first = false;
                        Interlocked.Add(ref Bytes, got);
                        if (fs.Position >= fs.Length || Cancel) break;
                        carry = Math.Min(OV, n);
                        Buffer.BlockCopy(buf, n - carry, buf, 0, carry);
                    }
                }
                Interlocked.Increment(ref Files);
                return true;
            }
            catch { return false; }
        }

        // Chromium cache stores compressed API bodies; try to inflate an embedded gzip stream.
        void ScanGzipInside(string path, ScanCtx c)
        {
            try
            {
                byte[] b;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length < 64 || fs.Length > (8 << 20)) return;
                    b = new byte[fs.Length];
                    int got = 0;
                    while (got < b.Length) { int r = fs.Read(b, got, b.Length - got); if (r <= 0) break; got += r; }
                }
                int lim = Math.Min(b.Length - 3, 4096);
                for (int i = 0; i < lim; i++)
                {
                    if (b[i] != 0x1f || b[i + 1] != 0x8b || b[i + 2] != 8) continue;
                    MemoryStream outMs = new MemoryStream();
                    try
                    {
                        using (MemoryStream ms = new MemoryStream(b, i, b.Length - i))
                        using (GZipStream gz = new GZipStream(ms, CompressionMode.Decompress))
                        {
                            byte[] tmp = new byte[65536];
                            int r;
                            while ((r = gz.Read(tmp, 0, tmp.Length)) > 0)
                            {
                                outMs.Write(tmp, 0, r);
                                if (outMs.Length > (32 << 20)) break;
                            }
                        }
                    }
                    catch { }
                    if (outMs.Length > 0) ScanBuffer(outMs.GetBuffer(), (int)outMs.Length, 0, c);
                    return;
                }
            }
            catch { }
        }

        void Walk(string root, int maxDepth, Func<string, bool> skipDir, Action<string> onFile, Action<string> onDir)
        {
            if (!Directory.Exists(root)) return;
            Stack<KeyValuePair<string, int>> st = new Stack<KeyValuePair<string, int>>();
            st.Push(new KeyValuePair<string, int>(root, 0));
            while (st.Count > 0 && !Cancel)
            {
                KeyValuePair<string, int> cur = st.Pop();
                string[] files = null;
                try { files = Directory.GetFiles(cur.Key); } catch { }
                if (files != null && onFile != null)
                    foreach (string f in files) { if (Cancel) return; onFile(f); }
                if (cur.Value >= maxDepth) continue;
                string[] dirs = null;
                try { dirs = Directory.GetDirectories(cur.Key); } catch { }
                if (dirs == null) continue;
                foreach (string d in dirs)
                {
                    try { if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                    if (skipDir != null && skipDir(d)) continue;
                    if (onDir != null) onDir(d);
                    st.Push(new KeyValuePair<string, int>(d, cur.Value + 1));
                }
            }
        }

        static readonly HashSet<string> SkipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cache", "code cache", "gpucache", "node_modules", ".git", "packages", "windows", "winsxs",
            "$recycle.bin", "system volume information", "windowsapps", "servicing", "assembly"
        };

        static bool SkipCommon(string d) { return SkipNames.Contains(Path.GetFileName(d)); }

        static string FileStamp(string p)
        {
            try { return "   ·   modified " + File.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm"); } catch { return ""; }
        }

        void NameCheck(string full, string source, int minSev)
        {
            if (SkipPath(full)) return;
            string n = Path.GetFileName(full);
            foreach (Rule r in Matcher.MatchName(n, minSev))
                Add(r.Sev, r.Cat, source, r.Pat, full + FileStamp(full));
        }

        static string Run(string exe, string args, int timeoutMs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                using (Process p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(timeoutMs);
                    return o;
                }
            }
            catch { return ""; }
        }

        static List<string> FixedDrives()
        {
            List<string> l = new List<string>();
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                    if (d.DriveType == DriveType.Fixed && d.IsReady) l.Add(d.Name);
            }
            catch { }
            return l;
        }

        // ================================================================== MODULES
        // ---- 1. System integrity
        void ModSystem()
        {
            Act("Checking system integrity");
            if (!Admin)
                Add(0, "System", "System", "Not elevated", "Run as administrator for Prefetch, BAM and event-log checks.");

            try
            {
                object sb = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled", null);
                if (sb is int && (int)sb == 0)
                    Add(1, "System", "Secure Boot", "Disabled", "Secure Boot is turned off (needed by some kernel-level cheats).");
            }
            catch { }
            P(1, 5);

            Act("Checking Fast Boot");
            try
            {
                object fb = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", null);
                if (fb is int && (int)fb == 1)
                    Add(1, "System", "Fast Boot", "Fast Boot is enabled", "Fast Boot keeps parts of the previous session alive, so boot-time traces may not reflect a clean start.");
            }
            catch { }

            if (Admin)
            {
                Act("Reading boot configuration");
                string bcd = Run("bcdedit", "/enum {current}", 8000);
                foreach (string line in bcd.Split('\n'))
                {
                    string l = line.ToLowerInvariant();
                    if (l.Contains("testsigning") && l.Contains("yes"))
                        Add(2, "System", "Boot configuration", "Test signing ON", "Unsigned kernel drivers can be loaded (testsigning = Yes).");
                    if (l.Contains("nointegritychecks") && l.Contains("yes"))
                        Add(3, "System", "Boot configuration", "Integrity checks OFF", "Driver signature enforcement disabled (nointegritychecks = Yes).");
                }
            }
            P(2, 5);

            CheckLogClear("Security", 1102, "Security event log cleared");
            P(3, 5);
            CheckLogClear("System", 104, "System event log cleared");
            P(4, 5);

            try
            {
                object pf = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", null);
                if (pf is int && (int)pf == 0)
                    Add(1, "Anti-Forensics", "Prefetch", "Prefetch disabled", "EnablePrefetcher = 0, so no execution history is recorded.");
                if (Admin)
                {
                    string pd = Path.Combine(Env("SystemRoot"), "Prefetch");
                    if (Directory.Exists(pd))
                    {
                        int cnt = Directory.GetFiles(pd, "*.pf").Length;
                        if (cnt < 8)
                            Add(1, "Anti-Forensics", "Prefetch", "Prefetch almost empty", cnt + " .pf files found. It may have been cleared recently.");
                    }
                }
            }
            catch { }
        }

        void CheckLogClear(string log, int id, string title)
        {
            if (!Admin) return;
            Act("Checking " + log + " event log");
            string xml = Run("wevtutil", "qe " + log + " /q:\"*[System[(EventID=" + id + ")]]\" /c:1 /rd:true /f:xml", 10000);
            Match m = Regex.Match(xml, "SystemTime='([^']+)'");
            if (!m.Success) return;
            DateTime t;
            if (!DateTime.TryParse(m.Groups[1].Value, out t)) return;
            t = t.ToLocalTime();
            double days = (DateTime.Now - t).TotalDays;
            Add(days <= 14 ? 2 : 1, "Anti-Forensics", log + " log", title, "Cleared on " + t.ToString("yyyy-MM-dd HH:mm") + " (" + (int)days + " days ago).");
        }

        // ---- Memory integrity / HVCI configuration
        void ModMemoryIntegrity()
        {
            Act("Checking memory integrity");
            try
            {
                object enabled = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                    "Enabled", null);
                if (enabled is int)
                {
                    bool on = (int)enabled == 1;
                    Add(on ? 0 : 1, "System", "Memory integrity", on ? "Memory Integrity is enabled" : "Memory Integrity is disabled",
                        on ? "Hypervisor-protected code integrity (HVCI) is enabled."
                           : "Hypervisor-protected code integrity (HVCI) is disabled. This setting alone is not evidence of cheating.");
                }
                else
                    Add(0, "System", "Memory integrity", "State unavailable", "Windows did not expose an HVCI configuration value.");
            }
            catch (Exception ex) { Add(0, "Error", "Memory integrity", "Registry query failed", ex.Message); }
        }

        // ---- 2. DMA hardware
        void ModDma()
        {
            Act("Enumerating PCIe / USB devices");
            HashSet<string> seen = new HashSet<string>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name,DeviceID,Manufacturer FROM Win32_PnPEntity"))
                {
                    ManagementObjectCollection col = s.Get();
                    foreach (ManagementBaseObject o in col)
                    {
                        if (Cancel) return;
                        string id = (o["DeviceID"] ?? "").ToString();
                        string name = (o["Name"] ?? "").ToString();
                        string mf = (o["Manufacturer"] ?? "").ToString();
                        if (!seen.Add(id)) continue;
                        string u = id.ToUpperInvariant(), ln = name.ToLowerInvariant(), lm = mf.ToLowerInvariant();
                        string d = name + "   ·   " + id;
                        if (u.Contains("VEN_10EE"))
                        {
                            if (u.Contains("DEV_0666")) Add(3, Rules.Dma, "PCIe device", "Xilinx DEV_0666", d + "   ·   default PCILeech FPGA device ID.");
                            else Add(2, Rules.Dma, "PCIe device", "Xilinx FPGA", d + "   ·   review: FPGA boards are used for DMA cheats.");
                        }
                        else if (u.Contains("VID_0403&PID_601F") || ln.Contains("ft601") || ln.Contains("superspeed-fifo"))
                            Add(3, Rules.Dma, "USB device", "FTDI FT601 bridge", d + "   ·   USB3 FIFO bridge typical for DMA boards.");
                        else if (lm.Contains("xilinx") || ln.Contains("xilinx"))
                            Add(2, Rules.Dma, "Device", "Xilinx device", d);
                        else if (ln.Contains("kmbox"))
                            Add(3, Rules.Dma, "USB device", "KMBox", d);
                        else if (u.StartsWith("USB\\VID_0403&PID_6010") || u.StartsWith("USB\\VID_0403&PID_6014") || u.StartsWith("USB\\VID_0403&PID_6001"))
                            Add(1, Rules.Dma, "USB device", "FTDI device", d + "   ·   common on legit hardware too.");
                        else if (ln.Contains("data acquisition and signal processing") && !u.Contains("VEN_8086") && !u.Contains("VEN_1022") && !u.Contains("VEN_10DE"))
                            Add(1, Rules.Dma, "PCIe device", "Unidentified controller", d + "   ·   non-chipset device with no driver.");
                    }
                }
            }
            catch (Exception ex) { Add(0, "Error", "DMA hardware", "WMI query failed", ex.Message); }
        }

        // ---- 3. Drivers
        // Driver file names (without .sys) abused by manual mappers. Matched against the file name, never as a substring.
        static readonly string[] Byovd =
        {
            "iqvw64e", "capcom", "dbutil_2_3", "rtcore64", "rtcore32", "mhyprot", "mhyprot2", "mhyprot3", "gdrv",
            "winring0x64", "asmmap64", "asio64", "physmem", "phymem64", "kprocesshacker", "atillk64", "glckio2",
            "eneio64", "gmerdrv", "wnbios", "bs_rcio64", "directio64", "piddrv", "msio64", "ntiolib_x64", "amifldrv64", "mimidrv"
        };

        static string MatchByovd(string serviceName, string path)
        {
            string svc = (serviceName ?? "").ToLowerInvariant();
            string file = "";
            try { file = Path.GetFileNameWithoutExtension((path ?? "").Trim('"')).ToLowerInvariant(); } catch { }
            foreach (string b in Byovd)
                if (file == b || svc == b) return b;
            return null;
        }

        void ModDrivers()
        {
            Act("Inspecting loaded drivers");
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name,PathName,State FROM Win32_SystemDriver"))
                {
                    ManagementObjectCollection col = s.Get();
                    foreach (ManagementBaseObject o in col)
                    {
                        if (Cancel) return;
                        string name = (o["Name"] ?? "").ToString();
                        string path = (o["PathName"] ?? "").ToString();
                        string state = (o["State"] ?? "").ToString();
                        string low = (name + " " + path).ToLowerInvariant();
                        string d = name + "   ·   " + path + "   ·   " + state;
                        string hit = MatchByovd(name, path);
                        if (hit != null)
                        {
                            Add(2, Rules.Tool, "Driver", hit, d + "   ·   known mapper-abused driver (legit only with its vendor software).");
                            continue;
                        }
                        if (low.Contains("\\users\\") || low.Contains("\\temp\\") || low.Contains("\\appdata\\"))
                        {
                            Add(2, Rules.Tool, "Driver", "Driver in user folder", d);
                            continue;
                        }
                        foreach (Rule r in Matcher.MatchName(low, 2))
                            Add(r.Sev, r.Cat, "Driver", r.Pat, d);
                    }
                }
            }
            catch (Exception ex) { Add(0, "Error", "Drivers", "WMI query failed", ex.Message); }
        }

        // ---- 4. Processes
        static readonly string[] ModWhitelist =
        {
            "\\fivem", "\\citizenfx", "\\discord", "\\overwolf", "\\nvidia", "\\steam", "\\obs", "\\rockstar",
            "\\epic", "\\microsoft", "\\intel", "\\amd", "\\windowsapps", "\\razer", "\\logitech", "\\corsair",
            "\\msi", "\\medal", "\\streamlabs", "\\xsplit", "\\google", "\\mozilla", "\\opera", "\\brave",
            "\\programs\\", "\\spotify", "\\teamviewer", "\\anydesk"
        };

        void ModProcesses()
        {
            Act("Enumerating processes");
            Process[] all;
            try { all = Process.GetProcesses(); } catch { return; }
            int i = 0;
            foreach (Process p in all)
            {
                if (Cancel) return;
                P(i++, all.Length);
                string name = "", title = "", path = "";
                try { name = p.ProcessName; } catch { }
                try { title = p.MainWindowTitle; } catch { }
                try { path = p.MainModule.FileName; } catch { }
                Act("Process: " + name);
                string hay = name + " " + title + " " + path;
                List<Rule> processMatches = Matcher.MatchName(hay, 1);
                if (processMatches.Count > 0) flaggedProcesses[p.Id] = processMatches;
                foreach (Rule r in processMatches)
                    Add(r.Sev, r.Cat, "Process", r.Pat, "PID " + p.Id + "   ·   " + name + (title.Length > 0 ? "   ·   \"" + title + "\"" : "") + (path.Length > 0 ? "   ·   " + path : ""));

                string ln = name.ToLowerInvariant();
                if (ln.StartsWith("fivem") || ln == "gta5" || ln.Contains("gtaprocess"))
                {
                    try
                    {
                        foreach (ProcessModule m in p.Modules)
                        {
                            string mp = (m.FileName ?? "").ToLowerInvariant();
                            foreach (Rule r in Matcher.MatchName(Path.GetFileName(mp), 1))
                                Add(r.Sev, r.Cat, "Game process module", r.Pat, name + " (PID " + p.Id + ")   ·   " + m.FileName);
                            if (!(mp.Contains("\\users\\") || mp.Contains("\\temp\\") || mp.Contains("\\programdata\\"))) continue;
                            bool ok = false;
                            foreach (string w in ModWhitelist) if (mp.Contains(w)) { ok = true; break; }
                            if (!ok)
                                Add(2, Rules.Tool, "Game process module", "Unusual module loaded", name + " (PID " + p.Id + ")   ·   " + m.FileName + "   ·   review: possible injected DLL.");
                        }
                    }
                    catch { }
                }
            }
        }

        // ---- Network connections associated with processes already matched by scan rules
        void ModNetworkConnections()
        {
            Act("Analyzing network connections");
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("root\\StandardCimv2",
                    "SELECT OwningProcess,RemoteAddress,RemotePort FROM MSFT_NetTCPConnection WHERE State = 5"))
                using (ManagementObjectCollection col = s.Get())
                {
                    HashSet<string> reported = new HashSet<string>();
                    foreach (ManagementBaseObject o in col)
                    {
                        if (Cancel) return;
                        uint rawPid;
                        if (!uint.TryParse((o["OwningProcess"] ?? "").ToString(), out rawPid) || rawPid > int.MaxValue) continue;
                        List<Rule> matches;
                        if (!flaggedProcesses.TryGetValue((int)rawPid, out matches)) continue;
                        string remote = (o["RemoteAddress"] ?? "").ToString();
                        string port = (o["RemotePort"] ?? "").ToString();
                        foreach (Rule r in matches)
                        {
                            string key = rawPid + "|" + remote + "|" + port + "|" + r.Pat;
                            if (!reported.Add(key)) continue;
                            Add(r.Sev, "Network", "Network connection", r.Pat,
                                "Established TCP connection from PID " + rawPid + " to " + remote + ":" + port + ". The owning process also matched a scan rule.");
                        }
                    }
                }
            }
            catch (Exception ex) { Add(0, "Network", "Network connections", "Inspection unavailable", ex.Message); }
        }

        // ---- Read-only scan of executable private regions in FiveM / GTA processes
        [StructLayout(LayoutKind.Sequential)]
        struct MemoryBasicInformation
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public UIntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UIntPtr VirtualQueryEx(IntPtr process, IntPtr address, out MemoryBasicInformation info, UIntPtr length);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, UIntPtr size, out UIntPtr read);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        void ModMemoryRegions()
        {
            Act("Scanning executable game memory regions");
            Process[] processes;
            try { processes = Process.GetProcesses(); } catch { return; }
            bool foundTarget = false;
            foreach (Process p in processes)
            {
                if (Cancel) return;
                string name;
                try { name = p.ProcessName; } catch { continue; }
                string lower = name.ToLowerInvariant();
                if (!(lower.StartsWith("fivem") || lower == "gta5" || lower.Contains("gtaprocess"))) continue;
                foundTarget = true;
                Act("Memory regions: " + name);
                IntPtr handle = OpenProcess(0x0410, false, p.Id);
                if (handle == IntPtr.Zero) continue;
                try
                {
                    const ulong maxBytesPerProcess = 32UL * 1024UL * 1024UL;
                    const int readChunk = 64 * 1024;
                    const uint memCommit = 0x1000, memPrivate = 0x20000, pageGuard = 0x100, pageNoAccess = 0x01;
                    ulong scanned = 0;
                    long address = 0;
                    ScanCtx ctx = Ctx("Memory regions › " + name + " (PID " + p.Id + ")", 2, true, null, 0, 0);
                    int structSize = Marshal.SizeOf(typeof(MemoryBasicInformation));
                    for (int regions = 0; regions < 250000 && scanned < maxBytesPerProcess && !Cancel; regions++)
                    {
                        MemoryBasicInformation info;
                        UIntPtr queried = VirtualQueryEx(handle, new IntPtr(address), out info, new UIntPtr((uint)structSize));
                        if (queried == UIntPtr.Zero) break;
                        long baseAddress = info.BaseAddress.ToInt64();
                        ulong regionSize = info.RegionSize.ToUInt64();
                        if (regionSize == 0 || regionSize > (ulong)long.MaxValue || baseAddress > long.MaxValue - (long)regionSize) break;
                        long nextAddress = baseAddress + (long)regionSize;
                        uint protection = info.Protect & 0xFF;
                        bool executable = protection == 0x10 || protection == 0x20 || protection == 0x40 || protection == 0x80;
                        if (info.State == memCommit && info.Type == memPrivate && executable && (info.Protect & (pageGuard | pageNoAccess)) == 0)
                        {
                            ulong offset = 0;
                            while (offset < regionSize && scanned < maxBytesPerProcess && !Cancel)
                            {
                                int wanted = (int)Math.Min((ulong)readChunk, Math.Min(regionSize - offset, maxBytesPerProcess - scanned));
                                byte[] buffer = new byte[wanted];
                                UIntPtr bytesRead;
                                bool ok = ReadProcessMemory(handle, new IntPtr(baseAddress + (long)offset), buffer, new UIntPtr((uint)wanted), out bytesRead);
                                ulong got = bytesRead.ToUInt64();
                                if (got > 0)
                                {
                                    ScanBuffer(buffer, (int)got, 0, ctx);
                                    offset += got;
                                    scanned += got;
                                }
                                if (!ok || got == 0) break;
                            }
                        }
                        if (nextAddress <= address) break;
                        address = nextAddress;
                    }
                }
                finally { CloseHandle(handle); }
            }
            if (!foundTarget)
                Add(0, "Memory", "Memory regions", "No target game running", "Start FiveM or GTA V to inspect executable private memory regions.");
        }

        // ---- 5. Execution traces
        static string Rot13(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                char ch = c[i];
                if (ch >= 'a' && ch <= 'z') c[i] = (char)('a' + (ch - 'a' + 13) % 26);
                else if (ch >= 'A' && ch <= 'Z') c[i] = (char)('A' + (ch - 'A' + 13) % 26);
            }
            return new string(c);
        }

        void TraceCheck(string source, string path, string extra)
        {
            foreach (Rule r in Matcher.MatchName(path, 1))
            {
                string note = "";
                try { if (Path.IsPathRooted(path) && !File.Exists(path)) note = "   ·   file no longer on disk"; } catch { }
                Add(r.Sev, r.Cat, source, r.Pat, path + extra + note);
            }
        }

        // An executable that ran recently (BAM timestamp) and is no longer on disk.
        void CheckDeletedExecution(string path, byte[] data)
        {
            try
            {
                if (data == null || data.Length < 8 || !Path.IsPathRooted(path) || File.Exists(path)) return;
                long ft = BitConverter.ToInt64(data, 0);
                if (ft <= 0) return;
                double mins = (DateTime.Now - DateTime.FromFileTime(ft)).TotalMinutes;
                if (mins < 0 || mins > 60 * 24) return;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".exe" && ext != ".scr" && ext != ".com") return;
                string ago = mins < 60 ? (int)mins + " min ago" : (int)(mins / 60) + " h ago";
                Add(1, "Anti-Forensics", "Registry › BAM", Path.GetFileName(path).ToUpperInvariant() + " executed " + ago + " and deleted", path);
            }
            catch { }
        }

        void ModTraces()
        {
            // UserAssist
            Act("UserAssist");
            try
            {
                using (RegistryKey ua = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist"))
                {
                    if (ua != null)
                        foreach (string g in ua.GetSubKeyNames())
                            using (RegistryKey cnt = ua.OpenSubKey(g + "\\Count"))
                            {
                                if (cnt == null) continue;
                                foreach (string vn in cnt.GetValueNames())
                                {
                                    string dec = Rot13(vn);
                                    string extra = "";
                                    byte[] data = cnt.GetValue(vn) as byte[];
                                    if (data != null && data.Length >= 68)
                                    {
                                        long ft = BitConverter.ToInt64(data, 60);
                                        if (ft > 0) { try { extra = "   ·   last run " + DateTime.FromFileTime(ft).ToString("yyyy-MM-dd HH:mm"); } catch { } }
                                    }
                                    TraceCheck("Registry › UserAssist", dec, extra);
                                }
                            }
                }
            }
            catch { }
            P(1, 8);

            Act("MUICache");
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache"))
                {
                    if (k != null)
                        foreach (string vn in k.GetValueNames())
                        {
                            TraceCheck("Registry › MuiCache", vn, "");
                            object v = k.GetValue(vn);
                            if (v is string) TraceCheck("Registry › MuiCache", (string)v, "");
                        }
                }
            }
            catch { }
            P(2, 8);

            Act("Compatibility Assistant");
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store"))
                {
                    if (k != null) foreach (string vn in k.GetValueNames()) TraceCheck("Registry › AppCompat", vn, "");
                }
            }
            catch { }
            P(3, 8);

            if (Admin)
            {
                Act("Background Activity Monitor");
                foreach (string basePath in new[] { @"SYSTEM\CurrentControlSet\Services\bam\State\UserSettings", @"SYSTEM\CurrentControlSet\Services\bam\UserSettings" })
                {
                    try
                    {
                        using (RegistryKey bam = Registry.LocalMachine.OpenSubKey(basePath))
                        {
                            if (bam == null) continue;
                            foreach (string sid in bam.GetSubKeyNames())
                                using (RegistryKey k = bam.OpenSubKey(sid))
                                {
                                    if (k == null) continue;
                                    foreach (string vn in k.GetValueNames())
                                    {
                                        string extra = "";
                                        byte[] data = k.GetValue(vn) as byte[];
                                        if (data != null && data.Length >= 8)
                                        {
                                            long ft = BitConverter.ToInt64(data, 0);
                                            if (ft > 0) { try { extra = "   ·   last run " + DateTime.FromFileTime(ft).ToString("yyyy-MM-dd HH:mm"); } catch { } }
                                        }
                                        TraceCheck("Registry › BAM", vn, extra);
                                        CheckDeletedExecution(vn, data);
                                    }
                                }
                        }
                    }
                    catch { }
                }

                Act("Prefetch");
                try
                {
                    string pd = Path.Combine(Env("SystemRoot"), "Prefetch");
                    if (Directory.Exists(pd))
                        foreach (string f in Directory.GetFiles(pd, "*.pf"))
                            foreach (Rule r in Matcher.MatchName(Path.GetFileName(f), 1))
                                Add(r.Sev, r.Cat, "Prefetch", r.Pat, Path.GetFileName(f) + "   ·   last run " + File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm"));
                }
                catch { }
            }
            P(5, 8);

            Act("Recent files");
            try
            {
                string recent = Path.Combine(AD, @"Microsoft\Windows\Recent");
                if (Directory.Exists(recent))
                    foreach (string f in Directory.GetFiles(recent, "*.lnk"))
                        NameCheck(f, "Recent files", 1);
            }
            catch { }

            Act("Jump lists");
            try
            {
                string jl = Path.Combine(AD, @"Microsoft\Windows\Recent");
                foreach (string sub in new[] { "AutomaticDestinations", "CustomDestinations" })
                {
                    string d = Path.Combine(jl, sub);
                    if (!Directory.Exists(d)) continue;
                    foreach (string f in Directory.GetFiles(d))
                        ScanFile(f, Ctx("Jump list › " + Path.GetFileName(f), 2, true, null, 60, 100), 20 << 20);
                }
            }
            catch { }
            P(6, 8);

            Act("Activity timeline");
            try
            {
                string cdp = Path.Combine(LAD, "ConnectedDevicesPlatform");
                if (Directory.Exists(cdp))
                    foreach (string f in Directory.GetFiles(cdp, "ActivitiesCache.db", SearchOption.AllDirectories))
                        ScanFile(f, Ctx("Windows Timeline › ActivitiesCache", 2, true, null, 60, 120), 400L << 20);
            }
            catch { }
            P(7, 8);

            Act("PowerShell history");
            try
            {
                string ps = Path.Combine(AD, @"Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt");
                if (File.Exists(ps)) ScanFile(ps, Ctx("PowerShell history", 1, false, null, 60, 100), 50 << 20);
            }
            catch { }
        }

        // ---- 6. Recycle Bin
        void ModRecycle()
        {
            Act("Reading Recycle Bin");
            string sid = "";
            try { sid = WindowsIdentity.GetCurrent().User.Value; } catch { return; }
            foreach (string drive in FixedDrives())
            {
                string dir = Path.Combine(drive, "$Recycle.Bin", sid);
                if (!Directory.Exists(dir)) continue;
                string[] infos;
                try { infos = Directory.GetFiles(dir, "$I*"); } catch { continue; }
                foreach (string f in infos)
                {
                    if (Cancel) return;
                    try
                    {
                        byte[] b = File.ReadAllBytes(f);
                        if (b.Length < 30) continue;
                        long ver = BitConverter.ToInt64(b, 0);
                        long ft = BitConverter.ToInt64(b, 16);
                        string orig;
                        if (ver == 2)
                        {
                            int chars = BitConverter.ToInt32(b, 24);
                            int len = Math.Min(Math.Max(chars, 0) * 2, b.Length - 28);
                            orig = Encoding.Unicode.GetString(b, 28, len);
                        }
                        else orig = Encoding.Unicode.GetString(b, 24, Math.Min(520, b.Length - 24));
                        orig = orig.TrimEnd('\0');
                        string when = "";
                        try { when = "   ·   deleted " + DateTime.FromFileTime(ft).ToString("yyyy-MM-dd HH:mm"); } catch { }
                        foreach (Rule r in Matcher.MatchName(Path.GetFileName(orig), 1))
                            Add(r.Sev, r.Cat, "Recycle Bin", r.Pat, orig + when);
                    }
                    catch { }
                }
                try
                {
                    double hrs = (DateTime.Now - Directory.GetLastWriteTime(dir)).TotalHours;
                    if (infos.Length > 0 && hrs < 1)
                        Add(1, "Anti-Forensics", "Recycle Bin", "Recycle bin modified", "Recycle Bin on " + drive + " was modified " + (int)(hrs * 60) + " min ago.");
                }
                catch { }
                try
                {
                    if (infos.Length == 0 && (DateTime.Now - Directory.GetLastWriteTime(dir)).TotalHours < 12)
                        Add(1, "Anti-Forensics", "Recycle Bin", "Recently emptied", "Recycle Bin on " + drive + " was emptied " + Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm") + ".");
                }
                catch { }
            }
        }

        // ---- 7. File system (names)
        void ModFiles()
        {
            List<string> roots = new List<string>();
            List<int> depths = new List<int>();
            roots.Add(UP); depths.Add(7);
            roots.Add(Env("ProgramData")); depths.Add(3);
            roots.Add(Env("ProgramFiles")); depths.Add(2);
            roots.Add(Env("ProgramFiles(x86)")); depths.Add(2);
            string sysDrive = Path.GetPathRoot(Env("SystemRoot"));
            foreach (string d in FixedDrives())
            {
                roots.Add(d); depths.Add(3);
            }
            HashSet<string> topSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "users", "programdata", "program files", "program files (x86)", "windows", "$recycle.bin", "system volume information" };
            for (int i = 0; i < roots.Count; i++)
            {
                if (Cancel) return;
                string root = roots[i];
                if (string.IsNullOrEmpty(root)) continue;
                Act("Scanning names: " + root);
                P(i, roots.Count);
                bool isDrive = root.Length <= 3;
                Walk(root, depths[i],
                    delegate(string d)
                    {
                        if (SkipCommon(d)) return true;
                        if (isDrive && topSkip.Contains(Path.GetFileName(d)) && Path.GetDirectoryName(d) != null && Path.GetDirectoryName(d).Length <= 3) return true;
                        return false;
                    },
                    delegate(string f)
                    {
                        Interlocked.Increment(ref Files);
                        if ((Files & 0x1FF) == 0) Act(f);
                        NameCheck(f, "File name", 1);
                    },
                    delegate(string d) { NameCheck(d, "Folder name", 1); });
            }
        }

        // ---- 8. File contents / binary signatures
        static readonly HashSet<string> BinExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".exe", ".dll", ".sys", ".bin", ".rom", ".scr", ".drv", ".ocx", ".asi" };
        static readonly HashSet<string> TxtExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".lua", ".txt", ".json", ".cfg", ".ini", ".log", ".bat", ".cmd", ".ps1", ".md", ".js", ".py", ".xml", ".yaml", ".yml", ".html", ".htm", ".url", ".csv", ".conf", ".toml" };

        void ModContents()
        {
            Act("Collecting candidate files");
            List<string> bins = new List<string>();
            List<string> txts = new List<string>();
            Action<string> collect = delegate(string f)
            {
                if (bins.Count + txts.Count > 9000) return;
                if (SkipPath(f)) return;
                string ext = Path.GetExtension(f);
                if (BinExt.Contains(ext)) bins.Add(f);
                else if (TxtExt.Contains(ext)) txts.Add(f);
            };
            HashSet<string> appSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "discord", "discordcanary", "discordptb", "google", "mozilla", "opera software", "code", "slack",
                "spotify", "microsoft", "braveSoftware", "vivaldi", "cache", "code cache", "gpucache", "node_modules", ".git", "packages"
            };
            Func<string, bool> skip = delegate(string d) { return appSkip.Contains(Path.GetFileName(d)); };

            string dl = Path.Combine(UP, "Downloads");
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Walk(dl, 4, skip, collect, null);
            Walk(desk, 4, skip, collect, null);
            Walk(docs, 4, skip, collect, null);
            Walk(Path.Combine(UP, "Videos"), 2, skip, collect, null);
            Walk(Env("TEMP"), 3, skip, collect, null);
            Walk(AD, 3, skip, collect, null);
            Walk(Path.Combine(LAD, "Temp"), 3, skip, collect, null);
            Walk(Env("ProgramData"), 2, skip, collect, null);
            foreach (string drive in FixedDrives())
            {
                string sysRoot = Path.GetPathRoot(Env("SystemRoot"));
                HashSet<string> skipTop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "users", "programdata", "program files", "program files (x86)", "windows", "$recycle.bin", "system volume information" };
                Walk(drive, 3,
                    delegate(string d)
                    {
                        if (appSkip.Contains(Path.GetFileName(d))) return true;
                        string par = Path.GetDirectoryName(d);
                        return par != null && par.Length <= 3 && skipTop.Contains(Path.GetFileName(d));
                    }, collect, null);
            }

            HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> queue = new List<string>();
            foreach (string f in bins) if (done.Add(f)) queue.Add(f);
            foreach (string f in txts) if (done.Add(f)) queue.Add(f);
            for (int i = 0; i < queue.Count; i++)
            {
                if (Cancel) return;
                string f = queue[i];
                string name = Path.GetFileName(f);
                Act(f);
                if ((i & 7) == 0) P(i, queue.Count);
                bool bin = BinExt.Contains(Path.GetExtension(f));
                ScanCtx c = Ctx((bin ? "Binary › " : "File › ") + name, bin ? 2 : 1, true, delegate(string s) { return f + "   ·   " + s; }, 70, 110);
                ScanFile(f, c, bin ? (40L << 20) : (4L << 20));
            }
        }

        // ---- 9. Browsers
        class BrowserDef { public string Name, Root; public bool Flat; }

        static string BrowserPost(string s)
        {
            Match m = Regex.Match(s, "[?&](?:q|query|search_query|text)=([^&\\s]+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string q = m.Groups[1].Value.Replace('+', ' ');
                try { q = Uri.UnescapeDataString(q); } catch { }
                return "Search: \"" + q + "\"   ▸   " + s;
            }
            return s;
        }

        void ModBrowsers()
        {
            List<BrowserDef> defs = new List<BrowserDef>();
            defs.Add(new BrowserDef { Name = "Chrome", Root = Path.Combine(LAD, @"Google\Chrome\User Data") });
            defs.Add(new BrowserDef { Name = "Edge", Root = Path.Combine(LAD, @"Microsoft\Edge\User Data") });
            defs.Add(new BrowserDef { Name = "Brave", Root = Path.Combine(LAD, @"BraveSoftware\Brave-Browser\User Data") });
            defs.Add(new BrowserDef { Name = "Vivaldi", Root = Path.Combine(LAD, @"Vivaldi\User Data") });
            defs.Add(new BrowserDef { Name = "Chromium", Root = Path.Combine(LAD, @"Chromium\User Data") });
            defs.Add(new BrowserDef { Name = "Opera", Root = Path.Combine(AD, @"Opera Software\Opera Stable"), Flat = true });
            defs.Add(new BrowserDef { Name = "Opera GX", Root = Path.Combine(AD, @"Opera Software\Opera GX Stable"), Flat = true });

            string[] dbFiles = { "History", "Shortcuts", "Favicons", "Web Data", "Top Sites" };
            int idx = 0;
            int found = 0;
            foreach (BrowserDef b in defs)
            {
                P(idx++, defs.Count + 1);
                if (Cancel) return;
                if (!Directory.Exists(b.Root)) continue;
                found++;
                List<string> profiles = new List<string>();
                if (b.Flat) profiles.Add(b.Root);
                else
                {
                    try
                    {
                        foreach (string d in Directory.GetDirectories(b.Root))
                        {
                            string n = Path.GetFileName(d);
                            if (n == "Default" || n.StartsWith("Profile ") || n.StartsWith("Guest")) profiles.Add(d);
                        }
                    }
                    catch { }
                }
                foreach (string prof in profiles)
                {
                    string pn = Path.GetFileName(prof);
                    foreach (string fn in dbFiles)
                    {
                        string f = Path.Combine(prof, fn);
                        if (!File.Exists(f)) continue;
                        Act(b.Name + " › " + pn + " › " + fn);
                        bool ok = ScanFile(f, Ctx(b.Name + " › " + pn + " › " + fn, 1, false, BrowserPost, 100, 140), 600L << 20);
                        if (!ok) Add(0, "Browser", b.Name + " › " + fn, "Could not read", "File is locked. Close " + b.Name + " and scan again for full coverage.");
                    }
                    string sess = Path.Combine(prof, "Sessions");
                    if (Directory.Exists(sess))
                        foreach (string f in Directory.GetFiles(sess))
                        {
                            Act(b.Name + " › Sessions › " + Path.GetFileName(f));
                            ScanFile(f, Ctx(b.Name + " › " + pn + " › Sessions", 1, false, BrowserPost, 100, 140), 100L << 20);
                        }
                    string cache = Path.Combine(prof, @"Cache\Cache_Data");
                    if (Directory.Exists(cache)) ScanCache(cache, b.Name + " › " + pn + " › Cache", 300L << 20, false);
                }
            }

            // Firefox
            Act("Firefox");
            string ffRoot = Path.Combine(AD, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ffRoot))
            {
                found++;
                foreach (string prof in Directory.GetDirectories(ffRoot))
                {
                    string pn = Path.GetFileName(prof);
                    foreach (string fn in new[] { "places.sqlite", "places.sqlite-wal", "formhistory.sqlite", "favicons.sqlite" })
                    {
                        string f = Path.Combine(prof, fn);
                        if (!File.Exists(f)) continue;
                        Act("Firefox › " + fn);
                        bool ok = ScanFile(f, Ctx("Firefox › " + pn + " › " + fn, 1, false, BrowserPost, 100, 140), 600L << 20);
                        if (!ok) Add(0, "Browser", "Firefox › " + fn, "Could not read", "File is locked. Close Firefox and scan again for full coverage.");
                    }
                }
                string ffCache = Path.Combine(LAD, @"Mozilla\Firefox\Profiles");
                if (Directory.Exists(ffCache))
                    foreach (string prof in Directory.GetDirectories(ffCache))
                    {
                        string e = Path.Combine(prof, @"cache2\entries");
                        if (Directory.Exists(e)) ScanCache(e, "Firefox › Cache", 200L << 20, false);
                    }
            }
            if (found == 0) Add(0, "Browser", "Browsers", "None found", "No supported browser profiles were found for this user.");
        }

        void ScanCache(string dir, string source, long cap, bool gzip)
        {
            long total = 0;
            string[] files;
            try { files = Directory.GetFiles(dir); } catch { return; }
            int i = 0;
            foreach (string f in files)
            {
                if (Cancel || total > cap) return;
                long len = 0;
                try { len = new FileInfo(f).Length; } catch { continue; }
                if (len > (8 << 20) || len == 0) continue;
                total += len;
                if ((i++ & 15) == 0) Act(source + " › " + Path.GetFileName(f));
                ScanCtx c = Ctx(source, 1, false, gzip ? (Func<string, string>)DiscordPost : BrowserPost, gzip ? 250 : 100, gzip ? 300 : 140);
                ScanFile(f, c, 8 << 20);
                if (gzip) ScanGzipInside(f, c);
            }
        }

        // ---- 10. Discord
        static string DiscordPost(string s)
        {
            int i = s.IndexOf("\"content\":\"", StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                string t = s.Substring(i + 11);
                int e = t.IndexOf("\",\"", StringComparison.Ordinal);
                if (e > 0) t = t.Substring(0, e);
                string who = "";
                Match m = Regex.Match(s, "\"username\":\"([^\"]{1,40})\"");
                if (m.Success) who = "@" + m.Groups[1].Value + ": ";
                return "Message  ·  " + who + t;
            }
            return s;
        }

        void ModDiscord()
        {
            string[] roots =
            {
                Path.Combine(AD, "discord"), Path.Combine(AD, "discordcanary"), Path.Combine(AD, "discordptb"),
                Path.Combine(AD, @"vesktop\sessionData")
            };
            string[] subs = { @"Cache\Cache_Data", @"Local Storage\leveldb", "Session Storage", "IndexedDB" };
            int found = 0, i = 0;
            foreach (string r in roots)
            {
                P(i++, roots.Length);
                if (Cancel) return;
                if (!Directory.Exists(r)) continue;
                found++;
                string label = "Discord › " + Path.GetFileName(r.EndsWith("sessionData") ? Path.GetDirectoryName(r) : r);
                foreach (string s in subs)
                {
                    string d = Path.Combine(r, s);
                    if (!Directory.Exists(d)) continue;
                    if (s.StartsWith("Cache"))
                    {
                        ScanCache(d, label + " › cache", 500L << 20, true);
                        continue;
                    }
                    Walk(d, 3, null, delegate(string f)
                    {
                        Act(label + " › " + Path.GetFileName(f));
                        ScanFile(f, Ctx(label + " › " + s, 1, false, DiscordPost, 250, 300), 60L << 20);
                    }, null);
                }
            }
            if (found == 0) Add(0, "Discord", "Discord", "Not found", "No Discord desktop data folder found for this user.");
        }

        // ---- 11. FiveM
        void ModFiveM()
        {
            string app = Path.Combine(LAD, @"FiveM\FiveM.app");
            if (!Directory.Exists(app))
            {
                Add(0, "FiveM", "FiveM", "Not installed", "No FiveM.app folder found for this user.");
                return;
            }

            Act("FiveM logs");
            try
            {
                string logs = Path.Combine(app, "logs");
                if (Directory.Exists(logs))
                {
                    List<string> l = new List<string>(Directory.GetFiles(logs, "*.log"));
                    l.Sort(delegate(string a, string b) { return File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)); });
                    for (int i = 0; i < Math.Min(l.Count, 25); i++)
                    {
                        if (Cancel) return;
                        P(i, 25);
                        Act("FiveM › " + Path.GetFileName(l[i]));
                        ScanFile(l[i], Ctx("FiveM › logs › " + Path.GetFileName(l[i]), 1, false, null, 90, 150), 40L << 20);
                    }
                }
            }
            catch { }

            Act("FiveM crash reports");
            try
            {
                string cr = Path.Combine(app, "crashes");
                if (Directory.Exists(cr))
                {
                    int n = 0;
                    foreach (string f in Directory.GetFiles(cr))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext != ".txt" && ext != ".json" && ext != ".log") continue;
                        if (n++ > 60 || Cancel) break;
                        ScanFile(f, Ctx("FiveM › crashes › " + Path.GetFileName(f), 1, false, null, 90, 150), 8L << 20);
                    }
                }
            }
            catch { }

            Act("FiveM plugins");
            try
            {
                string pl = Path.Combine(app, "plugins");
                if (Directory.Exists(pl))
                    foreach (string f in Directory.GetFiles(pl, "*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext == ".dll" || ext == ".asi")
                            Add(2, Rules.Tool, "FiveM › plugins", Path.GetFileName(f), f + FileStamp(f) + "   ·   review: plugins folder is normally empty. Graphics proxies (dxgi/d3d11) are often ReShade, but the same trick is used for injection.");
                    }
            }
            catch { }

            Act("Game folder");
            try
            {
                string game = "";
                string ini = Path.Combine(app, "CitizenFX.ini");
                if (File.Exists(ini))
                    foreach (string line in File.ReadAllLines(ini))
                        if (line.StartsWith("IVPath=", StringComparison.OrdinalIgnoreCase)) game = line.Substring(7).Trim();
                if (game.Length > 0 && Directory.Exists(game))
                    foreach (string f in Directory.GetFiles(game))
                    {
                        string n = Path.GetFileName(f).ToLowerInvariant();
                        NameCheck(f, "GTA V folder", 1);
                        if (n.EndsWith(".asi") || n == "dinput8.dll" || n.StartsWith("scripthookv"))
                            Add(1, Rules.Tool, "GTA V folder", n, f + FileStamp(f) + "   ·   ASI/hook loader (common for single-player mods).");
                    }
            }
            catch { }
        }

        // ================================================================== REPORT
        public string BuildReport()
        {
            List<Finding> l = Snapshot();
            l.Sort(delegate(Finding a, Finding b) { return b.Sev != a.Sev ? b.Sev.CompareTo(a.Sev) : a.Time.CompareTo(b.Time); });
            StringBuilder sb = new StringBuilder();
            string[] v = { "CLEAN", "SUSPICIOUS", "FLAGGED" };
            sb.AppendLine("AC NOXXER  -  scan report");
            sb.AppendLine("============================================================");
            sb.AppendLine("Machine   : " + Environment.MachineName);
            sb.AppendLine("User      : " + Environment.UserName);
            sb.AppendLine("Started   : " + Started.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Duration  : " + (Ended - Started).ToString(@"mm\:ss"));
            sb.AppendLine("Elevated  : " + (Admin ? "yes" : "no"));
            sb.AppendLine("Verdict   : " + v[Verdict()]);
            sb.AppendLine("High/Med/Low : " + TotalHigh + " / " + TotalMed + " / " + TotalLow);
            sb.AppendLine("Files scanned: " + Files);
            sb.AppendLine();
            string[] names = { "INFO", "LOW", "MEDIUM", "HIGH" };
            for (int s = 3; s >= 0; s--)
            {
                bool head = false;
                foreach (Finding f in l)
                {
                    if (f.Sev != s) continue;
                    if (!head) { sb.AppendLine("--- " + names[s] + " ---"); head = true; }
                    sb.AppendLine("[" + names[s] + "] " + f.Type + "  |  " + f.Source + "  |  match: " + f.Match);
                    sb.AppendLine("    " + f.Detail);
                }
                if (head) sb.AppendLine();
            }
            sb.AppendLine("Matches are indicators, not proof. Review each item in context.");
            return sb.ToString();
        }
    }
}
