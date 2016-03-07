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
    class PlugSocket
    {
        private NetworkStream ns;
        private TcpClient client;
        private MainWindow mainWindow;
        private int dataLength;
        private IoTDTO dto;
        public bool clientAlive = true;
        public SendACK sendDataClass = new SendACK();
        private const byte plug_controlCode = (byte)0X69;
        private bool plugLockUnlockToggle = false;
        private DateTime startTime;
        private DateTime endTime;

        public PlugSocket(TcpClient client, NetworkStream ns, MainWindow mainWindow)
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
                        Console.WriteLine("PlugSocket클래스 ns.Read 에러: " + e.Message);
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
                        sendDataClass.SendSTS_ACK(dto, ns); //status에 대한 ACK전송
                        
                        if (dto.capabilitydatatype_data[1] == 0) //0 off 1 on
                        {
                            saveStatusDataForApp("off");
                            try
                            {
                                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                                {
                                    mainWindow.img_plugLockStatus.Source = DrawImage("plugLock"); //plug socket이 비활성화 되어 있는 상태
                                    mainWindow.img_PlugLockUnlock.Source = DrawImage("unlock"); // 버튼이미지를 unlock으로 설정해 활성화가 가능하게
                                    plugLockUnlockToggle = true; //이벤트 입력시 해제 명령을 내림
                                    mainWindow.lbl_plugStatus.Content = "Disabled";
                                }));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("IoT_Plug if(dto.capabilitydatatype_data[1] == 0)에러 발생!\n" + e.Message);
                            }
                        }
                        else
                        {
                            saveStatusDataForApp("on");
                            try
                            {
                                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                                {
                                    mainWindow.img_plugLockStatus.Source = DrawImage("plugUnlock");
                                    mainWindow.img_PlugLockUnlock.Source = DrawImage("lock");
                                    plugLockUnlockToggle = false;
                                    mainWindow.lbl_plugStatus.Content = "Enabled";
                                }));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("IoT_Plug if_else 에러 발생!\n" + e.Message);
                            }
                        }
                    }
                    else if (dto.Cmd == 131 && dto.Stx == 123 && dto.Etx == 125)
                    {
                        if (dto.Result == 1)
                        {
                            Console.WriteLine("Plug 제어 실패 다시 전송 요청합니다");
                            if (plugLockUnlockToggle == true) saveStatusDataForApp("on");
                            else saveStatusDataForApp("off");
                            PlugControl();
                        }
                        else if (dto.Result == 0)
                        {
                            Console.WriteLine("Plug 제어 성공");
                            if (plugLockUnlockToggle == true) saveStatusDataForApp("off");
                            else saveStatusDataForApp("on");
                            //이때 상태값 변경해서 넣기 Thread sleep 이 먹는지도 확인!!
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
                Console.WriteLine("Plug 클래스 ns.Close() or client.Close()에러 : " + e.Message);
                initClientStatusTable();
                return;
            }
        }

        private void saveStatusDataForApp(string status) //mainWindow clientStatusTable에 데이터를 저장하는 부분
        {
            JObject obj = new JObject();
            obj.Add("net", "on");
            obj.Add("status", status);
            if (mainWindow.clientStatusTable["IoT_Plug"] != null) mainWindow.clientStatusTable["IoT_Plug"] = obj;
            else mainWindow.clientStatusTable.Add("IoT_Plug", obj);
        }
        
        public void PlugControl()//////////// 초록색 lock버튼을 눌렀을때
        {
            try
            {
                if (!clientAlive || dto == null) return;
                if (plugLockUnlockToggle == true)
                {
                    SendControlData(plug_controlCode, (byte)0X01); //플러그가 비활성이니까 활성되게 //1 : On 
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => mainWindow.img_PlugLockUnlock.Source = DrawImage("unlock")
                    ));
                    plugLockUnlockToggle = false;
                }
                else
                {
                    SendControlData(plug_controlCode, (byte)0X00); //0 : Off
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => mainWindow.img_PlugLockUnlock.Source = DrawImage("lock") //해제버튼이 먹었으면 lock버튼화
                    ));
                    plugLockUnlockToggle = true;
                }
            }
            catch (Exception ee)
            {
                MessageBox.Show("PlugLockControl 에러 발생!\n" + ee.ToString());
            }
        }

        public void PlugControlFromApp(string control)//////////// 초록색 lock버튼을 눌렀을때
        {
            try
            {
                if (!clientAlive || dto == null) return;
                if (control.Equals("1")) // on
                {
                    if (plugLockUnlockToggle == false) return;
                    SendControlData(plug_controlCode, (byte)0X01); //플러그가 비활성이니까 활성되게 //1 : On 
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => mainWindow.img_PlugLockUnlock.Source = DrawImage("unlock")
                    ));
                    plugLockUnlockToggle = false;
                }
                else // off
                {
                    if (plugLockUnlockToggle == true) return;
                    SendControlData(plug_controlCode, (byte)0X00); //플러그가 비활성이니까 활성되게 //0 : Off
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => mainWindow.img_PlugLockUnlock.Source = DrawImage("lock")
                    ));
                    plugLockUnlockToggle = true;
                }
                
            }
            catch (Exception ee)
            {
                MessageBox.Show("PlugLockControl 에러 발생!\n" + ee.ToString());
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
                if (ns.CanWrite) ns.Write(SysControlData, 0, SysControlData.Length);
                String data_str = "Plug_{";
                foreach (var data_ in SysControlData)
                {
                    data_str += String.Format("{0:X}", data_) + "-";
                } data_str = data_str.Remove(data_str.Length - 1);
                makeLogFile2(data_str + " } Length : " + SysControlData.Length);

            }
            catch (Exception e)
            {
                Console.WriteLine("PlugSocket 클래스 SendControlData 에러 : " + e.Message);
                initClientStatusTable();
                clientAlive = false;
                return;
            }
        }
        private void printOriginData(byte[] data)
        {          
            String data_str = "Plug_{ ";
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
            string directory = @"C:\Temp\IoT Integrate Log\PlugSocket\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] PlugSocket Log_Received.txt", true))
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
            string directory = @"C:\Temp\IoT Integrate Log\PlugSocket\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] PlugSocket Log_Send.txt", true))
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
            //Dashboard초기화
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
            {
                mainWindow.img_plugLockStatus.Source = DrawImage("plugWait"); //plug socket이 비활성화 되어 있는 상태
                mainWindow.img_PlugLockUnlock.Source = DrawImage("lockWait3"); // 버튼이미지를 unlock으로 설정해 활성화가 가능하게                
                mainWindow.lbl_plugStatus.Content = "";
            }));

            //APP전송 객체 초기화 부분 net off
            JObject plainObj = new JObject();
            plainObj.Add("net", "off");

            if (mainWindow.clientStatusTable != null)
            {
                lock (mainWindow.clientStatusTable)
                {
                    if (mainWindow.clientStatusTable["IoT_Plug"] != null) mainWindow.clientStatusTable["IoT_Plug"] = plainObj;
                    else mainWindow.clientStatusTable.Add("IoT_Plug", plainObj);
                }
            }
            //this.clientAlive = false;
        }
    }
}
