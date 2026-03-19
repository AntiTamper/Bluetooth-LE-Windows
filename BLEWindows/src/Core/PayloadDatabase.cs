using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using BLEWindows.Models;

namespace BLEWindows.Core
{
    public static class PayloadDatabase
    {
        public static List<BleDeviceDef> Devices { get; private set; } = new List<BleDeviceDef>();

        public static void LoadDatabase(string targetPath = "payloads.json")
        {
            try
            {
                if (File.Exists(targetPath))
                {
                    string json = File.ReadAllText(targetPath);
                    Devices = JsonSerializer.Deserialize<List<BleDeviceDef>>(json) ?? new List<BleDeviceDef>();
                }
            } 
            catch { }
        }

        public static byte[] HexToBytes(string hex)
        {
            hex = hex.Replace(" ", "");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        public static void ApplyPayload(BluetoothLEAdvertisement advertisement, BleDeviceDef device)
        {
            try {
                byte[] payloadData = HexToBytes(device.HexPayload);

                // Hardware Protection: Automatically truncate ANY generated payload 
                // that exceeds the maximum physical bytes allowed in a legacy BLE packet.
                // 31 bytes total - 4 bytes base overhead (Adv. + Manufacturer Headers) = 27 bytes max.
                if (payloadData.Length > 27)
                {
                    Array.Resize(ref payloadData, 27);
                }

                var writer = new DataWriter();
                writer.WriteBytes(payloadData);
                var buffer = writer.DetachBuffer();

                if (device.IsServiceData)
                {
                    var dataSection = new BluetoothLEAdvertisementDataSection();
                    dataSection.DataType = 0x16; 
                    
                    byte[] svcData = new byte[2 + payloadData.Length];
                    byte[] uuidBytes = BitConverter.GetBytes(device.Identifier);
                    svcData[0] = uuidBytes[0];
                    svcData[1] = uuidBytes[1];
                    Array.Copy(payloadData, 0, svcData, 2, payloadData.Length);
                    
                    var svcWriter = new DataWriter();
                    svcWriter.WriteBytes(svcData);
                    dataSection.Data = svcWriter.DetachBuffer();
                    advertisement.DataSections.Add(dataSection);
                }
                else
                {
                    var manufacturerData = new BluetoothLEManufacturerData() { CompanyId = device.Identifier };
                    manufacturerData.Data = buffer;
                    advertisement.ManufacturerData.Add(manufacturerData);
                }
            } catch { } // Safety parsing
        }
    }
}
