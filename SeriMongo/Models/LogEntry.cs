using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SeriMongo.Models
{
	[BsonIgnoreExtraElements]
	public class LogEntry
	{
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }

		[Key]
		public string LogId
		{
			get
			{
				return Id.ToString();
			}
            set 
			{
				// ObjectId.Parse
			}
		}

		[BsonDateTimeOptions(Kind = DateTimeKind.Local)]
		public DateTime Timestamp { get; set; }

		public string Level { get; set; }

		public string RenderedMessage { get; set; }

		[BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]		
		public Dictionary<string, object> Properties { get; set; }		
	}
}
