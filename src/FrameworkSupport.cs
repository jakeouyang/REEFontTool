using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ReeFont;
public record FrameworkStatus(bool Dll, bool Config, bool? Loose) {
 public bool Ready => Dll && Config && Loose == true;
 public string Message(bool zh) => (Dll, Config, Loose) switch {
  (false, _, _) => zh ? "未检测到 dinput8.dll，请安装 REFramework。" : "No dinput8.dll found. Install REFramework.",
  (true, false, _) => zh ? "检测到 dinput8.dll，但没有 REFramework 配置；请启动游戏确认框架并开启离散加载。" : "dinput8.dll found, but no REFramework config. Launch the game and enable Loose File Loader.",
  (true, true, true) => zh ? "检测到 REFramework 安装痕迹，离散加载已开启（配置检查）。" : "REFramework installation detected; Loose File Loader enabled (configuration check).",
  (true, true, false) => zh ? "检测到 REFramework 安装痕迹，离散加载未开启。" : "REFramework installation detected; Loose File Loader disabled.",
  _ => zh ? "检测到 REFramework 安装痕迹，未找到有效的离散加载设置，请在游戏内开启。" : "REFramework installation detected; Loose File Loader setting unknown. Enable it in game."
 };
}
public static class FrameworkSupport {
 public const string Url="https://github.com/praydog/REFramework/releases";
 public static FrameworkStatus Inspect(string root) {
  bool dll=File.Exists(Engine.Safe(root,"dinput8.dll")); var path=Engine.Safe(root,"re2_fw_config.txt"); bool config=File.Exists(path); bool? loose=null;
  if(config) foreach(var line in File.ReadLines(path)) { var parts=line.Split('=',2); if(parts.Length==2&&parts[0].Trim().Equals("LooseFileLoader_Enabled",StringComparison.OrdinalIgnoreCase)) loose=bool.TryParse(parts[1].Trim(),out var value)?value:null; }
  return new(dll,config,loose);
 }
 public static string SeenPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"REE.Font.Tool","framework-help-seen-v1.txt");
 public static bool WasSeen(string path)=>File.Exists(path);
 public static void MarkSeen(string path) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,"seen"); }
 public static Window Dialog(Window owner,bool zh) {
  var panel=new StackPanel{Margin=new Thickness(28)};
  var dialog=new Window{Title=zh?"字体替换前须知":"Before replacing fonts",Owner=owner,Width=610,SizeToContent=SizeToContent.Height,MaxHeight=SystemParameters.WorkArea.Height*.9,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=Brushes.Black,Foreground=new SolidColorBrush(Color.FromRgb(0,165,177)),FontFamily=new FontFamily("Segoe UI"),FontSize=16,Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Resources=owner.Resources};
  panel.Children.Add(new TextBlock{Text=zh?"需要 REFramework":"REFramework is required",FontSize=25,Margin=new Thickness(0,0,0,20)});
  panel.Children.Add(new TextBlock{Text=zh?"本工具通过 REFramework 的离散文件加载功能，让游戏读取替换字体。\n\n1. 从官方 GitHub 下载适配游戏的 REFramework。\n2. 将 dinput8.dll 解压到游戏根目录（与游戏 EXE 同级）。\n3. 启动游戏，在 REFramework 菜单中开启 Loose File Loader → Enable Loose File Loader。\n4. 退出游戏，再使用本工具替换字体。\n\n选择游戏目录后，工具会检查 DLL 和离散加载配置；实际加载情况仍以游戏内为准。":"This tool uses REFramework’s Loose File Loader to load replacement fonts.\n\n1. Download a game-compatible REFramework from official GitHub.\n2. Extract dinput8.dll beside the game executable.\n3. Launch the game. In REFramework, enable Loose File Loader → Enable Loose File Loader.\n4. Exit the game, then replace the font with this tool.\n\nSelecting a game directory checks the DLL and loader configuration. Confirm actual loading in game.",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White});
  panel.Children.Add(new TextBlock{Text=Url,FontSize=13,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,18,0,12)});
  var download=new Button{Content=zh?"打开 GitHub 下载页":"Open GitHub downloads",Margin=new Thickness(0,0,0,12)};
  download.Click+=(_,_)=>{try{Process.Start(new ProcessStartInfo(Url){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(dialog,ex.Message);}}; panel.Children.Add(download);
  panel.Children.Add(new TextBlock{Text=zh?"此说明仅在首次启动时自动显示，之后可点击主窗口 REFramework 再次查看。":"Shown automatically once. Click REFramework in the main window to read this again.",TextWrapping=TextWrapping.Wrap,FontSize=13,Margin=new Thickness(0,0,0,16)});
  var close=new Button{Content=zh?"知道了":"Got it",IsCancel=true,IsDefault=true}; close.Click+=(_,_)=>dialog.Close(); panel.Children.Add(close);
  return dialog;
 }
}
