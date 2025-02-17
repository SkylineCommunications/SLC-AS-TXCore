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

namespace MWCorePauseResume_1
{
	using System.Collections.Generic;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				var elementName = engine.GetScriptParam("MWCore Element Name").Value.Replace("[\"", string.Empty).Replace("\"]", string.Empty);
				var element = engine.FindElement(elementName);

				if (!element.IsActive)
				{
					engine.GenerateInformation("[Techex MWCore Pause/Resume] Techex MWCore element is not active.");
					return;
				}

				var iotype = engine.GetScriptParam("IO Type").Value.Replace("[\"", string.Empty).Replace("\"]", string.Empty);

				int paramId = -1;
				if (iotype == "Source")
				{
					paramId = 9721;
				}
				else if (iotype == "Output")
				{
					paramId = 9003;
				}
				else
				{
					engine.GenerateInformation("[Techex MWCore Pause/Resume] Invalid IO Type (Source or Output).");
					return;
				}

				string statusToSet = string.Empty;
				var status = engine.GetScriptParam("Status").Value.Replace("[\"", string.Empty).Replace("\"]", string.Empty);
				if (status == "Pause")
				{
					statusToSet = "True";
				}
				else if (status == "Resume")
				{
					statusToSet = "False";
				}
				else
				{
					engine.GenerateInformation("[Techex MWCore Pause/Resume] Invalid Status (Pause or Resume).");
					return;
				}

				var jsonid = engine.GetScriptParam("IO ID").Value;
				var iopk = JsonConvert.DeserializeObject<List<string>>(jsonid)[0];

				engine.GenerateInformation($"[Techex MWCore Pause/Resume] Set {iotype} to {statusToSet}={status} (PK: {iopk}).");

				engine.SetFlag(RunTimeFlags.NoCheckingSets);
				element.SetParameterByPrimaryKey(paramId, iopk, statusToSet); // enable statistics

				if (status == "Resume" && paramId == 9721)
				{
					element.SetParameterByPrimaryKey(9728, iopk, "True"); // enable statistics
				}

				engine.Sleep(3000);
				element.SetParameter(200, 1); // refresh
			}
			catch (System.Exception ex)
			{
				engine.Log($"Exception: {ex}");
				engine.ExitFail($"Error while updating port state.");
			}
		}
	}
}