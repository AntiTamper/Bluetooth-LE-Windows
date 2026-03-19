using System;
using System.Windows.Forms;
using BLEWindows.UI;
using BLEWindows.Core;

namespace BLEWindows
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            if (Environment.GetEnvironmentVariable("SPOOFER_BOOT_TOKEN") != "SYNAPSE_AUTHORIZED")
            {
                Environment.Exit(0);
            }

            ApplicationConfiguration.Initialize();
            
            string payloadPath = Environment.GetEnvironmentVariable("SPOOFER_PAYLOAD_PATH");
            if (string.IsNullOrEmpty(payloadPath)) payloadPath = "payloads.json";

            PayloadDatabase.LoadDatabase(payloadPath);
            Application.Run(new MainForm());
        }
    }
}
