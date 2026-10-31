using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WF=System.Windows.Forms;
namespace PhotoTutor {
 internal static class Revision130Checks {
  internal static readonly string Folder=Path.Combine(Path.GetTempPath(),"PhotoTutorCaptureChecks");
  internal static PhotoData[] Fixtures(){Directory.CreateDirectory(Folder);var list=new List<PhotoData>();string[] text={"跨页测试 · 第1张\n\n已知函数 f(x)=x²−4x+k。\n\n附加条件与问题在第2张。","跨页测试 · 第2张\n\n承接第1张，已知 f(1)=2。\n\n（1）求 k 的值。\n（2）求函数最小值及取得时的 x。"};for(int i=0;i<text.Length;i++){string path=Path.Combine(Folder,"跨页测试_"+(i+1)+".png");using(var bmp=new Bitmap(1200,700))using(var graphics=Graphics.FromImage(bmp))using(var font=new Font("Microsoft YaHei",30)){graphics.Clear(Color.White);graphics.DrawString(text[i],font,Brushes.Black,new RectangleF(60,50,1080,600));bmp.Save(path,System.Drawing.Imaging.ImageFormat.Png);}list.Add(PhotoData.FromFile(path,false));}return list.ToArray();}
  internal static int Live(){var images=Fixtures();var settings=Settings.Load();var r=AiClient.Solve(settings,images[0],"合并两张图片按教材解答格式完整作答，最后写本题完成。",_=>{},_=>{},CancellationToken.None,null,null,images).GetAwaiter().GetResult();File.WriteAllText(Path.Combine(Folder,"跨页两图_真实模型回答.md"),r.Text,Encoding.UTF8);File.WriteAllText(Path.Combine(Folder,"跨页两图_真实接口.json"),JsonConvert.SerializeObject(new{imageCount=2,imageBytes=images.Sum(x=>x.Jpeg.Length),firstSeconds=r.FirstSeconds,totalSeconds=r.TotalSeconds,complete=r.Complete,characters=r.Text.Length},Formatting.Indented));return r.Complete?0:1;}
  sealed class Fake:HttpMessageHandler {
   internal JObject? Request;
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Request=JObject.Parse(await request.Content.ReadAsStringAsync());return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"答案完整\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",Encoding.UTF8,"text/event-stream")};}
  }
  internal static int Run(){Directory.CreateDirectory(Folder);Fixtures();var results=new List<object>();int failed=0;
   void Check(string name,Action action){try{action();results.Add(new{name,passed=true});}catch(Exception e){failed++;results.Add(new{name,passed=false,error=e.ToString()});}}
   Check("框选坐标包含留白和缩放换算",()=>{var fit=CropGeometry.Fit(new Size(1000,800),new Size(1600,800));var rect=CropGeometry.Pixels(new RectangleF(125,275,500,250),fit,new Size(1600,800));if(rect.X!=200||rect.Y!=200||rect.Width!=800||rect.Height!=400)throw new Exception(rect.ToString());});
   Check("反向框选和越界夹取",()=>{var fit=new RectangleF(0,0,800,600);var rect=CropGeometry.Pixels(new RectangleF(700,550,-600,-500),fit,new Size(1600,1200));if(rect.X!=200||rect.Y!=100||rect.Width!=1200||rect.Height!=1000)throw new Exception(rect.ToString());var all=CropGeometry.Pixels(new RectangleF(-100,-100,1200,1000),fit,new Size(1600,1200));if(all.Width!=1600||all.Height!=1200)throw new Exception("越界");});
   var photo=PhotoData.Create(Checks.Chart(),"软件题图",false);
   Check("实际裁剪尺寸及原图保留",()=>{var cut=photo.Crop(new System.Windows.Int32Rect(20,30,200,150),false);if(cut.Image.PixelWidth!=200||cut.Image.PixelHeight!=150||photo.Image.PixelWidth<=200)throw new Exception("尺寸不对");});
   Check("两张题图直接作为两个图像发送",()=>{using(var fake=new Fake())using(var http=new HttpClient(fake)){var s=new Settings{DeepSeekApiKey="offline-only"};AiClient.SolveUsing(http,s,photo,"完整解答",_=>{},_=>{},CancellationToken.None,null,null,new[]{photo,photo}).GetAwaiter().GetResult();var messages=(JArray)fake.Request!["messages"]!;var images=((JArray)messages[1]!["content"]!).OfType<JObject>().Count(x=>(string?)x["type"]=="image_url");if(images!=2||fake.Request["max_tokens"]!=null)throw new Exception("多图丢失或输出上限被重加");}});
   Check("跨页追问仍带齐全部图片",()=>{using(var fake=new Fake())using(var http=new HttpClient(fake)){var s=new Settings{DeepSeekApiKey="offline-only"};AiClient.SolveUsing(http,s,photo,"解释第二问",_=>{},_=>{},CancellationToken.None,new[]{new ChatTurn("user","先解第一问"),new ChatTurn("assistant","第一问已完成")},null,new[]{photo,photo}).GetAwaiter().GetResult();var messages=(JArray)fake.Request!["messages"]!;if(((JArray)messages[1]!["content"]!).OfType<JObject>().Count(x=>(string?)x["type"]=="image_url")!=2||messages.Count!=5)throw new Exception("追问原题丢失");}});
   Check("六张上限在发送前校验",()=>{using(var fake=new Fake())using(var http=new HttpClient(fake)){try{AiClient.SolveUsing(http,new Settings{DeepSeekApiKey="offline-only"},photo,"解题",_=>{},_=>{},CancellationToken.None,null,null,Enumerable.Repeat(photo,7).ToArray()).GetAwaiter().GetResult();throw new Exception("未拒绝");}catch(InvalidOperationException){if(fake.Request!=null)throw new Exception("校验前已发送");}}});
   Check("现场截图根号与向量可渲染",()=>{foreach(string formula in new[]{@"\vec{AB}=(2-0,\ 0-0,\ 0-0)=(2,0,0)",@"F_1(-\sqrt3,0),\quad F_2(\sqrt3,0)",@"\overrightarrow{MF_1}=(-\sqrt3-x,-y)",@"\overrightarrow{MF_2}=(\sqrt3-x,-y)",@"\frac{x^2}{4}+\frac{y^2}{1}=1"})if(!ScienceSupport.CloudMath.Prefer(formula))WpfMath.Parsers.WpfTeXFormulaParser.Instance.Parse(ScienceSupport.ScientificNotation.Normalize(formula));});
   Check("分步书写和对齐公式不显示红色代码",()=>{using(var form=new WF.Form{Width=1200,Height=1050})using(var panel=new LessonPanel{Dock=WF.DockStyle.Fill}){form.Controls.Add(panel);string text="# 解\n由椭圆方程可得两个焦点：\n$$F_1(-\\sqrt3,0),\\quad F_2(\\sqrt3,0)$$\n设椭圆上一点 M(x,y)，则\n$$\\begin{aligned}\\overrightarrow{MF_1}&=(-\\sqrt3-x,-y)\\\\\\overrightarrow{MF_2}&=(\\sqrt3-x,-y)\\end{aligned}$$\n向量夹角为钝角时：\n$$\\vec{MF_1}\\cdot\\vec{MF_2}<0$$\n故按点积条件判断，并代入原方程核验。\n本题完成。";panel.Display(text,true);form.Show();WF.Application.DoEvents();if(panel.Controls.OfType<WF.Label>().Any(x=>x.ForeColor==Color.DarkRed))throw new Exception("仍有红色原始公式");if(panel.Controls.OfType<WF.Panel>().Count(x=>(string?)x.Tag=="formula")<4)throw new Exception("公式未分行");using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(Folder,"书写格式_现场公式复测.png"));}form.Close();}});
   Check("行内公式与中文保持同一行",()=>{using(var f=new WF.Form{Width=1200,Height=700})using(var panel=new LessonPanel{Dock=WF.DockStyle.Fill}){f.Controls.Add(panel);panel.Display("由 $c=\\sqrt3$ 得到焦点。\n$$\\vec{AB}=(2-0,\\ 0-0,\\ 0-0)=(2,0,0)$$",true);f.Show();WF.Application.DoEvents();if(!panel.Controls.OfType<InlineMathRow>().Any()||panel.Controls.OfType<WF.Label>().Any(x=>x.ForeColor==Color.DarkRed))throw new Exception("行内公式失败");using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,f.Size));bmp.Save(Path.Combine(Folder,"行内公式与转义空格.png"));}f.Close();}});
   File.WriteAllText(Path.Combine(Folder,"1.3.0离线回归.json"),JsonConvert.SerializeObject(new{failed,checks=results},Formatting.Indented),Encoding.UTF8);return failed==0?0:1;
  }
 }
}
