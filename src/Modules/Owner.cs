﻿using CookComputing.XmlRpc;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Google.Cloud.Storage.V1;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static Lykos.Modules.Helpers;

namespace Lykos.Modules
{
    partial class Owner : BaseCommandModule
    {

        [Command("delete")]
        [RequireOwner]
        public async Task Delete(CommandContext ctx, ulong messageId)
        {
            await ctx.Message.DeleteAsync();
            var msg = await ctx.Channel.GetMessageAsync(messageId);
            await msg.DeleteAsync();
        }

        [Command("yeet")]
        [RequireOwner]
        public async Task Yeet(CommandContext ctx, ulong messageId)
        {
            await ctx.Message.DeleteAsync();
            var msg = await ctx.Channel.GetMessageAsync(messageId);
            await msg.SuppressEmbedsAsync();
        }

        [Group("debug")]
        [Aliases("d")]
        [RequireOwner]
        class DebugCmds : BaseCommandModule
        {
            [Command("modcheck")]
            [Description("Check whether a user has permisssion to mod another user.")]
            [Aliases("mod")]
            public async Task Modcheck(CommandContext ctx, DiscordMember firstMember, [RemainingText] DiscordMember target = null)
            {
                if (target == null)
                {
                    target = firstMember;
                    firstMember = await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id);
                }

                var invoker_hier = Mod.GetHier(firstMember);
                var target_hier = Mod.GetHier(target);

                bool allowed = Mod.AllowedToMod(firstMember, target);

                await ctx.RespondAsync($"According to my calulcations, **{firstMember.Username}#{firstMember.Discriminator}** has a Role Hierachy of `{invoker_hier.ToString()}`" +
                    $"and **{target.Username}#{target.Discriminator}** has `{target_hier.ToString()}`.\nFrom this, I can conclude that the answer is `{allowed.ToString()}`.");
            }

            [Command("sysinfo")]
            [Description("Where am I running? Lets find out together!")]
            public async Task Sysinfo(CommandContext ctx)
            {
                await ctx.RespondAsync($"🤔 Hmm, based on my research it seems that:\n" +
                    $"- This device is calling itself `{System.Environment.MachineName}`\n" +
                    $"- The OS platform is `{Helpers.GetOSPlatform().ToString()}`\n" +
                    $"- The OS describes itself as `{RuntimeInformation.OSDescription}`\n" +
                    $"- The OS architecture appears to be `{RuntimeInformation.OSArchitecture}`\n" +
                    $"- The framework I'm running from is `{RuntimeInformation.FrameworkDescription}`\n");
            }
        }

        [Group("system")]
        [Aliases("s", "sys")]
        [RequireOwner]
        [Hidden]
        class SystemCmds : BaseCommandModule
        {
            [Command("reconnect"), Aliases("rc", "re")]
            [Description("Goodbye, hello! This will reconnect my websocket connection.")]
            public async Task Reconnect(CommandContext ctx)
            {
                var msg = await ctx.RespondAsync("Reconnecting to websocket...");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await ctx.Client.ReconnectAsync();
                watch.Stop();
                await msg.ModifyAsync($"Reconnected to websocket!\n- This took `{watch.ElapsedMilliseconds}ms` to complete!");
            }

            [Command("shutdown"), Aliases("shut", "sd", "s", "kill")]
            [Description("A soft exit. I will disconnect and then end my process.")]
            public async Task Shutdown(CommandContext ctx)
            {
                var msg = await ctx.RespondAsync("Disonnecting from websocket...");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await ctx.Client.DisconnectAsync();
                watch.Stop();
                await msg.ModifyAsync($"Disconnected from websocket!\n- This took `{watch.ElapsedMilliseconds}ms` to complete!\nNow exiting main process. Goodbye!");
                Environment.Exit(0);
            }

            [Command("die")]
            [Description("A more permanent goodbye! I will try to end my own service.")]
            public async Task Die(CommandContext ctx, string target = null)
            {
                if (target != null && System.Environment.MachineName.ToLower() != target.ToLower())
                {
                    return;
                }

                var msg = await ctx.RespondAsync("Disonnecting from websocket...");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await ctx.Client.DisconnectAsync();
                watch.Stop();
                await msg.ModifyAsync($"Disconnected from websocket!\n- This took `{watch.ElapsedMilliseconds}ms` to complete!\nNow stopping main service. If that doesn't work, I'll just end my process!");

                ShellResult finishedShell = Helpers.RunShellCommand("pm2 stop lykos");

                if (finishedShell.proc.ExitCode != 0)
                {
                    Environment.Exit(1);
                }

            }

            [Command("sh")]
            [Aliases("cmd")]
            [Description("Run shell commands! Bash for Linux/macOS, batch for Windows!")]
            public async Task Shell(CommandContext ctx, [RemainingText] string command)
            {
                var msg = await ctx.RespondAsync("executing..");

                ShellResult finishedShell = Helpers.RunShellCommand(command);


                if (finishedShell.result.Length > 1947)
                {
                    HasteBinResult hasteURL = await Program.hasteUploader.Post(finishedShell.result);
                    if (hasteURL.IsSuccess)
                    {
                        await msg.ModifyAsync($"Done, but output exceeded character limit! (`{finishedShell.result.Length}`/`1947`)\nFull output can be viewed here: https://paste.erisa.moe/raw/{hasteURL.Key}\nProcess exited with code `{finishedShell.proc.ExitCode}`.");
                    }
                    else
                    {
                        await msg.ModifyAsync("Error occured during upload to hastebin. Action was executed regardless, exit code was `{proc.ExitCode}`");
                    }
                }
                else
                {
                    await msg.ModifyAsync($"Done, output: ```\n{finishedShell.result}```Process exited with code `{finishedShell.proc.ExitCode}`.");
                }
            }

            [Command("say"), Aliases("echo")]
            public async Task Say(CommandContext ctx, [RemainingText] string input)
            {
                await ctx.RespondAsync(input);
            }

        }

        [Group("eri")]
        [RequireOwner]
        [Description("Commands that manage data across Erisas things and stuff.")]
        partial class Eri : BaseCommandModule
        {
            [Command("gibinvite")]
            [Description("???")]
            public async Task Gibinvite(CommandContext ctx, int max_uses = 1, int age = 0)
            {
                var channel = await ctx.Client.GetChannelAsync(230004550973521932);
                var inv = await channel.CreateInviteAsync(age, max_uses, false, true, $"gibinvite command used in {ctx.Channel.Id}");

                DiscordDmChannel chan = await ctx.Member.CreateDmChannelAsync();
                await chan.SendMessageAsync($"Here's the invite you asked for: https://discord.gg/{inv.Code}");
                await ctx.RespondAsync($"<:check:314349398811475968> I've DMed you an invite to **Erisa's Corner** with `{max_uses}` use(s) and an age of `{age}`!");
                
            }

            [Group("update")]
            partial class Update : BaseCommandModule
            {

                [Command("avatar")]
                [Description("Updates cdn.erisa.moe/avatars/current.png or any other filename.")]
                public async Task Avatar(CommandContext ctx, string name = "current")
                {
                    if (ctx.User.Id != 228574821590499329 && ctx.User.Id != 202122613118468097)
                    {
                        await ctx.RespondAsync("<:xmark:314349398824058880> This command can only be used by Erisa or Esumi!");
                        return;
                    }

                    if (ctx.User.Id == 202122613118468097 && name == "current")
                    {
                        name = "esumi";
                    }

                    DiscordMessage msg;
                    string objectName;

                    msg = await ctx.RespondAsync($"Selected name: `{name}`\n<a:loading:585958072850317322> - Uploading to Google Cloud...");
                    objectName = $"avatars/{name}.png";


                    string avatarUrl = $"https://cdn.discordapp.com/avatars/{ctx.User.Id}/{ctx.User.AvatarHash}.png?size=4096";
                    using (var client = new WebClient())
                    {
                        client.DownloadFile(avatarUrl, "AVATAR.png");
                    }

                    Google.Apis.Storage.v1.Data.Object storageObject;
                    using (var f = File.OpenRead("AVATAR.png"))
                    {
                        objectName ??= Path.GetFileName("AVATAR.png");
                        try
                        {
                            storageObject = await Program.storageClient.UploadObjectAsync(Program.bucketName, objectName, "image/png", f);
                        }
                        catch (Google.GoogleApiException e)
                        {
                            await msg.ModifyAsync($"<:xmark:314349398824058880> A Google Cloud API error occured during upload! ```\n{e.Message}```");
                            storageObject = null;
                            return;
                        }
                        Console.WriteLine($"Uploaded {objectName}.");
                    }

                    try
                    {
                        storageObject = Program.storageClient.GetObject(Program.bucketName, objectName, new GetObjectOptions() { Projection = Projection.Full });
                    }
                    catch (Google.GoogleApiException e)
                    {
                        await msg.ModifyAsync($"<:xmark:314349398824058880>  A Google Cloud API error occured during object access! ```\n{e.Message}```");
                        return;
                    }

                    storageObject.Acl.Add(new Google.Apis.Storage.v1.Data.ObjectAccessControl()
                    {
                        Bucket = Program.bucketName,
                        Entity = "allUsers",
                        Role = "READER"
                    });

                    try
                    {
                        var updatedObject = await Program.storageClient.UpdateObjectAsync(storageObject, new UpdateObjectOptions()
                        {
                            IfMetagenerationMatch = storageObject.Metageneration
                        });
                    }
                    catch (Google.GoogleApiException e)
                    {
                        await msg.ModifyAsync($"<:xmark:314349398824058880> A Google Cloud API error occured during object updating! ```\n{e.Message}```");
                        return;
                    }

                    await msg.ModifyAsync($"Selected name: `{name}`\n<:check:314349398811475968> - Uploaded to Google Cloud!");
                }
            }
        }

    }
}
