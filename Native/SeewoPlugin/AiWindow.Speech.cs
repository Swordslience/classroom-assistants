using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Windows.Threading;

namespace ClassroomAI.Seewo;
internal sealed partial class AiWindow {
 TaskCompletionSource<bool>? playFinished;
 static string CacheFolder()=>RuntimeFiles.Root;
 async Task<string> Synthesize(string text,CancellationToken token){
  if(string.IsNullOrWhiteSpace(config.VoiceApiKey))throw new InvalidOperationException("文字已显示，尚未配置豆包语音Key。");
  text=Regex.Replace(text,@"[#*_`|]","");
  using var request=new HttpRequestMessage(HttpMethod.Post,"https://openspeech.bytedance.com/api/v3/tts/unidirectional");request.Headers.Add("X-Api-Key",config.VoiceApiKey);request.Headers.Add("X-Api-Resource-Id","seed-tts-2.0");request.Headers.Add("X-Api-Request-Id",Guid.NewGuid().ToString());
  request.Content=new StringContent(JsonSerializer.Serialize(new{user=new{uid="nailong-classroom"},req_params=new{text,speaker=config.VoiceSpeaker,audio_params=new{format="pcm",sample_rate=24000,speech_rate=0}}}),Encoding.UTF8,"application/json");
  using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);if(!response.IsSuccessStatusCode)throw new InvalidOperationException("豆包语音 HTTP "+(int)response.StatusCode+"；文字回答已保留。");
  using var stream=await response.Content.ReadAsStreamAsync(token);using var reader=new StreamReader(stream);using var pcm=new MemoryStream();bool complete=false;string? line;
  while((line=await reader.ReadLineAsync().WaitAsync(token))is not null){line=line.Trim();if(line.StartsWith("data:"))line=line[5..].Trim();if(line.Length==0||line=="[DONE]")continue;using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
   if(root.TryGetProperty("code",out var code)){long value=code.GetInt64();if(value==20000000)complete=true;else if(value!=0)throw new InvalidOperationException("豆包语音服务返回代码 "+value);}
   if(root.TryGetProperty("data",out var data)&&data.ValueKind==JsonValueKind.String){byte[] bytes=Convert.FromBase64String(data.GetString()!);pcm.Write(bytes,0,bytes.Length);}
  }
  if(!complete||pcm.Length==0)throw new InvalidOperationException("语音未接收完整，可重试朗读。");token.ThrowIfCancellationRequested();
  string path=Path.Combine(CacheFolder(),"voice_"+Guid.NewGuid().ToString("N")+".wav");WriteWave(path,pcm.ToArray());return path;
 }
 static void WriteWave(string file,byte[] pcm){using var f=File.Create(file);using var writer=new BinaryWriter(f);writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+pcm.Length);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(24000);writer.Write(48000);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(pcm.Length);writer.Write(pcm);}
 async Task PlayFile(string file,CancellationToken token){
  token.ThrowIfCancellationRequested();StopPlayback();playbackAllowed=true;var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);playFinished=completion;audioPath=file;player.Open(new Uri(file));
  using var registration=token.Register(()=>Dispatcher.BeginInvoke(new Action(()=>{if(ReferenceEquals(playFinished,completion))StopPlayback();})));
  await completion.Task.WaitAsync(token);
 }
 async Task SpeakText(string text,CancellationToken token){if(string.IsNullOrWhiteSpace(text))throw new InvalidOperationException("暂无可朗读内容。");using var pipeline=new SpeechPipeline(this,token);pipeline.Append(text);pipeline.Complete();await pipeline.Finished;}
 sealed class SpeechPipeline:IDisposable {
  readonly AiWindow owner;readonly CancellationToken token;readonly CancellationTokenSource lifetime;readonly Channel<string> queue=Channel.CreateUnbounded<string>(new UnboundedChannelOptions{SingleReader=true,SingleWriter=true});readonly SpeechBuffer buffer=new();readonly List<string> files=new();
  public Task Finished{get;}bool completed;
  public SpeechPipeline(AiWindow owner,CancellationToken token){this.owner=owner;lifetime=CancellationTokenSource.CreateLinkedTokenSource(token);this.token=lifetime.Token;Finished=Consume();}
  public void Append(string piece){if(completed)return;buffer.Append(piece);foreach(string part in buffer.Drain(false))queue.Writer.TryWrite(part);}
  public void Complete(){if(completed)return;completed=true;foreach(string part in buffer.Drain(true))queue.Writer.TryWrite(part);queue.Writer.TryComplete();}
  public void Cancel(){lifetime.Cancel();queue.Writer.TryComplete();}
  public void Dispose(){lifetime.Dispose();}
  async Task Consume(){
   await foreach(string text in queue.Reader.ReadAllAsync(token)){token.ThrowIfCancellationRequested();string file=await owner.Synthesize(text,token);files.Add(file);await owner.PlayFile(file,token);}
   if(files.Count>1){using var pcm=new MemoryStream();foreach(var file in files){byte[] wav=await File.ReadAllBytesAsync(file,token);pcm.Write(wav,44,wav.Length-44);}string joined=Path.Combine(CacheFolder(),"voice_joined_"+Guid.NewGuid().ToString("N")+".wav");WriteWave(joined,pcm.ToArray());owner.audioPath=joined;}
  }
 }
}
