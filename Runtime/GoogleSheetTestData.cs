
namespace GoogleSheetsConnection
{
    /// <summary>
    /// Just test data.
    /// </summary>
    [System.Serializable]
    public class GoogleSheetTestData
    {
        public string CharacterSelected = "Name";
        public float DistanceTraveled = 0f;
        public int EnemiesKilled = 0;
        public GoogleSheetTestDataUpgrades[] Upgrades;
        public GoogleSheetTestDataUpgrades SomeUpgrade;
    }

    /// <summary>
    /// Just test data.
    /// </summary>
    [System.Serializable]
    public class GoogleSheetTestDataUpgrades
    {
        public string Upgrade01 = "Upgrade";
        public float Upgrade01HighlightTime = 0f;
        public string Upgrade02 = "Upgrade";
        public float Upgrade02HighlightTime = 0f;
        public string Upgrade03 = "Upgrade";
        public float Upgrade03HighlightTime = 0f;
        public string UpgradeSelected = "Upgrade";

        public GoogleSheetTestDataModifiers[] Modifiers;
    }

    /// <summary>
    /// Just test data.
    /// </summary>
    [System.Serializable]
    public class GoogleSheetTestDataModifiers
    {
        public int Mod = 1;
    }
}