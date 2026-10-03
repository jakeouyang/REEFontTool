using System.Windows;
namespace ReeFont;
public partial class App : Application
{
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  if(e.Args.Length > 0 && e.Args[0] == "--self-test") {
   try { SelfTest.Run(e.Args.Skip(1).ToArray()); Shutdown(0); }
   catch(Exception ex) { System.IO.File.WriteAllText("self-test-error.txt", ex.ToString()); Shutdown(1); }
   return;
  }
  new MainWindow().Show();
 }
}
