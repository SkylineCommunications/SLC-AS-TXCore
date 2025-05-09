namespace MWCoreDownloadThumbnails_1.Misc
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	using MWCoreDownloadThumbnails_1.Misc.Enums;

	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;

	internal class ElementCache
	{
		private const double _cachingTime = 300;
		private static readonly object _padlock = new object();
		private static readonly Dictionary<string, ElementCache> _instances = new Dictionary<string, ElementCache>();
		private readonly int _rowsPerPage;

		private ElementCache(GQIDMS dms, IGQILogger logger, string elementName, int pageSize, DateTime lastRun)
		{
			if (String.IsNullOrEmpty(elementName))
			{
				throw new ArgumentNullException(nameof(elementName));
			}

			if (lastRun == default)
			{
				throw new ArgumentNullException(nameof(lastRun));
			}

			DMSMessage responseElement = dms.SendMessage(GetLiteElementInfo.ByName(elementName));
			Element = responseElement as LiteElementInfoEvent;
			Thumbnails = new Dictionary<string, string>();
			LastRun = lastRun;
			_rowsPerPage = pageSize;

			logger.Information($"Initialize new cache for {elementName}");

			Streams = GetStreamsTable(dms);

			RowsProcessed = 0;
		}

		public int RowsPerPage => _rowsPerPage;

		public int RowsProcessed { get; private set; }

		public LiteElementInfoEvent Element { get; set; }

		public Dictionary<string, string> Thumbnails { get; set; }

		public List<StreamsTable> Streams { get; set; }

		public DateTime LastRun { get; set; }

		public static ElementCache Instance(GQIDMS dms, IGQILogger logger, string elementName, int pageSize)
		{
			var dateNow = DateTime.UtcNow;
			ElementCache instance;

			lock (_padlock)
			{
				var cacheLimit = TimeSpan.FromSeconds(_cachingTime);
				if (!_instances.TryGetValue(elementName, out instance) ||
					(dateNow - instance.LastRun) > cacheLimit)
				{
					instance = new ElementCache(dms, logger, elementName, pageSize, dateNow);
					_instances[elementName] = instance;
				}

				foreach (var element in _instances.Keys.ToArray())
				{
					if ((dateNow - _instances[element].LastRun) > cacheLimit)
					{
						_instances.Remove(element);
					}
				}

				return instance;
			}
		}

		public void GetStreamThumbnails(GQIDMS dms, IGQILogger logger)
		{
			if (RowsProcessed >= Streams.Count)
			{
				return;
			}

			int startIndex = RowsProcessed == 0 ? 0 : RowsProcessed - 1;

			int count = (startIndex + _rowsPerPage) > Streams.Count ? Streams.Count - startIndex : _rowsPerPage;

			string token = Thumbnail.Login(dms, Element);

			foreach (var stream in Streams.GetRange(startIndex, count))
			{
				if (stream.ThumbnailStatus == "Enabled")
				{
					stream.ThumbnailImage = RequestStreamThumbnail(logger, token, stream.ThumbnailLink);
				}
			}

			RowsProcessed += count;

			// logger.Information($"GetThumbnails| RowsProcessed: {RowsProcessed}  Streams: {Streams.Count}");
			lock (_padlock)
			{
				_instances[Element.Name] = this;
			}
		}

		private static string RequestStreamThumbnail(IGQILogger logger, string token, string thumbnailUrl)
		{
			Task<string> downloadTask = Task.Run(async () => await Thumbnail.DownloadImage(logger, token, thumbnailUrl));
			downloadTask.Wait();
			return downloadTask.Result;
		}

		private List<StreamsTable> GetStreamsTable(GQIDMS dms)
		{
			var responseStreamsTable = dms.SendMessage(new GetPartialTableMessage
			{
				DataMinerID = Element.DataMinerID,
				ElementID = Element.ElementID,
				ParameterID = (int)ParameterPids.StreamsTable,
				Filters = new[] { "forceFullTable=true" /*, "column=xx,yy"*/ },
			}) as ParameterChangeEventMessage;

			if (!responseStreamsTable.NewValue.IsArray)
			{
				return new List<StreamsTable>();
			}

			var table = new List<StreamsTable>();

			var cols = responseStreamsTable.NewValue.ArrayValue[0].ArrayValue;
			for (int idxRow = 0; idxRow < cols.Length; idxRow++)
			{
				// start of row 'idxRow'
				table.Add(StreamsTable.CreateStreamRow(responseStreamsTable, idxRow));
			}

			return table;
		}
	}
}