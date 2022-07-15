using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions; // Regex

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ModbusCS
{
    class Program
    {
        static void Main(string[] args)
        {
            SerialMain(args);
        }
        static void GatewayMain(string[] args)
        {
            TcpListener Listener = null;
            TcpClient client = null;

            int PORT = 5555;

            Console.WriteLine("서버소켓");
            try
            {
                Listener = new TcpListener(PORT);
                Listener.Start(); // Listener 동작 시작

                while (true)
                {
                   // client =
                   //Listener.AcceptTcpClient();
                   // Receiver r = new Receiver();

                   // r.startClient(client);

                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
            }
            finally
            {
            }
        }
        static void SerialMain(string[] args)
        {
            Console.WriteLine("Smart LV Gateway Rev2");
            string SerialPortName = "COM4";
            int SerialBaudrate = 38400;
            ModbusMasterRTU Ch1 = new ModbusMasterRTU(1, SerialPortName, SerialBaudrate);
            List<DeviceType> device_list = AutoDiscovery.AutoDiscoveryRange(Ch1, 1, 4, 4, SerialBaudrate);
            foreach (DeviceType device in device_list)
            {
                Console.WriteLine("LID: {0} PID: {1} Name: {2}", device.logical_id, device.physical_id, device.device_name);
            }
            Console.ReadLine();
            int index = ACBOCR.GetLastWaveNumber(Ch1, 1);
            ACBOCR.WaveComtrade Comtrade = ACBOCR.PollWaveComtrade(Ch1, 1, (byte)index);
            Console.WriteLine(Comtrade.GetFileName("192.168.0.2", 257));
            Console.WriteLine(Comtrade.GetFileContents("ACBOCR-A", 257));
            ACBOCR.TripWaveFileRecord wave_record = ACBOCR.PollWaveData(Ch1, 1, (byte)index);
            Console.WriteLine(wave_record.Count());
            Console.WriteLine(wave_record.GetFileContents());
            ACBOCR.SaveFile(Comtrade, wave_record, "ACBOCR-A", "192.168.0.2", 257);

            Console.ReadLine();
        }
    }
}
