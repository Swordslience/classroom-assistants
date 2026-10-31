using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using WpfMath.Rendering;
using XamlMath;
namespace PhotoTutor {
 internal static class FormulaRaster {
  static double referenceX;
  internal static bool OuterBox(string value,out string inner){inner=value;value=value.Trim();if(!value.StartsWith(@"\boxed{"))return false;int depth=0;for(int i=6;i<value.Length;i++){if(value[i]=='\\'){i++;continue;}if(value[i]=='{')depth++;else if(value[i]=='}'&&--depth==0){if(i!=value.Length-1)return false;inner=value.Substring(7,i-7);return true;}}return false;}
  static Bitmap Raw(string expression){var formula=WpfMath.Parsers.WpfTeXFormulaParser.Instance.Parse(expression);return Raster.ToBitmap(formula.RenderToBitmap(WpfTeXEnvironment.Create(TexStyle.Display,64,"Cambria Math")));}
  static int InkHeight(Bitmap image){int top=image.Height,bottom=-1;for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(image.GetPixel(x,y).A>40){top=Math.Min(top,y);bottom=Math.Max(bottom,y);}return Math.Max(1,bottom-top+1);}
  internal static Bitmap Local(string expression,double em){bool box=OuterBox(expression,out var body);if(box)expression=body;expression=ScienceSupport.ScientificNotation.Normalize(expression);using(var raw=Raw(expression)){if(referenceX<=0)using(var x=Raw("x"))referenceX=InkHeight(x);var text=new System.Windows.Media.FormattedText("x",CultureInfo.InvariantCulture,System.Windows.FlowDirection.LeftToRight,new System.Windows.Media.Typeface("Cambria Math"),em,System.Windows.Media.Brushes.Black,1);double target=text.BuildGeometry(new System.Windows.Point()).Bounds.Height;using(var scaled=Scale(raw,target/referenceX)){if(!box)return new Bitmap(scaled);var framed=new Bitmap(scaled.Width+12,scaled.Height+12,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(framed)){g.Clear(Color.White);g.DrawImageUnscaled(scaled,6,6);using(var pen=new Pen(Color.Black,Math.Max(1,(float)(em/32))))g.DrawRectangle(pen,1,1,framed.Width-3,framed.Height-3);}return framed;}}}
  internal static Bitmap Cloud(byte[] bytes,double em){using(var stream=new MemoryStream(bytes))using(var raw=Image.FromStream(stream))return Scale(raw,em/64);}
  static Bitmap Scale(Image source,double factor){int w=Math.Max(1,(int)Math.Ceiling(source.Width*factor)),h=Math.Max(1,(int)Math.Ceiling(source.Height*factor));if(w>16000||h>12000||(long)w*h>48000000)throw new InvalidOperationException("公式显示尺寸过大，请分行查看。");var result=new Bitmap(w,h,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(result)){g.Clear(Color.White);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(source,0,0,w,h);}return result;}
 }
}
