using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PhotoTutor {
 internal static class ChatChecks {
  sealed class Fake:HttpMessageHandler {
   public JObject? Body;
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Body=JObject.Parse(await request.Content.ReadAsStringAsync());string data="data: "+new JObject{["choices"]=new JArray(new JObject{["delta"]=new JObject{["content"]="这是完整回答。"},["finish_reason"]="stop"})}.ToString(Formatting.None)+"\n\ndata: [DONE]\n\n";return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(data,Encoding.UTF8,"text/event-stream")};}
  }
  public static int Run(){var rows=new List<object>();int failed=0;var settings=new Settings{DeepSeekApiKey="offline-placeholder"};
   void Check(string name,Action action){try{action();rows.Add(new{name,passed=true});}catch(Exception e){failed++;rows.Add(new{name,passed=false,error=e.Message});}}
   Check("无照片也能发送正常文本问题",()=>{using(var fake=new Fake())using(var client=new HttpClient(fake)){var result=AiClient.SolveUsing(client,settings,null,"为什么等式两边可以同时减去5？",_=>{},_=>{},CancellationToken.None).GetAwaiter().GetResult();if(result.Text!="这是完整回答。"||fake.Body!.ToString().Contains("image_url")||(string?)fake.Body["messages"]?.Last?["content"]!="为什么等式两边可以同时减去5？")throw new Exception("纯文本请求格式错误");}});
   Check("纯文本连续追问带前一轮问题和答案",()=>{using(var fake=new Fake())using(var client=new HttpClient(fake)){var history=new[]{new ChatTurn("user","这题x是多少？"),new ChatTurn("assistant","x=7。")};AiClient.SolveUsing(client,settings,null,"为什么？",_=>{},_=>{},CancellationToken.None,history).GetAwaiter().GetResult();var messages=(JArray)fake.Body!["messages"]!;if(messages.Count!=4||(string?)messages[1]?["content"]!="这题x是多少？"||(string?)messages[2]?["content"]!="x=7。"||(string?)messages[3]?["content"]!="为什么？")throw new Exception("丢失前文");}});
   Check("拍照后文字追问保留同一题图且不重复附图",()=>{using(var fake=new Fake())using(var client=new HttpClient(fake)){var photo=PhotoData.Create(Checks.Chart(),"测试图",false);var history=new[]{new ChatTurn("user","请讲解"),new ChatTurn("assistant","先展开括号。")};AiClient.SolveUsing(client,settings,photo,"接下来怎么做？",_=>{},_=>{},CancellationToken.None,history).GetAwaiter().GetResult();string json=fake.Body!.ToString();if(json.Split(new[]{"\"image_url\": {"},StringSplitOptions.None).Length!=2||(string?)fake.Body["messages"]?.Last?["content"]!="接下来怎么做？")throw new Exception("题图上下文错误");}});
   Check("问答分别展示且新对话完全清除旧上下文",()=>{var chat=new Dialogue();chat.Pending="问题甲";chat.Commit("回答甲");chat.Pending="问题乙";string shown=chat.Display("回答乙");if(!shown.Contains("问题甲")||!shown.Contains("回答甲")||!shown.Contains("问题乙")||!shown.Contains("回答乙")||chat.Context().Length!=2)throw new Exception("对话展示或上下文错误");chat.Commit("回答乙");chat.Clear();if(chat.Context().Length!=0||chat.Display("").Length!=0)throw new Exception("新对话残留");});
   Check("保留全部可见历史但限制最近送入模型的轮次",()=>{var chat=new Dialogue();for(int i=0;i<20;i++){chat.Pending="Q"+i;chat.Commit("A"+i);}var context=chat.Context();if(context.Length>12||context.Last().Text!="A19"||!chat.Display("").Contains("Q0"))throw new Exception("历史截取错误");});
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"验收");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"1.1.0对话检查.json"),JsonConvert.SerializeObject(new{version="1.1.0",failed,checks=rows},Formatting.Indented),Encoding.UTF8);return failed==0?0:1;
  }
 }
}
