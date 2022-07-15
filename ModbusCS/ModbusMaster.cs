using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCS
{
    /*
        Function Code   Action          Table Name
        01 (01 hex)	    Read Discrete   Output Coils
        05 (05 hex)	    Write single    Discrete Output Coil
        15 (0F hex)	    Write multiple  Discrete Output Coils
        02 (02 hex)	    Read Contact    Discrete Input Contacts
        04 (04 hex)	    Read Analog     Input Registers
        03 (03 hex)	    Read Analog     Output Holding Registers
        06 (06 hex)	    Write single    Analog Output Holding Register
        16 (10 hex)	    Write multiple  Analog Output Holding Registers
        43 (2B hex)     Encapsulated Interface Read/Write 
        100(64 hex)     Read File Record
    */

    public interface IModbusMaster
    {
        int ReadCoil(byte unit_id, ushort address, ushort quantity, out byte[] Response); // Return data
        int ReadContact(byte unit_id, ushort address, ushort quantity, out byte[] Response); // Return data
        int ReadHolding(byte unit_id, ushort address, ushort quantity, out byte[] Response, bool word_swap = true, bool byte_swap = true); // Return data
        int ReadInput(byte unit_id, ushort address, ushort quantity, out byte[] Response, bool word_swap = true, bool byte_swap = true); // Return data
        int WriteCoilSingle(byte unit_id, ushort address, bool data, out byte[] Response); // Return PDU
        int WriteHoldingSingle(byte unit_id, ushort address, ushort data, out byte[] Response); // Return PDU
        int WriteCoil(byte unit_id, ushort address, ushort quantity, byte[] data, out byte[] Response); // Return PDU
        int WriteHolding(byte unit_id, ushort address, ushort quantity, byte[] data, out byte[] Response); // Return PDU
        int EncapsulatedInterface(byte unit_id, byte[] data, out byte[] Response); // Return PDU
        int ReadFileRecord(byte unit_id, byte reference_type, ushort file_number, ushort record_number, ushort record_length, out byte[] Response); // Return PDU
        int ModbusBypass(byte unit_id, byte[] Request_PDU, out byte[] Response); // Return PDU
        DateTime ReadTime(byte unit_id);
        Dictionary<int, string> ReadProductInfo(byte unit_id, byte id_code);
        void WriteTime(byte unit_id, DateTime datetime);
        void SwapData(ref byte[] data, int byte_length, bool word_swap, bool byte_swap);
    }

    public class ModbusMasterRTU : ModbusTransportRTU, IModbusMaster
    {
        const byte FC_READ_COIL = 0x01;             // 01 (01 hex)
        const byte FC_READ_CONTACT = 0x02;          // 02 (02 hex)
        const byte FC_READ_HOLDING = 0x03;          // 03 (03 hex)
        const byte FC_READ_INPUT = 0x04;            // 04 (04 hex)
        const byte FC_WRITE_COIL_SINGLE = 0x05;     // 05 (05 hex)
        const byte FC_WRITE_HOLDING_SINGLE = 0x06;  // 06 (06 hex)
        const byte FC_WRITE_COIL = 0x0F;            // 15 (0F hex)
        const byte FC_WRITE_HOLDING = 0x10;         // 16 (10 hex)
        const byte FC_ENCAP_INTERFACE = 0x2B;       // 43 (2B hex)
        const byte FC_READ_FILE_RECORD = 0x64;      // 100(64 hex)
        const int EXCEPTION_CONTEXT_NOT_MATCHING = -4;
        int timeout = 1000;

        public ModbusMasterRTU(int index, string portname, int baudrate = 38400) : base(index, portname, baudrate)
        {
        }

        private int ReadDefault(byte unit_id, byte function_code, ushort address, ushort quantity, out byte[] Response, int expected_length = 0)
        {
            byte[] ModbusPDU = new byte[]
            {
                    function_code,
                    (byte) ((address >> 8) & 0xFF),
                    (byte) ((address >> 0) & 0xFF),
                    (byte)((quantity >> 8) & 0xFF),
                    (byte)((quantity >> 0) & 0xFF)
            };
            byte[] ADUContext = MakeADUContext(unit_id);
            byte[] ModbusADU = PDUtoADU(ADUContext, ModbusPDU);

            int status = ExchangeFrame(ModbusADU, out Response, timeout, expected_length);

            // @note: this function does not matching fc oriented context matching
            return status;
        }
        public int ReadCoil(byte unit_id, ushort address, ushort quantity, out byte[] Response)
        {
            int status = ReadDefault(unit_id, FC_READ_COIL, address, quantity, out Response, 5 + (quantity+7) / 8);
            if (status == 0 && Response != null) {
                int byte_length = Response[2];
                Response = Response.Skip(3).Take(byte_length).ToArray();
            }
            return status;
        }
        public int ReadContact(byte unit_id, ushort address, ushort quantity, out byte[] Response)
        {
            int status = ReadDefault(unit_id, FC_READ_CONTACT, address, quantity, out Response, 5 + (quantity+7) / 8);
            if (status == 0 && Response != null) {
                int byte_length = Response[2];
                Response = Response.Skip(3).Take(byte_length).ToArray();
            }
            return status;
            }
        public int ReadHolding(byte unit_id, ushort address, ushort quantity, out byte[] Response, bool word_swap = true, bool byte_swap = true)
        {
            int status = ReadDefault(unit_id, FC_READ_HOLDING, address, quantity, out Response, 5 + quantity * 2);
            if (status == 0 && Response != null) {
                int byte_length = Response[2];
                Response = Response.Skip(3).Take(byte_length).ToArray();
                
                SwapData(ref Response, byte_length, word_swap, byte_swap);
            }
            // Console.WriteLine("Read Holding Status: {0}", status);
            return status;
            }
        public int ReadInput(byte unit_id, ushort address, ushort quantity, out byte[] Response, bool word_swap = true, bool byte_swap = true)
        {
            int status = ReadDefault(unit_id, FC_READ_INPUT, address, quantity, out Response, 5 + quantity * 2);
            if (status == 0 && Response != null) {
                int byte_length = Response[2];
                Response = Response.Skip(3).Take(byte_length).ToArray();

                SwapData(ref Response, byte_length, word_swap, byte_swap);
            }
            // Console.WriteLine("Read Input Status: {0}", status);
            return status;
            }
        public int ReadHolding(byte unit_id, ushort address, ushort quantity, out byte[] Response)
        {
            return ReadHolding(unit_id, address, quantity, out Response, true, true);
        }
        public int ReadInput(byte unit_id, ushort address, ushort quantity, out byte[] Response)
        {
            return ReadInput(unit_id, address, quantity, out Response, true, true);
        }
        public int WriteCoilSingle(byte unit_id, ushort address, bool data, out byte[] Response)
        {
            byte function_code = FC_WRITE_COIL_SINGLE;
            ushort bdata = (data) ? (ushort)0xFF00 : (ushort)0x0000;
            byte[] ModbusPDU = new byte[]
            {
                    function_code,
                    (byte) ((address >> 8) & 0xFF),
                    (byte) ((address >> 0) & 0xFF),
                    (byte)((bdata >> 8) & 0xFF),
                    (byte)((bdata >> 0) & 0xFF)
            };
            byte[] ADUContext = MakeADUContext(unit_id);
            byte[] ModbusADU = PDUtoADU(ADUContext, ModbusPDU);

            int status = ExchangeFrame(ModbusADU, out Response, timeout);

            // @todo: fc context matching
            return status;
        }
        public int WriteHoldingSingle(byte unit_id, ushort address, ushort data, out byte[] Response)
        {
            byte function_code = FC_WRITE_HOLDING_SINGLE;
            byte[] ModbusPDU = new byte[]
            {
                    function_code,
                    (byte) ((address >> 8) & 0xFF),
                    (byte) ((address >> 0) & 0xFF),
                    (byte)((data >> 8) & 0xFF),
                    (byte)((data >> 0) & 0xFF)
            };
            byte[] ADUContext = MakeADUContext(unit_id);
            byte[] ModbusADU = PDUtoADU(ADUContext, ModbusPDU);

            int status = ExchangeFrame(ModbusADU, out Response, timeout);

            // @note: this function does not matching fc oriented context matching
            return status;
        }
        public int WriteCoil(byte unit_id, ushort address, ushort quantity, byte[] data, out byte[] Response)
        {
            byte function_code = FC_WRITE_COIL;
            byte data_len = (byte)data.Length;
            byte[] ModbusPDUContext = new byte[6]
            {
                    function_code,
                    (byte)((address >> 8) & 0xFF),
                    (byte)((address >> 0) & 0xFF),
                    (byte)((quantity >> 8) & 0xFF),
                    (byte)((quantity >> 0) & 0xFF),
                    (byte)data.Length,
            };
            var list = new List<byte>();
            list.AddRange(ModbusPDUContext);
            list.AddRange(data);
            byte[] ModbusPDU = list.ToArray();

            byte[] ADUContext = MakeADUContext(unit_id);
            byte[] ModbusADU = PDUtoADU(ADUContext, ModbusPDU);

            int status = ExchangeFrame(ModbusADU, out Response, timeout);

            // @note: this function does not matching fc oriented context matching
            return status;
        }
        public int WriteHolding(byte unit_id, ushort address, ushort quantity, byte[] data, out byte[] Response)
        {
            byte function_code = FC_WRITE_HOLDING;
            byte data_len = (byte)data.Length;
            byte[] ModbusPDUContext = new byte[6]
            {
                    function_code,
                    (byte)((address >> 8) & 0xFF),
                    (byte)((address >> 0) & 0xFF),
                    (byte)((quantity >> 8) & 0xFF),
                    (byte)((quantity >> 0) & 0xFF),
                    (byte)data.Length,
            };
            var list = new List<byte>();
            list.AddRange(ModbusPDUContext);
            list.AddRange(data);
            byte[] ModbusPDU = list.ToArray();

            byte[] ADUContext = MakeADUContext(unit_id);
            byte[] ModbusADU = PDUtoADU(ADUContext, ModbusPDU);

            int status = ExchangeFrame(ModbusADU, out Response, timeout);

            // @note: this function does not matching fc oriented context matching
            return status;
        }
        public int EncapsulatedInterface(byte unit_id, byte[] data, out byte[] Response)
        {
            byte function_code = FC_ENCAP_INTERFACE;
            byte data_len = (byte)data.Length;
            byte[] ModbusPDUContext = new byte[1]
            {
                    function_code
            };
            var list = new List<byte>();
            list.AddRange(ModbusPDUContext);
            list.AddRange(data);
            byte[] ModbusPDU = list.ToArray();

            byte[] ADUContext = MakeADUContext(unit_id);
            byte[] ModbusADU = PDUtoADU(ADUContext, ModbusPDU);

            int status = ExchangeFrame(ModbusADU, out Response, timeout);

            // @note: this function does not matching fc oriented context matching
            return status;
        }
        public int ReadFileRecord(byte unit_id, byte reference_type, ushort file_number, ushort record_number, ushort record_length, out byte[] Response)
        {
            byte function_code = FC_READ_FILE_RECORD;
            byte data_len = 7;
            byte[] ModbusPDU = new byte[]
            {
                    function_code,
                    data_len,
                    reference_type,
                    (byte)((file_number >> 8) & 0xFF),
                    (byte)((file_number >> 0) & 0xFF),
                    (byte)((record_number >> 8) & 0xFF),
                    (byte)((record_number >> 0) & 0xFF),
                    (byte)((record_length >> 8) & 0xFF),
                    (byte)((record_length >> 0) & 0xFF)
            };
            byte[] ADUContext = MakeADUContext(unit_id);
            byte[] ModbusADU = PDUtoADU(ADUContext, ModbusPDU);

            int status = ExchangeFrame(ModbusADU, out Response, timeout);
            if(status == 0)
                Response = ADUtoPDU(Response);
            // @todo: fc context matching
            return status;
        }

        public int ModbusBypass(byte unit_id, byte[] Request_PDU, out byte[] Response)
        {
            byte[] ADUContext = MakeADUContext(unit_id);
            byte[] ModbusADU = PDUtoADU(ADUContext, Request_PDU);
            int status = ExchangeFrame(ModbusADU, out Response, timeout);
            if (status == 0)
                Response = ADUtoPDU(Response);
            return status;
        }
        private Dictionary<int, string> ParseProductInfo(byte[] RxPDU)
        {
            Dictionary<int, string> info_dict = new Dictionary<int, string>();
            int num_of_obj = RxPDU[6];
            //Console.WriteLine("Num Of Object: {0}", num_of_obj);
            int base_len = 7;
            int len = 0;
            int object_id = 0;
            for (int i = 0; i < num_of_obj; i++)
            {
                object_id = RxPDU[base_len];
                len = RxPDU[base_len + 1];
                //Console.WriteLine("Object[{0}] : {1}", RxPDU[base_len], len);
                byte[] obj_byte_arr = RxPDU.Skip(base_len + 2).Take(len).ToArray();
                string obj_str = Encoding.Default.GetString(obj_byte_arr).Trim('\0');
                //Console.WriteLine("[{0}]: {1}",object_id, obj_str);
                info_dict.Add(object_id, obj_str);
                base_len += 2 + len;
            }


            return info_dict;
        }
        /*
         * @param id_code : object number(1: baisc, 2: regular, 3:extended)
         */
        public Dictionary<int, string> ReadProductInfo(byte unit_id, byte id_code)
        {
            byte[] data = new byte[3]
            {
                    0x0E,
                    id_code,
                    0x00
            };
            byte[] Response;

            int status = EncapsulatedInterface(unit_id, data, out Response);
            if (status == 0)
            {
                byte[] PDU = ADUtoPDU(Response);
                return ParseProductInfo(PDU);
            }
            else
            {
                Console.WriteLine("Error : {0}", status);
                return new Dictionary<int, string>();
            }
        }
        /*
         * @brief   Read Time Using 0x2B Function.
         * @return  When error occured return DateTime.MinValue. Normally return normal DateTime value.
         */
        public DateTime ReadTime(byte unit_id)
        {
            byte[] data = new byte[2]
            {
                    0x0F,
                    0x00
            };
            byte[] Response;
            DateTime datetime = DateTime.MinValue; // Default return.

            int status = EncapsulatedInterface(unit_id, data, out Response);
            if (status == 0)
            {
                byte[] PDU = ADUtoPDU(Response);
                byte[] TimeData = PDU.Skip(3).Take(8).ToArray();
                int year =      TimeData[1] + 2000;
                int month =     TimeData[2];
                int day =       TimeData[3];
                int hour =      TimeData[4];
                int minute =    TimeData[5];
                int second =    BitConverter.ToUInt16(TimeData.Skip(6).Take(2).Reverse().ToArray(), 0) / 1000;
                int milisecond= BitConverter.ToUInt16(TimeData.Skip(6).Take(2).Reverse().ToArray(), 0) % 1000;
                datetime = new DateTime(year, month, day, hour, minute, second, milisecond);
                // Console.WriteLine(datetime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            }
            else
            {

            }
            return datetime;
        }
        public void WriteTime(byte unit_id, DateTime datetime)
        {
            int sec_ms = datetime.Second * 1000 + datetime.Millisecond;
            byte[] time_arr = new byte[8]
            {
                0x00,
                (byte)(datetime.Year-2000),
                (byte)(datetime.Month),
                (byte)(datetime.Day),
                (byte)(datetime.Hour),
                (byte)(datetime.Minute),
                (byte)((sec_ms >> 8) & 0xFF),
                (byte)((sec_ms >> 0) & 0xFF)
            };

            byte[] tmp_data = new byte[2]
            {
                    0x10,
                    0x00
            };
            var list = new List<byte>();
            list.AddRange(tmp_data);
            list.AddRange(time_arr);
            byte[] data = list.ToArray();

            byte[] Response;

            int status = EncapsulatedInterface(unit_id, data, out Response);
            if (status == 0)
            {
                byte[] PDU = ADUtoPDU(Response);
                byte[] TimeData = PDU.Skip(3).Take(8).ToArray();
            }
            else
            {

            }
        }
        /*
         *  Given Data : 00 11 22 33 swap_byte = true , swap_word = false 
         *  ReturnData : 00 11 22 33 swap_byte = false, swap_word = false 
         *  ReturnData : 11 00 33 22 swap_byte = true , swap_word = false
         *  ReturnData : 22 33 00 11 swap_byte = false, swap_word = true
         *  ReturnData : 33 22 11 00 swap_byte = true , swap_word = true
         */
        public void SwapData(ref byte[] data, int byte_length, bool word_swap = true, bool byte_swap = true)
        {
            if (word_swap)
            {
                SwapWord(ref data, byte_length);
            }
            if (byte_swap)
            {
                SwapByte(ref data, byte_length);
            }
        }
        static void SwapWord(ref byte[] data, int byte_length)
        {
            if(byte_length % 2 != 0)
            {
                return;
            }
            int word_length = byte_length / 2;
            for (int i = 0; i < word_length / 2; i++)
            {
                int j = word_length - 1 - i;
                var tmp1 = data[2 * i + 0];
                var tmp2 = data[2 * i + 1];
                data[2 * i + 0] = data[2 * j + 0];
                data[2 * i + 1] = data[2 * j + 1];
                data[2 * j + 0] = tmp1;
                data[2 * j + 1] = tmp2;
            }
        }
        static void SwapByte(ref byte[] data, int byte_length)
        {
            for (int i = 0; i < byte_length; i += 2)
            {
                var tmp = data[i + 0];
                data[i + 0] = data[i + 1];
                data[i + 1] = tmp;
            }
        }

        bool ValidateReadCoil(byte[] Request, byte[] Response) { return true; }
        bool ValidateReadContact(byte[] Request, byte[] Response) { return true; }
        bool ValidateReadHolding(byte[] Request, byte[] Response) { return true; }
        bool ValidateReadInput(byte[] Request, byte[] Response) { return true; }
        bool ValidateWriteCoilSingle(byte[] Request, byte[] Response) { return true; }
        bool ValidateWriteHoldingSingle(byte[] Request, byte[] Response) { return true; }
        bool ValidateWriteCoil(byte[] Request, byte[] Response) { return true; }
        bool ValidateWriteHolding(byte[] Request, byte[] Response) { return true; }
        bool ValidateEncapsulatedInterface(byte[] Request, byte[] Response) { return true; }
        bool ValidateReadFileRecord(byte[] Request, byte[] Response) { return true; }
    }
}

