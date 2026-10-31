using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cvte.EasiNote;
using Cvte.Paint.Framework;
using Cvte.Paint.Features.Elements;
using Cvte.Paint.Features.Elements.Texts;
using ClassroomAiBridge;

namespace ClassroomAI.Seewo;

internal sealed record PageSnapshot(CurrentPage Page, string Text, string ImagePath);

internal sealed class CurrentPage
{
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    public IEasiNoteBoardService Api { get; }
    public Slide Slide { get; }
    public Window Host { get; }
    public bool IsDisplay { get; }
    public int Index { get; }
    public string Identity => Process.GetCurrentProcess().Id + ":" + IsDisplay + ":" + Slide.Id;
    public string Description => (IsDisplay ? "授课" : "备课") + "第" + (Index + 1) + "/" + Api.SlideCount + "页";

    CurrentPage(IEasiNoteBoardService api, bool display, Window host)
    {
        Api = api; Slide = api.CurrentSlide; Host = host; IsDisplay = display; Index = api.CurrentSlideIndex;
    }

    static CurrentPage? Visible(IEasiNoteBoardService api, bool display)
    {
        // IsPreLoaded remains true even after a real document is opened in this host version.
        // Native visibility and document-window identity are the useful checks instead.
        if (!api.IsLoaded || api.CurrentSlide is null || api.Board is null || !api.Board.IsVisible) return null;
        var host = Window.GetWindow(api.Board);
        if (host is null || !host.IsVisible || host.WindowState == WindowState.Minimized ||
            PresentationSource.FromVisual(api.Board) is null || !IsWindowVisible(new WindowInteropHelper(host).Handle) ||
            (!display && string.IsNullOrWhiteSpace(host.Title))) return null;
        return new CurrentPage(api, display, host);
    }

    public static CurrentPage Resolve()
    {
        Application.Current.Dispatcher.VerifyAccess();
        // A rendered lecture board wins. A preloaded or hidden editor is never a fallback.
        var display = Visible(EN.DisplayingBoardApi, true);
        if (display is not null) return display;
        if (EN.App.CurrentMode == AppMode.Display)
            throw new InvalidOperationException("授课画板尚未就绪；未向备课页写入。请在当前课件重新打开助教。");
        return Visible(EN.EditingBoardApi, false) ??
            throw new InvalidOperationException("当前助教没有绑定可见课件。请从正在显示的课件打开豆包助教。");
    }

    public bool IsStillCurrent()
    {
        try { return Resolve().Identity == Identity && ReferenceEquals(Api.CurrentSlide, Slide); }
        catch (InvalidOperationException) { return false; }
    }

    public PageSnapshot Capture()
    {
        if (!IsStillCurrent()) throw new InvalidOperationException("页面已切换，请重新读取。");
        Slide.UpdateLayout();
        double width = Slide.ActualWidth, height = Slide.ActualHeight;
        if (width < 1 || height < 1) throw new InvalidOperationException("当前页还没有完成显示，请稍后再试。");
        var text = new StringBuilder();
        void Walk(System.Collections.Generic.IEnumerable<Element> elements)
        {
            foreach (var element in elements)
            {
                if (!element.IsVisible || element.Opacity < .01) continue;
                if (element is TextElement t)
                {
                    var value = t.TextEditor.Text;
                    if (!string.IsNullOrWhiteSpace(value)) text.AppendLine(value);
                }
                if (element is GroupElement group) Walk(group.Elements);
            }
        }
        Walk(Slide.Elements);
        // Render the slide visual itself, excluding assistant windows, taskbar and desktop.
        double scale = Math.Min(1, 1280 / width);
        int pw = Math.Max(1, (int)Math.Ceiling(width * scale));
        int ph = Math.Max(1, (int)Math.Ceiling(height * scale));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pw, ph));
            dc.DrawRectangle(new VisualBrush(Slide) { Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, width, height) },
                null, new Rect(0, 0, pw, ph));
        }
        var bitmap = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze();
        string folder = RuntimeFiles.Snapshots; RuntimeFiles.Clean();
        string path = Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
        using (var stream = File.Create(path))
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
        }
        return new PageSnapshot(this, text.ToString(), path);
    }
}
