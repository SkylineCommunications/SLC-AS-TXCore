// Ignore Spelling: dms

namespace MWCoreAdHocE2EStreams_1.Misc
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using global::Misc.Enums;

	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.Net.Messages;

	internal sealed class StreamsIO
	{
		private const double _cachingTime = 300;
		private static readonly object _padlock = new object();
		private static Dictionary<string, ElementCache> _instances = new Dictionary<string, ElementCache>();
		private readonly GQIDMS _dms;
		private Dictionary<string, HashSet<Hop>> _hops;

		private StreamsIO(GQIDMS dms, string elementName)
		{
			_dms = dms;

			var responseElement = dms.SendMessage(new GetElementByNameMessage(elementName)) as ElementInfoEventMessage;

			var edgesTable = GetEdgesTable(responseElement);

			if (!edgesTable.Any())
			{
				throw new ArgumentException("Edges table not found!");
			}

			EdgesTable = edgesTable;

			var inputsTable = GetInputsTable(responseElement);
			if (!inputsTable.Any())
			{
				throw new ArgumentException("Sources table not found!");
			}

			InputsTable = inputsTable;

			var outputsTable = GetOutputsTable(responseElement);
			if (!outputsTable.Any())
			{
				throw new ArgumentException("Outputs table not found!");
			}

			OutputsTable = outputsTable;

			_hops = new Dictionary<string, HashSet<Hop>>();
		}

		public IEnumerable<EdgeTable> EdgesTable { get; }

		public IEnumerable<Iotable> InputsTable { get; }

		public IEnumerable<Iotable> OutputsTable { get; }

		public static StreamsIO Instance(GQIDMS dms, IGQILogger logger, string elementName = "", bool forceRefresh = false)
		{
			var now = DateTime.UtcNow;
			ElementCache instance;
			logger.Debug($"Element: {elementName}");

			if (forceRefresh /*&& !_instances.Keys.Any()*/)
			{
				var newInstances = new Dictionary<string, ElementCache>();
				DMSMessage[] responseElement = dms.SendMessages(GetLiteElementInfo.ByProtocol("Techex MWCore", "Production"));// as LiteElementInfoEvent;
				foreach (LiteElementInfoEvent item in responseElement)
				{
					logger.Debug($"Element: {item.Name}");
					instance = new ElementCache(new StreamsIO(dms, item.Name), now);
					newInstances[item.Name] = instance;
				}

				lock (_padlock)
				{
					_instances = newInstances;
				}

				return default;
			}
			else
			{
				lock (_padlock)
				{
					if (!_instances.TryGetValue(elementName, out instance) ||
						(now - instance.LastRun) > TimeSpan.FromSeconds(_cachingTime))
					{
						instance = new ElementCache(new StreamsIO(dms, elementName), now);
						_instances[elementName] = instance;
					}

					return instance.Instance;
				}
			}
		}

		/// <summary>
		/// Get previous hop - look for outputs connected to the started inputs.
		/// Get next hop - look for inputs that are connected to the started outputs.
		/// </summary>
		/// <param name="instance">An instance of <see cref="StreamsIO"/>, representing the data structure containing inputs and outputs.</param>
		/// <param name="edgeName">The name of the edge to filter connections.</param>
		/// <param name="streamName">The name of the stream to filter connections.</param>
		/// <returns>
		/// A list of <see cref="Hop"/> objects, each representing a connection between inputs and outputs along with additional metadata.
		/// </returns>
		public HashSet<Hop> GetHops(string edgeName, string streamName)
		{
			HashSet<Hop> hops;
			string key = $"{edgeName}/{streamName}";
			if (_hops.TryGetValue(key, out hops))
			{
				return hops;
			}
			else
			{
				hops = new HashSet<Hop>();
			}

			var inputs = new HashSet<Iotable>(InputsTable.Where(input =>
				input.Stream == streamName && input.MWEdge == edgeName && !input.Protocol.Equals("2022-7")));

			var outputs = new HashSet<Iotable>(OutputsTable.Where(output =>
				output.Stream == streamName && output.MWEdge == edgeName && output.Port != "-1"));

			foreach (var input in inputs)
			{
				foreach (var output in outputs)
				{
					hops.Add(Hop.CreateHop(input, output, IOType.Input, 0, input.InputState == "True", true));
				}
			}

			// Get next hop - look for inputs that are connected to the started outputs.
			FindNextHop(this, hops, outputs, 0);

			// Get previous hop - look for outputs connected to the started inputs.
			FindPreviousHop(this, hops, inputs, 0);

			_hops[key] = hops;

			return hops;
		}

		private static bool FindNextHop(StreamsIO streamsIO, HashSet<Hop> hops, IEnumerable<Iotable> startOutputsId, int depth, int maxDepth = 10)
		{
			if (depth > maxDepth || !startOutputsId.Any())
			{
				return false;
			}

			int previousHops = hops.Count;

			HashSet<Iotable> nextInputsId = new HashSet<Iotable>();
			startOutputsId.ForEach(output => FindNextInput(streamsIO, hops, output, nextInputsId, depth));

			if (nextInputsId.Count < 1 && hops.Count == previousHops)
			{
				return false;
			}

			HashSet<Iotable> nextOutputs = new HashSet<Iotable>();
			nextInputsId.ForEach(input => FindNextOutput(streamsIO, hops, input, nextOutputs, depth));

			return FindNextHop(streamsIO, hops, nextOutputs, ++depth);
		}

		private static void FindNextInput(StreamsIO streamsIO, HashSet<Hop> hops, Iotable output, HashSet<Iotable> nextInputsId, int depth)
		{
			Iotable[] inputs;
			inputs = FindPossibleNextInputs(streamsIO, output);

			if (inputs.Length > 0)
			{
				foreach (var input in inputs)
				{
					hops.Add(Hop.CreateHop(output, input, IOType.Output, depth + 1, output.Status == "False"));
					nextInputsId.Add(input);
				}
			}
			else
			{
				hops.Add(Hop.CreateHop(output, null, IOType.Output, depth + 1, output.Status == "False"));
			}
		}

		private static void FindNextOutput(StreamsIO streamsIO, HashSet<Hop> hops, Iotable input, HashSet<Iotable> nextOutputs, int depth)
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

				hops.Add(Hop.CreateHop(input, output, IOType.Input, depth + 1, input.InputState == "True"));

				nextOutputs.Add(output);
			}
		}

		private static bool FindPreviousHop(StreamsIO streamsIO, HashSet<Hop> hops, IEnumerable<Iotable> startInputsId, int depth, int maxDepth = 10)
		{
			if (depth > maxDepth || !startInputsId.Any())
			{
				return false;
			}

			HashSet<Iotable> previousOutputs = new HashSet<Iotable>();
			startInputsId.ForEach(input => FindPreviousOutput(streamsIO, hops, input, previousOutputs, depth));

			if (!previousOutputs.Any())
			{
				return false;
			}

			HashSet<Iotable> previousInputsId = new HashSet<Iotable>();
			previousOutputs.ForEach(output => FindPreviousInput(streamsIO, hops, output, previousInputsId, depth));

			return FindPreviousHop(streamsIO, hops, previousInputsId, ++depth);
		}

		private static void FindPreviousInput(StreamsIO streamsIO, HashSet<Hop> hops, Iotable output, HashSet<Iotable> startInputsId, int depth)
		{
			var inputs = streamsIO.InputsTable.Where(input =>
				input.Stream == output.Stream && input.MWEdge == output.MWEdge && !input.Protocol.Equals("2022-7"))
				.ToArray();

			foreach (var input in inputs)
			{
				hops.Add(Hop.CreateHop(input, output, IOType.Input, -depth - 1, input.InputState == "True"));

				startInputsId.Add(input);
			}
		}

		private static void FindPreviousOutput(StreamsIO streamsIO, HashSet<Hop> hops, Iotable input, HashSet<Iotable> previousOutputs, int depth)
		{
			Iotable[] outputs;
			outputs = FindPossiblePreviousOutputs(streamsIO, input);

			foreach (var output in outputs)
			{
				if (output.Port == "-1")
				{
					continue;
				}

				hops.Add(Hop.CreateHop(output, input, IOType.Output, -depth - 1, output.Status == "False"));

				previousOutputs.Add(output);
			}
		}

		private static Iotable[] FindPossibleNextInputs(StreamsIO streamsIO, Iotable output)
		{
			Iotable[] inputs;
			if (output.Type != "0" && !output.Protocol.ToLower().Equals("hls")) // push or NA -7
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

			return inputs;
		}

		private static Iotable[] FindPossiblePreviousOutputs(StreamsIO streamsIO, Iotable input)
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
					(input.Ip == output.Ip || ContainsIoIp(streamsIO, output.MWEdge, input.Ip))
					&& input.Port == output.Port)
					.ToArray();
			}

			return outputs;
		}

		private static bool ContainsIoIp(StreamsIO streamsIO, string edgeName, string ioip)
		{
			var edge = streamsIO.EdgesTable.FirstOrDefault(x => x.Name == edgeName);

			if (edge == default)
			{
				return false;
			}

			return edge.Ip == ioip;
		}

		#region GetTables

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
				return Enumerable.Empty<EdgeTable>();
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

			if (!responseInputsTable.NewValue.IsArray)
			{
				return Enumerable.Empty<Iotable>();
			}

			var iotable = new List<Iotable>();
			var cols = responseInputsTable.NewValue.ArrayValue[0].ArrayValue;
			for (int idxRow = 0; idxRow < cols.Length; idxRow++)
			{
				// start of row 'idxRow'
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
					Bitrate = 0,
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
				return Enumerable.Empty<Iotable>();
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
					Bitrate = 0,
				});
			}

			return iotable;
		}

		#endregion GetTables
	}
}