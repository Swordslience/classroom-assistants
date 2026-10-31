using System;
using System.Linq;
using System.Threading.Tasks;
using WF=System.Windows.Forms;
namespace PhotoTutor {
 internal sealed partial class TutorForm {
  readonly WF.Timer devicePoll=new WF.Timer{Interval=2000};
  bool deviceRefreshRunning,cameraWanted=true,formReady,regionDialogOpen;
  string activeCameraId="";DateTime retryCameraAt=DateTime.MinValue;
  void InitDeviceMonitor(){devicePoll.Tick+=async(_,__)=>await RefreshDevices();Activated+=async(_,__)=>{if(formReady)await RefreshDevices();};}
  protected override void WndProc(ref WF.Message message){base.WndProc(ref message);if(message.Msg==0x219&&formReady&&!IsDisposed)_=RefreshDevices();}
  async Task RefreshDevices(bool autoOpen=true){if(deviceRefreshRunning||IsDisposed||Disposing)return;deviceRefreshRunning=true;
   try{var found=await Task.Run(CameraSession.Enumerate);if(Entry.DemoCamera)found=found.Concat(new[]{new CameraDevice{Name="演示高拍仪（软件测试）",Id="TEST_DEMO_INPUT"}}).ToArray();if(IsDisposed||Disposing)return;
    string selected=(devices.SelectedItem as CameraDevice)?.Id??settings.PreferredCamera;
    var existing=devices.Items.Cast<CameraDevice>().Select(d=>d.Id+"\n"+d.Name).ToArray();bool changed=!existing.SequenceEqual(found.Select(d=>d.Id+"\n"+d.Name));
    if(camera!=null&&!found.Any(d=>d.Id==activeCameraId)){CloseCamera(true);retryCameraAt=DateTime.UtcNow.AddSeconds(2);status.Text="相机已断开，正在等待重新连接；题图和选区保留。";}
    if(changed){refreshingDevices=true;try{devices.Items.Clear();devices.Items.AddRange(found);devices.SelectedItem=found.FirstOrDefault(d=>d.Id==selected)??found.FirstOrDefault(d=>d.Id==settings.PreferredCamera)??found.FirstOrDefault();}finally{refreshingDevices=false;}if(found.Length==0)status.Text="等待相机连接（会自动发现）；也可载入原始照片。";else if(camera==null&&!regionDialogOpen)status.Text="已自动发现相机；可以拍照选题。";}
    if(camera!=null&&camera.PreviewStalled&&!regionDialogOpen){CloseCamera(true);retryCameraAt=DateTime.UtcNow.AddSeconds(1);status.Text="预览停止刷新，正在重新连接相机；已选题目保留。";}
    Buttons();if(autoOpen&&found.Length>0&&cameraWanted&&!regionDialogOpen&&DateTime.UtcNow>=retryCameraAt)await Guard(EnsureCamera);
   }catch(Exception){if(!IsDisposed&&!Disposing&&camera==null)status.Text="正在等待相机设备恢复；题图不受影响。";retryCameraAt=DateTime.UtcNow.AddSeconds(5);}finally{deviceRefreshRunning=false;}
  }
 }
}
