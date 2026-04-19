// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Android.App;
using Android.OS;
using Hermes.Mobile.Android;

namespace Shared.Android;

[Activity(Label = "Shared Android", MainLauncher = true)]
public class MainActivity : Activity
{
    private HermesMobileAndroidHost? _host;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var builder = HermesMobileAndroidBuilder.CreateDefault(this);
        builder.RootComponents.Add<Shared.App.App>("#app");

        _host = builder.Build();
        SetContentView(_host.RootView);
        _host.Start();
    }

    protected override async void OnDestroy()
    {
        if (_host is not null)
            await _host.DisposeAsync();
        base.OnDestroy();
    }
}
