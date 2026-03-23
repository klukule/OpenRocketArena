using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Services;

public class CmsStoreService
{
    private readonly ILogger<CmsStoreService> _logger;
    private Dictionary<string, StoreOffer> _offers = new();
    private Dictionary<string, StoreItem> _items = new();

    public CmsStoreService(IConfiguration config, IWebHostEnvironment env, ILogger<CmsStoreService> logger)
    {
        _logger = logger;

        var version = config["Cms:Versions:stage"] ?? "1.default";
        var storePath = Path.Combine(env.WebRootPath, "cms", version, "store.json");

        if (File.Exists(storePath))
            Load(storePath);
        else
            logger.LogWarning("store.json not found at {Path}", storePath);
    }

    private void Load(string path)
    {
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var store = JsonSerializer.Deserialize<StoreDocument>(json, options);

        if (store == null)
        {
            _logger.LogError("Failed to deserialize store.json");
            return;
        }

        _offers = store.Offer.ToDictionary(o => o.LookupField, o => o);
        _items = store.Item.ToDictionary(i => i.LookupField, i => i);

        _logger.LogInformation("Loaded CMS store: {Offers} offers, {Items} items", _offers.Count, _items.Count);
    }

    public StoreOffer? GetOffer(string lookupField) => _offers.GetValueOrDefault(lookupField);

    public StoreItem? GetItem(string lookupField) => _items.GetValueOrDefault(lookupField);

    public IEnumerable<StoreItem> GetItemsByType(string itemType) => _items.Values.Where(i => i.ItemType == itemType);
}

// --- Deserialization models for store.json ---

public class StoreDocument
{
    [JsonPropertyName("offer")]
    public List<StoreOffer> Offer { get; set; } = [];

    [JsonPropertyName("item")]
    public List<StoreItem> Item { get; set; } = [];

    [JsonPropertyName("item_character")]
    public List<StoreItem> ItemCharacter { get; set; } = [];

    [JsonPropertyName("item_skin")]
    public List<StoreItem> ItemSkin { get; set; } = [];
}

public class StoreOffer
{
    [JsonPropertyName("lookup_field")]
    public string LookupField { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("price_rocketbux")]
    public int PriceRocketBux { get; set; }

    [JsonPropertyName("price_rocketparts")]
    public int PriceRocketParts { get; set; }

    [JsonPropertyName("buy_once")]
    public bool BuyOnce { get; set; }

    [JsonPropertyName("offer_type")]
    public string OfferType { get; set; } = string.Empty;

    [JsonPropertyName("offer_items")]
    public List<StoreOfferItem> OfferItems { get; set; } = [];
}

public class StoreOfferItem
{
    [JsonPropertyName("item")]
    public StoreItem Item { get; set; } = new();

    [JsonPropertyName("refund_value")]
    public int RefundValue { get; set; }
}

public class StoreItem
{
    [JsonPropertyName("lookup_field")]
    public string LookupField { get; set; } = string.Empty;

    [JsonPropertyName("item_name")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("item_type")]
    public string ItemType { get; set; } = string.Empty;

    [JsonPropertyName("parts_value")]
    public int PartsValue { get; set; }

    [JsonPropertyName("equip_on_acquire")]
    public bool EquipOnAcquire { get; set; }
}
