namespace ClassroomAI.Seewo;

// Results belong to the request that produced them, never to a shared error field.
internal sealed record OperationOutcome(string Status,string Message,string? Error=null) {
 internal static async Task<OperationOutcome> Capture(Func<Task> action,CancellationToken token,Func<bool> isCurrent,Func<string> message,Func<Exception,string> friendly) {
  try {
   await action();
   if(!isCurrent())return new("superseded","已由新任务替代。");
   if(token.IsCancellationRequested)return new("cancelled","已停止本次请求和播放。");
   return new("done",message());
  } catch(OperationCanceledException) {
   return isCurrent()?new("cancelled","已停止本次请求和播放。"):new("superseded","已由新任务替代。");
  } catch(Exception error) {
   if(!isCurrent())return new("superseded","已由新任务替代。");
   string text=friendly(error);return new("error",text,text);
  }
 }
}
