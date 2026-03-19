using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BLEWindows.Models
{
    public class AppConfig
    {
        public int Interval { get; set; } = 1500;
        public bool OverrideTxPower { get; set; } = false;
        public int TxPower { get; set; } = 0;
        public List<string> SelectedDevices { get; set; } = new List<string>();
        public bool StartMinimized { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        public bool AutoStartSpoofing { get; set; } = false;
        public int NetTxPowerIndex { get; set; } = 4;
        
        public string ServiceName { get; set; } = null;
        public string ServiceDesc { get; set; } = null;

        public void Save()
        {
            try {
                File.WriteAllText("config.json", JsonSerializer.Serialize(this));
            } catch {}
        }

        public static AppConfig Load()
        {
            try {
                if (File.Exists("config.json"))
                    return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText("config.json"));
            } catch {}
            return new AppConfig();
        }
    }
}
