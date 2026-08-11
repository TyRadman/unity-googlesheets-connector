using UnityEngine;

namespace GoogleSheetsConnection
{
    public readonly struct SheetTarget
    {
        public readonly string Url;
        public readonly string Secret;
        public readonly string Tab;

        public SheetTarget(string url, string secret, string tab)
        {
            Url = NormalizeUrl(url);
            Secret = secret == null ? "" : secret.Trim();
            Tab = tab == null ? "" : tab.Trim();
        }

        public SheetTarget WithTab(string tab)
        {
            return new SheetTarget(Url, Secret, tab);
        }

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(Url)
                    && Url.StartsWith("https://")
                    && Url.EndsWith("/exec")
                    && !string.IsNullOrEmpty(Tab);
            }
        }

        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "";
            }

            string s = url.Trim().Trim('"', '\'');

            int hash = s.IndexOf('#');
            if (hash >= 0)
            {
                s = s.Substring(0, hash);
            }

            int query = s.IndexOf('?');
            if (query >= 0)
            {
                s = s.Substring(0, query);
            }

            s = s.TrimEnd('/');

            if (s.EndsWith("/dev"))
            {
                s = s.Substring(0, s.Length - 4) + "/exec";
                Debug.LogWarning(
                    "[SheetLog] a /dev URL was supplied. That endpoint requires a signed-in " +
                    "editor and cannot work from a build - using /exec instead. Note that /exec " +
                    "serves the last DEPLOYED version, not your latest saved script.");
            }

            if (s.Contains("/macros/s/") && !s.EndsWith("/exec"))
            {
                s += "/exec";
            }

            return s;
        }

        public override string ToString()
        {
            return $"{(string.IsNullOrEmpty(Tab) ? "<no tab>" : Tab)} @ {(string.IsNullOrEmpty(Url) ? "<no url>" : Url)}";
        }
    }
}