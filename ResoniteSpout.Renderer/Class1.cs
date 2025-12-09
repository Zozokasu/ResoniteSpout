using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using InterprocessLib;
using Renderite.Unity;
using UnityEngine;
using ResoniteSpout.Shared;

namespace ResoniteSpoutRenderer
{
    [BepInPlugin("zozokasu.ResoniteSpout.Renderer", "ResoniteSpoutRenderer", "0.2.0")]
    public class ResoniteSpoutRenderer : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        private Messenger _msg;
        private readonly ConcurrentQueue<Action> _mainQueue = new();

        // ★ SpoutName → SpoutStruct のマッピング（複数管理）
        private static Dictionary<string, SpoutStruct> spouts = new();

        // ★ Receiver: SpoutName → SpoutReceiverStruct のマッピング
        private static Dictionary<string, SpoutReceiverStruct> receivers = new();
        
        public class SpoutStruct
        {
            public string SpoutName;
            public int AssetId;
            public IntPtr SpoutSender;
            public Texture2D SharedTexture;
            public int InitializationAttempts;
            
            public bool IsValid => SpoutSender != IntPtr.Zero;
            public bool IsReady => IsValid && SharedTexture != null;
            
            public bool TryCreateSharedTexture()
            {
                InitializationAttempts++;
                
                if (SpoutSender == IntPtr.Zero)
                {
                    Log.LogError($"[{SpoutName}] Sender is null, cannot create shared texture");
                    return false;
                }

                var ptr = PluginEntry.Sender_GetTexturePointer(SpoutSender);
                if (ptr == IntPtr.Zero)
                {
                    if (InitializationAttempts > 100)
                    {
                        Log.LogError($"[{SpoutName}] Failed to get texture pointer after {InitializationAttempts} attempts");
                        return false;
                    }
                    return false;
                }

                int width = PluginEntry.Sender_GetTextureWidth(SpoutSender);
                int height = PluginEntry.Sender_GetTextureHeight(SpoutSender);
                
                SharedTexture = Texture2D.CreateExternalTexture(
                    width,
                    height,
                    TextureFormat.ARGB32,  // ResoSpoutと同じ（Senderはカラー）
                    false,
                    false,
                    ptr);
                SharedTexture.hideFlags = HideFlags.DontSave;
                
                Log.LogInfo($"[{SpoutName}] Created shared texture {width}x{height} after {InitializationAttempts} attempts");
                return true;
            }
            
            public void Dispose()
            {
                if (SpoutSender != IntPtr.Zero)
                {
                    // PluginEntry.DestroySharedObject(SpoutSender);
                    SpoutSender = IntPtr.Zero;
                }

                if (SharedTexture != null)
                {
                    Destroy(SharedTexture);
                    SharedTexture = null;
                }

                Log.LogInfo($"[{SpoutName}] Disposed");
            }
        }

        // ★ Receiver 用の構造体
        public class SpoutReceiverStruct
        {
            public string SpoutName;       // 受信するSpoutソース名
            public int AssetId;            // 書き込み先 RenderTexture の AssetId
            public IntPtr SpoutReceiver;   // KlakSpout receiver ポインタ
            public Texture2D ReceivedTexture;  // 受信したテクスチャ
            public int InitializationAttempts;

            public bool IsValid => SpoutReceiver != IntPtr.Zero;
            public bool IsReady => IsValid && ReceivedTexture != null;

            public bool TryCreateReceivedTexture()
            {
                InitializationAttempts++;

                if (SpoutReceiver == IntPtr.Zero)
                {
                    Log.LogError($"[Receiver:{SpoutName}] Receiver is null, cannot create texture");
                    return false;
                }

                // Receiver を更新してテクスチャポインタを取得
                Util.IssueReceiverPluginEvent(PluginEntry.Event.Update, SpoutReceiver);

                // CheckValid でレシーバーの状態を確認
                bool isValid = PluginEntry.CheckValid(SpoutReceiver);

                var ptr = PluginEntry.Receiver_GetTexturePointer(SpoutReceiver);
                if (ptr == IntPtr.Zero)
                {
                    if (InitializationAttempts == 100 || InitializationAttempts % 300 == 0)
                    {
                        Log.LogWarning($"[Receiver:{SpoutName}] Still waiting for texture pointer (attempt {InitializationAttempts}, valid={isValid})");
                        Log.LogWarning($"[Receiver:{SpoutName}] Make sure the Spout source '{SpoutName}' is actively sending");
                    }
                    return false;
                }

                int width = PluginEntry.GetTextureWidth(SpoutReceiver);
                int height = PluginEntry.GetTextureHeight(SpoutReceiver);

                if (width <= 0 || height <= 0)
                {
                    if (InitializationAttempts % 60 == 0)
                    {
                        Log.LogWarning($"[Receiver:{SpoutName}] Invalid dimensions: {width}x{height}");
                    }
                    return false;
                }

                ReceivedTexture = Texture2D.CreateExternalTexture(
                    width,
                    height,
                    TextureFormat.R8,  // ResoSpoutと同じフォーマット
                    false,
                    false,
                    ptr);
                ReceivedTexture.hideFlags = HideFlags.DontSave;

                Log.LogInfo($"[Receiver:{SpoutName}] Created received texture {width}x{height} after {InitializationAttempts} attempts");
                return true;
            }

            public void UpdateReceivedTexture()
            {
                if (SpoutReceiver == IntPtr.Zero || ReceivedTexture == null)
                    return;

                var ptr = PluginEntry.Receiver_GetTexturePointer(SpoutReceiver);
                if (ptr != IntPtr.Zero)
                {
                    ReceivedTexture.UpdateExternalTexture(ptr);
                }
            }

            public void Dispose()
            {
                if (SpoutReceiver != IntPtr.Zero)
                {
                    // PluginEntry.DestroySharedObject(SpoutReceiver);
                    SpoutReceiver = IntPtr.Zero;
                }

                if (ReceivedTexture != null)
                {
                    Destroy(ReceivedTexture);
                    ReceivedTexture = null;
                }

                Log.LogInfo($"[Receiver:{SpoutName}] Disposed");
            }
        }

        void Awake()
        {
            Log = Logger;

            _msg = new Messenger("Zozokasu.ResoniteSpout", [typeof(SpoutCommand) ]);

            _msg.ReceiveObject<SpoutCommand>("SpoutCommand", (command) =>
            {
                _mainQueue.Enqueue(() => ProcessCommand(command));
                Log.LogInfo($"Command received: {command.Type}, Name: '{command.SpoutName}', AssetId: {command.AssetId}");
            });

            _msg.ReceiveString("DbgMessage", (s =>
            {
                Log.LogInfo($"[DEBUG]: {s}");
            }));

            Log.LogInfo($"[ResoniteSpoutRenderer] Initialized. Graphics: {SystemInfo.graphicsDeviceType}");
        }

        void Update()
        {
            // Spout DLL のポーリング
            //PluginEntry.Poll();

            // メインスレッドでコマンドを処理
            while (_mainQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Log.LogError($"Error processing command: {e}"); }
            }

            SendRenderTextures();
            ReceiveToRenderTextures();
        }
        
        void OnDestroy()
        {
            foreach (var spout in spouts.Values)
            {
                spout.Dispose();
            }
            spouts.Clear();

            foreach (var receiver in receivers.Values)
            {
                receiver.Dispose();
            }
            receivers.Clear();
        }
        
        private void ProcessCommand(SpoutCommand command)
        {
            switch (command.Type)
            {
                // Sender commands
                case SpoutCommandType.Create:
                    CreateSpout(command.SpoutName, command.AssetId);
                    break;

                case SpoutCommandType.Update:
                    UpdateSpout(command.SpoutName, command.AssetId);
                    break;

                case SpoutCommandType.Delete:
                    DeleteSpout(command.SpoutName);
                    break;

                // Receiver commands
                case SpoutCommandType.ReceiverCreate:
                    CreateReceiver(command.SpoutName, command.AssetId);
                    break;

                case SpoutCommandType.ReceiverUpdate:
                    UpdateReceiver(command.SpoutName, command.AssetId);
                    break;

                case SpoutCommandType.ReceiverDelete:
                    DeleteReceiver(command.SpoutName);
                    break;
            }
        }
        
        private void CreateSpout(string spoutName, int assetId)
        {
            Log.LogInfo($"[{spoutName}] CreateSpout called with AssetId: {assetId}");
            
            // ★ 既に存在する場合は削除してから作り直す
            if (spouts.ContainsKey(spoutName))
            {
                Log.LogInfo($"[{spoutName}] Already exists, recreating...");
                spouts[spoutName].Dispose();
                spouts.Remove(spoutName);
            }
            
            // RenderTexture を取得
            var rtAsset = RenderingManager.Instance.RenderTextures.GetAsset(assetId);
            if (rtAsset?.Texture == null)
            {
                Log.LogError($"[{spoutName}] RenderTexture not found for AssetId: {assetId}");
                return;
            }
            
            RenderTexture texture = rtAsset.Texture;
            Log.LogInfo($"[{spoutName}] RenderTexture found: {texture.width}x{texture.height}");
            
            // Spout Sender を作成
            Log.LogInfo($"[{spoutName}] Calling CreateSender('{spoutName}', {texture.width}, {texture.height})...");
            IntPtr sender;
            try
            {
                sender = PluginEntry.CreateSender(spoutName, texture.width, texture.height);
            }
            catch (Exception ex)
            {
                Log.LogError($"[{spoutName}] CreateSender threw exception: {ex}");
                return;
            }

            if (sender == IntPtr.Zero)
            {
                Log.LogError($"[{spoutName}] Failed to create Spout sender (returned IntPtr.Zero)");
                return;
            }
            
            Log.LogInfo($"[{spoutName}] Spout sender created: {sender}");

            // ★ 初期化のために Update イベントを発行
            Util.IssueSenderPluginEvent(PluginEntry.Event.Update, sender);
            
            // SpoutStruct を作成
            var spout = new SpoutStruct
            {
                SpoutName = spoutName,
                AssetId = assetId,
                SpoutSender = sender,
                InitializationAttempts = 0
            };
            
            spouts[spoutName] = spout;
            Log.LogInfo($"[{spoutName}] Added to spouts dictionary. Total spouts: {spouts.Count}");
        }
        
        private void UpdateSpout(string spoutName, int assetId)
        {
            if (!spouts.ContainsKey(spoutName))
            {
                Log.LogWarning($"[{spoutName}] Not found for update, creating new...");
                CreateSpout(spoutName, assetId);
                return;
            }
            
            var spout = spouts[spoutName];
            
            // AssetId が同じ場合はスキップ
            if (spout.AssetId == assetId)
            {
                Log.LogInfo($"[{spoutName}] AssetId unchanged ({assetId}), skipping update");
                return;
            }
            
            Log.LogInfo($"[{spoutName}] Updating: AssetId {spout.AssetId} → {assetId}");
            
            // 作り直す
            CreateSpout(spoutName, assetId);
        }
        
        private void DeleteSpout(string spoutName)
        {
            if (spouts.ContainsKey(spoutName))
            {
                spouts[spoutName].Dispose();
                spouts.Remove(spoutName);
                Log.LogInfo($"[{spoutName}] Deleted. Remaining spouts: {spouts.Count}");
            }
            else
            {
                Log.LogWarning($"[{spoutName}] Not found for deletion");
            }
        }

        // ★ Receiver 用メソッド
        private void CreateReceiver(string spoutName, int assetId)
        {
            Log.LogInfo($"[Receiver:{spoutName}] CreateReceiver called with AssetId: {assetId}");

            // 利用可能なSpoutソースをスキャン
            ScanAndLogAvailableSpoutSources();

            // 既に存在する場合は削除してから作り直す
            if (receivers.ContainsKey(spoutName))
            {
                Log.LogInfo($"[Receiver:{spoutName}] Already exists, recreating...");
                receivers[spoutName].Dispose();
                receivers.Remove(spoutName);
            }

            // Spout Receiver を作成
            IntPtr receiver = PluginEntry.CreateReceiver(spoutName);

            if (receiver == IntPtr.Zero)
            {
                Log.LogError($"[Receiver:{spoutName}] Failed to create Spout receiver");
                return;
            }

            Log.LogInfo($"[Receiver:{spoutName}] Spout receiver created: {receiver}");

            // 初期化のために Update イベントを発行
            Util.IssueReceiverPluginEvent(PluginEntry.Event.Update, receiver);

            // SpoutReceiverStruct を作成
            var receiverStruct = new SpoutReceiverStruct
            {
                SpoutName = spoutName,
                AssetId = assetId,
                SpoutReceiver = receiver,
                InitializationAttempts = 0
            };

            receivers[spoutName] = receiverStruct;
            Log.LogInfo($"[Receiver:{spoutName}] Added to receivers dictionary. Total receivers: {receivers.Count}");
        }

        // 利用可能なSpoutソースをスキャンしてログに出力
        private void ScanAndLogAvailableSpoutSources()
        {
            int count = PluginEntry.ScanSharedObjects();
            Log.LogInfo($"[Spout] Found {count} available Spout sources:");
            for (int i = 0; i < count; i++)
            {
                string name = PluginEntry.GetSharedObjectNameString(i);
                Log.LogInfo($"  [{i}] '{name}'");
            }
        }

        private void UpdateReceiver(string spoutName, int assetId)
        {
            if (!receivers.ContainsKey(spoutName))
            {
                Log.LogWarning($"[Receiver:{spoutName}] Not found for update, creating new...");
                CreateReceiver(spoutName, assetId);
                return;
            }

            var receiver = receivers[spoutName];

            // AssetId が同じ場合はスキップ
            if (receiver.AssetId == assetId)
            {
                Log.LogInfo($"[Receiver:{spoutName}] AssetId unchanged ({assetId}), skipping update");
                return;
            }

            Log.LogInfo($"[Receiver:{spoutName}] Updating: AssetId {receiver.AssetId} → {assetId}");

            // AssetId だけ更新（Spout 名が同じなら receiver は再作成不要）
            receiver.AssetId = assetId;
        }

        private void DeleteReceiver(string spoutName)
        {
            if (receivers.ContainsKey(spoutName))
            {
                receivers[spoutName].Dispose();
                receivers.Remove(spoutName);
                Log.LogInfo($"[Receiver:{spoutName}] Deleted. Remaining receivers: {receivers.Count}");
            }
            else
            {
                Log.LogWarning($"[Receiver:{spoutName}] Not found for deletion");
            }
        }

        private void SendRenderTextures()
        {
            if (spouts.Count == 0)
                return;
            
            foreach (var kvp in spouts)
            {
                var spoutName = kvp.Key;
                var spout = kvp.Value;
                
                try
                {
                    if (!spout.IsValid)
                    {
                        continue;
                    }
                    
                    var rtAsset = RenderingManager.Instance.RenderTextures.GetAsset(spout.AssetId);
                    if (rtAsset?.Texture == null)
                    {
                        if (spout.InitializationAttempts == 1)
                        {
                            Log.LogWarning($"[{spoutName}] RenderTexture not available for AssetId: {spout.AssetId}");
                        }
                        continue;
                    }
                    
                    // ★ ResoSpoutと同じ順序: まずUpdateを呼ぶ
                    Util.IssueSenderPluginEvent(PluginEntry.Event.Update, spout.SpoutSender);

                    // Shared Texture がまだない場合は作成を試みる（Updateの後）
                    if (spout.SharedTexture == null)
                    {
                        if (spout.InitializationAttempts < 5 || spout.InitializationAttempts % 60 == 0)
                        {
                            Log.LogInfo($"[{spoutName}] Attempting to create shared texture (attempt {spout.InitializationAttempts})...");
                        }

                        if (!spout.TryCreateSharedTexture())
                        {
                            continue;
                        }
                    }

                    RenderTexture source = rtAsset.Texture;

                    // Blit with vertical flip
                    var tempRt = RenderTexture.GetTemporary(
                        spout.SharedTexture.width,
                        spout.SharedTexture.height,
                        0,
                        RenderTextureFormat.ARGB32);

                    Graphics.Blit(source, tempRt, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
                    Graphics.CopyTexture(tempRt, spout.SharedTexture);
                    RenderTexture.ReleaseTemporary(tempRt);
                }
                catch (Exception e)
                {
                    Log.LogError($"[{spoutName}] Error sending texture: {e}");
                }
            }
        }

        // ★ Spout から受信して RenderTexture に書き込む
        private void ReceiveToRenderTextures()
        {
            if (receivers.Count == 0)
                return;

            foreach (var kvp in receivers)
            {
                var spoutName = kvp.Key;
                var receiver = kvp.Value;

                try
                {
                    if (!receiver.IsValid)
                    {
                        continue;
                    }

                    // Receiver を更新
                    Util.IssueReceiverPluginEvent(PluginEntry.Event.Update, receiver.SpoutReceiver);

                    // Received Texture がまだない場合は作成を試みる
                    if (receiver.ReceivedTexture == null)
                    {
                        if (receiver.InitializationAttempts < 5 || receiver.InitializationAttempts % 60 == 0)
                        {
                            Log.LogInfo($"[Receiver:{spoutName}] Attempting to create received texture (attempt {receiver.InitializationAttempts})...");
                        }

                        if (!receiver.TryCreateReceivedTexture())
                        {
                            continue;
                        }
                    }
                    else
                    {
                        // 既存のテクスチャを更新
                        receiver.UpdateReceivedTexture();
                    }

                    // 書き込み先の RenderTexture を取得
                    var rtAsset = RenderingManager.Instance.RenderTextures.GetAsset(receiver.AssetId);
                    if (rtAsset?.Texture == null)
                    {
                        if (receiver.InitializationAttempts == 1)
                        {
                            Log.LogWarning($"[Receiver:{spoutName}] Target RenderTexture not available for AssetId: {receiver.AssetId}");
                        }
                        continue;
                    }

                    RenderTexture target = rtAsset.Texture;

                    // Blit with vertical flip (Spout は上下反転している)
                    Graphics.Blit(receiver.ReceivedTexture, target, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
                }
                catch (Exception e)
                {
                    Log.LogError($"[Receiver:{spoutName}] Error receiving texture: {e}");
                }
            }
        }
    }
}