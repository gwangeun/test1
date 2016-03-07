using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SoftwareJinHeung
{
    class SendACK
    {
        private NetworkStream ns;
        internal void SendREGDEV_ACK(IoTDTO dto, NetworkStream ns)
        {
            this.ns = ns;
            byte[] regdev_ACK = new byte[17];
            regdev_ACK[0] = dto.Stx;
            regdev_ACK[1] = dto.Ver;
            regdev_ACK[2] = dto.Seq[0];
            regdev_ACK[3] = dto.Seq[1];
            regdev_ACK[4] = (byte)0X81;
            regdev_ACK[5] = dto.Len[0];
            regdev_ACK[6] = dto.Len[1];

            regdev_ACK[7] = (byte)0;
            regdev_ACK[8] = CurrentTime()[0];
            regdev_ACK[9] = CurrentTime()[1];
            regdev_ACK[10] = CurrentTime()[2];
            regdev_ACK[11] = CurrentTime()[3];
            regdev_ACK[12] = CurrentTime()[4];
            regdev_ACK[13] = CurrentTime()[5];
            regdev_ACK[14] = CurrentTime()[6];

            regdev_ACK[15] = dto.Cs;
            regdev_ACK[16] = dto.Etx;
            
            WriteData(regdev_ACK);
        }

        internal void SendSTS_ACK(IoTDTO dto, NetworkStream ns)
        {
            this.ns = ns;
            byte[] sendSTS_ACK = new byte[17];
            sendSTS_ACK[0] = dto.Stx;
            sendSTS_ACK[1] = dto.Ver;
            sendSTS_ACK[2] = dto.Seq[0];
            sendSTS_ACK[3] = dto.Seq[1];
            sendSTS_ACK[4] = (byte)0X82;
            sendSTS_ACK[5] = dto.Len[0];
            sendSTS_ACK[6] = dto.Len[1];

            sendSTS_ACK[7] = (byte)0x00;
            sendSTS_ACK[8] = (byte)0x00;
            sendSTS_ACK[9] = (byte)0x00;
            sendSTS_ACK[10] = (byte)0x00;
            sendSTS_ACK[11] = (byte)0x00;
            sendSTS_ACK[12] = (byte)0x00;
            sendSTS_ACK[13] = (byte)0x00;
            sendSTS_ACK[14] = (byte)0x00;

            sendSTS_ACK[15] = dto.Cs;
            sendSTS_ACK[16] = dto.Etx;

            WriteData(sendSTS_ACK);
        }
        
        public byte[] CurrentTime()
        {
            byte[] currTime = new byte[8];
            currTime[0] = (byte)DateTime.Now.Year;
            currTime[1] = (byte)DateTime.Now.Month;
            currTime[2] = (byte)DateTime.Now.Day;
            currTime[3] = (byte)DateTime.Now.Hour;
            currTime[4] = (byte)DateTime.Now.Minute;
            currTime[5] = (byte)DateTime.Now.Second;

            return currTime;
        }
        internal void WriteData(byte[] data)
        {
            try
            {
                if (ns.CanWrite) ns.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine("SendACK 클래스 WriteData() 에러: " + e.Message);
                if (ns != null) ns.Close();
                return;
            }
        }
    }
}