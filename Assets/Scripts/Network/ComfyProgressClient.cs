using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Network
{
    /// <summary>요청별 소켓. 워커는 Unity API를 호출하지 않고 제한된 큐로 이벤트만 전달한다.</summary>
    public sealed class ComfyProgressClient
    {
        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private readonly CancellationTokenSource _lifetime;
        private readonly Queue<JObject> _events = new Queue<JObject>();
        private Task _receiver;
        private Exception _failure;

        public ComfyProgressClient(CancellationToken token)
        {
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        }

        public async Task ConnectAsync(Uri server, string clientId)
        {
            var address = new UriBuilder(server) { Scheme = "ws", Path = "/ws", Query = "clientId=" + Uri.EscapeDataString(clientId) };
            using (var connection = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
            {
                connection.CancelAfter(TimeSpan.FromSeconds(5));
                await _socket.ConnectAsync(address.Uri, connection.Token);
            }
            _receiver = ReceiveAsync();
        }

        public bool TryRead(out JObject message)
        {
            lock (_events)
            {
                if (_events.Count > 0)
                {
                    message = _events.Dequeue();
                    return true;
                }
                if (_failure != null)
                    throw new IOException("ComfyUI progress connection failed.", _failure);
            }
            message = null;
            return false;
        }

        private async Task ReceiveAsync()
        {
            var buffer = new byte[8192];
            try
            {
                while (!_lifetime.IsCancellationRequested)
                {
                    using (var message = new MemoryStream())
                    {
                        WebSocketReceiveResult part;
                        do
                        {
                            part = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _lifetime.Token).ConfigureAwait(false);
                            if (part.MessageType == WebSocketMessageType.Close)
                                throw new IOException("ComfyUI closed the progress connection.");
                            if (part.MessageType == WebSocketMessageType.Text)
                            {
                                if (message.Length + part.Count > 262144)
                                    throw new IOException("ComfyUI progress message exceeded the size limit.");
                                message.Write(buffer, 0, part.Count);
                            }
                        } while (!part.EndOfMessage);

                        if (part.MessageType != WebSocketMessageType.Text)
                            continue; // 미리보기 바이너리는 보관하지 않는다.
                        var data = JObject.Parse(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                        lock (_events)
                        {
                            if (_events.Count == 128)
                                _events.Dequeue();
                            _events.Enqueue(data);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                if (!_lifetime.IsCancellationRequested)
                    lock (_events) _failure = exception;
            }
        }

        public async Task StopAsync()
        {
            _lifetime.Cancel();
            _socket.Abort();
            if (_receiver != null)
                await _receiver;
            _socket.Dispose();
            _lifetime.Dispose();
            lock (_events) _events.Clear();
        }

        // 전체 시간 비율이 아니라 현재 고정 워크플로의 작업 단계 비율이다.
        public static float GetProgress(JObject message, string promptId)
        {
            var data = message["data"] as JObject;
            if (data == null || (string)data["prompt_id"] != promptId)
                return 0;
            switch ((string)message["type"])
            {
                case "execution_error":
                case "execution_interrupted":
                    throw new InvalidOperationException("ComfyUI execution failed: " + data);
                case "execution_start": return 0.05f;
                case "executing": return NodeProgress((string)data["node"], 0);
                case "execution_cached":
                    float cached = 0;
                    if (data["nodes"] is JArray nodes)
                        foreach (var node in nodes)
                            cached = Math.Max(cached, NodeProgress((string)node, 1));
                    return cached;
                case "progress":
                    return NodeProgress((string)data["node"], Fraction(data));
                case "progress_state":
                    float progress = 0;
                    if (data["nodes"] is JObject states)
                        foreach (var entry in states.Properties())
                        {
                            var state = entry.Value as JObject;
                            if (state == null) continue;
                            progress = Math.Max(progress, NodeProgress(entry.Name,
                                (string)state["state"] == "finished" ? 1 : Fraction(state)));
                        }
                    return progress;
                case "execution_success": return 0.96f;
                default: return 0;
            }
        }

        private static float Fraction(JObject data)
        {
            float maximum = (float?)data["max"] ?? 0;
            float value = (float?)data["value"] ?? 0;
            if (float.IsNaN(value) || float.IsInfinity(value) || maximum <= 0 || float.IsInfinity(maximum)) return 0;
            return Math.Max(0, Math.Min(1, value / maximum));
        }

        private static float NodeProgress(string node, float fraction)
        {
            switch (node)
            {
                case "1": case "2": case "3": return 0.05f + 0.03f * fraction;
                case "4": case "5": case "6": case "7": case "8": case "9": case "10": return 0.08f + 0.07f * fraction;
                case "11": return 0.15f + 0.65f * fraction;
                case "12": return 0.80f + 0.10f * fraction;
                case "13": return 0.90f + 0.05f * fraction;
                default: return 0;
            }
        }
    }
}
