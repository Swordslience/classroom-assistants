using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PhotoTutor {
 internal struct PixelArea {
  public int Left,Top,Right,Bottom;
  public int Width=>Right-Left;public int Height=>Bottom-Top;
 }
 internal static class ScreenPick {
  delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref PixelArea bounds,IntPtr data);
  [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
  [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint point);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
  [StructLayout(LayoutKind.Sequential)] struct CursorPoint {public int X,Y;}
  [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
  [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
  [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
  [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dest,int x,int y,int w,int h,IntPtr source,int sx,int sy,int operation);
  [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapHeader header,uint usage,out IntPtr pixels,IntPtr section,uint offset);
  [StructLayout(LayoutKind.Sequential)] struct BitmapHeader {
   public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,SizeImage;public int XPels,YPels;public uint ColorsUsed,ColorsImportant;
  }
  public static Task<PixelArea?> SelectAsync() {
   var completion=new TaskCompletionSource<PixelArea?>();var windows=new List<PickWindow>();bool finished=false;
   Action<PixelArea?> finish=area=>{if(finished)return;finished=true;foreach(var w in windows)w.Close();completion.TrySetResult(area);};
   EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr m,IntPtr dc,ref PixelArea b,IntPtr state)=>{windows.Add(new PickWindow(b,finish));return true;},IntPtr.Zero);
   if(windows.Count==0){completion.SetResult(null);return completion.Task;}
   foreach(var w in windows)w.Show();return completion.Task;
  }
  public static BitmapSource Capture(PixelArea area) {
   if(area.Width<48||area.Height<48||(long)area.Width*area.Height>16000000)throw new InvalidOperationException("选区太小或太大，请框选当前屏幕中的完整题目。");
   IntPtr screen=GetDC(IntPtr.Zero),memory=IntPtr.Zero,bitmap=IntPtr.Zero,old=IntPtr.Zero;
   if(screen==IntPtr.Zero)throw new InvalidOperationException("当前桌面不可取图，请用原生展台拍照保存后载入。");
   try {
    memory=CreateCompatibleDC(screen);var header=new BitmapHeader{Size=40,Width=area.Width,Height=-area.Height,Planes=1,BitCount=32};IntPtr pixels;
    bitmap=CreateDIBSection(screen,ref header,0,out pixels,IntPtr.Zero,0);
    if(memory==IntPtr.Zero||bitmap==IntPtr.Zero||pixels==IntPtr.Zero)throw new InvalidOperationException("取图内存不足，请缩小选区。");
    old=SelectObject(memory,bitmap);
    if(!BitBlt(memory,0,0,area.Width,area.Height,screen,area.Left,area.Top,0x40CC0020))throw new InvalidOperationException("未能读取展台画面，请在原生软件拍照保存后载入。");
    int stride=checked(area.Width*4);var bytes=new byte[checked(stride*area.Height)];Marshal.Copy(pixels,bytes,0,bytes.Length);
    var result=BitmapSource.Create(area.Width,area.Height,96,96,PixelFormats.Bgr32,null,bytes,stride);result.Freeze();return result;
   }finally{if(old!=IntPtr.Zero)SelectObject(memory,old);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(memory!=IntPtr.Zero)DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
  }
  sealed class PickWindow:Window {
   readonly PixelArea monitor;readonly Action<PixelArea?> finish;readonly Canvas canvas=new Canvas();
   readonly Rectangle box=new Rectangle{Stroke=Brushes.Gold,StrokeThickness=4,Fill=new SolidColorBrush(Color.FromArgb(10,255,255,255)),IsHitTestVisible=false};
   CursorPoint start;bool dragging;
   public PickWindow(PixelArea bounds,Action<PixelArea?> finish) {
    monitor=bounds;this.finish=finish;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;ShowInTaskbar=false;Topmost=true;Background=new SolidColorBrush(Color.FromArgb(55,0,0,0));Content=canvas;Cursor=Cursors.Cross;
    canvas.Background=Brushes.Transparent;canvas.Children.Add(box);
    var hint=new Border{Background=new SolidColorBrush(Color.FromRgb(35,45,55)),Padding=new Thickness(16),CornerRadius=new CornerRadius(8),IsHitTestVisible=false,Child=new TextBlock{Text="拖动框选题目和作答 · 松手后自动讲解 · Esc取消",FontSize=28,Foreground=Brushes.White}};
    Canvas.SetLeft(hint,20);Canvas.SetTop(hint,20);canvas.Children.Add(hint);
    Loaded+=(_,__)=>{SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),bounds.Left,bounds.Top,bounds.Width,bounds.Height,0x0040);Activate();};
    KeyDown+=(_,e)=>{if(e.Key==Key.Escape)finish(null);};
    MouseLeftButtonDown+=(_,e)=>{GetCursorPos(out start);dragging=true;CaptureMouse();Draw(start);e.Handled=true;};
    MouseMove+=(_,e)=>{if(dragging){CursorPoint p;GetCursorPos(out p);Draw(p);}};
    MouseLeftButtonUp+=(_,e)=>{if(!dragging)return;dragging=false;ReleaseMouseCapture();CursorPoint end;GetCursorPos(out end);finish(Area(end));e.Handled=true;};
    TouchDown+=(_,e)=>{GetCursorPos(out start);dragging=true;CaptureTouch(e.TouchDevice);e.Handled=true;};
    TouchMove+=(_,e)=>{if(!dragging)return;var p=PointToScreen(e.GetTouchPoint(this).Position);Draw(new CursorPoint{X=(int)p.X,Y=(int)p.Y});e.Handled=true;};
    TouchUp+=(_,e)=>{if(!dragging)return;dragging=false;var p=PointToScreen(e.GetTouchPoint(this).Position);ReleaseTouchCapture(e.TouchDevice);finish(Area(new CursorPoint{X=(int)p.X,Y=(int)p.Y}));e.Handled=true;};
   }
   PixelArea Area(CursorPoint end) {
    int x=Math.Max(monitor.Left,Math.Min(monitor.Right,end.X)),y=Math.Max(monitor.Top,Math.Min(monitor.Bottom,end.Y));
    return new PixelArea{Left=Math.Min(start.X,x),Top=Math.Min(start.Y,y),Right=Math.Max(start.X,x),Bottom=Math.Max(start.Y,y)};
   }
   void Draw(CursorPoint p){var a=Area(p);var first=PointFromScreen(new Point(a.Left,a.Top));var last=PointFromScreen(new Point(a.Right,a.Bottom));Canvas.SetLeft(box,first.X);Canvas.SetTop(box,first.Y);box.Width=Math.Max(0,last.X-first.X);box.Height=Math.Max(0,last.Y-first.Y);}
  }
 }
}
