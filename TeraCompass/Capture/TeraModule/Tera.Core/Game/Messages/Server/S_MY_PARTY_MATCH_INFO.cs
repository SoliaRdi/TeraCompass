using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class S_MY_PARTY_MATCH_INFO : ParsedMessage
    {
        internal S_MY_PARTY_MATCH_INFO(TeraMessageReader reader) : base(reader)
        {
            var offset = reader.ReadUInt16();
            
            reader.Skip(1);
            Message = reader.ReadTeraString();
        }

        public string Message { get; }
    }
}