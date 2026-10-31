using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using WF=System.Windows.Forms;
namespace PhotoTutor {
 internal sealed partial class TutorForm {
  readonly List<PhotoData> pages=new List<PhotoData>(),originalPages=new List<PhotoData>();int selectedPage;
  readonly WF.CheckBox multiPage=new WF.CheckBox{Text="跨页组题：先收集，再解答",AutoSize=true};
  readonly WF.Label pageCount=new WF.Label{AutoSize=true,Padding=new WF.Padding(4,10,4,0)};
  void InitPageTools(WF.FlowLayoutPanel capture,WF.FlowLayoutPanel imageTools){
   imageTools.Controls.Add(B("框选题目",()=>_=Guard(CropCurrent)));imageTools.Controls.Add(B("恢复整图",RestoreFullPage));
   var navigation=imageTools;navigation.Controls.Add(B("上一图",()=>SelectPage(-1)));navigation.Controls.Add(pageCount);navigation.Controls.Add(B("下一图",()=>SelectPage(1)));navigation.Controls.Add(B("删除本图",RemovePage));navigation.Controls.Add(B("前移本图",()=>MovePage(-1)));navigation.Controls.Add(B("后移本图",()=>MovePage(1)));UpdatePageCount();
  }
  void ClearOriginals(){foreach(var value in originalPages)value.ReleaseOriginal();originalPages.Clear();}
  void KeepOriginal(PhotoData full){var cached=full.CacheOriginal();var old=originalPages[selectedPage];originalPages[selectedPage]=cached;old.ReleaseOriginal();}
  void UpdatePageCount(){pageCount.Text=regionPayload!=null?"组合图"+(selectedPage+1)+"/"+pages.Count+" · "+regionPayload.SourcePageCount+"源页":pages.Count==0?"本题0/6张":"第"+(selectedPage+1)+"/"+pages.Count+"张";}
  void RefreshPageView(){
   if(pages.Count==0){photo=null;picture.SetImage(null);sourceLabel.Text="等待题目图片 · 可拍照框选或直接输入文字";}
   else{selectedPage=Math.Max(0,Math.Min(selectedPage,pages.Count-1));photo=pages[selectedPage];picture.SetImage(Raster.ToBitmap(photo.Image));picture.ResetView();sourceLabel.Text="本题共"+pages.Count+(regionPayload!=null?"张组合图":"张")+" · 当前"+(selectedPage+1)+" · "+photo.Image.PixelWidth+"×"+photo.Image.PixelHeight+" · "+photo.Jpeg.Length/1024+"KB";}
   UpdatePageCount();Buttons();
  }
  void ResetForChangedImages(){Stop("");ResetPractice();SetAnswerOnly(false);answer="";dialogue.Clear();DeleteVoice();lesson.Display(pages.Count==0?"请拍照框选题目，或在下方输入问题。":"已收集本题"+pages.Count+"张图片。\n可继续添加跨页图片，收集完整后点“讲解本题”。",false);lastUse=DateTime.UtcNow;}
  void AcceptPage(PhotoData value,bool append){
   if(regionWorkspace!=null)ClearRegions();regionPayload=null;
   if(append&&pages.Count>=6)throw new InvalidOperationException("本题已收集6张，请删除不相关图片或开启新对话。");
   if(!append){pages.Clear();ClearOriginals();}pages.Add(value);originalPages.Add(value);selectedPage=pages.Count-1;ResetForChangedImages();RefreshPageView();status.Text=multiPage.Checked?"已收集"+pages.Count+"张，翻页后可继续拍；最后点“讲解本题”。":"题目选区已就绪。";
  }
  void ClearPages(){pages.Clear();ClearOriginals();selectedPage=0;photo=null;UpdatePageCount();}
  void SelectPage(int delta){if(pages.Count==0)return;CloseCamera();selectedPage=Math.Max(0,Math.Min(pages.Count-1,selectedPage+delta));RefreshPageView();if(work==null)status.Text="查看本题第"+(selectedPage+1)+"张；解答会同时读取全部"+pages.Count+"张。";}
  void MovePage(int delta){if(regionPayload!=null){status.Text="多区域题请在区域篮调整顺序，避免改变题目身份。";return;}int next=selectedPage+delta;if(next<0||next>=pages.Count)return;CloseCamera();var current=pages[selectedPage];var original=originalPages[selectedPage];pages[selectedPage]=pages[next];originalPages[selectedPage]=originalPages[next];pages[next]=current;originalPages[next]=original;selectedPage=next;ResetForChangedImages();RefreshPageView();status.Text="图片顺序已调整，将按当前顺序解题。";}
  void RemovePage(){if(regionPayload!=null){status.Text="多区域题请在区域篮删除区域或原始页，再预览确认。";return;}if(pages.Count==0)return;CloseCamera();pages.RemoveAt(selectedPage);originalPages[selectedPage].ReleaseOriginal();originalPages.RemoveAt(selectedPage);selectedPage=Math.Max(0,Math.Min(selectedPage,pages.Count-1));ResetForChangedImages();RefreshPageView();status.Text="已删除这张图片，剩余"+pages.Count+"张；可补拍或重新讲解。";}
  async Task CropCurrent(){await EditRegions(null);}
  void RestoreFullPage(){if(regionPayload!=null){status.Text="原始页始终保留。请打开“重新选题／继续补拍”，重选区域或明确将整页加入本题。";return;}if(pages.Count==0)return;CloseCamera();pages[selectedPage]=PhotoData.Create(originalPages[selectedPage].Image,"已恢复整图",high.Checked);ResetForChangedImages();RefreshPageView();status.Text="已恢复本张原始整图，可重新框选。";}
 }
}
