using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace ReeFont;
public partial class MainWindow : Window {
 bool zh,busy; Game? Selected=>Games.SelectedItem as Game;
 Game[] catalog=[]; bool filtering;
 string T(string en,string cn)=>zh?cn:en;
 public MainWindow(bool showWelcome=true) {
  InitializeComponent();
  SourceInitialized+=(_,_)=> {
   var handle=new WindowInteropHelper(this).Handle; int dark=1,black=0;
   HwndSource.FromHwnd(handle)?.AddHook(WindowBounds.Hook);
   DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
   DwmSetWindowAttribute(handle,34,ref black,sizeof(int));
  };
  var area=SystemParameters.WorkArea;
  Width=Math.Min(1100,area.Width*.88); Height=Math.Min(720,area.Height*.88);
  MinWidth=Math.Min(980,Width); MinHeight=Math.Min(560,Height);
  if(showWelcome) ContentRendered+=ShowWelcome;
  try { catalog=Engine.Catalog(Path.Combine(AppContext.BaseDirectory,"Projects")); Games.ItemsSource=catalog; Games.SelectedIndex=0; }
  catch(Exception ex) { Log(ex.Message); ReplaceButton.IsEnabled=false; }
  Log(T("Select the game directory and a TTF / OTF font.","请选择游戏根目录和 TTF / OTF 字体。"));
  Log(T("Restore only touches tracked files; backups are retained.","还原仅处理本工具记录匹配的资源，备份会保留。"));
 }
 void Log(string text) { LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}\n"); LogBox.ScrollToEnd(); }
 internal static bool Matches(Game game,string query) {
  string Normalize(string s)=>string.Concat(s.Where(char.IsLetterOrDigit)).ToLowerInvariant();
  var hay=Normalize(game.Name+game.Zh+game.Id+game.List);
  return query.Split(' ',StringSplitOptions.RemoveEmptyEntries).All(term=>hay.Contains(Normalize(term)));
 }
 void SearchChanged(object sender,System.Windows.Controls.TextChangedEventArgs e) {
  if(Games==null) return; var previous=Selected; var results=catalog.Where(g=>Matches(g,SearchBox.Text)).ToArray();
  filtering=true;
  try { Games.ItemsSource=results; Games.SelectedItem=results.Contains(previous)?previous:results.FirstOrDefault(); } finally { filtering=false; }
  if(Selected!=previous) GameChanged(Games,null!);
  NoResults.Visibility=results.Length==0?Visibility.Visible:Visibility.Collapsed;
  Games.Visibility=results.Length==0?Visibility.Hidden:Visibility.Visible;
  ExportButton.IsEnabled=ReplaceButton.IsEnabled=RestoreButton.IsEnabled=BrowseDirectory.IsEnabled=results.Length>0;
 }
 void Bilibili(object sender,RoutedEventArgs e) { try { Process.Start(new ProcessStartInfo("https://space.bilibili.com/8480063"){UseShellExecute=true}); } catch(Exception ex) { Log(ex.Message); } }
 void GameChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e) {
  if(DirectoryBox==null||filtering) return; DirectoryBox.Clear(); FontBox.Clear();
  foreach(var (box,key) in new[]{(SC,"sc"),(TC,"tc"),(EN,"en")}) { box.IsEnabled=Selected?.Fonts.ContainsKey(key)==true; box.IsChecked=key=="sc"&&box.IsEnabled; box.Visibility=box.IsEnabled?Visibility.Visible:Visibility.Hidden; }
  if(Selected!=null) Log(T("Selected: ","已选择：")+Selected.Name);
 }
 void SelectDirectory(object sender,RoutedEventArgs e) {
  if(Selected==null) return; var dlg=new OpenFolderDialog();
  if(dlg.ShowDialog()==true) try { Engine.ValidateDirectory(Selected,dlg.FolderName); DirectoryBox.Text=dlg.FolderName; Log(T("Game directory verified.","游戏目录校验通过。")); Log(FrameworkSupport.Inspect(dlg.FolderName).Message(zh)); } catch(Exception ex) { Log(ex.Message); }
 }
 void SelectFont(object sender,RoutedEventArgs e) {
  var dlg=new OpenFileDialog{Filter="Font (*.ttf;*.otf)|*.ttf;*.otf"};
  if(dlg.ShowDialog()==true) try { Engine.ReadFont(dlg.FileName); FontBox.Text=dlg.FileName; Log(T("Font verified. Glyph coverage depends on your font.","字体校验通过；字符覆盖取决于所选字库。")); } catch(Exception ex) { Log(ex.Message); }
 }
 async void Replace(object sender,RoutedEventArgs e)=>await Run(false);
 async void Restore(object sender,RoutedEventArgs e)=>await Run(true);
 async void ExportMod(object sender,RoutedEventArgs e) {
  if(busy||Selected==null) return;
  var game=Selected; var font=FontBox.Text;
  var languages=new[]{(SC,"sc"),(TC,"tc"),(EN,"en")}.Where(x=>x.Item1.IsChecked==true&&x.Item1.IsEnabled).Select(x=>x.Item2).ToArray();
  try {
   Engine.ReadFont(font); if(languages.Length==0) throw new IOException(T("Select a language.","请选择语言。"));
   var dialog=new SaveFileDialog{Filter="ZIP (*.zip)|*.zip",DefaultExt=".zip",AddExtension=true,OverwritePrompt=true,FileName=ModExporter.FileName(game,font,languages)};
   if(dialog.ShowDialog()!=true) return;
   var preview=ModExporter.Preview(game,font,languages);
   busy=true; ExportButton.IsEnabled=SearchBox.IsEnabled=Games.IsEnabled=BrowseDirectory.IsEnabled=BrowseFont.IsEnabled=ReplaceButton.IsEnabled=RestoreButton.IsEnabled=false;
   Status.Text=T("Exporting…","正在导出…");
   await Task.Run(()=>ModExporter.Export(game,font,languages,dialog.FileName,preview));
   Log(T("Fluffy Mod Manager ZIP exported: ","Fluffy Mod Manager ZIP 已导出：")+dialog.FileName); Status.Text=T("Ready","就绪");
  } catch(Exception ex) { Log(ex.Message); Status.Text=T("Action required","需要处理"); }
  finally { busy=false; ExportButton.IsEnabled=SearchBox.IsEnabled=Games.IsEnabled=BrowseDirectory.IsEnabled=BrowseFont.IsEnabled=ReplaceButton.IsEnabled=RestoreButton.IsEnabled=Selected!=null; }
 }
 async Task Run(bool restore) {
  if(busy||Selected==null) return; var game=Selected; var root=DirectoryBox.Text; var font=FontBox.Text;
  var languages=new[]{(SC,"sc"),(TC,"tc"),(EN,"en")}.Where(x=>x.Item1.IsChecked==true&&x.Item1.IsEnabled).Select(x=>x.Item2).ToArray();
  busy=true; ExportButton.IsEnabled=SearchBox.IsEnabled=Games.IsEnabled=BrowseDirectory.IsEnabled=BrowseFont.IsEnabled=ReplaceButton.IsEnabled=RestoreButton.IsEnabled=false; Status.Text=T("Working…","处理中…");
  try { await Task.Run(()=>{ Action<string> log=s=>Dispatcher.Invoke(()=>Log(s)); if(restore) Engine.Restore(game,root,log); else Engine.Apply(game,root,font,languages,log); }); Status.Text=T("Ready","就绪"); Log(restore?T("Restored. Backups retained.","还原完成，备份已保留。"):T("Font files installed. Start the game to verify appearance.","字体文件已安装，请启动游戏验证显示效果。")); }
  catch(Exception ex) { Status.Text=T("Action required","需要处理"); Log(ex.Message); }
  finally { busy=false; ExportButton.IsEnabled=SearchBox.IsEnabled=Games.IsEnabled=BrowseDirectory.IsEnabled=BrowseFont.IsEnabled=ReplaceButton.IsEnabled=RestoreButton.IsEnabled=true; }
 }
 void ToggleLanguage(object sender,RoutedEventArgs e) {
  ExportButton.Content=zh?"Export ZIP":"导出 ZIP";
  SearchBox.Tag=zh?"Search games…":"搜索游戏名称或缩写…"; DirectoryBox.Tag=zh?"Select the game folder…":"请选择游戏根目录…"; FontBox.Tag=zh?"Select a TTF / OTF font…":"请选择 TTF / OTF 字体…"; LogBox.Tag=zh?"Replacement and restore activity…":"显示替换与还原记录…"; NoResults.Text=zh?"No matching games":"未找到匹配游戏";
  zh=!zh; LanguageButton.Content=zh?"English":"中文"; DirectoryLabel.Text=T("Game Directory :","游戏目录："); FontLabel.Text=T("Font File:","字体文件："); BrowseDirectory.Content=BrowseFont.Content=T("Browse...","浏览…"); SC.Content=T("Simplified Chinese","简体中文"); TC.Content=T("Traditional Chinese","繁体中文"); EN.Content=T("English","英文"); ReplaceButton.Content=T("Replace","替换"); RestoreButton.Content=T("Restore","还原"); Status.Text=T("Ready","就绪");
 }

 void ShowWelcome(object? sender,EventArgs e) {
  ContentRendered-=ShowWelcome;
  if(!FrameworkSupport.WasSeen(FrameworkSupport.SeenPath)) ShowFrameworkHelp();
 }
 void ShowFrameworkHelp() {
  FrameworkSupport.Dialog(this,zh).ShowDialog();
  try { FrameworkSupport.MarkSeen(FrameworkSupport.SeenPath); } catch(Exception ex) { Log(T("Could not save first-run setting: ","无法保存首次启动设置：")+ex.Message); }
 }
 void FrameworkHelp(object sender,RoutedEventArgs e)=>ShowFrameworkHelp();
 void Drag(object sender,MouseButtonEventArgs e) { if(e.OriginalSource is System.Windows.Controls.TextBlock||e.OriginalSource is System.Windows.Controls.Grid) DragMove(); }
 void Minimize(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
 void Maximize(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
 void Exit(object sender,RoutedEventArgs e) { if(!busy) Close(); }
 protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { if(busy) e.Cancel=true; base.OnClosing(e); }
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
}


