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
    class Valve
    {
        private NetworkStream ns;
        private TcpClient client;
        private MainWindow mainWindow;
        private int dataLength;
        private IoTDTO dto;
        public bool clientAlive = true;
        public SendACK sendDataClass = new SendACK();
        private const byte valve_controlCode = (byte)0X68;
        private bool valveLockUnlockToggle = false;
        private DateTime startTime;
        private DateTime endTime;
        public Valve(TcpClient client, NetworkStream ns, MainWindow mainWindow)
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
                    Console.WriteLine("PlugSocket클래스 client.Available 에러: " + e.Message);
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
                        Console.WriteLine("Valve 클래스 ns.Read 에러: " + e.Message);
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
                        if (dto.capabilitydatatype_data[1] == 0) //상태값 0이면 해제 1이면 잠금
                        {
                            saveStatusDataForApp("unlock");
                            try
                            {
                                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                                {
                                    mainWindow.img_valveLockUnlockStatus.Source = DrawImage("valveUnlock");
                                    mainWindow.img_valveLockControl.Source = DrawImage("lock");
                                    mainWindow.lbl_valveStatus.Content = "Unlock";
                                    valveLockUnlockToggle = false; //valve 상태가 해제일 때
                                }));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("IoT_Valve if(dto.capabilitydatatype_data[1] == 0) 에러 발생!\n" + e.Message);
                            }
                        }
                        else
                        {
                            saveStatusDataForApp("lock");
                            try
                            {
                                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                                {
                                    mainWindow.img_valveLockUnlockStatus.Source = DrawImage("valveLock");
                                    mainWindow.img_valveLockControl.Source = DrawImage("unlock");
                                    mainWindow.lbl_valveStatus.Content = "Lock";
                                    valveLockUnlockToggle = true;//valve 상태가 잠겼을 때
                                }));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("IoT_Valve if_else에러 발생!\n" + e.Message);
                            }
                        }
                    }
                    else if (dto.Cmd == 131 && dto.Stx == 123 && dto.Etx == 125)
                    {
                        if (dto.Result == 1)
                        {
                            Console.WriteLine("Valve 제어 실패 다시 전송 요청합니다");
                            if (valveLockUnlockToggle == true) saveStatusDataForApp("unlock");
                            else saveStatusDataForApp("lock");
                            ValveControl();
                        }
                        else if (dto.Result == 0)
                        {
                            Console.WriteLine("Valve 제어 성공");
                            if (valveLockUnlockToggle == true) saveStatusDataForApp("lock");
                            else saveStatusDataForApp("unlock");
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
                Console.WriteLine("Valve 클래스 ns.Close() or client.Close()에러 : " + e.Message);
                initClientStatusTable();
                return;
            }
        }
        public void ValveControl()
        {
            try
            {
                if (!clientAlive || dto == null) return;
                if (valveLockUnlockToggle == true)
                {
                    SendControlData(valve_controlCode, (byte)0X00); //벨브가 잠겼으니까 해제버튼먹게
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => mainWindow.img_valveLockControl.Source = DrawImage("lock") //해제버튼이 먹었으면 lock버튼화
                    ));
                    valveLockUnlockToggle = false;
                }
                else
                {
                    SendControlData(valve_controlCode, (byte)0X01);
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => mainWindow.img_valveLockControl.Source = DrawImage("unlock")
                    ));
                    valveLockUnlockToggle = true;
                }
            }
            catch (Exception ee)
            {
                MessageBox.Show("valveControl 에러 발생!\n" + ee.ToString());
            }
        }

        public void ValveControlFromApp(string control)
        {
            try
            {
                if (!clientAlive || dto == null) return;
                if (control.Equals("0")) //해제
                {
                    if (valveLockUnlockToggle == false) return;
                    SendControlData(valve_controlCode, (byte)0X00); //벨브가 잠겼으니까 해제버튼먹게
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => mainWindow.img_valveLockControl.Source = DrawImage("lock") //해제버튼이 먹었으면 lock버튼화
                    ));
                    valveLockUnlockToggle = false;
                }
                else // 잠금
                {
                    if (valveLockUnlockToggle == true) return;
                    SendControlData(valve_controlCode, (byte)0X01);
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => mainWindow.img_valveLockControl.Source = DrawImage("unlock")
                    ));
                    valveLockUnlockToggle = true;
                }
            }
            catch (Exception ee)
            {
                MessageBox.Show("valveControl 에러 발생!\n" + ee.ToString());
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
                String data_str = "Valve_{";
                foreach (var data_ in SysControlData)
                {
                    data_str += String.Format("{0:X}", data_) + "-";
                } data_str = data_str.Remove(data_str.Length - 1);
                makeLogFile2(data_str + " } Length : " + SysControlData.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine("Valve 클래스 SendControlData()에러 : " + e.Message);
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
            if (mainWindow.clientStatusTable["IoT_Valve"] != null) mainWindow.clientStatusTable["IoT_Valve"] = obj;
            else mainWindow.clientStatusTable.Add("IoT_Valve", obj);
        }
        private void printOriginData(byte[] data)
        {
            String data_str = "Valve_{ ";
            foreach (var data_ in data)
            {
                data_str += data_ + ", ";
            }
            data_str = data_str.Remove(data_str.Length - 2);
            Console.WriteLine(data_str + " } Length : " + data.Length);
            makeLogFile(data_str + " } Length : " + data.Length);
        }
        private BitmapImage DrawImage(string imgName)
        {
            BitmapImage bit = new BitmapImage();
            bit.BeginInit();
            bit.UriSource = new Uri("pack://application:,,,/SoftwareJinHeung;component/Image/EmergencyIcon/" + imgName + ".png");
            bit.EndInit();
            return bit;
        }
        private void initClientStatusTable()
        {
            //Dashboard초기화
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
            {
                mainWindow.img_valveLockUnlockStatus.Source = DrawImage("valveWait2"); //plug socket이 비활성화 되어 있는 상태
                mainWindow.img_valveLockControl.Source = DrawImage("lockWait3"); // 버튼이미지를 unlock으로 설정해 활성화가 가능하게                
                mainWindow.lbl_valveStatus.Content = "";
            }));

            //APP전송 객체 초기화 부분 net off
            JObject plainObj = new JObject();
            plainObj.Add("net", "off");

            if (mainWindow.clientStatusTable != null)
            {
                lock (mainWindow.clientStatusTable)
                {
                    if (mainWindow.clientStatusTable["IoT_Valve"] != null) mainWindow.clientStatusTable["IoT_Valve"] = plainObj;
                    else mainWindow.clientStatusTable.Add("IoT_Valve", plainObj);
                }
            }
            //this.clientAlive = false;
        }
        private void makeLogFile(string line)
        {
            string directory = @"C:\Temp\IoT Integrate Log\Valve\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] Valve Log_Received.txt", true))
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
            string directory = @"C:\Temp\IoT Integrate Log\Valve\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] Valve Log_Send.txt", true))
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
    }
}
