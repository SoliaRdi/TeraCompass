using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows;
using TeraCompass.Tera.Core.Game;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Services;
using TeraCompass.ViewModels;
using Message = TeraCompass.Tera.Core.Message;

namespace TeraCompass.Processing
{
    public class PacketProcessor
    {
        public delegate void ConnectedHandler(string serverName);

        public delegate void GuildIconEvent(Bitmap icon);


        private static PacketProcessor _instance;

        public CompassViewModel CompassViewModel { get; internal set; }
        private bool _keepAlive = true;
        
        internal MessageFactory MessageFactory = new MessageFactory();
        internal bool NeedInit = true;

        
        public bool NeedToReset;
        public bool NeedToResetCurrent;

        internal PacketProcessingFactory PacketProcessing = new PacketProcessingFactory();
        public Server Server;
        internal UserLogoTracker UserLogoTracker = new UserLogoTracker();
        Type UnknownType = typeof(UnknownMessage);
        private PacketProcessor()
        {
            TeraSniffer.Instance.NewConnection += HandleNewConnection;
            TeraSniffer.Instance.EndConnection += HandleEndConnection;

            var packetAnalysis = new Thread(PacketAnalysisLoop);
            packetAnalysis.Start();
            TeraSniffer.Instance.EnableMessageStorage = true;
        }
        public PlayerTracker PlayerTracker { get; internal set; }

        public bool TimedEncounter { get; set; }

        public static PacketProcessor Instance => _instance ?? (_instance = new PacketProcessor());

        public EntityTracker EntityTracker { get; internal set; }
        public bool SendFullDetails { get; set; }


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


        protected virtual void HandleEndConnection()
        {
            NeedInit = true;
            MessageFactory = new MessageFactory();
            Debug.WriteLine("ConnectionEnded");
        }

        protected virtual void HandleNewConnection(Server server)
        {
            Server = server;
            NeedInit = true;
            MessageFactory = new MessageFactory();

            Connected?.Invoke(server.Name);
        }
        private void PacketAnalysisLoop()
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
                if (message.GetType() == UnknownType) { continue; }

                PacketProcessing.Process(message);
            }
        }
    }
}