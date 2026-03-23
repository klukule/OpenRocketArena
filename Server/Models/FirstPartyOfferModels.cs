using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class FirstPartyOffersResponse
{
    [JsonPropertyName("offers")]
    public List<FirstPartyOfferDto> Offers { get; set; } = [];
}

public class FirstPartyOfferDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shortDescription")]
    public string ShortDescription { get; set; } = string.Empty;

    [JsonPropertyName("longDescription")]
    public string LongDescription { get; set; } = string.Empty;

    [JsonPropertyName("contentRating")]
    public List<FirstPartyRatingDto> ContentRating { get; set; } = [];

    [JsonPropertyName("skus")]
    public List<FirstPartySkuDto> SKUs { get; set; } = [];
}

public class FirstPartyRatingDto
{
    [JsonPropertyName("system")]
    public string System { get; set; } = string.Empty;
}

public class FirstPartySkuDto
{
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public float Price { get; set; }

    [JsonPropertyName("displayPrice")]
    public string DisplayPrice { get; set; } = string.Empty;

    [JsonPropertyName("listPrice")]
    public float ListPrice { get; set; }

    [JsonPropertyName("displayListPrice")]
    public string DisplayListPrice { get; set; } = string.Empty;
}
