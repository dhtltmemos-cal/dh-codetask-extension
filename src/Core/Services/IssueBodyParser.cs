using System;
using System.Text.RegularExpressions;
using DhCodetaskExtension.Core.Models;

namespace DhCodetaskExtension.Core.Services
{
    /// <summary>
    /// Trich xuat IP, database, port tu body cua Gitea issue.
    ///
    /// 3 tang xu ly theo thu tu uu tien:
    ///   Tier 1 — Badge shields.io : ![keyword_in_alt](url) `value`
    ///   Tier 2 — Inline backtick  : keyword:`value`
    ///   Tier 3 — Plain text       : keyword: value  (sau khi strip HTML/Markdown)
    ///
    /// v3.13: Ho tro template DH-His dung shields.io badge.
    /// </summary>
    public static class IssueBodyParser
    {
        // ═══════════════════════════════════════════════════════════════════
        // TIER 1 — Shields.io badge format
        // Pattern: ![alt-contains-keyword](url) `VALUE`
        // ═══════════════════════════════════════════════════════════════════

        // DB: alt text chua ten_csdl / co_so_du_lieu / database
        private static readonly Regex DbBadge = new Regex(
            @"!\[[^\]]*(?:ten[_\s]*csdl|c[o\u00f4\u01a1][_\s]*s[o\u1edf][_\s]*d[u\u01b0][_\s]*li[e\u1ec7]u|database)[^\]]*\]\([^)]+\)\s*`([^`]+)`",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Port: alt chua port
        private static readonly Regex PortBadge = new Regex(
            @"!\[[^\]]*port[^\]]*\]\([^)]+\)\s*`(\d{2,5})`",
            RegexOptions.IgnoreCase);

        // IP: alt chua ip / may_chu / host
        private static readonly Regex IpBadge = new Regex(
            @"!\[[^\]]*(?:ip|m[a\u00e1]y[_\s]*ch[u\u1ee7]|host)[^\]]*\]\([^)]+\)\s*`((?:\d{1,3}\.){3}\d{1,3})`",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // ═══════════════════════════════════════════════════════════════════
        // TIER 2 — Inline backtick: keyword:`value` hoac keyword: `value`
        // Vi du: IP May chu:`192.168.50.79`
        // ═══════════════════════════════════════════════════════════════════

        private static readonly Regex IpInline = new Regex(
            @"(?:ip|m[a\u00e1]y\s*ch[u\u1ee7]|host)[^`\n]{0,25}`((?:\d{1,3}\.){3}\d{1,3})`",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex PortInline = new Regex(
            @"port[^`\n]{0,25}`(\d{2,5})`",
            RegexOptions.IgnoreCase);

        private static readonly Regex DbInline = new Regex(
            @"(?:ten[_\s]*csdl|database|csdl)\s*[:\-=]?\s*`([a-zA-Z0-9][a-zA-Z0-9_\-]{1,64})`",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // ═══════════════════════════════════════════════════════════════════
        // TIER 3 — Plain text patterns (applied after StripHtml)
        // ═══════════════════════════════════════════════════════════════════

        private static readonly Regex IpPlainKeyword = new Regex(
            @"(?:ip[\s_]*(?:server|host|m[a\u00e1]y)?|host|m[a\u00e1]y[\s_]*ch[u\u1ee7])\s*[:\-=]?\s*((?:\d{1,3}\.){3}\d{1,3})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex IpPlainFallback = new Regex(
            @"\b((?:\d{1,3}\.){3}\d{1,3})\b");

        private static readonly Regex DbPlain = new Regex(
            @"(?:ten[\s_]*csdl|database|csdl|db)\s*[:\-=]?\s*([a-zA-Z0-9][a-zA-Z0-9_\-]{1,64})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Port plain: cho phep gap lon giua keyword va gia tri (vi badge URL bi strip)
        private static readonly Regex PortPlainWide = new Regex(
            @"port[\s_]*(?:csdl|db|database)?\b[^0-9\n]{0,100}(\d{2,5})\b",
            RegexOptions.IgnoreCase);

        private static readonly Regex PortPlainTight = new Regex(
            @"(?:port|cong)\s*[:\-=]?\s*(\d{2,5})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // ═══════════════════════════════════════════════════════════════════
        // HTML/Markdown stripper
        // NOTE: chi strip ** __ ``` (multi-char), KHONG strip don _ de giu
        //       ky tu gach duoi trong ten database (vi du: tra_minhquang_full)
        // ═══════════════════════════════════════════════════════════════════

        private static readonly Regex HtmlBlock  = new Regex(
            @"<br\s*/?>|</(?:p|div|tr|li|h\d)>", RegexOptions.IgnoreCase);
        private static readonly Regex HtmlTag    = new Regex(@"<[^>]+>");
        private static readonly Regex HtmlEntity = new Regex(@"&(?:#\d+|[a-zA-Z]+);");
        private static readonly Regex MdMulti    = new Regex(@"\*{2,3}|_{2,3}|`{1,3}");
        private static readonly Regex MultiSpace = new Regex(@"[ \t]{2,}");

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            var s = HtmlBlock.Replace(html, "\n");
            s = HtmlTag.Replace(s, " ");
            s = s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                  .Replace("&nbsp;", " ").Replace("&quot;", "\"").Replace("&#39;", "'");
            s = HtmlEntity.Replace(s, " ");
            s = MdMulti.Replace(s, " ");   // strip ** __ ``` but NOT single _
            s = MultiSpace.Replace(s, " ");
            return s.Trim();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════════════════════════════

        public static IssueConnectionInfo Parse(string body, string defaultServerIp = null)
        {
            var result = new IssueConnectionInfo();
            if (string.IsNullOrEmpty(body)) return result;

            var plain = StripHtml(body);

            result.IpServer = FirstOf(body,  IpBadge, IpInline)
                           ?? FirstOf(plain, IpPlainKeyword, IpPlainFallback)
                           ?? string.Empty;

            result.Database = FirstOf(body,  DbBadge, DbInline)
                           ?? FirstOf(plain, DbPlain)
                           ?? string.Empty;

            result.Port     = FirstOf(body,  PortBadge, PortInline)
                           ?? FirstOf(plain, PortPlainWide, PortPlainTight)
                           ?? string.Empty;

            // Validate port 1-65535
            if (!string.IsNullOrEmpty(result.Port))
            {
                int portNum;
                if (!int.TryParse(result.Port, out portNum) || portNum < 1 || portNum > 65535)
                    result.Port = string.Empty;
            }

            // Fallback IP from settings
            if (string.IsNullOrEmpty(result.IpServer) && !string.IsNullOrEmpty(defaultServerIp))
                result.IpServer = defaultServerIp;

            return result;
        }

        private static string FirstOf(string text, params Regex[] patterns)
        {
            foreach (var p in patterns)
            {
                var m = p.Match(text);
                if (m.Success && m.Groups.Count > 1)
                {
                    var v = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            return null;
        }
    }
}
