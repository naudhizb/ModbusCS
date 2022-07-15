using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions; // Regex

namespace ModbusCS
{
    public class DeviceType
    {
        public byte logical_id;
        public byte physical_id;
        public string modbus_map_identifier;
        public string manufacturer_name;
        public string product_code;
        public string device_name;
        public string hw_version;
        public string sw_version;
        public string serial_number;
        public string user_defined_name;
        public UInt32 usage;
        public UInt32 additional_information_1;
        public UInt32 additional_information_2;
        public UInt32 additional_information_3;
        public UInt32 additional_information_4;
        public string installation_year;
        public string installation_month;
        public string installation_day;
        public string connected_device_id;

        public DeviceType(byte logical_id, byte physical_id, string modbus_map_identifier)
        {
            this.logical_id = logical_id;
            this.physical_id = physical_id;
            this.modbus_map_identifier = modbus_map_identifier;
            this.manufacturer_name = "LS ELECTRIC";
            this.product_code = "";
            this.device_name = modbus_map_identifier;
            this.hw_version = "";
            this.sw_version = "";
            this.serial_number = "";
            this.user_defined_name = "";
            this.usage = 0;
            this.additional_information_1 = 0;
            this.additional_information_2 = 0;
            this.additional_information_3 = 0;
            this.additional_information_4 = 0;
            this.installation_year = "";
            this.installation_month = "";
            this.installation_day = "";
            this.connected_device_id = "";
        }
        public DeviceType(byte logical_id, byte physical_id, Dictionary<int, string> product_info)
        {
            this.logical_id = logical_id;
            this.physical_id = physical_id;
            this.modbus_map_identifier = product_info.TryGetValue(0x01, out var value1) ? value1 : "";
            this.manufacturer_name = product_info.TryGetValue(0x00, out var value2) ? value2 : "";
            this.product_code = product_info.TryGetValue(0x81, out var value3) ? value3 : "";
            this.device_name = product_info.TryGetValue(0x04, out var value4) ? value4 : "";
            this.hw_version = product_info.TryGetValue(0x82, out var value5) ? value5 : "";
            this.sw_version = product_info.TryGetValue(0x81, out var value6) ? value6 : "";
            this.serial_number = product_info.TryGetValue(0x80, out var value7) ? value7 : "";
            this.user_defined_name = "";
            this.installation_year = "";
            this.installation_month = "";
            this.installation_day = "";
            this.connected_device_id = "";
            this.usage = 0;
            this.additional_information_1 = 0;
            this.additional_information_2 = 0;
            this.additional_information_3 = 0;
            this.additional_information_4 = 0;
        }

    }
    public static class AutoDiscovery
    {
        public static int Add(int a, int b)
        {
            return a + b;
        }
        delegate bool LegacyDeviceCheckFunc(ModbusMasterRTU Channel, byte unit_id);
        const byte max_device_per_channel = 16;
        static bool isS_EMPR(ModbusMasterRTU Channel, byte unit_id)
        {
            // Read Holding 79, 1
            byte[] Response;
            int status = Channel.ReadHolding(unit_id, 79, 1, out Response);
            if (status != 0)
            {
                return false;
            }
            ushort data = BitConverter.ToUInt16(Response, 0);
            if ((2013 <= data) && (data <= 2100))
            {
                // Range 2013 ~ 2100
                return true;
            }
            else
            {
                return false;
            }
        }
        static bool isGIMAC1000(ModbusMasterRTU Channel, byte unit_id)
        {
            // Read Input 1006 2
            // Readable
            int status = Channel.ReadInput(unit_id, 79, 1, out byte[] Response);
            if (status == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        static bool isGIMAC_B_Temp(ModbusMasterRTU Channel, byte unit_id)
        {
            // ** Note: Not Yet Verified
            // Read Holding 2000 1
            // Value == Unit_id
            byte[] Response;
            int status = Channel.ReadHolding(unit_id, 2000, 1, out Response);
            if (status != 0)
            {
                return false;
            }
            ushort data = BitConverter.ToUInt16(Response, 0);
            if (data == unit_id)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        static bool isDMPi(ModbusMasterRTU Channel, byte unit_id)
        {
            // Read Holding 5078, 2
            // Readable
            int status = Channel.ReadHolding(unit_id, 5078, 2, out byte[] Response);
            if (status == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        struct LegacyDeviceInfo
        {
            public string Name;
            public LegacyDeviceCheckFunc Func;
            public LegacyDeviceInfo(string Name, LegacyDeviceCheckFunc Func)
            {
                this.Name = Name;
                this.Func = Func;
            }
        }
        static List<LegacyDeviceInfo> LegacyDeviceList = new List<LegacyDeviceInfo>()
            {
                new LegacyDeviceInfo( "S-EMPR", isS_EMPR ),
                new LegacyDeviceInfo( "GIMAC1000", isGIMAC1000),
                new LegacyDeviceInfo( "GIMAC-B-Temp", isGIMAC_B_Temp),
                new LegacyDeviceInfo( "DMPi", isDMPi)
            };
        static public void ChangeDeviceBaudrate(ModbusMasterRTU Channel, byte unit_id)
        {

        }
        static private Dictionary<int, string> FindLSProductInfo(ModbusMasterRTU Channel, byte unit_id)
        {
            Dictionary<int, string> product_info = Channel.ReadProductInfo(unit_id, 1);
            if (product_info.ContainsKey(1))
            {
                Console.WriteLine("Find Device @ {0} : {1}", unit_id, product_info[1]);
                Dictionary<int, string> info_2 = Channel.ReadProductInfo(unit_id, 2);
                Dictionary<int, string> info_3 = Channel.ReadProductInfo(unit_id, 3);
                foreach (KeyValuePair<int, string> item in info_2)
                {
                    product_info.Add(item.Key, item.Value);
                }
                foreach (KeyValuePair<int, string> item in info_3)
                {
                    product_info.Add(item.Key, item.Value);
                }

                foreach (KeyValuePair<int, string> item in product_info)
                    Console.WriteLine("Product Object[{0}]: {1}", item.Key, item.Value);
            }
            return product_info;
        }
        static private List<DeviceType> FindETags(ModbusMasterRTU Channel, ref byte logical_unit_id, byte unit_id)
        {
            // Given Unit_id is E-Collector.
            List<DeviceType> e_tag_list = new List<DeviceType>();
            byte[] Response;
            int status = Channel.ReadHolding(unit_id, 199, 1, out Response);
            if (status != 0)
            {
                return e_tag_list;
            }
            ushort num_of_tags = BitConverter.ToUInt16(Response, 0);
            Console.WriteLine("{0} E-Tag Found", num_of_tags);
            status = Channel.ReadHolding(unit_id, 200, 4, out Response, false, true); // Note: Bits order in WORDS already swapped
            // e.g. 4 tag in value returns  00 3c 00 00 00 00 00 00 0x00000000003c
            if (status != 0)
            {
                return e_tag_list;
            }
            Response.Reverse();
            ulong bit_of_tags = BitConverter.ToUInt64(Response, 0);
            bit_of_tags >>= 2;
            Console.WriteLine("Tag Bitmap: {0}", string.Format("0x{0:X}", bit_of_tags));
            for (int i = 0; i < 64; i++)
            {
                if ((bit_of_tags & (1ul << i)) != 0)
                {
                    byte tag_unit_id = (byte)(128 + i);
                    Console.WriteLine("E-Tag @ {0} Found!!", tag_unit_id);
                    //DeviceType device_found = new DeviceType(
                    //    (byte)(logical_unit_id),
                    //    tag_unit_id,
                    //    "E-TAG"
                    //);
                    DeviceType device_found = AutoDiscoverySingle(Channel, logical_unit_id, tag_unit_id);
                    if (device_found != null)
                    {
                        e_tag_list.Add(device_found);
                        logical_unit_id++;
                    }
                }
            }
            return e_tag_list;
        }
        static private DeviceType FindLegacyDevice(ModbusMasterRTU Channel, byte logical_unit_id, byte unit_id)
        {
            foreach (LegacyDeviceInfo Legacy in LegacyDeviceList)
            {
                if (Legacy.Func(Channel, unit_id))
                {
                    Console.WriteLine("Find Device @ {0} : {1}", unit_id, Legacy.Name);
                    DeviceType device_found = new DeviceType(
                        logical_unit_id,
                        unit_id,
                        Legacy.Name
                    );
                    return device_found;
                }
            }
            return null;
        }
        static public DeviceType AutoDiscoverySingle(ModbusMasterRTU Channel, byte logical_unit_id, byte physical_unit_id)
        {
            Dictionary<int, string> product_info = FindLSProductInfo(Channel, physical_unit_id);
            DeviceType device_found = null;
            if (product_info.ContainsKey(0) && product_info.ContainsKey(1))
            { // LS Device Exists
                Regex ls_regex = new Regex(@"LS");
                string manufacturer_name = product_info[0];
                if (!ls_regex.IsMatch(manufacturer_name))
                {
                    return device_found; // 0x2B Implemented but Not LS Device
                }
                device_found = new DeviceType(logical_unit_id, physical_unit_id, product_info);
                return device_found;
            }
            Thread.Sleep(100);
            device_found = FindLegacyDevice(Channel, logical_unit_id, physical_unit_id);
            return device_found;
        }
        static public List<DeviceType> AutoDiscoveryRange(ModbusMasterRTU Channel, byte channel_no, byte start_unit_id, byte end_unit_id, int baudrate)
        {
            // Backup Device Info
            // Stop Serial Channel (Fast Update, Bypass)
            // Delete Current Device Info

            byte logical_unit_id = (channel_no == 1) ? (byte)2 : (byte)33;


            List<DeviceType> device_list = new List<DeviceType>();
            for (byte unit_id = start_unit_id; unit_id <= end_unit_id; unit_id++)
            {
                ChangeDeviceBaudrate(Channel, unit_id);
                DeviceType device_found = AutoDiscoverySingle(Channel, logical_unit_id, unit_id);
                if (device_found != null)
                {
                    Regex ecollector_regex = new Regex(@"COLLECTOR");
                    string modbus_map_identifier = device_found.modbus_map_identifier;
                    if (ecollector_regex.IsMatch(modbus_map_identifier))
                    {
                        Console.WriteLine("E-collector Found. Add E-Tags...");
                        List<DeviceType> e_tags_list = FindETags(Channel, ref logical_unit_id, unit_id);
                        foreach (var e_tag in e_tags_list)
                        {
                            device_list.Add(e_tag);
                        }
                        //break; // End of Find
                    }
                    else
                    {
                        device_list.Add(device_found);
                        logical_unit_id++;
                    }
                }
            }
            if (device_list.Count > max_device_per_channel)
            {
                return device_list.GetRange(0, max_device_per_channel);
            }
            return device_list;
        }
    }

}
