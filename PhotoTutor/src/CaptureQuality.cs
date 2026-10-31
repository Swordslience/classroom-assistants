using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DirectShowLib;

namespace PhotoTutor {
 [System.Runtime.InteropServices.ComVisible(true),System.Runtime.InteropServices.ClassInterface(System.Runtime.InteropServices.ClassInterfaceType.None)]
 internal sealed class CameraSamplePulse:ISampleGrabberCB {
  long sequence;internal long Sequence=>Interlocked.Read(ref sequence);
  public int SampleCB(double time,IMediaSample sample){Interlocked.Increment(ref sequence);return 0;}
  public int BufferCB(double time,IntPtr buffer,int length){Interlocked.Increment(ref sequence);return 0;}
 }
 internal static class CameraFocus {
  // Query capabilities before setting Auto. Fixed-focus and vendor-only drivers remain usable.
  internal static bool Enable(IAMCameraControl? control) {
   if(control==null)return false;
   try {
    int min,max,step,value;CameraControlFlags caps,flags;
    if(control.GetRange(CameraControlProperty.Focus,out min,out max,out step,out value,out caps)<0||(caps&CameraControlFlags.Auto)==0)return false;
    if(control.Get(CameraControlProperty.Focus,out int current,out flags)>=0)value=current;
    value=Math.Max(min,Math.Min(max,value));
    return control.Set(CameraControlProperty.Focus,value,CameraControlFlags.Auto)>=0;
   }catch(Exception error){SessionDiagnostics.Record("camera-autofocus-unavailable",null,error);return false;}
  }
 }
 internal sealed class CameraMode {
  internal int Width,Height,Index;internal long Interval;
  internal long Pixels=>(long)Width*Height;
  internal static bool Safe(int w,int h)=>w>=32&&h>=32&&(long)w*h<=24000000&&(((long)w*3+3)&~3L)*h<=72000000;
  internal static CameraMode? Choose(System.Collections.Generic.IEnumerable<CameraMode> modes)=>modes.Where(m=>Safe(m.Width,m.Height)).OrderByDescending(m=>m.Pixels).ThenBy(m=>m.Interval<=0?long.MaxValue:m.Interval).FirstOrDefault();
 }
 internal sealed class CaptureFrameBuffer:IDisposable {
  readonly object gate=new object();readonly Stopwatch clock=Stopwatch.StartNew();
  BitmapSource? latest;byte[] scene=Array.Empty<byte>();double sharpness;long sequence,at;bool disposed,capturing;
  internal double AgeMilliseconds {get{lock(gate)return latest==null?double.PositiveInfinity:clock.ElapsedMilliseconds-at;}}
  internal bool HasFrame {get{lock(gate)return latest!=null;}}
  internal void Publish(BitmapSource source) {
   // This small analysis copy is never used for the photo or the saved original.
   double scale=Math.Min(1,640d/Math.Max(source.PixelWidth,source.PixelHeight));
   BitmapSource small=scale<1?new TransformedBitmap(source,new ScaleTransform(scale,scale)):source;
   var gray=new FormatConvertedBitmap(small,PixelFormats.Gray8,null,0);int w=gray.PixelWidth,h=gray.PixelHeight;
   var pixels=new byte[w*h];gray.CopyPixels(pixels,w,0);double edge=0;int samples=0;var summary=new byte[192];
   for(int y=1;y<h-1;y+=2)for(int x=1;x<w-1;x+=2){int i=y*w+x;edge+=Math.Abs(pixels[i-1]-pixels[i+1])+Math.Abs(pixels[i-w]-pixels[i+w]);samples++;}
   for(int y=0;y<12;y++)for(int x=0;x<16;x++)summary[y*16+x]=pixels[Math.Min(h-1,(2*y+1)*h/24)*w+Math.Min(w-1,(2*x+1)*w/32)];
   lock(gate){if(disposed)return;latest=source;scene=summary;sharpness=samples==0?0:edge/samples;at=clock.ElapsedMilliseconds;sequence++;}
  }
  internal async Task<BitmapSource> Capture(CancellationToken token,int settleMs=700,int maximumMs=2200) {
   long seen;lock(gate){if(disposed)throw new OperationCanceledException("相机已关闭。");if(capturing)throw new InvalidOperationException("正在等待对焦，请勿重复拍照。");capturing=true;seen=sequence;}
   var timer=Stopwatch.StartNew();BitmapSource? best=null,last=null;byte[]? previous=null;double bestScore=-1;int fresh=0,stable=0;
   try {
    while(timer.ElapsedMilliseconds<maximumMs){token.ThrowIfCancellationRequested();await Task.Delay(60,token).ConfigureAwait(false);
     BitmapSource? frame;byte[] current;double score;long number;double age;
     lock(gate){if(disposed)throw new OperationCanceledException("相机已关闭。");frame=latest;current=scene;score=sharpness;number=sequence;age=clock.ElapsedMilliseconds-at;}
     if(frame==null||number==seen||age>700)continue;seen=number;fresh++;last=frame;
     double difference=previous==null?0:current.Zip(previous,(a,b)=>Math.Abs(a-b)).Average();previous=current;
     if(difference>9){best=null;bestScore=-1;stable=0;}else stable++;
     if(timer.ElapsedMilliseconds>=settleMs&&score>=bestScore){best=frame;bestScore=score;}
     if(timer.ElapsedMilliseconds>=settleMs+350&&fresh>=4&&stable>=3&&best!=null)return best;
    }
    // A stalled stream must never silently photograph a cached, old frame.
    if(last==null||AgeMilliseconds>700)throw new InvalidOperationException("预览没有收到新的画面，本次未拍照；相机会自动重连，请确认预览刷新后重拍。");
    return best??last;
   }finally{lock(gate)capturing=false;}
  }
  public void Dispose(){lock(gate){disposed=true;latest=null;scene=Array.Empty<byte>();}}
 }
}
