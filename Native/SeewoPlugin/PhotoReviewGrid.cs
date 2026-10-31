using System.Windows;
using System.Windows.Controls;

namespace ClassroomAI.Seewo;
internal sealed class PhotoReviewGrid:Grid {
 readonly UIElement picture;readonly GridSplitter splitter;
 GridLength imageWidth=new(1,GridUnitType.Star),answerWidth=new(1,GridUnitType.Star);
 bool shown=true;
 public PhotoReviewGrid(UIElement picture,UIElement answer){
  this.picture=picture;
  ColumnDefinitions.Add(new ColumnDefinition{Width=imageWidth});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(6)});ColumnDefinitions.Add(new ColumnDefinition{Width=answerWidth});
  Children.Add(picture);splitter=new GridSplitter{Width=6,HorizontalAlignment=HorizontalAlignment.Stretch};SetColumn(splitter,1);Children.Add(splitter);SetColumn(answer,2);Children.Add(answer);
 }
 public void ShowPicture(bool visible){
  if(shown==visible)return;
  if(!visible){imageWidth=ColumnDefinitions[0].Width;answerWidth=ColumnDefinitions[2].Width;}
  picture.Visibility=splitter.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
  ColumnDefinitions[0].Width=visible?imageWidth:new GridLength(0);ColumnDefinitions[1].Width=new GridLength(visible?6:0);ColumnDefinitions[2].Width=visible?answerWidth:new GridLength(1,GridUnitType.Star);shown=visible;
 }
}
