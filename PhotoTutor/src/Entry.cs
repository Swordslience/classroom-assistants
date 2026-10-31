using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Security.Principal;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows;

namespace PhotoTutor {
 internal static class Entry {
  public static bool DemoCamera;internal static bool OfflineUiTesting;
  [STAThread] public static int Main(string[] args) {
   if(args.Length==2&&args[0]=="--analysis-worker")return AnalysisWorker.Run(args[1]);
   SessionDiagnostics.Register();
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly;
   if(args.Length>0&&args[0]=="--self-test")return Checks.Run();
   if(args.Length>0&&args[0]=="--revision-test")return RevisionChecks.Run();
   if(args.Length>0&&args[0]=="--chat-test")return ChatChecks.Run();
   if(args.Length>0&&args[0]=="--capture-test")return Revision130Checks.Run();
   if(args.Length>0&&args[0]=="--capture-live")return Revision130Checks.Live();
   if(args.Length>0&&args[0]=="--review-test")return ReviewFixChecks.Run(args.Length>1?args[1]:System.IO.Path.Combine(System.IO.Path.GetTempPath(),"PhotoTutorReview"),args.Contains("--cloud"));
   if(args.Length>0&&args[0]=="--school-test")return SchoolUsabilityChecks.Run(args.Length>1?args[1]:System.IO.Path.Combine(System.IO.Path.GetTempPath(),"PhotoTutorSchool"));
   if(args.Length>1&&args[0]=="--candidate-corpus")return CandidateCorpusChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--formula-boundary-test")return FormulaBoundaryChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--symbols-test")return SymbolIntegrityChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--layout-review")return LayoutReviewChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--standard-page-test")return StandardPageChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--capture-flow-test")return CaptureWorkflowChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--camera-quality-test")return CameraQualityChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--preview-layout-test")return PreviewLayoutChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--stability-test")return AnalysisStabilityChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--segmentation-test")return SegmentationRegressionChecks.Run(args[1]);
   if(args.Length>1&&args[0]=="--regions-test")return MultiRegionChecks.Run(args[1]);
   DemoCamera=args.Contains("--demo-camera");
   bool created;using(var single=new Mutex(true,"Local\\PhotoTutor_"+WindowsIdentity.GetCurrent().User.Value,out created)) {
   if(!created){MessageBox.Show("深度求索已经运行，请从任务栏打开现有窗口。","深度求索");return 0;}
   ProductIdentity.MigrateDesktopName();
   Settings.CleanOldVoice();
   System.Windows.Forms.Application.EnableVisualStyles();System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
   if(System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"诊断.flag")))new UiProbe();
   System.Windows.Forms.Application.ThreadException+=(_,e)=>{SessionDiagnostics.Record("ui-error",null,e.Exception);MessageBox.Show("操作未完成，已记录诊断信息。原图仍在时可重新选题。\n"+e.Exception.Message,"深度求索");};
   SessionDiagnostics.Record("startup");
   try{System.Windows.Forms.Application.Run(new TutorForm());SessionDiagnostics.Record("normal-exit");return 0;}catch(Exception e){SessionDiagnostics.Record("startup-error",null,e);MessageBox.Show("启动未完成："+e.Message,"深度求索");return 1;}
   }
  }
 }
}
