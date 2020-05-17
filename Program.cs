using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using Papyrus.Automation;
using Papyrus.Networking;

namespace Papyrus {
	class Program {
		// Config
		private static string _configPath = "configuration.json";
		private static ProcessManager bds;
		private static BackupManager _backupManager;
		private static bool _isAlive = true;
		private static Version _localVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

		// Main
		private static string[] _toIgnore;
		private static ProcessStartInfo serverStartInfo;

		// Timers
		private static Timer keepAliveTimer;
		private static Timer backupIntervalTimer;
		private static Timer backupNotificationTimer;

		public static RunConfiguration RunConfig;
		public delegate void InputStreamHandler(string text);

		static void Main() {
			Console.WriteLine("[Papyrus] Papyrus Automation Tool v{0} build {1}\n\tby clarkx86 & DeepBlue\n\tforked by ENX\n",
				UpdateChecker.ParseVersion(_localVersion, VersionFormatting.MAJOR_MINOR_REVISION), _localVersion.Build);

			if (File.Exists(_configPath)) {
				try {
					Setup();
				} catch (Exception ex) {
					HandleCrash(ex);
				}

				Task main = Task.Run(() => {
					while (_isAlive) {
						ReadInput(Console.ReadLine());
					}
				});

				main.Wait();
			} else {
				CreateConfig();
			}
		}

		private static void CreateConfig() {
			Console.WriteLine("[Papyrus] No previous configuration file found. Creating one...");

			using (StreamWriter writer = new StreamWriter(_configPath)) {
				writer.Write(JsonConvert.SerializeObject(new RunConfiguration() {
					BdsPath = "",
					BdsFileName = "",
					WorldName = "Bedrock level",
					PapyrusBinPath = "",
					PapyrusGlobalArgs = "-w ${WORLD_PATH} -o ${OUTPUT_PATH} --htmlfile index.html -f webp -q -1 --deleteexistingupdatefolder",
					PapyrusTasks = new string[] {
							 "--dim 0",
							 "--dim 1",
							 "--dim 2"
						},
					PapyrusOutputPath = "",
					ArchivePath = "./backups/",
					BackupsToKeep = 10,
					BackupOnStartup = true,
					EnableKeepAlive = false,
					EnableBackups = true,
					EnableRenders = true,
					BackupInterval = 60,
					RenderInterval = 180,
					PreExec = "",
					PostExec = "",
					QuietMode = false,
					HideStdout = true,
					BusyCommands = true,
					StopBeforeBackup = (Environment.OSVersion.Platform != PlatformID.Win32NT ? false : true), // Should be reverted to "false" by default when 1.16 releases
					NotifyBeforeStop = 60,
					CheckForUpdates = true,
				}, Formatting.Indented));
			}

			Console.WriteLine("[Papyrus] Done! Please edit the \"{0}\" file and restart this application.", _configPath);
		}

		private static void Setup() {
			Console.WriteLine("[Papyrus] Loading Setup...");

			LoadConfiguration(_configPath);

			string _filePath = Path.Join(RunConfig.BdsPath,
					string.IsNullOrEmpty(RunConfig.BdsFileName) ? (Environment.OSVersion.Platform == PlatformID.Unix ? "bedrock_server" : "bedrock_server.exe") : RunConfig.BdsFileName);

			if (!File.Exists(_filePath)) {
				Console.WriteLine("[Papyrus] ERROR: Unable to find bds exec at \"{0}\".", _filePath);
				Environment.Exit(0);
			}

			serverStartInfo = new ProcessStartInfo() {
				FileName = _filePath,
				WorkingDirectory = RunConfig.BdsPath
			};

			if (Environment.OSVersion.Platform == PlatformID.Unix) {
				serverStartInfo.EnvironmentVariables.Add("LD_LIBRARY_PATH", RunConfig.BdsPath); // Set environment variable for linux-based systems
			}

			_toIgnore = new string[] {
				"^(" + RunConfig.WorldName.Trim() + @"\/\d+\.\w+\:\d+)",
				"^(Saving...)",
				"^(A previous save has not been completed.)",
				"^(Data saved. Files are now ready to be copied.)",
				"^(Changes to the level are resumed.)"
			};

			InitBDS();
		}

		private static void InitBDS() {
			Console.WriteLine("[Papyrus] Initiating BDS...");

			bds = new ProcessManager(serverStartInfo, _toIgnore);
			bds.Start();

			Console.WriteLine("[Papyrus] Server started, waiting for completion...");
			bds.WaitForStart();
			Console.WriteLine("[Papyrus] Server running.");

			if (RunConfig.EnableKeepAlive) {
				keepAliveTimer = new Timer(10000) {
					AutoReset = true
				};
				keepAliveTimer.Elapsed += (object sender, ElapsedEventArgs e) => {
					if (!bds.IsRunning) {
						Console.WriteLine("[Papyrus] KeepAlive enabled, server not running, starting...");

						StopBDS();
						Setup();
					}
				};
				keepAliveTimer.Start();
			}

			InitManagers();
		}

		private static void InitManagers() {
			Console.WriteLine("[Papyrus] Initiating Managers...");

			_backupManager = new BackupManager(bds, RunConfig, keepAliveTimer);

			if (RunConfig.BackupOnStartup) {
				Console.WriteLine("[Papyrus] Creating initial world backup...");
				_backupManager.CreateWorldBackup(true, false); // If "StopBeforeBackup" is set to "true" this will also automatically start the server when it's done
			}

			if (RunConfig.EnableBackups) {
				backupIntervalTimer = new Timer(RunConfig.BackupInterval * 60000) {
					AutoReset = true
				};
				backupIntervalTimer.Elapsed += (object sender, ElapsedEventArgs e) => {
					InvokeBackup();
				};
				backupIntervalTimer.Start();

				if (RunConfig.StopBeforeBackup) {
					backupNotificationTimer = new Timer((RunConfig.BackupInterval * 60000) - Math.Clamp(RunConfig.NotifyBeforeStop * 1000, 0, RunConfig.BackupInterval * 60000)) {
						AutoReset = false
					};
					backupNotificationTimer.Elapsed += (object sender, ElapsedEventArgs e) => {
						bds.SendTellraw(string.Format("[Papyrus] Restarting server in {0}.", RunConfig.NotifyBeforeStop));
					};
					backupNotificationTimer.Start();
				}
			}


			Console.WriteLine("[Papyrus] Initiation complete.");
		}

		private static void StopBDS() {
			keepAliveTimer?.Stop();
			keepAliveTimer?.Close();
			backupIntervalTimer?.Stop();
			backupIntervalTimer?.Close();
			backupNotificationTimer?.Stop();
			backupNotificationTimer?.Close();

			if (bds.IsRunning) {
				bds.SendInput("stop");
				bds.WaitForExit();
				bds.Close();
			}
		}

		private static void ReadInput(string input) {
			if (RunConfig.BusyCommands || !_backupManager.Processing) {
				#region CUSTOM COMMANDS
				MatchCollection cmd = Regex.Matches(input.ToLower().Trim(), @"(\S+)");

				if (cmd.Count > 0) {
					bool result = false;

					switch (cmd[0].Captures[0].Value) {
						case "force":
							Console.WriteLine("[Papyrus] Force command detected...");

							if (cmd.Count >= 3) {
								switch (cmd[1].Captures[0].Value) {
									case "start":
										switch (cmd[2].Captures[0].Value) {
											case "backup":
												InvokeBackup();

												result = true;

												break;
										}
										break;
								}
							}

							break;

						case "stop":
							Console.WriteLine("[Papyrus] Stopping server...");

							StopBDS();

							Console.WriteLine("[Papyrus] Server stopped.");

							if (RunConfig.EnableKeepAlive) {
								Console.WriteLine("[Papyrus] KeepAlive enabled, server will restart shortly.");

								keepAliveTimer?.Start();
							} else {
								Console.WriteLine("[Papyrus] KeepAlive disabled, papyrus will exit now.");

								_isAlive = false;
							}

							result = true;

							break;

						case "quit":
							Console.WriteLine("[Papyrus] Quitting server...");

							StopBDS();

							Console.WriteLine("[Papyrus] Server stopped, papyrus will exit now.");

							_isAlive = false;

							result = true;

							break;


						default:
							Console.WriteLine("[Papyrus] Custom command detected, redirecting to main process...");

							bds.SendInput(input);

							result = true;

							break;
					}

					if (!result) {
						Console.WriteLine("Could not execute command \"{0}\".", input);
					}
				}
				#endregion
			} else {
				Console.WriteLine("Could not execute command \"{0}\". Please wait until all tasks have finished or enable \"BusyCommands\" in your \"{1}\".", input, _configPath);
			}
		}

		public static void InvokeBackup() {
			if (!_backupManager.Processing) {
				_backupManager.CreateWorldBackup(false, true);
			} else {
				if (!RunConfig.QuietMode) {
					Console.WriteLine("A backup task is still running.");
				}
			}
		}

		private static void LoadConfiguration(string configPath) {
			Console.WriteLine("[Papyrus] Loading configuration...", configPath);

			RunConfiguration runConfig;
			using (StreamReader reader = new StreamReader(Path.Join(Directory.GetCurrentDirectory(), _configPath))) {
				runConfig = JsonConvert.DeserializeObject<RunConfiguration>(reader.ReadToEnd());
			}

			Console.WriteLine("[Papyrus] Configuration loaded.");

			// ONLY FOR 1.14, should be fixed in the next BDS build
			if (!runConfig.StopBeforeBackup && Environment.OSVersion.Platform == PlatformID.Win32NT) {
				Console.WriteLine("[Papyrus]: NOTICE Hot-backups are currently not supported on Windows. Please enable \"StopBeforeBackup\" in the \"{0}\" instead.", _configPath);
				Environment.Exit(0);
			}

			RunConfig = runConfig;
		}

		private static void HandleCrash(Exception ex) {
			Console.WriteLine("[Papyrus] Exception found, exiting. {0}", ex.Message);

			StopBDS();

			Environment.Exit(0);
		}
	}
}
