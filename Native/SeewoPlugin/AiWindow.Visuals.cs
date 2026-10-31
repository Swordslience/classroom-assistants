using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ClassroomAiBridge;
namespace ClassroomAI.Seewo;
internal sealed partial class AiWindow {
 readonly VisualMaterials visualMaterials=new();
 string visualSource="page",visualKind="auto",visualLanguage="auto",visualFingerprint="",selectedVisualId="",visualPreview="";
 VisualDesign? visualDesign;
 bool visualRecommendations;
 Window? visualPreviewWindow;
 readonly TextBlock visualSourceLabel=new(){FontSize=20,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
 readonly WrapPanel visualThumbnails=new();
 readonly ScrollViewer visualMaterialScroll=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=140};
 bool visualDetailsExpanded=true;
 readonly ComboBox visualSourceBox=new(){FontSize=20,Width=165,Margin=new Thickness(4),ItemsSource=new[]{"当前课件","拍照材料","仅文字要求"},SelectedIndex=0};
 readonly ComboBox visualKindBox=new(){FontSize=20,Width=165,Margin=new Thickness(4),ItemsSource=new[]{"自动推荐","内容脉络图","关系因果图","过程示意图","情境插画","自定义要求"},SelectedIndex=0};
 readonly ComboBox visualLanguageBox=new(){FontSize=20,Width=195,Margin=new Thickness(4),ItemsSource=new[]{"按学科自动语言","中英对照","中文","原文语言"},SelectedIndex=0};
 bool changingVisual;
 static readonly string[] VisualSources={"page","materials","text"},VisualKinds={"auto","outline","relations","process","scene","custom"},VisualLanguages={"auto","bilingual","zh","original"};
 void InitVisualPanel(DockPanel panel){
  var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);panel.Children.Insert(0,top);
  var options=new WrapPanel();options.Children.Add(visualSourceBox);options.Children.Add(visualKindBox);options.Children.Add(visualLanguageBox);
  options.Children.Add(Tool("添加／继续补图",()=>{try{AddVisualFiles();}catch(Exception e){status.Text=e.Message;}}));options.Children.Add(Tool("清空材料",()=>{try{RequireVisualIdle();visualMaterials.Clear();InvalidateVisual();RefreshVisualPanel();}catch(Exception e){status.Text=e.Message;}}));
  options.Children.Add(Tool("查看／收起材料",()=>{visualDetailsExpanded=!visualDetailsExpanded;RefreshVisualPanel();}));options.Children.Add(Tool("放大预览",ShowVisualPreview));
  visualMaterialScroll.Content=visualThumbnails;top.Children.Add(options);top.Children.Add(visualSourceLabel);top.Children.Add(visualMaterialScroll);
  void Changed(){if(changingVisual)return;try{SetVisualOptions(VisualSources[Math.Max(0,visualSourceBox.SelectedIndex)],VisualKinds[Math.Max(0,visualKindBox.SelectedIndex)],VisualLanguages[Math.Max(0,visualLanguageBox.SelectedIndex)]);}catch(Exception e){status.Text=e.Message;RefreshVisualPanel();}}
  visualSourceBox.SelectionChanged+=(_,_)=>Changed();visualKindBox.SelectionChanged+=(_,_)=>Changed();visualLanguageBox.SelectionChanged+=(_,_)=>Changed();RefreshVisualPanel();
 }
 void RequireVisualIdle(){if(cts is not null)throw new InvalidOperationException("请等待当前任务完成，或先点停止，再修改材料和图解类型。");}
 void SetVisualOptions(string source,string kind,string language){
  if(!VisualSources.Contains(source)||!VisualKinds.Contains(kind)||!VisualLanguages.Contains(language))throw new InvalidOperationException("图解选项无效。");
  if(source==visualSource&&kind==visualKind&&language==visualLanguage)return;
  RequireVisualIdle();if(source!=visualSource)visualMaterials.Renew();visualSource=source;visualKind=kind;visualLanguage=language;InvalidateVisual();RefreshVisualPanel();
 }
 void ClearVisualPicture(){visualPreviewWindow?.Close();visualPreview="";picturePath=null;insert.IsEnabled=false;preview.Source=null;revision++;ScheduleState();}
 void InvalidateVisual(){ClearVisualPicture();visualDesign=null;visualRecommendations=false;visualFingerprint=selectedVisualId="";suggestions=Array.Empty<ImageSuggestion>();suggestionCards.Children.Clear();ScheduleState();}
 void RefreshVisualPanel(){
  changingVisual=true;visualSourceBox.SelectedIndex=Array.IndexOf(VisualSources,visualSource);visualKindBox.SelectedIndex=Array.IndexOf(VisualKinds,visualKind);visualLanguageBox.SelectedIndex=Array.IndexOf(VisualLanguages,visualLanguage);changingVisual=false;
  visualSourceLabel.Text=visualSource=="materials"?$"依据：拍照材料 {visualMaterials.Items.Count}/6 张 · 可继续补图；补图后重新生成":"依据："+(visualSource=="text"?"教师文字要求":"当前可见课件")+" · 英语默认中英对照，其他学科按内容组织。";
  visualMaterialScroll.Visibility=visualSource=="materials"&&visualDetailsExpanded?Visibility.Visible:Visibility.Collapsed;
  visualThumbnails.Children.Clear();if(visualSource=="materials")foreach(var item in visualMaterials.Items){
   var box=new StackPanel{Width=155,Margin=new Thickness(5)};box.Children.Add(new Image{Source=item.Image,Height=75,Stretch=Stretch.Uniform});box.Children.Add(new TextBlock{Text="第"+(visualMaterials.Items.IndexOf(item)+1)+"张",FontSize=17,HorizontalAlignment=HorizontalAlignment.Center});
   var buttons=new WrapPanel();void Action(Action action){try{RequireVisualIdle();action();InvalidateVisual();RefreshVisualPanel();}catch(Exception e){status.Text=e.Message;}}
   buttons.Children.Add(Tool("←",()=>Action(()=>visualMaterials.Move(item.Id,-1,visualMaterials.Revision))));buttons.Children.Add(Tool("→",()=>Action(()=>visualMaterials.Move(item.Id,1,visualMaterials.Revision))));buttons.Children.Add(Tool("删",()=>Action(()=>visualMaterials.Remove(item.Id,visualMaterials.Revision))));box.Children.Add(buttons);visualThumbnails.Children.Add(box);
  }ScheduleState();
 }
 void AddVisualFiles(){
  RequireVisualIdle();var dialog=new OpenFileDialog{Title="添加教材／文章照片（可多选，按页顺序添加）",Filter="材料图片|*.jpg;*.jpeg;*.png",Multiselect=true};if(dialog.ShowDialog(this)!=true)return;
  if(dialog.FileNames.Length+visualMaterials.Items.Count>6)throw new InvalidOperationException("同组材料最多6张。");
  SetVisualOptions("materials",visualKind,visualLanguage);visualDetailsExpanded=true;
  try{foreach(string file in dialog.FileNames){if(new FileInfo(file).Length>30*1024*1024)throw new InvalidOperationException("单张照片超过30MB，请先裁剪。");var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=2200;bitmap.UriSource=new Uri(file);bitmap.EndInit();bitmap.Freeze();visualMaterials.Add(Guid.NewGuid().ToString("N"),VisualBitmap.Preview(bitmap,2200,3800000));}}
  finally{InvalidateVisual();RefreshVisualPanel();}
 }
 object VisualState()=>new{source=visualSource,kind=visualKind,language=visualLanguage,collectionId=visualMaterials.CollectionId,revision=visualMaterials.Revision,count=visualMaterials.Items.Count,bytes=visualMaterials.Bytes,
  materials=visualMaterials.Items.Select((x,i)=>new{id=x.Id,index=i+1,thumbnail=x.Thumbnail}),preview=visualPreview,ready=picturePath is not null,
  subject=visualDesign?.Subject??"",resolvedLanguage=visualDesign?.Language??"",selectedId=selectedVisualId,
  plans=visualRecommendations?visualDesign?.Plans.Select(x=>new{id=x.Id,title=x.Title,purpose=x.Purpose,kind=x.Kind,prompt=x.Choice}):null};
 void ResetVisualSession(){visualMaterials.Clear();visualSource="page";visualKind=visualLanguage="auto";InvalidateVisual();RefreshVisualPanel();}
 void MaterialCommand(JsonElement message,string action){
  RequireVisualIdle();if(message.GetProperty("collectionId").GetString()!=visualMaterials.CollectionId)throw new InvalidOperationException("材料组已更新，请等待同步后重试。");
  if(action=="visual_add"){
   if(visualSource!="materials")throw new InvalidOperationException("请先切换到拍照材料，再添加照片。");
   if(!visualMaterials.Add(message.GetProperty("materialId").GetString()??"",message.GetProperty("photo").GetString()??""))return;
  }else if(action=="visual_remove")visualMaterials.Remove(message.GetProperty("materialId").GetString()??"",message.GetProperty("materialRevision").GetInt32());
  else if(action=="visual_move")visualMaterials.Move(message.GetProperty("materialId").GetString()??"",message.GetProperty("direction").GetInt32(),message.GetProperty("materialRevision").GetInt32());
  else if(action=="visual_clear"){visualMaterials.Check(message.GetProperty("materialRevision").GetInt32());visualMaterials.Clear();}
  visualDetailsExpanded=true;InvalidateVisual();RefreshVisualPanel();status.Text="材料已更新，共"+visualMaterials.Items.Count+"张。可直接生成，或先选配图建议；添加材料本身不调用AI。";
 }
 sealed record VisualSnapshot(string Stamp,List<object> Content,string Description);
 VisualSnapshot CaptureVisual(){
  string text=drafts["image"].Trim();var content=new List<object>();string stamp,description;
  if(visualSource=="materials"){
   if(visualMaterials.Items.Count==0)throw new InvalidOperationException("请先拍照或添加一张材料；之后也可继续补图。");
   stamp="materials:"+visualMaterials.CollectionId+":"+visualMaterials.Revision;description="拍照材料，共"+visualMaterials.Items.Count+"张（按顺序合并）";
   content=VisualDesign.MaterialContent(visualMaterials.Items);
  }else if(visualSource=="text"){
   if(text.Length==0)throw new InvalidOperationException("请写一句要讲清的内容，或选择课件／拍照材料。");stamp="text";description="教师文字要求";
  }else{var page=ReadPage();content=PageContent(page,"只依据当前课件材料组织课堂图解，不扩写材料外事实。",true);stamp="page:"+page.Page.Identity+":"+VisualBitmap.Hash(page.Text+Convert.ToBase64String(File.ReadAllBytes(page.ImagePath)));description=page.Page.Description;}
  content.Add(new{type="text",text="教师想讲清什么（可为空）："+text+"\n图解类型："+visualKind+"；语言选择："+visualLanguage+"。auto语言时英语材料使用中英对照，其他学科依据内容与术语选择。"});
  return new(VisualBitmap.Hash(stamp+"|"+text+"|"+visualKind+"|"+visualLanguage),content,description);
 }
 async Task<VisualDesign> PlanVisual(VisualSnapshot snapshot,CancellationToken token,bool recommendations=false){
  if(visualDesign is not null&&visualFingerprint==snapshot.Stamp&&(!recommendations||visualRecommendations))return visualDesign;
  status.Text="正在合并阅读材料并设计课堂图解…";ScheduleState();
  var planning=JsonSerializer.Deserialize<Connection>(JsonSerializer.Serialize(config))!;planning.EnableSearch=false;
  string instruction=recommendations?"此次点击配图建议：最多给2个有区别的方案，便于老师选择。":"此次课堂直接生成：仅输出1个最合适的完整方案，不生成备选。auto类型时优先清晰的内容脉络/关系/过程图；教师明确要求场景或情境时用scene。";
  string raw=await ModelEngine.Complete(planning,VisualDesign.SystemPrompt+"\n"+instruction,snapshot.Content,_=>{},s=>Dispatcher.Invoke(()=>{if(!token.IsCancellationRequested){status.Text=s;ScheduleState();}}),token);
  token.ThrowIfCancellationRequested();if(CaptureVisual().Stamp!=snapshot.Stamp)throw new InvalidOperationException("材料或要求已变化，本次旧方案未使用。");
  var design=VisualDesign.Parse(raw);if(visualKind is "outline" or "relations" or "process" or "scene"){design.Plans=design.Plans.Where(x=>x.Kind==visualKind).ToList();if(design.Plans.Count==0)throw new InvalidOperationException("未得到所选类型的图解，请重试或选择自动推荐。");}ClearVisualPicture();visualDesign=design;visualRecommendations=recommendations;visualFingerprint=snapshot.Stamp;questionSource=snapshot.Description;selectedVisualId="";if(recommendations)RenderVisualSuggestions();else{suggestionCards.Children.Clear();suggestionCards.Visibility=Visibility.Collapsed;ScheduleState();}return design;
 }
 void RenderVisualSuggestions(){
  suggestionCards.Children.Clear();suggestionCards.Visibility=Visibility.Visible;if(visualDesign is null)return;
  foreach(var plan in visualDesign.Plans){var button=new Button{Padding=new Thickness(12),Margin=new Thickness(0,0,0,7),HorizontalContentAlignment=HorizontalAlignment.Stretch,Content=new TextBlock{Text=plan.Title+" · "+plan.Purpose,FontSize=25,TextWrapping=TextWrapping.Wrap}};button.Click+=(_,_)=>{try{UseVisualPlan(plan.Id);}catch(Exception e){status.Text=e.Message;}};suggestionCards.Children.Add(button);}ScheduleState();
 }
 void UseVisualPlan(string id){
  RequireVisualIdle();if(visualDesign?.Plans.All(x=>x.Id!=id)!=false||CaptureVisual().Stamp!=visualFingerprint)throw new InvalidOperationException("方案已过期，请按当前材料重新获取建议。");if(selectedVisualId!=id)ClearVisualPicture();selectedVisualId=id;status.Text="已选择："+visualDesign.Plans.First(x=>x.Id==id).Title+"。可生成，或补充讲解要求后重新设计。";ScheduleState();
 }
 async Task SuggestVisuals(CancellationToken token){SetMode("image");var snapshot=CaptureVisual();await PlanVisual(snapshot,token,true);status.Text="已推荐图解方案。请选择后生成，也可直接生成第一个方案；英语材料默认中英对照。";ScheduleState();}
 async Task GenerateVisual(CancellationToken token){
  SetMode("image");StopPlayback();visualRecommendations=false;suggestionCards.Children.Clear();suggestionCards.Visibility=Visibility.Collapsed;var snapshot=CaptureVisual();var design=await PlanVisual(snapshot,token);var plan=design.Plans.FirstOrDefault(x=>x.Id==selectedVisualId)??design.Plans[0];selectedVisualId=plan.Id;ClearVisualPicture();
  status.Text=plan.Kind=="scene"?"正在生成情境插画，可随时停止…":"正在排版课堂图解…";ScheduleState();string path;
  path=await VisualGeneration.Create(plan,design.Subject,visualLanguage,Path.Combine(RuntimeFiles.Root,"教学图解"),async(prompt,ct)=>{status.Text="正在调用云端生成讲解图，可在手机查看进度；助教无需展开…";ScheduleState();var reply=await ImageApi.Generate(config.ApiKey,prompt,ct,Path.Combine(RuntimeFiles.Root,"教学图解"));return reply.FilePath;},token);
  token.ThrowIfCancellationRequested();if(CaptureVisual().Stamp!=snapshot.Stamp)throw new InvalidOperationException("材料已变化，旧图未作为本次结果显示。");
  LoadPicture(path);status.Text="已生成："+plan.Title+" · 依据"+snapshot.Description+"。请先核对预览，再插入当前页；也可继续补图重新生成。";ScheduleState();
  visualDetailsExpanded=false;suggestionCards.Visibility=Visibility.Collapsed;RefreshVisualPanel();
 }
 void ShowVisualPreview(){if(preview.Source is not BitmapSource source){status.Text="请先生成一张讲解图。";return;}visualPreviewWindow?.Close();var window=new Window{Title="教学讲解图预览",Owner=this,Width=SystemParameters.WorkArea.Width*.92,Height=SystemParameters.WorkArea.Height*.92,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=new Image{Source=source,Stretch=Stretch.Uniform},Background=System.Windows.Media.Brushes.White};visualPreviewWindow=window;window.Closed+=(_,_)=>{if(ReferenceEquals(visualPreviewWindow,window))visualPreviewWindow=null;};window.Show();}
}
