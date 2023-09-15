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

namespace MWCoreStatisticsEnable_1
{
	using System;
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
			var element = engine.GetDummy("MWCore Element");

			if (!element.IsActive)
			{
				engine.GenerateInformation("[Techex MWCore Statistics] Techex MWCore element is not active.");
				return;
			}

			string[] servers;
			var mwcoreServer = engine.GetScriptParam("MWCore Server").Value;

			if (string.IsNullOrWhiteSpace(mwcoreServer) ||
				mwcoreServer.ToUpper() == "ALL")
			{
				servers = element.GetTablePrimaryKeys(1400); // servers table
			}
			else
			{
				servers = new[] { mwcoreServer };
			}

			foreach (var server in servers)
			{
				var state = Convert.ToString(element.GetParameter(1417, server)); // statistics column

				if (state == "1") // enable
				{
					continue;
				}

				engine.GenerateInformation($"[Techex MWCore Statistics] Enable statistics for {server.Replace("[\"", string.Empty).Replace("\"]", string.Empty)}.");
				element.SetParameterByPrimaryKey(1417, server.Replace("[\"", string.Empty).Replace("\"]", string.Empty), 1); // enable statistics
			}
		}
	}
}