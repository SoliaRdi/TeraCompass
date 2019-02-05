using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Capture;
using Capture.Hook;
using Capture.Interface;
using Capture.TeraModule.Tera.Core.Game;

namespace TeraCompass.ViewModels
{
    public class MainViewModel : Conductor<IScreen>.Collection.AllActive, IHandle<string>
    {
        private CaptureProcess _captureProcess;

        private string _logData;
        private bool _waitSplash;


        public MainViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);
        }

        public bool WaitSplash
        {
            get => _waitSplash;
            set
            {
                _waitSplash = value;
                NotifyOfPropertyChange();
            }
        }

        public void Handle(string message)
        {
            LogEvent(message);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern long GetWindowText(IntPtr hwnd, StringBuilder lpString, long cch);

        [DllImport("User32.dll")]
        private static extern IntPtr GetParent(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsHungAppWindow(IntPtr hWnd);

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern long GetClassName(IntPtr hwnd, StringBuilder lpClassName, long nMaxCount);

        private string GetClassNameOfWindow(IntPtr hwnd)
        {
            var className = "";
            StringBuilder classText = null;
            try
            {
                var cls_max_length = 1000;
                classText = new StringBuilder("", cls_max_length + 5);
                GetClassName(hwnd, classText, cls_max_length + 2);

                if (!string.IsNullOrEmpty(classText.ToString()) && !string.IsNullOrWhiteSpace(classText.ToString()))
                    className = classText.ToString();
            }
            catch (Exception ex)
            {
                className = ex.Message;
            }
            finally
            {
                classText = null;
            }

            return className;
        }

        private void AttachProcess()
        {
            var exeName = Path.GetFileNameWithoutExtension("TERA.exe");

            Process = Process.GetProcessesByName(exeName).FirstOrDefault();
            if (Process != null)
            {
                Process.WaitForInputIdle();
                var className = GetClassNameOfWindow(Process.MainWindowHandle);
                while (!className.Contains("Launch"))
                {
                    Process.Refresh();
                    Thread.Sleep(50);
                    className = GetClassNameOfWindow(Process.MainWindowHandle);
                }

                Process.Refresh();
                Thread.Sleep(100);
                LogEvent(className);
                if (Process.MainWindowHandle == IntPtr.Zero)
                {
                    LogEvent("no MainWindowHandle...");
                    return;
                }

                if (HookManager.IsHooked(Process.Id))
                {
                    LogEvent("Game already hooked...");
                    return;
                }

                Thread.Sleep(100);

                while (IsHungAppWindow(Process.MainWindowHandle)) Thread.Sleep(100);


                var direct3DVersion = Direct3DVersion.Direct3D9;

                var cc = new CaptureConfig
                {
                    Direct3DVersion = direct3DVersion,
                    TargetFramesPerSecond = 5
                };

                WaitAppExit();
                LogEvent("Begin inject");
                var captureInterface = new CaptureInterface();
                captureInterface.RemoteMessage += e => { LogEvent(e.Message); };
                //captureInterface.Disconnected += CaptureInterface_Disconnected;
                try
                {
                    _captureProcess = new CaptureProcess(Process, cc, captureInterface);
                }
                catch (Exception ex)
                {
                    LogEvent(ex.Message);
                    if (ex.InnerException != null) LogEvent(ex.InnerException.Message);
                    if (ex.InnerException != null) LogEvent(ex.InnerException.StackTrace);
                    LogEvent(ex.StackTrace);
                }
            }
            else
            {
                LogEvent("error...");
            }
        }

        public void WaitAppExit()
        {
            Task.Factory.StartNew(() =>
            {
                var query = new WqlEventQuery(
                    "__InstanceDeletionEvent",
                    new TimeSpan(0, 0, 4),
                    "TargetInstance isa \"Win32_Process\" and TargetInstance.Name = 'TERA.EXE'"
                );

                using (var watcher = new ManagementEventWatcher(query))
                {
                    watcher.WaitForNextEvent();
                    LogEvent("Client down");
                    InitializeProgram();
                    watcher.Stop();
                }
            });
        }

        public void LogEvent(string text)
        {
            LogData += Environment.NewLine + text;
        }

        public void Handle(CollectionEntity entity)
        {
        }

        public void InitializeProgram()
        {
            Task.Factory.StartNew(() =>
            {
                var query = new WqlEventQuery(
                    "__InstanceCreationEvent",
                    new TimeSpan(0, 0, 1),
                    "TargetInstance isa \"Win32_Process\" and TargetInstance.Name = 'TERA.EXE'"
                );

                using (var watcher = new ManagementEventWatcher(query))
                {
                    LogEvent("Waiting game...");
                    watcher.WaitForNextEvent();
                    LogEvent("Game launched...");

                    watcher.Stop();
                    Task.Factory.StartNew(AttachProcess);
                }
            });
        }

        public void PcapWarning(string str)
        {
            LogEvent(str);
        }

        #region public members

        public string LogData
        {
            get => _logData;
            set
            {
                _logData = value;
                NotifyOfPropertyChange(() => LogData);
            }
        }

        public Process Process { get; set; }

        #endregion
    }
}