using System;
using System.Collections.Generic;
using System.Linq;
using Capture.TeraModule.Processing.Packets;
using Capture.TeraModule.Tera.Core.Game.Messages.Server;
using TeraCompass.Processing.Packets;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Messages.Client;
using TeraCompass.Tera.Core.Game.Messages.Server;

using C_LOGIN_ARBITER = TeraCompass.Tera.Core.Game.Messages.Client.C_LOGIN_ARBITER;

namespace TeraCompass.Tera.Core.Game.Services
{
    // Creates a ParsedMessage from a Message
    // Contains a mapping from OpCodeNames to message types and knows how to instantiate those
    // Since it works with OpCodeNames not numeric OpCodes, it needs an OpCodeNamer
    public class MessageFactory
    {
        private static readonly Delegate UnknownMessageDelegate = Helpers.Contructor<Func<TeraMessageReader, UnknownMessage>>();
        private static readonly Dictionary<ushort, Delegate> OpcodeNameToType = new Dictionary<ushort, Delegate> {{ 19900, Helpers.Contructor<Func<TeraMessageReader, C_CHECK_VERSION>>() } };
        private static readonly Dictionary<string, Delegate> CoreServices = new Dictionary<string, Delegate>
        {
            {"C_CHECK_VERSION", Helpers.Contructor<Func<TeraMessageReader,C_CHECK_VERSION>>()},
            {"C_LOGIN_ARBITER", Helpers.Contructor<Func<TeraMessageReader,C_LOGIN_ARBITER>>()},
            {"S_SPAWN_USER", Helpers.Contructor<Func<TeraMessageReader,SpawnUserServerMessage>>()},
            {"S_SPAWN_ME", Helpers.Contructor<Func<TeraMessageReader,SpawnMeServerMessage>>()},
            {"S_LOAD_TOPO", Helpers.Contructor<Func<TeraMessageReader,S_LOAD_TOPO>>()},
            {"S_LOGIN", Helpers.Contructor<Func<TeraMessageReader,LoginServerMessage>>()},
            {"S_DESPAWN_USER", Helpers.Contructor<Func<TeraMessageReader,SDespawnUser>>()},
            {"S_USER_LOCATION", Helpers.Contructor<Func<TeraMessageReader,S_USER_LOCATION>>()},
            {"C_PLAYER_LOCATION", Helpers.Contructor<Func<TeraMessageReader,C_PLAYER_LOCATION>>()},
            {"C_PLAYER_FLYING_LOCATION", Helpers.Contructor<Func<TeraMessageReader,C_PLAYER_FLYING_LOCATION>>()},
            {"S_CHANGE_RELATION", Helpers.Contructor<Func<TeraMessageReader,S_CHANGE_RELATION>>()},
            {"S_DEAD_LOCATION", Helpers.Contructor<Func<TeraMessageReader,S_DEAD_LOCATION>>()},
            {"S_CREATURE_LIFE", Helpers.Contructor<Func<TeraMessageReader,SCreatureLife>>()},
            { "S_USER_STATUS", Helpers.Contructor<Func<TeraMessageReader,SUserStatus>>()},
            { "S_RETURN_TO_LOBBY", Helpers.Contructor<Func<TeraMessageReader,S_RETURN_TO_LOBBY>>()},
            { "S_USER_LOCATION_IN_ACTION", Helpers.Contructor<Func<TeraMessageReader,S_USER_LOCATION_IN_ACTION>>()},
            { "S_USER_FLYING_LOCATION", Helpers.Contructor<Func<TeraMessageReader,S_USER_FLYING_LOCATION>>()},
            { "S_SPAWN_COLLECTION", Helpers.Contructor<Func<TeraMessageReader,S_SPAWN_COLLECTION>>()},
            { "S_DESPAWN_COLLECTION", Helpers.Contructor<Func<TeraMessageReader,S_DESPAWN_COLLECTION>>()},
            { "S_USER_DEATH", Helpers.Contructor<Func<TeraMessageReader,S_USER_DEATH>>()},
        };


        private readonly OpCodeNamer _opCodeNamer;
        private readonly OpCodeNamer _sysMsgNamer;
        public string Region;
        public uint Version;
        public int ReleaseVersion;

        public MessageFactory(OpCodeNamer opCodeNamer, string region, uint version, OpCodeNamer sysMsgNamer=null)
        {
            _opCodeNamer = opCodeNamer;
            _sysMsgNamer = sysMsgNamer;
            OpcodeNameToType.Clear();
            CoreServices.ToList().ForEach(x=>OpcodeNameToType[_opCodeNamer.GetCode(x.Key)]=x.Value);
            OpcodeNameToType[0] = UnknownMessageDelegate;
            Version = version;
            Region = region;
        }

        public void ReloadSysMsg() { _sysMsgNamer?.Reload(Version, ReleaseVersion); }

        public MessageFactory()
        {
            _opCodeNamer = new OpCodeNamer(new Dictionary<ushort, string> { { 19900, "C_CHECK_VERSION" } });
            Version = 0;
            Region = "Unknown";
        }

        private ParsedMessage Instantiate(ushort opCode, TeraMessageReader reader)
        {
            if (!OpcodeNameToType.TryGetValue(opCode, out var type))
                type = UnknownMessageDelegate;
            return (ParsedMessage) type.DynamicInvoke(reader);
        }

        public ParsedMessage Create(Message message)
        {
            var reader = new TeraMessageReader(message, _opCodeNamer, this, _sysMsgNamer);
            return Instantiate(message.OpCode, reader);
        }
    }
}
