// KlakSpout - Spout video frame sharing plugin for Unity
// https://github.com/keijiro/KlakSpout

using UnityEngine;
using System.Runtime.InteropServices;

namespace ResoniteSpoutRenderer
{
    static class PluginEntry
    {
        //#region Plugin polling

        //private static int _lastUpdateFrame = -1;
        //public static void Poll()
        //{
        //    if (Time.frameCount != _lastUpdateFrame)
        //    {
        //        GL.IssuePluginEvent(GetRenderEventFunc(), 0);
        //        _lastUpdateFrame = Time.frameCount;
        //    }
        //}
        //#endregion

        internal enum Event { Update, Dispose }

        internal static bool IsAvailable
        {
            get
            {
                return SystemInfo.graphicsDeviceType ==
                    UnityEngine.Rendering.GraphicsDeviceType.Direct3D11;
            }
        }

        // ========== Sender functions (KlakSpout_send.dll) ==========
        [DllImport(@"KlakSpout_send.dll", EntryPoint = "GetRenderEventFunc")]
        internal static extern System.IntPtr Sender_GetRenderEventFunc();

        [DllImport(@"KlakSpout_send.dll", EntryPoint = "CreateSender")]
        internal static extern System.IntPtr CreateSender(string name, int width, int height);

        [DllImport(@"KlakSpout_send.dll", EntryPoint = "GetTexturePointer")]
        internal static extern System.IntPtr Sender_GetTexturePointer(System.IntPtr ptr);

        [DllImport(@"KlakSpout_send.dll", EntryPoint = "GetTextureWidth")]
        internal static extern int Sender_GetTextureWidth(System.IntPtr ptr);

        [DllImport(@"KlakSpout_send.dll", EntryPoint = "GetTextureHeight")]
        internal static extern int Sender_GetTextureHeight(System.IntPtr ptr);

        // ========== Receiver functions (KlakSpout.dll) ==========
        [DllImport(@"KlakSpout.dll", EntryPoint = "GetRenderEventFunc")]
        internal static extern System.IntPtr Receiver_GetRenderEventFunc();

        [DllImport(@"KlakSpout.dll", EntryPoint = "CreateReceiver")]
        internal static extern System.IntPtr CreateReceiver(string name);

        [DllImport(@"KlakSpout.dll", EntryPoint = "GetTexturePointer")]
        internal static extern System.IntPtr Receiver_GetTexturePointer(System.IntPtr ptr);

        [DllImport(@"KlakSpout.dll", EntryPoint = "GetTextureWidth")]
        internal static extern int GetTextureWidth(System.IntPtr ptr);

        [DllImport(@"KlakSpout.dll", EntryPoint = "GetTextureHeight")]
        internal static extern int GetTextureHeight(System.IntPtr ptr);

        [DllImport(@"KlakSpout.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CheckValid(System.IntPtr ptr);

        [DllImport(@"KlakSpout.dll")]
        internal static extern int ScanSharedObjects();

        [DllImport(@"KlakSpout.dll")]
        internal static extern System.IntPtr GetSharedObjectName(int index);

        internal static string GetSharedObjectNameString(int index)
        {
            var ptr = GetSharedObjectName(index);
            return ptr != System.IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
        }

    }
}