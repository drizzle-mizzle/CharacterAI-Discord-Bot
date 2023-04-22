using CharacterAI;
using CharacterAI.Models;
using CharacterAI_Discord_Bot.Handlers;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace CharacterAI_Discord_Bot.Service
{
    /// <summary>
    /// Just a set of methods that change bot basic definitions
    /// </summary>
    public partial class CommonService
    {
        public async Task SetCharacterAsync(string charId, CommandsHandler handler, SocketCommandContext context, bool reset = false)
        {
            var cI = handler.CurrentIntegration;

            SetupResult result;
            while (true)
            {
                try
                {
                    result = await cI.SetupAsync(charId, startWithNewChat: reset);
                    break;
                }
                catch (Exception e)
                {
                    Failure($"Setup Failed. Trying again...\nDetails:\n{e}");
                }
            }

            if (!result.IsSuccessful)
            {
                await context.Message.ReplyAsync($"{WARN_SIGN_DISCORD}️ Failed to set a character!").ConfigureAwait(false);
                return;
            }

            bool firstLaunch = !handler.Channels.Any();
            if (firstLaunch)
            {
                var savedData = GetStoredData(charId);

                handler.BlackList = savedData.BlackList;
                Log("Restored blocked users: ");
                Success(handler.BlackList.Count.ToString());

                handler.Channels = savedData.Channels;
                Log("Restored channels: ");
                Success(handler.Channels.Count.ToString());
            }
            else handler.Channels.Clear();

            SaveData(channels: handler.Channels);

            string reply = cI.CurrentCharacter.Greeting!;

            if (BotConfig.CharacterAvatarEnabled)
                try { await SetBotNicknameAndRole(cI.CurrentCharacter.Name!, context.Client).ConfigureAwait(false); }
                catch { reply += "\n⚠️ Failed to set bot name! Probably, missing permissions?"; }
            if (BotConfig.CharacterNameEnabled)
                try { await SetBotAvatar(context.Client.CurrentUser, cI.CurrentCharacter!, @HttpClient).ConfigureAwait(false); }
                catch { reply += "\n⚠️ Failed to set bot avatar!"; }
            if (BotConfig.DescriptionInPlaying)
                _ = SetPlayingStatusAsync(context.Client, integration: cI).ConfigureAwait(false);

            await context.Message
                .ReplyAsync($"{context.Message.Author.Mention} {reply}")
                .ConfigureAwait(false);
        }

        public async Task SetBotNicknameAndRole(string name, DiscordSocketClient client)
        {
            var guildsList = client.Guilds;

            foreach (var guild in guildsList)
            {
                try
                {
                    var botAsGuildUser = guild.GetUser(client.CurrentUser.Id);
                    await botAsGuildUser.ModifyAsync(u => { u.Nickname = name; }).ConfigureAwait(false);

                    await CreateBotRoleAsync(guild);
                } catch { };
            }
        }

        public async Task SetBotAvatar(SocketSelfUser bot, Character character, HttpClient httpClient)
        {
            Stream image;
            byte[]? response = await TryDownloadImgAsync(character.AvatarUrlFull!, httpClient);
            response ??= await TryDownloadImgAsync(character.AvatarUrlMini!, httpClient);

            if (response is not null)
                image = new MemoryStream(response);
            else
            {
                try { image = new FileStream(defaultAvatarPath, FileMode.Open); }
                catch (Exception e)
                {
                    Failure($"Failed to set default bot avatar.\n" + e.ToString());
                    return;
                }
            }

            await bot.ModifyAsync(u => { u.Avatar = new Discord.Image(image); }).ConfigureAwait(false);
        }

        public async Task SetPlayingStatusAsync(DiscordSocketClient client, int type = 0, string? status = null, Integration? integration = null)
        {
            if (integration is not null)
                status = integration.CurrentCharacter.IsEmpty ? "No character selected" : integration.CurrentCharacter.Title;
            else if (status == "0")
                status = null;

            await client.SetGameAsync(status, type: (ActivityType)type).ConfigureAwait(false);
        }
    }
}
