using RFIDReaderAPI;
using SQLite.Net;
using SQLite.Net.Platform.Win32;
using SQLite.Net.Attributes;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using Path = System.IO.Path;
using System.Collections.Generic;
using System.Threading;
using RFIDReaderAPI.Interface;
using RFIDReaderAPI.Models;

namespace RACDataDump
{
    #region Tags
    public class Tags
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [MaxLength(64)]
        public string EPC { get; set; }
        [MaxLength(64)]
        public string TID { get; set; }
        [MaxLength(64)]
        public string UserData { get; set; }
        public int AntennaNumber { get; set; }
        [MaxLength(32)]
        public string ReaderName { get; set; }
        public string Time { get; set; }
        public override string ToString()
        {
            return EPC;
        }
    }
    #endregion Tags

    #region Database
    public class Database : SQLiteConnection
    {
        public Database(string path) : base(new SQLitePlatformWin32(), path)
        {
            CreateTable<Tags>();
        }
        
        public IEnumerable<Tags> QueryTagsByEPC(string epc)
        {
            return Table<Tags>().Where(x => x.EPC == epc);
        }
        public IEnumerable<Tags> QueryTagsByTID(string tid)
        {
            return Table<Tags>().Where(x => x.TID == tid);
        }
        
        public Tags QueryEPC(string epc)
        {
            return (from s in Table<Tags>()
                    where s.EPC == epc
                    select s).FirstOrDefault();
        }

        public bool AddTag(RFIDReaderAPI.Models.Tag_Model tag)
        {
            try
            {
                var t = new Tags
                { EPC = tag.EPC, TID = tag.TID, UserData = tag.UserData, Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), AntennaNumber = tag.ANT_NUM, ReaderName = tag.ReaderName };
                Insert(t);
                return true;
            }
            catch (Exception ex) { Console.WriteLine("[ERROR] " + ex.Message); return false; }
        }
    }
    #endregion Database

    #region Program
    class Program : IAsynchronousMessage
    {
        static Program rapidaccess = new Program();
        static IPAddress ip;
        static string ReaderIP = "192.168.1.116:9090"; //default RFD7206X reader IP
        static string connectStr = string.Empty;
        public Database _db;
        static Boolean usbReader = false;
        static List<string> listUsbDevicePath = null;//Save the recognized usb device file path
        static public Dictionary<string, string> dic_UsbDevicePath_Name = new Dictionary<string, string>(); //Usb device path and device name Dictionary
        static bool SendPingCommand(IPAddress ip)
        {
            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();

            // Use the default Ttl value which is 128,
            // but change the fragmentation behavior.
            options.DontFragment = true;

            // Create a buffer of 32 bytes of data to be transmitted.
            string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            int timeout = 120;
            PingReply reply = pingSender.Send(ip, timeout, buffer, options);
            if (reply.Status == IPStatus.Success)
            {
                Console.WriteLine("Address: {0}", reply.Address.ToString());
                Console.WriteLine("RoundTrip time: {0}", reply.RoundtripTime);
                Console.WriteLine("Time to live: {0}", reply.Options.Ttl);
                Console.WriteLine("Don't fragment: {0}", reply.Options.DontFragment);
                Console.WriteLine("Buffer size: {0}", reply.Buffer.Length);
            }
            return (reply.Status == IPStatus.Success);
        }
        
        static void Main(string[] args)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
            Console.WriteLine(versionInfo.FileDescription + " " + versionInfo.FileVersion);
            Console.WriteLine(versionInfo.LegalCopyright + "\r\n");
            Arguments CommandLine = new Arguments(args);
            if (CommandLine["ip"] != null)
            {
                Console.WriteLine("ip value: " +
                    CommandLine["ip"]);
                try
                {
                    ip = IPAddress.Parse(CommandLine["ip"]);
                    if (SendPingCommand(ip))
                        ReaderIP = CommandLine["ip"] + ":9090";

                }
                catch (Exception ex) {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
            else
            {
                usbReader = true;
            }
            //Console.WriteLine("Note: ip parameter not defined, using default reader ip!");

            rapidaccess.Run();
        }
        #region RFID
        void Run()
        {
            if (usbReader)
            {
                try
                {
                    InitCom();
                    IntPtr Handle = new IntPtr();
                    connectStr = listUsbDevicePath[0];
                    Console.WriteLine("USB " + (RFIDReader.CreateUsbConn(connectStr, Handle, rapidaccess) ? "Connected" : "Failed"));
                }
                catch (Exception ex)
                {
                    Console.Beep();
                    Console.WriteLine("Error: USB Reader not found/unable to connect");
                    Console.WriteLine("Debug:" + ex.Message);
                }
            }
            else
            {
                connectStr = ReaderIP;
                try
                {
                    Console.WriteLine("TCP/IP " + (RFIDReader.CreateTcpConn(connectStr, rapidaccess) ? "Connected" : "Failed")); // TCP Connect
                }
                catch (Exception ex)
                {
                    Console.Beep();
                    Console.WriteLine("Error: Reader not found/unable to connect");
                    Console.WriteLine("Debug:" + ex.Message);
                }
            }
 
            if (connectStr != null)
            {
                Console.WriteLine("\r\nStart reading RFID Tags.. Press any key to exit program\r\n");
                RFIDReaderAPI.RFIDReader._RFIDConfig.SetTagUpdateParam(connectStr, 2000, 0);
                RFIDReaderAPI.RFIDReader._Tag6C.GetEPC_TID_UserData(connectStr, eAntennaNo._1, eReadType.Inventory, 0, 2); // change to eAntennaNo._2 for two antennas
                Console.ReadKey();
                RFIDReaderAPI.RFIDReader._Tag6C.Stop(connectStr); //  Stop
                RFIDReaderAPI.RFIDReader.CloseConn(connectStr); ; //  Close                    
            }
            Console.WriteLine("\r\nProgram exiting..");
            System.Environment.Exit(0);
        }
        void InitCom()
        {
            try
            {
                dic_UsbDevicePath_Name.Clear();
                listUsbDevicePath = RFIDReaderAPI.RFIDReader.GetUsbHidDeviceList();
                for (int i = 0; i < listUsbDevicePath.Count; i++)
                {
                    string path = listUsbDevicePath[i];
                    dic_UsbDevicePath_Name.Add(path, "UHF READER " + (i + 1));
                    string name = dic_UsbDevicePath_Name[path].ToString();
                }
            }
            catch { }
        }

        #endregion RFID
        #endregion Program

        #region Interface Method
        // Tag Data
        void Initialize()
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "rfidtags.db");
            _db = new Database(dbPath);
        }

        public void OutPutTags(RFIDReaderAPI.Models.Tag_Model tag)
        {
            Initialize();
            try
            {
                if (_db.AddTag(tag))
                Console.WriteLine("EPC:" + tag.EPC + " - TID:" + tag.TID + " - UserData:" + tag.UserData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        public void WriteDebugMsg(string msg)
        {

        }

        public void WriteLog(string msg)
        {

        }

        public void PortConnecting(string connID)
        {

        }

        public void PortClosing(string connID)
        {

        }

        public void OutPutTagsOver()
        {

        }
        public void GPIControlMsg(GPI_Model gpi_model)
        {
            //throw new NotImplementedException();
        }

        public void EventUpload(CallBackEnum type, object param)
        {
            //throw new NotImplementedException();
        }

    }
    #endregion
}
