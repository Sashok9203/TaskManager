using Microsoft.Win32;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace WpfApp3
{
    

    [AddINotifyPropertyChangedInterface]
    internal class ProcessInfo
    {
        public PerformanceCounter CpuCounter;
        public string ProcessName { get; set; }
        public int ID { get; set; }
        public string Priority { get; set; }
        public string ProcessorTime { get; set; }
        public string StartTime { get; set; }
        public string ProcessQwner { get; set; }
        public string Ram { get; set; }
    }

    internal class Model : INotifyPropertyChanged
    {
        private int selectedIndex = 0;
        private readonly string noInfoStr = "No info";
        private DispatcherTimer timer;
        private Process[] processes;
        private Thread UpdateInfo;

        private void UpdateProcesses()
        {
            string processName, priority, startTime, processQwner, processorTime, ram;
            processName = priority = startTime = processQwner = processorTime = ram = noInfoStr;
            int id;
            PerformanceCounter cpuCounter, ramCounter;

            processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try { priority = process.PriorityClass.ToString(); } catch { priority = noInfoStr; };
                ProcessInfo? processInfo = ProcessesInfo.FirstOrDefault(x => x.ID == process.Id);
                if (processInfo != null)
                {
                    try { processorTime = $"{Math.Round(processInfo.CpuCounter.NextValue() / Environment.ProcessorCount, 1)} %"; } catch { }
                    try { ram = $"{Math.Round((double)process.WorkingSet64 / 1024, 3)} Kb"; } catch { }
                    Application.Current?.Dispatcher.Invoke(DispatcherPriority.Render,  new Action(() =>
                    {
                        processInfo.Priority = priority;
                        processInfo.Ram = ram;
                        processInfo.ProcessorTime = processorTime;
                    }));
                }
                else
                {
                    try { startTime = process.StartTime.ToShortTimeString(); } catch { startTime = noInfoStr; }
                    processQwner = GetUserNameProcess(process.Id);
                    processName = process.ProcessName;
                    id = process.Id;
                    cpuCounter = new("Process", "% Processor Time", processName);
                    try { processorTime = $"{Math.Round(cpuCounter.NextValue() / Environment.ProcessorCount, 1)} %"; } catch { }
                    try { ram = $"{Math.Round((double)process.WorkingSet64 / 1024, 3)} Kb"; } catch { }
                    Application.Current?.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
                    {
                        ProcessInfo temp;
                        temp = new()
                        {
                            CpuCounter = cpuCounter,
                            ProcessorTime = processorTime,
                            ID = id,
                            Priority = priority,
                            StartTime = startTime,
                            ProcessName = processName,
                            ProcessQwner = GetUserNameProcess(id),
                            Ram = ram
                        };
                        ProcessesInfo.Add(temp);
                    }));
                }
            }

            if (processes.Length < ProcessesInfo.Count)
            {
                List<ProcessInfo> toDelete = new();
                foreach (var process in ProcessesInfo)
                    if (processes.FirstOrDefault(x => x.Id == process.ID) == null) toDelete.Add(process);
                foreach (var process in toDelete)
                    Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                    {
                        ProcessesInfo.Remove(process);
                    }));
            }
        }

        private string GetUserNameProcess(int processId)
        {
            string query = "Select * From Win32_Process Where ProcessID = " + processId;
            ManagementObjectSearcher searcher = new(query);
            ManagementObjectCollection processList = searcher.Get();

            foreach (ManagementObject obj in processList)
            {
                int returnVal = -1;
                string[] argList = new string[] { string.Empty, string.Empty };
                try { returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList)); } catch { }
                if (returnVal == 0) return argList[0];
            }
            return noInfoStr;
        }

        private void Update(object sender, EventArgs e)
        {
            if (UpdateInfo.ThreadState == System.Threading.ThreadState.Unstarted) UpdateInfo.Start();
            else if (UpdateInfo.ThreadState == System.Threading.ThreadState.Stopped)
            {
                UpdateInfo = new(UpdateProcesses)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                UpdateInfo.Start();
                if (DataVisible == Visibility.Hidden)
                {
                    DataVisible = Visibility.Visible;
                    OnPropertyChanged("DataVisible");
                }
            }
        }

        private void killProcess()
        {
            try { Process.GetProcessById(SelectedProcess.ID).Kill(); }
            catch (Exception e) { MessageBox.Show(e.Message); }
        }

        private void closeProcess()
        {
            try 
            {
                Process proc = Process.GetProcessById(SelectedProcess.ID);
                if (proc.CloseMainWindow()) proc.Close();
                else proc.Kill(true);
            } catch (Exception e) { MessageBox.Show(e.Message); }
        }

        private void exit() => Application.Current.Shutdown();

        private void start()
        {
            try { Process.Start(Path.Combine(ProcPath)); }
            catch (Exception e) { MessageBox.Show(e.Message); }
        }

        private void getPath()
        {
            OpenFileDialog ofd = new() { Filter = "EXE files (*.exe)|*.exe" };
            ofd.ShowDialog();
            ProcPath = ofd.FileName;
        }

        public Model()
        {
            DataVisible = Visibility.Hidden;
            ProcessesInfo = new();
            UpdateInfo = new(UpdateProcesses);
            timer = new();
            timer.Tick += Update;
            timer.Start();
        }

        public ProcessInfo SelectedProcess { get; set; }

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                selectedIndex = value;
                int sec = 0;
                switch (selectedIndex)
                {
                    case 0:
                    case 1:
                        sec = selectedIndex + 1;
                        break;
                    case 2:
                        sec = 5;
                        break;
                    case 3:
                        timer.Stop();
                    return;
                }
                timer.Stop();
                timer.Interval = new TimeSpan(0, 0, sec);
                timer.Start();
            }
        }

        public Visibility DataVisible { get; set; }
        public ObservableCollection<ProcessInfo> ProcessesInfo { get; set; }

        public  string[] UpdateComboboxStr { get; } = { "1 second", "2 seconds", "5 seconds", "Manual" };
        public event PropertyChangedEventHandler? PropertyChanged;

        public string ProcPath { get; set; }

        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public RelayCommand UpdateButton => new((o) => Update(this, new()),(o)=> DataVisible == Visibility.Visible && SelectedIndex == 3);
        public RelayCommand KillProcButton => new((o) => killProcess(), (o) => DataVisible == Visibility.Visible && SelectedProcess != null);
        public RelayCommand CloseProcButton => new((o) => closeProcess(), (o) => DataVisible == Visibility.Visible && SelectedProcess != null);
        public RelayCommand ExitButton => new((o) => exit());
        public RelayCommand StartButton => new((o) => start(),(o) => !string.IsNullOrEmpty(ProcPath));
        public RelayCommand GetPathButton => new((o) => getPath());

    }
}
