using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GoogleSheetsConnection
{
    public class SheetLogRunner : MonoBehaviour
    {
        class Batch
        {
            public readonly SheetTarget Target;
            public readonly List<string> Rows = new List<string>();
            public Batch(SheetTarget t) { Target = t; }

            public bool Matches(SheetTarget t)
            {
                return Target.Url == t.Url && Target.Secret == t.Secret && Target.Tab == t.Tab;
            }
        }

        static SheetLogRunner _instance;
        static bool _quitting;

        readonly List<Batch> _pending = new List<Batch>();
        readonly List<string> _globalKeys = new List<string>();
        readonly List<string> _globalValues = new List<string>();
        readonly StringBuilder _sb = new StringBuilder(4096);

        string _globalsFragment = "";
        bool _sending;
        bool _flushRequested;

        public static SheetLogRunner Instance
        {
            get
            {
                if (_instance == null && !_quitting)
                {
                    Bootstrap();
                }

                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("[SheetLog]");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SheetLogRunner>();
        }

        void Awake()
        {
            _instance = this;
            StartCoroutine(FlushLoop());
        }

        void OnApplicationQuit() { _quitting = true; }
        void OnApplicationPause(bool p) { if (p) RequestFlush(); }
        void OnApplicationFocus(bool f) { if (!f) RequestFlush(); }

        public void SetGlobal(string key, object value)
        {
            int i = _globalKeys.IndexOf(key);
            string v = value == null ? "" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);

            if (i >= 0)
            {
                _globalValues[i] = v;
            }
            else
            {
                _globalKeys.Add(key);
                _globalValues.Add(v);
            }

            RebuildGlobals();
        }

        public void ClearGlobals()
        {
            _globalKeys.Clear();
            _globalValues.Clear();
            _globalsFragment = "";
        }

        void RebuildGlobals()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _globalKeys.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                AppendEscaped(sb, _globalKeys[i]);
                sb.Append(':');
                AppendEscaped(sb, _globalValues[i]);   // always a string; sheets coerce numerics anyway
            }

            _globalsFragment = sb.ToString();
        }

        string ApplyGlobals(string rowJson)
        {
            if (string.IsNullOrEmpty(_globalsFragment))
            {
                return rowJson;
            }

            int brace = rowJson.IndexOf('{');
            if (brace < 0)
            {
                return rowJson;
            }

            return "{" + _globalsFragment + "," + rowJson.Substring(brace + 1);
        }

        public void Enqueue(SheetTarget target, string rowJson)
        {
            if (!target.IsValid)
            {
                Debug.LogError("[SheetLog] invalid target: " + target + ". URL must be https and end in /exec, tab must be set.");
                return;
            }

            Batch batch = null;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Matches(target)) { batch = _pending[i]; break; }
            }

            if (batch == null)
            {
                batch = new Batch(target);
                _pending.Add(batch);
            }

            batch.Rows.Add(ApplyGlobals(rowJson));
        }

        public void RequestFlush()
        {
            _flushRequested = true;
        }

        public void SendNow(SheetTarget target, string rowJson, Action<bool, string> onDone)
        {
            if (!target.IsValid)
            {
                if (onDone != null)
                {
                    onDone(false, "invalid target");
                }

                return;
            }
            StartCoroutine(Post(target, new List<string> { ApplyGlobals(rowJson) }, onDone));
        }

        public int PendingRowCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _pending.Count; i++)
                {
                    n += _pending[i].Rows.Count;
                }

                return n;
            }
        }

        IEnumerator FlushLoop()
        {
            while (true)
            {
                float t = 0f;
                while (t < SheetLog.FlushInterval && !_flushRequested)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                _flushRequested = false;

                if (PendingRowCount > 0)
                {
                    yield return FlushRoutine();
                }
            }
        }

        IEnumerator FlushRoutine()
        {
            if (_sending)
            {
                yield break;
            }

            _sending = true;

            for (int i = 0; i < _pending.Count; i++)
            {
                var batch = _pending[i];

                while (batch.Rows.Count > 0)
                {
                    int take = Mathf.Min(batch.Rows.Count, Mathf.Max(1, SheetLog.MaxRowsPerSend));
                    var slice = batch.Rows.GetRange(0, take);

                    bool ok = false;
                    yield return Post(batch.Target, slice, (success, body) => ok = success);

                    if (!ok)
                    {
                        break;                     // keep rows, retry next interval
                    }

                    batch.Rows.RemoveRange(0, take);    // drop only on confirmed success
                }
            }

            _sending = false;
        }

        IEnumerator Post(SheetTarget target, List<string> rowsJson, Action<bool, string> onDone)
        {
            byte[] body = Encoding.UTF8.GetBytes(BuildEnvelope(target, rowsJson));
            string lastError = "";

            for (int attempt = 0; attempt <= Mathf.Max(0, SheetLog.MaxRetries); attempt++)
            {
                if (attempt > 0)
                {
                    yield return new WaitForSecondsRealtime(Mathf.Pow(2f, attempt));   // 2s, 4s
                }

                using (var req = new UnityWebRequest(target.Url, UnityWebRequest.kHttpVerbPOST))
                {
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.downloadHandler = new DownloadHandlerBuffer();

                    req.SetRequestHeader("Content-Type", "text/plain;charset=utf-8");
                    req.timeout = SheetLog.TimeoutSeconds;

                    yield return req.SendWebRequest();

                    bool transportOk = req.result == UnityWebRequest.Result.Success;
                    string text = transportOk ? req.downloadHandler.text : req.error;

                    if (transportOk && text != null && text.Contains("\"ok\":true"))
                    {
                        if (SheetLog.VerboseLogging)
                        {
                            Debug.Log("[SheetLog] wrote " + rowsJson.Count + " row(s) to '" + target.Tab + "'.", gameObject);
                        }

                        if (onDone != null)
                        {
                            onDone(true, text);
                        }

                        yield break;
                    }

                    lastError = text;

                    if (transportOk && text != null &&
                        (text.Contains("unauthorized") || text.Contains("bad json")))
                    {
                        break;
                    }
                }
            }

            Debug.LogWarning("[SheetLog] failed writing to '" + target.Tab + "': " + lastError);
            if (onDone != null)
            {
                onDone(false, lastError);
            }
        }

        string BuildEnvelope(SheetTarget target, List<string> rowsJson)
        {
            _sb.Length = 0;
            _sb.Append("{\"secret\":");
            AppendEscaped(_sb, target.Secret ?? "");
            _sb.Append(",\"sheet\":");
            AppendEscaped(_sb, target.Tab);
            _sb.Append(",\"rows\":[");

            for (int i = 0; i < rowsJson.Count; i++)
            {
                if (i > 0) _sb.Append(',');
                _sb.Append(rowsJson[i]);   // already valid JSON from JsonUtility
            }

            _sb.Append("]}");
            return _sb.ToString();
        }

        static void AppendEscaped(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}