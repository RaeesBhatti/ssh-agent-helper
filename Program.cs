using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;

namespace SSH_Agent_Helper
{
    class Program
    {
        static string SSH_AUTH_SOCK = "SSH_AUTH_SOCK";
        static string SSH_AGENT_PID = "SSH_AGENT_PID";
        static void Main()
        {
            String[] arguments = Environment.GetCommandLineArgs();

            if(arguments.Length > 1)
            {
                if (arguments.Contains("/register-startup"))
                {
                    if (arguments.Length > 2)
                    {
                        manageStartup(arguments.Last());
                    } else
                    {
                        manageStartup("");
                    }
                } else if (arguments.Contains("/unregister-startup"))
                {
                    manageStartup("", true);
                } else if (arguments.Contains("/add")) {
                    Console.WriteLine(arguments.Last());
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
                                        "ssh-agent-helper.exe /register-startup \"/add %USERPROFILE%\\.ssh\\id_rsa\"");
                    Console.WriteLine("");
                    Console.WriteLine("/unregister-startup :                         " +
                                        "Disable run at Windows Startup behaviour.");
                    Console.WriteLine("/add \"path\" :                                 " +
                                        "Start the ssh-agent and add the key located at \"path\" to it");
                    Console.WriteLine("/? :                                          Print this information.");
                }
                Environment.Exit(1);
            }


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
                    
                    if(command[0] == SSH_AUTH_SOCK && command.Length > 1)
                    {
                        Environment.SetEnvironmentVariable(SSH_AUTH_SOCK, command[1], EnvironmentVariableTarget.User);
                        Console.WriteLine("set "+ SSH_AUTH_SOCK + "=" + command[1]);
                    } else if(command[0] == SSH_AGENT_PID && command.Length > 1)
                    {
                        Environment.SetEnvironmentVariable(SSH_AGENT_PID, command[1], EnvironmentVariableTarget.User);
                        Console.WriteLine("set "+ SSH_AGENT_PID + "=" + command[1]);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(1);
            }
        }

        static string findProgram(string name)
        {
            Process Where = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "C:\\Windows\\System32\\where.exe",
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
                Console.WriteLine(e.Message);
                Environment.Exit(1);
                return "";
            }
        }

        static void manageStartup(string arguments, bool remove = false)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey
                    ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (!remove)
            {
                string parameter = (new Uri(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).LocalPath;
                if(arguments.Length > 0)
                {
                    parameter = "\"" + parameter + "\" \"" + arguments + "\"";
                }
                registryKey.SetValue("SSH Agent Helper", parameter);
            }
            else
            {
                registryKey.DeleteValue("SSH Agent Helper");
            }
        }

        static void addKeys(string[] paths)
        {
            string SSHAddPath = findProgram("ssh-add");
            Process SSHAdd = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = SSHAddPath,
                    UseShellExecute = false,
                    Arguments = String.Join(" ", paths),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            try
            {
                SSHAdd.Start();

                SSHAdd.OutputDataReceived += processBuffer;
                SSHAdd.ErrorDataReceived += processBuffer;

                SSHAdd.BeginOutputReadLine();
                SSHAdd.BeginErrorReadLine();
                SSHAdd.WaitForExit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(1);
            }
        }

        static void processBuffer(object sender, DataReceivedEventArgs e)
        {
            Console.Write(e.Data);
        }
    }
}
