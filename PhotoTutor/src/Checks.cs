using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace PhotoTutor {
 internal static class Checks {
  public static BitmapSource Chart() {
   var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) {
    dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1600,1000));
    string text="数学练习：解一元一次方程\n\n题目：3(x - 2) + 5 = 20，求 x。\n\n学生作答：3x - 6 + 5 = 20\n                    3x = 21\n                    x = 7\n\n请核对结果，并说明每一步的依据。";
    dc.DrawText(new FormattedText(text,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),52,Brushes.Black,1),new Point(60,65));
   }
   var result=new RenderTargetBitmap(1600,1000,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
  }
  public static int Run() {
   var rows=new List<object>();int failed=0;
   void Check(string name,Action action){try{action();rows.Add(new{name,passed=true});}catch(Exception e){failed++;rows.Add(new{name,passed=false,error=e.Message});}}
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"验收");Directory.CreateDirectory(folder);var chart=Chart();
   Check("题目JPEG可编码与解码",()=>{var p=PhotoData.Create(chart,"验收题图",false);using(var s=new MemoryStream(p.Jpeg)){var decoded=BitmapDecoder.Create(s,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);if(decoded.Frames[0].PixelWidth!=1600)throw new Exception("解码宽度错误");}if(p.Jpeg.Length>1500000)throw new Exception("测试图未合理压缩");File.WriteAllBytes(Path.Combine(folder,"数学测试题.jpg"),p.Jpeg);});
   Check("旋转保留题图尺寸",()=>{var p=PhotoData.Create(chart,"验收题图",false).Rotate(false);if(p.Image.PixelWidth!=1000||p.Image.PixelHeight!=1600)throw new Exception("方向不正确");});
   Check("大图限制最长边",()=>{var large=new TransformedBitmap(chart,new ScaleTransform(2,2));var p=PhotoData.Create(large,"大图",false);if(Math.Max(p.Image.PixelWidth,p.Image.PixelHeight)>1601)throw new Exception("缩放未生效");});
   Check("过小取图被拒绝",()=>{try{PhotoData.Create(BitmapSource.Create(8,8,96,96,PixelFormats.Gray8,null,new byte[64],8),"小图",false);}catch(InvalidOperationException){return;}throw new Exception("未拒绝小图");});
   Check("数学语音分数次序",()=>{if(SpeechText.Convert(@"$\frac{3}{4}$")!="4分之3")throw new Exception("分数次序错误");});
   Check("数学语音平方和乘法",()=>{var text=SpeechText.Convert(@"x^2\times3");if(text!="x的平方乘以3")throw new Exception("语音转换错误");});
   Check("Windows配置可加密回读",()=>{var bytes=Encoding.UTF8.GetBytes("development-only");var encoded=ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser);if(Encoding.UTF8.GetString(ProtectedData.Unprotect(encoded,null,DataProtectionScope.CurrentUser))!="development-only")throw new Exception("加密回读失败");});
   Check("摄像设备枚举可退出",()=>{var all=CameraSession.Enumerate();rows.Add(new{cameraDevices=all.Length,physicalCameraTested=false});});
   Check("公式可解析",()=>{WpfMath.Parsers.WpfTeXFormulaParser.Instance.Parse(@"3(x-2)+5=20");WpfMath.Parsers.WpfTeXFormulaParser.Instance.Parse(@"x=\frac{21}{3}=7");});
   File.WriteAllText(Path.Combine(folder,"基础自检.json"),JsonConvert.SerializeObject(new{version="1.0.0",failed,checks=rows},Formatting.Indented),Encoding.UTF8);return failed==0?0:1;
  }
 }
}
