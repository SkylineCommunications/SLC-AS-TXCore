namespace MWCoreDownloadThumbnails_1.Misc
{
	using System;

	using Skyline.DataMiner.Net.Messages;

	internal class StreamsTable
	{
		public string MWEdge { get; set; }

		public string DisplayKey { get; set; }

		public string TsSyncLoss { get; set; }

		public string CcErrorRate { get; set; }

		public string ThumbnailLink { get; set; }

		public string ThumbnailImage { get; set; }

		public string ThumbnailStatus { get; set; }

		public string StreamName { get; set; }

		public string Id { get; set; }

		public string TsAlarm { get; set; }

		public string IpAlarm { get; set; }

		public static StreamsTable CreateStreamRow(ParameterChangeEventMessage responseStreamsTable, int idxRow)
		{
			return new StreamsTable
			{
				Id = responseStreamsTable.NewValue.GetTableCell(idxRow, 0)?.CellValue.GetAsStringValue(),
				DisplayKey = responseStreamsTable.NewValue.GetTableCell(idxRow, 1)?.CellValue.GetAsStringValue(),
				ThumbnailStatus = ParseThumbnailStatus(responseStreamsTable.NewValue.GetTableCell(idxRow, 2)?.CellValue.GetAsStringValue()),
				MWEdge = responseStreamsTable.NewValue.GetTableCell(idxRow, 4)?.CellValue.GetAsStringValue(),
				TsSyncLoss = ParseTsSyncLoss(responseStreamsTable.NewValue.GetTableCell(idxRow, 5)?.CellValue.GetAsStringValue()),
				CcErrorRate = ParseCcErrorRate(responseStreamsTable.NewValue.GetTableCell(idxRow, 8)?.CellValue.GetAsStringValue()),
				ThumbnailLink = responseStreamsTable.NewValue.GetTableCell(idxRow, 9)?.CellValue.GetAsStringValue(),
				StreamName = responseStreamsTable.NewValue.GetTableCell(idxRow, 10)?.CellValue.GetAsStringValue(),
				TsAlarm = ParseAlarmValue(responseStreamsTable.NewValue.GetTableCell(idxRow, 11)?.CellValue.GetAsStringValue()),
				IpAlarm = ParseAlarmValue(responseStreamsTable.NewValue.GetTableCell(idxRow, 12)?.CellValue.GetAsStringValue()),
			};
		}

		private static string ParseThumbnailStatus(string value)
		{
			if (String.IsNullOrWhiteSpace(value) || value == "-1")
			{
				return "N/A";
			}

			return value == "True" ? "Enabled" : "Disabled";
		}

		private static string ParseTsSyncLoss(string value)
		{
			if (String.IsNullOrWhiteSpace(value) || value == "-1")
			{
				return "N/A";
			}

			return value == "1" ? "Yes" : "No";
		}

		private static string ParseCcErrorRate(string value)
		{
			if (String.IsNullOrWhiteSpace(value) || value == "-1")
			{
				return "N/A";
			}

			return $"{value} Errors/s";
		}

		private static string ParseAlarmValue(string value)
		{
			if (String.IsNullOrWhiteSpace(value) || value == "-1")
			{
				return "N/A";
			}

			switch (value)
			{
				case "1":
					return "Critical";

				case "2":
					return "Major";

				case "3":
					return "Minor";

				case "4":
					return "Warning";

				case "5":
					return "Normal";

				case "6":
					return "Error";

				default:
					return "N/A";
			}
		}
	}
}