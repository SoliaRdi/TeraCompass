using System.Diagnostics;
using Capture.TeraModule.Settings;
using Capture.TeraModule.ViewModels;
using TeraCompass.Processing;
using TeraCompass.Tera.Core.Game.Messages.Server;
using TeraCompass.Tera.Core.Game.Services;

namespace Capture.TeraModule.Processing.Packets
{
    internal class S_LOGIN
    {
        internal S_LOGIN(LoginServerMessage message)
        {
            if (PacketProcessor.Instance.NeedInit)
            {
                PacketProcessor.Instance.RaiseConnected(BasicTeraData.Instance.Servers.GetServerName(message.ServerId, PacketProcessor.Instance.Server));
                PacketProcessor.Instance.Server = BasicTeraData.Instance.Servers.GetServer(message.ServerId, PacketProcessor.Instance.Server);
                PacketProcessor.Instance.MessageFactory.Region = PacketProcessor.Instance.Server.Region;
                var trackerreset = true;
                if (PacketProcessor.Instance.EntityTracker != null)
                {
                    try
                    {
                        var oldregion = BasicTeraData.Instance.Servers.GetServer(PacketProcessor.Instance.EntityTracker.CompassUser.ServerId).Region;
                        trackerreset = PacketProcessor.Instance.Server.Region != oldregion;
                    }
                    catch
                    {
                        Trace.WriteLine(
                            "New server:" + PacketProcessor.Instance.Server + ";Old server Id:" + PacketProcessor.Instance.EntityTracker.CompassUser?.ServerId);
                        throw;
                    }
                }
                if (trackerreset)
                {
                    PacketProcessor.Instance.EntityTracker = new EntityTracker();
                    PacketProcessor.Instance.CompassViewModel = new CompassViewModel();
                }
                PacketProcessor.Instance.NeedInit = false;
            }

            PacketProcessor.Instance.EntityTracker.Update(message);
            Services.GameState = GameState.InGame;
        }
    }
}