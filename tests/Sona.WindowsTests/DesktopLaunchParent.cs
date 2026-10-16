using System.Diagnostics;
using System.Text.Json;
using Sona.App.Windows;

internal static class DesktopLaunchParent
{
    public static void Run(string root,string launcher,string route)
    {
        root=Path.GetFullPath(root);
        if(!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(root),"^sona-upgrade-rehearsal-[a-f0-9]{32}$")||!File.Exists(Path.Combine(root,".sona-rehearsal")))throw new IOException("Isolated root required.");
        var exe=Path.Combine(root,"Programs","Sona","0.3.0-aaaaaaaa","Sona.exe");
        BoundedProcess.ContainApplication();
        Process app;
        if(route=="direct")
        {
            var info=new ProcessStartInfo(exe){UseShellExecute=false};info.ArgumentList.Add("--test-ui");info.Environment["SONA_DATA"]=Path.Combine(root,"profile");info.Environment["MURMUR_DATA"]=Path.Combine(root,"profile");
            app=Process.Start(info)!;
        }
        else if(route=="explorer")
        {
            var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");
            var info=new ProcessStartInfo(shell){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-NoProfile","-NonInteractive","-File",Path.GetFullPath(launcher),"-FilePath",exe,"-RehearsalRoot",root,"-RecoveryPath",Directory.GetFiles(Path.Combine(root,"recovery"),"upgrade-*.dpapi").Single()})info.ArgumentList.Add(arg);
            info.Environment.Remove("PSModulePath"); // Let Windows PowerShell resolve its own built-in modules.
            // A tiny wrapper serializes the public acknowledgement, never the draft.
            using var child=Process.Start(info)!;
            var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();
            if(!child.WaitForExit(35000))throw new TimeoutException("Desktop parent deadline.");
            if(child.ExitCode!=0)throw new IOException("Desktop launch failed: "+error.GetAwaiter().GetResult());
            using var result=JsonDocument.Parse(output.GetAwaiter().GetResult());app=Process.GetProcessById(result.RootElement.GetProperty("PID").GetInt32());
        }
        else throw new ArgumentException("Unknown test route.");
        using(app)
        {
            var until=DateTime.UtcNow.AddSeconds(10);while(DateTime.UtcNow<until){app.Refresh();if(app.HasExited)throw new IOException("Fixture exited early.");if(app.MainWindowHandle!=0)break;Thread.Sleep(50);}
            if(app.MainWindowHandle==0)throw new TimeoutException("Isolated window not ready.");
            File.WriteAllText(Path.Combine(root,route+"-launch.json"),JsonSerializer.Serialize(new{PID=app.Id,Path=app.MainModule!.FileName,InLauncherJob=BoundedProcess.InApplicationJob(app.Handle)}));
        }
        // Exit closes this process's kill-on-close job, exactly as a terminal owner
        // shutdown would. The direct control dies; the Explorer descendant survives.
    }
}
