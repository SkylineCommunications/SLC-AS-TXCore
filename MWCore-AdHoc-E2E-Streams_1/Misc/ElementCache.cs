namespace MWCoreAdHocE2EStreams_1.Misc
{
	using System;

	internal class ElementCache
	{
		public ElementCache(StreamsIO instance, DateTime lastRun)
		{
			if (instance is null)
			{
				throw new ArgumentNullException(nameof(instance));
			}

			if (lastRun == default)
			{
				throw new ArgumentNullException(nameof(lastRun));
			}

			Instance = instance;
			LastRun = lastRun;
		}

		public StreamsIO Instance { get; set; }

		public DateTime LastRun { get; set; }
	}
}