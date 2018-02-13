# SSH Agent Helper
It sets up your environment (CMD, PowerShell) for ssh-agent seamlessly

## What does it fix?
In a world before this program, if you wanted to use `ssh` (or `git` authenticated via ssh keys) from Command Prompt
or PowerShell, you either had to use startup scripts for the terminals or set the `ssh-agent` variables manually
on each instance. This program just simply make `ssh-agent` universally available to programs launched from
Windows environment. Which means that you can use your precious `ssh` and `git` right from Command Prompt, PowerShell,
Bash or any other.

## Usage
Download the binary from [latest release](https://github.com/raeesbhatti/ssh-agent-helper/releases/latest).
* `ssh-agent-helper.exe`: Invoking the program without any parameters will result in running `ssh-agent`
and setting `SSH_AUTH_SOCK` and `SSH_AGENT_PID` as current user's environment variables. This will allow
`ssh-add`, `ssh` or any other programs that consume `ssh-agent` to conect to it without any further
configuration. But you must restart Command Prompt or PowerShell after this to take effect.
* `ssh-agent-helper.exe` with `--register-startup` or `-r` parameter configures `ssh-agent` to run at the time of Windows startup.
* `ssh-agent-helper.exe` with `-r -a (path for (multiple) id_rsa here)` configures `ssh-agent` to run at Windows startup and add specified SSH keys to the agent. E.g.`ssh-agent-helper.exe -r -a %USERPROFILE%\.ssh\id_rsa`
* `ssh-agent-helper.exe` with `--unregister-startup` or `-u` will disable run at Windows startup functionality.

You can get the usage information by invoking the program with `--help` switch.

## How can I contribute?
Try to use use and report bugs if you face any. Suggest any ideas you think can make this project better.

## License
This project is covered by MIT License and the LICENSE file is included with the source code.
