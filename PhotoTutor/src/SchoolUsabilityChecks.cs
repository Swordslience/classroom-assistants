using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using WF=System.Windows.Forms;
namespace PhotoTutor {
 internal static class SchoolUsabilityChecks {
  static bool Pump(Func<bool> done,int seconds=8){var until=DateTime.UtcNow.AddSeconds(seconds);while(DateTime.UtcNow<until){WF.Application.DoEvents();if(done())return true;Thread.Sleep(20);}return false;}
  static IEnumerable<WF.Control> Descendants(WF.Control root){foreach(WF.Control c in root.Controls){yield return c;foreach(var child in Descendants(c))yield return child;}}
  internal static int Run(string folder){Directory.CreateDirectory(folder);Settings.IsolatedTestRoot=Path.Combine(folder,"isolated-runtime");int failed=0;var checks=new List<object>();void Check(string name,Action test){try{test();checks.Add(new{name,pass=true});}catch(Exception e){failed++;checks.Add(new{name,pass=false,error=e.ToString()});}}
   Check("4K在200%和300%默认宽窗靠下且不越界",()=>{foreach(int dpi in new[]{96,192,288}){var work=new Rectangle(100,50,dpi==96?1920:3840,dpi==96?1040:2080);var r=ClassroomWindowPlacement.Calculate(work,dpi);if(!work.Contains(r)||r.Width<work.Width*.80||r.Top<work.Top+work.Height*.15||r.Bottom>work.Bottom||r.Bottom<work.Bottom-100)throw new Exception("窗口位置不符合下端宽窗要求："+r);}});
   Check("本地boxed与常见角度斜率直接显示无需云端",()=>{foreach(var expression in new[]{@"\boxed{\alpha=0^\circ,\ k=0;}",@"\boxed{\alpha=60^\circ,\quad k=\sqrt{3}}",@"k_{BD}=\frac{\frac{\sqrt{3}a}{2}-0}{\frac{a}{2}-a}=-\sqrt{3}"})using(var image=FormulaRaster.Local(expression,32)){if(image.Width<10||image.Height<10)throw new Exception("本机公式无图形");}});
   Check("公式与Unicode按同一DPI缩放而非固定小图",()=>{using(var a=FormulaRaster.Local(@"\alpha=60^\circ",32))using(var b=FormulaRaster.Local(@"\alpha=60^\circ",64))using(var c=FormulaRaster.Local(@"\alpha=60^\circ",96)){if(Math.Abs(b.Height/(double)a.Height-2)>.2||Math.Abs(c.Width/(double)a.Width-3)>.2)throw new Exception("公式未按DPI同比缩放");}using(var font=new Font("Microsoft YaHei",24))using(var a=new InlineMathRow(2000,font,"x⃗",96))using(var b=new InlineMathRow(2000,font,"x⃗",288)){a.AddText("x⃗");b.AddText("x⃗");a.Reflow();b.Reflow();if(b.Height<a.Height*2.5)throw new Exception("行内字形未缩放");}});
   Check("正文行内公式与盒装答案的实际排版截图",()=>{using(var form=new WF.Form{Width=1500,Height=900})using(var panel=new LessonPanel{Dock=WF.DockStyle.Fill}){form.Controls.Add(panel);panel.Display("# 角度与斜率\n对角线 $BD$：\n$$k_{BD}=\\frac{\\frac{\\sqrt{3}a}{2}-0}{\\frac{a}{2}-a}=-\\sqrt{3}$$\n因为倾斜角 $\\alpha\\in[0^\\circ,180^\\circ)$，且 $\\tan120^\\circ=-\\sqrt{3}$，所以结论为：\n$$\\boxed{\\alpha=120^\\circ,\\quad k=-\\sqrt{3}}$$\n向量字符：𝐯 𝐀 x⃗。",true);form.Show();WF.Application.DoEvents();if(panel.Controls.OfType<WF.Label>().Any(x=>x.ForeColor==Color.DarkRed))throw new Exception("仍有错误公式");if(panel.Controls.OfType<WF.Label>().Any(x=>x.Text.Contains("x⃗")))throw new Exception("组合向量仍绕过字形渲染");using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(folder,"角度斜率_字号统一.png"));}form.Close();}});
   Check("模拟设备启动自动预览且更新入口可见无打开按钮",()=>{Entry.DemoCamera=true;WF.Application.EnableVisualStyles();using(var form=new TutorForm()){form.Show();if(!Pump(()=>typeof(TutorForm).GetField("live",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!=null))throw new Exception("未自动打开模拟高拍仪");var controls=Descendants(form).ToArray();var update=controls.OfType<WF.Button>().Single(x=>x.Text=="检查更新");var bounds=form.RectangleToScreen(form.ClientRectangle);if(!update.Visible||!bounds.Contains(update.RectangleToScreen(update.ClientRectangle)))throw new Exception("更新入口不在窗口内");if(controls.OfType<WF.Button>().Any(x=>x.Text.Contains("打开高拍仪")||x.Text.Contains("关闭高拍仪")))throw new Exception("仍有重复打开按钮");var clear=controls.OfType<WF.Button>().Single(x=>x.Text=="清空输入");if(!bounds.Contains(clear.RectangleToScreen(clear.ClientRectangle)))throw new Exception("清空输入被底部截断");using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(folder,"默认宽窗与自动高拍仪.png"));}form.Close();Pump(()=>!CameraSession.IsActive,3);}Entry.DemoCamera=false;});
   File.WriteAllText(Path.Combine(folder,"学校体验回归.json"),JsonConvert.SerializeObject(new{failed,checks},Formatting.Indented));return failed==0?0:1;
  }
 }
}
