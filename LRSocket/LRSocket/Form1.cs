using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LRSocket
{
    public partial class Form1 : Form
    {
        LRConnection lr;

        public Form1()
        {
            InitializeComponent();
            lr = new LRConnection(4242, "Localhost");

        }

        private void bConnect_Click(object sender, EventArgs e)
        {
            lState.Text = "";
            try
            {
                lr.Connect();
                lConnction.Text = "Connceted";
                lConnction.BackColor = Color.LightGreen;

            }
            catch (Exception ex)
            {
                lState.Text = ex.ToString();
                lConnction.Text = "Conncetion Error";
                lConnction.BackColor = Color.LightPink;
                lState.Text = ex.ToString();

            }
        }

        private void bDisconnect_Click(object sender, EventArgs e)
        {
            lState.Text = "";
            try
            {
                //lr.WriteLine("stop");
                lr.Disconnect();
                lConnction.Text = "Disconnected";
                lConnction.BackColor = Color.LightGray;

            }
            catch (Exception ex)
            {
                lConnction.Text = "Disconnection Error";
                lConnction.BackColor = Color.LightPink;
                lState.Text = ex.ToString();

                lr.Disconnect();
            }
        }

        private void bRead_Click(object sender, EventArgs e)
        {
            // lState.Text = l
            lr.Receive();
        }

        private void bSend_Click(object sender, EventArgs e)
        {
            lr.WriteLine("+");
        }

      
    }
    public class LRConnection
    {
        private static TcpClient tcpSocket;
        private int Port { get; set; }
        string Host { get; set; }

        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);

        private static string response;

        public LRConnection(int port, string hostname)
        {
            Port = port;
            Host = hostname;
            
        }
        public void Connect()
        {
            tcpSocket = new TcpClient();
            tcpSocket.BeginConnect(Host, Port, new AsyncCallback(ConnectCallback), tcpSocket.Client);
            connectDone.WaitOne();       
        }
        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;  // Retrieve the socket from the state object.  
                client.EndConnect(ar);                  // Complete the connection.  

                Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());
                connectDone.Set();                      // Signal that the connection has been made.  
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        public void Disconnect()
        {
            if (IsConnected==true)
            {
                tcpSocket.Client.Close();
                tcpSocket.Close();
            }
           
        }
        public string Read()
        {
            if (IsConnected)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();

            while (tcpSocket.Available > 0)
            { 
                int input = tcpSocket.GetStream().ReadByte();
                sb.Append((char)input);
            }
            //System.Threading.Thread.Sleep(TimeOutMs);
           
            return sb.ToString();
        }
        private void Send(string data)
        {
            //if (IsConnected)
            //{
            //    byte[] buf = System.Text.ASCIIEncoding.ASCII.GetBytes(data);
            //    tcpSocket.GetStream().Write(buf, 0, buf.Length);
            //}
            //else
            //{
            //    throw new Exception("Verbindung ist getrennt!");
            //}

            byte[] byteData = Encoding.ASCII.GetBytes(data);
            tcpSocket.Client.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(SendCallback), tcpSocket.Client);
        }
        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;  // Retrieve the socket from the state object.  
                int bytesSent = client.EndSend(ar);     // Complete sending the data to the remote device.  
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);


                sendDone.Set();                         // Signal that all bytes have been sent.  
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public  void Receive()
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = tcpSocket.Client;

                // Begin receiving the data from the remote device.
                tcpSocket.Client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;
               
                int bytesRead = client.EndReceive(ar);  // Read data from the remote device.
                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));          // There might be more data, so store the data received so far.              
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);  //  Get the rest of the data.
                    Console.WriteLine(state.sb.ToString());
                }
                else
                {
                    if (state.sb.Length > 1)  // All the data has arrived; put it in response.
                    {
                        response = state.sb.ToString();
                        Console.WriteLine(response);
                    }
                    receiveDone.Set();       // Signal that all bytes have been received.
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


        public void WriteLine(string cmd)
        {
            Send(cmd + "\n");
        }
    
    
        public bool IsConnected
        {
            get
            {
                try
                {
                    if (tcpSocket != null && tcpSocket.Client != null && tcpSocket.Client.Connected)
                    {
                        /* pear to the documentation on Poll:
                         * When passing SelectMode.SelectRead as a parameter to the Poll method it will return 
                         * -either- true if Socket.Listen(Int32) has been called and a connection is pending;
                         * -or- true if data is available for reading; 
                         * -or- true if the connection has been closed, reset, or terminated; 
                         * otherwise, returns false
                         */

                        // Detect if client disconnected
                        if (tcpSocket.Client.Poll(0, SelectMode.SelectRead))
                        {
                            byte[] buff = new byte[1];
                            if (tcpSocket.Client.Receive(buff, SocketFlags.Peek) == 0)
                            {
                                // Client disconnected
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

    }
    public class StateObject
    {
        // Client socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 256;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }


}
