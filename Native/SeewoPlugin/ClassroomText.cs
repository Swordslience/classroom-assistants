using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Globalization;
using System.Text;
using WpfMath.Controls;

namespace ClassroomAI.Seewo;

// Native WPF rendering: no embedded browser, no remote fonts, no GPU inference.
internal sealed class ClassroomText : ScrollViewer
{
    readonly StackPanel rows = new() { Margin = new Thickness(14) };
    readonly Dictionary<string,System.Windows.Media.Imaging.BitmapSource> glyphs=new();
    string text = "";
    public bool IsReceiving {get;set;}
    public string ViewId {get;private set;}=Guid.NewGuid().ToString("N");
    public double BodySize { get; set; } = 32; // WPF DIP; 32 DIP = 24 pt.
    public string Text { get => text; set { var next=value??"";if(next!=text&&(text.Length==0||!next.StartsWith(text,StringComparison.Ordinal)))ViewId=Guid.NewGuid().ToString("N");text=next;double offset=VerticalOffset;Render();if(text.Length>0)ScrollToVerticalOffset(offset); if(text.Length==0)ScrollToTop(); } }
    public ClassroomText() { Content = rows; VerticalScrollBarVisibility = ScrollBarVisibility.Visible; HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; PanningMode=PanningMode.VerticalOnly; CanContentScroll=false; Focusable=true; }
    public void RefreshSize(double size) { BodySize = System.Math.Clamp(size, 32, 44); Render(); }
    void Render()
    {
        rows.Children.Clear();
        if(IsReceiving){rows.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=BodySize,Foreground=Brushes.Black});return;}
        string normalized = text.Replace(@"\[", "\n$$").Replace(@"\]", "$$\n");
        bool formula = false;
        string formulaText = "";
        foreach (string line in normalized.Split('\n'))
        {
            string s = line.Trim();
            if (s.StartsWith("$$")) {
                if (s.Length > 4 && s.EndsWith("$$")) { rows.Children.Add(Math(s[2..^2])); continue; }
                if (formula) { rows.Children.Add(Math(formulaText)); formulaText = ""; formula = false; }
                else { formula = true; formulaText = s[2..]; }
                continue;
            }
            if (formula) { formulaText += " " + s; continue; }
            if (!s.Contains("$")&&!Regex.IsMatch(s,@"[\u4e00-\u9fff]")&&Regex.IsMatch(s,@"\\(?:sqrt|frac|vec|overrightarrow|quad)")){rows.Children.Add(Math(s));continue;}
            if (Regex.IsMatch(s, @"^\|?[\s:\-|]+\|[\s:\-|]*$")) continue;
            if (s.Length == 0) { rows.Children.Add(new Border { Height = 12 }); continue; }
            var heading = Regex.Match(s, @"^#{1,6}\s+");
            if (heading.Success) s = s[heading.Length..];
            // Simple Markdown table rows retain all cells; no tiny table fonts.
            if (s.StartsWith("|") && s.EndsWith("|")) s = string.Join("   ·   ", s.Trim('|').Split('|').Select(c => c.Trim()));
            var wrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 8) };
            foreach (string part in Regex.Split(s, @"(\$[^$\r\n]+\$|\\\(.+?\\\))")) {
                if ((part.StartsWith("$") && part.EndsWith("$")) || (part.StartsWith(@"\(") && part.EndsWith(@"\)"))) {
                    wrap.Children.Add(Math(part.StartsWith("$") ? part[1..^1] : part[2..^2])); continue;
                }
                var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = BodySize + (heading.Success ? 2 : 0), Foreground = Brushes.Black, MaxWidth = 1200 };
                foreach (string t in Regex.Split(part, @"(\*\*.+?\*\*)")) {
                    if (t.StartsWith("**") && t.EndsWith("**") && t.Length > 4) {var bold=new Bold{Foreground=Brushes.DarkSlateBlue};AppendText(bold.Inlines,t[2..^2]);block.Inlines.Add(bold);}
                    else AppendText(block.Inlines,t);
                }
                if (heading.Success) block.FontWeight = FontWeights.Bold;
                block.SetBinding(FrameworkElement.MaxWidthProperty, new System.Windows.Data.Binding("ActualWidth") { Source = this, Converter = new ContentWidth() });
                wrap.Children.Add(block);
            }
            rows.Children.Add(wrap);
        }
        if (formula && formulaText.Length > 0) rows.Children.Add(Math(formulaText));
    }
    void AppendText(InlineCollection output,string value){var elements=StringInfo.GetTextElementEnumerator(value);var plain=new StringBuilder();void Flush(){if(plain.Length==0)return;output.Add(new Run(plain.ToString()));plain.Clear();}while(elements.MoveNext()){string part=elements.GetTextElement();bool math=false;for(int i=0;i<part.Length;i++){if(part[i]>='\u20d0'&&part[i]<='\u20ff')math=true;else if(char.IsHighSurrogate(part[i])&&i+1<part.Length&&char.IsLowSurrogate(part[i+1])){int code=char.ConvertToUtf32(part,i);if(code>=0x1d400&&code<=0x1d7ff)math=true;}}if(!math){plain.Append(part);continue;}Flush();var source=Glyph(part);output.Add(new InlineUIContainer(new Image{Source=source,Width=source.PixelWidth,Height=source.PixelHeight}){BaselineAlignment=BaselineAlignment.Center});}Flush();}
    System.Windows.Media.Imaging.BitmapSource Glyph(string text){string key=text+"|"+BodySize;if(glyphs.TryGetValue(key,out var cached))return cached;var formatted=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Cambria Math"),BodySize,Brushes.Black,1);var geometry=formatted.BuildGeometry(new Point());var bounds=geometry.Bounds;var visual=new DrawingVisual();using(var draw=visual.RenderOpen()){draw.PushTransform(new TranslateTransform(2-bounds.X,2-bounds.Y));draw.DrawGeometry(Brushes.Black,null,geometry);}var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(System.Math.Max(1,(int)System.Math.Ceiling(bounds.Width)+4),System.Math.Max(1,(int)System.Math.Ceiling(bounds.Height)+4),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();if(glyphs.Count>=64)glyphs.Clear();glyphs[key]=bitmap;return bitmap;}
    FrameworkElement Math(string expression)
    {
        if(IsReceiving){int depth=0;for(int i=0;i<expression.Length;i++){if(expression[i]=='\\'){i++;continue;}if(expression[i]=='{')depth++;else if(expression[i]=='}')depth--;}if(depth!=0||expression.EndsWith("\\"))return new TextBlock{Text="公式接收中…",FontSize=BodySize};}
        if(ScienceSupport.CloudMath.Prefer(expression))return CloudFormula(expression);
        if(Regex.IsMatch(expression,@"\\begin\{(?:aligned|align\*?|gathered|gather\*?|split)\}")){var group=new StackPanel();string parts=Regex.Replace(expression,@"\\(?:begin|end)\{(?:aligned|align\*?|gathered|gather\*?|split)\}","");foreach(string row in Regex.Split(parts,@"\\\\(?:\[[^\]]*\])?"))if(!string.IsNullOrWhiteSpace(row))group.Children.Add(Math(row.Replace("&","")));return group;}
        if(ScienceSupport.ScientificNotation.TryChemistry(expression,out var chemistry))return new TextBlock{Text=chemistry,FontSize=BodySize,TextWrapping=TextWrapping.Wrap,Foreground=chemistry.StartsWith("公式待核对")?Brushes.DarkRed:Brushes.Black};
        expression=ScienceSupport.ScientificNotation.Normalize(expression);
        try {
            // Parsing eagerly catches unsupported LaTeX (e.g. mhchem) before layout.
            WpfMath.Parsers.WpfTeXFormulaParser.Instance.Parse(expression);
            var formula=new FormulaControl { Formula = expression, Scale = BodySize, Margin = new Thickness(3, 3, 3, 8) };
            var scroll=new ScrollViewer {Content=formula,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.HorizontalOnly};
            scroll.SetBinding(FrameworkElement.MaxWidthProperty,new System.Windows.Data.Binding("ActualWidth"){Source=this,Converter=new ContentWidth()});return scroll;
        } catch {
            return CloudFormula(expression);
        }
    }
    FrameworkElement CloudFormula(string expression){var box=new StackPanel();var label=new TextBlock{Text="复杂公式排版中…",FontSize=BodySize,TextWrapping=TextWrapping.Wrap};box.Children.Add(label);var scroll=new ScrollViewer{Content=box,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};scroll.SetBinding(FrameworkElement.MaxWidthProperty,new System.Windows.Data.Binding("ActualWidth"){Source=this,Converter=new ContentWidth()});_ = FillFormula(box,label,expression);return scroll;}
    async Task FillFormula(StackPanel box,TextBlock label,string expression){try{var bytes=await ScienceSupport.CloudMath.Render(expression,BodySize*1.4);var image=new System.Windows.Media.Imaging.BitmapImage();using(var stream=new System.IO.MemoryStream(bytes)){image.BeginInit();image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();}box.Children.Clear();box.Children.Add(new Image{Source=image,Width=image.PixelWidth/1.4,Height=image.PixelHeight/1.4,HorizontalAlignment=HorizontalAlignment.Left,ToolTip=expression});}catch{label.Text="公式需核对（原式保留）："+expression;label.Foreground=Brushes.DarkRed;}}
    sealed class ContentWidth : System.Windows.Data.IValueConverter {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) => System.Math.Max(100, (double)v - 45);
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) => throw new NotSupportedException();
    }
}
