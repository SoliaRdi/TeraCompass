using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class SPartyMemberAbnormalDel : ParsedMessage
    {
        internal SPartyMemberAbnormalDel(TeraMessageReader reader) : base(reader)
        {
            ServerId = reader.ReadUInt32();
            PlayerId = reader.ReadUInt32();
            AbnormalityId = reader.ReadInt32();
        }


        public int AbnormalityId { get; }

        public uint ServerId { get; }
        public uint PlayerId { get; }
    }
}