using System;
using System.Diagnostics;
using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages
{
    // Base class for parsed messages
    public abstract class ParsedMessage : Message
    {
        internal ParsedMessage(TeraMessageReader reader)
            : base(reader.Message.Time, reader.Message.Direction, reader.Message.Data)
        {
            Raw = reader.Message.Payload.Array;
            OpCodeName = reader.OpCodeName;
        }

        public byte[] Raw { get; protected set; }

        public string OpCodeName { get; }

        public void PrintRaw()
        {
            Trace.WriteLine(OpCodeName + " : " + OpCode + " : " + Direction + " : Size " + Payload.Count + " : Time " + Time);
            Trace.WriteLine(BitConverter.ToString(Payload.Array));
        }
    }
}