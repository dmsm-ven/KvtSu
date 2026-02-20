using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KvtSu;
public class Product
{
    public string? Uri { get; set; }
    public string? Sku { get; set; }
    public string? Model { get; set; }
    public string? Name { get; set; }
    public string? ProductType { get; set; }

    public List<string> Images { get; set; } = new();
    public List<string> Features { get; set; } = new();
    public List<PdfInstruction> Instructions { get; set; } = new();
    public List<string> ChildProductsSkuList { get; set; } = new();
    public string ProductPurposeText { get; set; } = string.Empty;

    [JsonIgnore]
    public string Manufacturer => "КВТ";

    [JsonIgnore]
    public string ManufacturerFtpPath => "kvt";

    public string? TechDescriptionTableMarkup { get; set; }
    public string? ShortDescriptionMarkup { get; set; }
    public string? CharacteristicsTableMarkup { get; set; }
    public ProductDimensions Dimensions { get; set; } = new();

    public Product? ParentProduct { get; set; }


}

public class ProductDimensions
{
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Weight { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Length == default && Width == default && Height == default && Weight == default;
}

public class PdfInstruction
{
    public string Name { get; set; }
    public string Uri { get; set; }
}
