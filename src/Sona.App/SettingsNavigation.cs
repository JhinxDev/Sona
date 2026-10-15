using System.Windows;
using System.Windows.Controls;

namespace Sona.App;

public partial class MainWindow
{
    private void SettingsSection_Checked(object sender,RoutedEventArgs e)
    {
        if(SettingsScroll==null||SettingsGeneralSection==null||sender is not RadioButton {Tag:string selected})return;
        var sections=new Dictionary<string,(FrameworkElement Panel,string Title,string Description)>
        {
            ["General"]=(SettingsGeneralSection,"General","Make Sona fit your day."),
            ["Audio"]=(SettingsAudioSection,"Audio & typing","Choose your microphone and how completed dictation reaches your app."),
            ["Appearance"]=(SettingsAppearanceSection,"Appearance","Choose a theme and recording display."),
            ["Cleanup"]=(SettingsCleanupSection,"Text cleanup","Polish your words locally while keeping the original available."),
            ["Privacy"]=(SettingsPrivacySection,"Privacy","Control what stays on this device."),
            ["Discord"]=(SettingsDiscordSection,"Discord","Mute your Discord microphone while you dictate."),
            ["About"]=(SettingsAboutSection,"About","Version, update checks and application information.")
        };
        if(!sections.TryGetValue(selected,out var current))return;
        foreach(var item in sections) item.Value.Panel.Visibility=item.Key==selected?Visibility.Visible:Visibility.Collapsed;
        SettingsSectionTitle.Text=current.Title;SettingsSectionDescription.Text=current.Description;
        SettingsScroll.ScrollToTop();RefreshDiscordConnection();
    }
}
