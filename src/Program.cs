﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Lykos.Modules;
using Microsoft.Extensions.Logging;
using Minio;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lykos.Config;

namespace Lykos
{
    class Program
    {
        static DiscordClient discord;
        static CommandsNextExtension commands;
        public static Random rnd = new();
        public static ConfigJson cfgjson;
        public static HasteBinClient hasteUploader;
        public static MinioClient minio;
        internal static EventId EventID { get; } = new EventId(1000, "Bot");

        static void Main()
        {
            MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string configFile = "config.json";
            string json = "";

            if (!File.Exists(configFile))
                configFile = "config/config.json";

            if (File.Exists(configFile))
            {
                using FileStream fs = File.OpenRead(configFile);
                using StreamReader sr = new(fs, new UTF8Encoding(false));
                json = await sr.ReadToEndAsync();
            }
            else
            {
                json = System.Environment.GetEnvironmentVariable("LYKOS_CONFIG");
            }

            cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            hasteUploader = new HasteBinClient(cfgjson.HastebinEndpoint);

            minio = new MinioClient
            (
                cfgjson.S3.Endpoint,
                cfgjson.S3.AccessKey,
                cfgjson.S3.SecretKey,
                cfgjson.S3.Region
            ).WithSSL();

            discord = new DiscordClient(new DiscordConfiguration
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information
            });

            Task OnReady(DiscordClient client, ReadyEventArgs e)
            {
                Console.WriteLine($"Logged in as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
                return Task.CompletedTask;
            };


            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = cfgjson.Prefixes,

            });

            Type[] commandClasses =
            {
                typeof(Utility),
                typeof(Mod),
                typeof(Owner),
                typeof(Fun)
            };

            foreach (Type cmdClass in commandClasses)
            {
                commands.RegisterCommands(cmdClass);
            }

            async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
            {
                // gallery
                if (e.Channel.Id == 671182122429710346)
                {
                    // Delete the message if there are no attachments, unless the message contains a URL.
                    if (e.Message.Attachments.Count == 0 && !(e.Message.Content.Contains("http")))
                    {
                        await e.Message.DeleteAsync();
                        DiscordChannel log = await client.GetChannelAsync(671183700448509962);
                        var embed = new DiscordEmbedBuilder()
                        .WithDescription(e.Message.Content)
                        .WithTimestamp(DateTime.Now)
                        .WithFooter(
                            "Relayed from #gallery",
                            null
                        )
                        .WithAuthor(
                            e.Author.Username,
                            null,
                            $"https://cdn.discordapp.com/avatars/{e.Author.Id}/{e.Author.AvatarHash}.png?size=128"
                        )
                        ;

                        await log.SendMessageAsync(null, embed);
                    }
                }

                // story 2
                if (e.Channel.Id == 695636314959118376)
                {
                    System.Collections.Generic.IReadOnlyList<DSharpPlus.Entities.DiscordMessage> prevMsgs = await e.Channel.GetMessagesBeforeAsync(e.Message.Id, 1);
                    DSharpPlus.Entities.DiscordMessage prevMsg = prevMsgs[0];
                    DSharpPlus.Entities.DiscordChannel log = await client.GetChannelAsync(695636452804919297);
                    if (e.Message.Content.Contains(" "))
                    {
                        await e.Message.DeleteAsync();
                        await log.SendMessageAsync($"{e.Author.Mention}:\n>>> {e.Message.Content}");
                    }
                    else if (e.Message.Author.Id == prevMsg.Author.Id)
                    {
                        await e.Message.DeleteAsync();
                        await log.SendMessageAsync($"(SAMEAUTHOR) {e.Author.Mention}:\n>>> {e.Message.Content}");
                    }

                }

                // Prefix query handling
                if
                (
                  e.Message.Content.ToLower() == $"what prefix <@{client.CurrentUser.Id}>" ||
                  e.Message.Content.ToLower() == $"what prefix <@!{client.CurrentUser.Id}>"
                )
                {
                    await e.Channel.SendMessageAsync($"My prefixes are: ```json\n" +
                        $"{JsonConvert.SerializeObject(cfgjson.Prefixes)}```");
                }

                // Yell at people who get the prefix wrong, but only if the argument is an actual command.
                if (e.Message.Content.ToLower().StartsWith("ik "))
                {
                    string potentialCmd = e.Message.Content.Split(' ')[1];
                    foreach (System.Collections.Generic.KeyValuePair<string, Command> cmd in commands.RegisteredCommands)
                    {
                        // Checks command name, display name and all aliases.
                        if (cmd.Key == potentialCmd || potentialCmd == cmd.Value.QualifiedName || cmd.Value.Aliases.Contains(potentialCmd))
                        {
                            await e.Channel.SendMessageAsync("It looks like you misunderstood my prefix.\n" +
                                "The main prefix for me is `lk`. The first letter is a lowercase `l`/`L`, not an uppercase `i`/`I\n`" +
                                "The prefix is inspired by my name, **L**y**k**os.");
                            break;
                        }
                    }
                }

            };

            // Gallery edit handling
            async Task MessageUpdated(DiscordClient client, MessageUpdateEventArgs e)
            {
                // #gallery
                if (e.Channel.Id == 671182122429710346)
                {
                    // Delete the message if there are no attachments, unless the message contains a URL.
                    if (e.Message.Attachments.Count == 0 && !(e.Message.Content.Contains("http")))
                    {
                        await e.Message.DeleteAsync();
                        DSharpPlus.Entities.DiscordChannel log = await client.GetChannelAsync(671183700448509962);
                        await log.SendMessageAsync($"[EDIT] {e.Author.Mention}:\n>>> {e.Message.Content}");
                    }
                }
            };

            // Leave event handling, for my servers
            async Task GuildMemberRemoved(DiscordClient client, GuildMemberRemoveEventArgs e)
            {
                DSharpPlus.Entities.DiscordChannel channel = null;
                // Erisa's Corner
                if (e.Guild.Id == 228625269101953035)
                {
                    // #general-chat
                    channel = await client.GetChannelAsync(751534914469363832);
                }
                // Project Evenfall
                else if (e.Guild.Id == 535688189659316245)
                {
                    // #greetings
                    channel = await client.GetChannelAsync(542497115583283220);
                }

                if (channel != null)
                {
                    await channel.SendMessageAsync($"**{e.Member.Username}** has left us 😔");
                }
            };


            async Task CommandsNextService_CommandErrored(CommandsNextExtension cnext, CommandErrorEventArgs e)
            {
                if (e.Exception is CommandNotFoundException && (e.Command == null || e.Command.QualifiedName != "help"))
                    return;

                CommandContext ctx = e.Context;
                // This is a fairly ugly workaround but, it does appear to be stable for this command at least.
                if (e.Command != null && e.Command.Name == "avatar" && e.Exception is System.ArgumentException)
                {
                    await ctx.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Xmark} User not found! " +
                        $"Only mentions, IDs and Usernames are accepted.\n" +
                        $"Note: It is not needed to specify `byid`, simply use the ID directly.");
                }

                e.Context.Client.Logger.LogError(EventID, e.Exception, "Exception occurred during {0}'s invocation of '{1}'", e.Context.User.Username, e.Context.Command.QualifiedName);

                var exs = new List<Exception>();
                if (e.Exception is AggregateException ae)
                    exs.AddRange(ae.InnerExceptions);
                else
                    exs.Add(e.Exception);

                foreach (var ex in exs)
                {
                    if (ex is CommandNotFoundException && (e.Command == null || e.Command.QualifiedName != "help"))
                        return;

                    if (ex is ChecksFailedException && (e.Command.Name != "help"))
                        return;

                    var embed = new DiscordEmbedBuilder
                    {
                        Color = new DiscordColor("#FF0000"),
                        Title = "An exception occurred when executing a command",
                        Description = $"`{e.Exception.GetType()}` occurred when executing `{e.Command.QualifiedName}`.",
                        Timestamp = DateTime.UtcNow
                    };
                    embed.WithFooter(discord.CurrentUser.Username, discord.CurrentUser.AvatarUrl)
                        .AddField("Message", ex.Message);
                    if (e.Exception.GetType().ToString() == "System.ArgumentException")
                        embed.AddField("Note", "This usually means that you used the command incorrectly.\n" +
                            "Please double-check how to use this command.");
                    await e.Context.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
                }
            }

            Task Discord_ThreadCreated(DiscordClient client, ThreadCreateEventArgs e)
            {
                client.Logger.LogDebug(eventId: EventID, $"Thread created in {e.Guild.Name}. Thread Name: {e.Thread.Name}");
                return Task.CompletedTask;
            }

            Task Discord_ThreadUpdated(DiscordClient client, ThreadUpdateEventArgs e)
            {
                client.Logger.LogDebug(eventId: EventID, $"Thread updated in {e.Guild.Name}. New Thread Name: {e.ThreadAfter.Name}");
                return Task.CompletedTask;
            }

            Task Discord_ThreadDeleted(DiscordClient client, ThreadDeleteEventArgs e)
            {
                client.Logger.LogDebug(eventId: EventID, $"Thread deleted in {e.Guild.Name}. Thread Name: {e.Thread.Name ?? "Unknown"}");
                return Task.CompletedTask;
            }

            Task Discord_ThreadListSynced(DiscordClient client, ThreadListSyncEventArgs e)
            {
                client.Logger.LogDebug(eventId: EventID, $"Threads synced in {e.Guild.Name}.");
                return Task.CompletedTask;
            }

            Task Discord_ThreadMemberUpdated(DiscordClient client, ThreadMemberUpdateEventArgs e)
            {
                client.Logger.LogDebug(eventId: EventID, $"Thread member updated.");
                Console.WriteLine($"Discord_ThreadMemberUpdated fired for thread {e.ThreadMember.ThreadId}. User ID {e.ThreadMember.Id}.");
                return Task.CompletedTask;
            }

            Task Discord_ThreadMembersUpdated(DiscordClient client, ThreadMembersUpdateEventArgs e)
            {
                client.Logger.LogDebug(eventId: EventID, $"Thread members updated in {e.Guild.Name}.");
                return Task.CompletedTask;
            }

            discord.Ready += OnReady;
            discord.MessageCreated += MessageCreated;
            discord.MessageUpdated += MessageUpdated;
            discord.GuildMemberRemoved += GuildMemberRemoved;
            commands.CommandErrored += CommandsNextService_CommandErrored;
            discord.ThreadCreated += Discord_ThreadCreated;
            discord.ThreadUpdated += Discord_ThreadUpdated;
            discord.ThreadDeleted += Discord_ThreadDeleted;
            discord.ThreadListSynced += Discord_ThreadListSynced;
            discord.ThreadMemberUpdated += Discord_ThreadMemberUpdated;
            discord.ThreadMembersUpdated += Discord_ThreadMembersUpdated;


            var slash = discord.UseSlashCommands();

            slash.RegisterCommands<SlashCommands>(438781053675634713);
            slash.RegisterCommands<SlashCommands>(228625269101953035);

            await discord.ConnectAsync();
            await Task.Delay(-1);
        }
    }

    public class Require​Owner​Attribute : CheckBaseAttribute
    {
        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            return Task.FromResult(Program.cfgjson.Owners.Contains(ctx.Member.Id));
        }
    }

}
