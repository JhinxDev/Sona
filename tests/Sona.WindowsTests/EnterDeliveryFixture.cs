using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;
using Sona.App;
using Sona.App.Audio;
using Sona.App.Windows;
using Sona.Core;

internal static class EnterDeliveryFixture
{
    public static object Run(string output)
    {
        output=Path.GetFullPath(output);if(Directory.Exists(output))throw new IOException("Use fresh fixture output.");Directory.CreateDirectory(output);
        var prior=Environment.GetEnvironmentVariable("SONA_DATA");Environment.SetEnvironmentVariable("SONA_DATA",Path.Combine(output,"profile"));
        new Preferences{SoundEffects=false,EnterToSend=true,AutoInsert=true,PasteCompletedDraft=true,CopyOnInsertionFailure=true}.Save(Path.Combine(output,"profile","preferences.json"));
        var checks=new List<string>();var failures=new List<string>();
        void Check(bool ok,string name){(ok?checks:failures).Add(name);}
        var thread=new Thread(()=>{
            var app=new ThemeResourceTestApplication();app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async()=>{
                MainWindow? window=null;
                try{
                    var original=new DraftTarget(12345,Environment.ProcessId,[1,2,3],0);DraftTarget? current=original;
                    (nint Window,int Owner) foreground=(original.Window,original.OwnerProcessId);
                    using var insertion=new Insertion(()=>current,()=>false,_=>throw new Exception("Unicode input must never run"));
                    typeof(Insertion).GetProperty("ForegroundForTests",BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(insertion,(Func<(nint Window,int Owner)>)(()=>foreground));
                    window=new MainWindow(["--test-ui"],insertion);window.PreparationForTests=_=>Task.CompletedTask;
                    var input=new Capture();window.RecorderForTests=()=>new Recorder(_=>input);
                    var copied="";window.ClipboardForTests=text=>copied=text;
                    window.ClipboardSequenceForTests=()=>1;
                    var finish=typeof(MainWindow).GetMethod("FinishAndSendShortcut",BindingFlags.Instance|BindingFlags.NonPublic)!;
                    var admission=typeof(MainWindow).GetMethod("CanFinishAndSendShortcut",BindingFlags.Instance|BindingFlags.NonPublic);
                    var operation=typeof(MainWindow).GetField("operation",BindingFlags.Instance|BindingFlags.NonPublic)!;
                    var pending=(PendingSubmission)typeof(MainWindow).GetField("pendingSubmission",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
                    var context=(RecordingDestinationContext)typeof(MainWindow).GetField("destinationContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
                    var pcm=new byte[64000];for(var i=1;i<pcm.Length;i+=2)pcm[i]=8;
                    foreach(var change in new[]{"epoch","field","unavailable","departure-return","between-hook-and-ui","valid","pasted-send-held","pasted-focus-changed"}){
                        current=original;foreground=(original.Window,original.OwnerProcessId);copied="";input=new Capture();
                        var paste=new FakeDelivery{HoldAfterPaste=change=="pasted-send-held"};insertion.PasteTransportForTests=paste;window.ClipboardSequenceForTests=()=>paste.Sequence;
                        if(change=="pasted-focus-changed")paste.AfterPastedRead=()=>{current=original with{FocusEpoch=1};context.Observe(new ActiveApplication(original.Window,original.OwnerProcessId,"Original",1));};
                        var recognition=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                        window.RecognitionForTests=(_,token)=>recognition.Task.WaitAsync(token);
                        Check(await window.BeginForTestsAsync(),change+": begins only fake capture");input.Emit(pcm);
                        if(change=="epoch")current=original with{FocusEpoch=1};
                        if(change=="field")current=original with{RuntimeId=[7]};
                        if(change=="unavailable")current=null;
                        if(change=="departure-return"){
                            context.Observe(new ActiveApplication(67890,Environment.ProcessId,"Other",1));
                            context.Observe(new ActiveApplication(original.Window,original.OwnerProcessId,"Original",2));
                        }
                        var canAdmit=admission?.Invoke(window,null) is true;
                        Check(canAdmit,change+": exact foreground window admits Enter without granting insertion");
                        if(change=="between-hook-and-ui")current=original with{FocusEpoch=1};
                        finish.Invoke(window,null);await Task.Delay(10);
                        Check(window.SessionForTests==SessionState.Transcribing&&input.Disposed,change+": Enter stops capture before recognition completes");
                        Check(paste.Pastes==0&&paste.Enters==0,change+": no partial result reaches the composer");
                        if(window.SessionForTests==SessionState.Recording){window.CancelForTests();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);recognition.TrySetResult("Fixture final text.");continue;}
                        recognition.SetResult("The complete final fixture draft.");await ((Task)operation.GetValue(window)!);
                        if(change is "pasted-send-held" or "pasted-focus-changed")
                        {
                            Check(paste.Pastes==1&&paste.Enters==0&&insertion.LastPasteConfirmed,"Full paste is distinct from withheld send");
                            Check(((System.Windows.Controls.TextBlock)window.OverlayForTests.FindName("CapsuleLabel")).Text=="Draft pasted"&& !((System.Windows.Controls.TextBlock)window.FindName("StatusDetail")).Text.Contains("Ctrl+V")&& !((System.Windows.Controls.TextBlock)window.OverlayForTests.FindName("CapsuleHint")).Text.Contains("Ctrl+V"),"Confirmed pasted draft never tells the owner to paste it a second time");
                        }
                        else if(change=="valid")Check(paste.Pastes==1&&paste.Enters==1&&paste.Payload=="The complete final fixture draft.","Unchanged field receives one complete draft and one send");
                        else Check(paste.Pastes==0&&paste.Enters==0&&window.SessionForTests==SessionState.Ready,"Changed or unavailable destination retains complete text with zero output: "+change);
                    }
                    current=original;foreground=(67890,original.OwnerProcessId);
                    Check(admission?.Invoke(window,null) is false,"Different foreground HWND leaves native Enter alone");
                    foreground=(original.Window,original.OwnerProcessId+1);
                    Check(admission?.Invoke(window,null) is false,"Different HWND owner leaves native Enter alone");
                    var gesture=new HoldGesture();gesture.Key(0xA2,true,0,toggle:true);var admitted=gesture.Key(0x5B,true,1,toggle:true);gesture.ConsumeToggle(admitted.Generation);gesture.Key(0x5B,false,2);gesture.Key(0xA2,false,3);
                    var enter=gesture.Key(13,true,4,finishOnEnter:true);
                    Check(enter.Suppress&&enter.Submit&&gesture.Key(13,true,5,finishOnEnter:false).Suppress&&gesture.Key(13,false,6,finishOnEnter:false).Suppress,"Consumed Enter repeats/up stay swallowed after destination movement");
                    current=original;foreground=(original.Window,original.OwnerProcessId);input=new Capture();
                    var pendingRecognition=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                    window.RecognitionForTests=(_,token)=>pendingRecognition.Task.WaitAsync(token);await window.BeginForTestsAsync();input.Emit(pcm);
                    var pendingPaste=new FakeDelivery();insertion.PasteTransportForTests=pendingPaste;
                    var stopping=window.StopForTestsAsync();finish.Invoke(window,null);
                    Check(pending.IsRequested,"Enter dispatched after capture stopped attaches to the pending complete result");
                    pendingRecognition.SetResult("The complete queued fixture draft.");await stopping;
                    Check(pendingPaste.Pastes==1&&pendingPaste.Enters==1,"Raced Enter is sent once only after the final result");
                    var observation=typeof(MainWindow).GetField("lastDelivery",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(window);
                    var metadata=JsonSerializer.Serialize(observation);
                    Check(observation!=null&&!metadata.Contains("fixture draft",StringComparison.OrdinalIgnoreCase),"Last delivery diagnostics contain classification without transcript text");
                    Check(!window.IsVisible&&!window.OverlayForTests.IsVisible,"All Enter regression windows and pills stay suppressed");Array.Clear(pcm);
                }catch(Exception error){failures.Add(error.ToString());}
                finally{window?.CancelForTests();window?.RequestQuit();app.Shutdown();}
            }));app.Run();
        }){IsBackground=true};
        try{thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!thread.Join(TimeSpan.FromSeconds(25)))throw new TimeoutException("Enter fixture exceeded its bound.");}
        finally{Environment.SetEnvironmentVariable("SONA_DATA",prior);}
        var report=new{Version=AppVersion.Number,BuildId=AppVersion.BuildId,Checks=checks,Failures=failures,MicrophoneOpened=false,GlobalInputSent=false,ProductionProfileUsed=false};
        File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));return report;
    }
    private sealed class Clock:TimeProvider{long now;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>now;public void Advance(double seconds)=>now+=(long)(seconds*1000);}
    private sealed class FakeDelivery:ICompletedDraftTransport {
        public int Pastes,Enters;public uint Sequence=1;public string Clipboard="fixture prior clipboard",Payload="";
        public object SaveClipboard()=>Clipboard;public uint SetClipboard(string text){Clipboard=Payload=text;return ++Sequence;}public uint ClipboardSequence=>Sequence;
        public void RestoreClipboard(object? previous){Clipboard=(string?)previous??"";Sequence++;}
        public bool Paste(){Pastes++;return true;}public bool Enter(){Enters++;return true;}public bool HoldAfterPaste{get;set;}public bool EnterHeld=>HoldAfterPaste&&Pastes>0;
        public Action? AfterPastedRead{get;set;}public string? ReadText(DraftTarget target){if(Pastes>0)AfterPastedRead?.Invoke();return Pastes>0?Payload:"";}public Task Delay(int milliseconds)=>Task.CompletedTask;
    }
    private sealed class Capture:IWaveIn{
        public bool Started,Disposed;public WaveFormat WaveFormat{get;set;}=new(16000,16,1);public event EventHandler<WaveInEventArgs>? DataAvailable;public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public void StartRecording()=>Started=true;public void StopRecording()=>RecordingStopped?.Invoke(this,new StoppedEventArgs());public void Dispose()=>Disposed=true;public void Emit(byte[] bytes)=>DataAvailable?.Invoke(this,new WaveInEventArgs(bytes,bytes.Length));
    }
}
