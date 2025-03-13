using System;
using System.IO;
using System.ServiceProcess;
using System.Diagnostics;

namespace ServiceChatRoom
{
    public partial class Service1 : ServiceBase
    {
        private ChatRoom server;
        private System.Threading.Thread thread;

        public Service1()
        {
            InitializeComponent();
            this.CanPauseAndContinue = false; // No se puede pausar el servicio
        }

        protected override void OnStart(string[] args)
        {
            server = new ChatRoom(this);
            thread = new System.Threading.Thread(() => server.StartServer());
            thread.Start();
        }

        protected override void OnStop()
        {
            server?.StopServer();
            WriteEventLog("Service stopped.");
        }


        // el type el tipo de mensaje que esera
        public void WriteEventLog(string message, EventLogEntryType type = EventLogEntryType.Information)
        {
            string source = "ChatRoomService";
            //antes de escribir en el visor de evento, verificcar que si ya esta registrada, si no existe ala crea
            if (!EventLog.SourceExists(source))
            {
                EventLog.CreateEventSource(source, "Application");
            }

            EventLog.WriteEntry(source, message, type);
        }
    }
}
