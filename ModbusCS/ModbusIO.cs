using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.IO.Ports;
using System.IO;

/*
 *                   << Modbus General Frame >> 
 *                   
 *                        |                  Modbus RTU ADU                   |
 *                        |<------------------------------------------------->|
 *                        |                                                   |
 *                        +------------+----------+-------------------+-------+
 *                        | Additional | Function |      Data ...     | Error |
 *                        |   Address  |   Code   |                   | Check |
 *                        +------------+----------+-------------------+-------+
 *                                     |                              |
 *                                     |<---------------------------->|
 *                                     |         Modbus PDU           |
 *                                     |                              |
 * +-----------+--------+------+-------+----------+-------------------+
 * |Transaction|Protocol|Length| Unit  | Function |      Data ...     |
 * |    ID[2]  |  ID[2] |  [2] | ID[1] |   Code   |                   |
 * +-----------+--------+------+-------+----------+-------------------+
 * |                                                                  |
 * |<---------------------------------------------------------------->|
 * |                        Modbus TCP ADU                            |
 */

namespace ModbusCS
{
    interface IModbusTrasnport
    {
        void TransmitFrame(byte[] Frame);
        int ReceiveFrame(out byte[] Frame, int timeout, int expected_length = 0);
        int ExchangeFrame(byte[] TxFrame, out byte[] RxFrame, int timeout, int expected_length = 0);
        byte[] MakeADUContext(byte unit_id, int sequence = 0);
        byte[] PDUtoADU(byte[] ADUContext, byte[] ModbusPDU);
        byte[] ADUtoPDU(byte[] ModbusADU);
        byte[] GetADUContext(byte[] ModbusADU);
    }
    public abstract class ModbusTransport : IModbusTrasnport
    {
        protected object Lock;
        public const int EXCEPTION_SUCCESS = 0;
        public const int EXCEPTION_TIMEOUT = -1;
        public const int EXCEPTION_CRC_ERROR = -2;
        public const int EXCEPTION_LENGTH_MISMATCH = -3;
        public int delay_between_poll = 20; // ms
        public int errorDelay = 200; // ms
        public abstract void FlushBuffer();
        public abstract void TransmitFrame(byte[] Frame);
        public abstract int ReceiveFrame(out byte[] Frame, int timeout, int expected_length = 0);
        public abstract int ExchangeFrame(byte[] TxFrame, out byte[] RxFrame, int timeout, int expected_length = 0);
        public abstract byte[] MakeADUContext(byte unit_id, int sequence = 0);
        public abstract byte[] PDUtoADU(byte[] ADUContext, byte[] ModbusPDU);
        public abstract byte[] ADUtoPDU(byte[] ModbusADU);
        public abstract byte[] GetADUContext(byte[] ModbusADU);
    }
    public class SerialPortExtension : SerialPort
    {
        public SerialPortExtension(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits) 
            : base(portName, baudRate, parity, dataBits, stopBits)
        {

        }
        public int ReadCount(byte[] buffer, int offset, int count)
        {
            int remaining = count;
            int readbyte = 0;
            int tmp_readbyte = 0;

            // Exception Handled outer scope of this function.
            do
            {
                tmp_readbyte = this.Read(buffer, offset + readbyte, remaining);
                readbyte += tmp_readbyte;
                remaining -= tmp_readbyte;
            } while (remaining > 0 && tmp_readbyte > 0);
            return readbyte;
        }
    }
    public class ModbusTransportRTU : ModbusTransport
    {
        //* @return status : 0: 성공, -1: 타임아웃, -2: CRC 오류, -3: Length Mismatch, 1이상: Modbus Excption 오류 발생

        long last_poll = 0;

        private int index;
        private string PortName;
        private int interCharDelay = 50; // might be need adjust for utilization & timing accuracy
        
        private SerialPortExtension sport;
        private int[] BaudrateList = new int[]{ 9600, 19200, 38400 };
        public int Baudrate
        {
            set
            {
                if(Array.Exists(BaudrateList, x => x == value)) // lambda
                {
                    lock(Lock)
                    {
                        if(sport != null)
                        {
                            FlushBuffer();
                            sport.BaudRate = value;
                        }
                    }
                }

            }
            get
            {
                lock (Lock)
                {
                    return sport.BaudRate;
                }
            }
        }

        public ModbusTransportRTU(int index, string portname, int baudrate = 38400)
        {
            this.Lock = new object();
            this.index = index;
            PortName = portname;
            sport = new SerialPortExtension(PortName, baudrate, Parity.None, 8, StopBits.One);
            sport.Open();
        }
        public void Close()
        {
            sport.Close();
        }
        private ushort makeCRC(byte[] data, int count)
        {
            ushort crc = 0xFFFF;
            byte lsb;

            for (int i = 0; i < count; i++)
            {
                crc = (ushort)(crc ^ (ushort)data[i]);
                for (int j = 0; j < 8; j++)
                {
                    lsb = (byte)(crc & 0x0001);
                    crc = (ushort)((crc >> 1) & 0x7fff);
                    if (lsb == 1)
                        crc = (ushort)(crc ^ 0xA001);
                }
            }
            return crc;
        }
        public bool validateADU(byte[] data, int count)
        {
            ushort crc = makeCRC(data, count-2);
            ushort crc_tmp = data[data.Length - 1];
            crc_tmp <<= 8;
            crc_tmp |= data[data.Length - 2];
            if (crc == crc_tmp)
                return true;
            else
                return false;
        }

        public override void FlushBuffer()
        {
            sport.DiscardInBuffer();
            sport.DiscardOutBuffer();
        }
        private void poll_delay()
        {
            long curr_poll = DateTime.Now.Ticks / 10000; // 100ns -> 1ms
            long diff_poll = curr_poll - last_poll;
            while (diff_poll < delay_between_poll)
            {
                int sleep_ms = (int)(delay_between_poll - diff_poll);
                Thread.Sleep(sleep_ms);
                //Console.WriteLine("Sleep Tick: {0}", sleep_ms);
                curr_poll = DateTime.Now.Ticks / 10000; // 100ns -> 1ms
                diff_poll = curr_poll - last_poll;
            } 
        }
        private void poll_update(int status)
        {
            long curr_poll = DateTime.Now.Ticks / 10000; // 100ns -> 1ms
            if(status == 0)
            {
                //Console.WriteLine("Update Tick: {0}", curr_poll - last_poll);
                last_poll = curr_poll;
            }
            else
            {
                //Console.WriteLine("Update Tick Error: {0}", curr_poll - last_poll + errorDelay);
                last_poll = curr_poll + errorDelay;
            }
        }
        private void TransmitFrameInternal(byte[] Frame)
        {
            Console.WriteLine("Tx: {0}", BitConverter.ToString(Frame));
            sport.Write(Frame, 0, Frame.Length);
        }
        public override void TransmitFrame(byte[] Frame)
        {
            lock(Lock)
            {
                poll_delay();
                FlushBuffer();
                TransmitFrameInternal(Frame);
                poll_update(0);
            }
        }
        /*
           * @return status : 0: 성공, -1: 타임아웃, -2: CRC 오류, -3: Length Mismatch, 1이상: 오류 발생
         */
        private int ReceiveFrameInternalExpected(out byte[] Frame, int timeout, int expected_length)
        {
            byte[] buffer = new byte[512];
            const int modbus_frame_min = 5;
            const int modbus_frame_max = 256;
            int receive_count = 0;

            // @todo : throughput limiter(delay after receive)

            
            if ((modbus_frame_max < expected_length) || (expected_length < modbus_frame_min))
            {
                Frame = null;
                return EXCEPTION_LENGTH_MISMATCH;
            }
            

            Thread.Sleep(10);

            // Get First Byte
            sport.ReadTimeout = timeout;
            try
            {
                sport.ReadCount(buffer, receive_count, 5); // is First Byte Receive? 
                receive_count += 5;
            }
            catch
            {
                // Timeout
                sport.ReadTimeout = timeout;
                Frame = null;
                return EXCEPTION_TIMEOUT;
            }

            if (validateADU(buffer, receive_count)) // is Completed Frame? 
            {
                Frame = buffer.Take(receive_count).ToArray();
                if ((Frame[1] & 0x80) != 0) // Exception Detected
                {
                    return Frame[2];
                }
            }

            // Other Frame Receive
            try
            {
                int received = sport.ReadCount(buffer, receive_count, expected_length - receive_count); // is First Byte Receive? 
                receive_count += received;
            }
            catch (Exception ex)
            {
                
            }


            if ((modbus_frame_max < receive_count) || (receive_count < modbus_frame_min))
            {
                Frame = null;
                return EXCEPTION_LENGTH_MISMATCH;
            }

            ushort crc = makeCRC(buffer, receive_count);
            if (crc != 0)
            {

                Frame = null;
                Console.WriteLine("Rx Error: {0}", BitConverter.ToString(buffer.Take(receive_count).ToArray()));
                return EXCEPTION_CRC_ERROR;
            }
            Frame = buffer.Take(receive_count).ToArray();
            if ((Frame[1] & 0x80) != 0) // Exception Detected
            {
                Console.WriteLine("Rx Exception: {0}", BitConverter.ToString(Frame));
                return Frame[2];
            }

            Console.WriteLine("Rx: {0}", BitConverter.ToString(Frame));
            return EXCEPTION_SUCCESS;
        }

        /*
           * @return status : 0: 성공, -1: 타임아웃, -2: CRC 오류, -3: Length Mismatch, 1이상: 오류 발생
         */
        private int ReceiveFrameInternal(out byte[] Frame, int timeout)
        {
            byte[] buffer = new byte[512];
            const int modbus_frame_min = 5;
            const int modbus_frame_max = 256;
            int receive_count = 0;

            // @todo : throughput limiter(delay after receive)

            /*
            if ((modbus_frame_max < expected_length) || (expected_length < modbus_frame_min))
            {
                Frame = null;
                return EXCEPTION_LENGTH_MISMATCH;
            }
            */

            Thread.Sleep(10);

            // Get First Byte
            sport.ReadTimeout = timeout;
            try
            {
                sport.ReadCount(buffer, receive_count, 5); // is First Byte Receive? 
                receive_count += 5;
            }
            catch
            {
                // Timeout
                sport.ReadTimeout = timeout;
                Frame = null;
                return EXCEPTION_TIMEOUT;
            }

            if (validateADU(buffer, receive_count)) // is Completed Frame? 
            {
                Frame = buffer.Take(receive_count).ToArray();
                if ((Frame[1] & 0x80) != 0) // Exception Detected
                {
                    Console.WriteLine("Rx Exception: {0}", BitConverter.ToString(Frame));
                    return Frame[2];
                }
            }

            // Other Frame Receive
            sport.ReadTimeout = interCharDelay;
            for (; receive_count <= modbus_frame_max;)
            {
                try
                {
                    sport.Read(buffer, receive_count, 1); // is First Byte Receive? 
                    receive_count += 1;
                }
                catch (Exception ex)
                {
                    // No futher Received Packet.
                    sport.ReadTimeout = timeout;

                    if ((modbus_frame_max < receive_count) || (receive_count < modbus_frame_min))
                    {
                        Frame = null;
                        return EXCEPTION_LENGTH_MISMATCH;
                    }

                    ushort crc = makeCRC(buffer, receive_count);
                    if (crc != 0)
                    {
                        
                        Frame = null;
                        Console.WriteLine("Rx Error: {0}",BitConverter.ToString(buffer.Take(receive_count).ToArray()));
                        return EXCEPTION_CRC_ERROR;
                    }
                    Frame = buffer.Take(receive_count).ToArray();
                    if((Frame[1] & 0x80) != 0) // Exception Detected
                    {
                        Console.WriteLine("Rx Exception: {0}", BitConverter.ToString(Frame));
                        return Frame[2];
                    }

                    Console.WriteLine("Rx : {0}", BitConverter.ToString(Frame));
                    return EXCEPTION_SUCCESS;
                }
            }

            // OVERFLOW
            Frame = null;
            return EXCEPTION_LENGTH_MISMATCH;
        }
        public override int ReceiveFrame(out byte[] Frame, int timeout, int expected_length = 0)
        {
            lock (Lock)
            {
                int status = 0;
                if (expected_length == 0)
                    status = ReceiveFrameInternal(out Frame, timeout);
                else
                    status = ReceiveFrameInternalExpected(out Frame, timeout, expected_length);
                return status;
            }
        }
        public override int ExchangeFrame(byte[] TxFrame, out byte[] RxFrame, int timeout, int expected_length = 0)
        {
            lock (Lock)
            {
                int status = 0;
                bool broadcast = TxFrame[0] == 0x00;
                poll_delay();
                FlushBuffer();
                TransmitFrameInternal(TxFrame);
                if (!broadcast)
                    if (expected_length == 0)
                        status = ReceiveFrameInternal(out RxFrame, timeout);
                    else
                        status = ReceiveFrameInternalExpected(out RxFrame, timeout, expected_length);
                else
                    RxFrame = null;
                poll_update(status);
                return status;
            }
        }

        public override byte[] MakeADUContext(byte unit_id, int sequence = 0)
        {
            byte[] ret = new byte[1] { unit_id };
            return ret;
        }

        public override byte[] PDUtoADU(byte[] ADUContext, byte[] ModbusPDU)
        {
            if(ADUContext.Length != 1)
            {
                return null;
            }

            // Modbus RTU ADU : Address + ModbusPDU + CRC
            byte[] ModbusADU = new byte[ModbusPDU.Length + 3];
            ModbusADU[0] = ADUContext[0];
            System.Buffer.BlockCopy(ModbusPDU, 0, ModbusADU, 1, ModbusPDU.Length);
            ushort crc = makeCRC(ModbusADU, ModbusADU.Length - 2);
            byte[] crcbytes = new byte[] { (byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF) };
            System.Buffer.BlockCopy(crcbytes, 0, ModbusADU, ModbusPDU.Length + 1, crcbytes.Length);
            return ModbusADU;
        }
        public override byte[] ADUtoPDU(byte[] ModbusADU)
        {
            if (ModbusADU == null | ModbusADU.Length == 0)
            {
                return new byte[0];
            }
            if(validateADU(ModbusADU, ModbusADU.Length))
            {
                int len = ModbusADU.Length;
                return ModbusADU.Skip(1).Take(len - 3).ToArray();
            }
            else
            {
                return null;
            }
        }
        public byte[] GetDataValue(byte[] ReadPDU)
        {
            byte data_length = ReadPDU[1];
            byte[] data_value = ReadPDU.Skip(2).Take(data_length).ToArray();
            return data_value;
        }
        public byte[] GetFileRecord(byte[] FileRecordPDU)
        {
            byte data_length = FileRecordPDU[2];
            byte[] data_value = FileRecordPDU.Skip(3).Take(data_length).ToArray();
            return data_value;
        }

        public override byte[] GetADUContext(byte[] ModbusADU)
        {
            if (validateADU(ModbusADU, ModbusADU.Length))
            {
                int len = ModbusADU.Length;
                return ModbusADU.Skip(0).Take(1).ToArray();
            }
            else
            {
                return null;
            }

        }

    }

    public class ModbusTransportTCP : ModbusTransport
    {
        public TcpClient client;
        protected NetworkStream NS = null;

        public ModbusTransportTCP(TcpClient client)
        {
            this.client = client;
            NS = client.GetStream();
            // 소켓에서 메시지를 가져오는 스트림
        }
        public override void FlushBuffer()
        {
            NS.Flush();
        }
        private void TransmitFrameInternal(byte[] Frame)
        {
            NS.Write(Frame, 0, Frame.Length);
        }
        public override void TransmitFrame(byte[] Frame)
        {
            lock(Lock)
            {
                TransmitFrameInternal(Frame);
            }
        }
        private int ReceiveFrameInternal(out byte[] Frame, int timeout, int expected_length = 0)
        {
            Frame = new byte[1024];
            if (timeout > -1)
            {
                NS.ReadTimeout = timeout;
            }
            else
            {
                NS.ReadTimeout = Int32.MaxValue;
            }

            int received = NS.Read(Frame, 0, Frame.Length);
            Frame = Frame.Take(received).ToArray();
            int status = EXCEPTION_TIMEOUT;
            if (Frame.Length != 0)
            {
                status = EXCEPTION_SUCCESS;
            }
            return status;

        }
        public override int ReceiveFrame(out byte[] Frame, int timeout, int expected_length = 0)
        {
            lock (Lock)
            {
                int status = ReceiveFrameInternal(out Frame, timeout, expected_length);
                return status;
            }
        }
        public override int ExchangeFrame(byte[] TxFrame, out byte[] RxFrame, int timeout, int expected_length = 0)
        {
            lock (Lock)
            {
                TransmitFrameInternal(TxFrame);
                int status = ReceiveFrameInternal(out RxFrame, timeout, expected_length);
                return status;
            }
        }
        public override byte[] MakeADUContext(byte unit_id, int sequence = 0)
        {
            byte[] ADUContext = new byte[6]{
                (byte)((sequence >> 8) & 0xFF),
                (byte)((sequence >> 0) & 0xFF),
                0x00,
                0x00,
                0x00,
                unit_id };
            return ADUContext;
        }
        public override byte[] PDUtoADU(byte[] ADUContext, byte[] ModbusPDU)
        {
            var list = new List<byte>();
            list.AddRange(ADUContext);
            list.AddRange(ModbusPDU);
            byte[] ModbusADU = list.ToArray();
            ModbusADU[5] = (byte)(ModbusPDU.Length + 1);
            return ModbusADU;
        }
        public override byte[] ADUtoPDU(byte[] ModbusADU)
        {
            byte[] ModbusPDU = ModbusADU.Skip(6).ToArray();
            if(ModbusADU[5] != ModbusPDU.Length + 1)
            {
                throw new Exception("Invalid Modbus ADU");
            }
            return ModbusPDU;
        }
        public override byte[] GetADUContext(byte[] ModbusADU)
        {
            byte[] ADUContext = ModbusADU.Take(6).ToArray();
            if(ModbusADU[2] != 0x00 && ModbusADU[3] != 0x00)
            {
                throw new Exception("Invalid Modbus ADU");
            }
            return ADUContext;
        }
    }
}
