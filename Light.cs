using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SoftwareJinHeung
{
    class Light
    {
        private NetworkStream ns;
        private TcpClient client;
        private MainWindow mainWindow;
        private int dataLength;
        private IoTDTO dto;
        public bool clientAlive = true;
        public SendACK sendDataClass = new SendACK();
        private const byte light_controlCode = (byte)0X67;
        private DateTime startTime;
        private DateTime endTime;

        public Light(TcpClient client, NetworkStream ns, MainWindow mainWindow)
        {
            this.ns = ns;
            this.client = client;
            this.mainWindow = mainWindow;
        }

        internal void Accept_Status_Data(object state)
        {
            while (clientAlive)
            {
                endTime = DateTime.Now;
                try
                {
                    dataLength = client.Available;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Light클래스 client.Available 에러: " + e.Message);
                    initClientStatusTable();
                    return;
                }
                if (dataLength > 0)
                {
                    startTime = DateTime.Now;
                    byte[] data = new byte[dataLength];
                    try
                    {
                        ns.Read(data, 0, dataLength);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Light클래스 ns.Read 에러: " + e.Message);
                        initClientStatusTable();
                        return;
                    }
                    printOriginData(data); //날데이터 찍어보기
                    dto = new IoTDTO(data);
                    if (dto.Cmd == 1 && dto.Stx == 123 && dto.Etx == 125)
                    {
                        sendDataClass.SendREGDEV_ACK(dto, ns);
                    }
                    else if (dto.Cmd == 2 && dto.Stx == 123 && dto.Etx == 125)
                    {
                        sendDataClass.SendSTS_ACK(dto, ns);

                        if (mainWindow.lightControlFromApp == true)
                        {
                            if (mainWindow.lightControl != dto.capabilitydatatype_data[1]) Light_slider_ValueChanged(mainWindow.lightControl+""); //light control명령이 제대로 먹히지 않은 경우
                        }

                        saveStatusDataForApp(dto.capabilitydatatype_data[1].ToString());

                        try
                        {
                            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                            {
                                mainWindow.lbl_lightLevel.Content = dto.capabilitydatatype_data[1];
                            }));
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show("else if (dto.Device_Name.Equals(IoT_Light)) 에러 발생!\n" + e.Message);
                        }
                    }
                    else if (dto.Cmd == 131 && dto.Stx == 123 && dto.Etx == 125)
                    {
                        if (dto.Result == 1)
                        {
                            Console.WriteLine("Light 제어 실패 다시 전송 요청합니다");
                            Light_slider_ValueChanged(null);
                        }
                        else if (dto.Result == 0)
                        {
                            Console.WriteLine("Light 제어 성공");
                            saveStatusDataForApp(mainWindow.lightControl+"");
                            mainWindow.lightControlFromApp = false;
                            try
                            {
                                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                                {
                                    mainWindow.lbl_lightLevel.Content = mainWindow.lightControl;
                                }));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("else if (dto.Device_Name.Equals(IoT_Light)) 에러 발생!\n" + e.Message);
                            }
                        }
                    }
                }
                Thread.Sleep(700);
                TimeSpan diff = endTime - startTime;
                if (diff.Seconds > 30) initClientStatusTable();
            }
            try
            {
                if (ns != null || ns.CanRead) ns.Close();
                if (client != null || client.Connected) client.Close();
                initClientStatusTable();
            }
            catch (Exception e)
            {
                Console.WriteLine("Light클래스  ns.Close() or client.Close()에러 : " + e.Message);
                initClientStatusTable();
                return;
            }
        }
        public void Light_slider_ValueChanged(string lightLevel)
        {
            if (lightLevel != null)
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                {
                    SendControlData(light_controlCode, (byte)Int32.Parse(lightLevel));
                    mainWindow.lightControl = (byte)Int32.Parse(lightLevel);
                }));
            }
            else
            {
                try
                {
                    if (!clientAlive || dto == null) return;
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                    {
                        SendControlData(light_controlCode, (byte)Int32.Parse(mainWindow.slider_light.Value.ToString()));
                        mainWindow.lightControl = (byte)Int32.Parse(mainWindow.slider_light.Value.ToString());
                    }));

                }
                catch (Exception ee)
                {
                    MessageBox.Show("Light_slider_ValueChanged 에러 발생!\n" + ee.ToString());
                }
            }
        }
        private void SendControlData(byte controlCode, byte controlData)
        {
            byte[] SysControlData = new byte[11];
            SysControlData[0] = dto.Stx;
            SysControlData[1] = dto.Ver;
            SysControlData[2] = dto.Seq[0];
            SysControlData[3] = dto.Seq[1];
            SysControlData[4] = (byte)0X03;
            SysControlData[5] = (byte)0X00;
            SysControlData[6] = (byte)0X02;

            SysControlData[7] = controlCode;
            SysControlData[8] = controlData;

            SysControlData[9] = dto.Cs;
            SysControlData[10] = dto.Etx;

            Console.WriteLine("========제어 데이터 보냄=======");

            try
            {
                if (ns == null || !ns.CanWrite) return;
                else ns.Write(SysControlData, 0, SysControlData.Length);
                String data_str = "Light_{";
                foreach (var data_ in SysControlData)
                {
                    data_str += String.Format("{0:X}", data_) + "-";
                } data_str = data_str.Remove(data_str.Length - 1);
                makeLogFile2(data_str + " } Length : " + SysControlData.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine("Light 클래스 SendControlData() 에러: " + e.Message);
                clientAlive = false;
                initClientStatusTable();
                return;
            }
        }
        private void saveStatusDataForApp(string status) //mainWindow clientStatusTable에 데이터를 저장하는 부분
        {
            JObject obj = new JObject();
            obj.Add("net", "on");
            obj.Add("status", status);
            if (mainWindow.clientStatusTable["IoT_Light"] != null) mainWindow.clientStatusTable["IoT_Light"] = obj;
            else mainWindow.clientStatusTable.Add("IoT_Light", obj);
        }
        private void printOriginData(byte[] data)
        {
            String data_str = "Light_{ ";
            foreach (var data_ in data)
            {
                data_str += data_ + ", ";
            }
            data_str = data_str.Remove(data_str.Length - 2);
            Console.WriteLine(data_str + " } Length : " + data.Length);
            makeLogFile(data_str + " } Length : " + data.Length);
        }
        private void makeLogFile(string line)
        {
            string directory = @"C:\Temp\IoT Integrate Log\Light\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] Light Log_Received.txt", true))
                {
                    lock (file)
                    {
                        file.WriteLine("[" + DateTime.Now + "] " + line);
                        file.Flush();
                        file.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("makeLogFile에러: " + e.Message);
                return;
            }
        }
        private void makeLogFile2(string line)
        {
            string directory = @"C:\Temp\IoT Integrate Log\Light\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] Light Log_Send.txt", true))
                {
                    lock (file)
                    {
                        file.WriteLine("[" + DateTime.Now + "] " + line);
                        file.Flush();
                        file.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("makeLogFile에러: " + e.Message);
                return;
            }
        }
        private BitmapImage DrawImage(string imgName)
        {
            BitmapImage bit = new BitmapImage();
            bit.BeginInit();
            bit.UriSource = new Uri("pack://application:,,,/SoftwareJinHeung;component/Image/EmergencyIcon/" + imgName + ".png");
            bit.EndInit();
            //mainWindow.img_plugLockStatus.Source = bit;
            return bit;
        }
        private void initClientStatusTable()
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => mainWindow.lbl_lightLevel.Content = "Wait..."));

            JObject plainObj = new JObject();
            plainObj.Add("net", "off");

            if (mainWindow.clientStatusTable != null)
            {
                lock (mainWindow.clientStatusTable)
                {
                    if (mainWindow.clientStatusTable["IoT_Light"] != null) mainWindow.clientStatusTable["IoT_Light"] = plainObj;
                    else mainWindow.clientStatusTable.Add("IoT_Light", plainObj);
                }
            }
            //this.clientAlive = false;
        }
    }
}
