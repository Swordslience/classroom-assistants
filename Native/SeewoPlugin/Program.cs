using System;
using System.IO;
using System.Threading.Tasks;
using Cvte.Composition;
using Cvte.EasiNote;
using System.Windows;
using System.Windows.Media;
using Cvte.Windows.Input;
using Cvte.Windows.Localization;
using System.Globalization;
using System.Collections.Generic;

namespace ClassroomAI.Seewo;
internal sealed class Program : dotnetCampus.EasiPlugins.EasiPlugin
{
    protected override async Task OnRunningAsync()
    {
        if (EN.CommandOptions.IsCloud) return;
        try
        {
            var manager = await Container.Current.GetAsync<IUIItemManager>().ConfigureAwait(false);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Lang.Sources.Add(new DictionaryLanguageSource {
                    [new CultureInfo("zh-CHS")] = new Dictionary<string,string> {
                        ["Lang.HeadToolBar.ClassroomAI20261024"]="奶龙助教",
                        ["Lang.ToolTip.Insert.ClassroomAI20261024.Title"]="奶龙助教",
                        ["Lang.ToolTip.Insert.ClassroomAI20261024.Text"]="AI生图与语音问答",
                        ["Lang.BoardEditContextMenu.ClassroomAI20261024"]="奶龙课堂助教"
                    }
                });
                Application.Current.Resources["Image.ToolBar.ClassroomAI20261024.TabUI"]=Brand.Icon();
                manager.Append(_=>new AiToolbarItem(),new UIItemAttribute(UIItemPurposes.HeadToolBar));
                manager.Append(_=>new AiBoardMenuItem(),new UIItemAttribute(UIItemPurposes.BoardEditMenu));
                var launcher=new Window { Title="奶龙课堂助教",Icon=Brand.Icon(),Width=154,Height=64,WindowStyle=WindowStyle.ToolWindow,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Left=SystemParameters.WorkArea.Right-172,Top=SystemParameters.WorkArea.Bottom-156 };
                var launchButton=new System.Windows.Controls.Button {Content="龙 · 奶龙助教",Background=Brushes.Gold,FontSize=18};launchButton.Click+=(_,_)=>AiWindow.Open();launcher.Content=launchButton;
                // Preloaded Shell processes used to expose ownerless topmost launchers.
                // Only show an entry owned by a real, visible board in this process.
                var watch = new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromMilliseconds(1500) };
                watch.Tick+=(_,_)=>{
                    try {
                        var page=CurrentPage.Resolve();
                        bool ownerChanged=launcher.Owner!=page.Host;if(ownerChanged) launcher.Owner=page.Host;
                        launcher.Topmost=page.IsDisplay;
                        bool first=!launcher.IsVisible;if(first) launcher.Show();LauncherPlacement.EnsureVisible(launcher,page.Host,first||ownerChanged);
                    } catch(InvalidOperationException) { if(launcher.IsVisible) launcher.Hide(); }
                    catch(Exception error) { System.Diagnostics.Trace.WriteLine(error); if(launcher.IsVisible) launcher.Hide(); }
                };
                watch.Start();
                RemoteClient.Start();
                Directory.CreateDirectory(ClassroomAiBridge.BudgetGate.Root);File.WriteAllText(Path.Combine(ClassroomAiBridge.BudgetGate.Root, "plugin-loaded.txt"), DateTime.Now.ToString("O")+" version="+typeof(Program).Assembly.GetName().Version);
            });
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(ClassroomAiBridge.BudgetGate.Root);File.WriteAllText(Path.Combine(ClassroomAiBridge.BudgetGate.Root, "plugin-error.txt"), ex.ToString());
        }
    }
}

internal sealed class AiToolbarItem : HeadToolBarItem {
    public AiToolbarItem(){ Key="HeadToolBar.ClassroomAI20261024";Type=UIItemTypes.Applications;ImageSourceKey="Image.ToolBar.ClassroomAI20261024.TabUI";ImageWidth=20;ImageHeight=20;SortHint=1001;Text="奶龙助教";Command=new DelegateCommand(()=>AiWindow.Open()); }
}
internal sealed class AiBoardMenuItem : BoardEditMenuItem {
    public AiBoardMenuItem(){Key="ClassroomAI20261024";Text="奶龙课堂助教";Predicate=_=>true;Command=new DelegateCommand(()=>AiWindow.Open());}
}
internal sealed class AiDisplayItem : DisplayFunctionToolBarItem {
    public AiDisplayItem(){Key="ClassroomAI20261024";Type=UIItemTypes.Applications;Text="奶龙助教";SortHint=1001;Image=Brand.Icon();Command=new DelegateCommand(()=>AiWindow.Open());}
}
