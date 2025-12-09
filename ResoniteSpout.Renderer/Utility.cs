// KlakSpout - Spout video frame sharing plugin for Unity
// https://github.com/keijiro/KlakSpout

using ResoniteSpoutRenderer;
using UnityEngine;
using UnityEngine.Rendering;

namespace ResoniteSpoutRenderer
{
    // Internal utilities
    static class Util
    {
        internal static void Destroy(UnityEngine.Object obj)
        {
            if (obj == null) return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }

        static CommandBuffer _senderCommandBuffer;
        static CommandBuffer _receiverCommandBuffer;

        // Sender用（KlakSpout_send.dll を使用）
        internal static void IssueSenderPluginEvent(PluginEntry.Event pluginEvent, System.IntPtr ptr)
        {
            if (_senderCommandBuffer == null) _senderCommandBuffer = new CommandBuffer();

            _senderCommandBuffer.IssuePluginEventAndData(
                PluginEntry.Sender_GetRenderEventFunc(), (int)pluginEvent, ptr
            );

            Graphics.ExecuteCommandBuffer(_senderCommandBuffer);

            _senderCommandBuffer.Clear();
        }

        // Receiver用（KlakSpout.dll を使用）
        internal static void IssueReceiverPluginEvent(PluginEntry.Event pluginEvent, System.IntPtr ptr)
        {
            if (_receiverCommandBuffer == null) _receiverCommandBuffer = new CommandBuffer();

            _receiverCommandBuffer.IssuePluginEventAndData(
                PluginEntry.Receiver_GetRenderEventFunc(), (int)pluginEvent, ptr
            );

            Graphics.ExecuteCommandBuffer(_receiverCommandBuffer);

            _receiverCommandBuffer.Clear();
        }
    }
}