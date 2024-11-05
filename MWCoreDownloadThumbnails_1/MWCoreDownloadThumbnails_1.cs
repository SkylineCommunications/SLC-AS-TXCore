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

namespace DownloadThumbnails_1
{
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Net.Http;
	using System.Net.Security;
	using System.Security.Cryptography.X509Certificates;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string _filesPath = "C:\\Skyline DataMiner\\Webpages\\TechexMWCoreThumbnails";
		private const string _loginEntrypoint = "https://<url>/auth/signin";
		private const string _password = "";
		private const string _user = "";

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				var element = engine.GetDummy("MWCore Element");

				if (!element.IsActive)
				{
					engine.GenerateInformation("[Techex MWCore Thumbnails] Techex MWCore element is not active.");
					return;
				}

				var responseTable = engine.SendSLNetSingleResponseMessage(new GetPartialTableMessage
				{
					DataMinerID = element.DmaId,
					ElementID = element.ElementId,
					ParameterID = 8700, // streams,
					Filters = new[] { "forceFullTable=true" /*, "column=xx,yy"*/ },
				}) as ParameterChangeEventMessage;

				if (!responseTable.NewValue.IsArray)
				{
					engine.GenerateInformation("[Techex MWCore Thumbnails] Thumbnails column empty.");
					return;
				}

				var cols = responseTable.NewValue.ArrayValue[0].ArrayValue;
				string[] thumbnails = new string[cols.Length];
				for (int idxRow = 0; idxRow < cols.Length; idxRow++)
				{
					// start of row 'idxRow'
					thumbnails[idxRow] = responseTable.NewValue.GetTableCell(idxRow, 9)?.CellValue.GetAsStringValue().Replace(@"http://https//", "https://");
				}

				string token = string.Empty;
				Task.Run(async () => { token = await Thumbnail.Login(_loginEntrypoint, _user, _password); }).Wait();
				RequestStreamThumbnail(engine, thumbnails, token);
			}
			catch (Exception ex)
			{
				engine.ExitFail($"[Techex MWCore Thumbnails] Exception: {ex}");
			}
		}

		private static void RequestStreamThumbnail(IEngine engine, IEnumerable<string> thumbnails, string token)
		{
			// engine.GenerateInformation($"token: {token}");
			foreach (var url in thumbnails)
			{
				var fileName = Thumbnail.GetStreamName(url);
				var filepath = $"{_filesPath}\\{fileName}";
				Task.Run(async () => await Thumbnail.DownloadImage(engine, token, url, filepath)).Wait();
			}

			engine.Sleep(5000);
		}
	}

	public class Thumbnail
	{
		public static async Task DownloadImage(IEngine engine, string token, string apiUrl, string savePath)
		{
			using (HttpClient httpClient = new HttpClient(CreateInsecureHandler()))
			{
				try
				{
					httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

					HttpResponseMessage response = await httpClient.GetAsync(apiUrl);
					response.EnsureSuccessStatusCode();

					byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
					await SaveImage(engine, imageBytes, savePath);
				}
				catch (Exception ex)
				{
					engine.Log($"MWCore Thumbnails|DownloadImage|Exception thrown: {ex}");
				}
			}
		}

		public static string GetStreamName(string url)
		{
			var regexGroups = Regex.Match(url, @"\/mwedge\/(?<mwedgeId>\w*)\/stream\/(?<streamId>\w*)").Groups;
			return $"{regexGroups["streamId"]}.jpg";
		}

		public static async Task<string> Login(string url, string user, string password)
		{
			string token = string.Empty;
			using (HttpClient client = new HttpClient(CreateInsecureHandler()))
			{
				//login
				var request = new HttpRequestMessage(HttpMethod.Post, url);
				var content = new StringContent($"{{\"username\":\"{user}\",\"password\":\"{password}\"}}", null, "application/json");
				request.Content = content;
				var response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
				string responseBody = await response.Content.ReadAsStringAsync();

				token = Convert.ToString(JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody)["token"]);
			}

			return token;
		}

		private static HttpClientHandler CreateInsecureHandler()
		{
			HttpClientHandler handler = new HttpClientHandler();
			handler.ServerCertificateCustomValidationCallback = ValidateCertificate;
			return handler;
		}

		private static async Task SaveImage(IEngine engine, byte[] imageBytes, string savePath)
		{
			try
			{
				using (FileStream outputStream = File.Create(savePath))
				{
					await outputStream.WriteAsync(imageBytes, 0, imageBytes.Length);
				}
			}
			catch (Exception ex)
			{
				engine.Log($"MWCore Thumbnails|SaveImage|Exception thrown: {ex}");
			}
		}

		private static bool ValidateCertificate(HttpRequestMessage request, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			// Allow all certificates (bypass SSL certificate validation)
			return true;
		}
	}
}