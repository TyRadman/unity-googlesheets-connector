using GoogleSheetsConnection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GoogleSheetsConnection
{
    public static class SheetLog
    {
        public static float FlushInterval = 5f;
        public static int MaxRowsPerSend = 100;
        public static int MaxRetries = 2;
        public static int TimeoutSeconds = 20;
        public static bool VerboseLogging = true;

        public static void Send<T>(SheetTarget target, T data)
        {
            if (data == null)
            {
                return;
            }

            string json = Serialize(data);
            if (json == null)
            {
                return;
            }

            SheetLogRunner.Instance.Enqueue(target, json);
        }

        public static void Send<T>(SheetTarget target, IEnumerable<T> rows)
        {
            if (rows == null)
            {
                return;
            }

            foreach (var r in rows)
            {
                Send(target, r);
            }
        }

        public static void SendImmediate<T>(SheetTarget target, T data, Action<bool, string> onDone = null)
        {
            if (data == null)
            {
                return;
            }

            string json = Serialize(data);
            if (json == null)
            {
                if (onDone != null)
                {
                    onDone(false, "serialization produced no fields");
                }

                return;
            }

            SheetLogRunner.Instance.SendNow(target, json, onDone);
        }

        public static void FlushNow()
        {
            SheetLogRunner.Instance.RequestFlush();
        }

        public static void SetGlobal(string key, object value)
        {
            SheetLogRunner.Instance.SetGlobal(key, value);
        }

        public static void ClearGlobals()
        {
            SheetLogRunner.Instance.ClearGlobals();
        }

        public static int PendingRowCount
        {
            get { return SheetLogRunner.Instance.PendingRowCount; }
        }

        static string Serialize<T>(T data)
        {
            string json = JsonUtility.ToJson(data);

            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                Debug.LogError(
                    "[SheetLog] " + typeof(T).Name + " serialized to nothing. It must be [Serializable] " +
                    "with public fields. JsonUtility ignores properties, dictionaries, and primitives.");
                return null;
            }

            return json;
        }
    }

}
