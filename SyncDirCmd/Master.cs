using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncDirCmd
{
	public enum Master
	{
		Left,
		Right,
		Both
	}

	public class MasterJsonConverter : JsonConverter
	{
		public override bool CanRead => true;

		public override bool CanWrite => true;

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(string);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return Enum.Parse(typeof(Master), (string)reader.Value, ignoreCase: true);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var master = (Master)value;

			switch (master)
			{
				case Master.Left:
					writer.WriteValue("left");
					break;
				case Master.Right:
					writer.WriteValue("right");
					break;
				case Master.Both:
					writer.WriteValue("both");
					break;
			}
		}
	}
}
