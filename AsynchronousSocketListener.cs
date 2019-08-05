
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace bd.tools.net
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    // State object for reading client data asynchronously  
    public class StateObject
    {
        // Client  socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }

    public class SocketListenerEventArgs : EventArgs
    {
        public const string MESSAGE_RECEIVED = "message_received";
        public const string SERVER_STERTED = "server_started";
  
        public string type { get; set; }
        public string data { get; set; }
        public DateTime TimeReached { get; set; }
    }

    public class AsynchronousSocketListener
    {

 
        public delegate void SocketListenerEventHandler(Object sender, SocketListenerEventArgs e);
        public event SocketListenerEventHandler SocketEvent;


        // Thread signal.  
        private ManualResetEvent allDone = new ManualResetEvent(false);

        private Thread socetThread;

        private string message = null;

        private const int defaultPort = 11000; 

        public AsynchronousSocketListener()
        {
            
        }

        public void Start()
        {
          // StartListening();

            if (socetThread != null)
            {
               // throw new Exception("Server's already running!");
            }

            socetThread = new Thread(StartListening);
            socetThread.Name = "SocetThread";
            socetThread.IsBackground = true;
            socetThread.Start();
        }


        public void NotifyUploadDone()
        {
     
        }

        private void StartListening()
        {
            // Data buffer for incoming data.  
            byte[] bytes = new Byte[1024];
         
            var host = Dns.GetHostEntry(Dns.GetHostName());

            IPAddress ipAddress = null;
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ip;
                    Console.WriteLine("ipadress "+ ipAddress);
                }
            }

            if (ipAddress == null)
            {
                Console.WriteLine("Local IP adress not found");
                return;
            }

            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            // Create a TCP/IP socket.  
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);
                Console.WriteLine("Socket oppended at:{0}:{1}", localEndPoint.Address.ToString(), AsynchronousSocketListener.defaultPort); 
                NotifyServerStart(localEndPoint.Address.ToString() + ":" + AsynchronousSocketListener.defaultPort);
                while (true)
                {
                    allDone.Reset();
     
                    // Start an asynchronous socket to listen for connections.  
                    Console.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    
          
                    // Wait until a connection is made before continuing.  
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

         ///   Console.WriteLine("\nPress ENTER to continue...");
          //  Console.Read();

        }

        private void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            allDone.Set();

               // Get the socket that handles the client request.  
               Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);

        }

        private void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.   
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.  
                state.sb.Append(Encoding.ASCII.GetString(
                    state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read   
                // more data.  
                content = state.sb.ToString();
          
                if (content.IndexOf("<EOF>") > -1)
                {
                    // All the data has been read from the   
                    // client. Display it on the console.  
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);

                    // Echo the data back to the client.  
             
                    ///  
              
                    NotifyMessageReceived(content);
     
                    Send(handler, content);
                }
                else
                {
                    // Not all data received. Get more.  
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
            }

        }
   
        private void NotifyMessageReceived(string data)
        {

            SocketListenerEventArgs args = new SocketListenerEventArgs();
            args.type = SocketListenerEventArgs.MESSAGE_RECEIVED;
            args.data = data;
            args.TimeReached = new DateTime();
       
            if (SocketEvent != null)
            {
                SocketEvent(this, args);
            }
        }

        private void NotifyServerStart(string data)
        {
            SocketListenerEventArgs args = new SocketListenerEventArgs();
            args.type = SocketListenerEventArgs.SERVER_STERTED;
            args.data = data;
            args.TimeReached = new DateTime();

            if (SocketEvent != null)
            {
                SocketEvent(this, args);
            }
        }

        private void Send(Socket handler, String data)
        {
            allDone.Set();
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
    
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

               handler.Shutdown(SocketShutdown.Send);
               handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}