using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DhCodetaskExtension.Core.Interfaces;
using DhCodetaskExtension.Core.Models;

namespace DhCodetaskExtension.Providers.TaskProviders
{
    /// <summary>Fallback provider: reads HTML title from URL or creates a blank task.</summary>
    public sealed class ManualTaskProvider : ITaskProvider
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public bool CanHandle(string url) => true;

        public async Task<TaskFetchResult> FetchAsync(string url, CancellationToken cancellationToken = default(CancellationToken))
        {
            var task = new TaskItem { Url = url ?? string.Empty, Id = string.Empty };
            if (string.IsNullOrWhiteSpace(url)) return TaskFetchResult.Ok(task);
            try
            {
                var html = await _http.GetStringAsync(url);  // await, not .GetAwaiter().GetResult()
                var m = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
                if (m.Success) task.Title = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            }
            catch { /* non-critical */ }
            return TaskFetchResult.Ok(task);
        }
    }

    public sealed class TaskProviderFactory
    {
        private readonly List<ITaskProvider> _providers = new List<ITaskProvider>();

        public void Register(ITaskProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _providers.Insert(0, provider);
        }

        public async Task<TaskFetchResult> FetchAsync(string url, CancellationToken ct = default(CancellationToken))
        {
            foreach (var p in _providers)
            {
                try { if (p.CanHandle(url)) return await p.FetchAsync(url, ct); }
                catch { }
            }
            return await new ManualTaskProvider().FetchAsync(url, ct);  // await, not sync block
        }
    }
}
