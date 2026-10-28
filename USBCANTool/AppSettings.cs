using System;
using System.IO;
using System.Xml.Serialization;

namespace USBCANTool
{
    public sealed class SavedSlot
    {
        public bool Enabled { get; set; }
        public bool RewriteOn { get; set; }
        public bool Extended { get; set; }
        public string Id { get; set; }
        public string Data { get; set; }
    }

    public sealed class SavedSettings
    {
        public uint DeviceType { get; set; }
        public int DeviceIndex { get; set; }
        public bool ProtocolFd { get; set; }
        public string Baud { get; set; }
        public string DataBaud { get; set; }
        public uint RxDeviceType { get; set; }
        public uint TxDeviceType { get; set; }
        public int RxDeviceIndex { get; set; }
        public int TxDeviceIndex { get; set; }
        public int RxChannel { get; set; }
        public int TxChannel { get; set; }
        public bool RxProtocolFd { get; set; }
        public bool TxProtocolFd { get; set; }
        public string RxBaud { get; set; }
        public string TxBaud { get; set; }
        public string RxDataBaud { get; set; }
        public string TxDataBaud { get; set; }
        public bool RxTerm { get; set; } = true;
        public bool TxTerm { get; set; } = true;
        public bool RxNonIso { get; set; }
        public bool TxNonIso { get; set; }
        public string FilterId { get; set; }
        public int FilterDir { get; set; }
        public bool GroupShow { get; set; }
        public SavedSlot[] Slots { get; set; }
    }

    public static class AppSettings
    {
        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "USBCANTool");
                return Path.Combine(dir, "settings.xml");
            }
        }

        public static SavedSettings Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return null;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new XmlSerializer(typeof(SavedSettings));
                    return serializer.Deserialize(stream) as SavedSettings;
                }
            }
            catch
            {
                return null;
            }
        }

        public static void Save(SavedSettings settings)
        {
            if (settings == null)
                return;
            try
            {
                string path = FilePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                string temp = path + ".tmp";
                using (var stream = File.Create(temp))
                {
                    var serializer = new XmlSerializer(typeof(SavedSettings));
                    serializer.Serialize(stream, settings);
                }
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            catch
            {
            }
        }
    }
}
