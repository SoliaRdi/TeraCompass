using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
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
        private string _logData;
        private CaptureProcess _captureProcess;
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
        #endregion
        private void AttachProcess()
        {
            string exeName = Path.GetFileNameWithoutExtension("TERA.exe");

            Process process= Process.GetProcessesByName(exeName).FirstOrDefault();
            if (process !=null)
            {
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    process.WaitForInputIdle();
                    var check = process.MainWindowHandle;
                    LogEvent(process.MainWindowHandle.ToString());
                    while (check == process.MainWindowHandle)
                    {
                        process = Process.GetProcessesByName(exeName).FirstOrDefault();
                        Thread.Sleep(500);
                    }
                }
                // Simply attach to the first one found.
                
                // If the process doesn't have a mainwindowhandle yet, skip it (we need to be able to get the hwnd to set foreground etc)
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    LogEvent("no MainWindowHandle...");
                    return;
                }

                // Skip if the process is already hooked (and we want to hook multiple applications)
                if (Capture.Hook.HookManager.IsHooked(process.Id))
                {
                    LogEvent("Game already hooked...");
                    return;
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
                _captureProcess = new CaptureProcess(process, cc, captureInterface);
                LogEvent("Compass initialized...");
            }
            else
            {
                LogEvent("error...");
            }
            Thread.Sleep(10);
            
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
