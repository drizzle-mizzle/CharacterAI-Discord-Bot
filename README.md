# CharacterAI Discord Bot
Simple unofficial Discord integration with [CharacterAI](https://beta.character.ai/) service which you can use to add any character on your Discord server.

![Logo](https://i.imgur.com/H5hDipp.jpg)

> (The service is currently in beta test state, and it does not have any public API documentation. In this regard, I could not find any convenient way to log into account, aside from clumsy one described below, and I also cannot guarantee that this bot will not suddenly stop working in any moment when CharacterAI's developers will make another update :P)

## Features
- Talk with any character on your own server and change them on the wing.
- Automatically sets the name and profile picture of the character.
- Supports answers swiping.
- Supports sending and receiving of images from character.
- Make bots talk to each other.
- Random replies.
- Experimental *audience mode* feature (read below).

![chrome_lqjAER1cug](https://user-images.githubusercontent.com/55811932/208914718-5e6fa518-da30-4807-92c7-c2238f4bef87.gif)

## Commands
- `set character <id>` - set character by id
	- Aliases: `set!`, `sc`
    - Example: `set! thep9Jza4nSUQQ_ok7YCEI2uMim5oH9OXcVUyo5-C7E`
- `reset character` - save and start new chat
    - Alias: `reset!`
- `audience toggle` - enable/disable audience mode *(What is the audience mode - read below)*
	- Alias: `amode!`
- `call user <@user_mention> <any text>` - Make character call other user *(use it to make two bots talk to each other)*
    - Aliases: `call!`, `cu`
    - Example: `call! @another_character Do you love donuts?`
    - *(if no text argument provided, default `"Hey!"` will be used)*
- `skip! <amount>` - Make character ignore next few messages *(use it to stop bots' conversation)*
    - Alias: `delay!`
    - Example: `skip! 2` => `Next 2 message(s) will be ignored`
    - *(if no amount argument provided, default '3' will be used)*
    - *(commands will not be ignored, amount can be reduced with another call)*
- `reply chance <chance>` - Change the probability of random replies on new users' messages `in %` *(It's better to use it with audience mode enabled)*
    - Alias: `rc`
    - Example: `rc 50` => `Probability of random answers was changed from 0% to 50%`
    - *(argument always required)*
    - *(keep in mind that with this feature enabled, commands can be executed without bot prefix/mention)*
- `hunt! <@user_mention>` - Make character always reply on messages of certain user
- `unhunt! <@user_mention>` - Stop hunting user
- `hunt chance <chance>` - Change the probability of replies to hunted user `in %`
	- Alias: `hc`
	- *(default value = 100)*
- `ping` - check latency

## Additional configuration
- If you want to give other users ability to configure a bot, give them a role and place it's name in `discord_bot_role`.
- Specify prefixes in `discord_bot_prefixes` field so you could call your bot without mention or reply.
- Set `default_audience_mode` to `True` if you want it to be enabled by default on a bot launch.
- Specify `default_no_permission_file` with a name of the gif/image you want to be shown when non-privileged user tries to execute bot commands (or just leave it empty to disable this feature)
- Set `auto_setup` to `True` and specify `auto_char_id` with id of a character if you want bot to set character automatically after every relaunch. 

##  How to set up
1. Download [last release](https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/releases/tag/1.55_hotfix)
2. Create a new Discord application with bot (you can easily find guide on the internet, so I won't focus on this part here).
	- *(don't forget to enable all **"Privileged Gateway Intents"** switchers)*
3. Get your bot token and place it in `Config.json` file.
4. Create character.ai account if you don't have one.
5. Sign in, open DevTools (<Ctrl+Shift+J> in Chrome), find "Network" page and go to the "Fetch/XHR" section.

![image](https://user-images.githubusercontent.com/55811932/208903651-17ffef98-6a88-47d2-92ec-6940e76fbf77.png)

![image](https://user-images.githubusercontent.com/55811932/208903737-1ec8741a-3151-455b-bca0-9b2cf878dd48.png)

6. Reload page (DevTools should remain open).
7. Now you should see a list of requests. Locate "auth0/" and open it's Response/Preview page.

![image](https://user-images.githubusercontent.com/55811932/208904061-f2628020-3e77-4f01-865b-809a8234c70b.png)

8. That's your accout auth token.

![image](https://user-images.githubusercontent.com/55811932/208904455-8331a2d5-5160-448e-9464-77fb62d410b7.png)

9. Copy it's value and place in `char_ai_user_token` field in `Config.json` file.
10. Launch the bot:
  - **Windows:**
    - Go to the `bin` folder.
    - Simply run **CharacterAI_Discord_Bot.exe**
  - **Linux**
    - Go to the `bin` folder.
    - Execute `chmod 777 ./CharacterAI_Discord_Bot`
    - Run `./CharacterAI_Discord_Bot`
11. Open a chat with a character you want to add on your server.
12. In adress bar locate and copy character's id (it's right after '/chat?char=...').

![image](https://user-images.githubusercontent.com/55811932/208032897-71a459f4-4db3-47b0-a042-d772a3f0c01b.png)

13. Go to your server and run bot command `set <id>`
	- (Bot will automatically set it's avatar and nickname)

![chrome_NJ88RGQgdn](https://user-images.githubusercontent.com/55811932/208912215-8ecbb70b-5f12-4739-9b6d-20bfebbe81eb.gif)

**14. Enjoy converstaion!**

## Audience mode (experimental)

![image](https://user-images.githubusercontent.com/55811932/208913065-e367dbfa-8296-43dd-a0fc-c5aec847f9e2.png)

When you talk with a character you can use only one character.ai account for every user on your server, and it does puts some limitations on a conversation. Thinking about it, I decided to try to "explain" character that there are many different users who speak with him using a single account. And... it really worked lol.
If you add a nickname and a quote to your reply, the character will, usually, understand the context, and his answers will be more consistent.
> This feature is disabled by default, but you can enable it with `amode` command.

**How does it looks in Discord:**

![image](https://user-images.githubusercontent.com/55811932/208031628-a52057dc-9cf4-4344-b1f0-3abd1c9ba51f.png)

![image](https://user-images.githubusercontent.com/55811932/208033040-f5385d42-c410-4471-9e07-58ef6310462a.png)

**How it actually is:**

![image](https://user-images.githubusercontent.com/55811932/208031792-d971acc6-afca-4bf4-8888-f287679c4f8b.png)

![image](https://user-images.githubusercontent.com/55811932/208032085-301df36b-e335-49af-9974-65b617c73f74.png)
