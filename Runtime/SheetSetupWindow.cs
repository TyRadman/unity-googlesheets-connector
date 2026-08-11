#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GoogleSheetsConnection
{
    public class SheetSetupWindow : EditorWindow
    {
        const string TemplateName = "CodeGsTemplate";

        SheetEndpoint _endpoint;

        string _url = "";
        string _secret = "";
        string _defaultTab = "Data";

        Vector2 _scroll;
        string _status;
        bool _testing;

        [MenuItem("Tools/Data Collection/Connect to Google Sheets")]
        static void Open()
        {
            var w = GetWindow<SheetSetupWindow>("Connect to Sheets");
            w.minSize = new Vector2(460, 560);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            Header("Connect to Google Sheets");
            EditorGUILayout.LabelField(
                "Sends serializable objects from your game to a Google Sheet. " +
                "Arrays become linked child tabs.",
                Wrapped());

            Space();

            Step(1, "Create a spreadsheet",
                 "A new, empty sheet. Tabs and headers are created automatically.");

            Space();

            Step(2, "Open the script editor",
                 "In the spreadsheet: Extensions > Apps Script. Delete the myFunction stub.");

            Space();

            Step(3, "Paste the script",
                 "Copy below, paste into Code.gs replacing everything, then save (Ctrl+S).");

            var template = FindTemplate();
            using (new EditorGUI.DisabledScope(template == null))
            {
                if (GUILayout.Button("Copy gs script", GUILayout.Height(28))) CopyScript();
            }
            if (template == null)
                EditorGUILayout.HelpBox(
                    "CodeGsTemplate.txt not found. Keep it inside the project.",
                    MessageType.Error);

            Space();

            Step(4, "Set your secret",
                 "Change SECRET near the top of the script to any random string. " +
                 "It stops stray traffic writing to your sheet; it is not real security, " +
                 "since it ships inside your build.");

            Space();

            Step(5, "Deploy",
                 "Deploy > New deployment > gear icon > Web app.\n" +
                 "Execute as:  Me\n" +
                 "Who has access:  Anyone   (not \"Anyone with Google account\")");

            Space();

            Step(6, "Authorize",
                 "Pick your account. On the \"Google hasn't verified this app\" screen: " +
                 "Advanced > Go to project > Allow. Expected for a script you wrote yourself.");

            Space();

            Step(7, "Copy the Web app URL",
                 "Ends in /exec. Paste it below along with your secret.");

            Space();
            Header("Endpoint");

            _endpoint = (SheetEndpoint)EditorGUILayout.ObjectField(
                "Existing asset", _endpoint, typeof(SheetEndpoint), false);

            if (_endpoint != null) DrawExisting();
            else DrawNew();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);

            Space();
            EditorGUILayout.HelpBox(
                "After editing the script, redeploy: Deploy > Manage deployments > pencil > " +
                "Version: New version. Saving alone does not change what /exec serves.",
                MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        void DrawNew()
        {
            EditorGUILayout.LabelField("Fill these in to create an asset, or assign one above.", Wrapped());

            _url = EditorGUILayout.TextField("Web app URL", _url);
            _secret = EditorGUILayout.TextField("Secret", _secret);
            _defaultTab = EditorGUILayout.TextField("Default tab", _defaultTab);

            if (GUILayout.Button("Create endpoint asset", GUILayout.Height(24)))
            {
                CreateEndpointAsset();
            }
        }

        void DrawExisting()
        {
            var so = new SerializedObject(_endpoint);
            EditorGUILayout.PropertyField(so.FindProperty("url"));
            EditorGUILayout.PropertyField(so.FindProperty("secret"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultTab"));
            so.ApplyModifiedProperties();

            using (new EditorGUI.DisabledScope(_testing))
            {
                if (GUILayout.Button(_testing ? "Testing..." : "Send test row", GUILayout.Height(24)))
                {
                    EditorCoroutine.Start(SendTest());
                }
            }
        }

        void CopyScript()
        {
            var asset = FindTemplate();
            if (asset == null)
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = asset.text;
            _status = "Script copied. Paste into Code.gs, replacing everything.";
        }

        static TextAsset FindTemplate()
        {
            foreach (var guid in AssetDatabase.FindAssets(TemplateName + " t:TextAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == TemplateName)
                {
                    return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                }
            }

            return null;
        }

        void CreateEndpointAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create endpoint asset", "SheetEndpoint", "asset", "");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var asset = CreateInstance<SheetEndpoint>();
            asset.url = _url;
            asset.secret = _secret;
            asset.defaultTab = _defaultTab;

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            _endpoint = asset;
            EditorGUIUtility.PingObject(asset);
            _status = "Created " + path;
        }

        IEnumerator SendTest()
        {
            _testing = true;
            _status = "Sending...";
            Repaint();

            var target = _endpoint.Tab("Test");
            string json = "{\"secret\":\"" + target.Secret + "\",\"sheet\":\"Test\"," +
                          "\"rows\":[{\"source\":\"unity-editor\",\"value\":1}]}";

            using (var req = new UnityWebRequest(target.Url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "text/plain;charset=utf-8");
                req.timeout = 20;

                yield return req.SendWebRequest();

                bool ok = req.result == UnityWebRequest.Result.Success;
                _status = ok ? req.downloadHandler.text : "Failed: " + req.error;
            }

            _testing = false;
            Repaint();
        }

        static void Header(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        static void Step(int n, string title, string body)
        {
            EditorGUILayout.LabelField(n + ".  " + title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(body, Wrapped());
        }

        static void Space() { EditorGUILayout.Space(8); }

        static GUIStyle _wrapped;
        static GUIStyle Wrapped()
        {
            if (_wrapped == null)
            {
                _wrapped = new GUIStyle(EditorStyles.label) { wordWrap = true };
            }

            return _wrapped;
        }
    }


    public static class EditorCoroutine
    {
        public static void Start(IEnumerator routine)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                bool running;
                try { running = Step(routine); }
                catch (System.Exception ex) { Debug.LogException(ex); running = false; }
                if (!running)
                {
                    EditorApplication.update -= step;
                }
            };
            EditorApplication.update += step;
        }

        static bool Step(IEnumerator routine)
        {
            var op = routine.Current as UnityWebRequestAsyncOperation;
            if (op != null && !op.isDone) return true;
            return routine.MoveNext();
        }
    }
}
#endif