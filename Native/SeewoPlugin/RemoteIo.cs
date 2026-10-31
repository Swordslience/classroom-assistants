using System.Net.WebSockets;
using System.IO;
namespace ClassroomAI.Seewo;
internal static class RemoteIo {
 internal static async Task<WebSocketReceiveResult> Receive(WebSocket socket,byte[] buffer,CancellationToken token,TimeSpan timeout){using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(timeout);try{return await socket.ReceiveAsync(new ArraySegment<byte>(buffer),deadline.Token).ConfigureAwait(false);}catch(OperationCanceledException)when(!token.IsCancellationRequested){socket.Abort();throw new IOException("课堂连接接收超时，正在重新连接；不会自动重发AI请求。");}}
 internal static async Task Send(WebSocket socket,SemaphoreSlim gate,byte[] data,CancellationToken token,TimeSpan timeout){using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(timeout);bool acquired=false;try{await gate.WaitAsync(deadline.Token).ConfigureAwait(false);acquired=true;await socket.SendAsync(new ArraySegment<byte>(data),WebSocketMessageType.Text,true,deadline.Token).ConfigureAwait(false);}catch(OperationCanceledException)when(!token.IsCancellationRequested){socket.Abort();throw new IOException("课堂连接发送超时，正在重新连接；不会自动重发未确认请求。");}finally{if(acquired)gate.Release();}}
}
