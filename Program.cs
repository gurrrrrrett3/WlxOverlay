﻿using X11Overlay.Core;
using X11Overlay.GFX.OpenGL;
using X11Overlay.Overlays;
using X11Overlay.Overlays.Simple;
using X11Overlay.Screen.Interop;
using X11Overlay.Types;

Config.Load();

var manager = OverlayManager.Initialize();

manager.RegisterChild(new DesktopCursor());

manager.RegisterChild(Config.Instance.LeftUsePtt
    ? new LaserPointerWithPushToTalk(LeftRight.Left)
    : new LaserPointer(LeftRight.Left));

manager.RegisterChild(Config.Instance.RightUsePtt
    ? new LaserPointerWithPushToTalk(LeftRight.Right)
    : new LaserPointer(LeftRight.Right));

IEnumerable<BaseOverlay> GetScreens()
{
    var numScreens = XScreenCapture.NumScreens();
    for (var s = 0; s < numScreens; s++)
    {
        var screen = new ScreenOverlay(s) { WantVisible = s == 0 };
        manager!.RegisterChild(screen);
        yield return screen;
    }
}

if (!KeyboardLayout.Load())
{
    Console.WriteLine("[Fatal] Keyboard layout is invalid.");
    Environment.Exit(1);
}

var keyboard = new KeyboardOverlay();
manager.RegisterChild(keyboard);

manager.RegisterChild(new Watch(keyboard, GetScreens()));

var engine = new GlGraphicsEngine();
engine.StartEventLoop();