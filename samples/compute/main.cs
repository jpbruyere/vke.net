// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;
using System.Linq;
using Vulkan;
using vke;

//very simple compute example that just do an addition on every items of a random list of numbers.
namespace SimpleCompute {
	class Program : IDisposable {
		static void Main (string[] args) {
			using (Program vke = new Program ())
				vke.Run ();
		}

		Instance instance;
		PhysicalDevice phy;
		Device dev;
		Queue computeQ;

		HostBuffer inBuff, outBuff;
		DescriptorPool dsPool;
		DescriptorSetLayout dsLayout;
		DescriptorSet dset;

		ComputePipeline plCompute;


		//random datas generation
		const uint data_size = 16;
		int[] datas;
		void createRandomDatas () {
			datas = new int[data_size];
			Random rnd = new Random ();
			for (uint i = 0; i < data_size; i++)
				datas[i] = rnd.Next ();
		}


		public Program () {
			instance = new Instance ();
			phy = instance.GetAvailablePhysicalDevice ().FirstOrDefault ();
			dev = new Device (phy);
			computeQ = new Queue (dev, VkQueueFlags.Compute);

			dev.Activate (default (VkPhysicalDeviceFeatures));

			createRandomDatas ();

			inBuff = new HostBuffer<int> (dev, VkBufferUsageFlags.StorageBuffer, datas);
			outBuff = new HostBuffer<uint> (dev, VkBufferUsageFlags.StorageBuffer, data_size);

			dsPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.StorageBuffer, 2));
			dsLayout = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Compute, VkDescriptorType.StorageBuffer),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Compute, VkDescriptorType.StorageBuffer)
			);

			plCompute = new ComputePipeline (new PipelineLayout (dev, dsLayout), "#shaders.compute.comp.spv" );

			dset = dsPool.Allocate (dsLayout);
			DescriptorSetWrites dsUpdate = new DescriptorSetWrites (dset, dsLayout);
			dsUpdate.Write (dev, inBuff.Descriptor, outBuff.Descriptor);
		}

		public void Run () {
			using (CommandPool cmdPool = new CommandPool (dev, computeQ.qFamIndex)) {
				PrimaryCommandBuffer cmd = cmdPool.AllocateAndStart (VkCommandBufferUsageFlags.OneTimeSubmit);
				plCompute.Bind (cmd);
				plCompute.BindDescriptorSet (cmd, dset);
				cmd.Dispatch (data_size * sizeof (int));
				cmd.End ();

				computeQ.Submit (cmd);
				computeQ.WaitIdle ();
			}

			printResults ();
		}

		void printResults () {
			int[] results = new int[data_size];

			outBuff.Map ();
			Marshal.Copy (outBuff.MappedData, results, 0, results.Length);

			Console.Write ("IN :");
			for (int i = 0; i < data_size; i++)
				Console.Write ($"{datas[i]} ");

			Console.WriteLine ();Console.WriteLine ();

			Console.Write ("OUT:");
			for (int i = 0; i < data_size; i++)
				Console.Write ($"{results[i]} ");

			Console.WriteLine ();
			outBuff.Unmap ();
		}

		public void Dispose () {
			dev.WaitIdle ();

			plCompute.Dispose ();
			dsLayout.Dispose ();
			dsPool.Dispose ();

			inBuff.Dispose ();
			outBuff.Dispose ();

			dev.Dispose ();
			instance.Dispose ();
		}
	}
}
