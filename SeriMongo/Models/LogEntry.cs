using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SeriMongo.Models
{
	public class LogEntry
	{
		public string Id { get; set; } = Guid.NewGuid().ToString("n");

		[Key]
		public string LogId
		{
			get
			{
				return Id;
			}
            set 
			{
				Id = value;
			}
		}

		public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

		public string Level { get; set; } = "Information";

		public string RenderedMessage { get; set; } = string.Empty;

        public string Exception { get; set; }

		public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
	}
}
