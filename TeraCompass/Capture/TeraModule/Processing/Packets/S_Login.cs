using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using TeraCompass.Tera.Core.Game.Messages.Server;
using TeraCompass.Tera.Core.Game.Services;
using TeraCompass.ViewModels;

namespace TeraCompass.Processing.Packets
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
                        Debug.WriteLine(
                            "New server:" + PacketProcessor.Instance.Server + ";Old server Id:" + PacketProcessor.Instance.EntityTracker.CompassUser?.ServerId,
                            false, true);
                        throw;
                    }
                }
                if (trackerreset)
                {
                    PacketProcessor.Instance.EntityTracker = new EntityTracker(BasicTeraData.Instance.MonsterDatabase);
                    PacketProcessor.Instance.PlayerTracker = new PlayerTracker(PacketProcessor.Instance.EntityTracker, BasicTeraData.Instance.Servers);
                }
                PacketProcessor.Instance.NeedInit = false;
            }

            PacketProcessor.Instance.EntityTracker.Update(message);

            PacketProcessor.Instance.PacketProcessing.Update();
            PacketProcessor.Instance.CompassViewModel=new CompassViewModel();
        }
    }
}