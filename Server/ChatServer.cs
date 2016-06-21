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
            InitializeComponent();//关闭对文本框的 跨线程操作
        }

        /// <summary>
        /// 监听 Socket 运行的线程
        /// </summary>
        Thread threadWatch = null;

        /// <summary>
        /// 监听 Socket
        /// </summary>
        Socket socketServer = null;

        //用来存储返回的新的用于通信的套接字 
        Dictionary<string, Socket> socketDict = new Dictionary<string, Socket>();
        /// <summary>
        /// 通信线程的集合，用来接收客户端发送的信息
        /// </summary>
        Dictionary<string, Thread> dictThread = new Dictionary<string, Thread>();
        //用来接收数据的线程
        Thread threadRec = null;

        private void btnBeginListen_Click(object sender, EventArgs e)
        {
            // 创建 服务器 负责监听的套接字 参数(使用IP4寻址协议，使用流式连接，使用TCP传输协议)
             socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);           
             //获取IP地址
             IPAddress ip = IPAddress.Parse(this.txtIP.Text.Trim());            
             //创建 包含IP和Port的网络节点对象
             IPEndPoint endPoint = new IPEndPoint(ip, int.Parse(this.txtPort.Text.Trim()));            
             //将负责监听 的套接字 绑定到 唯一的IP和端口上
             socketServer.Bind(endPoint);            
             //设置监听队列 一次可以处理的最大数量
             socketServer.Listen(10);            
             //创建线程 负责监听
             threadWatch = new Thread(WatchConnection);
              //设置为后台线程
             threadWatch.IsBackground = true;
             //开启线程
             threadWatch.Start();            
             ShowMsg("=====================服 务 器 启 动 成 功======================");
        }

        //监听方法
        void WatchConnection()
        {
            //持续不断的监听
            while (true)
            {
                //开始监听 客户端 连接请求 【注意】Accept方法会阻断当前的线程--未接受到请求 程序卡在那里
                Socket sokConnection = socketServer.Accept();//返回一个 负责和该客户端通信的 套接字
                //将返回的新的套接字 存储到 字典序列中
                socketDict.Add(sokConnection.RemoteEndPoint.ToString(), sokConnection);
                //向在线列表中 添加一个 客户端的ip端口字符串 作为客户端的唯一标识
                
                ItemChanged(sokConnection.RemoteEndPoint.ToString(), true);
                //打印输出
                ShowMsg("客户端连接成功:" + sokConnection.RemoteEndPoint.ToString());
                //为该通信Socket 创建一个线程 用来监听接收数据


                //在可以调用 OLE 之前，必须将当前线程设置为单线程单元(STA)模式。请确保您的 Main 函数带有 
                //////STAThreadAttribute 标记。 只有将调试器附加到该进程才会引发此异常。
                threadRec = new Thread(new ParameterizedThreadStart(RecMsg));
                threadRec.SetApartmentState(ApartmentState.STA);
                threadRec.IsBackground = true;
                threadRec.Start(sokConnection);

                dictThread.Add(sokConnection.RemoteEndPoint.ToString(), threadRec);
            }
        }
        //接受数据的方法
        void RecMsg(object socket)
        {
            Socket m_socket = (Socket)socket;
            //持续监听接收数据
            while (true)
            {
                try
                {
                    //实例化一个字符数组
                    byte[] data = new byte[1024 * 1024];
                    //接受消息数据
                    //远程主机强迫关闭了一个现有的连接。
                    int receiveBytesLength = m_socket.Receive(data);//客户端关闭了出错
                    if (data[0] == 0)
                    {
                        SaveFileDialog sfd = new SaveFileDialog();
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            using (FileStream fs = new FileStream(sfd.FileName, FileMode.Create))
                            {
                                fs.Write(data, 1, receiveBytesLength - 1);
                                fs.Flush();
                                ShowMsg("文件保存成功，路径为：" + sfd.FileName);
                            }
                        }
                    }
                    else
                    {
                        //转换成字符串
                        string recMsg = Encoding.UTF8.GetString(data, 0, receiveBytesLength);
                        //打印接收到的数据
                        ShowMsg(((Socket)socket).RemoteEndPoint.ToString() + ":" + recMsg);
                    }
                }
                catch(System.Exception err)
                {
                    ShowMsg(err.Message);
                    ItemChanged(m_socket.RemoteEndPoint.ToString(), false);
                    socketDict.Remove(m_socket.RemoteEndPoint.ToString());
                    dictThread.Remove(m_socket.RemoteEndPoint.ToString());                    
                    m_socket.Close();
                    break;
                }
            }
        }

        delegate void ItemChangedCallback(string itemText,bool IsAdd);
        private void ItemChanged(string itemText, bool IsAdd)
        {
            if (this.InvokeRequired) // 也可以启动时修改控件的 CheckForIllegalCrossThreadCalls 属性
            {
                this.Invoke(new ItemChangedCallback(ItemChanged), new object[] { itemText, IsAdd });
            }
            else
            {
                if (IsAdd)
                {
                    this.lbSocketOnline.Items.Add(itemText);
                }
                else
                {
                    this.lbSocketOnline.Items.Remove(itemText);
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

        private void btnSendMsg_Click(object sender, EventArgs e)
        {
            if (lbSocketOnline.Text.Length == 0)
                MessageBox.Show("至少选择一个客户端才能发送消息！");
            else
            {
                // Send() 只接受字节数组
                //获取发送信息
                string Message = txtSendMsg.Text.Trim();
                //将字符串转换成字节数组
                byte[] data = System.Text.Encoding.UTF8.GetBytes(Message);
                //找到对应的客户端 并发送数据
                socketDict[lbSocketOnline.Text].Send(data, SocketFlags.None);
                //打印输出
                ShowMsg("发送数据：" + Message);
                //清空输入消息的内容
                txtSendMsg.Text = "";
            }
        }

        private void btnSendToAll_Click(object sender, EventArgs e)
        {
            string msg = txtSendMsg.Text.Trim();
            foreach (var socket in socketDict.Values)
            {
                socket.Send(Encoding.UTF8.GetBytes(msg));
            }
            ShowMsg("群发数据：" + msg);
        }
    }
}

