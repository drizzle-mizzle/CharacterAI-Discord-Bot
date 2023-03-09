# CharacterAI Discord Bot
An unofficial Discord integration with [CharacterAI](https://beta.character.ai/) service which you can use to add any character on your own Discord server.

<div align="center">
    
![Logo](https://user-images.githubusercontent.com/55811932/224168501-48e81f64-9b2f-442c-a8fe-6ecab8d7aab2.png)<br>
<sup><b>If found this project useful for you, the best way to thank me is to simply leave a star ⭐</b></sup>
</div>

## 🪄Features
- Talk with any character on your own server and change them on the fly.
- Embedded character search.
- Automatically sets the name and profile picture of a character.
- Supports answers swiping and image sending.
- Supports multiple parallel chats with the same character.
- Random replies, audience mode and some other stuff.

    ![chrome_cshYMsGIaB](https://user-images.githubusercontent.com/55811932/211129383-c7cd4ca2-ceb4-42c5-8449-bc6ce9b2d538.gif)
    
## 📓Documentation
- [How to set up](https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/wiki/How-to-set-up)
- [Commands](https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/wiki/Commands)
- [Additional configuration](https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/wiki/Additional-configuration)

## 🩼Known issues
Some **Windows users** are facing the problem of being unable to access character.ai: https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/issues/28 <br>
If you encounter something similar to:<br>
> `Request failed! (https://beta.character.ai/chat/character/info/)`<br>
> `Response: Forbidden`

First, make sure that you use correct cAI user token.<br>
And, if it's correct, but you're still getting this error, sadly, **the only available workaround right now is to use Linux OS** for hosting.<br>
The simplest way is to just install WSL2 with Ubuntu, which is pretty easy to do, and launch this bot from there:<br>
[How to Install Ubuntu on WSL2 on Windows](https://ubuntu.com/tutorials/install-ubuntu-on-wsl2-on-windows-10#1-overview)
