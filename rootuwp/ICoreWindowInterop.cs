// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Source: https://github.com/microsoft/PowerToys

using System.Runtime.InteropServices;

namespace rootuwp
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F")]
    internal interface ICoreWindowInterop
    {
        System.IntPtr WindowHandle { get; }

        void MessageHandled(bool value);
    }
}
