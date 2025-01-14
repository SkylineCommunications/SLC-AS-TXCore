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

08/08/2023	1.0.0.1		SDT, Skyline	Initial version
****************************************************************************
*/

namespace MWCoreStreamAlarms_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Enums;
	using Skyline.DataMiner.Net.Filters;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.Net.Messages;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		public enum Parameters
		{
			Etr290Table = 10500,
			Etr290SourceKeyIdx = 6,
			Etr290SourceKeyPid = 10507,
			InputSourceTablePid = 9600,
			InputSourcesStreamFkIdx = 22,
			InputSourcesStreamFkPid = 9623,
			InputputStatisticsTablePid = 11200,
			OutputTablePid = 8900,
			OutputStreamFkIdx = 7,
			OutputStreamFkPid = 8908,
			OutputStatisticsTablePid = 11400,
			StreamsTablePid = 8700,
			StreamsTsSeverityIdx = 11,
			StreamsTsSeverityPid = 8712,
			StreamsIpSeverityIdx = 12,
			StreamsIpSeverityPid = 8713,
		}

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			var dms = engine.GetDms();
			ScriptParam paramCorrelationAlarmInfo = engine.GetScriptParam(65006);
			string alarmInfo = paramCorrelationAlarmInfo.Value;
			string[] parts = alarmInfo.Split('|');
			int dmaID = Tools.ToInt32(parts[1]);
			int elementID = Tools.ToInt32(parts[2]);
			string parameterID = Tools.ToString(parts[3]);
			string parameterIdx = parts[4];
			var element = engine.FindElement(dmaID, elementID);
			var severity = (EnumSeverity)Tools.ToInt32(parts[7]);

			if (Regex.IsMatch(parameterID, @"105\d\d"))
			{
				ProcessTransportStreamAlarm(engine, element, parameterIdx, severity);
			}
			else if (Regex.IsMatch(parameterID, @"114\d\d"))
			{
				var dmsElement = dms.GetElement(new DmsElementId(element.DmaId, element.ElementId));
				ProcessOutputAlarm(engine, element, dmsElement, parameterIdx, severity);
			}
			else if (Regex.IsMatch(parameterID, @"112\d\d"))
			{
				var dmsElement = dms.GetElement(new DmsElementId(element.DmaId, element.ElementId));
				ProcessInputAlarm(engine, element, dmsElement, parameterIdx, severity);
			}
			else
			{
				// Do nothing
			}
		}

		private static List<string> GetAllStreamInputsAndOuputs(IDmsElement dmsElement, string streamPk)
		{
			var inputsTable = dmsElement.GetTable((int)Parameters.OutputTablePid);
			var inputRows = inputsTable.QueryData(
				new List<ColumnFilter> { new ColumnFilter { ComparisonOperator = ComparisonOperator.Equal, Pid = (int)Parameters.OutputStreamFkPid, Value = streamPk } });

			var values = inputRows.Select(r => Convert.ToString(r[1])).ToList();

			var outputsTable = dmsElement.GetTable((int)Parameters.OutputTablePid);
			var outputRows = outputsTable.QueryData(
				new List<ColumnFilter> { new ColumnFilter { ComparisonOperator = ComparisonOperator.Equal, Pid = (int)Parameters.OutputStreamFkPid, Value = streamPk } });
			outputRows.ForEach(r => values.Add(Convert.ToString(r[1])));
			return values;
		}

		private static void ProcessInputAlarm(IEngine engine, Element element, IDmsElement dmsElement, string parameterIdx, EnumSeverity severity)
		{
			var inputSourceKey = element.GetTableKeyMappings((int)Parameters.InputputStatisticsTablePid).MapToKey(parameterIdx);
			var streamPk = Convert.ToString(element.GetParameterByPrimaryKey((int)Parameters.InputSourcesStreamFkPid, inputSourceKey));
			var currentSeverity = Convert.ToInt32(element.GetParameterByPrimaryKey((int)Parameters.StreamsTsSeverityPid, streamPk));
			List<string> values = GetAllStreamInputsAndOuputs(dmsElement, streamPk);

			var filter = new AlarmFilterItem[]
			{
				new AlarmFilterItemString(AlarmFilterField.ParameterIndex, AlarmFilterCompareType.Equality, values.ToArray()),
				new AlarmFilterItemInt(
					AlarmFilterField.SeverityID,
					new[] { (int)EnumSeverity.Major, (int)EnumSeverity.Minor, (int)EnumSeverity.Critical, (int)EnumSeverity.Warning, (int)EnumSeverity.Normal }),
			};

			var response = engine.SendSLNetSingleResponseMessage(new GetActiveAlarmsMessage
			{
				DataMinerID = element.DmaId,
				ElementID = element.ElementId,
				Filter = new AlarmFilter(filter),
			}) as ActiveAlarmsResponseMessage;

			var newSeverity = response.ActiveAlarms.Length != 0
				? response.ActiveAlarms.Min(alarm => alarm.SeverityID)
				: (int)severity;

			if (newSeverity != currentSeverity)
			{
				element.SetParameterByPrimaryKey((int)Parameters.StreamsIpSeverityPid, streamPk, newSeverity);
			}
		}

		private static void ProcessOutputAlarm(IEngine engine, Element element, IDmsElement dmsElement, string parameterIdx, EnumSeverity severity)
		{
			var outputKey = element.GetTableKeyMappings((int)Parameters.OutputStatisticsTablePid).MapToKey(parameterIdx);
			var streamPk = Convert.ToString(element.GetParameterByPrimaryKey((int)Parameters.OutputStreamFkPid, outputKey));
			var currentSeverity = Convert.ToInt32(element.GetParameterByPrimaryKey((int)Parameters.StreamsTsSeverityPid, streamPk));
			List<string> values = GetAllStreamInputsAndOuputs(dmsElement, streamPk);

			var filter = new AlarmFilterItem[]
			{
				new AlarmFilterItemString(AlarmFilterField.ParameterIndex, AlarmFilterCompareType.Equality, values.ToArray()),
				new AlarmFilterItemInt(
					AlarmFilterField.SeverityID,
					new[] { (int)EnumSeverity.Major, (int)EnumSeverity.Minor, (int)EnumSeverity.Critical, (int)EnumSeverity.Warning, (int)EnumSeverity.Normal }),
			};

			var response = engine.SendSLNetSingleResponseMessage(new GetActiveAlarmsMessage
			{
				DataMinerID = element.DmaId,
				ElementID = element.ElementId,
				Filter = new AlarmFilter(filter),
			}) as ActiveAlarmsResponseMessage;

			var newSeverity = response.ActiveAlarms.Length != 0
				? response.ActiveAlarms.Min(alarm => alarm.SeverityID)
				: (int)severity;

			if (newSeverity != currentSeverity)
			{
				element.SetParameterByPrimaryKey((int)Parameters.StreamsIpSeverityPid, streamPk, newSeverity);
			}
		}

		private static void ProcessTransportStreamAlarm(IEngine engine, Element element, string parameterIdx, EnumSeverity severity)
		{
			string[] inputSource = parameterIdx.Split('/');
			var filter = new AlarmFilterItem[]
			{
				new AlarmFilterItemString(AlarmFilterField.ParameterIndex, AlarmFilterCompareType.WildcardEquality, new[] { inputSource[0] + "*" }),
				new AlarmFilterItemInt(AlarmFilterField.SeverityID, new[] { (int)EnumSeverity.Major, (int)EnumSeverity.Minor, (int)EnumSeverity.Critical, (int)EnumSeverity.Warning, (int)EnumSeverity.Normal }),
			};

			var response = engine.SendSLNetSingleResponseMessage(new GetActiveAlarmsMessage
			{
				DataMinerID = element.DmaId,
				ElementID = element.ElementId,
				Filter = new AlarmFilter(filter),
			}) as ActiveAlarmsResponseMessage;

			var newSeverity = response.ActiveAlarms.Length != 0
				? response.ActiveAlarms.Min(alarm => alarm.SeverityID)
				: (int)severity;

			var etr290Pk = element.GetTableKeyMappings((int)Parameters.Etr290Table).MapToKey(parameterIdx);
			var inputSourceKey = Convert.ToString(element.GetParameterByPrimaryKey((int)Parameters.Etr290SourceKeyPid, etr290Pk));
			var streamPk = Convert.ToString(element.GetParameterByPrimaryKey((int)Parameters.InputSourcesStreamFkPid, inputSourceKey));
			var currentSeverity = Convert.ToInt32(element.GetParameterByPrimaryKey((int)Parameters.StreamsTsSeverityPid, streamPk));

			if (newSeverity != currentSeverity)
			{
				element.SetParameterByPrimaryKey((int)Parameters.StreamsTsSeverityPid, streamPk, newSeverity);
			}
		}
	}
}