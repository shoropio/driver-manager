using System.Runtime.InteropServices;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Enumera los adaptadores de vídeo vía DXGI y expone su nombre, LUID
/// (clave coincidente con los contadores WMI de GPU) y VRAM dedicada.
/// </summary>
internal static class DxgiAdapterEnumerator
{
    private const uint DxgiErrorNotFound = 0x887A0002;
    private static readonly Guid Factory1Iid = new("770aae78-f26f-4dba-a829-253c83d1b387");
    private static readonly Guid AdapterIid = new("2411e7e1-12ac-4ccf-bd14-9798e8534dc0");

    public static IReadOnlyList<DxgiAdapterInfo> Enumerate()
    {
        var adapters = new List<DxgiAdapterInfo>();
        var factoryIid = Factory1Iid;
        if (CreateDxgiFactory1(ref factoryIid, out var factoryPtr) != 0 || factoryPtr == IntPtr.Zero)
        {
            return adapters;
        }

        try
        {
            var factory = (IDxgiFactory1)Marshal.GetObjectForIUnknown(factoryPtr);
            uint index = 0;
            while (true)
            {
                var enumHr = factory.EnumAdapters1(index, out var adapterPtr);
                if (enumHr == unchecked((int)DxgiErrorNotFound) || adapterPtr == IntPtr.Zero)
                {
                    break;
                }

                try
                {
                    var iid = AdapterIid;
                    var qiHr = Marshal.QueryInterface(adapterPtr, in iid, out var qiAdapter);
                    if (qiHr == 0 && qiAdapter != IntPtr.Zero)
                    {
                        try
                        {
                            var adapter = (IDxgiAdapter)Marshal.GetObjectForIUnknown(qiAdapter);
                            if (adapter.GetDesc(out var desc) == 0)
                            {
                                adapters.Add(new DxgiAdapterInfo(
                                    desc.Description,
                                    BuildLuidKey(desc.AdapterLuid),
                                    desc.DedicatedVideoMemory.ToInt64()));
                            }
                        }
                        finally
                        {
                            Marshal.Release(qiAdapter);
                        }
                    }
                }
                finally
                {
                    Marshal.Release(adapterPtr);
                }

                index++;
            }
        }
        finally
        {
            Marshal.Release(factoryPtr);
        }

        return adapters;
    }

    private static string BuildLuidKey(DxgiLuid luid) =>
        $"0x{(uint)luid.HighPart:X8}_0x{luid.LowPart:X8}";

    [DllImport("dxgi.dll", EntryPoint = "CreateDXGIFactory1", PreserveSig = true)]
    private static extern int CreateDxgiFactory1(ref Guid riid, out IntPtr ppFactory);

    [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDxgiFactory1
    {
        int SetPrivateData();
        int SetPrivateDataInterface();
        int GetPrivateData();
        int GetParent();
        int EnumAdapters();
        int MakeWindowAssociation();
        int GetWindowAssociation();
        int CreateSwapChain();
        int CreateSoftwareAdapter();
        [PreserveSig] int EnumAdapters1(uint adapter, out IntPtr ppAdapter);
    }

    [ComImport, Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDxgiAdapter
    {
        int SetPrivateData();
        int SetPrivateDataInterface();
        int GetPrivateData();
        int GetParent();
        int EnumOutputs();
        [PreserveSig] int GetDesc(out DxgiAdapterDesc desc);
    }
}

internal sealed record DxgiAdapterInfo(string Description, string LuidKey, long DedicatedVideoMemory);

[StructLayout(LayoutKind.Sequential)]
internal struct DxgiLuid
{
    public uint LowPart;
    public int HighPart;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DxgiAdapterDesc
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Description;
    public uint VendorId;
    public uint DeviceId;
    public uint SubSysId;
    public uint Revision;
    public IntPtr DedicatedVideoMemory;
    public IntPtr DedicatedSystemMemory;
    public IntPtr SharedSystemMemory;
    public DxgiLuid AdapterLuid;
}
