using System;
using System.Collections.Generic;
using System.Drawing;
using Point=System.Drawing.Point;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using WF=System.Windows.Forms;
namespace PhotoTutor {
 internal static class CaptureWorkflowChecks {
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Private)!.GetValue(value)!;
  static IEnumerable<WF.Control> All(WF.Control value){foreach(WF.Control child in value.Controls){yield return child;foreach(var nested in All(child))yield return nested;}}
  static WF.Button Button(WF.Form form,string text)=>All(form).OfType<WF.Button>().Single(button=>button.Text==text);
  static void Pump(Func<bool> done,int seconds=15){var end=DateTime.UtcNow.AddSeconds(seconds);while(DateTime.UtcNow<end){WF.Application.DoEvents();if(done())return;Thread.Sleep(15);}throw new TimeoutException("UI operation did not finish");}
  static void Wait(Task task){Pump(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
  static void Hidden(WF.Form form){form.ShowInTaskbar=false;form.StartPosition=WF.FormStartPosition.Manual;form.Location=new Point(-12000,-12000);}
  static void Save(WF.Form form,string file){using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(file);}}
  static void Mouse(RegionCanvas canvas,string name,WF.MouseEventArgs args)=>typeof(WF.Control).GetMethod(name,Private)!.Invoke(canvas,new object[]{args});
  static void Drag(RegionCanvas canvas,Int32Rect region){var bounds=canvas.ImageBounds;var page=canvas.Page!;var start=new Point((int)(bounds.Left+region.X*bounds.Width/page.Width),(int)(bounds.Top+region.Y*bounds.Height/page.Height));var end=new Point((int)(bounds.Left+(region.X+region.Width)*bounds.Width/page.Width),(int)(bounds.Top+(region.Y+region.Height)*bounds.Height/page.Height));Mouse(canvas,"OnMouseDown",new WF.MouseEventArgs(WF.MouseButtons.Left,1,start.X,start.Y,0));Mouse(canvas,"OnMouseMove",new WF.MouseEventArgs(WF.MouseButtons.Left,0,end.X,end.Y,0));Mouse(canvas,"OnMouseUp",new WF.MouseEventArgs(WF.MouseButtons.Left,1,end.X,end.Y,0));WF.Application.DoEvents();}
  internal static int Run(string folder){Directory.CreateDirectory(folder);Settings.IsolatedTestRoot=Path.Combine(folder,"isolated-runtime");Entry.OfflineUiTesting=true;WF.Application.EnableVisualStyles();int failed=0;var rows=new List<object>();
   void Check(string name,Action action){try{action();rows.Add(new{name,passed=true});}catch(Exception error){failed++;rows.Add(new{name,passed=false,error=error.ToString()});}finally{CameraSession.EnumerateForTest=null;Entry.DemoCamera=false;}}
   Check("completed mouse drags append; repeated zoom/resize never changes the first confirmed box",()=>{using(var workspace=new QuestionWorkspace(Settings.DataRoot)){var page=workspace.Add(MultiRegionChecks.PrintedPage(),"合成三题");using(var form=new QuestionBoardDialog(workspace,true){AutoAnalyze=false}){Hidden(form);form.Show();WF.Application.DoEvents();var canvas=Field<RegionCanvas>(form,"canvas");Drag(canvas,new Int32Rect(80,80,500,280));string first=workspace.Active.Regions.Single();var original=workspace.Regions.Single(r=>r.Id==first).Bounds;
    for(int i=0;i<10;i++){if(i==2)canvas.Zoom(1.25f);if(i==5)canvas.Zoom(.8f);Drag(canvas,new Int32Rect(700,500+i*45,320,180));if(workspace.Active.Regions.Count!=i+2||workspace.Regions.Single(r=>r.Id==first).Bounds!=original)throw new Exception("a later drag replaced an earlier selection");}
    form.Width=Math.Max(form.MinimumSize.Width,form.Width-150);WF.Application.DoEvents();if(workspace.Regions.Single(r=>r.Id==first).Bounds!=original)throw new Exception("resize moved source coordinates");
    var basket=Field<WF.ListBox>(form,"basket");basket.SelectedIndex=1;string edited=workspace.Active.Regions[1];Button(form,"调整此框").PerformClick();Drag(canvas,new Int32Rect(650,400,400,220));if(workspace.Active.Regions.Count!=11||workspace.Regions.Single(r=>r.Id==first).Bounds!=original||workspace.Regions.Single(r=>r.Id==edited).Bounds.Y>450)throw new Exception("explicit adjustment did not target only the selected box");Drag(canvas,new Int32Rect(700,1000,320,180));if(workspace.Active.Regions.Count!=12)throw new Exception("twelfth region was not accepted");int before=workspace.Regions.Count;Drag(canvas,new Int32Rect(700,1200,320,180));if(workspace.Active.Regions.Count!=12||workspace.Regions.Count!=before||workspace.Regions.Single(r=>r.Id==first).Bounds!=original)throw new Exception("limit rejection changed existing boxes or created an orphan");Save(form,Path.Combine(folder,"实际鼠标事件_多框保留.png"));form.Close();}}});
   Check("opening a captured page starts real local candidate analysis and shows blue proposals without solving",()=>{using(var workspace=new QuestionWorkspace(Settings.DataRoot)){workspace.Add(MultiRegionChecks.PrintedPage(),"合成印刷页");using(var form=new QuestionBoardDialog(workspace,true)){Hidden(form);form.Show();Pump(()=>Field<CancellationTokenSource?>(form,"analysis")!=null);Wait(form.AnalysisTask);if(!workspace.Regions.Any(r=>r.Candidate)||workspace.Active.Regions.Count!=0||form.Result!=null)throw new Exception("automatic proposals were absent or automatically submitted: "+Field<WF.Label>(form,"status").Text);Save(form,Path.Combine(folder,"拍后自动题框_实际控件.png"));form.Close();}}});

   string fixtureIndex=Path.Combine(folder,"跨页样本索引.json");
   if(File.Exists(fixtureIndex))Check("two separate Chinese generated pages: lower-left then upper-right then next page, exact crop pixels and stable IDs",()=>{
    var files=Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(fixtureIndex));using(var workspace=new QuestionWorkspace(Settings.DataRoot)){
     var p1=workspace.AddFile((string)files[0]!);var second=PhotoData.FromFileForSelection((string)files[1]!);
     using(var form=new QuestionBoardDialog(workspace,true,(_,__)=>Task.FromResult<PhotoData?>(second)){AutoAnalyze=false}){
      Hidden(form);form.Show();WF.Application.DoEvents();var canvas=Field<RegionCanvas>(form,"canvas");
      Drag(canvas,new Int32Rect(45,725,470,635));string first=workspace.Active.Regions.Single();var firstBounds=workspace.Regions.Single(r=>r.Id==first).Bounds;
      Drag(canvas,new Int32Rect(545,70,470,345));string secondId=workspace.Active.Regions[1];var secondBounds=workspace.Regions.Single(r=>r.Id==secondId).Bounds;
      if(workspace.Active.Regions.Count!=2||workspace.Regions.Single(r=>r.Id==first).Bounds!=firstBounds)throw new Exception("upper-right replaced the lower-left");
      Save(form,Path.Combine(folder,"中文单道长题_左下接右上.png"));
      Button(form,"继续拍下一页").PerformClick();Pump(()=>workspace.Pages.Count==2&&canvas.Page!.Id!=p1.Id);
      Drag(canvas,new Int32Rect(65,105,450,510));
      if(workspace.Active.Regions.Count!=3||workspace.Regions.Single(r=>r.Id==first).Bounds!=firstBounds||workspace.Regions.Single(r=>r.Id==secondId).Bounds!=secondBounds)throw new Exception("next page changed earlier boxes");
      Save(form,Path.Combine(folder,"中文独立第二页_三框保留.png"));
      var payload=workspace.Compose(true,CancellationToken.None);if(payload.RegionCount!=3||payload.SourcePageCount!=2||payload.Images.Length!=2)throw new Exception("separate source pages were collapsed or lost");
      int imageIndex=-1,offset=0;string lastPage="";foreach(var id in workspace.Active.Regions){var r=workspace.Regions.Single(v=>v.Id==id);var page=workspace.Pages.Single(v=>v.Id==r.PageId);
       if(lastPage!=r.PageId){imageIndex++;offset=6;lastPage=r.PageId;}offset+=32;
       byte[] Pixels(BitmapSource image,Int32Rect box){var converted=new FormatConvertedBitmap(new CroppedBitmap(image,box),PixelFormats.Bgr24,null,0);var bytes=new byte[box.Width*box.Height*3];converted.CopyPixels(bytes,box.Width*3,0);return bytes;}
       var expected=Pixels(page.Decode(),new Int32Rect(r.Bounds.X+5,r.Bounds.Y+5,r.Bounds.Width-10,r.Bounds.Height-10));
       var actual=Pixels(payload.Images[imageIndex].Image,new Int32Rect(5,offset+5,r.Bounds.Width-10,r.Bounds.Height-10));if(!expected.SequenceEqual(actual))throw new Exception("composite reordered source pixels or included unselected content");
       offset+=r.Bounds.Height+8;
      }
      for(int i=0;i<payload.Images.Length;i++)File.WriteAllBytes(Path.Combine(folder,"中文长题_最终输入_"+(i+1)+".jpg"),payload.Images[i].Jpeg);
      string originalGroup=workspace.ActiveId;var originalIds=workspace.Active.Regions.ToArray();Button(form,"换一道题").PerformClick();var pages=Field<WF.ComboBox>(form,"pages");pages.SelectedIndex=0;WF.Application.DoEvents();Drag(canvas,new Int32Rect(545,485,470,340));
      if(workspace.Active.Regions.Count!=1||!workspace.Groups.Single(g=>g.Id==originalGroup).Regions.SequenceEqual(originalIds))throw new Exception("independent question was merged into the long question");
      form.Close();
     }
    }
   });

   Check("unnumbered prose and blank input do not produce fake question candidates",()=>{if(CandidateDetector.FromLines(Array.Empty<OcrLine>(),800,1000,800,1000).Count!=0||CandidateDetector.FromLines(new[]{new OcrLine{Text="没有题号的普通阅读材料",X=50,Y=100,Width=500,Height=30},new OcrLine{Text="第二段不是独立题目",X=50,Y=400,Width=500,Height=30}},800,1000,800,1000).Count!=0)throw new Exception("layout blocks were presented as questions");});
   Check("cancelled edit drafts do not change accepted pages, region IDs, coordinates or groups",()=>{using(var original=new QuestionWorkspace(Settings.DataRoot)){var page=original.Add(MultiRegionChecks.PrintedPage(),"原始页");var region=original.AddRegion(page.Id,new Int32Rect(50,80,500,300));original.Attach(region.Id);long stamp=original.Revision;using(var draft=original.Clone()){draft.Replace(region.Id,new Int32Rect(100,200,400,300));draft.Add(MultiRegionChecks.PrintedPage(),"取消的补页");if(draft.Pages.Count!=2||draft.Regions[0].Bounds==original.Regions[0].Bounds)throw new Exception("draft shares mutable source state");}if(original.Revision!=stamp||original.Pages.Count!=1||original.Active.Regions.Single()!=region.Id||original.Regions[0].Bounds!=new Int32Rect(50,80,500,300)||!File.Exists(page.File))throw new Exception("cancel leaked into accepted question");}});
   Check("orientation analysis boxes map back to original pixels for all four right-angle rotations",()=>{var original=new Int32Rect(100,200,300,400);var inputs=new[]{original,new Int32Rect(1600-200-400,100,400,300),new Int32Rect(1200-100-300,1600-200-400,300,400),new Int32Rect(200,1200-100-300,400,300)};int i=0;foreach(int angle in new[]{0,90,180,270}){var mapped=PageLayout.BackToOriginal(inputs[i++],1200,1600,angle,2400,3200);if(mapped!=new Int32Rect(200,400,600,800))throw new Exception("rotated proposal coordinates changed");}});
   Check("device timer discovers add/remove without refresh clicks, retains selected identity and does not cancel answers",()=>{CameraDevice[] available={new CameraDevice{Id="fake_a",Name="测试A"}};CameraSession.EnumerateForTest=()=>Volatile.Read(ref available);using(var form=new TutorForm()){typeof(TutorForm).GetField("cameraWanted",Private)!.SetValue(form,false);Hidden(form);form.Show();var devices=Field<WF.ComboBox>(form,"devices");Pump(()=>devices.Items.Count==1);typeof(TutorForm).GetField("answer",Private)!.SetValue(form,"keep existing answer");long version=Field<long>(form,"version");Volatile.Write(ref available,new[]{new CameraDevice{Id="fake_a",Name="测试A"},new CameraDevice{Id="fake_b",Name="测试B"}});Pump(()=>devices.Items.Count==2,6);if(((CameraDevice)devices.SelectedItem).Id!="fake_a")throw new Exception("poll reset selected camera");Volatile.Write(ref available,new[]{new CameraDevice{Id="fake_b",Name="测试B"}});Pump(()=>devices.Items.Count==1&&((CameraDevice)devices.SelectedItem).Id=="fake_b",6);if(Field<string>(form,"answer")!="keep existing answer"||Field<long>(form,"version")!=version||Field<CameraSession?>(form,"camera")!=null)throw new Exception("device discovery changed the lesson or opened a physical camera");if(All(form).OfType<WF.Button>().Any(b=>b.Text=="刷新设备"))throw new Exception("manual refresh is still required in the primary flow");form.Close();}});
   foreach(string captureButton in new[]{"拍照选题 F8","重新选题／继续补拍"})Check("photo action opens a nonblank editor and retains all cross-page boxes / "+captureButton,()=>{Entry.DemoCamera=true;CameraSession.EnumerateForTest=()=>Array.Empty<CameraDevice>();using(var main=new TutorForm()){Hidden(main);int calls=0;main.SolveForTest=()=>{calls++;return Task.CompletedTask;};int stage=-1;Exception? error=null;bool timedOut=false;string[] saved=Array.Empty<string>();Int32Rect[] rectangles=Array.Empty<Int32Rect>();QuestionWorkspace? workspace=null;
    using(var timer=new WF.Timer{Interval=100})using(var guard=new System.Threading.Timer(_=>{try{main.BeginInvoke(new Action(()=>{timedOut=true;foreach(var form in WF.Application.OpenForms.Cast<WF.Form>().Where(f=>f!=main).ToArray())form.Close();}));}catch{}},null,25000,Timeout.Infinite)){
     timer.Tick+=(_,__)=>{try{var board=WF.Application.OpenForms.OfType<QuestionBoardDialog>().FirstOrDefault();var capture=WF.Application.OpenForms.OfType<CapturePageDialog>().FirstOrDefault();var preview=WF.Application.OpenForms.Cast<WF.Form>().FirstOrDefault(f=>f.Text.StartsWith("确认本题"));
      if(stage==-1&&Field<BitmapSource?>(main,"live")!=null){stage=0;Button(main,captureButton).PerformClick();}
      else if(stage==0&&board!=null){stage=10;workspace=Field<QuestionWorkspace>(board,"workspace");var canvas=Field<RegionCanvas>(board,"canvas");if(canvas.Page==null)throw new Exception("camera frame was not imported");var p=canvas.Page;Drag(canvas,new Int32Rect(p.Width/10,p.Height/10,p.Width/3,p.Height/4));Drag(canvas,new Int32Rect(p.Width/2,p.Height/2,p.Width/3,p.Height/4));saved=workspace.Active.Regions.ToArray();rectangles=saved.Select(id=>workspace.Regions.Single(r=>r.Id==id).Bounds).ToArray();if(saved.Length!=2)throw new Exception("expected two selected boxes after two drags; actual="+saved.Length);Save(board,Path.Combine(folder,"完整拍照流程_第一页双框.png"));stage=1;Button(board,"继续拍下一页").PerformClick();}
      else if(stage==1&&capture!=null&&Button(capture,"拍下本页并返回选题").Enabled){stage=2;Button(capture,"拍下本页并返回选题").PerformClick();}
      else if(stage==2&&board!=null&&workspace?.Pages.Count==2){stage=12;for(int i=0;i<saved.Length;i++)if(workspace.Regions.Single(r=>r.Id==saved[i]).Bounds!=rectangles[i])throw new Exception("second-page capture changed a first-page box");var canvas=Field<RegionCanvas>(board,"canvas");if(canvas.Page!.Id!=workspace.Pages[1].Id)throw new Exception("new page was replaced by a basket selection");Drag(canvas,new Int32Rect(canvas.Page.Width/5,canvas.Page.Height/5,canvas.Page.Width/3,canvas.Page.Height/3));if(workspace.Active.Regions.Count!=3)throw new Exception("cross-page region missing");Save(board,Path.Combine(folder,"完整拍照流程_补页后三框.png"));stage=3;Button(board,"使用选区并讲解").PerformClick();}
      else if(stage==3&&preview!=null){stage=4;Button(preview,"确认，开始讲解").PerformClick();}
     }catch(Exception problem){error=problem;stage=99;foreach(var form in WF.Application.OpenForms.Cast<WF.Form>().Where(f=>f!=main).ToArray())form.Close();}};
     main.Show();timer.Start();Pump(()=>calls==1||error!=null||timedOut,28);timer.Stop();if(error!=null)throw error;if(timedOut)throw new TimeoutException("full capture workflow stalled");var payload=Field<QuestionPayload>(main,"regionPayload");if(calls!=1||payload.RegionCount!=3||payload.SourcePageCount!=2||payload.Images.Length!=2)throw new Exception("confirmation did not commit exactly the selected two-page group");main.Close();Pump(()=>!CameraSession.IsActive,5);
    }
   }});
   Entry.OfflineUiTesting=false;CameraSession.EnumerateForTest=null;Entry.DemoCamera=false;
   File.WriteAllText(Path.Combine(folder,"拍照后选题_实际流程回归.json"),JsonConvert.SerializeObject(new{failed,checks=rows,modelCalls=0,physicalCameraOpened=false,uiEvents="Actual WinForms mouse/button/timer/modal events in this test process; software camera frames only"},Formatting.Indented));return failed==0?0:1;
  }
 }
}
