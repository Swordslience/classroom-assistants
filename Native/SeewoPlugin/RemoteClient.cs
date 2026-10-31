using System.Net.Http;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ClassroomAiBridge;
using Cvte.EasiNote;
using QRCoder;

namespace ClassroomAI.Seewo;

internal sealed class RemoteClient {
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 static RemoteClient? instance;
 public static RemoteClient Instance => instance ??= new RemoteClient();
 readonly SemaphoreSlim sending=new(1,1);
 readonly string sessionId=Guid.NewGuid().ToString("N");
 readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(15)};
 readonly Dispatcher ui=Application.Current.Dispatcher;
 readonly DispatcherTimer watch=new(){Interval=TimeSpan.FromSeconds(2)};
 CancellationTokenSource? stop;
 ClientWebSocket? socket;
 RemoteSettings settings;
 string lastState="",lastError="",pairCode="";
 DateTime lastHeartbeat=DateTime.MinValue;
 string? teacherUid;
 string identityEpoch=Guid.NewGuid().ToString("N");
 readonly AsyncLocal<string?> commandEpoch=new();
 public static void Start()=>Instance.Connect();
 RemoteClient(){settings=RemoteSettings.Load();watch.Tick+=async(_,_)=>{try{await SendState();}catch(Exception e){lastError=e.Message;}};watch.Start();Application.Current.Exit+=(_,_)=>{stop?.Cancel();socket?.Abort();};
  try{EN.Account.UserLogout+=(_,_)=>TeacherChanged();EN.Account.UserLogined+=(_,_)=>TeacherChanged();}catch(Exception e){lastError="身份事件暂不可用："+e.GetType().Name;}
 }
 void TeacherChanged()=>ui.BeginInvoke(new Action(async()=>{try{RefreshTeacher(true);await SendState(true);}catch(Exception e){lastError="身份更新未完成："+e.GetType().Name;}}));
 TeacherIdentity RefreshTeacher(bool force=false){
  ui.VerifyAccess();var account=SeewoIdentity.Read();string uid=account?.Uid??"";
  if(force||teacherUid!=uid){teacherUid=uid;identityEpoch=Guid.NewGuid().ToString("N");lastState="";AiWindow.ResetTeacherSession();}
  return new TeacherIdentity(uid,account?.Name??"",account is not null,identityEpoch);
 }
 void Connect(){
  stop?.Cancel();socket?.Abort();stop=new CancellationTokenSource();lastState="";lastError="";
  if(string.IsNullOrWhiteSpace(settings.ServerUrl))return;
  _=Loop(stop.Token);
 }
 static Uri ValidBase(string value){
  var uri=new Uri(value.Trim().TrimEnd('/')+"/");
  if(uri.Scheme!="https"&&!(uri.Scheme=="http"&&(uri.IsLoopback||uri.Host.StartsWith("192.168.")||uri.Host.StartsWith("10."))))
   throw new InvalidOperationException("正式服务使用HTTPS；本机或内网联调可使用HTTP。");
  if(uri.AbsolutePath!="/"||!string.IsNullOrEmpty(uri.UserInfo))throw new InvalidOperationException("请填写服务根域名，不附带路径或账号密码。");
  return uri;
 }
 async Task<string> Register(CancellationToken token){
  var baseUri=ValidBase(settings.ServerUrl);
  var identity=await ui.InvokeAsync(()=>RefreshTeacher());
  using var content=new StringContent(JsonSerializer.Serialize(new{id=settings.DeviceId,secret=settings.DeviceSecret,name=settings.DeviceName,sessionId,identity=new{uid=identity.Uid,name=identity.Name,authenticated=identity.Authenticated,epoch=identity.Epoch}}),Encoding.UTF8,"application/json");
  using var response=await http.PostAsync(new Uri(baseUri,"api/device/register"),content,token);
  if(!response.IsSuccessStatusCode)throw new InvalidOperationException("设备连接HTTP "+(int)response.StatusCode);
  using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
  pairCode=doc.RootElement.GetProperty("code").GetString()??"";return pairCode;
 }
 async Task Loop(CancellationToken token){
  while(!token.IsCancellationRequested){
   ClientWebSocket? attempt=null;
   try{
    await Register(token);
    token.ThrowIfCancellationRequested();
    using var ws=new ClientWebSocket();attempt=ws;socket=ws;ws.Options.KeepAliveInterval=TimeSpan.FromSeconds(15);
    var url=new UriBuilder(ValidBase(settings.ServerUrl)){Scheme=ValidBase(settings.ServerUrl).Scheme=="https"?"wss":"ws",Path="/ws"}.Uri;
    await ws.ConnectAsync(url,token);
    await Send(new{type="hello",role="device",deviceId=settings.DeviceId,secret=settings.DeviceSecret,sessionId},token);
    lastError="";lastState="";
    while(ws.State==WebSocketState.Open&&!token.IsCancellationRequested){
     using var buffer=new MemoryStream();var bytes=new byte[8192];WebSocketReceiveResult r;
     do{r=await RemoteIo.Receive(ws,bytes,token,TimeSpan.FromSeconds(40));if(r.MessageType==WebSocketMessageType.Close)break;buffer.Write(bytes,0,r.Count);if(buffer.Length>6*1024*1024)throw new IOException("远程消息过大。");}while(!r.EndOfMessage);
     if(r.MessageType==WebSocketMessageType.Close)break;
     using var doc=JsonDocument.Parse(buffer.ToArray());var m=doc.RootElement.Clone();
     if(m.GetProperty("type").GetString()=="ready")await ui.InvokeAsync(async()=>{await SendState(true);await Send(new{type="ui_state",ui=AiWindow.UiState()});});
     else if(m.GetProperty("type").GetString()=="command")ui.BeginInvoke(new Action(async()=>await Execute(m)));
    }
   }catch(OperationCanceledException)when(token.IsCancellationRequested){break;}
    catch(Exception e){if(!token.IsCancellationRequested)lastError=e.Message;}
    finally{if(ReferenceEquals(socket,attempt))socket=null;}
   if(!token.IsCancellationRequested)try{await Task.Delay(4000,token);}catch(OperationCanceledException){break;}
  }
 }
 public async Task Send(object message,CancellationToken token=default){
  var ws=socket;if(ws?.State!=WebSocketState.Open)return;
  var data=await ui.InvokeAsync(()=>{
   var identity=RefreshTeacher();
   var envelope=JsonSerializer.SerializeToNode(message)!.AsObject();
   if((envelope["type"]?.GetValue<string>() is "result" or "input_changed")&&commandEpoch.Value is string scope&&scope!=identity.Epoch)return null;
   if(envelope["identityEpoch"] is JsonNode expected&&expected.GetValue<string>()!=identity.Epoch)return null;
   if(envelope["type"]?.GetValue<string>()=="state"&&envelope["state"]?["identity"]?["epoch"]?.GetValue<string>()!=identity.Epoch)return null;
   if(envelope["type"]?.GetValue<string>()=="ui_state")envelope["ui"]=JsonSerializer.SerializeToNode(AiWindow.UiState());
   envelope["identityEpoch"]=identity.Epoch;return Encoding.UTF8.GetBytes(envelope.ToJsonString());
  });
  if(data is null)return;
  try{if(ws.State==WebSocketState.Open)await RemoteIo.Send(ws,sending,data,token,TimeSpan.FromSeconds(12));}
  catch(IOException e){lastError=e.Message;ws.Abort();}
  catch(WebSocketException e){lastError=e.Message;ws.Abort();}
 }
 async Task SendState(bool force=false){
  var identity=RefreshTeacher();
  if(socket?.State!=WebSocketState.Open)return;
  object state;
  var account=new{uid=identity.Uid,name=identity.Name,authenticated=identity.Authenticated,epoch=identity.Epoch};
  try{var p=CurrentPage.Resolve();state=new{available=true,title=p.Host.Title,page=p.Index+1,count=p.Api.SlideCount,display=p.IsDisplay,foreground=new WindowInteropHelper(p.Host).Handle==GetForegroundWindow(),identity=account};}
  catch(InvalidOperationException){state=new{available=false,title="",page=0,count=0,display=false,foreground=false,identity=account};}
  string currentState=JsonSerializer.Serialize(state);
  if(force||currentState!=lastState||(DateTime.UtcNow-lastHeartbeat).TotalSeconds>=8){lastState=currentState;lastHeartbeat=DateTime.UtcNow;await Send(new{type="state",state});}
 }
 async Task Execute(JsonElement m){
  string id=m.GetProperty("id").GetString()??"",action=m.GetProperty("action").GetString()??"";
  try{
   if(m.GetProperty("expires").GetInt64()<DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())throw new InvalidOperationException("命令已过期，未执行。");
   var identity=RefreshTeacher();
   if(!TeacherCommandGuard.Allows(identity,m,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))throw new InvalidOperationException("当前希沃账号与网页绑定身份不一致，未执行。请使用对应老师的网页账号。");
   commandEpoch.Value=identity.Epoch;
   var p=CurrentPage.Resolve(); // Never fall through into another app or a hidden editor.
   if(action=="next"||action=="previous"){
    var command=action=="next"?ENCommands.PlayNext:ENCommands.PlayPrevious;
    p.Host.Activate();
    if(!command.CanExecute(null))throw new InvalidOperationException("希沃当前状态不允许切换步骤。");
    command.Execute(null);await Send(new{type="result",id,action,status="done",message="已执行希沃上一步/下一步。"});await SendState(true);return;
   }
   if(action=="goto"){
    int index=m.GetProperty("page").GetInt32()-1;
    if(index<0||index>=p.Api.SlideCount)throw new InvalidOperationException("目标页超出当前课件。");
    p.Api.GoToSlide(index);p.Host.Activate();await SendState(true);
    await Send(new{type="result",id,action,status="done",message="已跳转到第"+(index+1)+"页。"});return;
   }
   await AiWindow.Remote(m,this);
  }catch(Exception e){await Send(new{type="result",id,action,status="error",error=e.Message});}
  finally{commandEpoch.Value=null;}
 }
 public async void Pairing(Window owner){
  var panel=new System.Windows.Controls.StackPanel{Margin=new Thickness(20)};
  var url=new System.Windows.Controls.TextBox{Text=settings.ServerUrl,FontSize=20,MinWidth=480};
  var label=new System.Windows.Controls.TextBlock{FontSize=20,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12),Text="先登录个人希沃账号，再将自己的网页账号关联一次。多位老师可共用本电脑，身份一致才能控制。"};
  var image=new System.Windows.Controls.Image{Height=240,Stretch=System.Windows.Media.Stretch.Uniform};
  var button=new System.Windows.Controls.Button{Content="连接并显示配对码",FontSize=20,Padding=new Thickness(10)};
  var window=new Window{Title="手机控制 · 首次绑定",Owner=owner,SizeToContent=SizeToContent.WidthAndHeight,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel,MaxHeight=SystemParameters.WorkArea.Height};
  panel.Children.Add(label);panel.Children.Add(url);panel.Children.Add(button);panel.Children.Add(image);
  async Task RefreshCode(){
   if(!button.IsEnabled)return;
   button.IsEnabled=false;
   try{
    var identity=RefreshTeacher();if(!identity.Authenticated)throw new InvalidOperationException("尚未确认个人希沃身份。请先在希沃登录，再显示配对码。");
    string serverUrl=ValidBase(url.Text).ToString().TrimEnd('/');
    bool changed=serverUrl!=settings.ServerUrl;settings.ServerUrl=serverUrl;settings.Save();
    if(changed||socket?.State!=WebSocketState.Open)Connect();
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));string code=await Register(timeout.Token);
    using var generator=new QRCodeGenerator();using var data=generator.CreateQrCode(settings.ServerUrl+"/#pair="+code,QRCodeGenerator.ECCLevel.M);
    var png=new PngByteQRCode(data).GetGraphic(6);
    var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=new MemoryStream(png);bitmap.EndInit();bitmap.Freeze();image.Source=bitmap;
    await SendState(true);
    label.Text="希沃老师："+identity.Name+" · ID："+identity.Uid+"\n配对码："+code+"（5分钟有效，换账号后失效）\n手机扫码或输入配对码后点绑定，即可直接连接，无需电脑再次确认。\n"+(string.IsNullOrEmpty(lastError)?"":lastError);
   }catch(Exception e){label.Text=RemoteDeployment.IsOldLocalTest(url.Text)?"这是本机测试地址，手机无法通过它连接。请使用已部署的课堂服务地址。":e.Message;}finally{button.IsEnabled=true;}
  }
  button.Click+=async(_,_)=>await RefreshCode();
  window.Loaded+=async(_,_)=>{if(!string.IsNullOrWhiteSpace(url.Text))await RefreshCode();};
  window.ShowDialog();
  await Task.CompletedTask;
 }
}
