/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

dd/mm/2023	1.0.0.1		FME, Skyline	Initial version
12/02/2025	1.0.0.2		SDT, Skyline	Base64 thumbnails image.
****************************************************************************
*/

namespace DownloadThumbnails_1
{
	using System;
	using System.Collections.Generic;

	using MWCoreDownloadThumbnails_1.Misc;

	using Skyline.DataMiner.Analytics.GenericInterface;

	[GQIMetaData(Name = "MWCore Thumbnails")]
	public class GQIDataSourceAdHocThumbnails : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const int _pageSize = 100;
		private readonly GQIStringArgument _argumentElementName = new GQIStringArgument("Element Name") { IsRequired = true };

		private GQIDMS _dms;
		private IGQILogger _logger;
		private string _elementName;
		private int rowsReturned;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("ID"),
				new GQIStringColumn("Display Key"),
				new GQIStringColumn("MWEdge"),
				new GQIStringColumn("Stream Name"),
				new GQIStringColumn("Thumbnail Status"),
				new GQIStringColumn("Thumbnail Link"),
				new GQIStringColumn("Thumbnail"),
				new GQIStringColumn("TS Sync Loss"),
				new GQIStringColumn("CC Error Rate"),
				new GQIStringColumn("TS Alarm"),
				new GQIStringColumn("IP Alarm"),
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			GQIRow[] data2retrieve;
			bool hasNextPage = false;
			try
			{
				var element = ElementCache.Instance(_dms, _logger, _elementName, _pageSize);

				element.GetStreamThumbnails(_dms, _logger);

				List<GQIRow> rows = new List<GQIRow>();

				int startIndex = rowsReturned == 0 ? 0 : rowsReturned - 1;

				int count = (startIndex + _pageSize) > element.Streams.Count ? element.Streams.Count - startIndex : _pageSize;

				foreach (var stream in element.Streams.GetRange(startIndex, count))
				{
					rows.Add(new GQIRow(new[]
					{
						new GQICell { Value = stream.Id},
						new GQICell { Value = stream.DisplayKey},
						new GQICell { Value = stream.MWEdge},
						new GQICell { Value = stream.StreamName},
						new GQICell { Value = stream.ThumbnailStatus},
						new GQICell { Value = stream.ThumbnailLink},
						new GQICell { Value = stream.ThumbnailImage},
						new GQICell { Value = stream.TsSyncLoss},
						new GQICell { Value = stream.CcErrorRate},
						new GQICell { Value = stream.TsAlarm},
						new GQICell { Value = stream.IpAlarm},
					}));
				}

				data2retrieve = rows.ToArray();
				rowsReturned += count;
				hasNextPage = rowsReturned < element.Streams.Count;

				// _logger.Information($"HasNextPage: {hasNextPage}  RowsReturned: {rowsReturned}  Streams: {element.Streams.Count}");
			}
			catch (Exception ex)
			{
				_logger.Error($"Exception: {ex}");

				data2retrieve = new GQIRow[0];
			}

			return new GQIPage(data2retrieve)
			{
				HasNextPage = hasNextPage,
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[] { _argumentElementName };
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_elementName = args.GetArgumentValue(_argumentElementName);
			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			return default;
		}
	}
}