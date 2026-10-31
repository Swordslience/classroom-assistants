using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WF=System.Windows.Forms;

namespace PhotoTutor {
 internal static class RevisionChecks {
  sealed class Fake:HttpMessageHandler {
   public readonly List<JObject> Requests=new List<JObject>();
   readonly Queue<string> streams;
   public Fake(params string[] streams){this.streams=new Queue<string>(streams);}
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();Requests.Add(JObject.Parse(await request.Content.ReadAsStringAsync()));return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(streams.Dequeue(),Encoding.UTF8,"text/event-stream")};}
  }
  static string Sse(string text,string finish)=>"data: "+new JObject{["choices"]=new JArray(new JObject{["delta"]=new JObject{["content"]=text},["finish_reason"]=finish})}.ToString(Formatting.None)+"\n\ndata: [DONE]\n\n";
  public static int Run(){var rows=new List<object>();int failed=0;var photo=PhotoData.Create(Checks.Chart(),"离线回归题",false);var settings=new Settings{DeepSeekApiKey="offline-placeholder"};
   void Check(string name,Action run){try{run();rows.Add(new{name,passed=true});}catch(Exception e){failed++;rows.Add(new{name,passed=false,error=e.Message});}}
   Check("长讲解不受4096字符截断",()=>{string longText=new string('解',18000);using(var fake=new Fake(Sse(longText,"stop")))using(var client=new HttpClient(fake)){var result=AiClient.SolveUsing(client,settings,photo,"完整讲解",_=>{},_=>{},CancellationToken.None).GetAwaiter().GetResult();if(result.Text!=longText||fake.Requests[0]["max_tokens"]!=null||!result.Complete)throw new Exception("长内容被裁切或仍发送固定上限");}});
   Check("仅思考耗尽额度时自动恢复可见讲解",()=>{using(var fake=new Fake(Sse("","length"),Sse("答案为x=7。","stop")))using(var client=new HttpClient(fake)){var result=AiClient.SolveUsing(client,settings,photo,"完整讲解",_=>{},_=>{},CancellationToken.None).GetAwaiter().GetResult();if(result.Text!="答案为x=7。"||result.Continuations!=1||(string?)fake.Requests[1]["thinking"]?["type"]!="disabled")throw new Exception("未恢复答案");}});
   Check("中途截断续接保留原题与已输出内容",()=>{var shown=new StringBuilder();using(var fake=new Fake(Sse("第一步：移项。\n","length"),Sse("第二步：除以3，x=7。","stop")))using(var client=new HttpClient(fake)){var result=AiClient.SolveUsing(client,settings,photo,"完整讲解",p=>shown.Append(p),_=>{},CancellationToken.None).GetAwaiter().GetResult();var messages=(JArray)fake.Requests[1]["messages"]!;if(result.Text!=shown.ToString()||messages[1]?["content"]?[1]?["image_url"]?["url"]==null||(string?)messages[2]?["content"]!="第一步：移项。\n")throw new Exception("原题或前段丢失");}});
   Check("取消后不会继续自动付费请求",()=>{using(var stop=new CancellationTokenSource())using(var fake=new Fake(Sse("部分讲解","length")))using(var client=new HttpClient(fake)){try{AiClient.SolveUsing(client,settings,photo,"完整讲解",_=>stop.Cancel(),_=>{},stop.Token).GetAwaiter().GetResult();throw new Exception("没有取消");}catch(OperationCanceledException){if(fake.Requests.Count!=1)throw new Exception("停止后仍发请求");}}});
   Check("连续截断有重试保护且不伪报完成",()=>{using(var fake=new Fake(Sse("甲","length"),Sse("乙","length"),Sse("丙","length"),Sse("丁","length")))using(var client=new HttpClient(fake)){var result=AiClient.SolveUsing(client,settings,photo,"完整讲解",_=>{},_=>{},CancellationToken.None).GetAwaiter().GetResult();if(result.Complete||result.Text!="甲乙丙丁"||fake.Requests.Count!=4)throw new Exception("重试保护不正确");}});
   Check("长语音分段不丢字且不拆代理字符",()=>{string text=new string('讲',1799)+"😀。"+new string('解',6000);var parts=SpeechText.Parts(text);if(string.Concat(parts)!=text)throw new Exception("语音丢字");foreach(string part in parts)if(part.Length>1800||char.IsHighSurrogate(part[part.Length-1]))throw new Exception("分段错误");});
   Check("展开讲解会重排长文并可恢复题图",()=>{using(var split=new WF.SplitContainer{Width=1200,Height=600})using(var view=new LessonPanel{Dock=WF.DockStyle.Fill}){split.Panel2.Controls.Add(view);split.SplitterDistance=500;split.CreateControl();view.CreateControl();view.Display("完整讲解第一步\n"+new string('解',3000)+"\n最后答案：x=7",false);int narrow=view.Controls[0].MaximumSize.Width;split.Panel1Collapsed=true;split.PerformLayout();int wide=view.Controls[0].MaximumSize.Width;if(wide<=narrow)throw new Exception("展开后未重排");split.Panel1Collapsed=false;split.PerformLayout();if(view.Controls[0].MaximumSize.Width>=wide||!view.Controls[view.Controls.Count-1].Text.Contains("x=7"))throw new Exception("恢复题图或末尾内容失败");}});
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"验收");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"1.0.1回归.json"),JsonConvert.SerializeObject(new{version="1.0.1",failed,checks=rows},Formatting.Indented),Encoding.UTF8);return failed==0?0:1;
  }
 }
}
