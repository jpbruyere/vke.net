### Enabling extensions

The **`VkWindow`** class provides two properties that you may override to enable additional extensions.

```csharp
public override string[] EnabledInstanceExtensions => new string[] {
    Ext.I.VK_EXT_debug_utils
};
public override string[] EnabledDeviceExtensions => new string[] {
    Ext.D.VK_KHR_swapchain,
};
```
Extension's names are organized in two subclasses of the `Ext` static class, one for the instance extensions (**`Ext.I`**) and one for the device ones (**`Ext.D`**).
### Enabling features

Override the **`configureEnabledFeatures`** method of **`VkWindow`** to enable features. This method is called just after
the physical device selection, available features list is automatically queried from it and provided as the first argument.
```csharp
protected override void configureEnabledFeatures (
    VkPhysicalDeviceFeatures available_features,
    ref VkPhysicalDeviceFeatures enabled_features) {

    enabled_features.samplerAnisotropy = available_features.samplerAnisotropy;
}
```
### Creating queues

To create queues, override the **`createQueues`** method of **`VkWindow`**. This function is called before the logical device creation and will take care of physically available queues, creating duplicates if count exceed availability. The `base` method will create a default presentable queue.

```csharp
protected override void createQueues () {
	base.createQueues ();
	transferQ = new Queue (dev, VkQueueFlags.Transfer);
}
```

### Loading images

The `vke.Image` classes has facilities to load bitmaps from disk (jpg, gif, ...). KTX images use the `KTX.KTX.Load` static method.

```csharp
KTX.KTX.Load (presentQueue, cmdPool, path, ...
Image.Load (dev, presentQueue, cmdPool, path, ...
```

Once the image is loaded, you may create the associated View and Sampler by calling image instance methods. View and Sampler lifecycle will be bound to the image and disposed when the image is disposed.

The `Descriptor` property of the image will old the created handles.

```csharp
nextTexture.CreateView ();
nextTexture.CreateSampler ();
nextTexture.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
```