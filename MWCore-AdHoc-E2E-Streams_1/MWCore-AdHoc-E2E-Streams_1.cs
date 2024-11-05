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
****************************************************************************
*/

namespace GQIIntegrationSPI
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using MWCoreAdHocE2EStreams_1.Misc;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;

	[GQIMetaData(Name = "MWCore E2E Stream")]
	public class GQIDataSourceAdHocE2EStreams : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private GQIStringArgument _argumentEdgeName = new GQIStringArgument("MWEdge Name") { IsRequired = true };
		private GQIStringArgument _argumentElementName = new GQIStringArgument("Element Name") { IsRequired = true };
		private GQIStringArgument _argumentStreamName = new GQIStringArgument("Stream Name") { IsRequired = true };
		private bool _debug = false;
		private GQIDMS _dms;
		private string _edgeName;
		private string _elementName;
		private string _streamName;
		private string file = $"C:\\Users\\FlavioME\\Downloads\\e2eLog-{DateTime.Now.ToOADate()}.txt";

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("IO ID SRC"),
				new GQIStringColumn("IO Name SRC"),
				new GQIStringColumn("IO SRC"),
				new GQIStringColumn("IO State SRC"),
				new GQIStringColumn("IO Type SRC"),
				new GQIDoubleColumn("Bitrate SRC"),
				new GQIStringColumn("Stream Name SRC"),
				new GQIStringColumn("Edge Name SRC"),
				new GQIStringColumn("IO ID DST"),
				new GQIStringColumn("IO Name DST"),
				new GQIStringColumn("IO State DST"),
				new GQIStringColumn("IO Type DST"),
				new GQIDoubleColumn("Bitrate DST"),
				new GQIStringColumn("Stream Name DST"),
				new GQIStringColumn("Edge Name DST"),
				new GQIBooleanColumn("Starting Point"),
				new GQIIntColumn("Hop"),
				new GQIBooleanColumn("Active"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[] { _argumentElementName, _argumentEdgeName, _argumentStreamName };
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			GQIRow[] data2retrieve;
			if (_debug)
			{
				string text = $"--------{DateTime.Now}---------{Environment.NewLine}";
				File.WriteAllText(file, text);
			}

			try
			{
				var streams = StreamsIO.Instance(_dms, _elementName);
				var hops = StreamsIO.GetHops(streams, _edgeName, _streamName);

				if (_debug)
				{
					File.AppendAllText(file, "Ended GetHops loop!\n");
				}

				List<GQIRow> rows = new List<GQIRow>();
				foreach (var hop in hops)
				{
					rows.Add(new GQIRow(new[]
						{
						new GQICell { Value = hop.Id_Src}, // IO ID SRC
						new GQICell { Value = hop.Name_Src}, // IO Name SRC
						new GQICell { Value = hop.IOType == 0 ? "Source" : "Output" }, // IO Type SRC
						new GQICell { Value = hop.Status_Src}, // IO State SRC
						new GQICell { Value = hop.Type_Src}, // IO Type SRC
						new GQICell { Value = hop.Bitrate_Src}, // Bitrate SRC
						new GQICell { Value = hop.Stream_Src}, // Stream Name SRC
						new GQICell { Value = hop.MWEdge_Src}, // Edge Name SRC
						new GQICell { Value = hop.Id_Dst}, // IO ID SRC
						new GQICell { Value = hop.Name_Dst}, // IO Name SRC
						new GQICell { Value = hop.Status_Dst}, // IO State SRC
						new GQICell { Value = hop.Type_Dst}, // IO Type SRC
						new GQICell { Value = hop.Bitrate_Dst}, // Bitrate SRC
						new GQICell { Value = hop.Stream_Dst}, // Stream Name SRC
						new GQICell { Value = hop.MWEdge_Dst}, // Edge Name SRC
						new GQICell { Value = hop.Hop_Number == 1 && hop.IOType != 0 ? true : hop.Starting_Point},
						new GQICell { Value = hop.Hop_Number},
						new GQICell { Value = hop.IsActive},
					}));
				}

				// adding an extra line to facilitate the node-edge component
				//var lastHop = hops.OrderBy(x => x.Hop_Number).Last();
				var lastOutputs = hops.Where(x => x.IOType == IOType.Input && x.Hop_Number == hops.Max(y => y.Hop_Number));

				foreach (var lastHop in lastOutputs)
				{
					rows.Add(new GQIRow(new[]
							{
							new GQICell { Value = lastHop.Id_Dst }, // IO ID SRC
							new GQICell { Value = lastHop.Name_Dst }, // IO Name SRC
							new GQICell { Value = "Output" }, // IO Type SRC
							new GQICell { Value = lastHop.Status_Dst }, // IO State SRC
							new GQICell { Value = lastHop.Type_Dst }, // IO Type SRC
							new GQICell { Value = lastHop.Bitrate_Dst }, // Bitrate SRC
							new GQICell { Value = lastHop.Stream_Dst }, // Stream Name SRC
							new GQICell { Value = lastHop.MWEdge_Dst }, // Edge Name SRC
							new GQICell { Value = string.Empty }, // IO ID SRC
							new GQICell { Value = string.Empty }, // IO Name SRC
							new GQICell { Value = string.Empty }, // IO State SRC
							new GQICell { Value = string.Empty }, // IO Type SRC
							new GQICell { Value = 0d }, // Bitrate SRC
							new GQICell { Value = string.Empty }, // Stream Name SRC
							new GQICell { Value = string.Empty }, // Edge Name SRC
							new GQICell { Value = lastHop.Starting_Point},
							new GQICell { Value = lastHop.Hop_Number},
							new GQICell { Value = lastHop.IsActive},
						}));
				}

				data2retrieve = rows.ToArray();
			}
			catch (Exception ex)
			{
				if (_debug)
				{
					File.AppendAllText(file, $"Exception: {ex}\n");
				}

				data2retrieve = new GQIRow[0];
			}
			return new GQIPage(data2retrieve)
			{
				HasNextPage = false,
			};
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_elementName = args.GetArgumentValue(_argumentElementName);
			_edgeName = args.GetArgumentValue(_argumentEdgeName);
			_streamName = args.GetArgumentValue(_argumentStreamName);
			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			return default;
		}
	}
}