using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace FamilyCloud.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public static int StatusBarInsetPx { get; private set; }
    public static event Action<int>? StatusBarInsetChanged;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        WindowCompat.SetDecorFitsSystemWindows(Window!, false);
        ViewCompat.SetOnApplyWindowInsetsListener(Window!.DecorView, new InsetsListener());
    }

    private sealed class InsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (insets is null)
            {
                return WindowInsetsCompat.Consumed!;
            }

            var top = insets.GetInsets(WindowInsetsCompat.Type.SystemBars())!.Top;
            StatusBarInsetPx = top;
            StatusBarInsetChanged?.Invoke(top);
            return insets;
        }
    }
}
