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
            InitializeComponent();
        }

        /// <summary>
        /// 此线程用来接收服务器发送的数据
        /// </summary>
        Thread threadRecive = null;

        Socket socketClient = null;

        private void btnConnect_Click(object sender, EventArgs e)
        {
            // 客户端创建通讯套接字并连接服务器、开始接收服务器传来的数据
            socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketClient.Connect(IPAddress.Parse(txtIP.Text.Trim()), int.Parse(txtPort.Text.Trim()));
            ShowMsg(string.Format("连接服务器（{0}:{1}）成功！", txtIP.Text.Trim(), txtPort.Text.Trim()));

            threadRecive = new Thread(new ThreadStart(() =>
            {
                while (true)
                {
                    // Receive 方法从套接字中接收数据，并存入接收缓冲区
                    byte[] bytes = new byte[1024 * 1024 * 2];
                    int length = socketClient.Receive(bytes);
                    string msg = Encoding.UTF8.GetString(bytes, 0, length);
                    ShowMsg("接收到数据：" + msg);
                }
            }));
            threadRecive.IsBackground = true;
            threadRecive.Start();
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
            string msg = txtSendMsg.Text.Trim();
            socketClient.Send(Encoding.UTF8.GetBytes(msg));
            ShowMsg("发送数据：" + msg);
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
