using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Services;

namespace Capture.TeraModule.Processing.Packets
{
    class S_USER_DEATH:ParsedMessage
    {
        public bool Enemy { get; set; }
        public string Killed { get; set; }
        public string Killer { get; set; }
        internal S_USER_DEATH(TeraMessageReader reader) : base(reader)
        {
            Enemy = (reader.ReadByte() & 1) == 0;
            reader.Skip(4);
            Killed = reader.ReadTeraString();
            Killer = reader.ReadTeraString();
            File.AppendAllText("combat.log",$"{DateTime.Now}|{Enemy}|{Killed}|{Killer}\n");
        }
    }
}
