namespace MWCoreAdHocE2EStreams_1.Misc
{
	using System;
	using System.Collections.Generic;

	internal class Hop : IEquatable<Hop>
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

		public static bool operator ==(Hop left, Hop right)
		{
			return EqualityComparer<Hop>.Default.Equals(left, right);
		}

		public static bool operator !=(Hop left, Hop right)
		{
			return !(left == right);
		}

		public static Hop CreateHop(Iotable source, Iotable destination, IOType type, int hopNumber, bool isActive, bool startingPoint = false)
		{
			if (destination == null)
			{
				return CreateEndHop(source, type, hopNumber, isActive);
			}

			return new Hop
			{
				Bitrate_Dst = destination.Bitrate,
				Bitrate_Src = source.Bitrate,
				Id_Dst = destination.Id,
				Id_Src = source.Id,
				IOType = type,
				Ip_Dst = destination.Ip,
				Ip_Src = source.Ip,
				MWEdge_Dst = destination.MWEdge,
				MWEdge_Src = source.MWEdge,
				Name_Dst = destination.Name,
				Name_Src = source.Name,
				Port_Dst = destination.Port,
				Port_Src = source.Port,
				Stream_Dst = destination.Stream,
				Stream_Src = source.Stream,
				Type_Dst = destination.Type,
				Type_Src = source.Type,
				Status_Dst = destination.Status,
				Status_Src = source.Status,
				Starting_Point = startingPoint,
				Hop_Number = hopNumber,
				IsActive = isActive,
			};
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as Hop);
		}

		public bool Equals(Hop other)
		{
			if (other == null)
				return false;

			return Id_Dst == other.Id_Dst && Id_Src == other.Id_Src;
		}

		public override int GetHashCode()
		{
			int hashCode = -413437643;
			hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(Id_Dst);
			hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(Id_Src);
			return hashCode;
		}

		private static Hop CreateEndHop(Iotable source, IOType type, int hopNumber, bool isActive)
		{
			return new Hop
			{
				Bitrate_Dst = 0,
				Bitrate_Src = source.Bitrate,
				Id_Dst = string.Empty,
				Id_Src = source.Id,
				IOType = type,
				Ip_Dst = string.Empty,
				Ip_Src = source.Ip,
				MWEdge_Dst = string.Empty,
				MWEdge_Src = source.MWEdge,
				Name_Dst = string.Empty,
				Name_Src = source.Name,
				Port_Dst = string.Empty,
				Port_Src = source.Port,
				Stream_Dst = string.Empty,
				Stream_Src = source.Stream,
				Type_Dst = string.Empty,
				Type_Src = source.Type,
				Status_Dst = string.Empty,
				Status_Src = source.Status,
				Hop_Number = hopNumber,
				IsActive = isActive,
			};
		}
	}
}