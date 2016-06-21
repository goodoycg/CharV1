using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace Client
{
    public partial class ChatClient : Form
    {//http://www.cnblogs.com/holyknight-zld/archive/2012/07/18/WebFormSocket.html
        public ChatClient()
        {
            InitializeComponent();//关闭对TextBox的跨线程检测
        }

        /// <summary>
        /// 此线程用来接收服务器发送的数据
        /// </summary>
        Thread threadReceive = null;
        //客户端套接字
        Socket socketClient = null;        
        //连接服务器
        private void btnConnect_Click(object sender, EventArgs e)
        {            
            //新建一个Socket 负责 监听服务器的通信
            socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            timerClient.Tick -= TimerClient_Tick;
            timerClient.Tick += TimerClient_Tick;
            timerClient.Interval = 1000;
            timerClient.Enabled = true;
        }

        private void TimerClient_Tick(object sender, EventArgs e)
        {//连接远程主机

            try
            {
                //获取IP
                IPAddress ip = IPAddress.Parse(txtIP.Text.Trim());
                //新建一个网络节点
                IPEndPoint endPoint = new IPEndPoint(ip, int.Parse(txtPort.Text.Trim()));
                socketClient.Connect(endPoint);
                //打印输出
                ShowMsg("=====================服 务 器 连 接 成 功======================");
                //创建线程 监听服务器 发来的消息
                threadReceive = new Thread(RecMsg);
                //设置为后台线程
                threadReceive.IsBackground = true;
                //开启线程
                threadReceive.Start();
                this.timerClient.Enabled = false;
            }
            catch
            {

            }
        }

        //监听 服务器端 发来的消息
        void RecMsg()
        {
            while (true)
            {
                try
                {
                    //初始化一个 1M的 缓存区(字节数组)
                    byte[] data = new byte[1024 * 1024];
                    //将接受到的数据 存放到data数组中 返回接受到的数据的实际长度
                    int receiveBytesLength = socketClient.Receive(data);//服务端关闭了出错
                    //将字符串转换成字节数组
                    string strMsg = Encoding.UTF8.GetString(data, 0, receiveBytesLength);
                    //打印输出
                    ShowMsg("接受数据：" + strMsg);
                }
                catch
                {                    
                    socketClient.Close();
                    break;
                }
            }
        }

        delegate void ShowMsgCallback(string msg);
        private void ShowMsg(string msg)
        {
            if (this.InvokeRequired) // 也可以启动时修改控件的 CheckForIllegalCrossThreadCalls 属性
            {
                this.Invoke(new ShowMsgCallback(ShowMsg), new object[] { msg });
            }
            else
            {
                this.txtMsg.AppendText(msg + "\r\n");
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {            
            string Message = txtSendMsg.Text.Trim();
            //将字符串转换成字节数组
            byte[] data = System.Text.Encoding.UTF8.GetBytes(Message);
            //发送数据
            socketClient.Send(data, SocketFlags.None);
            ShowMsg("发送数据：" + Message);
            //清空输入消息的内容
            txtSendMsg.Text = "";
        }

        private void btnChooseFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtFilePath.Text = ofd.FileName;
            }
        }

        private void btnSendFile_Click(object sender, EventArgs e)
        {
            using (FileStream fs = new FileStream(txtFilePath.Text, FileMode.Open))
            {
                byte[] bytes = new byte[1024 * 1024 * 2];

                // 假设第一个字节为标志位：0 表示传送文件

                // 方式一：整体向后偏移 1 个字节；但这样有潜在缺点，
                // 有时在通信时会非常准确的按照约定的字节长度来传递，
                // 那么这种偏移方案显然是不可靠的
                // bytes[0] = 0; 
                // int length = fs.Read(bytes, 1, bytes.Length);

                // 方式二：创建多出 1 个字节的数组发送
                int length = fs.Read(bytes, 0, bytes.Length);
                byte[] newBytes = new byte[length + 1];
                newBytes[0] = 0;
                // BlockCopy() 会比你自己写for循环赋值更为简单合适
                Buffer.BlockCopy(bytes, 0, newBytes, 1, length);
                socketClient.Send(newBytes);
            }
        }
    }
}
