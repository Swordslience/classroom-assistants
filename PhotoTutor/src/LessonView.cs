using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfMath;
using WpfMath.Rendering;
using XamlMath;

namespace PhotoTutor {
 internal sealed class LessonView:ScrollViewer {
  readonly StackPanel lines=new StackPanel{Margin=new Thickness(14)};
  public double BodySize=32;
  public LessonView(){Content=lines;VerticalScrollBarVisibility=ScrollBarVisibility.Auto;HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;Background=Brushes.White;}
  public void Display(string text,bool formulas) {
   lines.Children.Clear();
   text=text.Replace(@"\[","\n$$").Replace(@"\]","$$\n").Replace(@"\(","$").Replace(@"\)","$");
   bool multiline=false;string expression="";
   foreach(string original in text.Split('\n')) {
    string line=original.Trim();
    if(line.Length==0)continue;
    if(formulas&&line.StartsWith("$$")) {
     if(line.Length>4&&line.EndsWith("$$")){lines.Children.Add(Formula(line.Substring(2,line.Length-4)));continue;}
     if(multiline){lines.Children.Add(Formula(expression));expression="";multiline=false;}else{expression=line.Substring(2);multiline=true;}continue;
    }
    if(multiline){expression+=" "+line;continue;}
    bool heading=Regex.IsMatch(line,@"^#{1,6}\s");line=Regex.Replace(line,@"^#{1,6}\s+","");
    var p=new TextBlock{TextWrapping=TextWrapping.Wrap,FontFamily=new FontFamily("Microsoft YaHei"),FontSize=BodySize,Margin=new Thickness(0,3,0,12),LineHeight=BodySize*1.5,FontWeight=heading?FontWeights.Bold:FontWeights.Normal};
    foreach(string part in Regex.Split(line,@"(\*\*[^*]+\*\*|\$[^$]+\$)")) {
     if(part.StartsWith("**")&&part.EndsWith("**")&&part.Length>4)p.Inlines.Add(new Bold(new Run(part.Substring(2,part.Length-4))){Foreground=new SolidColorBrush(Color.FromRgb(8,104,120))});
     else if(formulas&&part.StartsWith("$")&&part.EndsWith("$")&&part.Length>2)p.Inlines.Add(new InlineUIContainer(Formula(part.Substring(1,part.Length-2))){BaselineAlignment=BaselineAlignment.Center});
     else p.Inlines.Add(new Run(part));
    }
    lines.Children.Add(p);
   }
   if(expression.Length>0)lines.Children.Add(new TextBlock{Text=expression,FontSize=BodySize,TextWrapping=TextWrapping.Wrap});
  }
  FrameworkElement Formula(string value) {
   try {if(value.Length>800)throw new InvalidOperationException();var formula=WpfMath.Parsers.WpfTeXFormulaParser.Instance.Parse(value);var bitmap=formula.RenderToBitmap(WpfTeXEnvironment.Create(TexStyle.Display,BodySize,"Arial"));bitmap.Freeze();return new Image{Source=bitmap,Stretch=Stretch.None,Margin=new Thickness(3,1,3,5),HorizontalAlignment=HorizontalAlignment.Left};}
   catch{return new TextBlock{Text=value,FontSize=BodySize,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkRed};}
  }
 }
}
