<div align="center">

# LinkConvertisor

**Paste a link, get an MP3 or MP4. Clean, fast, one click.**

YouTube · YouTube Music · SoundCloud · Spotify · Deezer · Apple Music

[![Latest version](https://img.shields.io/github/v/release/LapPl4ND/LinkConvertisor?style=for-the-badge&label=version&color=FF3D7F)](https://github.com/LapPl4ND/LinkConvertisor/releases/latest)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-1E1E24?style=for-the-badge)

<br>

### [⬇️ Download LinkConvertisor](https://github.com/LapPl4ND/LinkConvertisor/releases/latest/download/LinkConvertisor.exe)

<sub>A single .exe file · No installation · No .NET required</sub>

</div>

<br>

## ✨ Features

<table>
<tr>
<td width="50%" valign="top">

### 🎯 Quick and simple
- Paste a link, hit **Analyze**, then **Convert**
- **Drag and drop** a link straight from your browser
- **Automatic detection** of a link copied to your clipboard
- Live progress with speed, and a **Cancel** button

</td>
<td width="50%" valign="top">

### 🎧 Proper files
- **MP3** at 128 / 192 / 256 / 320 kbps
- **MP4** in 480p / 720p / 1080p / Max (H.264 first, plays everywhere)
- Cover art + title + artist **embedded** in the file
- Square cover art for MP3s

</td>
</tr>
<tr>
<td width="50%" valign="top">

### 🎨 Same vibe as ImageGabe
- Dark / light theme, French / English
- Accent color follows the platform of your link
- Blurred ambient background from the cover art

</td>
<td width="50%" valign="top">

### ⚙️ Engine
- Powered by [yt-dlp](https://github.com/yt-dlp/yt-dlp) + [FFmpeg](https://ffmpeg.org)
- Downloaded automatically on first launch (~150 MB, once)
- One-click engine update from Settings when YouTube changes something

</td>
</tr>
</table>

## 🔎 How Spotify / Deezer / Apple Music work

Those platforms protect their audio, so LinkConvertisor never downloads from them.
It reads the **artist + title** of the track, finds the same song on YouTube, and
converts that — while keeping the original cover art and tags. Only **track** links
are supported for these platforms (no albums or playlists yet).

## 🛠️ Build it yourself

1. Open `LinkConvertisor.sln` in Visual Studio 2022 (.NET 8)
2. Press **F5**
3. To make a single shareable .exe: double-click `LinkConvertisor/publish.bat`

Optional: drop an `app.ico` next to `LinkConvertisor.csproj`, it's picked up automatically.

## ⚖️ Note

Only convert content you have the right to download (your own uploads, royalty-free
or Creative Commons music, etc.) and respect each platform's terms of service.
