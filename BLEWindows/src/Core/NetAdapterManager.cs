using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BLEWindows.Core
{
    public static class NetAdapterManager
    {
        public static async Task<bool> SetTransmitPowerAsync(string powerValue)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Specifically invokes the OS network namespace to overwrite the TX power
                    string psCommand = $"Get-NetAdapter | Where-Object {{ (Get-NetAdapterAdvancedProperty -Name $_.Name -DisplayName 'Transmit Power' -ErrorAction Ignore) -ne $null }} | Set-NetAdapterAdvancedProperty -DisplayName 'Transmit Power' -DisplayValue '{powerValue}' -ErrorAction Ignore";
                    
                    var psi = new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    
                    using (var process = Process.Start(psi))
                    {
                        process.WaitForExit();
                        return process.ExitCode == 0;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }
        public static async Task<bool> EnableBluetoothAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    var radios = await Windows.Devices.Radios.Radio.GetRadiosAsync();
                    bool anySucceeded = false;
                    foreach (var radio in radios)
                    {
                        if (radio.Kind == Windows.Devices.Radios.RadioKind.Bluetooth)
                        {
                            if (radio.State != Windows.Devices.Radios.RadioState.On)
                            {
                                var result = await radio.SetStateAsync(Windows.Devices.Radios.RadioState.On);
                                if (result == Windows.Devices.Radios.RadioAccessStatus.Allowed)
                                    anySucceeded = true;
                            }
                            else
                            {
                                anySucceeded = true;
                            }
                        }
                    }
                    return anySucceeded;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
