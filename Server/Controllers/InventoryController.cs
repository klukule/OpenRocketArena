using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenRocketArena.Server.Data;
using OpenRocketArena.Server.Entities;
using OpenRocketArena.Server.Models;
using OpenRocketArena.Server.Services;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// Mango Inventory endpoints
/// </summary>
[ApiController]
public class InventoryController(AppDbContext db, CmsStoreService store, ILogger<InventoryController> logger) : ControllerBase
{
    // Item category names matching the FMangoInventory field names
    private static readonly Dictionary<string, string> CategoryToProperty = new()
    {
        ["Character"] = "Characters",
        ["MegaBlastTrail"] = "MegaBlastTrails",
        ["ReturnTrail"] = "ReturnTrails",
        ["Skin"] = "Skins",
        ["TotemBorder"] = "TotemBorders",
        ["TotemPattern"] = "TotemPatterns",
        ["TotemShape"] = "TotemShapes",
        ["TotemStand"] = "TotemStands",
        ["TotemSymbols"] = "TotemSymbols",
        ["TotemVFX"] = "TotemVFXs",
        ["TotemCompanion"] = "TotemCompanions",
        ["Artifact"] = "Artifacts",
        ["CoreArtifact"] = "CoreArtifacts",
        ["SecondaryArtifact"] = "SecondaryArtifacts",
        ["UtilityArtifact"] = "UtilityArtifacts",
        ["CharacterArtifact"] = "CharacterArtifacts",
        ["ChatEmote"] = "ChatEmote",
        ["PreGameEmote"] = "PreGameEmote",
        ["VictoryPose"] = "VictoryPose",
    };

    [HttpGet("/inventory/v2/bulk")]
    public async Task<IActionResult> GetBulkInventory([FromQuery] string PlayerIds)
    {
        var playerIds = PlayerIds.Split([',', '+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var inventories = new List<InventoryDto>();

        foreach (var pid in playerIds)
        {
            var parts = pid.Split(':');
            if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId)) continue;
            var inventory = await LoadOrCreateInventory(accountId);
            inventories.Add(MapInventory(inventory, pid));
        }

        return Ok(new { Inventories = inventories });
    }

    [HttpGet("/inventory/v2/{playerId}")]
    public async Task<IActionResult> GetInventory(string playerId)
    {
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return BadRequest(new { error = "invalid_player_id" });

        var inventory = await LoadOrCreateInventory(accountId);
        return Ok(MapInventory(inventory, playerId));
    }

    [HttpPost("/inventory/v2/{playerId}/offer/{offerId}/{currency}/{transactionId}")]
    public async Task<IActionResult> PurchaseOffer(string playerId, string offerId, string currency, string transactionId)
    {
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return BadRequest(new { error = "invalid_player_id" });

        var offer = store.GetOffer(offerId);
        if (offer == null)
        {
            logger.LogWarning("Offer {OfferId} not found in CMS store", offerId);
            return NotFound(new { error = "offer_not_found" });
        }

        var inventory = await LoadOrCreateInventory(accountId);

        // Deduct currency
        switch (currency)
        {
            case "rocketparts":
                if (inventory.RocketParts < offer.PriceRocketParts)
                    return BadRequest(new { error = "insufficient_funds" });
                inventory.RocketParts -= offer.PriceRocketParts;
                break;
            case "rocketbux":
                if (inventory.RocketBucks < offer.PriceRocketBux)
                    return BadRequest(new { error = "insufficient_funds" });
                inventory.RocketBucks -= offer.PriceRocketBux;
                break;
        }

        // Grant items from the offer
        var itemsGranted = new List<OrderItemDto>();
        foreach (var offerItem in offer.OfferItems)
        {
            var item = offerItem.Item;
            if (string.IsNullOrEmpty(item.LookupField)) continue;

            if (offer.BuyOnce && inventory.Items.Any(i => i.CmsItemId == item.LookupField))
                continue;

            inventory.Items.Add(new InventoryItem
            {
                InventoryId = inventory.Id,
                CmsItemId = item.LookupField,
                ItemCategory = item.ItemType,
                Viewed = false,
                PopUpNotification = true
            });
            itemsGranted.Add(new OrderItemDto { CMSItemID = item.LookupField, ItemType = item.ItemType });
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Purchase: account {AccountId} bought offer {OfferId} ({Currency}), granted {Count} items", accountId, offerId, currency, itemsGranted.Count);

        return Ok(new OrderResponse
        {
            Order = new OrderDetailsDto
            {
                ItemsGranted = itemsGranted,
                ProfileModified = false
            },
            Inventory = MapInventory(inventory, playerId)
        });
    }

    private async Task<PlayerInventory> LoadOrCreateInventory(long accountId)
    {
        var inventory = await db.PlayerInventories
            .Include(i => i.Items)
            .Include(i => i.Chests)
            .Include(i => i.DupeItems)
            .Include(i => i.BlastPasses)
            .Include(i => i.Promotions)
            .Include(i => i.OneTimeOffers)
            .FirstOrDefaultAsync(i => i.AccountId == accountId);

        if (inventory == null)
        {
            inventory = new PlayerInventory { AccountId = accountId };
            db.PlayerInventories.Add(inventory);
            await db.SaveChangesAsync();
        }

        return inventory;
    }

    [HttpPut("/inventory/v2/{playerId}/viewed")]
    public async Task<IActionResult> SetItemsViewed(string playerId, [FromBody] PopUpNotificationRequest request)
    {
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return BadRequest(new { error = "invalid_player_id" });

        var inventory = await db.PlayerInventories
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.AccountId == accountId);

        if (inventory == null)
            return NotFound(new { error = "inventory_not_found" });

        foreach (var itemId in request.Items)
        {
            var item = inventory.Items.FirstOrDefault(i => i.CmsItemId == itemId);
            if (item != null)
                item.Viewed = true;
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("/inventory/{playerId}/popup-notification")]
    public async Task<IActionResult> SetPopUpNotification(string playerId, [FromBody] PopUpNotificationRequest request)
    {
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return BadRequest(new { error = "invalid_player_id" });

        var inventory = await db.PlayerInventories
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.AccountId == accountId);

        if (inventory == null)
            return NotFound(new { error = "inventory_not_found" });

        var updated = 0;
        foreach (var itemId in request.Items)
        {
            var item = inventory.Items.FirstOrDefault(i => i.CmsItemId == itemId);
            if (item != null)
            {
                item.PopUpNotification = false;
                updated++;
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("/inventory/v2/{playerId}/processrewards")]
    public async Task<IActionResult> ProcessRewards(string playerId)
    {
        var parts = playerId.Split(':');
        if (parts.Length == 0 || !long.TryParse(parts[0], out var accountId))
            return Ok(new { grants = (object?)null });

        var inventory = await LoadOrCreateInventory(accountId);

        // Grant all characters the player doesn't own yet - simulates user having bought mythic edition
        var allCharacters = store.GetItemsByType("Character");
        foreach (var character in allCharacters)
        {
            if (!inventory.Items.Any(i => i.CmsItemId == character.LookupField))
            {
                inventory.Items.Add(new InventoryItem
                {
                    InventoryId = inventory.Id,
                    CmsItemId = character.LookupField,
                    ItemCategory = "Character",
                    Viewed = true,
                    PopUpNotification = false
                });
            }
        }

        await db.SaveChangesAsync();
        return Ok(new { grants = (object?)null });
    }

    private static InventoryDto MapInventory(PlayerInventory inv, string playerId)
    {
        var itemsByCategory = inv.Items.GroupBy(i => i.ItemCategory)
            .ToDictionary(g => g.Key, g => g.Select(MapItem).ToList());

        List<ItemInventoryDto> GetCategory(string category) =>
            itemsByCategory.GetValueOrDefault(category, []);

        return new InventoryDto
        {
            PlayerId = playerId,
            RocketBucks = inv.RocketBucks,
            RocketParts = inv.RocketParts,
            Chests = inv.Chests.Select(c => new ChestInventoryDto { CMSChestID = c.CmsChestId, Count = c.Count }).ToList(),
            OneTimeOffers = inv.OneTimeOffers.Select(o => o.OfferId).ToList(),
            DupeItems = inv.DupeItems.Select(d => new DuplicateItemDto { ItemID = d.ItemId, ItemValue = d.ItemValue }).ToList(),
            BlastPasses = inv.BlastPasses.Select(b => new BlastPassInventoryDto { CMSBlastpassID = b.CmsBlastPassId }).ToList(),
            PromotionsOwned = inv.Promotions.Select(p => new PromotionInventoryDto { PromotionId = p.PromotionId }).ToList(),
            Characters = GetCategory("Character"),
            MegaBlastTrails = GetCategory("MegaBlastTrail"),
            ReturnTrails = GetCategory("ReturnTrail"),
            Skins = GetCategory("Skin"),
            TotemBorders = GetCategory("TotemBorder"),
            TotemPatterns = GetCategory("TotemPattern"),
            TotemShapes = GetCategory("TotemShape"),
            TotemStands = GetCategory("TotemStand"),
            TotemSymbols = GetCategory("TotemSymbols"),
            TotemVFXs = GetCategory("TotemVFX"),
            TotemCompanions = GetCategory("TotemCompanion"),
            Artifacts = GetCategory("Artifact"),
            CoreArtifacts = GetCategory("CoreArtifact"),
            SecondaryArtifacts = GetCategory("SecondaryArtifact"),
            UtilityArtifacts = GetCategory("UtilityArtifact"),
            CharacterArtifacts = GetCategory("CharacterArtifact"),
            ChatEmote = GetCategory("ChatEmote"),
            PreGameEmote = GetCategory("PreGameEmote"),
            VictoryPose = GetCategory("VictoryPose"),
        };
    }

    private static ItemInventoryDto MapItem(InventoryItem item) => new()
    {
        CMSItemID = item.CmsItemId,
        Viewed = item.Viewed,
        PopUpNotification = item.PopUpNotification
    };
}

public class PopUpNotificationRequest
{
    public List<string> Items { get; set; } = [];
}
