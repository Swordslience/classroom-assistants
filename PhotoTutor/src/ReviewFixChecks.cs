using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using WF=System.Windows.Forms;
namespace PhotoTutor {
 internal static class ReviewFixChecks {
  internal static int Run(string folder,bool cloud){Directory.CreateDirectory(folder);int failures=0;var checks=new List<object>();Action<string,Action> check=(name,test)=>{try{test();checks.Add(new{name,pass=true});}catch(Exception error){failures++;checks.Add(new{name,pass=false,error=error.ToString()});}};
   check("行内化学表达式必须优先专用排版",()=>{foreach(var formula in new[]{@"\ce{BaSO4 v}",@"\ce{CH3^.}",@"\ce{NH4+}",@"\ce{^{14}_{6}C}"})if(!ScienceSupport.CloudMath.Prefer(formula))throw new Exception("未选择化学排版："+formula);});
   check("长向量箭头保留原命令而非缩成单字箭头",()=>{foreach(var formula in new[]{@"\overrightarrow{MF_1}",@"\overleftarrow{AB}",@"\overleftrightarrow{AB}"})if(!ScienceSupport.CloudMath.Prefer(formula)||ScienceSupport.ScientificNotation.Normalize(formula)!=formula)throw new Exception("改变箭头语义或未选择排版");});
   check("数学粗体字符与组合箭头不可拆成两个绘制单元",()=>{using(var font=new Font("Microsoft YaHei",24)){foreach(var sample in new[]{"𝐯","𝐀","x⃗"})using(var row=new InlineMathRow(500,font,sample)){row.AddText(sample);var runs=(IList)typeof(InlineMathRow).GetField("runs",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(row)!;if(runs.Count!=1)throw new Exception(sample+"被拆成"+runs.Count+"段");string text=(string)runs[0]!.GetType().GetField("Text",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(runs[0])!;if(text!=sample)throw new Exception("字符内容改变");}}});
   check("长讲解分片保留全部文字且每片不超过800字符",()=>{string text=string.Concat(Enumerable.Repeat("向量𝐯的方向需要核对，电荷与下标不能遗漏。",160));var parts=SpeechText.Parts(text);if(string.Concat(parts)!=text||parts.Any(x=>x.Length>800||char.IsHighSurrogate(x[x.Length-1])))throw new Exception("朗读分片丢字或截断代理对");});
   if(cloud)check("实际行内化学与向量使用云图片而不产生错误简化",()=>{var formulas=new[]{@"\ce{BaSO4 v}",@"\ce{CH3^.}",@"\ce{NH4+}",@"\overrightarrow{MF_1}=(-\sqrt{3}-x,-y)"};foreach(var formula in formulas)ScienceSupport.CloudMath.Render(formula,64).GetAwaiter().GetResult();
    using(var form=new WF.Form{Width=1200,Height=1000})using(var panel=new LessonPanel{Dock=WF.DockStyle.Fill}){form.Controls.Add(panel);string text="# 行内公式语义复核\n沉淀：$"+formulas[0]+"$\n自由基：$"+formulas[1]+"$\n铵根：$"+formulas[2]+"$\n长箭头：$"+formulas[3]+"$\n粗体字符与组合箭头：𝐯 𝐀 x⃗；普通行内：$c=\\sqrt{3}$。";panel.Display(text,true);form.Show();WF.Application.DoEvents();var strips=panel.Controls.OfType<WF.Panel>().Where(p=>(string?)p.Tag=="formula").ToArray();if(strips.Length!=4||strips.Any(p=>!p.Controls.OfType<WF.PictureBox>().Any()))throw new Exception("行内复杂公式未形成四张图片");if(panel.Controls.OfType<WF.Label>().Any(l=>l.Text.Contains("BaSO₄ v")||l.Text.Contains("NH₄+")||l.Text.Contains("CH₃^.")))throw new Exception("仍显示错误化学简化");using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(folder,"行内化学_Unicode_长向量.png"));}form.Close();}
   });
   File.WriteAllText(Path.Combine(folder,"复审修复回归.json"),JsonConvert.SerializeObject(new{failures,checks},Formatting.Indented),Encoding.UTF8);return failures==0?0:1;
  }
 }
}
