<html>
    <body>
        <div align="center">
            <img alt="papyrus.automation" src="https://papyrus.clarkx86.com/wp-content/uploads/sites/2/2020/04/papyrus-automation_logo-3.png">
            <br>
            <a href="https://travis-ci.com/github/clarkx86/papyrus-automation"><img alt="Travis-CI" src="https://travis-ci.com/clarkx86/papyrus-automation.svg?branch=experimental"></a>
            <a href="https://discord.gg/J2sBaXa"><img alt="Discord" src="https://img.shields.io/discord/569841820092203011?label=chat&logo=discord&logoColor=white"></a>
            <br><br>
        </div>
    </body>
</html>

This is a **Minecraft: Bedrock Server** (BDS) backup **automation** tool primarily made to create incremental backups of your world, all while the server is running without any server-downtime using BDS's `save hold | query | resume` commands.

This fork has removed the rendering portion of the code, and will focus in automation only.

## Table of contents
- [Table of contents](#table-of-contents)
- [How does it work?](#how-does-it-work)
- [Get started](#get-started)
  - [Prerequisites](#prerequisites)
  - [Installing and configuring](#installing-and-configuring)
  - [Incremental backups](#incremental-backups)
- [Configuration overview](#configuration-overview)
- [Commands](#commands)
- [Disclaimer](#disclaimer-read-before-using)

## How does it work?
When this tool gets executed it creates an initial full backup of your world. Then it will launch your BDS instance as a child-process and redirects its stdout and stdin. It will then listen for certain "events" from BDS's stdout (like "Server started" messages, etc.) to determin it's current status. On an interval it will execute the `save hold | query | resume` commands and copies the required files to a temporary backup folder and compresses the world as a `.zip`-archive. It will then call PapyrusCS with user-individual arguments to render the world using the temporary world-backup directory.

## Get started
### Prerequisites
Before starting to set up this tool it is recommended to already have a [Bedrock Dedicated Server](https://www.minecraft.net/de-de/download/server/bedrock/) configured.
If you choose not to go with the self-contained release of this tool, you must have the latest [.NET Core runtime](https://docs.microsoft.com/en-us/dotnet/core/install/linux-package-manager-ubuntu-1804#install-the-net-core-runtime) installed aswell.

### Installing and configuring
First of all grab the latest pre-compiled binary from the release-tab or by [**clicking here**](https://github.com/clarkx86/papyrus-automation/releases/latest). You will find two releases: A larger self-contained archive which comes bundled with the .NET Core runtime and a smaller archive which depends on you having the .NET Core runtime already installed on your system.
Download and extract the archive and `cd` into the directory with the extracted files.

You may need to give yourself execution permission with:
```
chmod +x ./papyrus-automation
```
Now run this tool for the first time by typing:
```
./papyrus-automation
```
This will generate a new `configuration.json` in the same directory. Edit this file and specify at least all required parameters (see below for an overview).

Now you can restart the tool one more time with the same command as above. It should now spawn the BDS instance for you and execute renders on the specified interval (do not start the server manually).
Once the server has launched through this tool you will be able to use the server console and use it's commands just like you normally would.

### Keep Alive
This tool can automatically start your server in case it stops or crashes. Set the option `EnableKeepAlive` to `true` in the configuration file to enable it.

### Incremental backups
To create incremental world backups make sure the `CreateBackups` option is set to `true`. Backups will be stored in the directory specified by `ArchivePath`. This tool will automatically delete the oldest backups in that directory according to the threshold specified by the `BackupsToKeep` option to prevent eventually running out of disk space.

## Configuration overview
When you run this tool for the first time, it will generate a `configuration.json` and terminate. Before restarting the tool, edit this file to your needs. Here is a quick overview:
```
KEY               VALUE               ABOUT
----------------------------------------------------------
BdsPath           String  (!)         Path to the BDS root directory

BdsFileName       String              If not specified, bedrock_server or 
                                      bedrock_server.exe will be used

WorldName         String  (!)         Name of the world located in the servers
                                      /worlds/ directory (specify merely the name and
                                      not the full path)

PapyrusBinPath    String              Path to the papyrus executable (inclusive)

PapyrusGlobalArgs String              Global arguments that are present for each
                                      rendering task specified in the "PapyrusArgs"-
                                      array
                                      IMPORTANT: Do not change the already provided
                                      --world and --ouput arguments

PapyrusTasks      String [Array]      An array of additional arguments for papyrus,
                                      where each array entry executes another
                                      PapyrusCS process after the previous one has
                                      finished (e.g. for rendering of multiple
                                      dimensions)

PapyrusOutputPath String              Output path for the rendered papyrus map

ArchivePath       String              Path where world-backup archives should be
                                      created

BackupsToKeep     Integer             Amount of backups to keep in the "ArchivePath"-
                                      directory, old backups automatically get deleted

BackupOnStartup   Boolean (!)         Whether to create a full backup of the specified
                                      world before starting the BDS process
                                      IMPORTANT: It is highly encouraged to leave
                                      this setting on "true"

EnableBackups     Boolean (!)         Whether to create world-backups as .zip-archives

BackupInterval    Double              Time in minutes to take a backup and create a
                                      .zip-archive

PreExec           String              An arbitrary command that gets executed before
                                      each backup starts

PostExec          String              An arbitrary command that gets executed after
                                      each has finished

QuietMode         Boolean (!)         Suppress notifying players in-game that papyrus
                                      is creating a backup and render

HideStdout        Boolean (!)         Whether to hide the console output generated by
                                      the PapyrusCS rendering process, setting this
                                      to "true" may help debug your configuration but
                                      will result in a more verbose output

BusyCommands      Boolean (!)         Allow executing BDS commands while the tool is
                                      taking backups

StopBeforeBackup  Boolean (!)         Whether to stop, take a backup and then restart
                                      the server instead of taking a hot-backup

NotifyBeforeStop  Integer             Time in seconds before stopping the server for a
                                      backup, players on the server will be
                                      notified with a chat message

* values marked with (!) are required, non-required values should be provided depending on your specific configuration
```
You can find an example configuration [here](https://github.com/kmorales13/papyrus-automation/blob/master/examples/basic_example.json).

## Commands
papyrus.automation also provides a few new, and overloads some existing commands that you can execute to force-invoke backup- or rendering tasks and schedule server shutdowns.
```
COMMAND                               ABOUT
----------------------------------------------------------
force start backup                    Forces taking a (hot-)backup (according to your
                                      "StopBeforeBackup" setting)

force start render                    Forces PapyrusCS to execute and render your
                                      world

stop <time in seconds>                Schedules a server shutdown and notifies players
                                      in-game

reload papyrus                        Reloads the previously specified (or default)
                                      configuration file
```

## Disclaimer! Read before using!
Use this tool at **your own risk**! When using this software you agree to not hold us liable for any corrupted save data or deleted files. Make sure to configure everything correctly and thoroughly!

If you find any bugs, please report them on the issue tracker here on GitHub.