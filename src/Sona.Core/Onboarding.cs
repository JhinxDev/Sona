namespace Sona.Core;

/// <summary>Home-page onboarding state derived from profile load status and model readiness.</summary>
public sealed record OnboardingState(bool ShowWelcome, bool ShowSetup, bool ModelReady);

public static class Onboarding
{
    /// <summary>
    /// Welcome only for a genuinely new profile. Setup guidance appears whenever the
    /// selected local model is unusable, including on an existing profile whose
    /// runtime or model files went missing. Dismissal is remembered.
    /// </summary>
    public static OnboardingState Evaluate(PreferencesLoadStatus loadStatus, bool dismissed, bool modelReady)
    {
        var welcome = loadStatus == PreferencesLoadStatus.Missing && !dismissed;
        return new(welcome, !modelReady, modelReady);
    }
}
