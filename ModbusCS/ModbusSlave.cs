using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;


namespace ModbusCS
{
    interface IModbusSlave
    {
        byte[] ProcessFrame(byte[] RxPDU);
    }

    public class ModbusTCPSlave : ModbusTransportTCP, IModbusSlave
    {
        Thread server_thread;
        public void Run_Server()
        {
            while(client.Connected == true)
            {
                byte[] RxFrame;
                int status = ReceiveFrame(out RxFrame, -1, 0);
                if(status != 0)
                {
                    continue;
                }
                byte[] ADUContext = GetADUContext(RxFrame);
                byte[] RxPDU = ADUtoPDU(RxFrame);
                byte[] TxPDU = ProcessFrame(RxPDU);
                byte[] TxFrame = PDUtoADU(ADUContext, TxPDU);
                TransmitFrame(TxFrame);
            }
        }
        public byte[] ProcessFrame(byte[] RxPDU)
        {
            byte fc = RxPDU[0];
            byte[] ret = new byte[0];
            
            switch (fc)
            {
                case 0x03:
                case 0x04:
                    ushort address = BitConverter.ToUInt16(RxPDU.Skip(1).Take(2).Reverse().ToArray(),0);
                    ushort quantity = BitConverter.ToUInt16(RxPDU.Skip(3).Take(2).Reverse().ToArray(), 0);
                    byte[] data = new byte[2 * quantity];
                    List<byte> list = new List<byte>();
                    list.Add(fc);
                    list.AddRange(data);
                    ret = list.ToArray();
                    break;
                default:
                    ret = new byte[2] { (byte)(0x80 | fc), 0x02 };
                    break;
            }
            return ret;
        }
        public ModbusTCPSlave(TcpClient client) : base(client)
        {
        }
    }
    public class ModbusBypassTCPSlave : ModbusTCPSlave
    {
        ModbusMasterRTU bypass;
        public void Run_Server()
        {
            while (client.Connected == true)
            {
                byte[] RxFrame;
                int status = ReceiveFrame(out RxFrame, -1, 0);
                if (status != 0)
                {
                    continue;
                }
                byte[] ADUContext = GetADUContext(RxFrame);
                // byte[] BypassADU = RxFrame.Skip(6).Take(ADUContext[5]).ToArray();
                byte[] BypassPDU = ADUtoPDU(RxFrame);
                byte[] Response = null;
                try{
                    status = bypass.ModbusBypass(ADUContext[6], BypassPDU, out Response);
                    if(Response != null){
                        byte[] TxFrame = ADUContext.Take(6).Concat(Response.AsEnumerable()).ToArray();
                        TransmitFrame(TxFrame);
                    }
                }
                catch
                {
                    
                }
            }
        }
        public ModbusBypassTCPSlave(TcpClient client, ModbusMasterRTU bypass) : base(client)
        {
            this.bypass = bypass;
        }
    }
}
