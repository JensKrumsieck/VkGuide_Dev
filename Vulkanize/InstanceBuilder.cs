using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Windowing;

namespace Vulkanize;

public class InstanceBuilder
{
    private readonly Vk _vk = Vulkanize.Vk;

    private string _applicationName = "";
    private Version32 _appVersion = new(1, 0, 0);
    
    private string _engineName = "No Engine";
    private Version32 _engineVersion = new(1, 0, 0);

    private Version32 _apiVersion = Vk.Version12;

    private bool _enableValidationLayers;
    private bool _useDebugMessenger;
    private DebugUtilsMessengerCallbackFunctionEXT? _debugCallback;

    private DebugUtilsMessageSeverityFlagsEXT _debugSeverityFlags = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                                                    DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                                                    DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;

    private DebugUtilsMessageTypeFlagsEXT _debugMessageTypeFlags = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                                                   DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                                                                   DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
    
    private readonly List<string> _layers = new();
    private readonly List<string> _extensions = new();
    
    public InstanceBuilder SetAppName(string name)
    {
        _applicationName = name;
        return this;
    }

    public InstanceBuilder SetEngineName(string name)
    {
        _engineName = name;
        return this;
    }

    public InstanceBuilder SetAppVersion(Version32 version)
    {
        _appVersion = version;
        return this;
    }

    public InstanceBuilder SetAppVersion(uint major, uint minor, uint build) =>
        SetAppVersion(new Version32(major, minor, build));

    public InstanceBuilder SetEngineVersion(Version32 version)
    {
        _engineVersion = version;
        return this;
    }
    
    public InstanceBuilder SetEngineVersion(uint major, uint minor, uint build) =>
        SetEngineVersion(new Version32(major, minor, build));

    public InstanceBuilder RequireApiVersion(Version32 version)
    {
        _apiVersion = version;
        return this;
    }
    
    public InstanceBuilder RequireApiVersion(uint major, uint minor, uint build) =>
        RequireApiVersion(new Version32(major, minor, build));

    public InstanceBuilder EnableLayer(string layer)
    {
        _layers.Add(layer);
        return this;
    }

    public InstanceBuilder EnableLayers(params string[] layers)
    {
        _layers.AddRange(layers);
        return this;
    }

    public InstanceBuilder EnableExtension(string extension)
    {
        _extensions.Add(extension);
        return this;
    }

    public InstanceBuilder EnableExtensions(params string[] extensions)
    {
        _extensions.AddRange(extensions);
        return this;
    }
    
    public InstanceBuilder EnableValidationLayers(bool enableValidation = true)
    {
        _enableValidationLayers = enableValidation;
        if (_enableValidationLayers)
        {
            _extensions.Add(ExtDebugUtils.ExtensionName);
            _layers.Add("VK_LAYER_KHRONOS_validation");
        }
        return this;
    }

    public unsafe InstanceBuilder UseDefaultDebugMessenger()
    {
        _useDebugMessenger = true;
        _debugCallback = (DebugUtilsMessengerCallbackFunctionEXT)DefaultDebugCallback;
        return this;
    }

    public InstanceBuilder UseDebugCallback(DebugUtilsMessengerCallbackFunctionEXT callback)
    {
        _useDebugMessenger = true;
        _debugCallback = callback;
        return this;
    }

    public InstanceBuilder UseDebugMessageSeverity(DebugUtilsMessageSeverityFlagsEXT flags)
    {
        _useDebugMessenger = true;
        _debugSeverityFlags = flags;
        return this;
    }

    public InstanceBuilder AddDebugMessageSeverity(DebugUtilsMessageSeverityFlagsEXT flags)
    {
        _useDebugMessenger = true;
        _debugSeverityFlags |= flags;
        return this;
    }

    public InstanceBuilder UseDebugMessageType(DebugUtilsMessageTypeFlagsEXT flags)
    {
        _useDebugMessenger = true;
        _debugMessageTypeFlags = flags;
        return this;
    }
    
    public InstanceBuilder AddDebugMessageType(DebugUtilsMessageTypeFlagsEXT flags)
    {
        _useDebugMessenger = true;
        _debugMessageTypeFlags |= flags;
        return this;
    }
    
    public unsafe InstanceBuilder UseRequiredWindowExtensions(IWindow window)
    {
        ArgumentNullException.ThrowIfNull(window.VkSurface);
        var windowExtensions = window.VkSurface.GetRequiredExtensions(out var extensionCount);
        var extensions = SilkMarshal.PtrToStringArray((nint) windowExtensions, (int)extensionCount);
        _extensions.AddRange(extensions);
        if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) _extensions.Add("VK_KHR_portability_enumeration");
        return this;
    }
    public InstanceInfo Build()
    {
        var instance = CreateInstance();
        var instanceInfo = new InstanceInfo {Instance = instance};
        if (!_enableValidationLayers) return instanceInfo;
        var debugMessenger = CreateDebugUtilsMessenger(instance);
        return instanceInfo with {DebugMessenger = debugMessenger};

    }
     private unsafe Instance CreateInstance()
    {
        if (_enableValidationLayers && !Vulkanize.CheckValidationLayerSupport())
            throw new Exception("Validation layers requested but not supported");
        
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*) Marshal.StringToHGlobalAnsi(_applicationName),
            ApplicationVersion = _appVersion,
            PEngineName = (byte*) Marshal.StringToHGlobalAnsi(_engineName),
            EngineVersion = _engineVersion,
            ApiVersion = _apiVersion
        };

        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)_extensions.Count,
            PpEnabledExtensionNames = (byte**) SilkMarshal.StringArrayToPtr(_extensions.ToArray()),
            EnabledLayerCount = (uint)_layers.Count,
            PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(_layers.ToArray()),
            PNext = null
        };
        if (_enableValidationLayers && _useDebugMessenger)
        {
            var debugCreateInfo = new DebugUtilsMessengerCreateInfoEXT
            {
                SType = StructureType.DebugUtilsMessengerCreateInfoExt,
                MessageSeverity = _debugSeverityFlags,
                MessageType = _debugMessageTypeFlags,
                PfnUserCallback = _debugCallback
            };
            createInfo.PNext = &debugCreateInfo;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            createInfo.Flags = InstanceCreateFlags.EnumeratePortabilityBitKhr;
        if (_vk.CreateInstance(createInfo, null, out var instance) != Result.Success)
            throw new Exception("Could not create vulkan instance");

        Marshal.FreeHGlobal((nint) appInfo.PApplicationName);
        Marshal.FreeHGlobal((nint) appInfo.PEngineName);
        SilkMarshal.Free((nint) createInfo.PpEnabledExtensionNames);
        if (_enableValidationLayers) SilkMarshal.Free((nint) createInfo.PpEnabledLayerNames);
        return instance;
    }

    private unsafe DebugUtilsMessengerEXT CreateDebugUtilsMessenger(Instance instance)
    {
        if (!_vk.TryGetInstanceExtension(instance, out ExtDebugUtils debugUtils))
            throw new Exception("Could not get instance extension debugUtils");
        var createInfo = new DebugUtilsMessengerCreateInfoEXT
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = _debugSeverityFlags,
            MessageType = _debugMessageTypeFlags,
            PfnUserCallback = _debugCallback
        };
        if (debugUtils!.CreateDebugUtilsMessenger(instance, in createInfo, null, out var debugMessenger) !=
            Result.Success)
            throw new Exception("Failed to set up debug messenger");

        return debugMessenger;
    }
    
    private static unsafe uint DefaultDebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
        DebugUtilsMessageTypeFlagsEXT messageTypes,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        if (messageSeverity == DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt) return Vk.False;
        var message = Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage);
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} [{messageSeverity} -> validation layer: {message}");
        return Vk.False;
    }
}
