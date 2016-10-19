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
        static void Main(string[] args)
        {
            string SSHAgentPath = findSSHAgent();
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
                    
                    if(command[0] == SSH_AUTH_SOCK)
                    {
                        Environment.SetEnvironmentVariable(SSH_AUTH_SOCK, command[1], EnvironmentVariableTarget.User);
                        Console.WriteLine("set "+ SSH_AUTH_SOCK + "=" + command[1]);
                    } else if(command[0] == SSH_AGENT_PID)
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

        static string findSSHAgent()
        {
            Process Where = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "C:\\Windows\\System32\\where.exe",
                    Arguments = "ssh-agent",
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
                throw new Exception("ssh-agent.exe was not found in path");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(1);
                return "";
            }
        }
    }
}
