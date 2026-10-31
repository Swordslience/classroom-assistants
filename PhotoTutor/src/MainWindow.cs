using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace PhotoTutor {
 internal sealed class MainWindow:Window {
  readonly Settings settings=Settings.Load();
  readonly ComboBox devices=new ComboBox{MinWidth=220,MaxWidth=340,FontSize=22,Margin=new Thickness(4)};
  readonly ComboBox task=new ComboBox{ItemsSource=new[]{"完整讲解","给一点提示","核对作答"},SelectedIndex=0,FontSize=24,Margin=new Thickness(4),MinWidth=180};
  readonly CheckBox high=new CheckBox{Content="整页高清",FontSize=21,Margin=new Thickness(10),VerticalAlignment=VerticalAlignment.Center};
  readonly CheckBox autoVoice=new CheckBox{Content="讲解后朗读",FontSize=21,Margin=new Thickness(10),VerticalAlignment=VerticalAlignment.Center};
  readonly TextBlock status=new TextBlock{FontSize=21,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
  readonly TextBlock photoLabel=new TextBlock{Text="打开高拍仪，或从原生展台框选题目",FontSize=24,Margin=new Thickness(10),TextWrapping=TextWrapping.Wrap};
  readonly TextBlock hint=new TextBlock{Text="请把题干和作答一起拍清楚。图片只在当前程序内保留。",FontSize=22,Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap};
  readonly Image image=new Image{Stretch=Stretch.Uniform,RenderTransformOrigin=new Point(.5,.5)};
  readonly Border imageBox=new Border{Background=new SolidColorBrush(Color.FromRgb(240,244,246)),ClipToBounds=true};
  readonly LessonView lesson=new LessonView();
  readonly Button cameraButton,snapButton,solveButton,voiceButton;
  readonly ScaleTransform zoom=new ScaleTransform(1,1);readonly TranslateTransform pan=new TranslateTransform();
  CameraSession? camera;BitmapSource? liveFrame;PhotoData? photo;CancellationTokenSource? work;MediaPlayer? player;
  string responseText="",cachedSpeech="",cachedVoiceFile="";long workId;int cameraEpoch,pendingFrame;bool openingCamera,closed,picking,dragging,cameraDemo;
  Point pointer;DateTime lastTouch=DateTime.UtcNow;readonly DispatcherTimer idle=new DispatcherTimer{Interval=TimeSpan.FromSeconds(20)};
  public MainWindow() {
   Title="拍照解题助教 1.0 · 独立版";FontFamily=new FontFamily("Microsoft YaHei");Width=Math.Min(1440,SystemParameters.WorkArea.Width);Height=Math.Min(920,SystemParameters.WorkArea.Height);MinWidth=880;MinHeight=580;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=new SolidColorBrush(Color.FromRgb(247,249,250));
   var root=new DockPanel{Margin=new Thickness(12)};Content=root;
   var header=new DockPanel{Margin=new Thickness(6,0,6,8)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
   var name=new TextBlock{Text="拍照解题助教",FontSize=32,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(Color.FromRgb(12,95,115)),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(name,Dock.Left);header.Children.Add(name);
   var systemTools=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};systemTools.Children.Add(Button("帮助／演示",Help));systemTools.Children.Add(Button("全屏 F11",ToggleFullScreen));systemTools.Children.Add(Button("接口设置",Configure));header.Children.Add(systemTools);
   var row=new WrapPanel();DockPanel.SetDock(row,Dock.Top);root.Children.Add(row);row.Children.Add(devices);row.Children.Add(Button("刷新设备",()=>_=RefreshDevices()));
   cameraButton=Button("打开高拍仪",()=>_=Guard(ToggleCamera));row.Children.Add(cameraButton);
   snapButton=Button("拍照并讲解 F8",()=>_=Guard(TakePhoto));row.Children.Add(snapButton);snapButton.IsEnabled=false;
   row.Children.Add(Button("原生展台取图",()=>_=Guard(PickScreen)));row.Children.Add(Button("载入题目",()=>_=Guard(LoadFile)));
   var actions=new WrapPanel();DockPanel.SetDock(actions,Dock.Top);root.Children.Add(actions);actions.Children.Add(task);
   solveButton=Button("讲解本题",()=>_=Guard(Solve));actions.Children.Add(solveButton);voiceButton=Button("朗读讲解",()=>_=Guard(ReadAnswer));actions.Children.Add(voiceButton);
   var stop=Button("停止",()=>StopWork("已停止解题与朗读。"));stop.Foreground=Brushes.DarkRed;actions.Children.Add(stop);actions.Children.Add(Button("清空本题",Clear));actions.Children.Add(high);actions.Children.Add(autoVoice);
   high.IsChecked=settings.HighQuality;high.Checked+=(_,__)=>SaveQuality();high.Unchecked+=(_,__)=>SaveQuality();
   var fonts=new ComboBox{ItemsSource=new[]{"24号","27号","30号"},SelectedIndex=Math.Max(0,Math.Min(2,(settings.BodySize-32)/4)),FontSize=21,Margin=new Thickness(4),Width=95};actions.Children.Add(fonts);fonts.SelectionChanged+=(_,__)=>{settings.BodySize=32+fonts.SelectedIndex*4;lesson.BodySize=settings.BodySize;lesson.Display(responseText,true);settings.Save();};lesson.BodySize=settings.BodySize;
   DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
   var split=new Grid();split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(45,GridUnitType.Star),MinWidth=260});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(8)});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(55,GridUnitType.Star),MinWidth=300});root.Children.Add(split);
   var left=new DockPanel();Grid.SetColumn(left,0);split.Children.Add(left);DockPanel.SetDock(photoLabel,Dock.Top);left.Children.Add(photoLabel);
   var photoTools=new WrapPanel();DockPanel.SetDock(photoTools,Dock.Bottom);left.Children.Add(photoTools);photoTools.Children.Add(Button("＋",()=>Scale(1.25)));photoTools.Children.Add(Button("－",()=>Scale(.8)));photoTools.Children.Add(Button("复位",ResetView));photoTools.Children.Add(Button("旋转90°",Rotate));
   var transforms=new TransformGroup();transforms.Children.Add(zoom);transforms.Children.Add(pan);image.RenderTransform=transforms;imageBox.Child=image;left.Children.Add(imageBox);
   var divider=new GridSplitter{Width=8,HorizontalAlignment=HorizontalAlignment.Stretch};Grid.SetColumn(divider,1);split.Children.Add(divider);Grid.SetColumn(lesson,2);split.Children.Add(lesson);
   imageBox.MouseWheel+=(_,e)=>{Scale(e.Delta>0?1.15:1/1.15);e.Handled=true;};
   imageBox.MouseLeftButtonDown+=(_,e)=>{pointer=e.GetPosition(imageBox);dragging=true;imageBox.CaptureMouse();};
   imageBox.MouseMove+=(_,e)=>{if(dragging&&e.LeftButton==MouseButtonState.Pressed){Point p=e.GetPosition(imageBox);Vector d=p-pointer;if(d.Length>3){pan.X+=d.X;pan.Y+=d.Y;pointer=p;}}};
   imageBox.MouseLeftButtonUp+=(_,e)=>{if(!dragging)return;dragging=false;imageBox.ReleaseMouseCapture();lastTouch=DateTime.UtcNow;};
   imageBox.IsManipulationEnabled=true;imageBox.ManipulationStarting+=(_,e)=>{e.ManipulationContainer=imageBox;e.Mode=ManipulationModes.Scale|ManipulationModes.Translate;e.Handled=true;};
   imageBox.ManipulationDelta+=(_,e)=>{Scale(e.DeltaManipulation.Scale.X);pan.X+=e.DeltaManipulation.Translation.X;pan.Y+=e.DeltaManipulation.Translation.Y;e.Handled=true;};
   PreviewKeyDown+=(_,e)=>{if(e.Key==Key.F8){_=Guard(TakePhoto);e.Handled=true;}else if(e.Key==Key.Escape){StopWork("已停止。");if(WindowStyle==WindowStyle.None)ToggleFullScreen();}else if(e.Key==Key.F11){ToggleFullScreen();e.Handled=true;}};
   StateChanged+=(_,__)=>{if(WindowState==WindowState.Minimized)StopCamera();};
   Closed+=(_,__)=>{closed=true;StopCamera();StopWork("");idle.Stop();DeleteVoice();photo=null;image.Source=null;};
   idle.Tick+=(_,__)=>{if((DateTime.UtcNow-lastTouch).TotalMinutes>=10&&(photo!=null||camera!=null)){Clear();status.Text="闲置10分钟，已关闭相机并清空题目。";}};idle.Start();
   Loaded+=async(_,__)=>{await RefreshDevices();};
   lesson.Display("拍一题，讲一题\n\n1. 打开高拍仪，摆正题目。\n2. 点击“拍照并讲解”。\n3. 查看步骤，也可点“朗读讲解”。\n\n没有标准摄像头时：先打开学校原生视频展台，再点“原生展台取图”框选题目。\n\n不需要登录希沃，也不需要打开课件。",false);
   status.Text=AiClient.HasKey(settings)?"接口已配置 · 无需登录希沃 · 本机每日上限 "+settings.DailyLimit+" 次":"分享版尚未配置接口，请老师先点右上角“接口设置”。";UpdateButtons();
  }
  Button Button(string text,Action action){var b=new Button{Content=text,FontSize=22,Padding=new Thickness(12,8,12,8),Margin=new Thickness(4),MinHeight=48};b.Click+=(_,__)=>{lastTouch=DateTime.UtcNow;action();};return b;}
  async Task Guard(Func<Task> action){try{await action();}catch(OperationCanceledException){ }catch(Exception e){if(!closed)status.Text=e.Message;UpdateButtons();}}
  async Task RefreshDevices() {
   try {var found=await Task.Run(()=>CameraSession.Enumerate());if(Entry.DemoCamera)found=found.Concat(new[]{new CameraDevice{Name="演示高拍仪（软件测试）",Id="TEST_DEMO_INPUT"}}).ToArray();if(closed)return;devices.ItemsSource=found;devices.SelectedItem=found.FirstOrDefault(d=>d.Id==settings.PreferredCamera)??found.FirstOrDefault();
    if(found.Length==0)status.Text="未检测到高拍仪／摄像头；可先打开原生展台，再用框选取图，也可载入照片。";
    if(Entry.DemoCamera)status.Text="软件演示模式：使用合成题图测试流程，不代表真实高拍仪驱动已验证。";
   }catch{status.Text="设备枚举暂不可用，原生展台取图和载入照片仍可使用。";}
  }
  async Task ToggleCamera() {
   if(camera!=null){StopCamera();status.Text="相机已关闭。";return;}if(openingCamera)return;openingCamera=true;
   try {
    if(devices.SelectedItem is not CameraDevice d){await RefreshDevices();if(devices.SelectedItem is not CameraDevice)throw new InvalidOperationException("没有标准摄像头。请使用“原生展台取图”或“载入题目”。");d=(CameraDevice)devices.SelectedItem;}
    StopWork("");photo=null;responseText="";lesson.Display("正在打开高拍仪…\n\n把题干和作答一起放入画面。取图前不会调用AI。",false);DeleteVoice();settings.PreferredCamera=d.Id;settings.Save();
    int epoch=++cameraEpoch;cameraDemo=Entry.DemoCamera&&d.Id=="TEST_DEMO_INPUT";var session=new CameraSession(d.Id,high.IsChecked==true,cameraDemo?Checks.Chart():null);camera=session;
    session.Frame+=frame=>{if(closed||camera!=session||cameraEpoch!=epoch||Dispatcher.HasShutdownStarted)return;if(Interlocked.Exchange(ref pendingFrame,1)!=0)return;Dispatcher.BeginInvoke(new Action(()=>{try{if(!closed&&camera==session&&cameraEpoch==epoch){bool first=liveFrame==null;liveFrame=frame;image.Source=frame;snapButton.IsEnabled=true;photoLabel.Text=(cameraDemo?"演示预览 · 非真实高拍仪":"实时预览")+" · "+frame.PixelWidth+"×"+frame.PixelHeight+" · 尚未上传";if(first){status.Text="预览已就绪，拍清题干和作答后点击“拍照并讲解”。";lesson.Display("预览已就绪。\n\n点击“拍照并讲解”后才上传这一张题图，预览不会持续上传。",false);}}}finally{Interlocked.Exchange(ref pendingFrame,0);}}),DispatcherPriority.Background);};
    session.Failed+=message=>Dispatcher.BeginInvoke(new Action(()=>{if(camera==session){StopCamera();status.Text=message;}}));
    cameraButton.Content="关闭高拍仪";status.Text="相机正在启动；画面清楚后点击“拍照并讲解”。";ResetView();
   }finally{openingCamera=false;UpdateButtons();}
  }
  void StopCamera(){cameraEpoch++;var previous=camera;camera=null;previous?.Dispose();liveFrame=null;snapButton.IsEnabled=false;cameraButton.Content="打开高拍仪";}
  async Task TakePhoto() {
   if(camera==null||liveFrame==null)throw new InvalidOperationException("请先打开高拍仪，等画面清晰后再拍照。");
   var frame=liveFrame;bool wasDemo=cameraDemo;StopCamera();SetPhoto(PhotoData.Create(frame,wasDemo?"演示题图（非真实高拍仪）":"高拍仪拍照",high.IsChecked==true));await Solve();
  }
  async Task PickScreen() {
   if(picking)return;picking=true;StopWork("");StopCamera();Hide();
   try{var area=await ScreenPick.SelectAsync();if(area==null)return;await Task.Delay(180);var bitmap=ScreenPick.Capture(area.Value);SetPhoto(PhotoData.Create(bitmap,"原生展台画面",high.IsChecked==true));}
   finally{picking=false;if(!closed){Show();Activate();}}
   if(photo!=null)await Solve();
  }
  async Task LoadFile() {
   var dialog=new OpenFileDialog{Title="选择题目或展台保存的照片",Filter="题目图片|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff"};
   if(dialog.ShowDialog(this)!=true)return;StopCamera();SetPhoto(PhotoData.FromFile(dialog.FileName,high.IsChecked==true));await Solve();
  }
  void SetPhoto(PhotoData value){StopWork("");photo=value;responseText="";lesson.Display("图片已就绪，正在准备讲解…",false);DeleteVoice();image.Source=value.Image;photoLabel.Text=value.Label+" · "+value.Image.PixelWidth+"×"+value.Image.PixelHeight+" · "+(value.Jpeg.Length/1024)+"KB";ResetView();lastTouch=DateTime.UtcNow;UpdateButtons();}
  async Task Solve() {
   if(photo==null)throw new InvalidOperationException("请先拍照、框选或载入一张题目图片。");
   if(!AiClient.HasKey(settings))throw new InvalidOperationException("请老师先配置接口；自用版无需重新配置。");
   StopWork("");DailyUse.Begin(settings.DailyLimit);var local=new CancellationTokenSource();work=local;long id=workId;PhotoData current=photo;responseText="";DeleteVoice();lesson.Display("正在辨认题目并准备讲解…",false);UpdateButtons();
   string purpose=task.SelectedIndex==1?"先给一个关键提示，引导我自己做，不直接公布最终答案":task.SelectedIndex==2?"核对照片中的学生作答，说明是否正确和真实错误；没有作答时转为讲解思路":"完整讲解这道题，给出容易理解的步骤与答案";
   var text=new StringBuilder();var clock=Stopwatch.StartNew();long paint=0;
   try {
    var result=await AiClient.Solve(settings,current,purpose,piece=>Dispatcher.Invoke(()=>{if(local.IsCancellationRequested||workId!=id)return;text.Append(piece);responseText=text.ToString();if(clock.ElapsedMilliseconds-paint>200){lesson.Display(responseText,false);paint=clock.ElapsedMilliseconds;}}),message=>Dispatcher.Invoke(()=>{if(workId==id&&!local.IsCancellationRequested)status.Text=message;}),local.Token);
    if(workId!=id||local.IsCancellationRequested)return;responseText=result.Text;lesson.Display(responseText,true);status.Text="讲解完成 · 首段 "+result.FirstSeconds.ToString("0.0")+"秒 · 全文 "+result.TotalSeconds.ToString("0.0")+"秒 · 请结合原题核对";
    SaveMetric(result,current);
    if(Entry.DemoCamera&&current.Label.StartsWith("演示题图")){await Dispatcher.Yield(DispatcherPriority.Render);SaveDemoEvidence(result);}
    if(autoVoice.IsChecked==true)await ReadWithToken(responseText,id,local.Token);
   }catch(OperationCanceledException){if(workId==id)status.Text=local.IsCancellationRequested?"已停止本次解题。":"模型响应超时，题目已保留，可稍后重试。";}
   catch(Exception e){if(workId==id){status.Text=e.Message;lesson.Display(responseText.Length>0?responseText:"未能完成讲解。\n\n"+e.Message+"\n\n题目图片仍保留，可稍后点“讲解本题”重试。",true);}}
   finally{if(ReferenceEquals(work,local)){work=null;UpdateButtons();}local.Dispose();}
  }
  async Task ReadAnswer() {
   if(string.IsNullOrWhiteSpace(responseText))throw new InvalidOperationException("请先完成一道题的文字讲解。");StopWork("");var local=new CancellationTokenSource();work=local;long id=workId;UpdateButtons();
   try{await ReadWithToken(responseText,id,local.Token);}catch(OperationCanceledException){ }finally{if(ReferenceEquals(work,local)){work=null;UpdateButtons();}local.Dispose();}
  }
  async Task ReadWithToken(string text,long id,CancellationToken token) {
   status.Text="正在准备朗读，可随时停止…";
   if(cachedSpeech!=text||!File.Exists(cachedVoiceFile)) {
    string file=await AiClient.Voice(settings,text,token);if(workId!=id||token.IsCancellationRequested){try{File.Delete(file);}catch{ }token.ThrowIfCancellationRequested();return;}
    DeleteVoice();cachedSpeech=text;cachedVoiceFile=file;
   }
   token.ThrowIfCancellationRequested();var media=new MediaPlayer();player=media;var done=new TaskCompletionSource<bool>();
   media.MediaOpened+=(_,__)=>{if(!token.IsCancellationRequested&&workId==id&&ReferenceEquals(player,media)){status.Text="正在朗读 · 点击“停止”可立即停止";media.Play();}else{media.Close();done.TrySetCanceled();}};
   media.MediaEnded+=(_,__)=>done.TrySetResult(true);media.MediaFailed+=(_,e)=>done.TrySetException(new InvalidOperationException("朗读播放失败，文字仍可查看："+e.ErrorException.Message));
   using(var cancel=token.Register(()=>Dispatcher.BeginInvoke(new Action(()=>{media.Stop();media.Close();done.TrySetCanceled();})))) {
    try{media.Open(new Uri(cachedVoiceFile));await done.Task;if(workId==id&&!token.IsCancellationRequested)status.Text="朗读完成。";}
    finally{media.Close();if(ReferenceEquals(player,media))player=null;}
   }
  }
  void StopWork(string message){bool hadAudio=player!=null;workId++;work?.Cancel();work=null;player?.Stop();player?.Close();player=null;if(message.Length>0)status.Text=message;UpdateButtons();if(Entry.DemoCamera&&message.Length>0)try{File.WriteAllText(Path.Combine(Settings.DataRoot,"last-stop.json"),JsonConvert.SerializeObject(new{time=DateTime.Now,audioWasPlaying=hadAudio,requestCancelled=true,playerCleared=player==null,status=message}));}catch{ }}
  void Clear(){StopWork("");StopCamera();photo=null;image.Source=null;responseText="";DeleteVoice();lesson.Display("本题已清空。\n\n准备好下一道题后，可以拍照、从原生展台取图或载入照片。",false);photoLabel.Text="等待下一道题";status.Text="已清空本题图片和讲解。";ResetView();UpdateButtons();}
  void Rotate(){if(photo==null){status.Text="请先拍照或载入题目，再旋转。";return;}SetPhoto(photo.Rotate(high.IsChecked==true));lesson.Display("图片已旋转。\n\n确认方向正确后，点“讲解本题”。",false);status.Text="已旋转90°，没有自动发起新请求。";}
  void Scale(double factor){zoom.ScaleX=zoom.ScaleY=Math.Max(.25,Math.Min(6,zoom.ScaleX*factor));lastTouch=DateTime.UtcNow;}
  void ResetView(){zoom.ScaleX=zoom.ScaleY=1;pan.X=pan.Y=0;}
  void SaveQuality(){settings.HighQuality=high.IsChecked==true;settings.Save();if(camera!=null)status.Text="清晰度将在下次打开高拍仪时生效。";}
  void DeleteVoice(){if(cachedVoiceFile.Length>0)try{File.Delete(cachedVoiceFile);}catch{ }cachedVoiceFile=cachedSpeech="";}
  void UpdateButtons(){solveButton.IsEnabled=photo!=null&&work==null;voiceButton.IsEnabled=responseText.Length>0&&work==null;}
  void ToggleFullScreen(){if(WindowStyle==WindowStyle.None){WindowStyle=WindowStyle.SingleBorderWindow;WindowState=WindowState.Normal;}else{WindowStyle=WindowStyle.None;WindowState=WindowState.Maximized;}}
  void Help() {
   var panel=new StackPanel{Margin=new Thickness(20)};var dialog=new Window{Owner=this,Title="使用帮助／无设备测试",Width=680,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};
   panel.Children.Add(new TextBlock{FontSize=24,TextWrapping=TextWrapping.Wrap,Text="直接拍照：选设备→打开高拍仪→拍照并讲解。\n\n原生展台：在学校展台软件中显示完整题目→点本程序“原生展台取图”→框选题干和作答，自动讲解。\n\n如果摄像头被占用，先关闭另一套预览。字太小请靠近拍摄或勾选整页高清；照片模糊时AI应要求补拍。\n\n没有摄像头也能测试：下方演示模式使用合成题图，不验证实际硬件。重新启动程序会回到正常模式。"});
   panel.Children.Add(Button(Entry.DemoCamera?"退出演示模式":"开启无摄像头演示",()=>{Clear();Entry.DemoCamera=!Entry.DemoCamera;hint.Text=Entry.DemoCamera?"软件演示：当前题图为合成练习，不代表真实高拍仪驱动已验证。":"请把题干和作答一起拍清楚。图片只在当前程序内保留。";dialog.Close();_=RefreshDevices();}));dialog.ShowDialog();
  }
  static void SaveMetric(AnswerResult result,PhotoData current){try{string file=Path.Combine(Settings.DataRoot,"last-check.json");File.WriteAllText(file,JsonConvert.SerializeObject(new{time=DateTime.Now,firstSeconds=result.FirstSeconds,totalSeconds=result.TotalSeconds,imageBytes=current.Jpeg.Length,width=current.Image.PixelWidth,height=current.Image.PixelHeight,answerCharacters=result.Text.Length}));}catch{ }}
  void SaveDemoEvidence(AnswerResult result) {
   try{string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"验收");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"演示题真实回答.txt"),result.Text,Encoding.UTF8);
    var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(folder,"独立程序_真实解题.png")))encoder.Save(file);
   }catch{ }
  }
  void Configure() {
   StopWork("");var panel=new StackPanel{Margin=new Thickness(20)};
   var provider=new ComboBox{ItemsSource=new[]{"DeepSeek","Doubao"},SelectedItem=settings.Provider,FontSize=23};var deep=new PasswordBox{FontSize=22};var doubao=new PasswordBox{FontSize=22};var voice=new PasswordBox{FontSize=22};var limit=new TextBox{Text=settings.DailyLimit.ToString(),FontSize=23};
   void Field(string label,Control control){panel.Children.Add(new TextBlock{Text=label,FontSize=21,Margin=new Thickness(0,10,0,4)});panel.Children.Add(control);}
   Field("文字／看图模型",provider);Field("DeepSeek Key（留空保留）",deep);Field("豆包 Key（留空保留）",doubao);Field("豆包语音 Key（留空保留）",voice);Field("本机每日解题上限（1–1000）",limit);
   panel.Children.Add(new TextBlock{Text="学生使用无需登录。接口仅保存在本机当前Windows账户的加密配置中；不显示已有密钥。",FontSize=20,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,4)});
   var window=new Window{Owner=this,Title="老师设置 · 拍照解题助教",Width=640,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};
   panel.Children.Add(Button("保存设置",()=>{int value;if(!int.TryParse(limit.Text,out value)||value<1||value>1000){MessageBox.Show(window,"每日上限请输入1–1000。");return;}settings.Provider=(string)provider.SelectedItem;if(deep.Password.Trim().Length>0)settings.DeepSeekApiKey=deep.Password.Trim();if(doubao.Password.Trim().Length>0)settings.DoubaoApiKey=doubao.Password.Trim();if(voice.Password.Trim().Length>0)settings.VoiceApiKey=voice.Password.Trim();settings.DailyLimit=value;settings.Save();window.Close();status.Text="设置已保存，可以开始拍题；无需登录希沃。";}));window.ShowDialog();
  }
 }
}
