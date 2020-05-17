using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

namespace Papyrus.Automation
{
    public class ProcessManager
    {
        public Process Process { get; private set; }
        private ProcessStartInfo _startInfo;
        private string[] _ignorePatterns = new string[0];
        private string _lastMessage = "";
        private string _pattern;
        public bool HasMatched { get; private set; } = false;
        private string _matchedText;
        public bool EnableConsoleOutput { get; set; } = true;

        public bool IsRunning
        {
            get
            {
                bool result;
                try
                {
                    result = Process.HasExited ? false : true;
                } 
                catch 
                {
                    result = false;
                }

                return result;
            }
        }

        ///<param name="startInfo">Start configuration for the process.</param>
        public ProcessManager(ProcessStartInfo startInfo)
        {
            _startInfo = startInfo;
            Process = new Process();
            Process.StartInfo = startInfo;
            Process.StartInfo.RedirectStandardInput = true;
            Process.StartInfo.RedirectStandardOutput = true;
            Process.StartInfo.UseShellExecute = false;
            Process.OutputDataReceived += OutputTextReceived;
        }

        ///<summary>Starts the underlying process and begins reading it's output.</summary>
        ///<param name="startInfo">Start configuration for the process.</param>
        ///<param name="ignoreMessages">Array of messages that should not be redirected when written to the underlying processes stdout.</param>
        public ProcessManager(ProcessStartInfo startInfo, string[] ignorePatterns) : this(startInfo)
        {
            _ignorePatterns = ignorePatterns;
        }

        ///<summary>Starts the underlying process and begins reading it's output.</summary>
        public bool Start()
        {
            if (Process.Start())
            {
                Process.BeginOutputReadLine();
                return true;
            } 
            else 
            {
                return false;
            }
        }

        ///<summary>Frees the underlying process.</summary>
        public void Close()
        {
            Process?.CancelOutputRead();
            Process?.Close();
        }

        public void WaitForStart()
        {
            WaitForMatch(@"^.+ (Server started\.)");
        }

        public void WaitForExit()
        {
            WaitForMatch(@"(Quit correctly)");
        }

        ///<summary>Sends a command to the underlying processes stdin and executes it.</summary>
        ///<param name="cmd">Command to execute.</param>
        public void SendInput(string cmd)
        {
            Process.StandardInput.Write(cmd + "\n");
        }

        ///<summary>Halt program flow until the specified regex pattern has matched in the underlying processes stdout.</summary>
        public void WaitForMatch(string pattern)
        {
            bool ready = false;

            while (!ready)
            {
                if (!string.IsNullOrEmpty(_lastMessage))
                {
                    ready = Regex.Matches(_lastMessage, pattern).Count >= 1;
                }

                Thread.Sleep(1);
            }
        }

        public void SetMatchPattern(string pattern)
        {
            HasMatched = false;
            _pattern = pattern;
        }

        public string GetMatchedText()
        {
            return _matchedText;
        }

        ///<summary>Executes a custom command in the operating systems shell.</summary>
        ///<param name="cmd">Command to execute.</param>
        public static void RunCustomCommand(string cmd)
        {
            string shell = "";
            string args = "";
            switch ((int)Environment.OSVersion.Platform)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    shell = @"C:\Windows\System32\cmd.exe";
                    args = "/k \"" + cmd + "\"";
                    break;
                case 4:
                    shell = "/bin/bash";
                    args = "-c \"" + cmd + "\"";
                    break;
            }

            Process p = new Process();
            p.StartInfo.FileName = shell;
            p.StartInfo.Arguments = args;

            p.Start();
            p.WaitForExit();
        }

        public void SendTellraw(string message)
        {
            if (IsRunning && !Program.RunConfig.QuietMode)
            {
                #if !IS_LIB
                SendInput("tellraw @a {\"rawtext\":[{\"text\":\"§l[PAPYRUS]\"},{\"text\":\"§r " + message + "\"}]}");
                #endif
            }                
        }

        private void OutputTextReceived(object sender, DataReceivedEventArgs e)
        {
            _lastMessage = e.Data;

            if (!HasMatched && _pattern != null)
            {
                if (Regex.Matches(e.Data, _pattern).Count >= 1)
                {
                    HasMatched = true;
                    _pattern = null;
                    _matchedText = e.Data;
                }
            }

            if (EnableConsoleOutput)
            {
                bool showMsg = true;

                if (_ignorePatterns.Length > 0)
                {
                    foreach (string pattern in _ignorePatterns)
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data) && Regex.Matches(e.Data, pattern).Count > 0)
                        {
                            showMsg = false;
                            break;
                        }
                    }
                }

                if (showMsg) { Console.WriteLine(e.Data); }
            }
        }
    }
}
