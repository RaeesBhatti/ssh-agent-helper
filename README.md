# SSH Agent Helper
It sets up your environment (CMD, PowerShell) for ssh-agent seamlessly

## What does it fix?
In a world before this program, if you wanted to use `ssh` or `git` from Command Prompt or PowerShell,you either
had to use startup scripts for the terminals or set the SSH agent variables manually on each instance
of terminals. This program just simply make `ssh-agent` universally available to programs launched from
Windows environment. Which means that you can use your precious `ssh` and `git` right from Command Prompt
or PowerShell.

## Usage
Invoking the program without any parameters (`ssh-agent-helper.exe`) will result in running `ssh-agent.exe`
and setting `SSH_AUTH_SOCK` and `SSH_AGENT_PID` as current user's environment variables. This will allow
`ssh-add.exe`, `ssh.exe` or any other programs that consume `ssh-agent.exe` to conect to it without any further
configuration. But you must restart Command Prompt or PowerShell after this to take effect.

It can also be configured to run `ssh-agent.exe` at the time of Windows login and add ssh keys to it. You can use
`--register-startup` or `-r` switch for that.

E.g. `ssh-agent-helper.exe -r -a %USERPROFILE%\.ssh\id_rsa`. And you can use
`--unregister-startup` or `-u` to disable run at Windows login.

You can get the usage by invoking the program with `--help` switch.

## How can I contribute?
Try to use use and report bugs if you face any. Suggest any ideas you think can make this project better.

## License
This project is covered by MIT License and the LICENSE file is included with the source code.
