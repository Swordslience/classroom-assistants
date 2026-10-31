using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Speech.Recognition;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ClassroomAiBridge;
using Cvte.EasiNote;
using Cvte.Paint.Features.Elements.Images;

namespace ClassroomAI.Seewo;
internal sealed partial class AiWindow:Window {
 static AiWindow? current;
 readonly TextBox input=new(){AcceptsReturn=true,MaxLength=12000,TextWrapping=TextWrapping.Wrap,Height=135,FontSize=32,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,5,0,8)};
 readonly ClassroomText answer=new();readonly Image preview=new(){Stretch=Stretch.Uniform};readonly ClassroomText pageText=new();
 readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,FontSize=18,Margin=new Thickness(4)};
 readonly TextBlock pageLabel=new(){FontSize=18,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)};
 readonly Button generate=new(){Content="生成图片"},ask=new(){Content="回答并朗读"},insert=new(){Content="插入当前页"},stop=new(){Content="停止"},mic=new(){Content="语音输入"},replay=new(){Content="重播语音"};
 readonly Button readPage=new(){Content="读取本页"},askText=new(){Content="文字回答"},questions=new(){Content="推荐问题"},loadImage=new(){Content="载入图片"};
 readonly ComboBox level=new(){Width=170,FontSize=20,Margin=new Thickness(4),ItemsSource=new[]{"高中·标准","高中·基础支架","高中·挑战拓展"},SelectedIndex=0};
 readonly DispatcherTimer pageWatch=new(){Interval=TimeSpan.FromMilliseconds(1200)};
 readonly MediaPlayer player=new();readonly TabControl tabs=new();CancellationTokenSource? cts;string? picturePath;SpeechRecognitionEngine? recognizer;bool listening;
 static readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(120)};
 readonly Connection config;bool playbackAllowed;string? audioPath;
 readonly InputScrollSync inputScroll;
 readonly WrapPanel qaTools=new(),imageTools=new(),photoTools=new();
 readonly Dictionary<string,string> drafts=new(){{"qa",""},{"image",""},{"photo",""},{"practice",""}};
 string mode="qa",lastPageIdentity="",questionSource="",readDraft="";bool changingInput,changingMode;long revision;
 PageSnapshot? lockedQuestion;DateTime lastActivity=DateTime.UtcNow;
 readonly DispatcherTimer stateTimer=new(){Interval=TimeSpan.FromMilliseconds(180)};
 public static void Open(){try{if(current is null)current=new AiWindow();current.Present();}catch(Exception e){MessageBox.Show("助教暂时未能打开："+Friendly(e)+"\n请回到已打开的课件重试。","奶龙课堂助教");}}
 void Present(){AttachToPage();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;if(!IsVisible)Show();Activate();ScheduleState();}
 void AttachToPage(){var page=CurrentPage.Resolve();if(Owner!=page.Host){Owner=page.Host;lockedQuestion=null;}Topmost=page.IsDisplay;}
 internal static object UiState()=>current?.StateObject()??new{mode="qa",visible=false};
 static Button Tool(string label,Action action){var b=new Button{Content=label,FontSize=22,Padding=new Thickness(12,8,12,8),Margin=new Thickness(3)};b.Click+=(_,_)=>action();return b;}
 AiWindow(){
  inputScroll=new InputScrollSync(input);config=Connection.Load();var openingPage=CurrentPage.Resolve();Owner=openingPage.Host;Title="奶龙课堂助教 0.4.19 · 理科与举一反三";Icon=Brand.Icon();RuntimeFiles.Clean();Width=Math.Min(1460,SystemParameters.WorkArea.Width);Height=Math.Min(920,SystemParameters.WorkArea.Height);MinWidth=850;MinHeight=550;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(255,253,245));
  var dock=new DockPanel{Margin=new Thickness(12)};Content=dock;
  var header=new DockPanel{Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(header,Dock.Top);
  var logo=new Border{Background=Brushes.Gold,CornerRadius=new CornerRadius(12),Padding=new Thickness(12,4,12,4),Child=new TextBlock{Text="龙",FontSize=30,FontWeight=FontWeights.Bold}};DockPanel.SetDock(logo,Dock.Left);header.Children.Add(logo);
  var title=new TextBlock{Text="奶龙课堂助教",FontSize=27,FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0)};DockPanel.SetDock(title,Dock.Left);header.Children.Add(title);
  var utilities=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};AddTools(utilities);header.Children.Add(utilities);dock.Children.Add(header);
  DockPanel.SetDock(pageLabel,Dock.Top);dock.Children.Add(pageLabel);
  var bars=new StackPanel();DockPanel.SetDock(bars,Dock.Top);bars.Children.Add(qaTools);bars.Children.Add(imageTools);bars.Children.Add(photoTools);bars.Children.Add(practiceTools);dock.Children.Add(bars);
  foreach(var b in new[]{readPage,questions,askText,ask,mic,replay}){StyleTool(b);qaTools.Children.Add(b);}qaTools.Children.Add(Tool("清空输入",ClearInput));qaTools.Children.Add(Tool("固定此题",LockQuestion));qaTools.Children.Add(Tool("解除固定",()=>{lockedQuestion=null;questionSource="";status.Text="已解除固定，照片将参考当前页。";ScheduleState();}));qaTools.Children.Add(level);
  foreach(var b in new[]{generate,loadImage,insert}){StyleTool(b);imageTools.Children.Add(b);}imageTools.Children.Add(Tool("配图建议",()=>Start(SuggestImages)));imageTools.Children.Add(Tool("清空输入",ClearInput));
  photoTools.Children.Add(Tool("重新点评",()=>Start(t=>AnalysePhoto(t))));StyleTool(photoLayoutButton);photoLayoutButton.Click+=(_,_)=>TogglePhoto();photoTools.Children.Add(photoLayoutButton);photoTools.Children.Add(Tool("上一屏",()=>photoAnswer.PageUp()));photoTools.Children.Add(Tool("下一屏",()=>photoAnswer.PageDown()));photoTools.Children.Add(Tool("清除照片",ClearPhoto));photoTools.Children.Add(Tool("朗读点评",()=>Start(t=>SpeakText(photoAnswer.Text,t))));
  qaTools.Children.Add(Tool("出相似题",()=>Start(t=>GeneratePractice(t,"qa"))));photoTools.Children.Add(Tool("出相似题",()=>Start(t=>GeneratePractice(t,"photo"))));
  DockPanel.SetDock(input,Dock.Top);dock.Children.Add(input);DockPanel.SetDock(status,Dock.Bottom);dock.Children.Add(status);
  tabs.FontSize=24;tabs.Items.Add(new TabItem{Header="课堂问答",Content=answer});
  var images=new DockPanel();var cards=new ScrollViewer{Content=suggestionCards,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=180};DockPanel.SetDock(cards,Dock.Top);images.Children.Add(cards);InitVisualPanel(images);images.Children.Add(preview);tabs.Items.Add(new TabItem{Header="教学配图",Content=images});InitPhotoTab();InitPracticeTab();dock.Children.Add(tabs);
  tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs&&!changingMode){Stop();SetMode(tabs.SelectedIndex==3?"practice":tabs.SelectedIndex==1?"image":tabs.SelectedIndex==2?"photo":"qa");}};
  input.TextChanged+=(_,_)=>InputChanged();
  generate.Click+=(_,_)=>Start(Generate);ask.Click+=(_,_)=>Start(t=>Ask(t,true,false));askText.Click+=(_,_)=>Start(t=>Ask(t,false,false));questions.Click+=(_,_)=>Start(t=>Ask(t,false,true));
  readPage.Click+=(_,_)=>{try{ReadIntoInput();}catch(Exception e){status.Text=e.Message;}};
  loadImage.Click+=(_,_)=>{var dialog=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog(this)==true){LoadPicture(dialog.FileName);SetMode("image");}};
  stop.Click+=(_,_)=>Stop();insert.Click+=(_,_)=>Start(Insert);mic.Click+=(_,_)=>ToggleMic();replay.Click+=(_,_)=>{if(audioPath is not null)Start(t=>PlayFile(audioPath,t));};
  stateTimer.Tick+=async(_,_)=>{stateTimer.Stop();try{await RemoteClient.Instance.Send(new{type="ui_state",ui=StateObject()});}catch(Exception e){status.Text="同步暂不可用："+e.Message;}};
  IsVisibleChanged+=(_,_)=>{if(!IsVisible)visualPreviewWindow?.Close();ScheduleState();};StateChanged+=(_,_)=>ScheduleState();
  pageWatch.Tick+=(_,_)=>WatchPage();pageWatch.Start();
  Closing+=(_,e)=>{e.Cancel=true;Stop();visualPreviewWindow?.Close();Hide();ScheduleState();};
  Application.Current.Exit+=(_,_)=>{Stop();pageWatch.Stop();stateTimer.Stop();recognizer?.Dispose();ClearPhoto();};
  player.MediaOpened+=(_,_)=>{if(playbackAllowed){player.Play();status.Text="正在朗读，可随时停止。";ScheduleState();}else player.Close();};
  player.MediaEnded+=(_,_)=>{playbackAllowed=false;playFinished?.TrySetResult(true);ScheduleState();};player.MediaFailed+=(_,e)=>{playbackAllowed=false;playFinished?.TrySetException(e.ErrorException);ScheduleState();};
  Loaded+=(_,_)=>ScienceSupport.CloudUpdateLauncher.Open("seewo","0.4.19",Path.GetDirectoryName(typeof(AiWindow).Assembly.Location)!,true);
  status.Text="照片收到后自动点评；文字与照片仅用于当前课堂。";SetMode("qa");WatchPage();
 }
 void StyleTool(Button b){b.FontSize=22;b.Padding=new Thickness(12,8,12,8);b.Margin=new Thickness(3);}
 void Start(Func<CancellationToken,Task> action){_=Run(action);}
  async Task<OperationOutcome> Run(Func<CancellationToken,Task> action){
   Stop();var local=new CancellationTokenSource();cts=local;ScheduleState();
   try{var outcome=await OperationOutcome.Capture(()=>action(local.Token),local.Token,()=>ReferenceEquals(cts,local),()=>status.Text,Friendly);
    if(ReferenceEquals(cts,local)){
     if(outcome.Status=="cancelled"){status.Text=outcome.Message;if(mode=="practice"&&practice is null)practiceView.Text="本次出题已停止，原题与照片保留。";}
     else if(outcome.Status=="error"){status.Text=outcome.Message;if(mode=="practice"&&practice is null)practiceView.Text="相似题未完成："+status.Text+"\n原题与照片保留，可重试。";if(mode=="photo"&&photoAnswer.Text.Length==0)photoAnswer.Text="本次点评未完成："+status.Text+"\n照片已保留，可点击重新点评。";Log("error",new{error=outcome.Error});}
    }return outcome;
   }finally{if(ReferenceEquals(cts,local)){cts=null;bool answerReceiving=answer.IsReceiving,photoReceiving=photoAnswer.IsReceiving;answer.IsReceiving=photoAnswer.IsReceiving=false;if(answerReceiving)answer.RefreshSize(answer.BodySize);if(photoReceiving)photoAnswer.RefreshSize(photoAnswer.BodySize);ScheduleState();}local.Dispose();}
 }
 static string Friendly(Exception e)=>e is KeyNotFoundException?"收到的信息不完整，未能继续。请重试；无需重新填写密钥。":e.Message;
 void WatchPage(){
  try{var p=CurrentPage.Resolve();if(Owner!=p.Host){Owner=p.Host;lockedQuestion=null;}Topmost=p.IsDisplay;pageLabel.Text="当前："+p.Description+"  ·  "+(config.TextProvider=="DeepSeek"?"DeepSeek Flash 10.17":"豆包 Mini")+"  ·  "+(lockedQuestion is null?"未固定题目":"已固定题目");
   if(lastPageIdentity!=""&&lastPageIdentity!=p.Identity){if(mode!="photo")Stop();practice=null;practiceReveal=0;practiceSource="";practiceView.Text="页面已更换，请按新题重新生成练习。";answer.Text="";suggestions=Array.Empty<ImageSuggestion>();suggestionCards.Children.Clear();if(visualSource=="page")InvalidateVisual();if(drafts["qa"]==readDraft){drafts["qa"]="";if(mode=="qa")SetInput("",null);}questionSource="";if(mode!="photo")status.Text="已切换到"+p.Description+"，请按当前页读取、问答或生成练习。";ScheduleState();}lastPageIdentity=p.Identity;
  }catch(InvalidOperationException e){pageLabel.Text=e.Message;}
  if((DateTime.UtcNow-lastActivity).TotalMinutes>=30&&studentPhotoData is not null){ClearPhoto();status.Text="闲置30分钟，已清除学生照片和点评。";}
 }
 async Task Generate(CancellationToken token)=>await GenerateVisual(token);
 void LoadPicture(string path){picturePath=path;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=1600;image.UriSource=new Uri(path);image.EndInit();image.Freeze();preview.Source=image;visualPreview=VisualBitmap.Preview(image);insert.IsEnabled=true;status.Text="图片已载入。";ScheduleState();}
 PageSnapshot ReadPage(){return CurrentPage.Resolve().Capture();}
 void ReadIntoInput(){SetMode("qa");Stop();var snapshot=ReadPage();questionSource=snapshot.Page.Description;readDraft=string.IsNullOrWhiteSpace(snapshot.Text)?"请根据本页图片讲解题目，说明关键条件和思路。":snapshot.Text;SetInput(readDraft,null);answer.Text="";status.Text="已读取"+questionSource+"到输入框，手机同步更新；点击文字回答或回答并朗读。";ScheduleState();}
 void LockQuestion(){lockedQuestion=ReadPage();questionSource="已固定："+lockedQuestion.Page.Description;status.Text="同题连续点评将使用这个题干；换题请解除或重新固定。";ScheduleState();}
  async Task Insert(CancellationToken token){
   StopPlayback();if(picturePath is null)throw new InvalidOperationException("请先生成或载入一张图片。");
   var page=CurrentPage.Resolve();var board=page.Api;string resource=board.AddResource(picturePath);
   var source=(BitmapSource)preview.Source;var size=board.Board.SlideSize;double sw=size.Width,sh=size.Height;
   if(sw<1||sh<1)throw new InvalidOperationException("当前页尺寸不可用，未插入。");
   double width=sw*.58,height=width*source.PixelHeight/source.PixelWidth;if(height>sh*.68){height=sh*.68;width=height*source.PixelWidth/source.PixelHeight;}
   var picture=new Picture(resource){Bounds=new Rect((sw-width)/2,(sh-height)/2,width,height)};
   board.AddElement(picture);System.Windows.Controls.Panel.SetZIndex(picture,page.Slide.Elements.Select(System.Windows.Controls.Panel.GetZIndex).DefaultIfEmpty(0).Max()+1);
   await Dispatcher.Yield(DispatcherPriority.Render);if(token.IsCancellationRequested){board.RemoveElement(picture);token.ThrowIfCancellationRequested();}
   if(!page.IsStillCurrent()){board.RemoveElement(picture);throw new InvalidOperationException("插入期间页面发生变化，已撤回本次插入，请在目标页重试。");}
   if(!page.Slide.Elements.Any(e=>e.Id==picture.Id)||!board.ContainsElement(picture)||!picture.IsVisible){board.RemoveElement(picture);throw new InvalidOperationException("未验证图片出现在当前可见页，已撤回插入。");}
   // AddElement updates the board but does not itself record a user undo step.
   // Keep the host operation tied to the captured slide so redo never targets a later page.
   try{board.UndoRedo.Insert(new Cvte.Paint.Framework.Operations.AddElementsOperation(page.Slide,picture));}
   catch{board.RemoveElement(picture);throw;}
   board.SelectElements(new[]{picture});page.Slide.UpdateLayout();
   status.Text="已核对：插入"+page.Description+"。请按希沃保存课件。";
   Log("image_inserted_verified",new{page=page.Description,mode=EN.App.CurrentMode.ToString(),pid=Environment.ProcessId,id=picture.Id,slide=page.Index,slide_id=page.Slide.Id,visible=picture.IsVisible});
   Hide();page.Host.Activate();
 }

 async Task Ask(CancellationToken token,bool speak,bool recommend){
  SetMode("qa");var snapshot=ReadPage();questionSource=snapshot.Page.Description;answer.IsReceiving=true;answer.Text="";ScheduleState();
  string prompt=drafts["qa"].Trim();string task=recommend?"根据本页提出3个有梯度的课堂问题：证据理解、分析推理、迁移评价。问题在前，简短参考与依据在后。不编造材料或学情。":prompt.Length==0||prompt==readDraft?"请讲解当前页面，抓住关键条件、核心概念或人物冲突，不照抄页面。":prompt;
  var content=PageContent(snapshot,task);var output=new StringBuilder();var watch=Stopwatch.StartNew();long lastPaint=0;SpeechPipeline? speech=speak?new SpeechPipeline(this,token):null;
  try{
   string text=await ModelEngine.Complete(config,"你是高中跨学科课堂助教，自动适配真实学科。按问题所需完整讲解，不限制字数，不为了简短省略关键步骤或结论。材料不足直接说明，不能凭空补题。用简短标题、分段、粗体和必要公式突出重点。学情："+level.SelectedItem,content,piece=>Dispatcher.Invoke(()=>{if(token.IsCancellationRequested)return;output.Append(piece);speech?.Append(piece);if(watch.ElapsedMilliseconds-lastPaint>=450){answer.Text=output.ToString();lastPaint=watch.ElapsedMilliseconds;ScheduleState();}}),s=>Dispatcher.Invoke(()=>{if(!token.IsCancellationRequested){status.Text=s;ScheduleState();}}),token);
   token.ThrowIfCancellationRequested();if(!snapshot.Page.IsStillCurrent())throw new OperationCanceledException();answer.IsReceiving=false;answer.Text=text;ScheduleState();
   if(speech is not null){speech.Complete();try{await speech.Finished;}catch(OperationCanceledException){throw;}catch(Exception e){status.Text="文字已显示，朗读未完成："+Friendly(e);return;}}
   status.Text=(recommend?"推荐问题已更新":"回答已完成")+" · 依据"+questionSource;Log("answer",new{provider=config.TextProvider,seconds=watch.Elapsed.TotalSeconds,characters=text.Length});
  }catch{if(!token.IsCancellationRequested&&output.Length>0){answer.Text=output.ToString();ScheduleState();}if(speech is not null){speech.Cancel();try{await speech.Finished;}catch{ }}throw;}finally{speech?.Dispose();}
 }
 List<object> PageContent(PageSnapshot snapshot,string task,bool includeImage=true){
  var items=new List<object>{new{type="text",text="页面依据："+snapshot.Page.Description+"\n本页文字：\n"+snapshot.Text+"\n任务："+task}};
  if(includeImage&&File.Exists(snapshot.ImagePath))items.Add(new{type="image_url",image_url=new{url="data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(snapshot.ImagePath)),detail="high"}});return items;
 }
 void StopPlayback(){playbackAllowed=false;player.Stop();player.Close();playFinished?.TrySetCanceled();}
 void Stop(){cts?.Cancel();StopPlayback();if(listening)try{recognizer?.RecognizeAsyncCancel();}catch(InvalidOperationException){}listening=false;mic.Content="语音输入";ScheduleState();}
 void ToggleMic(){try{if(listening){recognizer?.RecognizeAsyncCancel();listening=false;mic.Content="语音输入";return;}var engines=SpeechRecognitionEngine.InstalledRecognizers();if(engines.Count==0)throw new InvalidOperationException("本机没有Windows语音识别引擎，可以使用手机输入法语音输入。");if(recognizer is null){recognizer=new SpeechRecognitionEngine(engines.FirstOrDefault(e=>e.Culture.TwoLetterISOLanguageName=="zh")??engines[0]);recognizer.LoadGrammar(new DictationGrammar());recognizer.SetInputToDefaultAudioDevice();recognizer.SpeechRecognized+=(_,e)=>Dispatcher.Invoke(()=>SetInput(input.Text+e.Result.Text,null));}recognizer.RecognizeAsync(RecognizeMode.Multiple);listening=true;mic.Content="停止收音";}catch(Exception e){status.Text=Friendly(e);}}
 static void Log(string kind,object data){try{Directory.CreateDirectory(BudgetGate.Root);File.AppendAllText(Path.Combine(BudgetGate.Root,"希沃验证记录.jsonl"),JsonSerializer.Serialize(new{time=DateTime.Now,kind,data})+"\n");}catch(IOException){}}
}
