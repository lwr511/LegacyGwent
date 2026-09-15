using System.Collections.Generic;

namespace Cynthia.Card
{
    // Separate from UserInfo so legacy whole-user replacements cannot overwrite the wallet.
    public sealed class PremiumCollection
    {
        public string Id { get; set; }
        public long MeteoritePowder { get; set; }
        public long Revision { get; set; }
        public DailyQuestProgress DailyQuests { get; set; }
        public List<string> OwnedCards { get; set; } = new List<string>();
        public List<string> SelectedCards { get; set; } = new List<string>();
        public List<PowderReward> Rewards { get; set; } = new List<PowderReward>();
    }

    public sealed class PowderReward
    {
        public string RewardId { get; set; }
        public long Amount { get; set; }
        public string Reason { get; set; }
        public string GrantedUtc { get; set; }
    }

    public sealed class PremiumCollectionResult
    {
        public string Status { get; set; }
        public PremiumCollection Collection { get; set; }
        public Dictionary<string, int> Costs { get; set; }
        public bool Success => Status == "ok";
    }
}
