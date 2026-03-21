using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension.Services
{
    // ======================================================================//
    //  Result DTOs — tương thích C# 7.0 / .NET 4.6 (không dùng ValueTuple) //
    // ======================================================================//

    public sealed class JsonParseResult
    {
        public bool    Valid  { get; set; }
        public JObject Obj    { get; set; }
        public string  Error  { get; set; }
        public static JsonParseResult Ok(JObject obj)    => new JsonParseResult { Valid = true,  Obj = obj };
        public static JsonParseResult Fail(string error) => new JsonParseResult { Valid = false, Error = error };
    }

    public sealed class JsonFormatResult
    {
        public string Result { get; set; }
        public string Error  { get; set; }
        public bool   IsOk   => Error == null;
        public static JsonFormatResult Ok(string result) => new JsonFormatResult { Result = result };
        public static JsonFormatResult Fail(string error) => new JsonFormatResult { Error = error };
    }

    public sealed class JsonSaveResult
    {
        public bool                  Success     { get; set; }
        public IReadOnlyList<string> ChangedKeys { get; set; }
        public string                Error       { get; set; }
        public static JsonSaveResult Ok(IReadOnlyList<string> keys) => new JsonSaveResult { Success = true, ChangedKeys = keys };
        public static JsonSaveResult Fail(string error) => new JsonSaveResult { Success = false, ChangedKeys = new List<string>(), Error = error };
    }

    public sealed class JsonResetResult
    {
        public bool   Success { get; set; }
        public string Error   { get; set; }
        public static JsonResetResult Ok()           => new JsonResetResult { Success = true };
        public static JsonResetResult Fail(string e) => new JsonResetResult { Error = e };
    }

    // ======================================================================//
    //  JsonConfigService                                                      //
    // ======================================================================//

    /// <summary>
    /// Dịch vụ cấu hình dạng JSON.
    /// Lưu settings dưới dạng JSON object trong %AppData%\DhCodetaskExtension\.
    /// Hỗ trợ dot-path access (e.g. "Advanced.RetryCount"),
    /// live diff logging khi save, và API cho editor dialog.
    ///
    /// HOW TO CUSTOMIZE:
    ///   - Đổi AppDataFolderName và ConfigFileName
    ///   - Cập nhật DefaultJson với schema của bạn
    ///   - Thêm typed convenience properties cho các key của bạn
    /// </summary>
    public sealed class JsonConfigService
    {
        private const string AppDataFolderName = "DhCodetaskExtension";
        private const string ConfigFileName    = "DhCodetaskExtension.json";

        /// <summary>
        /// JSON schema mặc định.
        /// TODO: Thay bằng schema cấu hình của extension bạn.
        /// </summary>
        public static readonly string DefaultJson = JsonConvert.SerializeObject(
            new
            {
                ServerUrl       = "https://api.example.com/v1",
                ApiKey          = "",
                TimeoutSeconds  = 30,
                MaxResults      = 50,
                RefreshInterval = 5,
                OutputFormat    = "JSON",
                EnableLogging   = true,
                DebugMode       = false,
                ShowStatusBar   = true,
                Tags            = new[] { "vsix", "dh-codetask" },
                Advanced        = new
                {
                    RetryCount   = 3,
                    RetryDelayMs = 500,
                    UserAgent    = "DhCodetaskExtension/1.0"
                }
            },
            Formatting.Indented);

        // ------------------------------------------------------------------ //
        //  Fields                                                              //
        // ------------------------------------------------------------------ //

        private readonly AsyncPackage        _package;
        private readonly OutputWindowService _outputWindow;
        private string  _configFilePath;
        private JObject _current;

        // ------------------------------------------------------------------ //
        //  Events                                                              //
        // ------------------------------------------------------------------ //

        /// <summary>Fired sau khi Save thành công. Payload: danh sách key đã thay đổi.</summary>
        public event Action<IReadOnlyList<string>> ConfigSaved;

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public JsonConfigService(AsyncPackage package, OutputWindowService outputWindow)
        {
            _package      = package      ?? throw new ArgumentNullException(nameof(package));
            _outputWindow = outputWindow ?? throw new ArgumentNullException(nameof(outputWindow));
        }

        // ================================================================== //
        //  Initialization                                                      //
        // ================================================================== //

        public async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder  = Path.Combine(appData, AppDataFolderName);
            Directory.CreateDirectory(folder);
            _configFilePath = Path.Combine(folder, ConfigFileName);
            _current = LoadFromFile();
            _outputWindow.Log($"[JsonConfig] Initialized. File: {_configFilePath}");
        }

        // ================================================================== //
        //  Read API                                                            //
        // ================================================================== //

        public JObject GetSnapshot()      => (JObject)_current.DeepClone();
        public string  ConfigFilePath     => _configFilePath ?? "(not initialized)";
        public string  GetCurrentJson(Formatting fmt = Formatting.Indented) => _current.ToString(fmt);

        public T Get<T>(string dotPath, T fallback)
        {
            try
            {
                JToken token = ResolvePath(_current, dotPath);
                return token == null ? fallback : token.ToObject<T>();
            }
            catch { return fallback; }
        }

        // Typed convenience properties
        public string ServerUrl      => Get<string>("ServerUrl",      "https://api.example.com/v1");
        public string ApiKey         => Get<string>("ApiKey",         "");
        public int    TimeoutSeconds => Get<int>   ("TimeoutSeconds", 30);
        public int    MaxResults     => Get<int>   ("MaxResults",     50);
        public int    RefreshInterval=> Get<int>   ("RefreshInterval",5);
        public string OutputFormat   => Get<string>("OutputFormat",   "JSON");
        public bool   EnableLogging  => Get<bool>  ("EnableLogging",  true);
        public bool   DebugMode      => Get<bool>  ("DebugMode",      false);
        public bool   ShowStatusBar  => Get<bool>  ("ShowStatusBar",  true);

        // ================================================================== //
        //  Validate & Format                                                   //
        // ================================================================== //

        public JsonParseResult TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return JsonParseResult.Fail("JSON string is empty.");
            try   { return JsonParseResult.Ok(JObject.Parse(json)); }
            catch (JsonException ex) { return JsonParseResult.Fail("JSON parse error: " + ex.Message); }
        }

        public JsonFormatResult FormatJson(string json)
        {
            var parsed = TryParse(json);
            return parsed.Valid
                ? JsonFormatResult.Ok(parsed.Obj.ToString(Formatting.Indented))
                : JsonFormatResult.Fail(parsed.Error);
        }

        // ================================================================== //
        //  Save                                                               //
        // ================================================================== //

        public JsonSaveResult Save(string newJson)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var parsed = TryParse(newJson);
            if (!parsed.Valid) return JsonSaveResult.Fail(parsed.Error);

            JObject newObj = parsed.Obj;
            List<string> changedKeys = DiffKeys(_current, newObj);

            _outputWindow.Log("[JsonConfig] ── Save ──────────────────────");
            if (changedKeys.Count == 0)
            {
                _outputWindow.Log("[JsonConfig] No changes detected.");
            }
            else
            {
                _outputWindow.Log($"[JsonConfig] {changedKeys.Count} key(s) changed:");
                foreach (string key in changedKeys)
                {
                    string oldVal = GetFlatValue(_current, key);
                    string newVal = GetFlatValue(newObj,   key);
                    if (key.IndexOf("ApiKey", StringComparison.OrdinalIgnoreCase) >= 0)
                    { oldVal = Mask(oldVal); newVal = Mask(newVal); }
                    _outputWindow.Log($"[JsonConfig]   {key}: \"{oldVal}\" → \"{newVal}\"");
                }
            }

            try
            {
                File.WriteAllText(_configFilePath, newObj.ToString(Formatting.Indented), Encoding.UTF8);
                _outputWindow.Log($"[JsonConfig] Saved → {_configFilePath}");
            }
            catch (Exception ex)
            {
                string msg = $"[JsonConfig] ERROR writing file: {ex.Message}";
                _outputWindow.Log(msg);
                return JsonSaveResult.Fail(msg);
            }

            _current = newObj;
            ConfigSaved?.Invoke(changedKeys);
            return JsonSaveResult.Ok(changedKeys);
        }

        public JsonResetResult ResetToDefault()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _outputWindow.Log("[JsonConfig] Resetting to default...");
            var result = Save(DefaultJson);
            return result.Success ? JsonResetResult.Ok() : JsonResetResult.Fail(result.Error);
        }

        // ================================================================== //
        //  Private helpers                                                     //
        // ================================================================== //

        private JObject LoadFromFile()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!File.Exists(_configFilePath))
            {
                _outputWindow.Log("[JsonConfig] File not found – using defaults.");
                try { File.WriteAllText(_configFilePath, DefaultJson, Encoding.UTF8); } catch { }
                return JObject.Parse(DefaultJson);
            }
            try
            {
                var obj = JObject.Parse(File.ReadAllText(_configFilePath, Encoding.UTF8));
                _outputWindow.Log("[JsonConfig] Loaded from file.");
                return obj;
            }
            catch (Exception ex)
            {
                _outputWindow.Log($"[JsonConfig] ERROR loading: {ex.Message} – using defaults.");
                return JObject.Parse(DefaultJson);
            }
        }

        private static List<string> DiffKeys(JObject oldObj, JObject newObj)
        {
            var oldFlat = Flatten(oldObj, "");
            var newFlat = Flatten(newObj, "");
            var changed = new List<string>();
            foreach (var kv in newFlat)
            {
                string oldVal;
                if (!oldFlat.TryGetValue(kv.Key, out oldVal) || oldVal != kv.Value)
                    changed.Add(kv.Key);
            }
            foreach (var kv in oldFlat)
                if (!newFlat.ContainsKey(kv.Key)) changed.Add(kv.Key + " (removed)");
            return changed;
        }

        private static Dictionary<string, string> Flatten(JObject obj, string prefix)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (JProperty prop in obj.Properties())
            {
                string key = string.IsNullOrEmpty(prefix) ? prop.Name : prefix + "." + prop.Name;
                JObject childObj = prop.Value as JObject;
                JArray  childArr = prop.Value as JArray;
                if (childObj != null)
                    foreach (var kv in Flatten(childObj, key)) result[kv.Key] = kv.Value;
                else if (childArr != null)
                    result[key] = childArr.ToString(Formatting.None);
                else
                    result[key] = prop.Value != null ? prop.Value.ToString() : "null";
            }
            return result;
        }

        private static string GetFlatValue(JObject obj, string flatKey)
        {
            string clean = flatKey.Replace(" (removed)", "");
            var flat = Flatten(obj, "");
            string val;
            return flat.TryGetValue(clean, out val) ? val : "(deleted)";
        }

        private static JToken ResolvePath(JObject obj, string dotPath)
        {
            JToken current = obj;
            foreach (string part in dotPath.Split('.'))
            {
                var jo = current as JObject;
                if (jo == null) return null;
                JToken next;
                if (!jo.TryGetValue(part, StringComparison.OrdinalIgnoreCase, out next)) return null;
                current = next;
            }
            return current;
        }

        private static string Mask(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            if (s.Length <= 4)           return "****";
            return "****" + s.Substring(s.Length - 4);
        }
    }
}
