using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Radios;
using BLEWindows.Models;

namespace BLEWindows.Core
{
    public class BleSpoofer
    {
        private BluetoothLEAdvertisementPublisher _publisher;
        private bool _isSpoofing = false;
        private System.Timers.Timer _randomTimer;
        private Random _rng = new Random();
        private List<BleDeviceDef> _currentGroup = new List<BleDeviceDef>();

        public short? TargetTxPower { get; set; } = null;
        public bool RandomizeInterval { get; set; } = false;
        public bool IsSpoofing => _isSpoofing;
        public event EventHandler<string> OnSpoofingChanged;
        public event EventHandler<Exception> OnError;

        public BleSpoofer()
        {
            _randomTimer = new System.Timers.Timer(1500); 
            _randomTimer.Elapsed += RandomTimer_Elapsed;
            EnsureBluetoothOnAsync();
        }

        private async void EnsureBluetoothOnAsync()
        {
            try
            {
                var access = await Radio.RequestAccessAsync();
                if (access == RadioAccessStatus.Allowed)
                {
                    var radios = await Radio.GetRadiosAsync();
                    var btRadio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
                    if (btRadio != null && btRadio.State != RadioState.On)
                    {
                        await btRadio.SetStateAsync(RadioState.On);
                    }
                }
            }
            catch { /* Ignore radio errors on desktop */ }
        }

        public void SetTimerInterval(double milliseconds)
        {
            _randomTimer.Interval = Math.Max(200, milliseconds);
        }

        private string GenerateRandomName(int maxAllowed)
        {
            if (maxAllowed <= 0) return null;
            int minLen = Math.Min(8, maxAllowed);
            int length = _rng.Next(minLen, maxAllowed + 1);
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder result = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                result.Append(chars[_rng.Next(chars.Length)]);
            }
            return result.ToString();
        }

        public void StartSpoofing(BleDeviceDef device)
        {
            if (_isSpoofing) StopSpoofing();
            
            _isSpoofing = true;
            _currentGroup = new List<BleDeviceDef> { device };
            OnSpoofingChanged?.Invoke(this, $"Spoofing ({device.Name})...");
            
            try
            {
                StartPublisherCurrentDevice();
            }
            catch (Exception ex)
            {
                StopSpoofing();
                OnError?.Invoke(this, ex);
            }
        }

        public void StartRandomSpoofer(List<BleDeviceDef> group)
        {
            if (group == null || group.Count == 0) return;
            StopSpoofing(); 
            _currentGroup = group;
            _isSpoofing = true;
            _randomTimer.Start();
            OnSpoofingChanged?.Invoke(this, "Spoofing (Random Group)...");
            
            RandomTimer_Elapsed(null, null);
        }

        private void RandomTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_isSpoofing || _currentGroup.Count == 0) return;

            // Randomize interval: between 50% and 150% of base value
            if (RandomizeInterval)
            {
                double baseMs = _randomTimer.Interval;
                double jitter = baseMs * (0.5 + _rng.NextDouble());
                _randomTimer.Interval = Math.Max(200, jitter);
            }

            try
            {
                StartPublisherCurrentDevice();
            }
            catch (Exception ex)
            {
                _randomTimer.Stop();
                StopSpoofing();
                OnError?.Invoke(this, ex);
            }
        }

        private void StartPublisherCurrentDevice()
        {
            var device = _currentGroup[_rng.Next(_currentGroup.Count)];
            Exception lastException = null;

            OnSpoofingChanged?.Invoke(this, $"Spoofing: {device.Name}...");

            // Attempt 1: Full features (Custom Name + Tx Power)
            try { TryPublish(device, true, true); return; } catch(Exception ex) { lastException = ex; }

            // Attempt 2: Drop Custom Name (Payload might unexpectedly exceed bounds)
            try { TryPublish(device, false, true); return; } catch(Exception ex) { lastException = ex; }

            // Attempt 3: Drop Tx Power but keep Custom Name (Hardware might not support TxPower overrides)
            try { TryPublish(device, true, false); return; } catch(Exception ex) { lastException = ex; }

            // Attempt 4: Drop both Custom Name and Tx Power (Safest compatible mode)
            try { TryPublish(device, false, false); return; } catch(Exception ex) { lastException = ex; }

            throw lastException ?? new Exception("Unknown error initiating publisher");
        }

        private void TryPublish(BleDeviceDef device, bool useName, bool useTxPower)
        {
            if (_publisher != null)
            {
                try { _publisher.Stop(); } catch { }
                _publisher = null;
            }

            _publisher = new BluetoothLEAdvertisementPublisher();

            if (useTxPower && TargetTxPower.HasValue) 
                _publisher.PreferredTransmitPowerLevelInDBm = TargetTxPower.Value;

            BleDeviceDef finalDevice = device;

            if (device.Category == "Windows")
            {
                // Hardcap the random string to exactly 12 chars (safely well under the 31-byte PDU limit even with Windows OS flag injections)
                string hexString = "";
                string name = GenerateRandomName(12);
                if (!string.IsNullOrEmpty(name))
                {
                    byte[] asciiBytes = Encoding.ASCII.GetBytes(name);
                    hexString = BitConverter.ToString(asciiBytes).Replace("-", "");
                }

                finalDevice = new BleDeviceDef
                {
                    Name = device.Name,
                    Category = device.Category,
                    Identifier = device.Identifier,
                    IsServiceData = device.IsServiceData,
                    HexPayload = device.HexPayload + hexString
                };
            }

            PayloadDatabase.ApplyPayload(_publisher.Advertisement, finalDevice);
            _publisher.Start();
        }

        public void StopSpoofing()
        {
            _isSpoofing = false;
            _randomTimer.Stop();

            if (_publisher != null)
            {
                try { _publisher.Stop(); } catch { }
                _publisher = null;
            }
            OnSpoofingChanged?.Invoke(this, "Idle");
        }
    }
}
