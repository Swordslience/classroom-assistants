using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace ClassroomAI.Seewo;
internal static class LauncherPlacement {
 internal static Point Calculate(Rect work,Rect host,Size size,double dpi){var bounds=Rect.Intersect(work,host);if(bounds.IsEmpty||bounds.Width<size.Width||bounds.Height<size.Height)bounds=work;double right=18*dpi,bottom=92*dpi;return new Point(Math.Max(bounds.Left,bounds.Right-size.Width-right),Math.Max(bounds.Top,bounds.Bottom-size.Height-bottom));}
 [StructLayout(LayoutKind.Sequential)]struct R{public int L,T,Rt,B;}
 [StructLayout(LayoutKind.Sequential)]struct MI{public int Size;public R Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
 [DllImport("user32.dll")]static extern bool GetMonitorInfo(IntPtr monitor,ref MI info);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out R rect);
 [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr hwnd);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
 internal static void EnsureVisible(Window launcher,Window owner,bool force=false){
  var target=new WindowInteropHelper(launcher).Handle;var host=new WindowInteropHelper(owner).Handle;if(target==IntPtr.Zero||host==IntPtr.Zero)return;
  var info=new MI{Size=Marshal.SizeOf<MI>()};if(!GetMonitorInfo(MonitorFromWindow(host,2),ref info)||!GetWindowRect(target,out var rect)||!GetWindowRect(host,out var hostRect))return;
  var work=info.Work;bool outside=rect.L<work.L||rect.T<work.T||rect.Rt>work.Rt||rect.B>work.B;if(!force&&!outside)return;
  double dpi=Math.Max(1,GetDpiForWindow(host)/96.0);int width=rect.Rt-rect.L,height=rect.B-rect.T;var point=Calculate(new Rect(work.L,work.T,work.Rt-work.L,work.B-work.T),new Rect(hostRect.L,hostRect.T,hostRect.Rt-hostRect.L,hostRect.B-hostRect.T),new Size(width,height),dpi);
  int x=(int)Math.Round(point.X),y=(int)Math.Round(point.Y);
  SetWindowPos(target,IntPtr.Zero,x,y,0,0,0x0001|0x0004|0x0010); // size/z-order/focus unchanged
 }
}
