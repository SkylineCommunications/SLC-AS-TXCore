namespace MWCoreDownloadThumbnails_1.Misc
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Net.Http;
	using System.Net.Security;
	using System.Security.Cryptography.X509Certificates;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;

	using MWCoreDownloadThumbnails_1.Misc.Enums;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;

	public class Thumbnail
	{
		public static string Login(GQIDMS dms, LiteElementInfoEvent element)
		{
			var request = new GetParameterMessage
			{
				DataMinerID = element.DataMinerID,
				ElId = element.ElementID,
				ParameterId = (int)ParameterPids.LoginResponse,
			};

			if (!(dms.SendMessage(request) is GetParameterResponseMessage responseLoginResponse))
			{
				throw new InvalidOperationException("MWCore Thumbnails|Login|Fail to retrieve login parameter.");
			}

			if (String.IsNullOrWhiteSpace(responseLoginResponse.Value.StringValue))
			{
				throw new InvalidOperationException("MWCore Thumbnails|Login|Login response is empty.");
			}

			var parsedResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseLoginResponse.Value.StringValue);

			if (!parsedResponse.TryGetValue("token", out object token))
			{
				throw new InvalidOperationException("MWCore Thumbnails|Login|Failed to find token.");
			}

			return Convert.ToString(token);
		}

		public static async Task<string> DownloadImage(IGQILogger logger, string token, string apiUrl)
		{
			using (HttpClient httpClient = new HttpClient(CreateInsecureHandler()))
			{
				try
				{
					httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

					HttpResponseMessage response = await httpClient.GetAsync(apiUrl);
					response.EnsureSuccessStatusCode();

					byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
					return Convert.ToBase64String(imageBytes);
				}
				catch (Exception ex)
				{
					logger.Error($"MWCore Thumbnails|DownloadImage|Exception thrown: {ex}");
				}
			}

			return String.Empty;
		}

		private static void SaveImageAsBase64(IGQILogger logger, byte[] imageBytes, string savePath)
		{
			try
			{
				string base64String = Convert.ToBase64String(imageBytes);
				File.WriteAllText(savePath, base64String);
			}
			catch (Exception ex)
			{
				logger.Error($"MWCore Thumbnails|SaveImageAsBase64|Exception thrown: {ex.Message}");
			}
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

		private static HttpClientHandler CreateInsecureHandler()
		{
			HttpClientHandler handler = new HttpClientHandler();
			handler.ServerCertificateCustomValidationCallback = ValidateCertificate;
			return handler;
		}

		private static bool ValidateCertificate(HttpRequestMessage request, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			// Allow all certificates (bypass SSL certificate validation)
			return true;
		}
	}
}