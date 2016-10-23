using CommandLine;
using CommandLine.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;

namespace SSH_Agent_Helper
{
    class Program
    {
        static string SSH_AUTH_SOCK = "SSH_AUTH_SOCK";
        static string SSH_AGENT_PID = "SSH_AGENT_PID";
        static string AgentSock;
        static string AgentPID;

        static void Main(string[] args)
        {

            AgentSock = Environment.GetEnvironmentVariable(SSH_AUTH_SOCK, EnvironmentVariableTarget.Process);
            AgentPID = Environment.GetEnvironmentVariable(SSH_AGENT_PID, EnvironmentVariableTarget.Process);
            if (String.IsNullOrEmpty(AgentSock))
            {
                AgentSock = Environment.GetEnvironmentVariable(SSH_AUTH_SOCK, EnvironmentVariableTarget.User);
            }
            if (String.IsNullOrEmpty(AgentPID))
            {
                AgentPID = AgentPID = Environment.GetEnvironmentVariable(SSH_AGENT_PID, EnvironmentVariableTarget.User);
            }

            var options = new Options();

            if (CommandLine.Parser.Default.ParseArguments(args, options) && args.Length > 0)
            {
                if (options.Kill)
                {
                    KillSSHAgent();
                }
                else if (options.RegisterStartup)
                {
                    if (options.Others.Count > 0)
                    {
                        options.Others.Add("-s");
                        RegisterStartup(options.Others);
                    }
                    else
                    {
                        RegisterStartup(new List<string>() { });
                    }
                }
                else if (options.UnregisterRestartup)
                {
                    UnregisterStartup();
                }
                else if (options.Startup && options.Add)
                {
                    RunSSHAgent();
                    AddSSHKeys(options.Others, true);
                }
                else if (options.Add)
                {
                    AddSSHKeys(options.Others);
                }
                else
                {
                    Console.Write(options.GetUsage());
                    Environment.Exit(1);
                }
                Environment.Exit(0);
            } else if (args.Length == 0)
            {
                RunSSHAgent();
            }
        }

        static void RunSSHAgent()
        {
            try
            {
                Process existingProcess = Process.GetProcessById(Convert.ToInt32(AgentPID));

                if (!existingProcess.Responding || Convert.ToInt32(AgentPID) < 1)
                {
                    throw new Exception("There is no process running or the previous ssh-agent is not responding.");
                }

                Console.Error.WriteLine("Another ssh-agent (PID: " + AgentPID + ") is already running healthily");
            } catch (Exception)
            {
                string SSHAgentPath = FindProgram("ssh-agent.exe");
                Process SSHAgent = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = SSHAgentPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                try
                {
                    SSHAgent.Start();

                    Process parent = FindParent.ParentProcess(Process.GetCurrentProcess());

                    while (!SSHAgent.StandardOutput.EndOfStream)
                    {
                        var line = SSHAgent.StandardOutput.ReadLine();

                        string[] splits = line.Split(';');
                        string[] command = splits[0].Split('=');

                        if (command[0] == SSH_AUTH_SOCK && command.Length > 1)
                        {
                            AgentSock = command[1];
                            Environment.SetEnvironmentVariable(SSH_AUTH_SOCK, command[1], EnvironmentVariableTarget.User);
                        }
                        else if (command[0] == SSH_AGENT_PID && command.Length > 1)
                        {
                            AgentPID = command[1];
                            Environment.SetEnvironmentVariable(SSH_AGENT_PID, command[1], EnvironmentVariableTarget.User);
                        }
                    }

                    if (parent.ProcessName == "powershell")
                    {
                        Console.WriteLine("$env:" + SSH_AUTH_SOCK + "=\"" + AgentSock + "\"");
                        Console.WriteLine("$env:" + SSH_AGENT_PID + "=\"" + AgentPID + "\"");
                    }
                    else
                    {
                        Console.WriteLine("set " + SSH_AUTH_SOCK + "=" + AgentSock);
                        Console.WriteLine("set " + SSH_AGENT_PID + "=" + AgentPID);
                    }

                    Console.WriteLine((parent.ProcessName == "powershell" ? "# " : "rem ") +
                                          "Your environment has been configured. " +
                                          "Run these commands to configure current terminal or open a new one.");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    Environment.Exit(1);
                }
            }
        }

        static void KillSSHAgent()
        {
            if(String.IsNullOrEmpty(AgentPID))
            {
                Console.Error.WriteLine("The environment is currently not configured for an ssh-agent. So, can't kill any.");
                Environment.Exit(1);
            }

            string SSHAgentPath = FindProgram("ssh-agent.exe");
            Process SSHAgent = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = SSHAgentPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    Arguments = "-k",
                }
            };

            try
            {
                SSHAgent.Start();

                Environment.SetEnvironmentVariable(SSH_AGENT_PID, null, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable(SSH_AUTH_SOCK, null, EnvironmentVariableTarget.User);

                Process parent = FindParent.ParentProcess(Process.GetCurrentProcess());

                if (parent.ProcessName == "powershell")
                {
                    Console.WriteLine("Remove-Item env:" + SSH_AUTH_SOCK);
                    Console.WriteLine("Remove-Item env:" + SSH_AGENT_PID);
                }
                else
                {
                    Console.WriteLine("set " + SSH_AUTH_SOCK + "=");
                    Console.WriteLine("set " + SSH_AGENT_PID + "=");
                }

                Console.WriteLine((parent.ProcessName == "powershell" ? "# " : "rem ") +
                                      "ssh-agent has been killed and your environment has been configured. " +
                                      "Run these commands to configure current terminal or open a new one.");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Environment.Exit(1);
            }
        }

        static string FindProgram(string name)
        {
            Process Where = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = @"C:\Windows\System32\where.exe",
                    Arguments = name,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            try
            {
                Where.Start();
                while (!Where.StandardOutput.EndOfStream)
                {
                    return (string)Where.StandardOutput.ReadLine();
                }
                throw new Exception(name + ".exe was not found in %PATH%");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Environment.Exit(1);
                return "";
            }
        }

        static void RegisterStartup(IList<string> args)
        {
            manageStartup(args);
        }

        static void UnregisterStartup()
        {
            manageStartup(new List<string>() { }, true);
        }

        private static void manageStartup(IList<string> args, bool remove = false)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey
                    (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (!remove)
            {
                string parameters = (new Uri(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).LocalPath;
                       parameters = parameters + " " + String.Join(" ", args);
                registryKey.SetValue("SSH Agent Helper", parameters);

                Console.WriteLine("SSH Agent Helper has been register to run at Startup with these parameters: " +
                                    String.Join(" ", parameters));
            } else if (registryKey.GetValue("SSH Agent Helper") != null)
            {
                registryKey.DeleteValue("SSH Agent Helper");
                Console.WriteLine("SSH Agent Helper registery for Startup has been removed.");
            }
            else
            {
                Console.WriteLine("SSH Agent Helper registery has already been removed.");
            }
        }

        static void AddSSHKeys(IList<string> paths, bool customENV = false)
        {
            string SSHAddPath = FindProgram("ssh-add.exe");
            Process SSHAdd = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = SSHAddPath,
                    UseShellExecute = false,
                    Arguments = String.Join(" ", paths)
                }
            };

            if (customENV)
            {
                SSHAdd.StartInfo.EnvironmentVariables[SSH_AGENT_PID] = AgentPID;
                SSHAdd.StartInfo.EnvironmentVariables[SSH_AUTH_SOCK] = AgentSock;
            }

            try
            {
                SSHAdd.Start();

                SSHAdd.WaitForExit();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Environment.Exit(1);
            }
        }
    }

    static class FindParent {
        public static Process ParentProcess(this Process process)
        {
            return Process.GetProcessById(ParentProcessId(process.Id));
        }
        public static int ParentProcessId(this Process process)
        {
            return ParentProcessId(process.Id);
        }
        public static int ParentProcessId(int Id)
        {
            PROCESSENTRY32 pe32 = new PROCESSENTRY32 { };
            pe32.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
            using (var hSnapshot = CreateToolhelp32Snapshot(SnapshotFlags.Process, (uint)Id))
            {
                if (hSnapshot.IsInvalid)
                    throw new Win32Exception();

                if (!Process32First(hSnapshot, ref pe32))
                {
                    int errno = Marshal.GetLastWin32Error();
                    if (errno == ERROR_NO_MORE_FILES)
                        return -1;
                    throw new Win32Exception(errno);
                }
                do
                {
                    if (pe32.th32ProcessID == (uint)Id)
                        return (int)pe32.th32ParentProcessID;
                } while (Process32Next(hSnapshot, ref pe32));
            }
            return -1;
        }
        private const int ERROR_NO_MORE_FILES = 0x12;
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeSnapshotHandle CreateToolhelp32Snapshot(SnapshotFlags flags, uint id);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);

        [Flags]
        private enum SnapshotFlags : uint
        {
            HeapList = 0x00000001,
            Process = 0x00000002,
            Thread = 0x00000004,
            Module = 0x00000008,
            Module32 = 0x00000010,
            All = (HeapList | Process | Thread | Module),
            Inherit = 0x80000000,
            NoHeaps = 0x40000000
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        };
        [SuppressUnmanagedCodeSecurity, HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
        internal sealed class SafeSnapshotHandle : SafeHandleMinusOneIsInvalid
        {
            internal SafeSnapshotHandle() : base(true)
            {
            }

            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            internal SafeSnapshotHandle(IntPtr handle) : base(true)
            {
                base.SetHandle(handle);
            }

            protected override bool ReleaseHandle()
            {
                return CloseHandle(base.handle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success), DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
            private static extern bool CloseHandle(IntPtr handle);
        }
    }

    class Options
    {
        [Option('r', "register-startup", Required = false,
            HelpText = "Register this program to run at Windows Startup. Parameters for startup are optional. " +
                       "E.g.: ssh-agent-helper -r -a %USERPROFILE%\\.ssh\\id_rsa")]
        public bool RegisterStartup { get; set; }

        [Option('u', "unregister-startup", Required = false,
            HelpText = "Disable run at Windows Startup behaviour.")]
        public bool UnregisterRestartup { get; set; }

        [Option('k', "kill", Required = false,
            HelpText = "Kill the current ssh-agent process and unset environment variables.")]
        public bool Kill { get; set; }

        [Option('s', "startup", Required = false,
            HelpText = "Used to incdicate startup, so that, ssh-agent can be started before adding keys.")]
        public bool Startup { get; set; }

        [Option('a', "add", Required = false,
            HelpText = "Adds key to ssh-agent. Useful for startup configuration.")]
        public bool Add { get; set; }

        [ValueList(typeof(List<string>))]
        public IList<string> Others { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
