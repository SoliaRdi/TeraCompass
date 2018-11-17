using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Capture.TeraModule.Processing;
using TeraCompass.Processing;
using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Client
{
    public class C_CHECK_VERSION : ParsedMessage
    {
        internal C_CHECK_VERSION(TeraMessageReader reader) : base(reader)
        {
            Versions = new Dictionary<uint, uint>();
            var count = reader.ReadUInt16();
            var offset = reader.ReadUInt16();
            for (var i = 1; i <= count; i++)
            {
                reader.BaseStream.Position = offset-4;
                var pointer = reader.ReadUInt16();
                Trace.Assert(pointer==offset);//should be the same
                var nextOffset = reader.ReadUInt16();
                var VersionKey = reader.ReadUInt32();
                var VersionValue = reader.ReadUInt32();
                Versions.Add(VersionKey,VersionValue);
                offset = nextOffset;
            }

            Trace.Write(BasicTeraData.Instance.ResourceDirectory);
            {
                if (!Directory.Exists(Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/")))
                    Directory.CreateDirectory(Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/"));

                OpcodeDownloader.DownloadIfNotExist(Versions[0], Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/"));
                if (!File.Exists(Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/{Versions[0]}.txt")) && !File.Exists(Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/protocol.{Versions[0]}.map")))
                {
                    Trace.Write("Unknown client version: " + Versions[0]);
                    PacketProcessor.Instance.Exit();
                    return;
                }
                var opCodeNamer = new OpCodeNamer(Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/{Versions[0]}.txt"));
                OpCodeNamer sysMsgNamer = null; //new OpCodeNamer(Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/smt_{Versions[0]}.txt"));
                TeraSniffer.Instance.Connected = true;
                PacketProcessor.Instance.MessageFactory = new MessageFactory(opCodeNamer, PacketProcessor.Instance.Server.Region, Versions[0], sysMsgNamer);

                if (TeraSniffer.Instance.ClientProxyOverhead + TeraSniffer.Instance.ServerProxyOverhead > 0x1000)
                {
                    Trace.Write("Client Proxy overhead: " + TeraSniffer.Instance.ClientProxyOverhead + "\r\nServer Proxy overhead: " +
                                TeraSniffer.Instance.ServerProxyOverhead);
                }
                Trace.Write("protocol version = " + Versions[0]);
            }
            //Trace.WriteLine(Versions.Aggregate(new StringBuilder(), (sb, x) => sb.Append(x.Key + " - " + x.Value + " | "), sb => sb.ToString(0, sb.Length - 1)));
        }

        public Dictionary<uint,uint> Versions { get; set; }
    }
}