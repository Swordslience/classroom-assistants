using System;
using System.Drawing;
using WF=System.Windows.Forms;

namespace PhotoTutor {
 internal static class CropGeometry {
  internal static RectangleF Fit(Size view,Size image){float ratio=Math.Min(view.Width/(float)image.Width,view.Height/(float)image.Height);return new RectangleF((view.Width-image.Width*ratio)/2,(view.Height-image.Height*ratio)/2,image.Width*ratio,image.Height*ratio);}
  internal static System.Windows.Int32Rect Pixels(RectangleF box,RectangleF image,Size source){
   int left=Math.Max(0,Math.Min(source.Width,(int)Math.Floor((Math.Min(box.Left,box.Right)-image.Left)*source.Width/image.Width)));
   int top=Math.Max(0,Math.Min(source.Height,(int)Math.Floor((Math.Min(box.Top,box.Bottom)-image.Top)*source.Height/image.Height)));
   int right=Math.Max(left,Math.Min(source.Width,(int)Math.Ceiling((Math.Max(box.Left,box.Right)-image.Left)*source.Width/image.Width)));
   int bottom=Math.Max(top,Math.Min(source.Height,(int)Math.Ceiling((Math.Max(box.Top,box.Bottom)-image.Top)*source.Height/image.Height)));
   return new System.Windows.Int32Rect(left,top,right-left,bottom-top);
  }
 }
 internal sealed class PhotoCropDialog:WF.Form {
  readonly CropCanvas canvas;readonly WF.Button accept;PhotoData? result;
  PhotoCropDialog(PhotoData source,bool high){
   Text="框选需要解答的题目";Font=new Font("Microsoft YaHei",18);StartPosition=WF.FormStartPosition.CenterParent;Size=new Size(Math.Min(1220,WF.Screen.PrimaryScreen.WorkingArea.Width),Math.Min(900,WF.Screen.PrimaryScreen.WorkingArea.Height));MinimumSize=new Size(800,540);KeyPreview=true;
   var bar=new WF.FlowLayoutPanel{Dock=WF.DockStyle.Top,AutoSize=true,Padding=new WF.Padding(6),WrapContents=true};
   WF.Button Button(string text,Action run){var b=new WF.Button{Text=text,AutoSize=true,MinimumSize=new Size(120,50),Margin=new WF.Padding(4)};b.Click+=(_,__)=>run();bar.Controls.Add(b);return b;}
   canvas=new CropCanvas(source);accept=Button("使用选区",()=>{try{var area=canvas.SelectedPixels;if(area.Width<32||area.Height<32)return;result=source.Crop(area,high);DialogResult=WF.DialogResult.OK;Close();}catch(Exception e){WF.MessageBox.Show(this,e.Message,"重新框选");}});accept.Enabled=false;
   Button("整张使用",()=>{result=source;DialogResult=WF.DialogResult.OK;Close();});Button("重新框选",()=>canvas.Reset());Button("取消",()=>{DialogResult=WF.DialogResult.Cancel;Close();});
   var tips=new WF.Label{Dock=WF.DockStyle.Bottom,AutoSize=true,Padding=new WF.Padding(8),Text="用手指或鼠标拖出方框，保留题干、配图和所需条件，再点“使用选区”。"};
   canvas.SelectionChanged+=()=>accept.Enabled=canvas.SelectedPixels.Width>=32&&canvas.SelectedPixels.Height>=32;
   Controls.Add(canvas);Controls.Add(bar);Controls.Add(tips);KeyDown+=(_,e)=>{if(e.KeyCode==WF.Keys.Escape){DialogResult=WF.DialogResult.Cancel;Close();}};
  }
  internal static PhotoData? Select(WF.IWin32Window owner,PhotoData source,bool high){using(var dialog=new PhotoCropDialog(source,high)){if(dialog.ShowDialog(owner)!=WF.DialogResult.OK)return null;var result=dialog.result;return result!=null&&result.IsSelectionSource?PhotoData.Create(result.Image,result.Label,high):result;}}
  sealed class CropCanvas:WF.Control {
   readonly Bitmap image;RectangleF selection;PointF start;bool dragging;
   internal event Action? SelectionChanged;
   RectangleF BoundsImage=>CropGeometry.Fit(ClientSize,image.Size);
   internal System.Windows.Int32Rect SelectedPixels=>selection.Width>0&&selection.Height>0?CropGeometry.Pixels(selection,BoundsImage,image.Size):new System.Windows.Int32Rect();
   internal CropCanvas(PhotoData source){image=Raster.ToBitmap(source.Image);Dock=WF.DockStyle.Fill;DoubleBuffered=true;BackColor=Color.FromArgb(35,39,43);Cursor=WF.Cursors.Cross;
    MouseDown+=(_,e)=>{if(e.Button!=WF.MouseButtons.Left||!BoundsImage.Contains(e.Location))return;start=e.Location;selection=RectangleF.Empty;dragging=true;Capture=true;SelectionChanged?.Invoke();Invalidate();};
    MouseMove+=(_,e)=>{if(!dragging)return;var bounds=BoundsImage;float x=Math.Max(bounds.Left,Math.Min(bounds.Right,e.X)),y=Math.Max(bounds.Top,Math.Min(bounds.Bottom,e.Y));selection=RectangleF.FromLTRB(Math.Min(start.X,x),Math.Min(start.Y,y),Math.Max(start.X,x),Math.Max(start.Y,y));Invalidate();};
    MouseUp+=(_,e)=>{if(!dragging)return;dragging=false;Capture=false;SelectionChanged?.Invoke();Invalidate();};
   }
   internal void Reset(){selection=RectangleF.Empty;SelectionChanged?.Invoke();Invalidate();}
   protected override void OnResize(EventArgs e){base.OnResize(e);selection=RectangleF.Empty;SelectionChanged?.Invoke();Invalidate();}
   protected override void OnPaint(WF.PaintEventArgs e){base.OnPaint(e);e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;e.Graphics.DrawImage(image,BoundsImage);if(selection.Width>0&&selection.Height>0){using(var shade=new SolidBrush(Color.FromArgb(120,0,0,0)))using(var region=new Region(BoundsImage)){region.Exclude(selection);e.Graphics.FillRegion(shade,region);}using(var pen=new Pen(Color.Gold,3))e.Graphics.DrawRectangle(pen,selection.X,selection.Y,selection.Width,selection.Height);}}
   protected override void Dispose(bool disposing){if(disposing)image.Dispose();base.Dispose(disposing);}
  }
 }
}
