using System;
using System.Collections.Generic;
using System.Linq;
using Capture.TeraModule.Processing.Packets;
using TeraCompass.Processing.Packets;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Messages.Server;
using TeraCompass.Tera.Core.Game.Services;
using C_CHECK_VERSION = TeraCompass.Tera.Core.Game.Messages.Client.C_CHECK_VERSION;
using C_LOGIN_ARBITER = TeraCompass.Tera.Core.Game.Messages.Client.C_LOGIN_ARBITER;
using C_PLAYER_LOCATION = TeraCompass.Tera.Core.Game.Messages.Client.C_PLAYER_LOCATION;
namespace Capture.TeraModule.Processing
{
    // Creates a ParsedMessage from a Message
    // Contains a mapping from OpCodeNames to message types and knows how to instantiate those
    // Since it works with OpCodeNames not numeric OpCodes, it needs an OpCodeNamer
    public class PacketProcessingFactory
    {
        private static readonly Dictionary<Type, Delegate> MainProcessor = new Dictionary<Type, Delegate>()
        {
            {typeof(C_LOGIN_ARBITER), Helpers.Contructor<Func<C_LOGIN_ARBITER, Capture.TeraModule.Processing.Packets.C_LOGIN_ARBITER>>()},
            {typeof(S_GET_USER_LIST), new Action<S_GET_USER_LIST>(x => PacketProcessor.Instance.UserLogoTracker.SetUserList(x))},
            {typeof(S_GET_USER_GUILD_LOGO), new Action<S_GET_USER_GUILD_LOGO>(x => PacketProcessor.Instance.UserLogoTracker.AddLogo(x))},
            {typeof(LoginServerMessage), Helpers.Contructor<Func<LoginServerMessage, S_LOGIN>>()},
            { typeof(C_PLAYER_LOCATION), new Action<C_PLAYER_LOCATION>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(C_PLAYER_FLYING_LOCATION), new Action<C_PLAYER_FLYING_LOCATION>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(S_USER_LOCATION),  new Action<S_USER_LOCATION>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(S_USER_FLYING_LOCATION), new Action<S_USER_FLYING_LOCATION>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(SpawnUserServerMessage),  new Action<SpawnUserServerMessage>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(SDespawnUser),  new Action<SDespawnUser>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(SpawnMeServerMessage), new Action<SpawnMeServerMessage>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(S_CHANGE_RELATION), new Action<S_CHANGE_RELATION>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(SCreatureLife), new Action<SCreatureLife>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(S_DEAD_LOCATION), new Action<S_DEAD_LOCATION>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
            { typeof(SUserStatus), new Action<SUserStatus>(x => PacketProcessor.Instance.EntityTracker.Update(x))},
        };

        public bool Process(ParsedMessage message)
        {
            MainProcessor.TryGetValue(message.GetType(), out Delegate type);
            if (type == null) { return false; }
            type.DynamicInvoke(message);
            return true;
        }
    }
}