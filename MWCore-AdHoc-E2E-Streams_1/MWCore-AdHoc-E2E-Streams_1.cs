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
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.Net.Messages;

	internal enum IOType
	{
		Input = 0,
		Output = 1,
	}

	internal enum MWCorePids
	{
		SourcesTablePid = 9600,
		OutputsTablePid = 8900,
		EdgesTablePid = 8500,
	}

	[GQIMetaData(Name = "MWCore E2E Stream")]
	public class GQIDataSourceAdHocE2EStreams : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private GQIStringArgument _argumentEdgeName = new GQIStringArgument("MWEdge Name") { IsRequired = true };
		private GQIStringArgument _argumentElementName = new GQIStringArgument("Element Name") { IsRequired = true };
		private GQIStringArgument _argumentStreamName = new GQIStringArgument("Stream Name") { IsRequired = true };
		private bool _debug = false;
		private GQIDMS _dms;
		private string _edgeName;
		private InputOuput[] _edgesTable;
		private string _elementName;
		private InputOuput[] _iotable;
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
				var responseElement = _dms.SendMessage(new GetElementByNameMessage(_elementName)) as ElementInfoEventMessage;

				_edgesTable = EdgesTable(responseElement);

				if (_edgesTable == default)
				{
					throw new ArgumentException("Edges table not found!");
				}

				_iotable = IOTable(responseElement);
				if (_iotable == default)
				{
					throw new ArgumentException("Sources or Outputs table not found!");
				}

				// Get previous hop - look for outputs connected to the started inputs
				// Get next hop - look for inputs that are connected to the started outputs
				var hops = GetHops();

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

		private bool ContainsIoIp(string edgeName, string ioip)
		{
			var edge = _edgesTable.FirstOrDefault(x => x.Name == edgeName);

			if (edge == default)
			{
				return false;
			}

			bool valid = false;
			var ips = ioip.Split(';');
			foreach (var ip in ips)
			{
				valid = edge.Ip.Contains(ip.Split(':')[0]);
				if (valid)
				{
					break;
				}
			}

			return valid;
		}

		private bool ContainsIoPort(string addresses, string port)
		{
			bool valid = false;
			var addrs = addresses.Split(';');
			foreach (var addr in addrs)
			{
				var tmp = addr.Split(':');

				if (tmp.Length < 2)
				{
					continue;
				}

				valid = tmp[1] == port;
				if (valid)
				{
					break;
				}
			}

			return valid;
		}

		private InputOuput[] EdgesTable(ElementInfoEventMessage responseElement)
		{
			var responseEdgesTable = _dms.SendMessage(new GetPartialTableMessage
			{
				DataMinerID = responseElement.DataMinerID,
				ElementID = responseElement.ElementID,
				ParameterID = 8500, // MWEdges,
				Filters = new[] { "forceFullTable=true" /*, "column=xx,yy"*/ },
			}) as ParameterChangeEventMessage;

			if (!responseEdgesTable.NewValue.IsArray)
			{
				return default;
			}

			var table = new List<InputOuput>();

			var cols = responseEdgesTable.NewValue.ArrayValue[0].ArrayValue;
			for (int idxRow = 0; idxRow < cols.Length; idxRow++)
			{
				// start of row 'idxRow'
				table.Add(new InputOuput
				{
					Id = responseEdgesTable.NewValue.GetTableCell(idxRow, 0)?.CellValue.GetAsStringValue(),
					Name = responseEdgesTable.NewValue.GetTableCell(idxRow, 1)?.CellValue.GetAsStringValue(),
					Ip = responseEdgesTable.NewValue.GetTableCell(idxRow, 9)?.CellValue.GetAsStringValue(),
				});
			}

			if (_debug)
			{
				foreach (var item in table)
				{
					File.AppendAllText(file, $"EDGES|| Id: {item.Id}|Name: {item.Name}|Ip: {item.Ip}|\n");
				}
			}

			return table.ToArray();
		}

		private bool FindNextHop(List<Hop> hops, IEnumerable<InputOuput> startOutputsId, int depth, int maxDepth = 10)
		{
			if (depth > maxDepth)
			{
				return false;
			}

			if (!startOutputsId.Any())
			{
				return false;
			}

			List<InputOuput> nextInputsId = new List<InputOuput>();
			startOutputsId.ForEach(output => FindNextInput(hops, output, nextInputsId, depth));

			if (nextInputsId.Count < 1)
			{
				return false;
			}

			List<InputOuput> nextOutputs = new List<InputOuput>();
			nextInputsId.Distinct().ForEach(input => FindNextOutput(hops, input, nextOutputs, depth));

			return FindNextHop(hops, nextOutputs.Distinct(), ++depth);
		}

		private void FindNextInput(List<Hop> hops, InputOuput output, List<InputOuput> startInputsId, int depth)
		{
			InputOuput[] inputs;
			if (output.Type == "1") // push
			{
				inputs = _iotable.Where(input =>
					(input.IOType == IOType.Input && input.Ip == output.Ip && input.Port == output.Port) ||
					(input.IOType == IOType.Input && ContainsIoIp(input.MWEdge, output.Ip) && input.Port == output.Port))
					.ToArray();
			}
			else
			{
				// listener
				inputs = _iotable.Where(input =>
					(input.IOType == IOType.Input && input.Ip == output.Ip && input.Port == output.Port) ||
					(input.IOType == IOType.Input && ContainsIoIp(output.MWEdge, input.Ip) && (input.Port == output.Port || ContainsIoPort(input.Ip, output.Port))))
					.ToArray();
			}

			if (_debug)
			{
				foreach (var input in _iotable)
				{
					File.AppendAllText(file, $"FIND INPUT| output {output.Type} (1=PUSH/0=LISTENER) | input ip: {input.Ip} | input port: {input.Port}|| contains > IP:{ContainsIoIp(output.MWEdge, input.Ip)} PORT:{ContainsIoPort(input.Ip, output.Port)}\n");

					if (input.IOType == IOType.Input && input.Ip == output.Ip && input.Port == output.Port)
					{
						File.AppendAllText(file, $"FIND INPUT 1| output Listener | input ip: {input.Ip} | input port: {input.Port}\n");
					}
					else if (input.IOType == IOType.Input && ContainsIoIp(output.MWEdge, input.Ip) && input.Port == output.Port)
					{
						File.AppendAllText(file, $"FIND INPUT 2| output Listener | input ip: {input.Ip} | input port: {input.Port}|| contains: {ContainsIoIp(output.MWEdge, input.Ip)}\n");
					}
					else
					{
						File.AppendAllText(file, $"FIND INPUT|No matches found!\n");
					}
				}

				foreach (var item in inputs)
				{
					File.AppendAllText(file, $"FOUND INPUT | input:{item.Name}| input:{item.Stream} | input:{item.MWEdge}\n");
				}
			}

			foreach (var input in inputs)
			{
				hops.Add(new Hop
				{
					Bitrate_Dst = input.Bitrate,
					Bitrate_Src = output.Bitrate,
					Id_Dst = input.Id,
					Id_Src = output.Id,
					IOType = IOType.Output,
					Ip_Dst = input.Ip,
					Ip_Src = output.Ip,
					MWEdge_Dst = input.MWEdge,
					MWEdge_Src = output.MWEdge,
					Name_Dst = input.Name,
					Name_Src = output.Name,
					Port_Dst = input.Port,
					Port_Src = output.Port,
					Stream_Dst = input.Stream,
					Stream_Src = output.Stream,
					Type_Dst = input.Type,
					Type_Src = output.Type,
					Status_Dst = input.Status,
					Status_Src = output.Status,
					Hop_Number = depth + 1,
					IsActive = output.Status == "False",
				});
			}

			startInputsId.AddRange(inputs);
		}

		private void FindNextOutput(List<Hop> hops, InputOuput input, List<InputOuput> nextOutputs, int depth)
		{
			if (_debug)
			{
				File.AppendAllText(file, $"FIND OUTPUT|| INPUT MWEDGE: {input.MWEdge}|Stream: {input.Stream}\n");
			}

			var outputs = _iotable.Where(output =>
				(output.IOType == IOType.Output && output.MWEdge == input.MWEdge && output.Stream == input.Stream))
				.ToArray();

			foreach (var output in outputs)
			{
				hops.Add(new Hop
				{
					Bitrate_Dst = output.Bitrate,
					Bitrate_Src = input.Bitrate,
					Id_Dst = output.Id,
					Id_Src = input.Id,
					IOType = IOType.Input,
					Ip_Dst = output.Ip,
					Ip_Src = input.Ip,
					MWEdge_Dst = output.MWEdge,
					MWEdge_Src = input.MWEdge,
					Name_Dst = output.Name,
					Name_Src = input.Name,
					Port_Dst = output.Port,
					Port_Src = input.Port,
					Stream_Dst = output.Stream,
					Stream_Src = input.Stream,
					Type_Dst = output.Type,
					Type_Src = input.Type,
					Status_Dst = output.Status,
					Status_Src = input.Status,
					Hop_Number = depth + 1,
					IsActive = input.InputState == "True",
				});
			}

			nextOutputs.AddRange(outputs);
		}

		private bool FindPreviousHop(List<Hop> hops, IEnumerable<InputOuput> startInputsId, int depth, int maxDepth = 10)
		{
			if (depth > maxDepth)
			{
				return false;
			}

			if (!startInputsId.Any())
			{
				return false;
			}

			List<InputOuput> previousOutputs = new List<InputOuput>();
			startInputsId.ForEach(input => FindPreviousOutput(hops, input, previousOutputs, depth));

			if (previousOutputs.Count < 1)
			{
				return false;
			}

			List<InputOuput> previousInputsId = new List<InputOuput>();
			previousOutputs.Distinct().ForEach(output => FindPreviousInput(hops, output, previousInputsId, depth));

			return FindPreviousHop(hops, previousInputsId.Distinct(), ++depth);
		}

		private void FindPreviousInput(List<Hop> hops, InputOuput output, List<InputOuput> startInputsId, int depth)
		{
			if (_debug)
			{
				File.AppendAllText(file, $"FIND OUTPUT|| INPUT MWEDGE: {output.MWEdge}|Stream: {output.Stream}\n");
			}

			var inputs = _iotable.Where(input =>
				(input.IOType == IOType.Input && input.MWEdge == output.MWEdge && input.Stream == output.Stream))
				.ToArray();

			foreach (var input in inputs)
			{
				hops.Add(new Hop
				{
					Bitrate_Dst = output.Bitrate,
					Bitrate_Src = input.Bitrate,
					Id_Dst = output.Id,
					Id_Src = input.Id,
					IOType = IOType.Input,
					Ip_Dst = output.Ip,
					Ip_Src = input.Ip,
					MWEdge_Dst = output.MWEdge,
					MWEdge_Src = input.MWEdge,
					Name_Dst = output.Name,
					Name_Src = input.Name,
					Port_Dst = output.Port,
					Port_Src = input.Port,
					Stream_Dst = output.Stream,
					Stream_Src = input.Stream,
					Type_Dst = output.Type,
					Type_Src = input.Type,
					Status_Dst = output.Status,
					Status_Src = input.Status,
					Hop_Number = -depth - 1,
					IsActive = input.InputState == "True",
				});
			}

			startInputsId.AddRange(inputs);
		}

		private void FindPreviousOutput(List<Hop> hops, InputOuput input, List<InputOuput> previousOutputs, int depth)
		{
			InputOuput[] outputs;
			if (input.Type == "1") // listener
			{
				outputs = _iotable.Where(output =>
					(output.IOType == IOType.Output && input.Ip == output.Ip && input.Port == output.Port) ||
					(output.IOType == IOType.Output && ContainsIoIp(input.MWEdge, output.Ip) && input.Port == output.Port))
					.ToArray();
			}
			else
			{
				// pull
				outputs = _iotable.Where(output =>
					(output.IOType == IOType.Output && input.Ip == output.Ip && input.Port == output.Port) ||
					(output.IOType == IOType.Output && ContainsIoIp(output.MWEdge, input.Ip) && (input.Port == output.Port || ContainsIoPort(input.Ip, output.Port))))
					.ToArray();
			}

			if (_debug)
			{
				foreach (var output in _iotable)
				{
					File.AppendAllText(file, $"FIND INPUT| output {input.Type == "1"} (1=PUSH/0=LISTENER) | input ip: {output.Ip} | input port: {output.Port}|| contains: {ContainsIoIp(input.MWEdge, output.Ip)}\n");

					if (output.IOType == IOType.Input && output.Ip == input.Ip && output.Port == input.Port)
					{
						File.AppendAllText(file, $"FIND INPUT 1| output Listener | input ip: {output.Ip} | input port: {output.Port}\n");
					}
					else if (output.IOType == IOType.Input && ContainsIoIp(input.MWEdge, output.Ip) && output.Port == input.Port)
					{
						File.AppendAllText(file, $"FIND INPUT 2| output Listener | input ip: {output.Ip} | input port: {output.Port}|| contains: {ContainsIoIp(input.MWEdge, output.Ip)}\n");
					}
					else
					{
						File.AppendAllText(file, $"FIND INPUT|No matches found!");
					}
				}

				foreach (var item in outputs)
				{
					File.AppendAllText(file, $"FOUND INPUT | input:{item.Name}| input:{item.Stream} | input:{item.MWEdge}\n");
				}
			}

			foreach (var output in outputs)
			{
				hops.Add(new Hop
				{
					Bitrate_Dst = input.Bitrate,
					Bitrate_Src = output.Bitrate,
					Id_Dst = input.Id,
					Id_Src = output.Id,
					IOType = IOType.Output,
					Ip_Dst = input.Ip,
					Ip_Src = output.Ip,
					MWEdge_Dst = input.MWEdge,
					MWEdge_Src = output.MWEdge,
					Name_Dst = input.Name,
					Name_Src = output.Name,
					Port_Dst = input.Port,
					Port_Src = output.Port,
					Stream_Dst = input.Stream,
					Stream_Src = output.Stream,
					Type_Dst = input.Type,
					Type_Src = output.Type,
					Status_Dst = input.Status,
					Status_Src = output.Status,
					Hop_Number = -depth - 1,
					IsActive = output.Status == "False",
				});
			}

			previousOutputs.AddRange(outputs);
		}

		private List<Hop> GetHops()
		{
			List<Hop> hops = new List<Hop>();

			var inputs = _iotable.Where(input =>
				(input.IOType == IOType.Input && input.MWEdge == _edgeName && input.Stream == _streamName))
				.ToArray();

			var outputs = _iotable.Where(output =>
				(output.IOType == IOType.Output && output.MWEdge == _edgeName && output.Stream == _streamName))
				.ToArray();

			if (_debug)
			{
				File.AppendAllText(file, $"GET HOPS||inputs count: {inputs.Length}|outputs count:{outputs.Length}|edge:{_edgeName}|stream:{_streamName}\n");
			}

			foreach (var input in inputs)
			{
				foreach (var output in outputs)
				{
					hops.Add(new Hop
					{
						Bitrate_Dst = output.Bitrate,
						Bitrate_Src = input.Bitrate,
						Id_Dst = output.Id,
						Id_Src = input.Id,
						IOType = IOType.Input,
						Ip_Dst = output.Ip,
						Ip_Src = input.Ip,
						MWEdge_Dst = output.MWEdge,
						MWEdge_Src = input.MWEdge,
						Name_Dst = output.Name,
						Name_Src = input.Name,
						Port_Dst = output.Port,
						Port_Src = input.Port,
						Stream_Dst = output.Stream,
						Stream_Src = input.Stream,
						Type_Dst = output.Type,
						Type_Src = input.Type,
						Status_Dst = output.Status,
						Status_Src = input.Status,
						Starting_Point = true,
						Hop_Number = 0,
						IsActive = input.InputState == "True",
					});
				}
			}

			FindPreviousHop(hops, inputs.Distinct(), 0);
			FindNextHop(hops, outputs.Distinct(), 0);

			return hops;
		}

		private InputOuput[] IOTable(ElementInfoEventMessage responseElement)
		{
			var responseInputsTable = _dms.SendMessage(new GetPartialTableMessage
			{
				DataMinerID = responseElement.DataMinerID,
				ElementID = responseElement.ElementID,
				ParameterID = 9600, // inputsPid,
				Filters = new[] { "forceFullTable=true" /*, "column=xx,yy"*/ },
			}) as ParameterChangeEventMessage;

			var responseOutputsTable = _dms.SendMessage(new GetPartialTableMessage
			{
				DataMinerID = responseElement.DataMinerID,
				ElementID = responseElement.ElementID,
				ParameterID = 8900, // outputsPid,
				Filters = new[] { "forceFullTable=true" /*, "column=xx,yy"*/ },
			}) as ParameterChangeEventMessage;

			if (!responseInputsTable.NewValue.IsArray ||
				!responseOutputsTable.NewValue.IsArray)
			{
				return default;
			}

			var iotable = new List<InputOuput>();

			var cols = responseInputsTable.NewValue.ArrayValue[0].ArrayValue;
			for (int idxRow = 0; idxRow < cols.Length; idxRow++)
			{
				// start of row 'idxRow'
				iotable.Add(new InputOuput
				{
					Id = responseInputsTable.NewValue.GetTableCell(idxRow, 0)?.CellValue.GetAsStringValue(),
					Name = responseInputsTable.NewValue.GetTableCell(idxRow, 1)?.CellValue.GetAsStringValue(),
					Status = responseInputsTable.NewValue.GetTableCell(idxRow, 20)?.CellValue.GetAsStringValue(),
					IOType = IOType.Input,
					Stream = responseInputsTable.NewValue.GetTableCell(idxRow, 23)?.CellValue.GetAsStringValue(),
					MWEdge = responseInputsTable.NewValue.GetTableCell(idxRow, 59)?.CellValue.GetAsStringValue(),
					Ip = responseInputsTable.NewValue.GetTableCell(idxRow, 58)?.CellValue.GetAsStringValue(),
					Port = responseInputsTable.NewValue.GetTableCell(idxRow, 7)?.CellValue.GetAsStringValue(),
					Type = responseInputsTable.NewValue.GetTableCell(idxRow, 5)?.CellValue.GetAsStringValue(),
					Bitrate = responseInputsTable.NewValue.GetTableCell(idxRow, 33)?.CellValue.DoubleValue,
					InputState = responseInputsTable.NewValue.GetTableCell(idxRow, 55)?.CellValue.GetAsStringValue(),
				});
			}

			cols = responseOutputsTable.NewValue.ArrayValue[0].ArrayValue;
			for (int idxRow = 0; idxRow < cols.Length; idxRow++)
			{
				// start of row 'idxRow'
				iotable.Add(new InputOuput
				{
					Id = responseOutputsTable.NewValue.GetTableCell(idxRow, 0)?.CellValue.GetAsStringValue(),
					Name = responseOutputsTable.NewValue.GetTableCell(idxRow, 1)?.CellValue.GetAsStringValue(),
					Status = responseOutputsTable.NewValue.GetTableCell(idxRow, 2)?.CellValue.GetAsStringValue(),
					IOType = IOType.Output,
					Stream = responseOutputsTable.NewValue.GetTableCell(idxRow, 9)?.CellValue.GetAsStringValue(),
					MWEdge = responseOutputsTable.NewValue.GetTableCell(idxRow, 10)?.CellValue.GetAsStringValue(),
					Ip = responseOutputsTable.NewValue.GetTableCell(idxRow, 4)?.CellValue.GetAsStringValue(),
					Port = responseOutputsTable.NewValue.GetTableCell(idxRow, 5)?.CellValue.GetAsStringValue(),
					Type = responseOutputsTable.NewValue.GetTableCell(idxRow, 15)?.CellValue.GetAsStringValue(),
					Bitrate = responseOutputsTable.NewValue.GetTableCell(idxRow, 27)?.CellValue.DoubleValue,
				});
			}

			if (_debug)
			{
				File.AppendAllText(file, "Finished getting sources and outputs \n");

				foreach (var item in iotable)
				{
					File.AppendAllText(file, $"IOTYPE: {item.IOType}|Id: {item.Id}|Name: {item.Name}|Status: {item.Status}|Stream: {item.Stream}|MWEdge: {item.MWEdge}|Ip: {item.Ip}|Port: {item.Port}|Type: {item.Type}|\n");
				}
			}

			return iotable.ToArray();
		}
	}

	internal class Hop
	{
		public double? Bitrate_Dst { get; set; }

		public double? Bitrate_Src { get; set; }

		public int Hop_Number { get; set; }

		public string Id_Dst { get; set; }

		public string Id_Src { get; set; }

		public IOType IOType { get; set; }

		public string Ip_Dst { get; set; }

		public string Ip_Src { get; set; }

		public bool IsActive { get; set; }

		public string MWEdge_Dst { get; set; }

		public string MWEdge_Src { get; set; }

		public string Name_Dst { get; set; }

		public string Name_Src { get; set; }

		public string Port_Dst { get; set; }

		public string Port_Src { get; set; }

		public bool Starting_Point { get; set; }

		public string Status_Dst { get; set; }

		public string Status_Src { get; set; }

		public string Stream_Dst { get; set; }

		public string Stream_Src { get; set; }

		public string Type_Dst { get; set; }

		public string Type_Src { get; set; } // pull, listener/push
	}

	internal class InputOuput
	{
		public double? Bitrate { get; set; }

		public string Id { get; set; }

		public string InputState { get; set; }

		public IOType IOType { get; set; }

		public string Ip { get; set; }

		public string MWEdge { get; set; }

		public string Name { get; set; }

		public string Port { get; set; }

		public string Status { get; set; }

		public string Stream { get; set; }

		public string Type { get; set; } // pull, listener/push
	}
}