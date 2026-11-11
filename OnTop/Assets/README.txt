thumbtack.ico is OnTop's own icon (exe, taskbar, tray, installer).

It was drawn from simple shapes by tools/make_icon.ps1. It is original
artwork, part of OnTop, under the same license (GPL-3.0-or-later). No
third-party image or icon set was used.

Frames: 16, 20, 24, 32, 40, 48, 64 px (32-bit BMP, loads everywhere) and
256 px (PNG). To rebuild it on Windows:

    powershell -ExecutionPolicy Bypass -File tools\make_icon.ps1 -OutIco OnTop\Assets\thumbtack.ico -PreviewPng preview.png

Users can still override the tray icon by placing their own thumbtack.ico in
%APPDATA%\OnTop\assets\ (installed) or <exe folder>\Data\assets\ (portable).
