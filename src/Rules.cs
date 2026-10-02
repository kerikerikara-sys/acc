using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Noxxer
{
    public class Rule
    {
        public string Pat;    // lower-case ASCII pattern
        public string Pat16;  // same pattern interleaved with \0 (UTF-16LE)
        public string Cat;
        public int Sev;       // 3 = high, 2 = medium, 1 = low
        public bool Word;     // require word boundaries
    }

    public static class Rules
    {
        public const string Cheat = "Cheat Software";
        public const string Dma = "DMA Hardware";
        public const string Bypass = "Anti-Forensics / Bypass";
        public const string Chat = "Chat / Intent";
        public const string Tool = "Suspicious Tool";
        public const string Custom = "Custom Keyword";

        public static readonly List<Rule> All = new List<Rule>();

        // Prefix a pattern with '!' to require whole-word matching.
        static void A(string cat, int sev, params string[] pats)
        {
            foreach (string raw in pats)
            {
                string p = raw;
                bool w = p.StartsWith("!");
                if (w) p = p.Substring(1);
                Add(cat, sev, p, w);
            }
        }

        // URLs and slugs use + - _ as word separators; treat them all as spaces on both sides of the match.
        public static string Norm(string s)
        {
            return s.Replace('+', ' ').Replace('-', ' ').Replace('_', ' ');
        }

        static void Add(string cat, int sev, string p, bool word)
        {
            p = Norm(p.ToLowerInvariant());
            foreach (Rule ex in All) if (ex.Pat == p && ex.Word == word) return;   // e.g. "red-engine" == "red engine"
            StringBuilder sb = new StringBuilder();
            foreach (char c in p) { sb.Append(c); sb.Append('\0'); }
            All.Add(new Rule { Pat = p, Pat16 = sb.ToString(), Cat = cat, Sev = sev, Word = word });
        }

        static Rules()
        {
            // ---- Named FiveM cheat products -------------------------------------------------
            // "Eulen" alone is also a German word (owls), so the bare name is only medium.
            A(Cheat, 2, "!eulen");
            A(Cheat, 3, "eulen.cc", "eulenloader", "eulen loader", "eulen cheats", "eulen fivem", "eulen menu",
                "redengine", "red engine", "red-engine",
                "skript.gg", "skriptgg",
                "hxsoftware", "hx software", "hx-software",
                "!susano", "!desudo",
                "lynxmenu", "lynx menu", "lynx cheats",
                "kiddion", "!kiddions");

            // ---- Generic FiveM cheat terms ---------------------------------------------------
            A(Cheat, 2, "fivem cheat", "fivem hack", "fivem executor", "lua executor", "fivem spoofer",
                "fivem bypass", "fivem aimbot", "fivem esp", "fivem mod menu", "fivem cheat menu",
                "fivem injector", "fivem unban", "fivem hwid", "fivem dumper", "fivem undetected",
                "undetected fivem", "undetected cheat", "hwid spoofer", "hwid changer", "hwid bypass",
                "anticheat bypass", "anti-cheat bypass", "txadmin bypass", "ban bypass",
                "!aimbot", "!triggerbot", "trigger bot", "silent aim", "silentaim", "!wallhack",
                "noclip menu", "resource dumper", "resource stopper", "cheat menu", "external cheat",
                "internal cheat", "extreme injector", "kdmapper", "drvmap", "!spoofer",
                "manual map injector", "cheatengine", "cheat engine");

            A(Tool, 1, "!undetected", "process hacker", "processhacker", "system informer", "x64dbg",
                "dll injector", "!injector", "!executor");

            // ---- DMA -------------------------------------------------------------------------
            A(Dma, 3, "pcileech", "leechcore", "memprocfs", "pciescreamer", "pcie screamer", "screamer m2",
                "screamerm2", "squirrel dma", "dma card", "dma cheat", "dma fivem", "fivem dma",
                "dma firmware", "dma fuser", "dma radar", "dma bypass", "dma spoofer", "dma setup",
                "kmbox", "kmboxnet", "captain dma", "enigma x1", "neutron dma", "raptor dma", "pcie dma",
                "fpga cheat", "ftd3xx", "ft601");
            A(Dma, 2, "vmm.dll");
            A(Dma, 1, "!xilinx", "artix-7", "artix 7");

            // ---- Anti-forensics / screenshare bypass -----------------------------------------
            A(Bypass, 3, "fsutil usn deletejournal", "usn deletejournal", "wevtutil cl ", "wevtutil.exe cl ");
            A(Bypass, 2, "clear-eventlog", "ss bypass", "screenshare bypass", "screen share bypass",
                "bypass screenshare", "bypass ss", "anti ss", "anti-ss", "ss proof", "clean before ss",
                "clean pc before ss", "hide cheats", "cheat hider", "timestomp", "delete prefetch",
                "clear prefetch", "prefetch delete");
            A(Bypass, 1, "sdelete");

            // ---- Chat / intent ----------------------------------------------------------------
            A(Chat, 2, "buy eulen", "buy redengine", "buy cheat", "buy fivem cheat", "cheat license",
                "cheat key", "cheat config", "cheat lifetime", "cheat reseller", "fivem cheat seller",
                "is it undetected", "still undetected", "ud status", "hwid ban", "unban service",
                "unban fivem");
            A(Chat, 1, "ban wave");

            LoadCustom();
        }

        // Optional noxxer_keywords.txt next to the exe: one keyword per line.
        // Prefix H: = high, L: = low, default = medium. Lines starting with # are ignored.
        static void LoadCustom()
        {
            try
            {
                string f = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "noxxer_keywords.txt");
                if (!File.Exists(f)) return;
                foreach (string line in File.ReadAllLines(f))
                {
                    string t = line.Trim();
                    if (t.Length < 3 || t.StartsWith("#")) continue;
                    int sev = 2;
                    if (t.StartsWith("H:", StringComparison.OrdinalIgnoreCase)) { sev = 3; t = t.Substring(2).Trim(); }
                    else if (t.StartsWith("L:", StringComparison.OrdinalIgnoreCase)) { sev = 1; t = t.Substring(2).Trim(); }
                    bool word = t.StartsWith("!");
                    if (word) t = t.Substring(1);
                    if (t.Length >= 3) Add(Custom, sev, t, word);
                }
            }
            catch { }
        }
    }

    public static class Matcher
    {
        static bool IsWord(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c >= 128;
        }

        // hay must already be lower-cased. Finds the next occurrence honouring word boundaries.
        public static bool Find(string hay, Rule r, int from, bool u16, out int idx)
        {
            string p = u16 ? r.Pat16 : r.Pat;
            int step = u16 ? 2 : 1;
            idx = -1;
            while (from <= hay.Length - p.Length)
            {
                int i = hay.IndexOf(p, from, StringComparison.Ordinal);
                if (i < 0) return false;
                if (!r.Word) { idx = i; return true; }
                bool okBefore = i - step < 0 || !IsWord(hay[i - step]);
                int a = i + p.Length;
                bool okAfter = a >= hay.Length || !IsWord(hay[a]);
                if (okBefore && okAfter) { idx = i; return true; }
                from = i + 1;
            }
            return false;
        }

        public static List<Rule> MatchName(string name, int minSev)
        {
            List<Rule> res = new List<Rule>();
            if (string.IsNullOrEmpty(name)) return res;
            string lower = Rules.Norm(name.ToLowerInvariant());
            foreach (Rule r in Rules.All)
            {
                if (r.Sev < minSev) continue;
                int idx;
                if (Find(lower, r, 0, false, out idx)) res.Add(r);
            }
            return res;
        }
    }
}
