﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Interactivity;
using Lykos.Modules;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Minio;
using Minio.Exceptions;
using Minio.DataModel;

namespace Lykos
{
    class Program
    {
        static DiscordClient discord;
        static CommandsNextExtension commands;
        public static Random rnd = new Random();
        public static ConfigJson cfgjson;
        public static HasteBinClient hasteUploader = new HasteBinClient("https://paste.erisa.moe");
        public static InteractivityExtension interactivity;
        public static MinioClient minio;

        static void Main()
        {
            MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var json = "";
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);

            minio = new MinioClient("s3.nl-ams.scw.cloud", cfgjson.S3.AccessKey, cfgjson.S3.SecretKey, cfgjson.S3.Region).WithSSL();

            if (cfgjson.YoutubeData != null || cfgjson.YoutubeData != "youtubekeyhere")
            {
                Console.WriteLine("[WARN] YouTube API functions have been deprecated, an API key is not needed and may cause problems in future versions.");
            }

            discord = new DiscordClient(new DiscordConfiguration
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Debug,
                
            });

            interactivity = discord.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = new System.TimeSpan(60)
            });

            discord.Ready += e =>
            {
                Console.WriteLine($"Logged in as {e.Client.CurrentUser.Username}#{e.Client.CurrentUser.Discriminator}");
                return Task.CompletedTask;
            };

            discord.MessageCreated += async e =>
            {
                if (e.Channel.Id == 671182122429710346)
                {
                    if (e.Message.Content != "")
                    {
                        await e.Message.DeleteAsync();
                        var log = await e.Client.GetChannelAsync(671183700448509962);
                        await log.SendMessageAsync($"{e.Author.Mention}:\n>>> {e.Message.Content}");
                    }
                }

                if (e.Message.Content.ToLower() == $"what prefix <@{e.Client.CurrentUser.Id}>" || e.Message.Content.ToLower() == $"what prefix <@!{e.Client.CurrentUser.Id}>")
                {
                    await e.Channel.SendMessageAsync($"My prefixes are: ```json\n{JsonConvert.SerializeObject(cfgjson.Prefixes)}```");
                }

                
            };

            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = cfgjson.Prefixes,
                
            });

            commands.CommandErrored += async e =>
            {
                var ctx = e.Context;
                if (e.Command != null && e.Command.Name == "avatar" && e.Exception is System.ArgumentException)
                {
                    await ctx.RespondAsync("<:xmark:314349398824058880> User not found! Only mentions, IDs and Usernames are accepted.\nNote: It is no longer needed to specify `byid`, simply use the ID directly.");
                }

                // Console.WriteLine(e.Exception is System.ArgumentException);
                //if (e.Exception is System.ArgumentException)
                //await ctx.CommandsNext.SudoAsync(ctx.User, ctx.Channel, $"help {ctx.Command.Name}");
            };

            commands.RegisterCommands(typeof(Dbots));
            commands.RegisterCommands(typeof(Utility));
            commands.RegisterCommands(typeof(Mod));
            commands.RegisterCommands(typeof(Owner));
            commands.RegisterCommands(typeof(Fun));

            await discord.ConnectAsync();
            // var msg = discord.GetChannelAsync(132632676225122304).GetMessageAsync(1);
            await Task.Delay(-1);
        }
    }

    public class Require​Owner​Attribute : CheckBaseAttribute
    {
        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            return Program.cfgjson.Owners.Contains(ctx.Member.Id);
        }
    }


    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("prefixes")]
        public string[] Prefixes { get; private set; }

        [JsonProperty("youtube_data_api")]
        public string YoutubeData { get; private set; }

        [JsonProperty("gravatar")]
        public GravatarConfig Gravatar { get; private set; }

        [JsonProperty("s3")]
        public JsonCfgS3 S3 { get; private set; }

        [JsonProperty("owners")]
        public List<ulong> Owners { get; private set; }

        [JsonProperty("cloudflare")]
        public CloudflareConfig Cloudflare { get; private set; }

    }

    public class CloudflareConfig
    {
        [JsonProperty("apiToken")]
        public string Token { get; private set; }

        [JsonProperty("zoneID")]
        public string ZoneID { get; private set; }

        [JsonProperty("urlPrefix")]
        public string UrlPrefix { get; private set; }
    }

    public class JsonCfgS3
    {
        [JsonProperty("endpoint")]
        public string Endpoint { get; private set; }

        [JsonProperty("region")]
        public string Region { get; private set; }

        [JsonProperty("bucket")]
        public string Bucket { get; private set; }

        [JsonProperty("accessKey")]
        public string AccessKey { get; private set; }

        [JsonProperty("secretKey")]
        public string SecretKey { get; private set; }

        [JsonProperty("providerDisplayName")]
        public string displayName { get; private set; }
    }

    public class GravatarConfig
    {
        [JsonProperty("email")]
        public string Email { get; private set; }

        [JsonProperty("password")]
        public string Password { get; private set; }
    }
}
