using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class InventoryDto
{
    public string PlayerId { get; set; } = string.Empty;
    public int RocketBucks { get; set; }
    public int RocketParts { get; set; }
    public List<ChestInventoryDto> Chests { get; set; } = [];
    public List<string> OneTimeOffers { get; set; } = [];
    public List<DuplicateItemDto> DupeItems { get; set; } = [];

    [JsonPropertyName("blastpasses")]
    public List<BlastPassInventoryDto> BlastPasses { get; set; } = [];

    public List<PromotionInventoryDto> PromotionsOwned { get; set; } = [];
    public List<ItemInventoryDto> Characters { get; set; } = [];
    public List<ItemInventoryDto> MegaBlastTrails { get; set; } = [];
    public List<ItemInventoryDto> ReturnTrails { get; set; } = [];
    public List<ItemInventoryDto> Skins { get; set; } = [];
    public List<ItemInventoryDto> TotemBorders { get; set; } = [];
    public List<ItemInventoryDto> TotemPatterns { get; set; } = [];
    public List<ItemInventoryDto> TotemShapes { get; set; } = [];
    public List<ItemInventoryDto> TotemStands { get; set; } = [];
    public List<ItemInventoryDto> TotemSymbols { get; set; } = [];
    public List<ItemInventoryDto> TotemVFXs { get; set; } = [];
    public List<ItemInventoryDto> TotemCompanions { get; set; } = [];
    public List<ItemInventoryDto> Artifacts { get; set; } = [];
    public List<ItemInventoryDto> CoreArtifacts { get; set; } = [];
    public List<ItemInventoryDto> SecondaryArtifacts { get; set; } = [];
    public List<ItemInventoryDto> UtilityArtifacts { get; set; } = [];
    public List<ItemInventoryDto> CharacterArtifacts { get; set; } = [];
    public List<ItemInventoryDto> ChatEmote { get; set; } = [];
    public List<ItemInventoryDto> PreGameEmote { get; set; } = [];
    public List<ItemInventoryDto> VictoryPose { get; set; } = [];
}

public class ItemInventoryDto
{
    public string CMSItemID { get; set; } = string.Empty;
    public bool Viewed { get; set; }
    public bool PopUpNotification { get; set; }
}

public class ChestInventoryDto
{
    public string CMSChestID { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DuplicateItemDto
{
    public string ItemID { get; set; } = string.Empty;
    public int ItemValue { get; set; }
}

public class BlastPassInventoryDto
{
    public string CMSBlastpassID { get; set; } = string.Empty;
}

public class PromotionInventoryDto
{
    public string PromotionId { get; set; } = string.Empty;
}

public class OrderResponse
{
    public OrderDetailsDto Order { get; set; } = new();
    public InventoryDto Inventory { get; set; } = new();
}

public class OrderDetailsDto
{
    public List<int> RocketBuxGranted { get; set; } = [];
    public List<OrderItemDto> ItemsGranted { get; set; } = [];
    public List<OrderXPBonusDto> XPBonusesGranted { get; set; } = [];
    public OrderBlastpassDto BlastpassGranted { get; set; } = new();
    public bool ProfileModified { get; set; }
}

public class OrderItemDto
{
    public string CMSItemID { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
}

public class OrderXPBonusDto
{
    public string BonusType { get; set; } = string.Empty;
    public float PctBonus { get; set; }
}

public class OrderBlastpassDto
{
    public string CMSBlastpassID { get; set; } = string.Empty;
    public List<string> OfferIds { get; set; } = [];
}
