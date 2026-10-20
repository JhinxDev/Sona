using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NAudio.Wave;
using Sona.App;
using Sona.App.Audio;
using Sona.App.Windows;
using Sona.Core;

internal static class ResponsivenessFixture
{
    public static object Run(string output)
    {
        output=Path.GetFullPath(output); if(Directory.Exists(output)) throw new IOException("Use a fresh evidence directory."); Directory.CreateDirectory(output);
        var prior=Environment.GetEnvironmentVariable("SONA_DATA"); Environment.SetEnvironmentVariable("SONA_DATA",Path.Combine(output,"profile"));
        new Preferences { SoundEffects=false,AutoInsert=true,CopyToClipboard=true,PasteCompletedDraft=false,LiveTranscription=false }.Save(Path.Combine(output,"profile","preferences.json"));
        var checks=new List<string>(); var failures=new List<string>(); var measurements=new Dictionary<string,double>();
        void Check(bool value,string name){(value?checks:failures).Add(name);}
        var thread=new Thread(()=>{
            var app=new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async()=>{
                MainWindow? window=null;
                try {
                    var inserted=0;var copied=0;DraftTarget? destination=new DraftTarget(12345,Environment.ProcessId,[1,2,3],0);
                    using var insertion=new Insertion(()=>destination,()=>false,text=>{inserted++;return (uint)(text.Length*2);});
                    window=new MainWindow(["--test-ui"],insertion);window.ClipboardForTests=_=>copied++;
                    Check(window.OverlayForTests.SuppressNativeDisplayForTests,"All isolated test windows suppress native recording display by default");
                    var overlay=window.OverlayForTests; var label=(TextBlock)overlay.FindName("CapsuleLabel");
                    overlay.UpdateTarget("Known app"); overlay.UpdateAppIcon(ApplicationIconCache.DefaultIcon);
                    Check(overlay.FindName("CapsuleIconPlate") is Border{Visibility:Visibility.Collapsed} && overlay.FindName("PanelIconPlate") is Border{Visibility:Visibility.Collapsed},"Unavailable app logo hides both plates instead of drawing a square");
                    var actualIcon=new DrawingImage(new GeometryDrawing(Brushes.MediumPurple,null,new EllipseGeometry(new Point(8,8),7,7)));actualIcon.Freeze();
                    overlay.UpdateAppIcon(actualIcon);
                    Check(overlay.FindName("CapsuleIconPlate") is Border{Visibility:Visibility.Visible},"An available app logo appears without a permanent placeholder");
                    overlay.UpdateAppIcon(ApplicationIconCache.DefaultIcon);
                    Check(overlay.FindName("CapsuleIconColumn") is ColumnDefinition column && column.Width.Value==0,"Missing-logo spacing collapses with the placeholder");
                    var translucent=(Brush)overlay.FindResource("OverlaySurface");
                    Check(!SystemParameters.HighContrast && translucent is LinearGradientBrush tinted && tinted.GradientStops.All(x=>x.Color.A is >0 and <255) || SystemParameters.HighContrast,"Pill translucency is independent of the main-window glass theme");
                                        var clock=new Clock(); var input=new Capture(); window.RecorderForTests=()=>new Recorder(_=>input,clock);
                    var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    window.PreparationForTests=token=>gate.Task.WaitAsync(token);
                    var start=Stopwatch.StartNew(); var beginning=window.BeginForTestsAsync(); await Task.Delay(25);
                    measurements["PreparingObservedMs"]=start.Elapsed.TotalMilliseconds;
                    Check(!beginning.IsCompleted && !input.Started && label.Text=="Preparing", "Preparing pill is updated while protection is pending and microphone stays closed");
                    window.CancelForTests(); gate.TrySetResult(); Check(!await beginning && !input.Started,"Cancelling preparation never opens the microphone");
                    input=new Capture(); window.PreparationForTests=_=>Task.CompletedTask;
                    window.RecognitionForTests=(_,_)=>Task.FromResult("The complete long recording stays in this draft.");
                    Check(await window.BeginForTestsAsync(),"Intentional fixture begins only fake capture");
                    var pcm=new byte[64000]; for(var i=1;i<pcm.Length;i+=2)pcm[i]=8;
                    clock.Advance(121); input.Emit(pcm);
                    var tick=typeof(MainWindow).GetMethod("RecordingTick",BindingFlags.Instance|BindingFlags.NonPublic);
                    Check(tick!=null,"Recording uses one duration/health boundary"); tick?.Invoke(window,null);
                    Check(window.SessionForTests==SessionState.Recording,"Recording continues beyond the old two-minute cutoff");
                    overlay.Update("Listening","Esc to cancel",182,.02);
                    Check(((TextBlock)overlay.FindName("CapsuleTime")).Text=="3:02","Pill timer continues beyond two minutes");
                    Check(overlay.Width<=232,"Capsule without transcript is horizontally compact");
                    overlay.UpdateTranscript("A provisional sentence is still easy to read.","Provisional");
                    Check(overlay.Width>=300,"Transcript receives readable width when it appears");
                    overlay.UpdateTranscript("",""); Check(overlay.Width<=232,"Capsule contracts after transcript disappears");
                    CaptureElement((Border)overlay.FindName("Shell"),"capsule-recording.png",overlay.Width);
                    foreach(var theme in new[]{"amethyst","paper"}){
                        ThemeManager.Apply(theme); var tip=new ToolTip{Content="Listening. Esc to cancel",Style=overlay.TryFindResource(typeof(ToolTip)) as Style};
                        Check(tip.Style!=null,theme+" tooltip has explicit theme-aware styling"); tip.ApplyTemplate();
                        if(tip.Template?.FindName("TipSurface",tip) is Border surface) CaptureElement(surface,"tooltip-"+theme+".png",240);
                        else Check(false,theme+" tooltip has readable custom content");
                    }
                    ThemeManager.Apply("amethyst"); clock.Advance(180); input.Emit(pcm); tick?.Invoke(window,null);
                    for(var i=0;i<100 && window.SessionForTests==SessionState.Transcribing;i++) await Task.Delay(20);
                    Check(tick!=null && input.Disposed && window.SessionForTests==SessionState.Ready,"Five-minute limit releases capture and retains a ready draft");
                    Check(inserted==0 && copied==0,"Limit stop suppresses otherwise eligible insertion and clipboard copy");
                    Check(((TextBlock)window.FindName("StatusDetail")).Text.Contains("limit",StringComparison.OrdinalIgnoreCase),"Limit stop explains why the draft was retained");
                    input=new Capture();await window.BeginForTestsAsync();clock.Advance(301);input.Emit(pcm);
                    var priorInserted=inserted;var priorCopied=copied;await window.StopForTestsAsync();
                    Check(window.SessionForTests==SessionState.Ready && inserted==priorInserted && copied==priorCopied,"Manual stop after the recording limit is retain-only before the timer tick");
                                        var prefs=(Preferences)typeof(MainWindow).GetField("preferences",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
                    prefs.LiveTranscription=true; var calls=0;
                    window.RecognitionForTests=(_,_)=>{calls++;return Task.FromResult("A complete preview of the captured audio.");};
                    async Task WaitPreview(){for(var i=0;i<150 && calls==0;i++)await Task.Delay(20);Check(calls>0,"Live preview arrives from fake captured PCM");}
                    input=new Capture(); Check(await window.BeginForTestsAsync(),"Live fixture starts independent capture"); input.Emit(pcm); await WaitPreview();
                    var beforeStop=calls; await window.StopForTestsAsync();
                    Check(calls==beforeStop && window.SessionForTests==SessionState.Inserted && inserted==1 && copied==1,"An exact complete live preview avoids duplicate final recognition");
                    input=new Capture(); calls=0; Check(await window.BeginForTestsAsync(),"Tail-change fixture starts independent capture"); input.Emit(pcm); await WaitPreview();
                    input.Emit(pcm); beforeStop=calls; await window.StopForTestsAsync();
                    Check(calls==beforeStop+1,"Newly captured tail always receives final recognition");
                    var capInput=new Capture(); using(var bounded=new Recorder(_=>capInput)){
                        bounded.Start(-1); var longPcm=new byte[32000*300+64000];for(var i=1;i<longPcm.Length;i+=2)longPcm[i]=8;
                        capInput.Emit(longPcm); Check(bounded.LimitReached && bounded.CapturedBytes==32000*300,"Recorder keeps all five minutes and bounds an oversize final audio callback");
                        var longWav=await bounded.StopAsync();Check(SpeechAudio.Plan(longWav).Sum(x=>x.Bytes)==32000*300,"Bounded recording is a complete five-minute WAV for recognition");Array.Clear(longWav);Array.Clear(longPcm);
                    }
                                        // Exercise actual native Save and agent patch, not just the Core preference DTO.
                    var mode=(ComboBox)window.FindName("CleanupModeChoice");
                    mode.SelectedItem=mode.Items.Cast<ComboBoxItem>().Single(x=>(string)x.Tag=="Auto");
                    ((CheckBox)window.FindName("CleanupEnabled")).IsChecked=true;
                    typeof(MainWindow).GetMethod("Preferences_Click",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[window,new RoutedEventArgs()]);
                    await Task.Delay(40);
                    Check(prefs.CleanupMode=="Auto" && Preferences.Load(Path.Combine(output,"profile","preferences.json")).CleanupMode=="Auto","Native Save activates and persists Auto cleanup immediately");
                    var cleanupCalls=0;window.CleanupForTests=(text,_)=>{cleanupCalls++;return Task.FromResult(text);};
                    await (Task)typeof(MainWindow).GetMethod("PolishAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,["Please open the draft.",CancellationToken.None,null])!;
                    Check(cleanupCalls==0,"Saved Auto mode changes the live cleanup decision without restart");
                    typeof(MainWindow).GetMethod("RefreshCleanupChoices",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
                    var nextMode=prefs.CleanupMode=="Auto"?"Always":"Auto";
                    var reply=await (Task<AgentReply>)typeof(MainWindow).GetMethod("HandleAgentOnUiAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[new AgentRequest("settings.patch",JsonSerializer.SerializeToElement(new{cleanupMode=nextMode})),CancellationToken.None])!;
                    Check(reply.Ok && prefs.CleanupMode==nextMode && (string)((ComboBoxItem)mode.SelectedItem).Tag==nextMode && Preferences.Load(Path.Combine(output,"profile","preferences.json")).CleanupMode==nextMode,"Agent cleanup patch updates active value, selector and persisted preference together");
                                        async Task<AgentReply> Patch(object values)=>await (Task<AgentReply>)typeof(MainWindow).GetMethod("HandleAgentOnUiAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[new AgentRequest("settings.patch",JsonSerializer.SerializeToElement(values)),CancellationToken.None])!;
                    Check((await Patch(new{cleanupEnabled=false,copyToClipboard=false,copyOnInsertionFailure=true,enterToSend=true,liveTranscription=false,pasteCompletedDraft=true})).Ok,"Recovery and Enter preferences activate through validated app controls");
                    var paste=new FakeDelivery();insertion.PasteTransportForTests=paste;window.ClipboardSequenceForTests=()=>paste.Sequence;
                    window.ClipboardForTests=text=>{paste.Clipboard=text;paste.Sequence++;};
                    var recognition=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                    window.RecognitionForTests=(_,ct)=>recognition.Task.WaitAsync(ct);
                    input=new Capture(); Check(await window.BeginForTestsAsync(),"Quick cleanup fixture begins fake recording");input.Emit(pcm);
                    var quick=(Button)overlay.FindName("CapsuleCleanup");
                    var tone=(ComboBox)window.FindName("CleanupToneChoice");var savedTone=tone.SelectedItem;
                    tone.SelectedItem=tone.Items.Cast<ComboBoxItem>().Single(x=>(string)x.Tag=="casual");
                    quick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(prefs.CleanupEnabled && prefs.CleanupMode=="Auto" && quick.Content?.ToString()=="A","Pill button immediately selects Auto for the current recording");
                    Check((string)((ComboBoxItem)tone.SelectedItem).Tag=="casual" && Preferences.Load(Path.Combine(output,"profile","preferences.json")).CleanupTone=="formal","Quick mode preserves unrelated pending settings");
                    CaptureElement((Border)overlay.FindName("Shell"),"capsule-quick-auto.png",overlay.Width);
                    quick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(prefs.CleanupMode=="Always" && prefs.CleanupEnabled,"Pill button selects Always on its next click");
                    quick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!prefs.CleanupEnabled,"Pill button cycles back to Off");tone.SelectedItem=savedTone;
                    var pendingGesture=new HoldGesture();pendingGesture.Key(0xA2,true,0,toggle:true);var admittedGesture=pendingGesture.Key(0x5B,true,1,toggle:true);Check(admittedGesture.Action==HoldAction.Begin && pendingGesture.ConsumeToggle(admittedGesture.Generation),"Toggle setup consumes its real admission before pill Stop");pendingGesture.Key(0x5B,false,202,toggle:true);pendingGesture.Key(0xA2,false,203,toggle:true);
                    ((Button)overlay.FindName("CapsuleStop")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var stopping=(Task)typeof(MainWindow).GetField("operation",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;await Task.Delay(10);
                    var pendingIntent=(PendingSubmission)typeof(MainWindow).GetField("pendingSubmission",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
                    Check(pendingIntent.IsOpen && !quick.IsEnabled && paste.Pastes==0 && paste.Enters==0,"Pending recognition cannot paste early and disables cleanup changes");
                    var queuedGesture=pendingGesture.Key(13,true,204,finishOnEnter:true,completing:pendingIntent.IsOpen);
                    if(queuedGesture.Action==HoldAction.QueueSend)pendingIntent.TryRequest();else typeof(MainWindow).GetMethod("FinishAndSendShortcut",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
                    Check(queuedGesture.Action==HoldAction.QueueSend,"Enter after actual pill Stop uses the pending-result queue");
                    recognition.SetResult("The complete finished draft.");await stopping;
                    Check(paste.Pastes==1 && paste.Enters==1 && paste.Payload=="The complete finished draft." && paste.Clipboard=="fixture prior clipboard","Queued Enter waits for full text, sends once and restores previous clipboard");
                    Check(!pendingIntent.TryRequest(),"Late send intent cannot be attached after completion");
                    paste=new FakeDelivery();insertion.PasteTransportForTests=paste;paste.AfterPaste=()=>destination=destination! with{RuntimeId=[9]};
                    recognition=new(TaskCreationOptions.RunContinuationsAsynchronously);input=new Capture();await window.BeginForTestsAsync();input.Emit(pcm);stopping=window.StopForTestsAsync();await Task.Delay(10);pendingIntent.TryRequest();recognition.SetResult("Keep the complete recovery draft.");await stopping;
                    Check(paste.Pastes==1 && paste.Enters==0 && paste.Clipboard=="Keep the complete recovery draft.","Changed destination blocks send and leaves complete text for ordinary Ctrl+V");
                    destination=new DraftTarget(12345,Environment.ProcessId,[1,2,3],0);
                    paste=new FakeDelivery();insertion.PasteTransportForTests=paste;paste.AfterPaste=()=>{paste.Clipboard="newer owner copy";paste.Sequence++;};
                    recognition=new(TaskCreationOptions.RunContinuationsAsynchronously);input=new Capture();await window.BeginForTestsAsync();input.Emit(pcm);stopping=window.StopForTestsAsync();await Task.Delay(10);pendingIntent.TryRequest();recognition.SetResult("This draft must not replace a newer copy.");await stopping;
                    Check(paste.Enters==0 && paste.Clipboard=="newer owner copy","New owner copy takes priority over automatic recovery");
                    destination=new DraftTarget(12345,Environment.ProcessId,[1,2,3],0);
                    paste=new FakeDelivery();insertion.PasteTransportForTests=paste;paste.AfterPaste=()=>destination=destination! with{RuntimeId=[10]};
                    recognition=new(TaskCreationOptions.RunContinuationsAsynchronously);input=new Capture();await window.BeginForTestsAsync();input.Emit(pcm);stopping=window.StopForTestsAsync();await Task.Delay(10);
                    paste.Clipboard="owner copy during recognition";paste.Sequence++;pendingIntent.TryRequest();recognition.SetResult("Do not replace the newer pre-paste copy.");await stopping;
                    Check(paste.Enters==0 && paste.Clipboard=="owner copy during recognition","Copy made during recognition is restored after a failed send");
                                        destination=new DraftTarget(12345,Environment.ProcessId,[1,2,3],0);paste=new FakeDelivery();insertion.PasteTransportForTests=paste;
                    var pasteGate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);paste.OnDelay=_=>pasteGate.Task;
                    window.RecognitionForTests=(_,_)=>Task.FromResult("A late queued complete draft.");input=new Capture();await window.BeginForTestsAsync();input.Emit(pcm);stopping=window.StopForTestsAsync();await Task.Delay(10);
                    Check(pendingIntent.IsOpen && paste.Pastes==1 && paste.Enters==0,"Enter remains intercepted until complete paste verification finishes");
                    pendingIntent.TryRequest();pasteGate.SetResult();await stopping;
                    Check(paste.Enters==1 && paste.Payload=="A late queued complete draft.","Enter during paste verification also sends the complete draft exactly once");
                                        destination=null;paste=new FakeDelivery();insertion.PasteTransportForTests=paste;
                    window.RecognitionForTests=(_,_)=>Task.FromResult("A draft with no eligible captured field.");input=new Capture();await window.BeginForTestsAsync();input.Emit(pcm);await window.StopForTestsAsync();
                    Check(paste.Pastes==0 && paste.Enters==0 && paste.Clipboard=="A draft with no eligible captured field.","Unavailable insertion target still supplies normal clipboard recovery");
                    paste=new FakeDelivery();insertion.PasteTransportForTests=paste;input=new Capture();await window.BeginForTestsAsync();input.Emit(pcm);
                    typeof(MainWindow).GetField("recordingCapture",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,new TargetCaptureResult(null,TargetCaptureStatus.PasswordField));
                    await window.StopForTestsAsync();Check(paste.Clipboard=="fixture prior clipboard" && paste.Pastes==0 && paste.Enters==0,"Password-field rejection never acquires an automatic recovery copy");
                    Check(!window.IsVisible&&!overlay.IsVisible,"Fixture displays no native test windows");
                    Array.Clear(pcm);
                } catch(Exception ex){failures.Add(ex.ToString());}
                finally { window?.RequestQuit(); app.Shutdown(); }
            })); app.Run();
            void CaptureElement(FrameworkElement element,string file,double width){element.Measure(new Size(width,1000));element.Arrange(new Rect(0,0,width,element.DesiredSize.Height));element.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(element);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,file));png.Save(stream);}
        }){IsBackground=true};
        try {thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!thread.Join(TimeSpan.FromSeconds(25)))throw new TimeoutException("Responsiveness fixture exceeded its bound.");}
        finally{Environment.SetEnvironmentVariable("SONA_DATA",prior);}
        var report=new{Version=AppVersion.Number,BuildId=AppVersion.BuildId,Checks=checks,Failures=failures,Measurements=measurements,MicrophoneOpened=false,GlobalInputSent=false,ProductionProfileUsed=false};
        File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));return report;
    }
    private sealed class Clock:TimeProvider{long now;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>now;public void Advance(double seconds)=>now+=(long)(seconds*1000);}
    private sealed class FakeDelivery:ICompletedDraftTransport {
        public int Pastes,Enters;public uint Sequence=1;public string Clipboard="fixture prior clipboard",Payload="";public Action? AfterPaste;public Func<int,Task>? OnDelay;
        public object SaveClipboard()=>Clipboard;public uint SetClipboard(string text){Clipboard=Payload=text;return ++Sequence;}public uint ClipboardSequence=>Sequence;
        public void RestoreClipboard(object? previous){Clipboard=(string?)previous??"";Sequence++;}
        public bool Paste(){Pastes++;AfterPaste?.Invoke();return true;}public bool Enter(){Enters++;return true;}public bool EnterHeld=>false;
        public string? ReadText(DraftTarget target)=>Pastes>0?Payload:"";public Task Delay(int milliseconds)=>OnDelay?.Invoke(milliseconds)??Task.CompletedTask;
    }
    private sealed class Capture:IWaveIn{
        public bool Started,Disposed;public WaveFormat WaveFormat{get;set;}=new(16000,16,1);public event EventHandler<WaveInEventArgs>? DataAvailable;public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public void StartRecording()=>Started=true;public void StopRecording()=>RecordingStopped?.Invoke(this,new StoppedEventArgs());public void Dispose()=>Disposed=true;public void Emit(byte[] bytes)=>DataAvailable?.Invoke(this,new WaveInEventArgs(bytes,bytes.Length));
    }
}
