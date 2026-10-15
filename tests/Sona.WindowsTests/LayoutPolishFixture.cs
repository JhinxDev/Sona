using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sona.App;
using Sona.Core;

internal static class LayoutPolishFixture
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    public static object Run(string output)
    {
        output=Path.GetFullPath(output);if(Directory.Exists(output))throw new IOException("Fresh evidence directory required.");Directory.CreateDirectory(output);
        var prior=Environment.GetEnvironmentVariable("SONA_DATA");var profile=Path.Combine(output,"profile");
        new Preferences{Theme="amethyst",Model="turbo",SoundEffects=false,AutoInsert=false,KeepHistory=false}.Save(Path.Combine(profile,"preferences.json"));
        Environment.SetEnvironmentVariable("SONA_DATA",profile);
        var foreground=GetForegroundWindow();Exception? failure=null;var shots=new List<object>();var measurements=new List<object>();var checks=new List<string>();
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
        var thread=new Thread(()=>
        {
            var app=new ThemeResourceTestApplication();app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async()=>
            {
                MainWindow? window=null;
                try
                {
                    window=new MainWindow(["--test-ui"]){Width=1280,Height=1000,Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};window.Show();
                    T Field<T>(string name)=>(T)window.FindName(name);
                    async Task Layout(){await Task.Delay(100);window.UpdateLayout();}
                    void Capture(string name)
                    {
                        var root=(FrameworkElement)window.Content;var w=root.ActualWidth;var h=root.ActualHeight;var scale=2d;
                        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(w*scale),(int)Math.Ceiling(h*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(root);
                        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));var path=Path.Combine(output,name+".png");using(var file=File.Create(path))png.Save(file);
                        shots.Add(new{name,Width=w,Height=h,Scale=scale,SHA256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))});
                    }
                    Field<TabControl>("Pages").SelectedIndex=1;Field<RadioButton>("SettingsAudioNav").IsChecked=true;await Layout();
                    var device=Field<ComboBox>("Device");var section=Field<StackPanel>("SettingsAudioSection");
                    // Locate the actual visible label through the visual tree; no control geometry is overridden.
                    IEnumerable<DependencyObject> Descendants(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
                    var label=Descendants(section).OfType<TextBlock>().Single(x=>x.Text=="Microphone");
                    double Center(FrameworkElement element)=>element.TransformToAncestor(window).Transform(new Point(0,element.ActualHeight/2)).Y;
                    Check(Math.Abs(Center(label)-Center(device))<.5,"Microphone label aligns with its dropdown row");
                    measurements.Add(new{Page="audio",MicrophoneLabelCenter=Center(label),DropdownCenter=Center(device),Delta=Math.Abs(Center(label)-Center(device))});Capture("audio-typing");
                    window.Width=740;window.Height=530;await Layout();
                    Check(Math.Abs(Center(label)-Center(device))<.5,"Microphone alignment survives the minimum window");
                    Check(Field<ScrollViewer>("SettingsScroll").ScrollableWidth==0,"Compact audio settings do not scroll horizontally");
                    Field<TabControl>("Pages").SelectedIndex=3;
                    foreach(var size in new[]{(Name:"models-900",Width:1280d,Height:900d),(Name:"models-roomy",Width:1280d,Height:1100d),(Name:"models-compact",Width:740d,Height:530d)})
                    {
                        window.Width=size.Width;window.Height=size.Height;await Layout();var scroll=Field<ScrollViewer>("ModelsScroll");scroll.ScrollToTop();await Layout();
                        Check(scroll.ScrollableWidth==0,size.Name+" has no horizontal overflow");
                        Check(scroll.VerticalScrollBarVisibility==ScrollBarVisibility.Auto,"Models retains automatic vertical scrolling at "+size.Name);
                        Check(size.Name=="models-compact" ? scroll.ScrollableHeight>0&&scroll.ComputedVerticalScrollBarVisibility==Visibility.Visible : scroll.ScrollableHeight==0&&scroll.ComputedVerticalScrollBarVisibility==Visibility.Collapsed,"Scrollbar reflects real overflow at "+size.Name);
                        var children=((StackPanel)scroll.Content).Children.Cast<FrameworkElement>().Select(x=>new{x.Name,Type=x.GetType().Name,x.ActualHeight,Margin=x.Margin.ToString(),Visibility=x.Visibility.ToString(),Text=x is TextBlock text?text.Text:null}).ToArray();
                        measurements.Add(new{Page=size.Name,scroll.ExtentHeight,scroll.ViewportHeight,scroll.ScrollableHeight,scroll.ScrollableWidth,Bar=scroll.ComputedVerticalScrollBarVisibility.ToString(),children});Capture(size.Name);
                    }
                    var result=Field<TextBlock>("ModelResult");
                    Check(result.Visibility==Visibility.Collapsed,"Empty model feedback reserves no space");
                    result.Text="Download cancelled. Partial data was removed.";await Layout();
                    Check(result.IsVisible&&result.ActualHeight>0,"Nonempty model feedback becomes visible");
                    var compact=Field<ScrollViewer>("ModelsScroll");compact.ScrollToEnd();await Layout();
                    var resultBottom=result.TransformToAncestor(compact).Transform(new Point(0,result.ActualHeight)).Y;
                    Check(resultBottom<=compact.ActualHeight&&resultBottom>0,"Compact scrolling reaches the complete feedback row");Capture("models-compact-footer");
                    result.Text="";await Layout();Check(result.Visibility==Visibility.Collapsed,"Clearing feedback removes its space again");
                    Check(!Field<Button>("RecordButton").IsEnabled,"Fixture recording remains disabled");
                    Check(GetForegroundWindow()==foreground,"Native captures preserve the foreground application");
                }
                catch(Exception ex){failure=ex;}
                finally{window?.RequestQuit();app.Shutdown();}
            }));app.Run();
        }){IsBackground=true};
        try{thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!thread.Join(TimeSpan.FromSeconds(30)))throw new TimeoutException("Layout fixture timed out.");if(failure!=null)throw new Exception("Layout fixture failed",failure);}
        finally{Environment.SetEnvironmentVariable("SONA_DATA",prior);}
        var report=new{Passed=checks.Count,Checks=checks,Measurements=measurements,Shots=shots,AssemblySHA256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(App).Assembly.Location))),ProductionTouched=false,MicrophoneOpened=false,GlobalInputSent=false,ForegroundPreserved=GetForegroundWindow()==foreground};File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));return report;
    }
}
