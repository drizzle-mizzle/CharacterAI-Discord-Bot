# CharacterAI Discord Bot
Simple integration with https://beta.character.ai service which you can use to add any character on your Discord server.

![CharacterAI](https://i.imgur.com/H5hDipp.jpg)

(The service is currently in beta test state, and it does not have any public API documentation. In this regard, I could not find any convenient way to log into account, aside from clumsy one described below, and I also cannot guarantee that this bot will not stop working in any moment when CharacterAI's developers will make another update :P)
Also, I have to note that I'm very new to .NET and C#, so my code can be (and it is) kinda awful. Whole thing was made just for fun and with self-educational intentions.

##  How to set up
1. Create a new Discord application with bot (you can easily find guide on the internet, so I won't focus on this part here).
2. Get your bot token and place it in `Precompiled/_YOUR_OS_/Config.json file.`.
3. Create character.ai account if you don't have one.
4. Sign in, open DevTools (<Ctrl+Shift+J> in Chrome), find "Network" page and go to the "Fetch/XHR" section.
5. Reload page (DevTools should remain open).

![image](https://user-images.githubusercontent.com/55811932/208026300-28c0339b-8e6f-49fd-992f-7e07d439d5ba.png)

6. Now you should see a list of requests. Locate "auth0/" and open it's response.
7. You must get a string that looks like `'{"key":"81a8d269da126081a5f4..."}'`, that's your accout auth token.

![image](https://user-images.githubusercontent.com/55811932/208027304-464216ec-4325-4662-a759-59699f0216e0.png)

8. Copy it's value and place in `Precompiled/_YOUR_OS_/Config.json file.`.
9. Launch the bot:
  - Windows:
    1. Go to the `Precompiled/win-x64 folder`
    2. Simply run **CharacterAI_Discord_Bot.exe**
  - Linux:
    1. Go to `Precompiled/linux-x64 folder`
    2. Execute `chmod 777 ./CharacterAI_Discord_Bot`
    3. Run `./CharacterAI_Discord_Bot`
10. Open a chat with a character you want to add on your server.
11. In adress bar locate and copy character's id (it's right after '/chat?char=...').

![image](https://user-images.githubusercontent.com/55811932/208032897-71a459f4-4db3-47b0-a042-d772a3f0c01b.png)

12. Go to your server and run bot command `@BOT_NAME !set-character <id>`

![image](https://user-images.githubusercontent.com/55811932/208030503-f7e9cac8-4b13-4900-976c-671af16861ad.png)

(Bot will automatically set it's avatar and nickname)

![image](https://user-images.githubusercontent.com/55811932/208030592-885e1755-62b3-455a-a608-054f36770c72.png)

13. Enjoy converstaion!

## Audience mode (experimental)
![image](https://user-images.githubusercontent.com/55811932/208030740-84062de1-b7df-4ffb-bd27-2cd59b5717c6.png)

When you talk with a character, it's obvious that you can use only one character.ai account for every user on your server, and it does puts some limitations on a conversation. Thinking about it, I decided to try to "explain" character that there are many different users who speak with him using a single account. And... tt really worked lol.
If you add a nickname and a quote to your reply, the character will, usually, understand the context, and his answers will be more consistent.

**How it looks in Discord:**

![image](https://user-images.githubusercontent.com/55811932/208031628-a52057dc-9cf4-4344-b1f0-3abd1c9ba51f.png)
![image](https://user-images.githubusercontent.com/55811932/208033040-f5385d42-c410-4471-9e07-58ef6310462a.png)

**How it actually is:**
![image](https://user-images.githubusercontent.com/55811932/208031792-d971acc6-afca-4bf4-8888-f287679c4f8b.png)
![image](https://user-images.githubusercontent.com/55811932/208032085-301df36b-e335-49af-9974-65b617c73f74.png)

This feature is disabled by default, but you can easily enable it with `@BOT_NAME !audience-toggle`.

