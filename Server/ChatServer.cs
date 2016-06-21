using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    public partial class ChatServer : Form
    {//http://www.cnblogs.com/holyknight-zld/archive/2012/07/18/WebFormSocket.html
        public ChatServer()
        {
            InitializeComponent();
            ListBox.CheckForIllegalCrossThreadCalls = false;
        }

        /// <summary>
        /// 监听 Socket 运行的线程
        /// </summary>
        Thread threadWatch = null;

        /// <summary>
        /// 监听 Socket
        /// </summary>
        Socket socketWatch = null;

        /// <summary>
        /// 服务器端通信套接字集合
        /// 必须在每次客户端连接成功之后，保存新建的通讯套接字，这样才能和后续的所有客户端通信
        /// </summary>
        Dictionary<string, Socket> dictCommunication = new Dictionary<string, Socket>();

        /// <summary>
        /// 通信线程的集合，用来接收客户端发送的信息
        /// </summary>
        Dictionary<string, Thread> dictThread = new Dictionary<string, Thread>();

        private void btnBeginListen_Click(object sender, EventArgs e)
        {
            // 创建服务器端监听 Socket (IP4寻址协议，流式连接，TCP协议传输数据)
            socketWatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 监听套接字绑定指定端口
            IPAddress address = IPAddress.Parse(txtIP.Text.Trim());
            IPEndPoint endPoint = new IPEndPoint(address, int.Parse(txtPort.Text.Trim()));
            socketWatch.Bind(endPoint);

            // 将监听套接字置于侦听状态，并设置连接队列的最大长度
            socketWatch.Listen(20);

            // 启动监听线程开始监听客户端请求
            threadWatch = new Thread(Watch);
            threadWatch.IsBackground = true;
            threadWatch.Start();
            ShowMsg("服务器启动完成！");
        }

        Socket socketCommunication = null;
        private void Watch()
        {
            while (true)
            {
                // Accept() 会创建新的通信 Socket，且会阻断当前线程，因此应置于非主线程上使用
                // Accept() 与线程上接受的委托类型不符，因此需另建一方法做桥接
                socketCommunication = socketWatch.Accept();

                // 将新建的通信套接字存入集合中，以便服务器随时可以向指定客户端发送消息
                // 如不置于集合中，每次 new 出的通信线程都是一个新的套接字，那么原套接字将失去引用
                dictCommunication.Add(socketCommunication.RemoteEndPoint.ToString(), socketCommunication);
                lbSocketOnline.Items.Add(socketCommunication.RemoteEndPoint.ToString());

                // Receive 也是一个阻塞方法，不能直接运行在 Watch 中，否则监听线程会阻塞
                // 另外，将每一个通信线程存入集合，方便今后的管理（如关闭、或挂起）
                Thread thread = new Thread(() =>
                {
                    while (true)
                    {
                        byte[] bytes = new byte[1024 * 1024 * 2];
                        int length = 0;
                        try
                        {
                            length = socketCommunication.Receive(bytes);
                        }
                        catch (SocketException ex)
                        {
                            ShowMsg("出现异常：" + ex.Message);
                            string key = socketCommunication.RemoteEndPoint.ToString();
                            lbSocketOnline.Items.Remove(key);
                            dictCommunication.Remove(key);
                            dictThread.Remove(key);
                            break;
                        }
                        if (bytes[0] == 0) // File
                        {
                            SaveFileDialog sfd = new SaveFileDialog();
                            if (sfd.ShowDialog() == DialogResult.OK)
                            {
                                using (FileStream fs = new FileStream(sfd.FileName, FileMode.Create))
                                {
                                    fs.Write(bytes, 1, length - 1);
                                    fs.Flush();
                                    ShowMsg("文件保存成功，路径为：" + sfd.FileName);
                                }
                            }
                        }
                        else // Msg
                        {
                            string msg = Encoding.UTF8.GetString(bytes, 0, length);
                            ShowMsg("接收到来自" + socketCommunication.RemoteEndPoint.ToString() + "的数据：" + msg);
                        }
                    }
                });
                thread.IsBackground = true;
                thread.Start();
                dictThread.Add(socketCommunication.RemoteEndPoint.ToString(), thread);

                ShowMsg("客户端连接成功！通信地址为：" + socketCommunication.RemoteEndPoint.ToString());
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

        private void btnSendMsg_Click(object sender, EventArgs e)
        {
            if (lbSocketOnline.Text.Length == 0)
                MessageBox.Show("至少选择一个客户端才能发送消息！");
            else
            {
                // Send() 只接受字节数组
                string msg = txtSendMsg.Text.Trim();
                dictCommunication[lbSocketOnline.Text].Send(Encoding.UTF8.GetBytes(msg));
                ShowMsg("发送数据：" + msg);
            }
        }

        private void btnSendToAll_Click(object sender, EventArgs e)
        {
            string msg = txtSendMsg.Text.Trim();
            foreach (var socket in dictCommunication.Values)
            {
                socket.Send(Encoding.UTF8.GetBytes(msg));
            }
            ShowMsg("群发数据：" + msg);
        }
    }
}

