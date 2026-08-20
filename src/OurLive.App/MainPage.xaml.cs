namespace OurLive.App;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

#if ANDROID
		blazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitializedAndroid;
#endif
	}

#if ANDROID
	private void OnBlazorWebViewInitializedAndroid(object? sender, Microsoft.AspNetCore.Components.WebView.BlazorWebViewInitializedEventArgs e)
	{
		void Push(int px)
		{
			var density = Android.App.Application.Context.Resources!.DisplayMetrics!.Density;
			var cssPx = (px / density).ToString(System.Globalization.CultureInfo.InvariantCulture);
			e.WebView.EvaluateJavascript(
				$"document.documentElement.style.setProperty('--android-safe-area-top', '{cssPx}px')", null);
		}

		Push(MainActivity.StatusBarInsetPx);
		MainActivity.StatusBarInsetChanged += Push;
	}
#endif
}
