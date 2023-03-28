// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;
using System.Linq;
using Vulkan;
using vke;
using System.Threading;

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
		DescriptorSetLayout dsLayout;
		Queue computeQ;


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

			dev.Activate (IntPtr.Zero, default (VkPhysicalDeviceFeatures));

			createRandomDatas ();


			dsLayout = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Compute, VkDescriptorType.StorageBuffer),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Compute, VkDescriptorType.StorageBuffer)
			);

			plCompute = new ComputePipeline (new PipelineLayout (dev, dsLayout), "#shaders.compute.comp.spv" );

		}
		const int threadCount = 64;
		object printMutex = new object();
		void computeThread() {
			HostBuffer inBuff, outBuff;
			DescriptorPool dsPool;
			DescriptorSet dset;

			inBuff = new HostBuffer<int> (dev, VkBufferUsageFlags.StorageBuffer, datas);
			outBuff = new HostBuffer<uint> (dev, VkBufferUsageFlags.StorageBuffer, data_size);

			dsPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.StorageBuffer, 2));
			dset = dsPool.Allocate (dsLayout);
			DescriptorSetWrites dsUpdate = new DescriptorSetWrites (dset, dsLayout);
			dsUpdate.Write (dev, inBuff.Descriptor, outBuff.Descriptor);

			using (CommandPool cmdPool = new CommandPool (dev, computeQ.qFamIndex)) {
				PrimaryCommandBuffer cmd = cmdPool.AllocateAndStart (VkCommandBufferUsageFlags.OneTimeSubmit);
				plCompute.Bind (cmd);
				plCompute.BindDescriptorSet (cmd, dset);
				cmd.Dispatch (data_size * sizeof (int));
				cmd.End ();
				lock(computeQ)
					computeQ.Submit (cmd);
				computeQ.WaitIdle ();
			}
			lock(printMutex)
				printResults (outBuff);

			dsPool.Dispose ();
			inBuff.Dispose ();
			outBuff.Dispose ();
			finishedThread++;
		}
		volatile int finishedThread = 0;
		void createThreads () {
			//Console.WriteLine ($"count down reset");
			for (int i = 0; i < threadCount; i++) {
				Thread t = new Thread (computeThread);
				t.IsBackground = true;
				t.Start ();
			}
		}

		public void Run () {
			createThreads();
			while (finishedThread < threadCount)
				Thread.Sleep(1);
			dev.WaitIdle();
		}

		void printResults (HostBuffer outBuff) {
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

			dev.Dispose ();
			instance.Dispose ();
		}
	}
}
