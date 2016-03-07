using Newtonsoft.Json.Linq;
using System;
using System.Collections;
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
    class Phone
    {
        private MainWindow mainWindow;
        private TcpClient client;
        private NetworkStream ns;
        
        private bool clientAlive;

        public Phone(MainWindow mainWindow, TcpClient client)
        {
            this.mainWindow = mainWindow;
            this.client = client;
            ns = client.GetStream();
            clientAlive = true;
            
        }
        public void AcceptRequest(object sdf)
        {
            try
            {
                string cmd = "";
                string phone = "";
                string device = "";
                string control = "";
                initClientStatusTable();

                while (clientAlive)
                {
                    int dataLength = client.Available;

                    if (dataLength > 0)
                    {
                        byte[] data = new byte[dataLength];
                        try
                        {
                            ns.Read(data, 0, data.Length);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Read도중 에러 발생: " + e.ToString());
                        }
                       
                        //핸드폰으로부터 데이터를 읽어들이는 부분
                        string inputData = Encoding.UTF8.GetString(data, 0, data.Length);
                        JObject obj = JObject.Parse(inputData);                        
                        // 상태
                        if (obj.GetValue("phone") != null) phone = obj.GetValue("phone").ToString();
                        if (obj.GetValue("CMD") != null) cmd = obj.GetValue("CMD").ToString();                        
                        // + 제어
                        if (obj.GetValue("device") != null) device = obj.GetValue("device").ToString();
                        if (obj.GetValue("control") != null) control = obj.GetValue("control").ToString();
                        
                        Console.WriteLine("inputData: " + inputData);
                        makeLogFile("InputData_ " + inputData);
                        //Console.WriteLine("phone: " + phone);
                        //Console.WriteLine("cmd: " + cmd);

                        if (cmd.Equals("1"))
                        {
                            lock (mainWindow.clientStatusTable)
                            {
                                obj.RemoveAll();
                                obj.Add("IoT_Plug", (JObject)mainWindow.clientStatusTable["IoT_Plug"]);
                                obj.Add("IoT_Sensor", (JObject)mainWindow.clientStatusTable["IoT_Sensor"]);
                                obj.Add("IoT_Light", (JObject)mainWindow.clientStatusTable["IoT_Light"]);
                                obj.Add("IoT_Valve", (JObject)mainWindow.clientStatusTable["IoT_Valve"]);
                                obj.Add("U_Safe", (JObject)mainWindow.clientStatusTable["U_Safe"]);
                                WriteData(obj);
                            }
                            //return;
                        }
                        else if (cmd.Equals("2"))
                        {
                            ShowControlFromApp(device);
                            if (mainWindow.clientTable != null)
                            {
                                lock (mainWindow.clientTable)
                                {
                                    ICollection clients = mainWindow.clientTable.Values;
                                    foreach (object clientClass in clients)
                                    {
                                        if (device.Equals("IoT_Plug") && clientClass.GetType() == typeof(PlugSocket))
                                        {
                                            ((PlugSocket)clientClass).PlugControlFromApp(control);
                                            Thread.Sleep(2000); //2초간 쉬었다가 꺼내봐 //1초는 안댐
                                            obj.RemoveAll();
                                            obj.Add("IoT_Plug", (JObject)mainWindow.clientStatusTable["IoT_Plug"]);
                                            WriteData(obj);
                                            //return;
                                        }
                                        else if (device.Equals("IoT_Valve") && clientClass.GetType() == typeof(Valve))
                                        {
                                            ((Valve)clientClass).ValveControlFromApp(control);
                                            Thread.Sleep(2000);
                                            obj.RemoveAll();
                                            obj.Add("IoT_Valve", (JObject)mainWindow.clientStatusTable["IoT_Valve"]);
                                            WriteData(obj);
                                            //return;
                                        }
                                        else if (device.Equals("IoT_Light") && clientClass.GetType() == typeof(Light))
                                        {
                                            if (mainWindow.lightControl.ToString().Equals(control)) return;

                                            mainWindow.lightControlFromApp = true;

                                            ((Light)clientClass).Light_slider_ValueChanged(control);
                                            
                                            if(control != null)mainWindow.lightControl = Int32.Parse(control);
                                            
                                            Thread.Sleep(2000);
                                            obj.RemoveAll();
                                            obj.Add("IoT_Light", (JObject)mainWindow.clientStatusTable["IoT_Light"]);
                                            WriteData(obj);
                                            //return;
                                        }
                                        else if (device.Equals("U_Safe") && clientClass.GetType() == typeof(USafe))
                                        {
                                            mainWindow.usafeControl = false;
                                            if (control.Equals("0")) ((USafe)clientClass).EmerRelease(); // usafe 컨트롤
                                            else if (control.Equals("1")) ((USafe)clientClass).AlermRelease(); // usafe 컨트롤
                                            else if (control.Equals("2")) ((USafe)clientClass).AllRelease(); // usafe 컨트롤
                                            else Console.WriteLine("**From Phone: 잘못된 형식의 control: " + control);
                                            Thread.Sleep(2000);
                                            obj.RemoveAll();
                                            obj.Add("U_Safe", (JObject)mainWindow.usafeControlTable["U_Safe"]); //usafe같은 경우엔 알림을 껏는지가 아닌 성공여부가 더 중요할듯허이
                                            WriteData(obj);
                                            //return;
                                        }
                                    }
                                    if (obj["CMD"] != null)
                                    {
                                        obj.RemoveAll();
                                        obj.Add(device, (JObject)mainWindow.clientStatusTable[device]);
                                        WriteData(obj);
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("mainWindow.clientTable == null...");
                            }
                        }
                        else
                        {
                            Console.WriteLine("**From Phone: 잘못된 형식의 cmd : " + cmd);
                        }                      
                    }
                    Thread.Sleep(700);
                } //while의 끝
            }
            catch (Exception e)
            {
                Console.WriteLine("Phone Server connect() exception 발생: " + e.ToString());
            }
        }

        private void ShowControlFromApp(string device)
        {
            switch (device)
            {
                case "IoT_Light":
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => mainWindow.img_ControlFromApp.Source = DrawImage("light")));
                    break;
                case "IoT_Valve":
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => mainWindow.img_ControlFromApp.Source = DrawImage("valve")));
                    break;
                case "IoT_Plug":
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => mainWindow.img_ControlFromApp.Source = DrawImage("plug")));
                    break;
                case "U_Safe":
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => mainWindow.img_ControlFromApp.Source = DrawImage("usafe")));
                    break;
            }
        }

        private void WriteData(JObject obj)
        {
            string outputData = obj.ToString();
            byte[] outData = Encoding.UTF8.GetBytes(outputData); //디바이스들의 현재상태값을 보내기
            try
            {
                ns.Write(outData, 0, outData.Length); //데이터 보내고 나면 이 연결 모두 끊음
                Console.WriteLine("=====요청한 데이터 보냄=====");
                makeLogFile2("OutputData_ "+outputData);
                this.clientAlive = false;
                try
                {
                    if (ns != null) ns.Close();
                    if (client != null) client.Close();
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate{
                        //mainWindow.img_phoneStatus.Source = DrawImage("phoneWait2");
                        mainWindow.img_ControlFromApp.Source = DrawImage("wait_white");
                    }));
                    Console.WriteLine("Disconnected...");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Close 도중 에러 발생: " + e.ToString());
                }
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine("Write도중 에러 발생: " + e.ToString());
            }
        }
        private void makeLogFile(string line)
        {
            string directory = @"C:\Temp\IoT Integrate Log\Phone\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] Phone Log_Received.txt", true))
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
            string directory = @"C:\Temp\IoT Integrate Log\Phone\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] Phone Log_Send.txt", true))
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
        private void initClientStatusTable()
        {
            if (mainWindow.usafeControlTable != null)
            {
                JObject plainObj = new JObject();
                plainObj.Add("net", "off");
                lock (mainWindow.usafeControlTable)
                {
                    if (mainWindow.usafeControlTable["U_Safe"] != null) mainWindow.usafeControlTable["U_Safe"] = plainObj;
                    else mainWindow.usafeControlTable.Add("U_Safe", plainObj);
                }
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

    }
}

