# Netease Cloud Music Discord Rpc  
  
  
Enables Discord [Rich Presence](https://discordapp.com/rich-presence) For Netease Cloud Music.  
将网易云音乐动态同步到Discord.  
2.0 Major update, Rpc will be clear when the music pauses.  
2.0版本更新之后, 可以在暂停时清除Rpc状态了.  
  
  
  
### Info
* This application will auto launch on system start.
* 这个软件会在你开机的时候自启动.  
* To add Application to whilelist, edit windows.txt. More info see [FindWindow](https://msdn.microsoft.com/en-us/library/windows/desktop/ms633499(v=vs.85).aspx)
* 要添加软件到白名单, 只需要在windows.txt新增一行输入白名单程序的lpClassName. 查看文档 [FindWindow](https://msdn.microsoft.com/en-us/library/windows/desktop/ms633499(v=vs.85).aspx)
  
  
  
### Feature
* Sync Rich Presence.
* Clear presence when you are using fullscreen or whitelist Application.
* 同步动态到Discord.
* 清除动态当你运行全屏程序或者其他白名单程序. (例如你全屏游玩CSGO或者打开了VisualStudio)
  
  
  
### Screenshot
![Screenshot](https://img.kxnrl.com/ugc/6929F80BC24B7D4388C852F8FBC3B870CE6E0C63)
  
  
  
### Changes log
#### 2.0
- 直接读取内存来获取歌曲进度和长度
- 新增->当暂停音乐时,Rpc状态会被清除
- 开机自启功能重做
- 更新.NET Framework 到4.8版本
- 使用新的DiscordRpc库
- 改为winform启动