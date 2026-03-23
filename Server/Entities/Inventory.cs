namespace OpenRocketArena.Server.Entities;

public class PlayerInventory
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public int RocketBucks { get; set; }
    public int RocketParts { get; set; }

    public Account Account { get; set; } = null!;
    public List<InventoryItem> Items { get; set; } = [];
    public List<InventoryChest> Chests { get; set; } = [];
    public List<InventoryDuplicateItem> DupeItems { get; set; } = [];
    public List<InventoryBlastPass> BlastPasses { get; set; } = [];
    public List<InventoryPromotion> Promotions { get; set; } = [];
    public List<InventoryOneTimeOffer> OneTimeOffers { get; set; } = [];
}

public class InventoryItem
{
    public long Id { get; set; }
    public long InventoryId { get; set; }
    public string CmsItemId { get; set; } = string.Empty;
    public string ItemCategory { get; set; } = string.Empty;
    public bool Viewed { get; set; }
    public bool PopUpNotification { get; set; }

    public PlayerInventory Inventory { get; set; } = null!;
}

public class InventoryChest
{
    public long Id { get; set; }
    public long InventoryId { get; set; }
    public string CmsChestId { get; set; } = string.Empty;
    public int Count { get; set; }

    public PlayerInventory Inventory { get; set; } = null!;
}

public class InventoryDuplicateItem
{
    public long Id { get; set; }
    public long InventoryId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int ItemValue { get; set; }

    public PlayerInventory Inventory { get; set; } = null!;
}

public class InventoryBlastPass
{
    public long Id { get; set; }
    public long InventoryId { get; set; }
    public string CmsBlastPassId { get; set; } = string.Empty;

    public PlayerInventory Inventory { get; set; } = null!;
}

public class InventoryPromotion
{
    public long Id { get; set; }
    public long InventoryId { get; set; }
    public string PromotionId { get; set; } = string.Empty;

    public PlayerInventory Inventory { get; set; } = null!;
}

public class InventoryOneTimeOffer
{
    public long Id { get; set; }
    public long InventoryId { get; set; }
    public string OfferId { get; set; } = string.Empty;

    public PlayerInventory Inventory { get; set; } = null!;
}
