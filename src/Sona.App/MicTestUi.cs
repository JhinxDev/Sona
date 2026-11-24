using System.Windows;
using System.Windows.Threading;
using NAudio;
using NAudio.Wave;

namespace Sona.App;

public partial class MainWindow
{
    private WaveInEvent? micTestWaveIn;
    private DispatcherTimer? micTestTimeout;
    private bool micTestActive;
    private long micTestLastUiTicks;
    private string micTestDeviceName = "System default";

    private void TestMicrophone_Click(object sender, RoutedEventArgs e)
    {
        if (micTestActive) { StopMicTest(); return; }
        if (arguments.Contains("--test-ui") || shutdownPending || windowClosed) return;
        if (SettingsBusy)
        {
            MicTestPanel.Visibility = Visibility.Visible;
            MicTestFill.Width = 0;
            MicTestStatus.Text = "Finish or cancel dictation before testing the microphone.";
            return;
        }
        var option = Device?.SelectedItem as DeviceOption;
        var deviceId = option?.Id ?? -1;
        micTestDeviceName = option?.Name ?? "System default";
        var capture = new WaveInEvent
        {
            DeviceNumber = deviceId,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 60,
            NumberOfBuffers = 3
        };
        micTestWaveIn = capture;
        micTestActive = true;
        micTestLastUiTicks = 0;
        TestMicrophoneButton.Content = "Stop mic test";
        MicTestPanel.Visibility = Visibility.Visible;
        MicTestFill.Width = 0;
        MicTestStatus.Text = $"Listening on {micTestDeviceName}. Speak normally (auto-stops in 30 s)...";
        capture.DataAvailable += (_, args) =>
        {
            if (!micTestActive || !ReferenceEquals(micTestWaveIn, capture) || windowClosed || args.BytesRecorded < 2) return;
            var nowTicks = Environment.TickCount64;
            if (nowTicks - micTestLastUiTicks < 33) return;
            micTestLastUiTicks = nowTicks;
            double sum = 0;
            var count = args.BytesRecorded / 2;
            for (var i = 0; i + 1 < args.BytesRecorded; i += 2)
            {
                var sample = BitConverter.ToInt16(args.Buffer, i) / 32768.0;
                sum += sample * sample;
            }
            var rms = Math.Sqrt(sum / Math.Max(1, count));
            var normalized = Overlay.MapLevel(rms);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
            {
                if (!micTestActive || !ReferenceEquals(micTestWaveIn, capture) || windowClosed) return;
                var maxTrack = Math.Max(0, MicTestTrack.ActualWidth - 2);
                MicTestFill.Width = maxTrack * normalized;
                MicTestStatus.Text = normalized >= 0.08
                    ? $"Signal detected on {micTestDeviceName}."
                    : $"Listening on {micTestDeviceName}. Speak slightly closer if bars stay low.";
            });
        };
        capture.RecordingStopped += (_, args) =>
        {
            if (args.Exception == null) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (ReferenceEquals(micTestWaveIn, capture))
                    StopMicTest("The microphone became unavailable. Check Windows audio settings.");
            });
        };
        try
        {
            capture.StartRecording();
            micTestTimeout ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            micTestTimeout.Tick -= MicTestTimeout_Tick;
            micTestTimeout.Tick += MicTestTimeout_Tick;
            micTestTimeout.Start();
        }
        catch (Exception ex) when (ex is MmException or InvalidOperationException or System.IO.IOException)
        {
            StopMicTest("Could not open the selected microphone. Check Windows privacy settings or choose another device.");
        }
    }

    private void MicTestTimeout_Tick(object? sender, EventArgs e)
    {
        if (micTestActive) StopMicTest("Microphone test stopped automatically after 30 seconds.");
    }

    private void StopMicTest(string? statusMessage = null)
    {
        micTestTimeout?.Stop();
        var active = micTestWaveIn;
        micTestWaveIn = null;
        micTestActive = false;
        if (active != null)
        {
            try { active.StopRecording(); } catch (MmException) { } catch (InvalidOperationException) { }
            active.Dispose();
        }
        if (TestMicrophoneButton != null) TestMicrophoneButton.Content = "Test microphone";
        if (MicTestFill != null) MicTestFill.Width = 0;
        if (MicTestPanel != null && MicTestStatus != null)
        {
            if (string.IsNullOrEmpty(statusMessage))
            {
                MicTestStatus.Text = "";
                MicTestPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                MicTestStatus.Text = statusMessage;
                MicTestPanel.Visibility = Visibility.Visible;
            }
        }
    }
}
