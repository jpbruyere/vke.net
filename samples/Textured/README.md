### Enabling extensions

The `VkWindow` class provides two properties that you may override to enable additional extensions.

### Enabling features

Override the `configureEnabledFeatures` method of `VkWindow` to enable features.
```csharp
protected override void configureEnabledFeatures (
             VkPhysicalDeviceFeatures available_features,
             ref VkPhysicalDeviceFeatures enabled_features) 
{    
	enabled_features.samplerAnisotropy = available_features.samplerAnisotropy;
}
```
### Creating queues

To create queues, override the `createQueues` method of `VkWindow`. This function is called before the logical device creation and will take care of physically available queues, creating duplicates if count exceed availability. The `base` method will create a default presentable queue.

```csharp
protected override void createQueues () {
	base.createQueues ();
	transferQ = new Queue (dev, VkQueueFlags.Transfer);
}
```