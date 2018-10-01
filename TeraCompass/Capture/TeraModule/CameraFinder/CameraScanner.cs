using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Capture.TeraModule.CameraFinder
{
    public class CameraScanner
    {
        private Process Process { get; set; }

        public CameraScanner(Process process)
        {
            CameraAddress = 0;
            CameraAngle = 0;
            Process = process;
        }

        public void FindCameraAddress()
        {
            using (var memoryScanner = new MemoryScanner(Process))
            {
                foreach (var region in memoryScanner.MemoryRegions().Where(
                    x => x.Protect.HasFlag(MemoryScanner.AllocationProtectEnum.PAGE_READWRITE) &&
                         x.State.HasFlag(MemoryScanner.StateEnum.MEM_COMMIT) &&
                         x.Type.HasFlag(MemoryScanner.TypeEnum.MEM_PRIVATE)))
                {
                    try
                    {
                        var patternData = BitConverter.ToString(memoryScanner.ReadMemory(region.BaseAddress, (int) region.RegionSize));
                        var match = Regex.Match(patternData, @"80\-3F\-00\-00\-80\-40\-00\-00\-80\-41\-00\-00\-80\-3F\-00\-00\-80\-3F\-FF\-FF\-FF\-FF\-00\-00\-00\-00\-00\-00\-FA\-44\-00\-00\-00\-00\-00\-00\-00\-00\-00\-00\-00\-00\-00\-00\-00\-00\-00\-00\-80\-3F.{498}\-FF\-FF\-(.{5})");

                        if (match.Success)
                        {
                            CameraAddress = region.BaseAddress + (uint) (match.Index + match.Length - 5) / 3;
                            return;
                        }
                        else
                        {
                            Debug.Write("Camera not founded");
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        public int Angle()
        {
            using (var memoryScanner = new MemoryScanner(Process))
            {
                var data = memoryScanner.ReadMemory(CameraAddress, 2);
                int angle = BitConverter.ToInt16(data, 0);

                CameraAngle = angle;
            }
            return CameraAngle;
        }


        public uint CameraAddress { get; set; }

        public int CameraAngle { get; private set; }
    }
}
