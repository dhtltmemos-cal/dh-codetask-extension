using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DhCodetaskExtension.Core.Interfaces;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using Newtonsoft.Json.Linq;

namespace DhCodetaskExtension.Providers.TaskProviders
{
    public sealed class GiteaTaskProvider : ITaskProvider
    {
        private readonly Func<AppSettings> _settingsProvider;
        // Fix #7: use per-request CancellationToken for timeout, not HttpClient.Timeout
        private static readonly HttpClient _http = new HttpClient();

        public GiteaTaskProvider(Func<AppSettings> settingsProvider)
        {
            _settingsProvider = settingsProvider
                ?? throw new ArgumentNullException(nameof(settingsProvider));
        }

        public bool CanHandle(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            var settings = _settingsProvider();
            if (string.IsNullOrEmpty(settings?.GiteaBaseUrl)) return false;
            return url.StartsWith(settings.GiteaBaseUrl, StringComparison.OrdinalIgnoreCase)
                && ParseIssueUrl(url, settings.GiteaBaseUrl, out _, out _, out _);
        }

        public async Task<TaskFetchResult> FetchAsync(
            string url, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var settings = _settingsProvider();
                if (!ParseIssueUrl(url, settings.GiteaBaseUrl, out var owner, out var repo, out var number))
                    return TaskFetchResult.Fail("Cannot parse issue URL from Gitea base URL.");

                string apiUrl = string.Format("{0}/api/v1/repos/{1}/{2}/issues/{3}",
                    settings.GiteaBaseUrl.TrimEnd('/'), owner, repo, number);

                // Fix #7: configurable timeout via linked CancellationTokenSource
                int timeoutSec = settings.GiteaTimeoutSeconds > 0 ? settings.GiteaTimeoutSeconds : 10;
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

                    using (var req = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                    {
                        if (!string.IsNullOrEmpty(settings.GiteaToken))
                            req.Headers.Authorization =
                                new AuthenticationHeaderValue("token", settings.GiteaToken);

                        var resp = await _http.SendAsync(req, timeoutCts.Token);

                        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                            return TaskFetchResult.Fail("401 Unauthorized — kiem tra lai Gitea Token.");
                        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                            return TaskFetchResult.Fail("404 Not Found — issue khong ton tai hoac URL sai.");

                        resp.EnsureSuccessStatusCode();
                        var json = await resp.Content.ReadAsStringAsync();
                        var obj  = JObject.Parse(json);

                        var labels = new System.Collections.Generic.List<string>();
                        foreach (var lbl in obj["labels"] ?? new JArray())
                            labels.Add(lbl["name"]?.ToString() ?? string.Empty);

                        string rawBody = obj["body"]?.ToString() ?? string.Empty;
                        string desc    = rawBody.Length > 500 ? rawBody.Substring(0, 497) + "..." : rawBody;

                        // v3.10: creator info
                        string createdByUser = string.Empty;
                        var userObj = obj["user"] as JObject;
                        if (userObj != null)
                            createdByUser = userObj["login"]?.ToString() ?? string.Empty;

                        string createdAt = string.Empty;
                        var createdAtToken = obj["created_at"];
                        if (createdAtToken != null)
                        {
                            DateTime dt;
                            if (DateTime.TryParse(createdAtToken.ToString(), out dt))
                                createdAt = dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                            else
                                createdAt = createdAtToken.ToString();
                        }

                        // v3.10: connection info from body
                        var connectionInfo = IssueBodyParser.Parse(rawBody, settings.DefaultServerIp);

                        var task = new TaskItem
                        {
                            Id             = obj["number"]?.ToString() ?? number,
                            Title          = obj["title"]?.ToString() ?? string.Empty,
                            Description    = StripMarkdown(desc),
                            Labels         = labels.ToArray(),
                            Url            = obj["html_url"]?.ToString() ?? url,
                            Owner          = owner,
                            Repo           = repo,
                            CreatedByUser  = createdByUser,
                            CreatedAt      = createdAt,
                            ConnectionInfo = connectionInfo
                        };
                        return TaskFetchResult.Ok(task);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                var s = _settingsProvider();
                int t = s.GiteaTimeoutSeconds > 0 ? s.GiteaTimeoutSeconds : 10;
                return TaskFetchResult.Fail(string.Format(
                    "Request timed out sau {0}s. Kiem tra GiteaBaseUrl hoac tang GiteaTimeoutSeconds.", t));
            }
            catch (Exception ex)
            {
                return TaskFetchResult.Fail(string.Format("Error: {0}", ex.Message));
            }
        }

        /// <summary>Fix #7: Test Gitea URL + token. Calls /api/v1/version.</summary>
        public async Task<TestConnectionResult> TestConnectionAsync()
        {
            try
            {
                var settings = _settingsProvider();
                if (string.IsNullOrWhiteSpace(settings.GiteaBaseUrl))
                    return TestConnectionResult.Fail("GiteaBaseUrl chua duoc cau hinh.");
                if (!settings.GiteaBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !settings.GiteaBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return TestConnectionResult.Fail("GiteaBaseUrl phai bat dau bang http:// hoac https://");

                int timeoutSec = settings.GiteaTimeoutSeconds > 0 ? settings.GiteaTimeoutSeconds : 10;
                string apiUrl  = string.Format("{0}/api/v1/version", settings.GiteaBaseUrl.TrimEnd('/'));

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec)))
                using (var req = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                {
                    if (!string.IsNullOrEmpty(settings.GiteaToken))
                        req.Headers.Authorization =
                            new AuthenticationHeaderValue("token", settings.GiteaToken);

                    var resp = await _http.SendAsync(req, cts.Token);

                    if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        return TestConnectionResult.Fail("401 Token khong hop le hoac khong co quyen.");
                    if (!resp.IsSuccessStatusCode)
                        return TestConnectionResult.Fail(
                            string.Format("HTTP {0} tu {1}", (int)resp.StatusCode, apiUrl));

                    var json = await resp.Content.ReadAsStringAsync();
                    var ver  = JObject.Parse(json)["version"]?.ToString() ?? "?";
                    return TestConnectionResult.Ok(string.Format("Ket noi thanh cong - Gitea v{0}", ver));
                }
            }
            catch (OperationCanceledException)
            {
                return TestConnectionResult.Fail("Timeout - server khong phan hoi. Kiem tra URL va mang.");
            }
            catch (Exception ex)
            {
                return TestConnectionResult.Fail(string.Format("Loi: {0}", ex.Message));
            }
        }

        private static bool ParseIssueUrl(string url, string baseUrl,
            out string owner, out string repo, out string number)
        {
            owner = repo = number = null;
            if (string.IsNullOrEmpty(url)) return false;
            var escaped = Regex.Escape(baseUrl.TrimEnd('/'));
            var pattern = string.Format(@"^{0}/([^/]+)/([^/]+)/issues/(\d+)", escaped);
            var m = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            owner  = m.Groups[1].Value;
            repo   = m.Groups[2].Value;
            number = m.Groups[3].Value;
            return true;
        }

        private static string StripMarkdown(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = Regex.Replace(s, @"```[\s\S]*?```", string.Empty);
            s = Regex.Replace(s, @"[*_`#>\[\]!]", string.Empty);
            return s.Trim();
        }
    }

    /// <summary>Result of GiteaTaskProvider.TestConnectionAsync()</summary>
    public sealed class TestConnectionResult
    {
        public bool   Success { get; private set; }
        public string Message { get; private set; }

        public static TestConnectionResult Ok(string msg)
            => new TestConnectionResult { Success = true,  Message = msg };
        public static TestConnectionResult Fail(string msg)
            => new TestConnectionResult { Success = false, Message = msg };
    }
}
