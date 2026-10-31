using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ScienceSupport;
using WF=System.Windows.Forms;
namespace PhotoTutor {
 internal sealed partial class TutorForm {
  readonly WF.TabControl rightTabs=new WF.TabControl{Dock=WF.DockStyle.Fill};
  readonly LessonPanel practiceView=new LessonPanel{Dock=WF.DockStyle.Fill};
  PracticeQuestion? practice;int practiceReveal;
  void InitPractice(){
   var mainTab=new WF.TabPage("题目讲解");mainTab.Controls.Add(lesson);rightTabs.TabPages.Add(mainTab);
   var practiceTab=new WF.TabPage("举一反三");var panel=new WF.TableLayoutPanel{Dock=WF.DockStyle.Fill,ColumnCount=1,RowCount=2};panel.RowStyles.Add(new WF.RowStyle(WF.SizeType.AutoSize));panel.RowStyles.Add(new WF.RowStyle(WF.SizeType.Percent,100));var tools=Row();
   tools.Controls.Add(B("生成相似题",()=>_=Guard(GeneratePractice)));tools.Controls.Add(B("给点提示",()=>RevealPractice(1)));tools.Controls.Add(B("显示答案",()=>RevealPractice(2)));tools.Controls.Add(B("显示解析",()=>RevealPractice(3)));tools.Controls.Add(B("只看题目",()=>RevealPractice(0)));
   panel.Controls.Add(tools,0,0);panel.Controls.Add(practiceView,0,1);practiceTab.Controls.Add(panel);rightTabs.TabPages.Add(practiceTab);
   practiceView.Display("先拍题、提问或完成一次讲解，再点击出相似题。\n题目默认不含答案，提示、答案、解析可分别展开。",false);
   fontSize.SelectedIndexChanged+=(_,__)=>{practiceView.BodyPoints=lesson.BodyPoints;if(practice!=null)RevealPractice(practiceReveal);};
  }
  void RevealPractice(int step){if(practice==null){status.Text="请先生成相似题。";return;}practiceReveal=step;practiceView.BodyPoints=lesson.BodyPoints;practiceView.Display(practice.Display(step),true);practiceView.AutoScrollPosition=Point.Empty;}
  void ResetPractice(){practice=null;practiceReveal=0;practiceView.Display("原题已更新，请按当前题目重新生成练习。",false);rightTabs.SelectedIndex=0;}
  async Task GeneratePractice(){
   if(work!=null){status.Text="当前请求仍在处理，请等待或先点击停止。";return;}
   if(photo==null&&dialogue.Count==0&&string.IsNullOrWhiteSpace(question.Text))throw new InvalidOperationException("请先拍题或输入原题，再出相似题。");
   Stop("");CloseCamera();if(photo==null)SetAnswerOnly(true);DailyUse.Begin(settings.DailyLimit);var local=new CancellationTokenSource();work=local;long id=version;Buttons();practice=null;practiceReveal=0;rightTabs.SelectedIndex=1;practiceView.Display("正在根据原题生成并核对一道相似题…\n答案暂不展示，可随时停止。",false);
   try{
    var result=await AiClient.Solve(settings,photo,"以全部题图和原题为依据生成一道相似练习。教师补充或原题："+question.Text,_=>{},s=>Ui(()=>{if(!local.IsCancellationRequested&&id==version)status.Text=s;}),local.Token,dialogue.Context(),PracticeQuestion.SystemPrompt,pages.ToArray(),regionPayload?.Instruction);
    if(local.IsCancellationRequested||id!=version)return;
    var item=JsonConvert.DeserializeObject<PracticeQuestion>(PracticeQuestion.JsonBody(result.Text))??throw new InvalidOperationException("练习格式无效，请重试。");item.Validate();practice=item;RevealPractice(0);status.Text="相似题已生成 · 默认只展示题干 · 可按需展开答案和解析。";
   }catch(OperationCanceledException){practiceView.Display("本次出题已停止，原题与讲解保留。",false);}
   catch(Exception){practiceView.Display("相似题未完成，请核对原题后重试；原题与讲解保留。",false);throw;}
   finally{if(ReferenceEquals(work,local)){work=null;Buttons();}local.Dispose();}
  }
 }
}
