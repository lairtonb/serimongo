using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace SeriMongo.Extensions
{
    /// <summary>
    /// This works around the fact that BsonDocument.Parse does not recognize ISO 8601 dates (https://jira.mongodb.org/browse/CSHARP-2233).
    /// </summary>
    /// <remarks>
    /// This is the work of Hugh Williams:
    /// https://jira.mongodb.org/secure/ViewProfile.jspa?name=hughbiquitous
    /// </remarks>
    public static class BsonExtensions
    {
        private static readonly string[] _formats =
{
            "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFFZ",
            "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFFK",
        };


        /// <summary>
        /// When you call BsonDocument.Parse on a JSON string containing ISO8601 dates (e.g., "2018-04-02T08:03:12.3456789-04:00")
        /// it does not interpret them as datetime values; it just treats them as strings.
        /// </summary>
        /// <remarks>I logged it at https://jira.mongodb.org/browse/CSHARP-2233 ... they closed it "as designed" so this will
        /// be an enduring solution.</remarks>
        public static BsonDocument ConvertToIsoDates(this BsonDocument bsonDocument)
        {
            for (var i = 0; i < bsonDocument.ElementCount; ++i)
            {
                var bsonValue = bsonDocument[i];
                switch (bsonValue.BsonType)
                {
                    case BsonType.String:
                        if (DateTime.TryParseExact(bsonValue.AsString,
                                                   _formats,
                                                   System.Globalization.CultureInfo.InvariantCulture,
                                                   System.Globalization.DateTimeStyles.None,
                                                   out var result))
                        {
                            bsonDocument[i] = new BsonDateTime(result);
                        }
                        break;
                    case BsonType.Array:
                        bsonDocument[i].AsBsonArray.ConvertToIsoDates();
                        break;
                    case BsonType.Document:
                        bsonDocument[i].AsBsonDocument.ConvertToIsoDates();
                        break;
                }
            }

            return bsonDocument;
        }

        /// <summary>
        /// When you call BsonDocument.Parse on a JSON string containing ISO8601 dates (e.g., "2018-04-02T08:03:12.3456789-04:00")
        /// it does not interpret them as datetime values; it just treats them as strings.
        /// </summary>
        /// <remarks>I logged it at https://jira.mongodb.org/browse/CSHARP-2233 ... they closed it "as designed" so this will
        /// be an enduring solution.</remarks>
        public static BsonArray ConvertToIsoDates(this BsonArray bsonArray)
        {
            for (var i = 0; i < bsonArray.Count; ++i)
            {
                var bsonValue = bsonArray[i];
                switch (bsonValue.BsonType)
                {
                    case BsonType.String:
                        if (DateTime.TryParseExact(bsonValue.AsString,
                                                   _formats,
                                                   CultureInfo.InvariantCulture,
                                                   DateTimeStyles.None,
                                                   out var result))
                        {
                            bsonArray[i] = new BsonDateTime(result);
                        }

                        break;
                    case BsonType.Array:
                        bsonArray[i].AsBsonArray.ConvertToIsoDates();
                        break;
                    case BsonType.Document:
                        bsonArray[i].AsBsonDocument.ConvertToIsoDates();
                        break;
                }
            }

            return bsonArray;
        }
    }
}
