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

        static void Main()
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

            String[] arguments = Environment.GetCommandLineArgs();

            if (arguments.Length > 1)
            {
                string[] switches = { };
                if (arguments.Contains("/register-startup"))
                {
                    if (arguments.Length > 2)
                    {
                        List<string> except = new List<string>();
                        except.Add("/register-startup");
                        List<string> tempList = arguments.ToList();
                        tempList.Add("/startup");
                        arguments = tempList.ToArray();
                        manageStartup(arguments.Skip(1).Except(except).ToArray());
                    } else
                    {
                        manageStartup(switches);
                    }
                } else if (arguments.Contains("/unregister-startup"))
                {
                    manageStartup(switches, true);
                } else if (arguments.Contains("/startup") && arguments.Contains("/add"))
                {
                    List<string> except = new List<string>();
                    except.Add("/startup");
                    except.Add("/add");
                    runAgent();
                    addKeys(arguments.Skip(1).Except(except).ToArray(), true);
                } else if (arguments.Contains("/add")) {
                    List<string> except = new List<string>();
                    except.Add("/add");
                    addKeys(arguments.Skip(1).Except(except).ToArray());
                } else
                {
                    Console.Error.WriteLine("Usage: ssh-agent-helper.exe <switch>");
                    Console.Error.WriteLine("");
                    Console.Error.WriteLine("Possible switches:");
                    Console.Error.WriteLine("--------------------------------------------");
                    Console.Error.WriteLine("/register-startup \"parameters for startup\":   " +
                                        "Register this program to run at Windows Startup." +
                                        " Parameters for");
                    Console.Error.WriteLine("                                              " +
                                        "startup are optional. E.g.: ");
                    Console.Error.WriteLine("                                              " +
                                        "ssh-agent-helper.exe /register-startup /add %USERPROFILE%\\.ssh\\id_rsa");
                    Console.Error.WriteLine("");
                    Console.Error.WriteLine("/unregister-startup :                         " +
                                        "Disable run at Windows Startup behaviour.");
                    Console.Error.WriteLine("/add \"path\" :                                 " +
                                        "Start the ssh-agent and add the key located at \"path\" to it");
                    Console.Error.WriteLine("/? :                                          Print this information.");
                    Environment.Exit(1);
                }
                Environment.Exit(0);
            }

            runAgent();

        }

        static void runAgent()
        {
            try
            {
                Process existingProcess = Process.GetProcessById(Convert.ToInt32(AgentPID));

                if (!existingProcess.Responding)
                {
                    throw new Exception("The previous ssh-agent is not responding");
                }

                Console.Error.WriteLine("Another ssh-agent (PID: " + AgentPID + ") is already running healthily");
            } catch (Exception exception)
            {
                string SSHAgentPath = findProgram("ssh-agent");
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

        static string findProgram(string name)
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

        static void manageStartup(string[] args, bool remove = false)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey
                    (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (!remove)
            {
                string parameter = (new Uri(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).LocalPath;
                if (args.Length > 0)
                {

                    string argument = "";
                    for (int i = 0; i < args.Length; i++) {
                        argument += " \"" + args[i] + "\"";
                    }
                    parameter = "\"" + parameter + "\"" + argument;
                }
                registryKey.SetValue("SSH Agent Helper", parameter);

                Console.WriteLine("SSH Agent Helper has been register to run at Startup with these parameters: " +
                                    String.Join(" ", parameter));
            } else if (registryKey.GetValue("SSH Agent Helper") != null)
            {
                registryKey.DeleteValue("SSH Agent Helper");
                Console.WriteLine("SSH Agent Helper registery for Startup has been removed.");
            }
            else
            {
                Console.WriteLine("SSH Agent Helper registery for has already been removed.");
            }
        }

        static void addKeys(string[] paths, bool customENV = false)
        {
            string SSHAddPath = findProgram("ssh-add");
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
}
