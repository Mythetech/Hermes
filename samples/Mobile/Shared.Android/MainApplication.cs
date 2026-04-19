// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Android.App;
using Android.Runtime;

namespace Shared.Android;

[Application]
public class MainApplication : Application
{
    public MainApplication(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }
}
