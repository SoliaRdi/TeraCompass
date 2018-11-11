using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using Capture;
using Capture.GUI;
using Capture.Interface;

namespace TeraCompass.ViewModels
{
    public class MainViewModel : Conductor<IScreen>.Collection.AllActive, IHandle<string>
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern long GetWindowText(IntPtr hwnd, StringBuilder lpString, long cch);

        [DllImport("User32.dll")]
        static extern IntPtr GetParent(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsHungAppWindow(IntPtr hWnd);

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern long GetClassName(IntPtr hwnd, StringBuilder lpClassName, long nMaxCount);
        string GetClassNameOfWindow(IntPtr hwnd)
        {
            string className = "";
            StringBuilder classText = null;
            try
            {
                int cls_max_length = 1000;
                classText = new StringBuilder("", cls_max_length + 5);
                GetClassName(hwnd, classText, cls_max_length + 2);

                if (!String.IsNullOrEmpty(classText.ToString()) && !String.IsNullOrWhiteSpace(classText.ToString()))
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
        public bool WaitSplash
        {
            get => _waitSplash;
            set
            {
                _waitSplash = value;
                NotifyOfPropertyChange();
            }
        }

        private string _logData;
        private CaptureProcess _captureProcess;
        private bool _waitSplash =true;

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
        private void AttachProcess()
        {
            string exeName = Path.GetFileNameWithoutExtension("TERA.exe");

            Process = Process.GetProcessesByName(exeName).FirstOrDefault();
            if (Process != null)
            {
                Process.WaitForInputIdle();
                var className = GetClassNameOfWindow(Process.MainWindowHandle);
                if (WaitSplash)
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
                if (Capture.Hook.HookManager.IsHooked(Process.Id))
                {
                    LogEvent("Game already hooked...");
                    return;
                }
                
                Thread.Sleep(100);
                while (IsHungAppWindow(Process.MainWindowHandle))
                {
                    Thread.Sleep(100);
                }



                Direct3DVersion direct3DVersion = Direct3DVersion.Direct3D9;

                CaptureConfig cc = new CaptureConfig()
                {
                    Direct3DVersion = direct3DVersion,
                    ShowOverlay = false,
                    TargetFramesPerSecond = 6
                };

                var captureInterface = new CaptureInterface();
                captureInterface.RemoteMessage += e => { LogEvent(e.Message);};
                //captureInterface.Disconnected += CaptureInterface_Disconnected;
                _captureProcess = new CaptureProcess(Process, cc, captureInterface);

                Process.EnableRaisingEvents = true;
                Process.Exited += Process_Exited;
            }
            else
            {
                LogEvent("error...");
            }
            Thread.Sleep(10);
            
        }

        private void CaptureInterface_Disconnected()
        {
            LogEvent("Client down");
            Thread.Sleep(1000);
            
            InitializeProgram();
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            
            LogEvent("Client down");
            InitializeProgram();
        }

        public MainViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);
        }

        public void UnloadProgram()
        {
           
        }
        public void LogEvent(string text)
        {
            LogData += Environment.NewLine + text;
        }

        public void Handle(string message)
        {
            LogEvent(message);
        }

        public void InitializeProgram()
        {
            if (Process.GetProcessesByName("TERA").Length==0)
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
                        ManagementBaseObject e = watcher.WaitForNextEvent();
                        LogEvent("Game launched...");

                        watcher.Stop();
                        Task.Factory.StartNew(AttachProcess);
                    }
                });
            }
            else
            {
                Task.Factory.StartNew(AttachProcess);
            }
        }
        public void PcapWarning(string str)
        {
            LogEvent(str);
        }
    }
}
