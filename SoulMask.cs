﻿using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Linq;
using System.Collections.Generic;

namespace WindowsGSM.Plugins
{
    public class SoulMask : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.SoulMask", // WindowsGSM.XXXX
            author = "Illidan",
            description = "WindowsGSM plugin for supporting SoulMask Dedicated Server",
            version = "1.0 ",
            url = "https://github.com/JTNeXuS2/SoulMask.cs", // Github repository link (Best practice)
            color = "#8802db" // Color Hex
        };

        // - Standard Constructor and properties
        public SoulMask(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "3017310"; /* taken via https://steamdb.info/app/3017310/info/ */

        // - Game server Fixed variables
        public override string StartPath => @"WS\Binaries\Win64\WSServer-Win64-Shipping.exe"; // Game server start path
        public string FullName = "SoulMask Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 4; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        public static string ConfigServerName = RandomNumberGenerator.Generate12DigitRandomNumber();

        // - Game server default values
        public string ServerName = "wgsm_SoulMask_dedicated";
        public string Defaultmap = "Level01_Main"; // Original (MapName)
        public string Maxplayers = "50"; // WGSM reads this as string but originally it is number or int (MaxPlayers)
        public string Port = "20700"; // WGSM reads this as string but originally it is number or int
        public string QueryPort = "20701"; // WGSM reads this as string but originally it is number or int (SteamQueryPort)
        public string EchoPort;
        public string RconPort;
        public string Additional => GetAdditional();

        private string GetAdditional()
        {
            string EchoPort = (int.Parse(_serverData.ServerQueryPort) + 1).ToString();
            string RconPort = (int.Parse(_serverData.ServerQueryPort) + 2).ToString();
            return $" -log -UTF8Output -MultiHome=0.0.0.0 -serverid=0 -rconaddr=0.0.0.0 -rconport=\"{RconPort}\" -EchoPort=\"{EchoPort}\" -forcepassthrough -initbackup -saving=600 -backupinterval=900 -adminpsw=adminpass -rconpsw=rconpass -serverpm=2";

        }

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            createBaseConfig();
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);

            // Prepare start parameter
            var param = new StringBuilder();
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMap) ? string.Empty : $"{_serverData.ServerMap} -server %*");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -port={_serverData.ServerPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerQueryPort) ? string.Empty : $" -QueryPort={_serverData.ServerQueryPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerName) ? string.Empty : $" -SteamServerName=\"{_serverData.ServerName}\"");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? string.Empty : $" -MaxPlayers={_serverData.ServerMaxPlayer}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerGSLT) ? string.Empty : $" -PrivateServerPassword={_serverData.ServerGSLT}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param.ToString(),
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
				// Модификация для вызова batch перед стартом сервера
                //await RunBatchScript();
				var scriptPath = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID), "OnStart.bat");
				await RunExternalScriptAsync(scriptPath);
				// END
                p.Start();
                if (AllowsEmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }

                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }
		// END
///////////////////////////////////////////////////////
    public async Task RunExternalScriptAsync(string scriptPath)
    {
        using (Process batch = new Process())
        {
            batch.StartInfo.FileName = "cmd.exe";
            batch.StartInfo.Arguments = $"/c \"{scriptPath}\""; // /c flag runs the command and terminates
			batch.StartInfo.WorkingDirectory = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID));
            //process.StartInfo.UseShellExecute = false;
            if (AllowsEmbedConsole)
            {
                batch.StartInfo.CreateNoWindow = true;
                batch.StartInfo.UseShellExecute = false;
                batch.StartInfo.RedirectStandardInput = true;
                batch.StartInfo.RedirectStandardOutput = true;
                batch.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                batch.OutputDataReceived += serverConsole.AddOutput;
                batch.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start the process asynchronously
            batch.Start();
            if (AllowsEmbedConsole)
            {
                batch.BeginOutputReadLine();
                batch.BeginErrorReadLine();
            }
            // Wait asynchronously for the script to complete
            await WaitForExitAsync(batch);
            // Call the closeProcess function with the process as an argument
            //closeProcess?.Invoke(process);
            // Script completed
            Console.WriteLine("Script exited");
        }
    }
    static Task WaitForExitAsync(Process batch)
    {
        var tcs = new TaskCompletionSource<object>();

        batch.EnableRaisingEvents = true;
        batch.Exited += (sender, e) => tcs.TrySetResult(null);

        if (batch.HasExited)
        {
            tcs.TrySetResult(null);
        }

        return tcs.Task;
    }
///////////////////////////////////////////////////////
        // - Stop server function
        public async Task Stop(Process p)
        {
			await Task.Run(() =>
			{
				 Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
				 Functions.ServerConsole.SendWaitToMainWindow("^c");
			});
			await Task.Delay(20000);
        }

        // - Update server function
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;
            await Task.Run(() => { p.WaitForExit(); });
            return p;
        }

        public void createBaseConfig()
        {
            string baseConfig = $"[BaseServerConfig]\r\nServerName={_serverData.ServerName}\r\nServerPassword=\r\nQueueThreshold=24\r\nServerFightModeType=1\r\nIsCanSelfDamage=1\r\nIsCanFriendDamage=1\r\nClearSeverTime=\r\nUseSteamSocket=1\r\nPort={_serverData.ServerPort}\r\nBeaconPort=27049\r\nShutDownServicePort=27050\r\nQueryPort={_serverData.ServerQueryPort}\r\nSaveWorldInterval=300\r\nGMOverlapRatio=1\r\nIsUnLockAllTalentAndRecipe=0\r\nGMBagInitGirdNum=40\r\nGreenHand=1\r\nCharacterInitItem=\r\nGMDeathDropMode=1\r\nGMDeathInventoryLifeSpan=1800\r\nCorpsePickAuthority=2\r\nGMCanDropItem=1\r\nGMCanDiscardItem=1\r\nGMDiscardBoxLifeSpan=300\r\nGMRebirthBaseCD=10\r\nGMRebirthExtraCD=1\r\nGMPenaltiesMaxNum=5\r\nGMPenaltiesCD=600\r\nConstructEnableRot=1\r\nGMAttackCityCdRatio=1\r\nOpenAllHouseFlag=0\r\nIsCanChat=1\r\nIsShowBlood=1\r\nSensitiveWords=1\r\nHealthDyingState=1\r\nUseACE=1\r\nServerAdminAccounts=\r\nIsShowGmTitle=1\r\nPlayerHotDefAddRate=1\r\nPlayerIceDefAddRate=1\r\nHeadNameDisplayDist_Team=200\r\nHeadNameDisplayDist_Enemy=20\r\nPlayerDeathAvatarItemDurableRate=0\r\nPlayerDeatShortcutItemDurableRate=0\r\nGMCraftTimeRate=1\r\nPlayerAddExpRate=1\r\nPlayerKillAddExpRate=1\r\nPlayerFarmAddExpRate=1\r\nPlayerCraftAddExpRate=1\r\nMoveSpeedRate=1\r\nJumpRate=1\r\nPlayerLandedDamageRate=1\r\nPlayerMaxHealthRate=1\r\nHealthRecoverRate=1\r\nPlayerMaxStaminaRate=1\r\nStaminaRecoverRate=1\r\nPlayerStaminaCostRate=1\r\nPlayerMaxHungerRate=1\r\nGMHungerDecRatio=1\r\nGMBodyHungerAddRate=1\r\nMaxBodyWaterRate=1\r\nGMWaterDecRatio=1\r\nGMBodyWaterAddRate=1\r\nMaxBreathRate=1\r\nBreathRecoverRate=1\r\nPlayerBreathCostRate=1\r\nGMPlayerHealthRate=1\r\nGMFoodDragDurationRate=1\r\nNpcRespawnRatio=1\r\nAnimalBodyStayTime=300\r\nHumanBodyStayTime=10\r\nGMNPCLootableItemRatio=1\r\nNpcSpawnLevelRatio=1\r\nWildNPCDamageRate=1\r\nWildNPCHealthRate=1\r\nWildNPCSpeedRate=1\r\nCityNPCLevelRate=1\r\nCityNPCDamageRate=1\r\nCityNPCHealthRate=1\r\nCityNPCSpeedRate=1\r\nCityNPCNumRate=1\r\nNpcDisplayDistance=50\r\nGMInventoryGainRate=1\r\nGMCityATKNPCLootItemRatio=1\r\nGMMaxHouseFlagNumber=1\r\nGMSetGJConstructMaxNumRatio=1\r\nGMHFTrapMaxNum=0\r\nGMHFTurretMaxNum=0\r\nGMConstructDefenseRatio=1\r\nGMTrapDefenseRatio=1\r\nGMTurretDefenseRatio=1\r\nGMTrapDamageRatio=1\r\nGMTurretDamageRatio=1\r\nGMConstructMaxHealthRatio=1\r\nGMConstructReturnHPRatio=1\r\nGMHouseFlagRepairHealthRatio=1\r\nGMTTC_Oil_Rate=1\r\nGMWaterCollecter_Rate=1\r\nGMTTC_Ore_Rate=1\r\nGMTTC_Fish_Rate=1\r\nCHFDamagedByPlayer=1\r\nCHFDamagedByVehicle=1\r\nCHFDamagedByNpc=1\r\nGMHouseFlagExcitantTime=1\r\nGMMaxRetrieveProductsRate=1\r\nGMTreeGainRate=1\r\nGMBushGainRate=1\r\nGMOreGainRate=1\r\nGMCropVegetableReapRatio=1\r\nGMFleshGainRate=1\r\nGMCropVegetableGrowRatio=1\r\nGMMeleeNpcDamageRatio=1\r\nGMRangedNpcDamageRatio=1\r\nGMMeleePlayerDamageRatio=1\r\nGMRangedPlayerDamageRatio=1\r\nGMMeleeConstructDamageRatio=1\r\nGMRangedConstructDamageRatio=1\r\nGMToolDamageRate=1\r\nGMDurabilityCostRatio=1\r\nGMVehiclePlayerDamageRatio=1\r\nGMVehicleConstructDamageRatio=1\r\nGMVehicleDamageRate=1\r\nIsCanMail=1.000000\r\nServerTags=1,2,3";
            
            Directory.CreateDirectory(ServerPath.GetServersServerFiles(_serverData.ServerID, "SoulMaskManager"));
            File.WriteAllText(ServerPath.GetServersServerFiles(_serverData.ServerID, "SoulMaskManager", $"ServerConfig_{ConfigServerName}.ini"), baseConfig);
        }
    }

    public class RandomNumberGenerator
    {
        public static string Generate12DigitRandomNumber()
        {
            Random random = new Random();
            string twelveDigitNumber = GenerateRandom12Digits(random);
            return twelveDigitNumber;
        }

        private static string GenerateRandom12Digits(Random random)
        {
            string result = "";
            for (int i = 0; i < 12; i++)
            {
                result += random.Next(0, 10).ToString(); // Generates a random digit between 0 and 9
            }
            return result;
        }
    }
}
