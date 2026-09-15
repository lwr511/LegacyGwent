using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace Cynthia.Card.Server
{
    public partial class GwentDatabaseService
    {
        private IMongoCollection<PremiumCollection> PremiumAccounts => GetDatabase().GetCollection<PremiumCollection>("premium_collection");
        private static readonly Lazy<Dictionary<string, int>> PremiumCosts = new Lazy<Dictionary<string, int>>(() =>
        {
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PremiumCrafting.json"));
            var config = JsonConvert.DeserializeObject<PremiumCraftingConfig>(json);
            if (config.Copper <= 0 || config.Silver <= 0 || config.Gold <= 0 || config.Leader <= 0)
                throw new InvalidDataException("Premium crafting costs must be positive.");
            var arts = new HashSet<string>(config.AvailableArtIds);
            return GwentMap.CardMap.Where(x => arts.Contains(x.Value.CardArtsId))
                .ToDictionary(x => x.Key, x => x.Value.Group == Group.Copper ? config.Copper :
                    x.Value.Group == Group.Silver ? config.Silver : x.Value.Group == Group.Leader ? config.Leader : config.Gold);
        });

        public async Task<PremiumCollectionResult> GetPremiumCollection(string username)
        {
            var user = await GetUserInfo().Find(x => x.UserName == username).FirstOrDefaultAsync();
            if (user == null) return new PremiumCollectionResult { Status = "unauthenticated" };
            await EnsureInitialPowder(user.Id);
            return Result("ok", await PremiumAccounts.Find(x => x.Id == user.Id).FirstAsync());
        }

        private static PremiumCollectionResult Result(string status, PremiumCollection account) =>
            new PremiumCollectionResult { Status = status, Collection = account, Costs = PremiumCosts.Value };

        public async Task<PremiumCollectionResult> CraftPremium(string username, string cardId)
        {
            var current = await GetPremiumCollection(username);
            if (!current.Success) return current;
            if (cardId == null || !PremiumCosts.Value.TryGetValue(cardId, out var cost)) return Result("unavailable", current.Collection);
            var f = Builders<PremiumCollection>.Filter;
            var filter = f.Eq(x => x.Id, current.Collection.Id) & f.Gte(x => x.MeteoritePowder, cost) & f.Not(f.AnyEq(x => x.OwnedCards, cardId));
            var updated = await PremiumAccounts.FindOneAndUpdateAsync(filter,
                Builders<PremiumCollection>.Update.Inc(x => x.MeteoritePowder, -cost).Inc(x => x.Revision, 1)
                    .AddToSet(x => x.OwnedCards, cardId).AddToSet(x => x.SelectedCards, cardId),
                new FindOneAndUpdateOptions<PremiumCollection> { ReturnDocument = ReturnDocument.After });
            if (updated != null) return Result("ok", updated);
            var latest = await PremiumAccounts.Find(x => x.Id == current.Collection.Id).FirstAsync();
            return Result(latest.OwnedCards.Contains(cardId) ? "already_owned" : "insufficient_powder", latest);
        }

        public async Task<PremiumCollectionResult> SelectPremium(string username, string cardId, bool premium)
        {
            var current = await GetPremiumCollection(username);
            if (!current.Success) return current;
            if (cardId == null || !GwentMap.CardMap.ContainsKey(cardId)) return Result("unavailable", current.Collection);
            var f = Builders<PremiumCollection>.Filter;
            var filter = f.Eq(x => x.Id, current.Collection.Id);
            if (premium) filter &= f.AnyEq(x => x.OwnedCards, cardId);
            var change = premium ? Builders<PremiumCollection>.Update.AddToSet(x => x.SelectedCards, cardId) :
                Builders<PremiumCollection>.Update.Pull(x => x.SelectedCards, cardId);
            var updated = await PremiumAccounts.FindOneAndUpdateAsync(filter, change.Inc(x => x.Revision, 1),
                new FindOneAndUpdateOptions<PremiumCollection> { ReturnDocument = ReturnDocument.After });
            return Result(updated == null ? "not_owned" : "ok", updated ?? current.Collection);
        }

        private sealed class PremiumCraftingConfig
        {
            public int Copper { get; set; }
            public int Silver { get; set; }
            public int Gold { get; set; }
            public int Leader { get; set; }
            public string[] AvailableArtIds { get; set; }
        }
    }
}
