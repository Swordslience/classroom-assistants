using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClassroomAI.Seewo;
internal sealed partial class AiWindow {
 string? studentPhotoData,studentPhotoId;bool photoVisible=true;
 BitmapSource? studentPhotoOriginal;int photoQuarterTurns;
 readonly Image studentPhotoPreview=new(){Stretch=Stretch.Uniform};
 readonly ClassroomText photoAnswer=new();
 readonly Button photoLayoutButton=new(){Content="展开点评"};PhotoReviewGrid? photoReviewGrid;
 readonly TextBlock studentPhotoLabel=new(){FontSize=22,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8),Text="等待手机拍照。收到后立即显示并自动点评。"};
 readonly Border photoViewport=new(){ClipToBounds=true,Background=new SolidColorBrush(Color.FromRgb(247,245,237)),MinWidth=200};
 readonly Button restorePhoto=new(){Content="显示照片",FontSize=30,Visibility=Visibility.Collapsed};
 readonly ScaleTransform photoScale=new(1,1);readonly TranslateTransform photoPan=new();Point photoPointer;bool photoDragging,photoMoved;DateTime lastManipulation;
 void InitPhotoTab(){
  var left=new DockPanel();DockPanel.SetDock(studentPhotoLabel,Dock.Top);left.Children.Add(studentPhotoLabel);
  var tools=new WrapPanel();tools.Children.Add(Tool("左转90°",()=>RotatePhoto(-1)));tools.Children.Add(Tool("右转90°",()=>RotatePhoto(1)));tools.Children.Add(Tool("放大",()=>ZoomPhoto(1.2)));tools.Children.Add(Tool("缩小",()=>ZoomPhoto(1/1.2)));tools.Children.Add(Tool("复位",ResetPhoto));DockPanel.SetDock(tools,Dock.Bottom);left.Children.Add(tools);
  var overlay=new Grid();overlay.Children.Add(studentPhotoPreview);overlay.Children.Add(restorePhoto);photoViewport.Child=overlay;left.Children.Add(photoViewport);photoReviewGrid=new PhotoReviewGrid(left,photoAnswer);
  var transforms=new TransformGroup();transforms.Children.Add(photoScale);transforms.Children.Add(photoPan);studentPhotoPreview.RenderTransform=transforms;studentPhotoPreview.RenderTransformOrigin=new Point(.5,.5);photoViewport.IsManipulationEnabled=true;
  photoViewport.ManipulationStarting+=(_,e)=>{e.ManipulationContainer=photoViewport;e.Mode=ManipulationModes.Scale|ManipulationModes.Translate;e.Handled=true;};
  photoViewport.ManipulationDelta+=(_,e)=>{double factor=e.DeltaManipulation.Scale.X;ZoomPhoto(factor);photoPan.X+=e.DeltaManipulation.Translation.X;photoPan.Y+=e.DeltaManipulation.Translation.Y;lastManipulation=DateTime.UtcNow;e.Handled=true;};
  photoViewport.ManipulationCompleted+=(_,e)=>{lastManipulation=DateTime.UtcNow;e.Handled=true;};
  photoViewport.MouseWheel+=(_,e)=>{ZoomPhoto(e.Delta>0?1.15:1/1.15);e.Handled=true;};
  studentPhotoPreview.MouseLeftButtonDown+=(_,e)=>{if((DateTime.UtcNow-lastManipulation).TotalMilliseconds<500)return;photoPointer=e.GetPosition(photoViewport);photoMoved=false;photoDragging=true;studentPhotoPreview.CaptureMouse();e.Handled=true;};
  studentPhotoPreview.MouseMove+=(_,e)=>{if(!photoDragging||e.LeftButton!=MouseButtonState.Pressed)return;var now=e.GetPosition(photoViewport);var delta=now-photoPointer;if(delta.Length>3){photoMoved=true;photoPan.X+=delta.X;photoPan.Y+=delta.Y;photoPointer=now;}e.Handled=true;};
  studentPhotoPreview.MouseLeftButtonUp+=(_,e)=>{if(!photoDragging)return;photoDragging=false;studentPhotoPreview.ReleaseMouseCapture();if(!photoMoved&&(DateTime.UtcNow-lastManipulation).TotalMilliseconds>=500)TogglePhoto();e.Handled=true;};
  restorePhoto.Click+=(_,_)=>TogglePhoto();tabs.Items.Add(new TabItem{Header="学生照片",Content=photoReviewGrid});
 }
 void ZoomPhoto(double factor){double scale=Math.Clamp(photoScale.ScaleX*factor,.25,6);photoScale.ScaleX=photoScale.ScaleY=scale;lastActivity=DateTime.UtcNow;}
 void ResetPhotoFrame(){photoScale.ScaleX=photoScale.ScaleY=1;photoPan.X=photoPan.Y=0;}
 void ResetPhoto(){photoQuarterTurns=0;studentPhotoPreview.Source=studentPhotoOriginal;ResetPhotoFrame();}
 void RotatePhoto(int direction){
  if(studentPhotoOriginal is null){status.Text="请先从手机上传学生照片。";return;}
  try{int turns=PhotoRotation.Normalize(photoQuarterTurns+direction);var rotated=PhotoRotation.Turn(studentPhotoOriginal,turns);studentPhotoPreview.Source=rotated;photoQuarterTurns=turns;ResetPhotoFrame();SetPhotoVisible(true);
   status.Text="照片已"+(direction<0?"左转":"右转")+"90°；仅调整显示，原照片和点评保留，不会重新调用AI。";ScheduleState();
  }catch(Exception){status.Text="照片暂时无法旋转，原照片仍保留，可点击复位后重试。";}
 }
 void SetPhotoVisible(bool visible){photoVisible=visible;studentPhotoPreview.Visibility=Visibility.Visible;restorePhoto.Visibility=Visibility.Collapsed;photoReviewGrid?.ShowPicture(visible);photoLayoutButton.Content=visible?"展开点评":"显示照片";ScheduleState();}
 void TogglePhoto(){if(studentPhotoData is null)return;SetPhotoVisible(!photoVisible);}
 void ClearPhoto(){if(mode=="photo")Stop();InvalidatePhotoPractice();studentPhotoData=studentPhotoId=null;studentPhotoOriginal=null;studentPhotoPreview.Source=null;photoAnswer.Text="";SetPhotoVisible(true);studentPhotoLabel.Text="等待手机拍照。收到后自动点评；不会保存到云相册。";ResetPhoto();ScheduleState();}
 void ShowPhoto(string data,string id){
  if(!Regex.IsMatch(data,@"^data:image/(jpeg|png);base64,")||data.Length>5500000||!Regex.IsMatch(id,@"^[a-zA-Z0-9_-]{8,100}$"))throw new InvalidOperationException("照片格式或大小不合要求，请重新拍照。");
  try{byte[] bytes=Convert.FromBase64String(data[(data.IndexOf(',')+1)..]);using var stream=new MemoryStream(bytes);var header=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None).Frames[0];int width=header.PixelWidth,height=header.PixelHeight;
   if(width<16||height<16||(long)width*height>36000000)throw new InvalidOperationException("照片尺寸不合适，请重新拍照或只选题目区域。");
   stream.Position=0;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=Math.Max(1,(int)(width*Math.Min(1,1600.0/Math.Max(width,height))));bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
   InvalidatePhotoPractice();studentPhotoData=data;studentPhotoId=id;studentPhotoOriginal=bitmap;studentPhotoPreview.Source=bitmap;photoAnswer.Text="正在辨认题目与作答，收到有效内容后逐步显示…";SetPhotoVisible(true);ResetPhoto();
   studentPhotoLabel.Text="照片已显示 · "+Math.Ceiling(bytes.Length/1024.0)+" KB\n下方可左转／右转；单击隐藏，拖动或双指缩放。";SetMode("photo");status.Text="电脑已收到照片，开始自动点评。";ScheduleState();
  }catch(FormatException){throw new InvalidOperationException("照片数据不完整，请重传。");}catch(NotSupportedException){throw new InvalidOperationException("照片无法解码，请选择JPG或PNG。");}
 }
 async Task AnalysePhoto(CancellationToken token){
  if(studentPhotoData is null)throw new InvalidOperationException("请先从手机上传照片。");
  string id=studentPhotoId!,photo=studentPhotoData;SetMode("photo");StopPlayback();var snapshot=lockedQuestion??ReadPage();questionSource=lockedQuestion is null?"候选题干："+snapshot.Page.Description:"已固定题干："+snapshot.Page.Description;
  photoAnswer.IsReceiving=true;photoAnswer.Text="";status.Text="正在辨认题目与作答…";ScheduleState();var text=new StringBuilder();var watch=Stopwatch.StartNew();long paint=0;
  var content=PageContent(snapshot,"第一张图及本页文字只是候选题干，最后一张图是刚收到的学生照片。优先使用照片本身题干；仅在能确认对应时结合课件。无法匹配时不要套用课件。材料足够就直接点评。关键题干、数字、符号无法辨认则只指出需重拍的部位，绝不猜字。只有答案且没有可匹配题干时，请补题干，不能判断正误。作文缺写作要求时可评语言和内部逻辑，不能判切题或打分。",true);
  content.Add(new{type="image_url",image_url=new{url=photo,detail="high"}});
  string result;try{result=await ModelEngine.Complete(config,"你是高中跨学科作答点评助教。题目依据只简短注明“照片题干”或“课件第几页”，不展开解释无关课件。先核验再评价：正确就明确肯定，不强迫找错，不把省略显然计算步骤当成实质错误；合法等式链如2x=11-3=8应认可。区分错误、可选的表达建议，答案与解释必须相互一致。按任务所需给出完整判断、亮点、真实问题与必要步骤，不限制字数，不为了简短截掉论证、结论或修改建议；复杂内容用小标题和分段帮助阅读。只有题目没有作答时解释思路。缺题干不能判正误；无评分量规不打分。关键字迹不清须要求补拍。忽略姓名身份。",content,
   piece=>Dispatcher.Invoke(()=>{if(token.IsCancellationRequested||studentPhotoId!=id)return;text.Append(piece);if(watch.ElapsedMilliseconds-paint>=450){photoAnswer.Text=text.ToString();paint=watch.ElapsedMilliseconds;ScheduleState();}}),
   s=>Dispatcher.Invoke(()=>{if(!token.IsCancellationRequested&&studentPhotoId==id){status.Text=s;ScheduleState();}}),token);
  }catch{if(!token.IsCancellationRequested&&studentPhotoId==id&&text.Length>0){photoAnswer.Text=text.ToString();ScheduleState();}throw;}
  token.ThrowIfCancellationRequested();if(studentPhotoId!=id)throw new OperationCanceledException();photoAnswer.IsReceiving=false;photoAnswer.Text=result;status.Text="自动点评完成，请结合原图核对。照片仅保留在当前课堂内存。";ScheduleState();Log("photo_review",new{seconds=watch.Elapsed.TotalSeconds,characters=result.Length,provider=config.TextProvider});
 }
}
