using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ejercicio3RedesChatRoom //TODO cuando el cliente se desconecta no se sale de verdad, cuando cierre abruptamente lanza excepcion
{// REvisar cierres abruptos al principio
    internal class Program
    {
        Socket serverSocket;
        int[] ports = { 31416, 31417, 31418, 31419, 31420 };
        int port;
        //TUPLA --> variable, puede contener varios valores
        //
        (string UserName, StreamWriter Writer) customers = ("", null);
        List<(string UserName, StreamWriter Writer)> customersServer = new List<(string, StreamWriter)>();
        static void Main(string[] args)
        {
            Program server = new Program();
            server.StartServer();
        }

        /**
         * El servidor permite conectarse a cuantos clientes deseen 
         * y lo que recibe de un cliente lo envía a todos los demás.
         * indicando la IP
         */
        public void StartServer()
        {
            port = GetPortAvaliable();
            if (port == -1)
            {
                Console.WriteLine("No available ports found. Exiting.");
                return;
            }

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

                while (true)
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