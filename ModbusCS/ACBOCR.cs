using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCS
{
    public static class ACBOCR
    {
        public class TripWaveFileRecord
        {
            public struct TripWaveCycle
            {
                public const int CYCLE_LENGTH = 20;
                public Int16 IA; // Fault / Trip Wave #1 IA															
                public Int16 IB; //Fault / Trip Wave #1 IB															
                public Int16 IC; //Fault / Trip Wave #1 IC															
                public Int16 IN; //Fault / Trip Wave #1 IN															
                public Int16 IZCT; //Fault / Trip Wave #1 External IZCT															
                public Int16 VA; //Fault / Trip Wave #1 VA															
                public Int16 VB; //Fault / Trip Wave #1 VB															
                public Int16 VC; //Fault / Trip Wave #1 VC															
                public Int16 RESERVED1; //RESERVED
                public Int16 RESERVED2; //RESERVED
                public TripWaveCycle(byte[] wave_data_record)
                {
                    if (wave_data_record.Length != 20)
                    {
                        IA = 0;
                        IB = 0;
                        IC = 0;
                        IN = 0;
                        IZCT = 0;
                        VA = 0;
                        VB = 0;
                        VC = 0;
                        RESERVED1 = 0;
                        RESERVED2 = 0;
                        return;
                    }
                    IA = BitConverter.ToInt16(wave_data_record.Skip(0).Take(2).Reverse().ToArray(), 0);
                    IB = BitConverter.ToInt16(wave_data_record.Skip(2).Take(2).Reverse().ToArray(), 0);
                    IC = BitConverter.ToInt16(wave_data_record.Skip(4).Take(2).Reverse().ToArray(), 0);
                    IN = BitConverter.ToInt16(wave_data_record.Skip(6).Take(2).Reverse().ToArray(), 0);
                    IZCT = BitConverter.ToInt16(wave_data_record.Skip(8).Take(2).Reverse().ToArray(), 0);
                    VA = BitConverter.ToInt16(wave_data_record.Skip(10).Take(2).Reverse().ToArray(), 0);
                    VB = BitConverter.ToInt16(wave_data_record.Skip(12).Take(2).Reverse().ToArray(), 0);
                    VC = BitConverter.ToInt16(wave_data_record.Skip(14).Take(2).Reverse().ToArray(), 0);
                    RESERVED1 = BitConverter.ToInt16(wave_data_record.Skip(16).Take(2).Reverse().ToArray(), 0);
                    RESERVED2 = BitConverter.ToInt16(wave_data_record.Skip(18).Take(2).Reverse().ToArray(), 0);

                }
                public string GetString(int index, float sampling_step = 1000.0f/60/32)
                {
                    //tcp_slv_datalogger_util.h:875
                    //sprintf(tmp_wav_str, "%d, %2f", i + 1, i * sampling_step * 1000.0);
                    //for (int j = 0; j < wav_digital_channel_count; j++)
                    //{
                    //    sprintf(tmp_wav_str2, ", %d", byte2sshort(trip_wave_event[i].trip_wave[j]));
                    //    strcat(tmp_wav_str, tmp_wav_str2);
                    //}
                    // sampling_step = 1000/60/32;
                    string ret = string.Format("{0}, {1:F6}, ", index + 1, index * sampling_step * 1000.0f);
                    ret += $"{IA}, ";
                    ret += $"{IB}, ";
                    ret += $"{IC}, ";
                    ret += $"{IN}, ";
                    ret += $"{IZCT}, ";
                    ret += $"{VA}, ";
                    ret += $"{VB}, ";
                    ret += $"{VC}";
                    return ret;
                }
            }

            public const int WAVE_RECORD_MAX = 256;
            List<TripWaveCycle> Wave = new List<TripWaveCycle>();
            public bool isValid()
            {
                return Wave.Count() == WAVE_RECORD_MAX;
            }
            public void Add(byte[] wave_data_record)
            {
                TripWaveCycle cycle = new TripWaveCycle(wave_data_record);
                Wave.Add(cycle);
            }
            public int Count()
            {
                return Wave.Count();
            }
            public string GetFileContents()
            {
                if(isValid())
                {
                    string ret = "";
                    for(int i = 0; i < Wave.Count(); i++)
                    {
                        TripWaveCycle cycle = Wave[i];
                        ret += cycle.GetString(i);
                        ret += "\n";
                    }
                    return ret;
                }
                else
                {
                    return "";
                }
            }
        }
        public struct TripWaveConfigChannel
        {
            public string channel_name;
            public string unit;
            public float multiplier;
            public float offset;
            public float ratio;
            public const int CHANNEL_DATA_LENGTH = 20;
            public TripWaveConfigChannel(byte[] channel_data)
            {
                if(channel_data.Length != CHANNEL_DATA_LENGTH)
                {
                    throw new ArgumentException("Comtrade Channel Data Length Not Match");
                }

                channel_name = Encoding.Default.GetString(channel_data.Take(6).ToArray()).Trim('\0');
                unit = Encoding.Default.GetString(channel_data.Skip(6).Take(2).ToArray()).Trim('\0');
                multiplier = BitConverter.ToSingle(channel_data.Skip(8).Take(4).Reverse().ToArray(), 0);
                offset     = BitConverter.ToSingle(channel_data.Skip(12).Take(4).Reverse().ToArray(), 0);
                ratio      = BitConverter.ToSingle(channel_data.Skip(16).Take(4).Reverse().ToArray(), 0);   
            }
        }
        public class WaveComtrade
        {
            UInt16 num_of_channel;
            UInt16 num_of_analog_channel;
            UInt16 num_of_digital_channel;
            List<TripWaveConfigChannel> channel_data;
            UInt16 frequency;
            UInt16 max_sample_size;
            UInt16 last_sample_number;
            byte RESERVED1;
            byte RESERVED2;
            byte RESERVED3;
            byte RESERVED4;
            byte RESERVED5;
            byte RESERVED6;
            byte RESERVED7;
            byte RESERVED8;
            byte   trip_time_invalid;
            DateTime trip_time;
            UInt16 file_data_type;
            UInt16 fault_index;
            UInt16 sample_count;
            public const int COMTRADE_LEGNTH = 188;
            public WaveComtrade(byte[] file_record)
            {
                if (file_record.Length != COMTRADE_LEGNTH)
                {
                    throw new ArgumentException("Wave Comtrade WaveLength Not Matching");
                }
                num_of_channel = file_record[0];
                num_of_analog_channel = (UInt16)((file_record[1] >> 4) & 0x0F);
                num_of_digital_channel = (UInt16)((file_record[1] >> 0) & 0x0F);
                int data_base = 2;
                channel_data = new List<TripWaveConfigChannel>();
                for (int channel = 0; channel < 8; channel++)
                {
                    byte[] config_bytes = file_record.Skip(data_base).Take(TripWaveConfigChannel.CHANNEL_DATA_LENGTH).ToArray();
                    TripWaveConfigChannel one_channel_data = new TripWaveConfigChannel(config_bytes);
                    channel_data.Add(one_channel_data);
                    data_base += TripWaveConfigChannel.CHANNEL_DATA_LENGTH;
                }

                frequency = BitConverter.ToUInt16(file_record.Skip(162).Take(2).Reverse().ToArray(),0);
                max_sample_size = BitConverter.ToUInt16(file_record.Skip(164).Take(2).Reverse().ToArray(), 0);
                last_sample_number = BitConverter.ToUInt16(file_record.Skip(166).Take(2).Reverse().ToArray(), 0);
                RESERVED1 = file_record[168];
                RESERVED2 = file_record[169];
                RESERVED3 = file_record[170];
                RESERVED4 = file_record[171];
                RESERVED5 = file_record[172];
                RESERVED6 = file_record[173];
                RESERVED7 = file_record[174];
                RESERVED8 = file_record[175];
                trip_time_invalid = file_record[176];
                byte[] TimeData = file_record.Skip(176).Take(8).ToArray();
                int year = TimeData[1] + 2000;
                int month = TimeData[2];
                int day = TimeData[3];
                int hour = TimeData[4];
                int minute = TimeData[5];
                int second = BitConverter.ToUInt16(TimeData.Skip(6).Take(2).Reverse().ToArray(), 0) / 1000;
                int milisecond = BitConverter.ToUInt16(TimeData.Skip(6).Take(2).Reverse().ToArray(), 0) % 1000;
                trip_time = new DateTime(year, month, day, hour, minute, second, milisecond);
                file_data_type = (ushort)(file_record[184] >> 3);
                fault_index = BitConverter.ToUInt16(file_record.Skip(184).Take(2).Reverse().ToArray(), 0);
                fault_index &= 0x07FF;
                sample_count = BitConverter.ToUInt16(file_record.Skip(186).Take(2).Reverse().ToArray(), 0);
            }
            public string GetFileName(string ip_addr, int uid)
            {
                DateTime datetime = trip_time;
                string time_str = string.Format("{0:D4}_{1:D2}_{2:D2}_{3:D2}_{4:D2}_{5:D2}_{6:D3}",
                    datetime.Year,
                    datetime.Month,
                    datetime.Day,
                    datetime.Hour,
                    datetime.Minute,
                    datetime.Second,
                    datetime.Millisecond);
                string ret = $"Wave_{ip_addr}_{uid}_{time_str}";
                return ret;
            }
            public string GetComtradeFileName(string ip_addr, int uid)
            {
                return GetFileName(ip_addr, uid) + ".cfg";
            }
            public string GetWaveFileName(string ip_addr, int uid)
            {
                return GetFileName(ip_addr, uid) + ".dat";
            }
            public string GetFileContents(string device_model, int uid)
            {
                string ret = "";
                ret += $"SLV_DEVICE_ID: {device_model}_{uid},1,2011\n";
                ret += $"{num_of_channel},{num_of_analog_channel}A,{num_of_digital_channel}D\n";
                for(int i = 0; i < num_of_analog_channel; i++)
                {
                    TripWaveConfigChannel channel_data_config = channel_data[i];
                    string config_string = string.Format("{0},{1},,,{2},{3:0.#0},{4:0.#0},0.000,-32767,32767,{5:0.#0},1.00,P\n",
                        i + 1,
                        channel_data_config.channel_name,
                        channel_data_config.unit,
                        channel_data_config.multiplier,
                        channel_data_config.offset,
                        channel_data_config.ratio);
                    ret += config_string;
                }
                ret += $"{frequency}\n";
                ret += "1\n";
                ret += string.Format("{0:0.#####0},{1}\n", frequency*sample_count, last_sample_number);
                // Start Time
                long start_tick = trip_time.Ticks;
                start_tick -= 1000* 10000*(max_sample_size / sample_count) / frequency / 2;
                DateTime start_time = new DateTime(start_tick);
                // (1000 * (byte2short(cfg_event->max_sample_size) / sample_count) / (byte2short(cfg_event->freq)) / 2);
                ret += DateTimeToStr(start_time) + "\n";
                ret += DateTimeToStr(trip_time) + "\n";
                ret += "ASCII\n";
                ret += "1\n";
                return ret;
            }
            private static string DateTimeToStr(DateTime datetime)
            {
                return string.Format("{0:D2}/{1:D2}/{2:D2},{3:D2}:{4:D2}:{5:D2}.{6:D3}000",
                    datetime.Day,
                    datetime.Month,
                    datetime.Year - 2000,
                    datetime.Hour,
                    datetime.Minute,
                    datetime.Second,
                    datetime.Millisecond);
            }
            public float GetSamplingStep()
            {
                float sampling_step = 16.66666666667f; // Default : tcp_slv_datalogger_util.h:290
                sampling_step = (float)(1000.0 / (float)(frequency) / (float)(sample_count));

                return 16.66666666667f;
            }
        }
        public static int GetLastWaveNumber(IModbusMaster Channel, byte unit_id)
        {
            byte[] Response;
            int status = 0;
            status = Channel.ReadInput(unit_id, 196, 1, out Response);
            if (status != 0)
            {
                return -1;
            }
            ushort data = BitConverter.ToUInt16(Response, 0);
            int WaveFileNumber = (data >> 9) & 0x7F;

            if (6 < WaveFileNumber)
            {
                throw new Exception("FileNumberError");
            }

            return WaveFileNumber;
        }
        public static WaveComtrade PollWaveComtrade(IModbusMaster Channel, byte unit_id, byte WaveFileNumber)
        {
            byte[] Response;
            int status = 0;
            status = Channel.ReadFileRecord(
                unit_id,
                0x07,
                (byte)WaveFileNumber,
                (ushort)1,
                (ushort)1,
                out Response);
            if(status != 0)
            {
                return null;
            }
            byte[] file_record = Response.Skip(4).Take(WaveComtrade.COMTRADE_LEGNTH).ToArray();
            WaveComtrade Comtrade = new WaveComtrade(file_record);
            return Comtrade;
        }
        private static string WaveComtradeToFilename(WaveComtrade Comtrade)
        {
            string ret = "";
            return ret;
        }
        private static string WaveComtradeToString(WaveComtrade Comtrade)
        {
            string ret = "";
            return ret;
        }
        public static void SaveWaveComtrade(WaveComtrade Comtrade, string filepath = "./")
        {
            string filename = WaveComtradeToFilename(Comtrade);
            string comtrade_string = WaveComtradeToString(Comtrade);
        }
        public static TripWaveFileRecord PollWaveData(IModbusMaster Channel, byte unit_id, byte WaveFileNumber)
        {
            byte[] Response;
            int status = 0;
            TripWaveFileRecord WaveRecord = new TripWaveFileRecord();
            const int WaveFileCount = TripWaveFileRecord.WAVE_RECORD_MAX;
            const int PollFileRecordMax = 12;
            for(int PollBase = 1; PollBase < WaveFileCount;)
            {
                int PollLength = Math.Min(PollFileRecordMax, WaveFileCount - PollBase + 1);

                status = Channel.ReadFileRecord(
                    unit_id, 
                    0x06, 
                    (byte)WaveFileNumber, 
                    (ushort)PollBase, 
                    (ushort)PollLength, 
                    out Response);
                if(status == 0)
                {
                    int record_length = TripWaveFileRecord.TripWaveCycle.CYCLE_LENGTH * PollLength;
                    byte[] file_records = Response.Skip(4).Take(record_length).ToArray();

                    for(int i = 0; i < file_records.Length; i+= TripWaveFileRecord.TripWaveCycle.CYCLE_LENGTH)
                    {
                        byte[] file_record = file_records.Skip(i).Take(TripWaveFileRecord.TripWaveCycle.CYCLE_LENGTH).ToArray();
                        WaveRecord.Add(file_record);
                    }
                    PollBase += PollLength;
                }
                else
                {
                    break; // *** YOU MUST TRY AFTER 20 SECOND *** 
                }
            }

            return WaveRecord;
            

        }
        public static bool SaveFile(WaveComtrade comtrade, TripWaveFileRecord wave_record, string device_model, string ip_addr, int uid, string filepath="./")
        {
            bool ret = true;
            System.IO.File.WriteAllText(filepath + comtrade.GetComtradeFileName(ip_addr, uid), comtrade.GetFileContents(device_model, uid));
            System.IO.File.WriteAllText(filepath + comtrade.GetWaveFileName(ip_addr, uid), wave_record.GetFileContents());
            return ret;
        }
    }
}
