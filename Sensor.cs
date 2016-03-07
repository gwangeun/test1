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
    class Sensor
    {
        private NetworkStream ns;
        private TcpClient client;
        private int dataLength;
        private IoTDTO dto;
        public bool clientAlive = true;
        public SendACK sendDataClass = new SendACK();
        private MainWindow mainWindow;
        private DateTime startTime;
        private DateTime endTime;

        public Sensor(TcpClient client, NetworkStream ns, MainWindow mainWindow)
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
                        Console.WriteLine("Sensor클래스 ns.Read 에러: " + e.Message);
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
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                        {
                            mainWindow.img_sensor_temper.Source = DrawImage("temper3");
                            mainWindow.img_sensor_hum.Source = DrawImage("hum");
                        }));
                        try
                        {
                            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                            {
                                //Console.WriteLine("dto.capabilitydatatype_data[0]: "+dto.capabilitydatatype_data[0]);
                                saveStatusDataForApp(dto);
                                if (dto.capabilitydatatype_data[0] == 0) mainWindow.img_sensor_movement.Source = DrawImage("moveOff3");
                                else mainWindow.img_sensor_movement.Source = DrawImage("moveOn3");
                                float temperature = dto.capabilitydatatype_data[1];
                                temperature = (float)temperature - (((float)10 / (float)100) * 30);
                                mainWindow.lbl_sensor_temperature.Content = temperature+"ºC"; //온도
                                mainWindow.lbl_sensor_humidity.Content = dto.capabilitydatatype_data[2].ToString()+"%"; //습도
                                mainWindow.lbl_sensor_light_level.Content = dto.capabilitydatatype_data[3]; //조도 level 라벨 표시
                                if (dto.capabilitydatatype_data[3] == 0) mainWindow.img_sensor_light_level.Source = DrawImage("lightbulb0"); //조도 level 그림 표시
                                else if (dto.capabilitydatatype_data[3] == 1) mainWindow.img_sensor_light_level.Source = DrawImage("lightbulb1");
                                else if (dto.capabilitydatatype_data[3] == 2) mainWindow.img_sensor_light_level.Source = DrawImage("lightbulb2");
                                else if (dto.capabilitydatatype_data[3] == 3) mainWindow.img_sensor_light_level.Source = DrawImage("lightbulb3");
                                else if (dto.capabilitydatatype_data[3] == 4) mainWindow.img_sensor_light_level.Source = DrawImage("lightbulb4");
                                else Console.WriteLine("알 수 없는 레벨: " + dto.capabilitydatatype_data[3]);
                            }));
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show("else if (dto.Device_Name.Equals(IoT_Sensor)) 에러 발생!\n" + e.Message);
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
            }catch(Exception e)
            {
                Console.WriteLine("Sensor클래스 ns.Close() or client.Close()에러 : " + e.Message);
                initClientStatusTable();
                return;
            }
        }
        private void saveStatusDataForApp(IoTDTO dto) //mainWindow clientStatusTable에 데이터를 저장하는 부분
        {
            JObject obj = new JObject();
            obj.Add("net", "on");
            obj.Add("movement", dto.capabilitydatatype_data[0].ToString());
            float temperature = dto.capabilitydatatype_data[1];
            temperature = (float)temperature - (((float)10 / (float)100) * 30);
            obj.Add("temperature", temperature.ToString());
            obj.Add("humidity", dto.capabilitydatatype_data[2].ToString());
            obj.Add("illumination", dto.capabilitydatatype_data[3].ToString());

            if (mainWindow.clientStatusTable["IoT_Sensor"] != null) mainWindow.clientStatusTable["IoT_Sensor"] = obj;
            else mainWindow.clientStatusTable.Add("IoT_Sensor", obj);
        }
        private void printOriginData(byte[] data)
        {
            String data_str = "Sensor_{ ";
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

        private void initClientStatusTable()
        {
            //Dashboard초기화
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate{
                mainWindow.img_sensor_movement.Source = DrawImage("waitMovement");
                mainWindow.img_sensor_temper.Source = DrawImage("waitTemper");
                mainWindow.img_sensor_hum.Source = DrawImage("waitTemper2");
                mainWindow.img_sensor_light_level.Source = DrawImage("illumWait");
                mainWindow.lbl_sensor_light_level.Content = "";
                mainWindow.lbl_sensor_temperature.Content = "...";
                mainWindow.lbl_sensor_humidity.Content = "...";
            }));
            

            //APP 전송 객체 초기화
            JObject plainObj = new JObject();
            plainObj.Add("net", "off");

            if (mainWindow.clientStatusTable != null)
            {
                lock (mainWindow.clientStatusTable)
                {
                    if (mainWindow.clientStatusTable["IoT_Sensor"] != null) mainWindow.clientStatusTable["IoT_Sensor"] = plainObj;
                    else mainWindow.clientStatusTable.Add("IoT_Sensor", plainObj);
                }
            }
            //this.clientAlive = false;
        }

        private void makeLogFile(string line)
        {
            string directory = @"C:\Temp\IoT Integrate Log\Sensor\";
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.Exists == false) di.Create();

            try
            {
                using (StreamWriter file = new StreamWriter(di + "[" + DateTime.Now.ToString("yyyy-MM-dd") + "] Sensor Log_Received.txt", true))
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
