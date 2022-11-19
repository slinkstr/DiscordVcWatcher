using Discord;
using Discord.WebSocket;
using System.Text.Json;

namespace DiscordVcWatcher
{
    internal class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public static DiscordSocketClient? SocketClient;
        public static ProgramConfig? Config;

        private async Task MainAsync()
        {
            SocketClient = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Debug,
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates
            });

            // config
            if (!File.Exists("config.json"))
            {
                Console.WriteLine("Unable to find config.json.");
                ExitProgram(1);
            }
            string cfgText = File.ReadAllText("config.json");
            Config = JsonSerializer.Deserialize<ProgramConfig>(cfgText, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            VerifyConfig(Config);

            await SocketClient.LoginAsync(TokenType.Bot, Config.Token);
            await SocketClient.StartAsync();
            SocketClient.Log += LogDiscord;
            SocketClient.UserVoiceStateUpdated += VoiceStateUpdated;
            
            await Task.Delay(-1);
        }

        private static async Task VoiceStateUpdated(SocketUser user, SocketVoiceState state1, SocketVoiceState state2)
        {
            Console.WriteLine($"User: {user}, state1: {state1}, state2: {state2}");

            var fromChannel = state1.VoiceChannel;
            var toChannel = state2.VoiceChannel;
            bool fromMatchesTarget = (fromChannel != null && fromChannel.Guild.Id == Config.TargetGuild);
            bool toMatchesTarget = (toChannel != null && toChannel.Guild.Id == Config.TargetGuild);

            if (!fromMatchesTarget && !toMatchesTarget) { return; }

            var guild = fromMatchesTarget ? fromChannel.Guild : toChannel.Guild;
            
            var targetUsers = GetAllConnectedUsers(guild).Where(x => Config.UserWhitelist.Any(y => y == x.Id)).ToList();
            string output = string.Join("\n", targetUsers.Select(x => x.Id));
            await File.WriteAllTextAsync("connectedUsers.txt", output);
        }

        private static List<SocketGuildUser> GetAllConnectedUsers(SocketGuild guild)
        {
            List<SocketGuildUser> connectedUsers = new List<SocketGuildUser>();
            foreach (var channel in guild.VoiceChannels)
            {
                connectedUsers.AddRange(channel.ConnectedUsers);
            }
            return connectedUsers;
        }

        private static async Task LogDiscord(LogMessage msg)
        {
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Debug:
                case LogSeverity.Verbose:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
            }
            Console.Write(msg.ToString(prependTimestamp: true));
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void VerifyConfig(ProgramConfig? config)
        {
            if (config == null)
            {
                throw new Exception("Failed to process config.");
            }
            if (string.IsNullOrWhiteSpace(config.Token))
            {
                throw new Exception("Token was invalid or not provided.");
            }
            if (config.TargetGuild == 0)
            {
                throw new Exception("TargetGuild was invalid or not provided.");
            }
            if (config.UserWhitelist.Count < 1)
            {
                throw new Exception("UserWhitelist was invalid or not provided.");
            }
            return;
        }

        private static void ExitProgram(int code)
        {
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Environment.Exit(code);
        }

        public class ProgramConfig
        {
            public string Token { get; set; } = "";
            public ulong TargetGuild { get; set; } = 0;
            public List<ulong> UserWhitelist { get; set; } = new List<ulong>();
        }
    }
}