using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DirectShowLib;

namespace PhotoTutor {
 internal sealed class CameraDevice {
  public string Name {get;set;}="";public string Id {get;set;}="";
  public override string ToString()=>Name;
 }
 // A small native DirectShow graph, no web browser, OpenCV, recorder or local model.
 internal sealed class CameraSession:IDisposable {
  static int active;
  internal static bool IsActive=>Volatile.Read(ref active)!=0;
  readonly ManualResetEvent stop=new ManualResetEvent(false);
  readonly object callbacks=new object();readonly TaskCompletionSource<bool> released=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);int disposing;
  internal Task Released=>released.Task;
  readonly Thread thread;
  readonly string deviceId;readonly bool high;
  readonly BitmapSource? demonstration;
  readonly CaptureFrameBuffer frames=new CaptureFrameBuffer();int focusRequested;
  readonly System.Diagnostics.Stopwatch sessionClock=System.Diagnostics.Stopwatch.StartNew();
  internal static Func<BitmapSource>? DemoFrameForTest;
  internal bool HasFrame=>frames.HasFrame;
  internal double FrameAgeMilliseconds=>frames.AgeMilliseconds;
  internal bool PreviewStalled=>HasFrame?FrameAgeMilliseconds>3500:sessionClock.ElapsedMilliseconds>8000;
  internal Task<BitmapSource> CaptureFocused(CancellationToken token){Interlocked.Exchange(ref focusRequested,1);return frames.Capture(token);}
  public event Action<BitmapSource>? Frame;
  public event Action<string>? Failed;
  public bool IsRunning=>thread.IsAlive;
  public static CameraDevice[] Enumerate() {
   if(EnumerateForTest!=null)return EnumerateForTest();
   var devices=DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
   try{return devices.Select(d=>new CameraDevice{Name=d.Name,Id=d.DevicePath}).ToArray();}
   finally{foreach(var d in devices)d.Dispose();}
  }
  internal static Func<CameraDevice[]>? EnumerateForTest;
  public CameraSession(string deviceId,bool high,BitmapSource? demonstration=null) {
   if(Interlocked.CompareExchange(ref active,1,0)!=0)throw new InvalidOperationException("上一次相机正在释放，请稍后再打开。");
   this.deviceId=deviceId;this.high=high;this.demonstration=demonstration;
   thread=new Thread(Pump){IsBackground=true,Name="PhotoTutor camera"};thread.SetApartmentState(ApartmentState.MTA);try{thread.Start();}catch{Interlocked.Exchange(ref active,0);stop.Dispose();released.TrySetResult(true);throw;}
  }
  void Pump() {
   if(demonstration!=null){try{while(!stop.WaitOne(125))Emit(DemoFrameForTest?.Invoke()??demonstration);}finally{CompleteRelease();}return;}
   IGraphBuilder? graph=null;ICaptureGraphBuilder2? builder=null;IBaseFilter? source=null,grabFilter=null,nullFilter=null;
   IMediaControl? control=null;IntPtr buffer=IntPtr.Zero;
   try {
    graph=(IGraphBuilder)new FilterGraph();builder=(ICaptureGraphBuilder2)new CaptureGraphBuilder2();DsError.ThrowExceptionForHR(builder.SetFiltergraph(graph));
    var devices=DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
    try {
     var selected=devices.FirstOrDefault(d=>d.DevicePath==deviceId);if(selected==null)throw new InvalidOperationException("设备已断开；重新连接后会自动检测并恢复。");
     object raw;Guid iid=typeof(IBaseFilter).GUID;selected.Mon.BindToObject(null,null,ref iid,out raw);source=(IBaseFilter)raw;
    }finally{foreach(var d in devices)d.Dispose();}
    DsError.ThrowExceptionForHR(graph.AddFilter(source,"Camera"));SetResolution(builder,source,high);
    var grab=(ISampleGrabber)new SampleGrabber();grabFilter=(IBaseFilter)grab;DsError.ThrowExceptionForHR(graph.AddFilter(grabFilter,"SingleFrame"));
    var requested=new AMMediaType{majorType=MediaType.Video,subType=MediaSubType.RGB24,formatType=FormatType.VideoInfo};
    try{DsError.ThrowExceptionForHR(grab.SetMediaType(requested));}finally{DsUtils.FreeAMMediaType(requested);}
    DsError.ThrowExceptionForHR(grab.SetBufferSamples(true));DsError.ThrowExceptionForHR(grab.SetOneShot(false));var pulse=new CameraSamplePulse();DsError.ThrowExceptionForHR(grab.SetCallback(pulse,1));long lastSample=0;
    nullFilter=(IBaseFilter)new NullRenderer();DsError.ThrowExceptionForHR(graph.AddFilter(nullFilter,"Discard"));
    DsError.ThrowExceptionForHR(builder.RenderStream(PinCategory.Capture,MediaType.Video,source,grabFilter,nullFilter));
    var connected=new AMMediaType();int width,height;
    try {DsError.ThrowExceptionForHR(grab.GetConnectedMediaType(connected));var h=(VideoInfoHeader)Marshal.PtrToStructure(connected.formatPtr,typeof(VideoInfoHeader));width=h.BmiHeader.Width;height=h.BmiHeader.Height;}
    finally{DsUtils.FreeAMMediaType(connected);}
    int absHeight=Math.Abs(height),stride=checked((width*3+3)&~3),length=checked(stride*absHeight);
    if(!CameraMode.Safe(width,absHeight)||length>72000000)throw new InvalidOperationException("设备画面超出安全内存范围，请选择其他设备格式。");
    buffer=Marshal.AllocCoTaskMem(length);var pixels=new byte[length];control=(IMediaControl)graph;DsError.ThrowExceptionForHR(control.Run());
    bool autoFocus=CameraFocus.Enable(source as IAMCameraControl);SessionDiagnostics.Record("camera-capture-ready",new{width,height=absHeight,autoFocus,nativePixels=true});
    DateTime started=DateTime.UtcNow;
    while(!stop.WaitOne(125)) {
     if(Interlocked.Exchange(ref focusRequested,0)!=0)CameraFocus.Enable(source as IAMCameraControl);
     long sample=pulse.Sequence;if(sample==lastSample)continue;
     int needed=0;int check=grab.GetCurrentBuffer(ref needed,IntPtr.Zero);
     if(check<0){if((DateTime.UtcNow-started).TotalSeconds>8)DsError.ThrowExceptionForHR(check);continue;}
     if(needed!=length)throw new InvalidOperationException("设备画面大小发生变化，正在自动重新连接。");
     int size=length;int result=grab.GetCurrentBuffer(ref size,buffer);
     if(result<0){if((DateTime.UtcNow-started).TotalSeconds>8)DsError.ThrowExceptionForHR(result);continue;}
     if(size<length||size>length)throw new InvalidOperationException("设备画面格式发生变化，正在自动重新连接。");
     Marshal.Copy(buffer,pixels,0,length);
     lastSample=sample;
     BitmapSource bitmap=BitmapSource.Create(width,absHeight,96,96,PixelFormats.Bgr24,null,pixels,stride);
     if(height>0)bitmap=new TransformedBitmap(bitmap,new ScaleTransform(1,-1));bitmap.Freeze();Emit(bitmap);
    }
    GC.KeepAlive(pulse);
   }catch(Exception e){lock(callbacks)if(disposing==0&&!stop.WaitOne(0))Failed?.Invoke(e is COMException?"高拍仪未能打开，请关闭占用相机的其他程序；也可载入原始照片。代码：0x"+e.HResult.ToString("X8"):e.Message);}
   finally {
    try{control?.Stop();}catch{ }
    if(buffer!=IntPtr.Zero)Marshal.FreeCoTaskMem(buffer);
    Release(nullFilter);Release(grabFilter);Release(source);Release(builder);Release(graph);
    CompleteRelease();
   }
  }
  static void SetResolution(ICaptureGraphBuilder2 builder,IBaseFilter source,bool high) {
   // Both settings capture native pixels. Never choose by a fixed 1920 target or 4:3 ratio.
   object raw;int hr=builder.FindInterface(PinCategory.Capture,MediaType.Video,source,typeof(IAMStreamConfig).GUID,out raw);
   if(hr<0||raw==null)return;
   var cfg=(IAMStreamConfig)raw;IntPtr caps=IntPtr.Zero;var modes=new List<Tuple<AMMediaType,CameraMode>>();AMMediaType? original=null;
   try {
    cfg.GetFormat(out original);int count,size;if(cfg.GetNumberOfCapabilities(out count,out size)<0||size<1||size>4096||count>4096)return;caps=Marshal.AllocCoTaskMem(size);
    for(int i=0;i<count;i++) {
     AMMediaType mt;if(cfg.GetStreamCaps(i,out mt,caps)<0)continue;
     bool keep=false;
     try {
      if(mt.formatPtr==IntPtr.Zero)continue;int w,h;long interval;
      if(mt.formatType==FormatType.VideoInfo){var v=(VideoInfoHeader)Marshal.PtrToStructure(mt.formatPtr,typeof(VideoInfoHeader));w=Math.Abs(v.BmiHeader.Width);h=Math.Abs(v.BmiHeader.Height);interval=v.AvgTimePerFrame;}
      else if(mt.formatType==FormatType.VideoInfo2){var v=(VideoInfoHeader2)Marshal.PtrToStructure(mt.formatPtr,typeof(VideoInfoHeader2));w=Math.Abs(v.BmiHeader.Width);h=Math.Abs(v.BmiHeader.Height);interval=v.AvgTimePerFrame;}
      else continue;
      if(!CameraMode.Safe(w,h))continue;
      modes.Add(Tuple.Create(mt,new CameraMode{Width=w,Height=h,Interval=interval,Index=i}));keep=true;
     }finally{if(!keep)DsUtils.FreeAMMediaType(mt);}
    }
    foreach(var mode in modes.OrderByDescending(v=>v.Item2.Pixels).ThenBy(v=>v.Item2.Interval<=0?long.MaxValue:v.Item2.Interval))if(cfg.SetFormat(mode.Item1)>=0)return;
    if(original!=null)cfg.SetFormat(original);
   }catch(COMException error){SessionDiagnostics.Record("camera-native-format-fallback",null,error);if(original!=null)try{cfg.SetFormat(original);}catch{}}
   finally{foreach(var mode in modes)DsUtils.FreeAMMediaType(mode.Item1);if(original!=null)DsUtils.FreeAMMediaType(original);if(caps!=IntPtr.Zero)Marshal.FreeCoTaskMem(caps);Release(raw);}
  }
  static void Release(object? value){if(value!=null&&Marshal.IsComObject(value))try{Marshal.ReleaseComObject(value);}catch{ }}
  void Emit(BitmapSource bitmap){frames.Publish(bitmap);lock(callbacks)if(disposing==0)Frame?.Invoke(bitmap);}
  void CompleteRelease(){frames.Dispose();Interlocked.Exchange(ref active,0);lock(callbacks){disposing=1;stop.Dispose();}released.TrySetResult(true);}
  public void Dispose(){lock(callbacks){if(disposing!=0)return;disposing=1;frames.Dispose();try{stop.Set();}catch(ObjectDisposedException){ }}}
 }
}
