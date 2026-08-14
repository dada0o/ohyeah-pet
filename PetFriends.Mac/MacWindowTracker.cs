using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace PetFriends.Mac;

internal readonly record struct MacHostWindow(
    uint WindowNumber,
    int ProcessId,
    double Left,
    double Top,
    double Width,
    double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

internal sealed class MacWindowTracker
{
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    private const uint WindowListOnScreenOnly = 1;
    private const uint WindowListExcludeDesktopElements = 16;
    private const uint NullWindowId = 0;
    private const uint Utf8Encoding = 0x08000100;
    private const int CfNumberSInt32Type = 3;
    private const int CfNumberDoubleType = 13;
    private const int WindowBelow = -1;

    private int _lastHostProcessId;

    public double MainDisplayWidth
    {
        get
        {
            var bounds = CGDisplayBounds(CGMainDisplayID());
            return Math.Max(1, bounds.Size.Width);
        }
    }

    public void TrackForegroundApplication()
    {
        var processId = GetFrontmostProcessId();
        if (processId > 0 && processId != Environment.ProcessId)
        {
            _lastHostProcessId = processId;
        }
    }

    public bool TryGetPreferredWindow(out MacHostWindow host)
    {
        TrackForegroundApplication();
        var windows = GetUsableWindows();
        if (windows.Count == 0)
        {
            host = default;
            return false;
        }

        if (_lastHostProcessId > 0)
        {
            foreach (var candidate in windows)
            {
                if (candidate.ProcessId != _lastHostProcessId) continue;
                host = candidate;
                return true;
            }
        }

        host = windows[0];
        _lastHostProcessId = host.ProcessId;
        return true;
    }

    public static bool OrderBelow(Window window, uint hostWindowNumber)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero || hostWindowNumber == 0) return false;
        var selector = sel_registerName("orderWindow:relativeTo:");
        objc_msgSend_OrderWindow(handle, selector, WindowBelow, (nint)hostWindowNumber);
        return true;
    }

    private static int GetFrontmostProcessId()
    {
        _ = NativeLibrary.Load(AppKit);
        var workspaceClass = objc_getClass("NSWorkspace");
        if (workspaceClass == IntPtr.Zero) return 0;
        var workspace = objc_msgSend_IntPtr(workspaceClass, sel_registerName("sharedWorkspace"));
        var application = objc_msgSend_IntPtr(workspace, sel_registerName("frontmostApplication"));
        return application == IntPtr.Zero
            ? 0
            : objc_msgSend_Int32(application, sel_registerName("processIdentifier"));
    }

    private static List<MacHostWindow> GetUsableWindows()
    {
        var result = new List<MacHostWindow>();
        var windowNumberKey = CreateKey("kCGWindowNumber");
        var ownerProcessKey = CreateKey("kCGWindowOwnerPID");
        var boundsKey = CreateKey("kCGWindowBounds");
        var layerKey = CreateKey("kCGWindowLayer");
        var alphaKey = CreateKey("kCGWindowAlpha");
        var windowList = CGWindowListCopyWindowInfo(
            WindowListOnScreenOnly | WindowListExcludeDesktopElements,
            NullWindowId);

        try
        {
            if (windowList == IntPtr.Zero) return result;
            var count = CFArrayGetCount(windowList);
            for (nint index = 0; index < count; index++)
            {
                var dictionary = CFArrayGetValueAtIndex(windowList, index);
                if (dictionary == IntPtr.Zero) continue;

                var processId = ReadInt(dictionary, ownerProcessKey);
                var layer = ReadInt(dictionary, layerKey);
                var alpha = ReadDouble(dictionary, alphaKey);
                if (processId <= 0 || processId == Environment.ProcessId || layer != 0 || alpha < .05) continue;

                var boundsDictionary = CFDictionaryGetValue(dictionary, boundsKey);
                if (boundsDictionary == IntPtr.Zero || !CGRectMakeWithDictionaryRepresentation(boundsDictionary, out var bounds)) continue;
                if (bounds.Size.Width < 280 || bounds.Size.Height < 180) continue;

                var number = ReadInt(dictionary, windowNumberKey);
                if (number <= 0) continue;
                result.Add(new MacHostWindow(
                    (uint)number,
                    processId,
                    bounds.Origin.X,
                    bounds.Origin.Y,
                    bounds.Size.Width,
                    bounds.Size.Height));
            }
        }
        finally
        {
            Release(windowList);
            Release(windowNumberKey);
            Release(ownerProcessKey);
            Release(boundsKey);
            Release(layerKey);
            Release(alphaKey);
        }

        return result;
    }

    private static IntPtr CreateKey(string value)
    {
        return CFStringCreateWithCString(IntPtr.Zero, value, Utf8Encoding);
    }

    private static int ReadInt(IntPtr dictionary, IntPtr key)
    {
        var number = CFDictionaryGetValue(dictionary, key);
        return number != IntPtr.Zero && CFNumberGetValueInt(number, CfNumberSInt32Type, out var value) ? value : 0;
    }

    private static double ReadDouble(IntPtr dictionary, IntPtr key)
    {
        var number = CFDictionaryGetValue(dictionary, key);
        return number != IntPtr.Zero && CFNumberGetValueDouble(number, CfNumberDoubleType, out var value) ? value : 0;
    }

    private static void Release(IntPtr value)
    {
        if (value != IntPtr.Zero) CFRelease(value);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly double X;
        public readonly double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize
    {
        public readonly double Width;
        public readonly double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly NativePoint Origin;
        public readonly NativeSize Size;
    }

    [DllImport(CoreGraphics)] private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);
    [DllImport(CoreGraphics)] private static extern uint CGMainDisplayID();
    [DllImport(CoreGraphics)] private static extern NativeRect CGDisplayBounds(uint display);
    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGRectMakeWithDictionaryRepresentation(IntPtr dictionary, out NativeRect bounds);

    [DllImport(CoreFoundation)] private static extern nint CFArrayGetCount(IntPtr array);
    [DllImport(CoreFoundation)] private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);
    [DllImport(CoreFoundation)] private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);
    [DllImport(CoreFoundation)] private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);
    [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr value);
    [DllImport(CoreFoundation, EntryPoint = "CFNumberGetValue")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFNumberGetValueInt(IntPtr number, int type, out int value);
    [DllImport(CoreFoundation, EntryPoint = "CFNumberGetValue")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFNumberGetValueDouble(IntPtr number, int type, out double value);

    [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
    [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern int objc_msgSend_Int32(IntPtr receiver, IntPtr selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_OrderWindow(IntPtr receiver, IntPtr selector, nint orderingMode, nint relativeWindowNumber);
}
