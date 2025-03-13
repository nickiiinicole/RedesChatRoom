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
        static void Main(string[] args)
        {
            ChatRoom chatRoom = new ChatRoom();
            chatRoom.StartServer();
        }
    }
}