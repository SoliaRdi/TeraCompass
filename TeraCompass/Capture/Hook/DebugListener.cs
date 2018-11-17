using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capture.Interface;

namespace Capture.Hook
{
    class DebugListener : TraceListener
    {
        private Capture.Interface.CaptureInterface _captureInterface;
        public DebugListener(Capture.Interface.CaptureInterface captureInterface)
        {
            _captureInterface = captureInterface;
        }

        public override void Write(string message)
        {
            _captureInterface.Message(MessageType.Debug, message);
        }

        public override void WriteLine(string message)
        {
            File.AppendAllText("debug.log", message);
        }
    }
}
