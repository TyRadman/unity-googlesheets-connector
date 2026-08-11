using UnityEngine;

namespace GoogleSheetsConnection
{
    [CreateAssetMenu(fileName = "SheetEndpoint", menuName = "Sheets/Endpoint")]
    public class SheetEndpoint : ScriptableObject
    {
        [Tooltip("Apps Script Web app URL. Paste it as-is; query strings are stripped automatically.")]
        public string url = "";

        [Tooltip("Must match SECRET in that project's Code.gs, at the version it was last deployed at.")]
        public string secret = "";

        [Tooltip("Used when a tab name is not supplied.")]
        public string defaultTab = "Data";

        public SheetTarget Tab(string tabName)
        {
            return new SheetTarget(url, secret, string.IsNullOrEmpty(tabName) ? defaultTab : tabName);
        }

        public SheetTarget Default
        {
            get { return Tab(defaultTab); }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Clean the field in place so what you see is what gets sent.
            string cleaned = SheetTarget.NormalizeUrl(url);
            if (cleaned != url)
            {
                url = cleaned;
            }

            if (secret != null)
            {
                secret = secret.Trim();
            }

            if (!string.IsNullOrEmpty(url) && !url.EndsWith("/exec"))
            {
                Debug.LogWarning("[SheetLog] '" + name + "' URL does not look like an Apps Script deployment: " + url, this);
            }
        }
#endif
    }
}