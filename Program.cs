using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
            String[] arguments = Environment.GetCommandLineArgs();

            if(arguments.Length > 1)
            {
                string[] args = {};
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
                        manageStartup(args);
                    }
                } else if (arguments.Contains("/unregister-startup"))
                {
                    manageStartup(args, true);
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
                    Console.WriteLine("Usage: ssh-agent-helper.exe <switch>");
                    Console.WriteLine("");
                    Console.WriteLine("Possible switches:");
                    Console.WriteLine("--------------------------------------------");
                    Console.WriteLine("/register-startup \"parameters for startup\":   " +
                                        "Register this program to run at Windows Startup." +
                                        " Parameters for");
                    Console.WriteLine("                                              " +
                                        "startup are optional. E.g.: ");
                    Console.WriteLine("                                              " +
                                        "ssh-agent-helper.exe /register-startup /add %USERPROFILE%\\.ssh\\id_rsa");
                    Console.WriteLine("");
                    Console.WriteLine("/unregister-startup :                         " +
                                        "Disable run at Windows Startup behaviour.");
                    Console.WriteLine("/add \"path\" :                                 " +
                                        "Start the ssh-agent and add the key located at \"path\" to it");
                    Console.WriteLine("/? :                                          Print this information.");
                    Environment.Exit(1);
                }
                Environment.Exit(0);
            }

            runAgent();
            
        }

        static void runAgent()
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
                while (!SSHAgent.StandardOutput.EndOfStream)
                {
                    var line = SSHAgent.StandardOutput.ReadLine();

                    string[] splits = line.Split(';');
                    string[] command = splits[0].Split('=');

                    if (command[0] == SSH_AUTH_SOCK && command.Length > 1)
                    {
                        AgentSock = command[1];
                        Environment.SetEnvironmentVariable(SSH_AUTH_SOCK, command[1], EnvironmentVariableTarget.User);
                        Console.WriteLine("set " + SSH_AUTH_SOCK + "=" + command[1]);
                    }
                    else if (command[0] == SSH_AGENT_PID && command.Length > 1)
                    {
                        AgentPID = command[1];
                        Environment.SetEnvironmentVariable(SSH_AGENT_PID, command[1], EnvironmentVariableTarget.User);
                        Console.WriteLine("set " + SSH_AGENT_PID + "=" + command[1]);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Environment.Exit(1);
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
                if(args.Length > 0)
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
}
