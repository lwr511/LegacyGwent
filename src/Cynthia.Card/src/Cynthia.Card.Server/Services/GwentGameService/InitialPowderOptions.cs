using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Cynthia.Card.Server
{
    public sealed class InitialPowderOptions
    {
        public long Amount { get; }

        public InitialPowderOptions(long amount)
        {
            if (amount < 0 || amount > 1000000000)
                throw new InvalidDataException("Initial powder Amount must be an integer from 0 to 1000000000.");
            Amount = amount;
        }

        public static InitialPowderOptions Load(string path = null)
        {
            var json = JObject.Parse(File.ReadAllText(path ?? Path.Combine(AppContext.BaseDirectory, "InitialPowder.json")));
            var amount = json["Amount"];
            if (amount == null || amount.Type != JTokenType.Integer)
                throw new InvalidDataException("InitialPowder.json requires an integer Amount.");
            return new InitialPowderOptions(amount.Value<long>());
        }
    }
}
