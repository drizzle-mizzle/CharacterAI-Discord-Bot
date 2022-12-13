# CharacterAI Discord Bot
Simple integration with https://beta.character.ai service which you can use to add any character on your Discord server.

![CharacterAI](https://i.imgur.com/H5hDipp.jpg)

(The service is currently in beta test state, and it does not have any public API documentation. In this regard, I could not find any convenient way to log into account, aside from clumsy one described below, and I also cannot guarantee that this bot will not stop working in any moment CharacterAI's developers will make another update :P)
Also, I have to note that I'm very-very new to .NET and C#, so my code can be a little awful. Whole thing was made just for fun and with self-educational intentions.
> **🙄 Well, it happened way faster than I thought. Currently, integration doesn't work anymore. Not fully sure, but seems like it's the same problem as here https://github.com/acheong08/ChatGPT/issues/261**

##  How to set up
1. Create a new Discord application with bot (you can easily find all guides on the internet, so I won't focus on this part here).
2. Get your bot token and place it in Precompiled/_YOUR_OS_/Config.json file.
3. Create character.ai account if you don't have one.
4. Sign in and open a chat with a character you want to add on your server.
5. In adress bar locate and copy character's id (it's right after '/chat?char=...'), place it in Precompiled/_YOUR_OS_/Config.json file.
6. Open DevTools (<Ctrl+Shift+J> in Chrome) and go to "Fetch/XHR" section.
7. Reload page (DevTools should remain open).
8. Now you should see a list of requests. Locate "auth0/" and open it's response.
![FetchXHR](https://i.imgur.com/UnOxKUg.png)
9. What you must see is a string that looks like '{"key":"81a8d269da126081a5f4..."}', that's your accout auth token.
10. Copy it's value and place in Precompiled/_YOUR_OS_/Config.json file.
## Launching bot:
Windows:
1. Go to Precompiled/win-x64 folder
2. Launch CharacterAI_Discord_Bot.exe

Linux:
1. Go to Precompiled/linux-x64 folder
2. Execute "chmod 777 ./CharacterAI_Discord_Bot"
3. Launch bot with "./CharacterAI_Discord_Bot"
