using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Capture.TeraModule.ViewModels;
using TeraCompass.Processing;
using TeraCompass.Tera.Core.Game;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Services;
using Message = TeraCompass.Tera.Core.Message;

namespace Capture.TeraModule.Processing
{
    public sealed class PacketProcessor
    {
        static PacketProcessor()
        {
            Instance = new PacketProcessor();
        }
        public delegate void ConnectedHandler(string serverName);

        public CompassViewModel CompassViewModel { get; internal set; }
        private bool _keepAlive = true;
        
        internal MessageFactory MessageFactory = new MessageFactory();
        internal bool NeedInit = true;


        internal PacketProcessingFactory PacketProcessing = new PacketProcessingFactory();
        public Server Server;
        internal UserLogoTracker UserLogoTracker = new UserLogoTracker();
        readonly Type UnknownType = typeof(UnknownMessage);
        private PacketProcessor()
        {
            TeraSniffer.Instance.NewConnection += HandleNewConnection;
            TeraSniffer.Instance.EndConnection += HandleEndConnection;

            var packetAnalysis = new Task(PacketAnalysisLoop);
            packetAnalysis.Start();
            TeraSniffer.Instance.EnableMessageStorage = true;
        }




        public static PacketProcessor Instance { get; set; }

        public EntityTracker EntityTracker { get; internal set; }



        public void Exit()
        {

            TeraSniffer.Instance.Enabled = false;
            _keepAlive = false;
            Thread.Sleep(500);
            Environment.Exit(1);
        }

        internal void RaiseConnected(string message)
        {
            Connected?.Invoke(message);
        }



        public event ConnectedHandler Connected;


        private void HandleEndConnection()
        {
            NeedInit = true;
            MessageFactory = new MessageFactory();
            Trace.WriteLine("ConnectionEnded");
        }

        private void HandleNewConnection(Server server)
        {
            Server = server;
            NeedInit = true;
            MessageFactory = new MessageFactory();

            Connected?.Invoke(server.Name);
        }
        private void PacketAnalysisLoop()
        {
            try
            {


                while (_keepAlive)
                {
                    var successDequeue = TeraSniffer.Instance.Packets.TryDequeue(out Message obj);
                    if (!successDequeue)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    var message = MessageFactory.Create(obj);
                    if (message.GetType() == UnknownType)
                    {
                        continue;
                    }

                    PacketProcessing.Process(message);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"message: {ex.Message}\n inner: {ex.InnerException}  \nstack: {ex.StackTrace}");
            }
        }
    }
}