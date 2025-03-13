using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceChatRoom
{
    internal class ChatRoom
    {
        private Socket serverSocket;
        private int[] ports = { 31416, 31417, 31418, 31419, 31420 };
        private int port;
        private bool isRunning = false;
        private Thread serverThread;
        (string UserName, StreamWriter Writer) customers = ("", null);
        List<(string UserName, StreamWriter Writer)> customersServer = new List<(string, StreamWriter)>();
        private Service1 service;
        private const string ConfigPath = @"C:\ProgramData\chatroom_config.txt";
        private const int DefaultPort = 31416;
        public ChatRoom(Service1 service)
        {
            this.service = service;
        }
        public void StartServer()
        {
            serverThread = new Thread(RunServer);
            serverThread.IsBackground = true;
            isRunning = true;
            serverThread.Start();
        }
        public void StopServer()
        {
            isRunning = false;
            serverSocket?.Close();
            Console.WriteLine("[SERVER] Stopped.");
        }
        public void RunServer()
        {
            //port = GetPortAvaliable();
            port = ReadPortFromConfig();
            if (!CheckPort(port))
            {
                Console.WriteLine("No available ports found. Exiting.");
                service.WriteEventLog($"No available ports found. Exiting");
                return;

            }

            service.WriteEventLog($"Using port {port}");
            IPEndPoint ie = new IPEndPoint(IPAddress.Any, port);
            //1ºcrear socket que esuche el puerto
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                //2ºEnlanzar el socket a un puerto
                serverSocket.Bind(ie);
                //3ºEscucha
                serverSocket.Listen(5);//poner espera como max 5
                Console.WriteLine("Server waiting");

                while (isRunning)
                {
                    //esperar y aceptar la conexion del cliente
                    Socket clientSocket = serverSocket.Accept();
                    Console.WriteLine($"Client connected from:  {clientSocket.RemoteEndPoint} ");

                    //crear hilo para manejar cliente
                    Thread clientThread = new Thread(() => HandlerClient(clientSocket));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
            }
            catch (Exception e) when (e is SocketException | e is IOException)
            {
                Console.WriteLine(e);
            }
        }
        public int GetPortAvaliable()
        {
            foreach (int port in ports)
            {
                if (CheckPort(port))
                {
                    return port; //ek que estea disponible primero 
                }
            }

            return -1; // Si no hay puertos disponibles
        }
        private int ReadPortFromConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string portText = File.ReadAllText(ConfigPath);
                    if (int.TryParse(portText, out int port))
                    {
                        return port;
                    }
                }

            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is ArgumentNullException)
            {
                service.WriteEventLog($"Error reading config: {ex.Message}", EventLogEntryType.Error);
            }

            return DefaultPort;
        }
        private bool CheckPort(int port)
        {
            try
            {
                using (Socket testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    testSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                }
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        public void HandlerClient(object socket)
        {
            bool isExit = false;
            Socket clientSocket = (Socket)socket;
            IPEndPoint ieClient = (IPEndPoint)clientSocket.RemoteEndPoint;
            string clientIP = ieClient.Address.ToString();
            string username = null;

            Console.WriteLine($"Handling client at {ieClient.Address}:{ieClient.Port}");

            try
            {
                //si la conexion se ha establecido se crean los streams
                using (NetworkStream ns = new NetworkStream(clientSocket))
                using (StreamReader sr = new StreamReader(ns))
                using (StreamWriter sw = new StreamWriter(ns))

                {
                    //doy mensaje de bienvenida
                    sw.WriteLine("Welcome to ChatRoomNicki's. Enter your username:");
                    sw.Flush();

                    //mandar el mensaje del usuario 
                    username = sr.ReadLine();
                    //sj esta vacio o no cumle entonces
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        throw new ArgumentException();
                    }


                    //si cierra abruptamente y ha escrito algo
                    //sigue y lo añade

                    //creo el usuario  
                    username = $"{username}@{clientIP}";

                    // Antes de agregarlo a la lista, verificamos si el socket sigue conectado


                    lock (this)
                    {
                        customersServer.Add((username, sw));
                    }

                    BroadcastMessage($"\n{username} has joined:D", username);
                    Console.WriteLine($"{username} connected");
                    sw.Flush();
                    //una vez conectado el usuario escribe su mensaje

                    string clientMessage = "";
                    while (!isExit && clientMessage != null)
                    {
                        clientMessage = sr.ReadLine();

                        //el  mesjae del readline va dentro ya que es constante 
                        if (!isExit && clientMessage != null)
                        {
                            if (clientMessage == "#exit")
                            {
                                //sw.WriteLine("You have disconnected from the chat");
                                //sw.Flush();
                                BroadcastMessage($"{username} has left the chat. ", username);
                                lock (this)
                                {
                                    customersServer.Remove((username, sw));
                                }
                                isExit = true;
                                clientSocket.Close();
                            }
                            else if (clientMessage == "#list")
                            {
                                sw.WriteLine("Connected users:");
                                sw.Flush();
                                lock (this)
                                {
                                    foreach (var client in customersServer)
                                    {
                                        sw.WriteLine($"- {client.UserName})");
                                        sw.Flush();
                                    }
                                }
                            }
                            else
                            {
                                BroadcastMessage($"{username}>{clientMessage}", username);
                            }
                        }
                        else
                        {
                            BroadcastMessage($"{username} disconnected unexpectedly", username);
                            throw new ArgumentException();
                        }
                    }

                }
            }

            //cuando se cierra abruptamente no  lo elimino de la coleccion
            //por lo de el usaurio por si lanza exceipcion
            catch (Exception e) when (e is SocketException | e is IOException | e is ArgumentException)
            {
                Console.WriteLine(e.ToString());
                //username = null;
            }
            lock (this)
            {
                if (!string.IsNullOrWhiteSpace(username))
                {
                    customersServer.RemoveAll(user => user.UserName == username);
                }
            }
        }
        //sender sera quien envia el mensjae 
        public void BroadcastMessage(string message, string sender)
        {
            lock (this)
            {
                foreach (var client in customersServer)
                {
                    if (client.UserName != sender)
                    {
                        try
                        {
                            client.Writer.WriteLine(message);
                            client.Writer.Flush();
                        }
                        catch (IOException e)
                        {
                            Console.WriteLine(e);
                            customersServer.Remove(client);
                        }
                    }
                }
            }
        }
    }
}
