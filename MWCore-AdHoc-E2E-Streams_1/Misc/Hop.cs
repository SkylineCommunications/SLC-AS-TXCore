namespace MWCoreAdHocE2EStreams_1.Misc
{
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
}