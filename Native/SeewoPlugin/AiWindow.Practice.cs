using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ScienceSupport;

namespace ClassroomAI.Seewo;
internal sealed partial class AiWindow {
 readonly ClassroomText practiceView=new();readonly WrapPanel practiceTools=new();
 PracticeQuestion? practice;int practiceReveal;string practiceSource="",practiceOrigin="qa";
 void InitPracticeTab(){
  practiceTools.Children.Add(Tool("生成相似题",()=>Start(t=>GeneratePractice(t,practiceOrigin))));
  practiceTools.Children.Add(Tool("给点提示",()=>RevealPractice(1)));
  practiceTools.Children.Add(Tool("显示答案",()=>RevealPractice(2)));
  practiceTools.Children.Add(Tool("显示解析",()=>RevealPractice(3)));
  practiceTools.Children.Add(Tool("只看题目",()=>RevealPractice(0)));
  tabs.Items.Add(new TabItem{Header="举一反三",Content=practiceView});
  practiceView.Text="根据当前页或学生照片生成一道相似题。\n默认只显示题目，提示、答案与解析由老师按需展开。";
 }
 void RevealPractice(int step){if(practice is null){status.Text="请先生成一道相似题。";return;}practiceReveal=step;practiceView.Text=practice.Display(step);practiceView.ScrollToTop();ScheduleState();}
 void InvalidatePhotoPractice(){if(practiceOrigin!="photo")return;practice=null;practiceReveal=0;practiceSource="";practiceView.Text="学生照片已更换或清除，请按新照片重新生成练习。";ScheduleState();}
 async Task GeneratePractice(CancellationToken token,string origin){
  practiceOrigin=origin=="photo"?"photo":"qa";SetMode("practice");practice=null;practiceReveal=0;
  practiceView.Text="正在依据原题生成并核对一道相似题…\n答案暂不展示。";status.Text="正在命题并自检，可随时停止。";ScheduleState();
  var snapshot=ReadPage();var photoId=studentPhotoId;var content=PageContent(snapshot,"仅根据本页真实题目生成一道高中相似题，不沿用其他页面的旧输入。学情："+level.SelectedItem);
  if(practiceOrigin=="photo"){
   if(studentPhotoData is null)throw new InvalidOperationException("请先上传学生照片，或回课堂问答按本页出题。");
   content.Add(new{type="text",text="以下学生照片是本次变式的主依据；当前页仅在题干一致时参考。不要把点评结论直接当题干。"});
   content.Add(new{type="image_url",image_url=new{url=studentPhotoData,detail="high"}});
  }
  practiceSource=practiceOrigin=="photo"?"学生照片 · "+snapshot.Page.Description:snapshot.Page.Description;
  var raw=await ModelEngine.Complete(config,PracticeQuestion.SystemPrompt,content,_=>{},s=>Dispatcher.Invoke(()=>{if(!token.IsCancellationRequested){status.Text=s;ScheduleState();}}),token);
  token.ThrowIfCancellationRequested();if(!snapshot.Page.IsStillCurrent()||(practiceOrigin=="photo"&&photoId!=studentPhotoId))throw new OperationCanceledException();
  var item=JsonSerializer.Deserialize<PracticeQuestion>(PracticeQuestion.JsonBody(raw),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidOperationException("练习格式无效，请重试。");item.Validate();
  practice=item;RevealPractice(0);status.Text="相似题已生成 · 默认不显示答案 · 依据"+practiceSource;ScheduleState();
 }
 object PracticeState()=>new{ready=practice is not null,title=practice?.Title??"举一反三",text=practice?.Display(practiceReveal)??practiceView.Text,reveal=practiceReveal,source=practiceSource};
}
