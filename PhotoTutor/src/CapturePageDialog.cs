using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WF=System.Windows.Forms;
using BitmapSource=System.Windows.Media.Imaging.BitmapSource;
namespace PhotoTutor {
 internal sealed class CapturePageDialog:WF.Form {
  readonly WF.ComboBox devices=new WF.ComboBox{Width=360,DropDownStyle=WF.ComboBoxStyle.DropDownList};readonly PhotoCanvas preview=new PhotoCanvas{Dock=WF.DockStyle.Fill};
  readonly WF.Label status=new WF.Label{Dock=WF.DockStyle.Bottom,AutoSize=true,Padding=new WF.Padding(8)};readonly WF.Timer poll=new WF.Timer{Interval=2000};readonly WF.Button take;
  readonly bool high;readonly CancellationTokenSource lifetime=new CancellationTokenSource();CameraSession? camera;BitmapSource? live;Task release=Task.CompletedTask;bool changing,refreshing,opening,closed,taking;string activeId="";int queued,generation;DateTime retryAt;
  internal PhotoData? Result;internal string SelectedDeviceId=> (devices.SelectedItem as CameraDevice)?.Id??"";
  CapturePageDialog(string preferred,bool high){this.high=high;Text="继续拍下一页 · 已选区域保留";Font=new Font("Microsoft YaHei",16);var area=WF.Screen.PrimaryScreen.WorkingArea;Size=new Size(Math.Min(1400,area.Width),Math.Min(900,area.Height));StartPosition=WF.FormStartPosition.CenterParent;if(Entry.OfflineUiTesting){ShowInTaskbar=false;StartPosition=WF.FormStartPosition.Manual;Location=new Point(-12000,-12000);}
   var bar=new WF.FlowLayoutPanel{Dock=WF.DockStyle.Top,AutoSize=true,WrapContents=true};bar.Controls.Add(devices);take=new WF.Button{Text="拍下本页并返回选题",AutoSize=true,MinimumSize=new Size(220,50),Enabled=false};take.Click+=async(_,__)=>await Take();bar.Controls.Add(take);var cancel=new WF.Button{Text="取消补拍",AutoSize=true,MinimumSize=new Size(100,50)};cancel.Click+=(_,__)=>Close();bar.Controls.Add(cancel);Controls.Add(preview);Controls.Add(status);Controls.Add(bar);
   devices.SelectedIndexChanged+=(_,__)=>{if(!changing){StopCamera();_=Open();}};Resize+=(_,__)=>{if(WindowState==WF.FormWindowState.Minimized)StopCamera();};poll.Tick+=async(_,__)=>await Refresh(preferred);Shown+=async(_,__)=>{poll.Start();await Refresh(preferred);};FormClosed+=(_,__)=>{closed=true;lifetime.Cancel();poll.Stop();poll.Dispose();StopCamera();};
  }
  async Task Take(){if(taking||camera==null)return;var session=camera;taking=true;take.Enabled=false;status.Text="正在自动对焦并等待画面稳定，请保持本页不动…";try{var frame=await session.CaptureFocused(lifetime.Token);if(closed||camera!=session)return;Result=PhotoData.SelectionSource(frame,"补拍原始页面");DialogResult=WF.DialogResult.OK;Close();}catch(OperationCanceledException){}catch(Exception error){if(!closed)status.Text=error.Message;}finally{taking=false;if(!closed)take.Enabled=live!=null;}}
  protected override bool ShowWithoutActivation=>Entry.OfflineUiTesting;
  async Task Refresh(string preferred){if(closed||refreshing)return;refreshing=true;try{var found=await Task.Run(CameraSession.Enumerate);if(Entry.DemoCamera)found=found.Concat(new[]{new CameraDevice{Name="演示高拍仪",Id="TEST_DEMO_INPUT"}}).ToArray();if(closed)return;string selected=SelectedDeviceId;
    if(camera!=null&&(!found.Any(d=>d.Id==activeId)||camera.PreviewStalled))StopCamera();
    if(!devices.Items.Cast<CameraDevice>().Select(d=>d.Id+"\n"+d.Name).SequenceEqual(found.Select(d=>d.Id+"\n"+d.Name))){changing=true;try{devices.Items.Clear();devices.Items.AddRange(found);devices.SelectedItem=found.FirstOrDefault(d=>d.Id==selected)??found.FirstOrDefault(d=>d.Id==preferred)??found.FirstOrDefault();}finally{changing=false;}}
    if(found.Length==0)status.Text="等待相机连接，会自动刷新；取消后可用“载入图片”补页。";else if(camera==null&&DateTime.UtcNow>=retryAt)await Open();
   }catch(Exception error){if(!closed)status.Text="相机暂未恢复："+error.Message;retryAt=DateTime.UtcNow.AddSeconds(3);}finally{refreshing=false;}}
  async Task Open(){if(opening||closed||WindowState==WF.FormWindowState.Minimized||camera!=null||devices.SelectedItem is not CameraDevice device)return;opening=true;int stamp=generation;try{
    if(!release.IsCompleted&&await Task.WhenAny(release,Task.Delay(5000))!=release){status.Text="相机正在释放，稍后会自动重试。";return;}
    if(closed||stamp!=generation||SelectedDeviceId!=device.Id)return;var session=new CameraSession(device.Id,high,Entry.DemoCamera&&device.Id=="TEST_DEMO_INPUT"?Checks.Chart():null);camera=session;activeId=device.Id;
    session.Frame+=frame=>{if(closed||camera!=session||Interlocked.Exchange(ref queued,1)!=0)return;Post(()=>{try{if(camera!=session||closed)return;live=frame;preview.SetImage(Raster.ToPreviewBitmap(frame,preview.Width,preview.Height));take.Enabled=!taking;if(!taking)status.Text="实时预览 · "+frame.PixelWidth+"×"+frame.PixelHeight+"；摆好下一页后拍照，原选区保留。";}finally{Interlocked.Exchange(ref queued,0);}});};
    session.Failed+=message=>Post(()=>{if(camera!=session)return;StopCamera();retryAt=DateTime.UtcNow.AddSeconds(3);status.Text=message+"；设备恢复后自动重试。";});status.Text="正在打开相机…";
   }catch(Exception error){if(!closed)status.Text=error.Message;retryAt=DateTime.UtcNow.AddSeconds(3);}finally{opening=false;}}
  void Post(Action action){if(closed||IsDisposed||!IsHandleCreated)return;try{BeginInvoke(new Action(()=>{if(!closed&&!IsDisposed)action();}));}catch(InvalidOperationException){}}
  void StopCamera(){generation++;Interlocked.Exchange(ref queued,0);var old=camera;camera=null;live=null;take.Enabled=false;if(old!=null){old.Dispose();release=old.Released;}}
  internal static async Task<PhotoData?> Capture(WF.IWin32Window owner,string preferred,bool high,CancellationToken token){using(var dialog=new CapturePageDialog(preferred,high)){using(token.Register(()=>dialog.Post(dialog.Close))){var response=dialog.ShowDialog(owner);await Task.WhenAny(dialog.release,Task.Delay(5000));token.ThrowIfCancellationRequested();return response==WF.DialogResult.OK?dialog.Result:null;}}}
 }
}
