using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WF=System.Windows.Forms;
namespace PhotoTutor {
 internal sealed partial class TutorForm {
  QuestionWorkspace? regionWorkspace;QuestionPayload? regionPayload;
  bool preparingSelection;
  readonly WF.CheckBox regionMode=new WF.CheckBox{Text="多区域／单题点选",AutoSize=true};
  internal Func<Task>? SolveForTest;
  void InitRegionTools(WF.FlowLayoutPanel capture,WF.FlowLayoutPanel tools){tools.Controls.Add(B("重新选题／继续补拍",()=>_=Guard(()=>EditRegions(null))));}
  Task EditRegionBitmap(System.Windows.Media.Imaging.BitmapSource source,string label)=>OpenNewSelection(new[]{PhotoData.SelectionSource(source,label)});
  async Task OpenNewSelection(PhotoData[] sources){if(work!=null)throw new InvalidOperationException("请先停止当前讲解，再拍新题。");if(preparingSelection||regionDialogOpen)throw new InvalidOperationException("正在准备选题窗口，请稍候。");preparingSelection=true;Buttons();status.Text="正在保留原始照片并准备选题窗口…";var workspace=new QuestionWorkspace(Settings.DataRoot);try{var originals=sources.Select(v=>new{Image=v.Image.IsFrozen?v.Image:v.Image.CloneCurrentValue(),v.Label}).ToArray();foreach(var original in originals)original.Image.Freeze();await Task.Run(()=>{foreach(var original in originals)workspace.Add(original.Image,original.Label);});if(IsDisposed){workspace.Dispose();return;}}catch{workspace.Dispose();throw;}finally{preparingSelection=false;if(!IsDisposed)Buttons();}await EditWorkspace(workspace,true);}
  async Task EditRegions(PhotoData? source){if(work!=null)throw new InvalidOperationException("请先停止当前讲解，再修改选区。");if(source!=null){await OpenNewSelection(new[]{source});return;}
   if(regionWorkspace==null&&originalPages.Count==0){await Snapshot();return;}
   var draft=regionWorkspace?.Clone()??new QuestionWorkspace(Settings.DataRoot);try{if(regionWorkspace==null)foreach(var original in originalPages)draft.CopyOriginal(original);}catch{draft.Dispose();throw;}
   await EditWorkspace(draft,true);
  }
  async Task<PhotoData?> CaptureNextPage(WF.IWin32Window owner,CancellationToken token){if(!cameraRelease.IsCompleted&&await Task.WhenAny(cameraRelease,Task.Delay(5000,token))!=cameraRelease)throw new InvalidOperationException("相机仍在释放，请稍候重试，已有选区保留。");token.ThrowIfCancellationRequested();return await CapturePageDialog.Capture(owner,settings.PreferredCamera,high.Checked,token);}
  async Task EditWorkspace(QuestionWorkspace workspace,bool fresh){Stop("");CloseCamera(true);regionDialogOpen=true;QuestionPayload? payload=null;
   try{using(var dialog=new QuestionBoardDialog(workspace,high.Checked,CaptureNextPage)){if(dialog.ShowDialog(this)==WF.DialogResult.OK)payload=dialog.Result;}}
   catch{if(fresh)workspace.Dispose();throw;}finally{regionDialogOpen=false;}
   if(payload==null){if(fresh)workspace.Dispose();RefreshPageView();status.Text="已取消选题，原题和回答保留。";return;}
   if(payload.Revision!=workspace.Revision){if(fresh)workspace.Dispose();throw new InvalidOperationException("选区已变化，请重新预览。");}
   if(regionWorkspace!=workspace)regionWorkspace?.Dispose();regionWorkspace=workspace;pages.Clear();ClearOriginals();pages.AddRange(payload.Images);originalPages.AddRange(payload.Images);selectedPage=0;ResetForChangedImages();regionPayload=payload;RefreshPageView();sourceLabel.Text="已确认："+payload.RegionCount+"区域／"+payload.SourcePageCount+"原始页";
   if(SolveForTest!=null)await SolveForTest();else await Solve();
  }
  void ClearRegions(){regionPayload=null;regionWorkspace?.Dispose();regionWorkspace=null;}
  string RegionTask(string text)=>regionPayload==null?text:regionPayload.Instruction+"\n本次要求："+text;
 }
}
