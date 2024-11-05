namespace MWCoreAdHocE2EStreams_1.Misc
{
	using System;
	using System.Collections.Generic;
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
		SourcesStatisticsTablePid = 11200,
		OutputsTablePid = 8900,
		OutputsStatisticsTablePid = 11400,
		EdgesTablePid = 8500,
	}

	internal sealed class StreamsIO
	{
		private const double _cachingTime = 300;
		private static readonly object _padlock = new object();
		private static Dictionary<string, ElementCache> _instances = new Dictionary<string, ElementCache>();
		private readonly GQIDMS _dms;

		private StreamsIO(GQIDMS dms, string elementName)
		{
			_dms = dms;

			var responseElement = dms.SendMessage(new GetElementByNameMessage(elementName)) as ElementInfoEventMessage;

			var edgesTable = GetEdgesTable(responseElement);

			if (edgesTable == default)
			{
				throw new ArgumentException("Edges table not found!");
			}

			EdgesTable = edgesTable;

			var inputsTable = GetInputsTable(responseElement);
			if (inputsTable == default)
			{
				throw new ArgumentException("Sources table not found!");
			}

			InputsTable = inputsTable;

			var outputsTable = GetOutputsTable(responseElement);
			if (outputsTable == default)
			{
				throw new ArgumentException("Outputs table not found!");
			}

			OutputsTable = outputsTable;
		}

		public IEnumerable<EdgeTable> EdgesTable { get; }

		public IEnumerable<Iotable> InputsTable { get; }

		public IEnumerable<Iotable> OutputsTable { get; }

		/// <summary>
		/// Get previous hop - look for outputs connected to the started inputs.
		/// Get next hop - look for inputs that are connected to the started outputs.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="edgeName"></param>
		/// <param name="streamName"></param>
		/// <returns></returns>
		public static List<Hop> GetHops(StreamsIO instance, string edgeName, string streamName)
		{
			List<Hop> hops = new List<Hop>();

			var inputs = instance.InputsTable.Where(input =>
				(input.Stream == streamName && input.MWEdge == edgeName && !input.Protocol.Equals("2022-7")))
				.ToArray();

			var outputs = instance.OutputsTable.Where(output =>
				(output.Stream == streamName && output.MWEdge == edgeName && !output.Protocol.Equals("2022-7")))
				.ToArray();

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

			// Get previous hop - look for outputs connected to the started inputs.
			FindPreviousHop(instance, hops, inputs.Distinct(), 0);

			// Get next hop - look for inputs that are connected to the started outputs.
			FindNextHop(instance, hops, outputs.Distinct(), 0);

			return hops;
		}

		public static StreamsIO Instance(GQIDMS dms, string elementName)
		{
			var now = DateTime.UtcNow;
			ElementCache instance;

			if (!_instances.TryGetValue(elementName, out instance) ||
				(now - instance.LastRun) > TimeSpan.FromSeconds(_cachingTime))
			{
				lock (_padlock)
				{
					if (!_instances.TryGetValue(elementName, out instance) ||
						(now - instance.LastRun) > TimeSpan.FromSeconds(_cachingTime))
					{
						instance = new ElementCache(new StreamsIO(dms, elementName), now);
						_instances[elementName] = instance;
					}
				}
			}

			return instance.Instance;
		}

		private static bool ContainsIoIp(StreamsIO streamsIO, string edgeName, string ioip)
		{
			var edge = streamsIO.EdgesTable.FirstOrDefault(x => x.Name == edgeName);

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

		private static bool ContainsIoPort(string addresses, string port)
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

		private static bool FindNextHop(StreamsIO streamsIO, List<Hop> hops, IEnumerable<Iotable> startOutputsId, int depth, int maxDepth = 10)
		{
			if (depth > maxDepth)
			{
				return false;
			}

			if (!startOutputsId.Any())
			{
				return false;
			}

			List<Iotable> nextInputsId = new List<Iotable>();
			startOutputsId.ForEach(output => FindNextInput(streamsIO, hops, output, nextInputsId, depth));

			if (nextInputsId.Count < 1)
			{
				return false;
			}

			List<Iotable> nextOutputs = new List<Iotable>();
			nextInputsId.Distinct().ForEach(input => FindNextOutput(streamsIO, hops, input, nextOutputs, depth));

			return FindNextHop(streamsIO, hops, nextOutputs.Distinct(), ++depth);
		}

		private static void FindNextInput(StreamsIO streamsIO, List<Hop> hops, Iotable output, List<Iotable> startInputsId, int depth)
		{
			
			Iotable[] inputs;
			if (output.Type != "0") // push or NA -7
			{
				inputs = streamsIO.InputsTable.Where(input =>
					(input.Ip == output.Ip || ContainsIoIp(streamsIO, input.MWEdge, output.Ip))
					&& input.Port == output.Port)
					.ToArray();
			}
			else
			{
				// listener
				inputs = streamsIO.InputsTable.Where(input =>
					(input.Ip == output.Ip || ContainsIoIp(streamsIO, output.MWEdge, input.Ip))
					&& input.Port == output.Port)
					.ToArray();
			}
			
			/*
			var inputs = streamsIO.InputsTable.Where(input =>
					(input.Ip == output.Ip || ContainsIoIp(streamsIO, output.MWEdge, input.Ip))
					&& input.Port == output.Port)
					.ToArray();
			*/

			if (inputs.Length > 0)
			{
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
			}
			else
			{
				hops.Add(new Hop
				{
					Bitrate_Dst = 0,
					Bitrate_Src = output.Bitrate,
					Id_Dst = string.Empty,
					Id_Src = output.Id,
					IOType = IOType.Output,
					Ip_Dst = string.Empty,
					Ip_Src = output.Ip,
					MWEdge_Dst = string.Empty,
					MWEdge_Src = output.MWEdge,
					Name_Dst = string.Empty,
					Name_Src = output.Name,
					Port_Dst = string.Empty,
					Port_Src = output.Port,
					Stream_Dst = string.Empty,
					Stream_Src = output.Stream,
					Type_Dst = string.Empty,
					Type_Src = output.Type,
					Status_Dst = string.Empty,
					Status_Src = output.Status,
					Hop_Number = depth + 1,
					IsActive = output.Status == "False",
				});
			}

			startInputsId.AddRange(inputs);
		}

		private static void FindNextOutput(StreamsIO streamsIO, List<Hop> hops, Iotable input, List<Iotable> nextOutputs, int depth)
		{
			var outputs = streamsIO.OutputsTable.Where(output =>
				output.Stream == input.Stream && output.MWEdge == input.MWEdge)
				.ToArray();

			foreach (var output in outputs)
			{
				if (output.Port == "-1")
				{
					continue;
				}


				if (output.Protocol == "2022-7")
				{
					// TODO RESUME
				}
				
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

		private static bool FindPreviousHop(StreamsIO streamsIO, List<Hop> hops, IEnumerable<Iotable> startInputsId, int depth, int maxDepth = 10)
		{
			if (depth > maxDepth)
			{
				return false;
			}

			if (!startInputsId.Any())
			{
				return false;
			}

			List<Iotable> previousOutputs = new List<Iotable>();
			startInputsId.ForEach(input => FindPreviousOutput(streamsIO, hops, input, previousOutputs, depth));

			if (previousOutputs.Count < 1)
			{
				return false;
			}

			List<Iotable> previousInputsId = new List<Iotable>();
			previousOutputs.Distinct().ForEach(output => FindPreviousInput(streamsIO, hops, output, previousInputsId, depth));

			return FindPreviousHop(streamsIO, hops, previousInputsId.Distinct(), ++depth);
		}

		private static void FindPreviousInput(StreamsIO streamsIO, List<Hop> hops, Iotable output, List<Iotable> startInputsId, int depth)
		{
			var inputs = streamsIO.InputsTable.Where(input =>
				input.Stream == output.Stream && input.MWEdge == output.MWEdge)
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

		private static void FindPreviousOutput(StreamsIO streamsIO, List<Hop> hops, Iotable input, List<Iotable> previousOutputs, int depth)
		{
			Iotable[] outputs;
			if (input.Type == "1") // listener
			{
				outputs = streamsIO.OutputsTable.Where(output =>
					(input.Ip == output.Ip || ContainsIoIp(streamsIO, input.MWEdge, output.Ip))
					&& input.Port == output.Port)
					.ToArray();
			}
			else
			{
				// pull
				outputs = streamsIO.OutputsTable.Where(output =>
					(input.Ip == output.Ip || ContainsIoIp(streamsIO, input.MWEdge, output.Ip))
					&& input.Port == output.Port)
					.ToArray();
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

		private IEnumerable<EdgeTable> GetEdgesTable(ElementInfoEventMessage responseElement)
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

			var table = new List<EdgeTable>();

			var cols = responseEdgesTable.NewValue.ArrayValue[0].ArrayValue;
			for (int idxRow = 0; idxRow < cols.Length; idxRow++)
			{
				// start of row 'idxRow'
				table.Add(new EdgeTable
				{
					Id = responseEdgesTable.NewValue.GetTableCell(idxRow, 0)?.CellValue.GetAsStringValue(),
					Name = responseEdgesTable.NewValue.GetTableCell(idxRow, 1)?.CellValue.GetAsStringValue(),
					Ip = responseEdgesTable.NewValue.GetTableCell(idxRow, 9)?.CellValue.GetAsStringValue(),
				});
			}

			return table;
		}

		private IEnumerable<Iotable> GetInputsTable(ElementInfoEventMessage responseElement)
		{
			var responseInputsTable = _dms.SendMessage(new GetPartialTableMessage
			{
				DataMinerID = responseElement.DataMinerID,
				ElementID = responseElement.ElementID,
				ParameterID = (int)MWCorePids.SourcesTablePid, // inputsPid,
				Filters = new[] { "forceFullTable=true" /*, "column=xx,yy"*/ },
			}) as ParameterChangeEventMessage;

			/*
			var responseInputsStatisticsTable = _dms.SendMessage(new GetPartialTableMessage
			{
				DataMinerID = responseElement.DataMinerID,
				ElementID = responseElement.ElementID,
				ParameterID = (int)MWCorePids.SourcesStatisticsTablePid,
				Filters = new[] { "forceFullTable=true" },
			}) as ParameterChangeEventMessage;
			*/

			if (!responseInputsTable.NewValue.IsArray)
			{
				return default;
			}

			var iotable = new List<Iotable>();
			string pk;
			var cols = responseInputsTable.NewValue.ArrayValue[0].ArrayValue;
			for (int idxRow = 0; idxRow < cols.Length; idxRow++)
			{
				// start of row 'idxRow'
				//pk = responseInputsTable.NewValue.GetTableCell(idxRow, 0)?.CellValue.GetAsStringValue();
				//var telemetryPk = responseInputsStatisticsTable.NewValue.GetTableCell(idxRow, 33);
				iotable.Add(new Iotable
				{
					Id = responseInputsTable.NewValue.GetTableCell(idxRow, 0)?.CellValue.GetAsStringValue(),
					Name = responseInputsTable.NewValue.GetTableCell(idxRow, 1)?.CellValue.GetAsStringValue(),
					Status = responseInputsTable.NewValue.GetTableCell(idxRow, 20)?.CellValue.GetAsStringValue(),
					IOType = IOType.Input,
					Stream = responseInputsTable.NewValue.GetTableCell(idxRow, 23)?.CellValue.GetAsStringValue(),
					MWEdge = responseInputsTable.NewValue.GetTableCell(idxRow, 31)?.CellValue.GetAsStringValue(),
					Ip = responseInputsTable.NewValue.GetTableCell(idxRow, 30)?.CellValue.GetAsStringValue(),
					Port = responseInputsTable.NewValue.GetTableCell(idxRow, 7)?.CellValue.GetAsStringValue(),
					Type = responseInputsTable.NewValue.GetTableCell(idxRow, 5)?.CellValue.GetAsStringValue(),
					Bitrate = 0, //responseInputsStatisticsTable.NewValue.GetTableCell(idxRow, 33)?.CellValue.DoubleValue//responseInputsTable.NewValue.GetTableCell(idxRow, 33)?.CellValue.DoubleValue,
					InputState = responseInputsTable.NewValue.GetTableCell(idxRow, 27)?.CellValue.GetAsStringValue(),
					Protocol = responseInputsTable.NewValue.GetTableCell(idxRow, 3)?.CellValue.GetAsStringValue(),
				});
			}

			return iotable;
		}

		private IEnumerable<Iotable> GetOutputsTable(ElementInfoEventMessage responseElement)
		{
			var responseOutputsTable = _dms.SendMessage(new GetPartialTableMessage
			{
				DataMinerID = responseElement.DataMinerID,
				ElementID = responseElement.ElementID,
				ParameterID = (int)MWCorePids.OutputsTablePid, // outputsPid,
				Filters = new[] { "forceFullTable=true" },
			}) as ParameterChangeEventMessage;

			if (!responseOutputsTable.NewValue.IsArray)
			{
				return default;
			}

			var iotable = new List<Iotable>();
			var cols = responseOutputsTable.NewValue.ArrayValue[0].ArrayValue;
			for (int idxRow = 0; idxRow < cols.Length; idxRow++)
			{
				// start of row 'idxRow'
				iotable.Add(new Iotable
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
					Protocol = responseOutputsTable.NewValue.GetTableCell(idxRow, 3)?.CellValue.GetAsStringValue(),
					Bitrate = 0,//responseOutputsTable.NewValue.GetTableCell(idxRow, 27)?.CellValue.DoubleValue,
				});
			}

			return iotable;
		}
	}
}