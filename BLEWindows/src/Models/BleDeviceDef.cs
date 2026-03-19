namespace BLEWindows.Models
{
    public class BleDeviceDef
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public ushort Identifier { get; set; }
        public string HexPayload { get; set; }
        public bool IsServiceData { get; set; }
    }
}
