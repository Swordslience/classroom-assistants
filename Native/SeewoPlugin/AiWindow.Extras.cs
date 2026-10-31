using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClassroomAiBridge;

namespace ClassroomAI.Seewo;
internal sealed record ImageSuggestion(string Title,string Purpose,string Prompt);
internal sealed partial class AiWindow {
 readonly StackPanel suggestionCards=new(){Margin=new Thickness(10)};
 ImageSuggestion[] suggestions=Array.Empty<ImageSuggestion>();
 readonly Dictionary<string,long> remoteSequences=new();
 void AddTools(Panel panel){
  panel.Children.Add(Tool("检查更新",()=>ScienceSupport.CloudUpdateLauncher.Open("seewo","0.4.19",Path.GetDirectoryName(typeof(AiWindow).Assembly.Location)!)));
  StyleTool(stop);panel.Children.Add(stop);panel.Children.Add(Tool("手机控制",()=>RemoteClient.Instance.Pairing(this)));panel.Children.Add(Tool("模型配置",Configure));
  var size=new ComboBox{ItemsSource=new[]{"24号","27号","30号","33号"},SelectedIndex=0,FontSize=20,Width=90,Margin=new Thickness(4)};
  size.SelectionChanged+=(_,_)=>{double value=32+size.SelectedIndex*4;input.FontSize=value;answer.RefreshSize(value);photoAnswer.RefreshSize(value);practiceView.RefreshSize(value);};panel.Children.Add(size);
 }
 void ScheduleState(){lastActivity=DateTime.UtcNow;if(!stateTimer.IsEnabled)stateTimer.Start();}
 object StateObject()=>new{version="0.4.19",revision,mode,visual=VisualState(),practice=PracticeState(),visible=IsVisible&&WindowState!=WindowState.Minimized,playing=playbackAllowed,drafts=new{qa=drafts["qa"],image=drafts["image"]},qaResult=answer.Text,
  suggestions=suggestions.Select(s=>new{title=s.Title,purpose=s.Purpose,prompt=s.Prompt}),photoId=studentPhotoId,photoReceived=studentPhotoData is not null,photoResult=photoAnswer.Text,
  photoHidden=!photoVisible,source=questionSource,status=status.Text,working=cts is not null,answerScroll=ScrollState(),inputScroll=InputViewportState()};
 object? InputViewportState(){if(mode is not("qa" or "image"))return null;try{return inputScroll.State(mode,CurrentPage.Resolve().Identity);}catch(InvalidOperationException){return null;}}
 ClassroomText? ScrollTarget(string area)=>area=="qa"?answer:area=="photo"?photoAnswer:area=="practice"?practiceView:null;
 object? ScrollState(){var target=ScrollTarget(mode);return target is null?null:new{mode,viewId=target.ViewId,ratio=target.ScrollableHeight<=0?0:target.VerticalOffset/target.ScrollableHeight};}
 void RemoteScroll(JsonElement message,string workspace){var target=ScrollTarget(workspace);if(target is null||workspace!=mode)return;if(!message.TryGetProperty("viewId",out var id)||id.GetString()!=target.ViewId)return;target.UpdateLayout();if(message.TryGetProperty("ratio",out var ratio)&&ratio.TryGetDouble(out var r)&&double.IsFinite(r))target.ScrollToVerticalOffset(Math.Clamp(r,0,1)*target.ScrollableHeight);else if(message.TryGetProperty("delta",out var delta)&&delta.TryGetDouble(out var d)&&double.IsFinite(d))target.ScrollToVerticalOffset(Math.Clamp(target.VerticalOffset+Math.Clamp(d,-1,1)*target.ViewportHeight,0,target.ScrollableHeight));}
 void SetMode(string next){
  if(next is not("qa" or "image" or "photo" or "practice"))return;
  if(mode!=next){drafts[mode]=input.Text;mode=next;changingInput=true;input.Text=drafts[next];changingInput=false;revision++;}
  changingMode=true;tabs.SelectedIndex=next=="qa"?0:next=="image"?1:next=="photo"?2:3;changingMode=false;
  qaTools.Visibility=next=="qa"?Visibility.Visible:Visibility.Collapsed;imageTools.Visibility=next=="image"?Visibility.Visible:Visibility.Collapsed;photoTools.Visibility=next=="photo"?Visibility.Visible:Visibility.Collapsed;
  practiceTools.Visibility=next=="practice"?Visibility.Visible:Visibility.Collapsed;
  input.Visibility=next is "photo" or "practice"?Visibility.Collapsed:Visibility.Visible;ScheduleState();
 }
 void ClearResult(string workspace){
  if(workspace=="qa")answer.Text="";else if(workspace=="image")InvalidateVisual();
 }
 void InputChanged(){if(changingInput)return;Stop();drafts[mode]=input.Text;revision++;ClearResult(mode);if(mode=="qa")readDraft="";ScheduleState();}
 void SetInput(string value,string? origin,string? workspace=null){
  workspace??=mode;if(workspace is "photo" or "practice"||!drafts.ContainsKey(workspace))return;
  if(drafts[workspace]!=value){Stop();drafts[workspace]=value;ClearResult(workspace);revision++;}
  if(mode==workspace){changingInput=true;input.Text=value;changingInput=false;}
  ScheduleState();
 }
 void ClearInput(){SetInput("",null);if(mode=="qa"){readDraft="";questionSource="";}status.Text="输入与本次旧结果已清空。";ScheduleState();}
 static AiWindow Ensure()=>current??=new AiWindow();
 internal static void ResetTeacherSession(){
  if(current is not { } w)return;
  w.Stop();w.ClearPhoto();w.ResetVisualSession();w.lockedQuestion=null;w.practice=null;w.practiceReveal=0;w.practiceSource="";w.remoteSequences.Clear();
  foreach(var key in w.drafts.Keys.ToArray())w.drafts[key]="";
  w.changingInput=true;w.input.Clear();w.changingInput=false;w.answer.Text="";w.pageText.Text="";w.preview.Source=null;w.picturePath=null;w.insert.IsEnabled=false;
  w.suggestions=Array.Empty<ImageSuggestion>();w.suggestionCards.Children.Clear();w.readDraft=w.questionSource=w.lastPageIdentity="";w.practiceView.Text="希沃身份已更新，请按当前老师的题目生成练习。";
  w.status.Text="希沃身份已更新；上一课堂内容已清空。仅关联同一希沃身份的网页账号可控制。";w.revision++;w.ScheduleState();
 }
 public static async Task Remote(JsonElement message,RemoteClient remote){
  var w=Ensure();w.AttachToPage();string action=message.GetProperty("action").GetString()??"",id=message.GetProperty("id").GetString()??"";
  string text=message.TryGetProperty("text",out var t)?t.GetString()??"":"";
  string workspace=message.TryGetProperty("mode",out var md)?md.GetString()??w.mode:w.mode;
  string origin=message.TryGetProperty("origin",out var o)?o.GetString()??"":"";
  if(action=="scroll_answer"){w.RemoteScroll(message,workspace);await remote.Send(new{type="result",id,action,status="done"});return;}
  if(action=="scroll_input"){if(workspace==w.mode&&workspace is "qa" or "image")w.inputScroll.Apply(message,workspace,CurrentPage.Resolve().Identity);return;}
  if(action=="input_sync"){
   if(message.TryGetProperty("seq",out var sq)){long seq=sq.GetInt64();string key=origin+":"+workspace;if(w.remoteSequences.TryGetValue(key,out var previous)&&seq<previous){await remote.Send(new{type="result",id,action,status="superseded",mode=workspace,seq,message="旧输入已被较新输入替代。"});return;}w.remoteSequences[key]=seq;}
   w.SetInput(text,origin,workspace);
  }else if(action=="set_mode"){if(w.mode!=workspace)w.Stop();w.SetMode(workspace);}
  else if(action=="state_request"){ }
  else if(action=="clear_input"){w.SetMode(workspace);w.ClearInput();}
  else if(action=="toggle"){if(w.IsVisible&&w.WindowState!=WindowState.Minimized)w.Hide();else w.Present();}
  else if(action=="hide")w.Hide();else if(action=="show")w.Present();
  else if(action=="stop"){w.Stop();w.status.Text="已停止回答与朗读。";}
  else if(action=="toggle_photo")w.TogglePhoto();
  else if(action=="clear_photo")w.ClearPhoto();
  else if(action=="visual_options"){w.SetMode("image");w.SetVisualOptions(message.GetProperty("visualSource").GetString()??"",message.GetProperty("visualKind").GetString()??"",message.GetProperty("visualLanguage").GetString()??"");}
  else if(action is "visual_add" or "visual_remove" or "visual_move" or "visual_clear"){w.SetMode("image");w.MaterialCommand(message,action);}
  else if(action=="use_suggestion"){w.SetMode("image");w.UseVisualPlan(message.GetProperty("suggestionId").GetString()??"");}
  else if(action.StartsWith("practice_")&&action!="practice_generate"){w.SetMode("practice");w.RevealPractice(action=="practice_hint"?1:action=="practice_answer"?2:action=="practice_solution"?3:0);}
  else if(action=="photo_sync"){
   string photoId=message.TryGetProperty("photoId",out var pi)?pi.GetString()??"":"";
   if(photoId!=w.studentPhotoId){w.Stop();w.ShowPhoto(message.GetProperty("photo").GetString()??"",photoId);w.Present();
    await remote.Send(new{type="result",id,action,status="received",photoId,message="电脑已显示照片，正在自动点评。"});
    w.Start(token=>w.AnalysePhoto(token));
   }
   await remote.Send(new{type="result",id,action,status="done",photoId=w.studentPhotoId,message="电脑已收到照片，自动点评进行中。"});
   }else if(action is "read" or "lock_question" or "unlock_question"){
    w.Present();if(action=="read")w.ReadIntoInput();else if(action=="lock_question"){w.Stop();w.LockQuestion();}else{w.Stop();w.lockedQuestion=null;w.questionSource="";w.status.Text="已解除固定，照片将参考当前页。";}
   }else{
   if(workspace!="photo"&&message.TryGetProperty("text",out _))w.SetInput(text,origin,workspace);
   if(VisualGeneration.PresentForRemote(action))w.Present();
   await remote.Send(new{type="result",id,action,status="started"});
    var outcome=await w.Run(async token=>{
    switch(action){
     case "questions":await w.Ask(token,false,true);break;
     case "practice_generate":await w.GeneratePractice(token,message.TryGetProperty("basis",out var basis)?basis.GetString()??"qa":"qa");break;
     case "ask_text":await w.Ask(token,false,false);break;
     case "ask_voice":await w.Ask(token,true,false);break;
     case "image_suggestions":w.SetMode("image");await w.SuggestImages(token);break;
     case "generate":w.SetMode("image");await w.Generate(token);break;
     case "insert":await w.Insert(token);break;
     case "replay":if(w.audioPath is null)throw new InvalidOperationException("暂无可重播语音。");await w.PlayFile(w.audioPath,token);break;
     case "grade_photo":case "explain_photo":case "review_photo":
      if(message.TryGetProperty("photo",out var ph)&&ph.ValueKind==JsonValueKind.String)w.ShowPhoto(ph.GetString()??"",message.GetProperty("photoId").GetString()??"");
      if(message.TryGetProperty("photoId",out var p)&&p.GetString()!=w.studentPhotoId)throw new InvalidOperationException("照片已更新，请先同步当前照片。");
      await w.AnalysePhoto(token);break;
     case "photo_voice":await w.SpeakText(w.photoAnswer.Text,token);break;
     default:throw new InvalidOperationException("当前版本不支持该操作，请刷新手机网页。");
    }
   });
    await remote.Send(new{type="ui_state",ui=w.StateObject()});
    await remote.Send(new{type="result",id,action,status=outcome.Status,error=outcome.Error,message=outcome.Message});return;
  }
  await remote.Send(new{type="ui_state",ui=w.StateObject()});
  await remote.Send(new{type="result",id,action,status="done",mode=action=="input_sync"?workspace:w.mode,text=action=="input_sync"?text:null,seq=message.TryGetProperty("seq",out var seqValue)?seqValue.GetInt64():0,message=w.status.Text});
 }
 async Task SuggestImages(CancellationToken token)=>await SuggestVisuals(token);
 void Configure(){
  var panel=new StackPanel{Margin=new Thickness(20)};
  var provider=new ComboBox{ItemsSource=new[]{"DeepSeek","Doubao"},SelectedItem=config.TextProvider,FontSize=22};var deep=new PasswordBox{FontSize=22};var ark=new PasswordBox{FontSize=22};var voice=new PasswordBox{FontSize=22};
  var effort=new ComboBox{ItemsSource=new[]{"low","high","max"},SelectedItem=config.ThinkingEffort,FontSize=22};var search=new CheckBox{Content="允许模型按需联网搜索",IsChecked=config.EnableSearch,FontSize=22};
  void Field(string label,Control field){panel.Children.Add(new TextBlock{Text=label,FontSize=20,Margin=new Thickness(0,10,0,4)});panel.Children.Add(field);}
  Field("文字／看图主模型",provider);Field("DeepSeek Key（留空保留）",deep);Field("豆包生图与备用模型Key（留空保留）",ark);Field("豆包语音Key（留空保留，现有音色不变）",voice);Field("思考强度：low响应更快，high/max更深入",effort);panel.Children.Add(search);
  var save=new Button{Content="保存到本机",FontSize=22,Margin=new Thickness(0,15,0,0),Padding=new Thickness(12)};
  var window=new Window{Title="奶龙助教 · 模型配置",Owner=this,Width=680,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};
  save.Click+=(_,_)=>{if(deep.Password.Length>0)config.DeepSeekApiKey=deep.Password.Trim();if(ark.Password.Length>0)config.ApiKey=ark.Password.Trim();if(voice.Password.Length>0)config.VoiceApiKey=voice.Password.Trim();config.TextProvider=(string)provider.SelectedItem;config.ThinkingEffort=(string)effort.SelectedItem;config.EnableSearch=search.IsChecked==true;Directory.CreateDirectory(BudgetGate.Root);File.WriteAllText(Path.Combine(BudgetGate.Root,"connection.json"),JsonSerializer.Serialize(config));window.Close();status.Text="配置已保存，无需重启助教。";ScheduleState();};panel.Children.Add(save);window.ShowDialog();
 }
}
