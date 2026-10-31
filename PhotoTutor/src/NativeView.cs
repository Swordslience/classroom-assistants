using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfMath;
using WpfMath.Rendering;
using XamlMath;
using WF=System.Windows.Forms;

namespace PhotoTutor {
 internal static class Raster {
  public static Bitmap ToPreviewBitmap(BitmapSource source,int width,int height){double scale=Math.Min(1,Math.Min(Math.Max(320,width)/(double)source.PixelWidth,Math.Max(240,height)/(double)source.PixelHeight));return ToBitmap(scale<1?new TransformedBitmap(source,new ScaleTransform(scale,scale)):source);}
  public static Bitmap ToBitmap(BitmapSource source) {
   var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int stride=converted.PixelWidth*4;var bytes=new byte[stride*converted.PixelHeight];converted.CopyPixels(bytes,stride,0);
   var bitmap=new Bitmap(converted.PixelWidth,converted.PixelHeight,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.WriteOnly,bitmap.PixelFormat);
   try{Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}finally{bitmap.UnlockBits(data);}return bitmap;
  }
 }
 internal sealed class LessonPanel:WF.FlowLayoutPanel {
  public float BodyPoints=24;
  Font bodyFont=new Font("Microsoft YaHei",24),boldFont=new Font("Microsoft YaHei",24,FontStyle.Bold);
  readonly Dictionary<string,Bitmap> formulaCache=new Dictionary<string,Bitmap>();
  double EmPixels=>BodyPoints*Math.Max(96,DeviceDpi)/72.0;
  public LessonPanel(){AutoScroll=true;FlowDirection=WF.FlowDirection.TopDown;WrapContents=false;BackColor=System.Drawing.Color.White;Padding=new WF.Padding(14);}
  protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(Controls.Count==0)return;int width=Math.Max(160,ClientSize.Width-48);SuspendLayout();foreach(WF.Control row in Controls){if(row is WF.Label)row.MaximumSize=new Size(width,0);else if(row is InlineMathRow)row.Width=width;else if(row is WF.Panel&&row.Tag is string tag&&tag=="formula")row.Width=width;}ResumeLayout();}
  void ClearRows(){while(Controls.Count>0){var c=Controls[0];Controls.RemoveAt(0);DisposeImages(c);c.Dispose();}}
  static void DisposeImages(WF.Control c){if(c is WF.PictureBox p)p.Image?.Dispose();foreach(WF.Control child in c.Controls)DisposeImages(child);}
  public bool NearEnd=>VerticalScroll.Value+ClientSize.Height>=DisplayRectangle.Height-80;
  public void Display(string text,bool formulas,bool toEnd=false,bool receiving=false) {
   int oldScroll=-AutoScrollPosition.Y;SuspendLayout();ClearRows();int width=Math.Max(160,ClientSize.Width-48);
   if(Math.Abs(bodyFont.Size-BodyPoints)>.1){ClearFormulaCache();bodyFont.Dispose();boldFont.Dispose();bodyFont=new Font("Microsoft YaHei",BodyPoints);boldFont=new Font("Microsoft YaHei",BodyPoints,FontStyle.Bold);}
   if(receiving){Controls.Add(new WF.Label{Text=text,AutoSize=true,MaximumSize=new Size(width,0),Font=bodyFont,ForeColor=System.Drawing.Color.FromArgb(25,35,45),UseCompatibleTextRendering=false});ResumeLayout();AutoScrollPosition=new Point(0,toEnd?VerticalScroll.Maximum:oldScroll);return;}
   var line=new List<MathFragment>();
   void Flush(){if(line.Count>0){AddLine(line,width,formulas);line.Clear();}}
   foreach(var fragment in formulas?MathDocument.Parse(text):new List<MathFragment>{new MathFragment(MathKind.Text,text)}){
    if(fragment.Kind==MathKind.Block){Flush();AddFormula(fragment.Content.Trim(),width);}
    else if(fragment.Kind==MathKind.Invalid){Flush();AddSourceWarning(fragment.Source,width,"公式定界符未闭合（原式保留）：");}
    else if(fragment.Kind==MathKind.Inline)line.Add(fragment);
    else{var parts=fragment.Content.Split('\n');for(int i=0;i<parts.Length;i++){if(i>0)Flush();if(parts[i].Length>0)line.Add(new MathFragment(MathKind.Text,parts[i].TrimEnd('\r')));}}
   }
   Flush();
   ResumeLayout();AutoScrollPosition=new Point(0,toEnd?VerticalScroll.Maximum:oldScroll);
  }
  void AddLine(List<MathFragment> line,int width,bool formulas){
   string source=string.Concat(line.ConvertAll(p=>p.Source)).Trim();if(source.Length==0)return;
   bool heading=source.StartsWith("#")||source.StartsWith("**");
   if(formulas&&line.Count==1&&line[0].Kind==MathKind.Text&&!Regex.IsMatch(source,@"[\u4e00-\u9fff]")&&Regex.IsMatch(source,@"\\(?:sqrt|frac|dfrac|vec|overrightarrow|lim|sum|int|quad|bigl|bigr|Bigl|Bigr)\b")){AddFormula(source,width);return;}
   if(formulas&&(line.Exists(p=>p.Kind==MathKind.Inline)||InlineMathRow.HasMathGlyph(source))&&AddInline(line,width,heading,source))return;
   foreach(var part in line){if(part.Kind==MathKind.Inline){AddFormula(part.Content,width);continue;}string plain=Regex.Replace(part.Content,@"^#{1,6}\s+","").Replace("**","").Trim();if(plain.Length==0)continue;Controls.Add(new WF.Label{Text=plain,AutoSize=true,MaximumSize=new Size(width,0),Font=heading?boldFont:bodyFont,ForeColor=heading?System.Drawing.Color.FromArgb(10,106,122):System.Drawing.Color.FromArgb(25,35,45),Margin=new WF.Padding(0,3,0,12),UseCompatibleTextRendering=false});}
  }
  void AddSourceWarning(string source,int width,string prefix){Controls.Add(new WF.Label{Text=prefix+source,AutoSize=true,MaximumSize=new Size(width,0),Font=bodyFont,ForeColor=System.Drawing.Color.DarkRed,Margin=new WF.Padding(0,3,0,12)});}
  void AddFormula(string expression,int width) {
   bool localBox=FormulaRaster.OuterBox(expression,out var boxed)&&!ScienceSupport.CloudMath.Prefer(boxed);
   if(ScienceSupport.CloudMath.Prefer(expression)&&!localBox&&!Regex.IsMatch(expression,@"\\begin\{(?:aligned|align\*?|gathered|gather\*?|split)\}")){AddCloudFormula(expression,width);return;}
   if(Regex.IsMatch(expression,@"\\begin\{(?:aligned|align\*?|gathered|gather\*?|split)\}")){string rows=Regex.Replace(expression,@"\\(?:begin|end)\{(?:aligned|align\*?|gathered|gather\*?|split)\}","");foreach(string row in Regex.Split(rows,@"\\\\(?:\[[^\]]*\])?"))if(!string.IsNullOrWhiteSpace(row))AddFormula(row.Replace("&",""),width);return;}
   if(ScienceSupport.ScientificNotation.TryChemistry(expression,out var chemistry)){Controls.Add(new WF.Label{Text=chemistry,AutoSize=true,MaximumSize=new Size(width,0),Font=bodyFont,ForeColor=chemistry.StartsWith("公式待核对")?System.Drawing.Color.DarkRed:System.Drawing.Color.Black});return;}
   try{var bitmap=FormulaBitmap(expression);var strip=new WF.Panel{Width=width,Height=bitmap.Height+16,AutoScroll=true,Tag="formula",Margin=new WF.Padding(0,2,0,8)};strip.Controls.Add(new WF.PictureBox{Image=bitmap,Size=bitmap.Size,SizeMode=WF.PictureBoxSizeMode.Normal,BackColor=System.Drawing.Color.White});Controls.Add(strip);
   }catch{AddCloudFormula(expression,width);}
  }
  void AddCloudFormula(string expression,int width){var box=new WF.Panel{Width=width,Height=80,AutoScroll=true,Tag="formula",Margin=new WF.Padding(0,2,0,8)};var label=new WF.Label{Text="复杂公式排版中…",Font=bodyFont,AutoSize=true};box.Controls.Add(label);Controls.Add(box);_ = FillCloudFormula(box,label,expression);}
  async Task FillCloudFormula(WF.Panel box,WF.Label label,string expression){double em=EmPixels;try{var bytes=await ScienceSupport.CloudMath.Render(expression,64);if(box.IsDisposed||IsDisposed)return;var bitmap=FormulaRaster.Cloud(bytes,em);box.Controls.Clear();label.Dispose();box.Height=bitmap.Height+20;box.Controls.Add(new WF.PictureBox{Image=bitmap,Size=bitmap.Size,SizeMode=WF.PictureBoxSizeMode.Normal,AccessibleDescription=expression});}catch{if(!label.IsDisposed){label.Text="公式需核对（原式保留）："+expression;label.ForeColor=System.Drawing.Color.DarkRed;label.MaximumSize=new Size(box.Width-10,0);box.Height=label.PreferredHeight+16;}}}
  Bitmap FormulaBitmap(string expression){if(expression.Length>800)throw new InvalidOperationException();string key=expression+"|"+EmPixels.ToString(System.Globalization.CultureInfo.InvariantCulture);Bitmap cached;if(!formulaCache.TryGetValue(key,out cached)){cached=FormulaRaster.Local(expression,EmPixels);if(formulaCache.Count>=64)ClearFormulaCache();formulaCache[key]=cached;}return new Bitmap(cached);}
   bool AddInline(List<MathFragment> line,int width,bool heading,string source){var row=new InlineMathRow(width,heading?boldFont:bodyFont,source,Math.Max(96,DeviceDpi));try{foreach(var part in line){if(part.Kind==MathKind.Inline){string expression=part.Content;if(ScienceSupport.CloudMath.Prefer(expression)){row.Dispose();return false;}var bitmap=FormulaBitmap(expression);if(bitmap.Width>width-20){bitmap.Dispose();row.Dispose();return false;}row.AddMath(bitmap);}else row.AddText(Regex.Replace(part.Content,@"^#{1,6}\s+","").Replace("**",""));}row.Reflow();Controls.Add(row);return true;}catch{row.Dispose();return false;}}
  void ClearFormulaCache(){foreach(var image in formulaCache.Values)image.Dispose();formulaCache.Clear();}
  protected override void Dispose(bool disposing){if(disposing){ClearRows();ClearFormulaCache();bodyFont.Dispose();boldFont.Dispose();}base.Dispose(disposing);}
 }
 internal sealed class PhotoCanvas:WF.Control {
  Bitmap? bitmap;float scale=1;PointF offset;Point down;bool moving;
  public PhotoCanvas(){DoubleBuffered=true;BackColor=System.Drawing.Color.FromArgb(239,244,246);SetStyle(WF.ControlStyles.Selectable,true);MouseDown+=(_,e)=>{Focus();down=e.Location;moving=true;Capture=true;};MouseUp+=(_,e)=>{moving=false;Capture=false;};MouseMove+=(_,e)=>{if(moving){offset.X+=e.X-down.X;offset.Y+=e.Y-down.Y;down=e.Location;Invalidate();}};MouseWheel+=(_,e)=>Zoom(e.Delta>0?1.15f:1/1.15f);}
  public void SetImage(Bitmap? value){var old=bitmap;bitmap=value;old?.Dispose();Invalidate();}
  public void Zoom(float factor){scale=Math.Max(.25f,Math.Min(6,scale*factor));Invalidate();}
  public void ResetView(){scale=1;offset=PointF.Empty;Invalidate();}
  protected override void OnPaint(WF.PaintEventArgs e){base.OnPaint(e);if(bitmap==null)return;float ratio=Math.Min(ClientSize.Width/(float)bitmap.Width,ClientSize.Height/(float)bitmap.Height)*scale;float w=bitmap.Width*ratio,h=bitmap.Height*ratio;e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;e.Graphics.DrawImage(bitmap,(ClientSize.Width-w)/2+offset.X,(ClientSize.Height-h)/2+offset.Y,w,h);}
  protected override void Dispose(bool disposing){if(disposing)bitmap?.Dispose();base.Dispose(disposing);}
  const int WM_GESTURE=0x119;
  [StructLayout(LayoutKind.Sequential)] struct Gesture {public uint size,flags,id;public IntPtr target;public short x,y;public uint instance,sequence;public ulong arguments;public uint extra;}
  [DllImport("user32.dll")] static extern bool GetGestureInfo(IntPtr handle,ref Gesture info);
  [DllImport("user32.dll")] static extern bool CloseGestureInfoHandle(IntPtr handle);
  ulong lastZoom;short panX,panY;
  protected override void WndProc(ref WF.Message m){if(m.Msg==WM_GESTURE){var g=new Gesture{size=(uint)Marshal.SizeOf(typeof(Gesture))};if(GetGestureInfo(m.LParam,ref g)){if(g.id==3){if((g.flags&1)!=0)lastZoom=g.arguments;else if(lastZoom>0){Zoom((float)g.arguments/lastZoom);lastZoom=g.arguments;}}else if(g.id==4){if((g.flags&1)==0){offset.X+=g.x-panX;offset.Y+=g.y-panY;Invalidate();}panX=g.x;panY=g.y;}CloseGestureInfoHandle(m.LParam);m.Result=IntPtr.Zero;return;}}base.WndProc(ref m);}
 }
 internal static class NativePick {
  public static Task<Rectangle?> Select() {
   var result=new TaskCompletionSource<Rectangle?>();var windows=new List<Picker>();bool finished=false;
   Action<Rectangle?> finish=r=>{if(finished)return;finished=true;foreach(var w in windows)w.Close();result.TrySetResult(r);};
   foreach(var screen in WF.Screen.AllScreens)windows.Add(new Picker(screen.Bounds,finish));foreach(var w in windows)w.Show();return result.Task;
  }
  public static BitmapSource Capture(Rectangle region) {
   if(region.Width<48||region.Height<48||(long)region.Width*region.Height>16000000)throw new InvalidOperationException("框选区域太小或太大，请只选完整题目。");
   using(var bitmap=new Bitmap(region.Width,region.Height,System.Drawing.Imaging.PixelFormat.Format24bppRgb)) {
    using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(region.Location,Point.Empty,region.Size,CopyPixelOperation.SourceCopy|CopyPixelOperation.CaptureBlt);
    using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);stream.Position=0;var result=new BitmapImage();result.BeginInit();result.CacheOption=BitmapCacheOption.OnLoad;result.StreamSource=stream;result.EndInit();result.Freeze();return result;}
   }
  }
  sealed class Picker:WF.Form {
   readonly Action<Rectangle?> finish;Point start,end;bool selecting;
   public Picker(Rectangle bounds,Action<Rectangle?> finish){this.finish=finish;AutoScaleMode=WF.AutoScaleMode.None;FormBorderStyle=WF.FormBorderStyle.None;Bounds=bounds;StartPosition=WF.FormStartPosition.Manual;TopMost=true;ShowInTaskbar=false;Opacity=.38;BackColor=System.Drawing.Color.Black;DoubleBuffered=true;Cursor=WF.Cursors.Cross;KeyPreview=true;
    KeyDown+=(_,e)=>{if(e.KeyCode==WF.Keys.Escape)finish(null);};
    MouseDown+=(_,e)=>{start=end=e.Location;selecting=true;Capture=true;Invalidate();};MouseMove+=(_,e)=>{if(selecting){end=e.Location;Invalidate();}};
    MouseUp+=(_,e)=>{if(!selecting)return;selecting=false;Capture=false;var first=PointToScreen(start);var last=PointToScreen(e.Location);finish(Rectangle.FromLTRB(Math.Min(first.X,last.X),Math.Min(first.Y,last.Y),Math.Max(first.X,last.X),Math.Max(first.Y,last.Y)));};
   }
   protected override void OnPaint(WF.PaintEventArgs e){base.OnPaint(e);using(var font=new Font("Microsoft YaHei",22))e.Graphics.DrawString("拖动框选完整题目和作答 · 松手自动讲解 · Esc取消",font,System.Drawing.Brushes.White,20,20);if(selecting)using(var pen=new System.Drawing.Pen(System.Drawing.Color.Gold,4))e.Graphics.DrawRectangle(pen,Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),Math.Abs(start.X-end.X),Math.Abs(start.Y-end.Y));}
  }
 }
}
