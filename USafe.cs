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
    class USafe
    {
        private NetworkStream ns;
        private TcpClient client;
        private MainWindow mainWindow;
        private int dataLength;
        private IoTDTO dto;
        public bool clientAlive = true;
        public SendACK sendDataClass = new SendACK();
        private const byte uSafe_controlCode = (byte)0X66;
        private DateTime startTime;
        private DateTime endTime;
        public USafe(TcpClient client, NetworkStream ns, MainWindow mainWindow)
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
                        Console.WriteLine("USafe클래스 ns.Read 에러: " + e.Message);
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
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                        {
                            mainWindow.img_usafe_temper.Source = DrawImage("temper");
                        }));
                        sendDataClass.SendSTS_ACK(dto, ns);
                        saveStatusDataForApp(dto);
                        try
                        {
                            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                            {
                                if (dto.capabilitydatatype_data[0] == 0) mainWindow.img_usafe_fire.Source = DrawImage("fireOff"); //화재
                                else mainWindow.img_usafe_fire.Source = DrawImage("fireOn");

                                if (dto.capabilitydatatype_data[1] == 0) mainWindow.img_usafe_smoke.Source = DrawImage("smokeOn2"); //연기
                                else mainWindow.img_usafe_smoke.Source = DrawImage("smokeOff2");

                                if (dto.capabilitydatatype_data[2] == 0) mainWindow.img_usafe_movement.Source = DrawImage("moveOff"); //동체
                                else mainWindow.img_usafe_movement.Source = DrawImage("moveOn");

                                float temperature = dto.capabilitydatatype_data[3];
                                temperature = (float)temperature - (((float)10 / (float)100) * 30);
                                mainWindow.lbl_usafe_temperature.Content = temperature + "ºC"; //온도

                                if (dto.capabilitydatatype_data[4] == 0) mainWindow.img_usafe_gas.Source = DrawImage("gasOff5"); //가스
                                else mainWindow.img_usafe_gas.Source = DrawImage("gasOn2");
                            }));
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show("else if (dto.Device_Name.Equals(U_Safe)) 에러 발생!\n" + e.Message);
                        }

                        if (dto.capabilitydatatype_data[7] == 0) //베터리 모드
                        {
                            try
                            {
                                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                                {
                                    mainWindow.img_usafe_powerMode.Source = DrawImage("batteryOff");
                                    mainWindow.lbl_batteryPercent.Content = "";
                                }));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("U_Safe_if(dto.capabilitydatatype_data[7] == 0) 에러 발생!\n" + e.Message);
                            }
                        }
                        else
                        {
                            try
                            {
                                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                                {
                                    mainWindow.img_usafe_powerMode.Source = DrawImage("batteryOn");
                                    mainWindow.lbl_batteryPercent.Content = getBatteryPercentage(dto.capabilitydatatype_data[8]) + "%";
                                }));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("U_Safe_if_else 에러 발생!\n" + e.Message);
                            }
                        }
                        /*
                            1 불꽃감지센서	10 (0x0A)	0 : 미감지, 1 : 감지	
                            2 연기감지센서	11 (0x0B)	0 : 미감지, 1 : 감지	
                            3 동체감지센서	12 (0x0C)	0 : 미감지, 1 : 감지	
                            4 온도센서	13 (0x0D)	0 ~ 100	
                            5 가스감지센서	14 (0x0E)		
                            6 통신 주기 설정	101 (0x65)	10 ~ 240	
                            7 비상 존 진입 여부	102 (0x66)	0 : 비상 존 미진입, 1 : 비상 존 진입	
                            8 파워 모드	103 (0x67)	0 : 아답타, 1 : 배터리	
                            9 배터리 잔량	104 (0x68)	level 0 ~ 4 (0: 0%, 4: 100%)
                         */
                    }
                    else if (dto.Cmd == 3 && dto.Stx == 123 && dto.Etx == 125)
                    {
                        Console.WriteLine("USafe 제어 성공");
                        saveControlForApp("100");
                    }
                }
                Thread.Sleep(700);
                TimeSpan diff = endTime - startTime;
                if (diff.Seconds > 30)
                {
                    //Console.WriteLine("30초 초과");
                    initClientStatusTable();
                }
            }
            try
            {
                if (ns != null || ns.CanRead) ns.Close();
                if (client != null || client.Connected) client.Close();
                initClientStatusTable();
            }
            catch (Exception e)
            {
                Console.WriteLine("USafe클래스 ns.Close() or client.Close()에러 : " + e.Message);
                initClientStatusTable();
                return;
            }
        }
        private void saveControlForApp(string status) //mainWindow usafeControlTable 에 제어여부를 저장하는 부분
        {
            JObject obj = new JObject();
            obj.Add("net", "on");
            obj.Add("status", status);
            if (mainWindow.usafeControlTable["U_Safe"] != null) mainWindow.usafeControlTable["U_Safe"] = obj;
            else mainWindow.usafeControlTable.Add("U_Safe", obj);
        }
        public void AllRelease()
        {
            try
            {
                if (!clientAlive || dto != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => SendControlData(uSafe_controlCode, (byte)0X02)));
                    saveControlForApp("200");
                }
            }
            catch (Exception ee)
            {
                MessageBox.Show("AllRelease 에러 발생!\n" + ee.ToString());
            }
        }

        public void AlermRelease()
        {
            try
            {
                if (!clientAlive || dto != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => SendControlData(uSafe_controlCode, (byte)0X01)));
                    saveControlForApp("200");
                }
            }
            catch (Exception ee)
            {
                MessageBox.Show("AlermRelease 에러 발생!\n" + ee.ToString());
            }
        }

        public void EmerRelease()
        {
            try
            {
                if (!clientAlive || dto != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
                        () => SendControlData(uSafe_controlCode, (byte)0X00)));
                    saveControlForApp("200");
                }
            }
            catch (Exception ee)
            {
                MessageBox.Show("EmerRelease 에러 발생!\n" + ee.ToString());
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
                String data_str = "Send to Usafe_{";
                foreach (var data_ in SysControlData)
                {
                    data_str += String.Format("{0:X}", data_) + "-";
                } data_str = data_str.Remove(data_str.Length - 1);
                //Console.WriteLine(data_str + " } Length : " + SysControlData.Length);
                makeLogFile2(data_str + " } Length : " + SysControlData.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine("USafe 클래스 SendControlData() 에러: "+e.Message);
                this.clientAlive = false;
                initClientStatusTable();
                return;
            }
        }
        private void saveStatusDataForApp(IoTDTO dto) //mainWindow clientStatusTable에 데이터를 저장하는 부분
        {
            JObject obj = new JObject();
            obj.Add("net", "on");
            obj.Add("fire", dto.capabilitydatatype_data[0].ToString());
            obj.Add("smoke", dto.capabilitydatatype_data[1].ToString());
            obj.Add("gas", dto.capabilitydatatype_data[4].ToString());

            if (dto.capabilitydatatype_data[0] == 0 && dto.capabilitydatatype_data[1] == 0 && dto.capabilitydatatype_data[4] == 0)
            {
                mainWindow.usafeControl = false;
                obj.Add("alerm", "off");
            }
            else if (dto.capabilitydatatype_data[0] == 1 || dto.capabilitydatatype_data[1] == 1 || dto.capabilitydatatype_data[4] == 1)
            {
                if (mainWindow.usafeControl == true) //usafe 알람 끄는 control을 했으면
                {
                    obj.Add("alerm", "off");
                }
                else obj.Add("alerm", "on");
            }
            else Console.WriteLine("usafe_saveStatusDataForApp 이게 안걸릴리가 없지 않아?");

            if (mainWindow.clientStatusTable["U_Safe"] != null) mainWindow.clientStatusTable["U_Safe"] = obj;
            else mainWindow.clientStatusTable.Add("U_Safe", obj);
        }
        private void printOriginData(byte[] data)
        {
            String data_str = "Usafe_{ ";
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
            //mainWindow.img_plugLockStatus.Source = bit;
            return bit;
        }
        private void makeLogFile(string line)
        {
            string directory = @"C:\Temp\IoT Integrate Log\USafe\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] USafe Log_Received.txt", true))
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
            string directory = @"C:\Temp\IoT Integrate Log\USafe\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] USafe Log_Send.txt", true))
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
        private string getBatteryPercentage(byte p)
        {
            //0~4 0% 100%
            if (p == 0) return "0";
            if (p == 1) return "25";
            if (p == 2) return "50";
            if (p == 3) return "75";
            if (p == 4) return "100";
            else return "error";
        }
        private void initClientStatusTable()
        {
            //Console.WriteLine("initClientStatusTable() 호출");
            //Dashboard초기화
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
            {
                mainWindow.img_usafe_temper.Source = DrawImage("waitTemper");
                mainWindow.img_usafe_fire.Source = DrawImage("fireWait");
                mainWindow.img_usafe_smoke.Source = DrawImage("smokeWait");
                mainWindow.img_usafe_movement.Source = DrawImage("waitMovement2");
                mainWindow.img_usafe_gas.Source = DrawImage("gasWait");
                mainWindow.img_usafe_powerMode.Source = DrawImage("batteryWait");
                mainWindow.lbl_usafe_temperature.Content = "...";
            }));
            

            //APP 전송 객체 초기화
            JObject plainObj = new JObject();
            plainObj.Add("net", "off");

            if (mainWindow.clientStatusTable != null)
            {
                lock (mainWindow.clientStatusTable)
                {
                    if (mainWindow.clientStatusTable["U_Safe"] != null) mainWindow.clientStatusTable["U_Safe"] = plainObj;
                    else mainWindow.clientStatusTable.Add("U_Safe", plainObj);
                }
            }
            //this.clientAlive = false;
        }
    }
}
