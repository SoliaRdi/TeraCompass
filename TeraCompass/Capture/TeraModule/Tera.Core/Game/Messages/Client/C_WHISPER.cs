using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Client
{
    public class C_WHISPER : ParsedMessage
    {
        internal C_WHISPER(TeraMessageReader reader) : base(reader)
        {
            TargetOffset = reader.ReadUInt16();
            TextOffset = reader.ReadUInt16();
            Target = reader.ReadTeraString();
            Text = reader.ReadTeraString();


        }

        public ushort TargetOffset { get; set; }
        public ushort TextOffset { get; set; }
        public string Target { get; set; }
        public string Text { get; set; }

    }
}