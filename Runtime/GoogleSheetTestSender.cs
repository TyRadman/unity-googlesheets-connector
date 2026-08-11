using UnityEngine;

namespace GoogleSheetsConnection
{
    public class GoogleSheetTestSender : MonoBehaviour
    {
        [SerializeField] private SheetEndpoint _sheets;
        [SerializeField] private GoogleSheetTestData _data;

        private void Start()
        {
            SheetLog.Send(_sheets.Tab("Test Data"), _data);
        }
    }
}