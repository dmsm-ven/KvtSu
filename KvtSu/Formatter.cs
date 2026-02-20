using HtmlAgilityPack;
using KvtSu.Helpers;
using SlugGenerator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace KvtSu;

public class Formatter
{
    private readonly IReadOnlyList<Product> products;
    private readonly IReadOnlyList<Product> ModelRangeProducts;
    private readonly int startId;

    public Formatter(IEnumerable<Product> products, int startId)
    {
        this.products = products.ToList();
        this.startId = startId;
    }

    internal string GetGeneralExport()
    {
        var sb = new StringBuilder();

        int id = startId;
        foreach (var product in products)
        {
            string mainImage = GetMainImage(product);

            string caption = product.Sku + " КВТ " + product.Name;
            string seoCaption = caption.TrimLengthByWord(maxLength: 128);
            string keyword = caption.TrimLengthByWord(maxLength: 64).GenerateSlug();
            string meta_title = $"{seoCaption} купить в Санкт-Петербурге";
            string meta_desc = $"{meta_title} с доставкой по России";

            sb
                .AppendTab(id.ToString())   //product_id
                .AppendTab(product.Name)   //name(ru)
                .AppendTab(string.Empty)   //categories
                .AppendTab($"KV-{product.Sku}")   //sku
                .AppendTab(string.Empty)   //upc
                .AppendTab(string.Empty)   //ean
                .AppendTab(string.Empty)   //jan
                .AppendTab(string.Empty)   //isbn
                .AppendTab(string.Empty)   //mpn
                .AppendTab(string.Empty)   //location
                .AppendTab("0")   //quantity
                .AppendTab(product.Sku)   //model
                .AppendTab(product.Manufacturer)   //manufacturer
                .AppendTab(mainImage)   //image_name
                .AppendTab("yes")   //shipping
                .AppendTab("0")   //price
                .AppendTab("0")   //points
                .AppendTab(string.Empty)   //date_added
                .AppendTab(string.Empty)   //date_modified
                .AppendTab(string.Empty)   //date_available
                .AppendTab("0")   //weight
                .AppendTab("kg")   //weight_unit
                .AppendTab("0")    //length
                .AppendTab("0")    //width
                .AppendTab("0")    //height
                .AppendTab("mm")   //length_unit
                .AppendTab("true")   //status
                .AppendTab("0")   //tax_class_id
                .AppendTab(keyword)   //seo_keyword
                .AppendTab(string.Empty)   //description(ru)
                .AppendTab("0")   //category_show(ru)
                .AppendTab("0")   //main_product(ru)
                .AppendTab(meta_title)   //meta_title(ru)
                .AppendTab(meta_desc)   //meta_description(ru)
                .AppendTab(string.Empty)   //meta_keywords(ru)
                .AppendTab("10")   //stock_status_id
                .AppendTab("0,1,2,3,4,5,6,7,8")   //store_ids
                .AppendTab("0:,1:,2:,3:,4:,5:,6:,7:,8:")   //layout
                .AppendTab(string.Empty)   //related_ids
                .AppendTab(string.Empty)   //adjacent_ids
                .AppendTab(string.Empty)   //tags(ru)
                .AppendTab("1")   //sort_order
                .AppendTab("true")   //subtract
                .AppendLine("1"); //minimum

            id++;
        }

        var result = sb.ToString();
        return result;
    }

    internal string GetAdjacent()
    {
        var sb = new StringBuilder();

        int id = startId;
        foreach (var p in ModelRangeProducts)
        {
            string skuArray = string.Join(",", p.ChildProductsSkuList.Select(sku => $"'KV-{sku}'"));
            sb.Append("INSERT INTO oc_product_adjacent (product_id, adjacent_id) ");
            sb.Append($"SELECT {id}, product_id ");
            sb.Append($"FROM oc_product p ");
            sb.Append($"JOIN oc_manufacturer m ON (p.manufacturer_id = m.manufacturer_id) ");
            sb.Append($"WHERE m.name = 'КВТ' AND p.sku IN ({skuArray});\r\n");

            id++;
        }

        var result = sb.ToString();
        return result;
    }

    internal string GetAdditionalImagesExport()
    {
        var sb = new StringBuilder();

        int id = startId;
        foreach (var product in products)
        {
            int sort_order = 0;

            var allImages = GetAllImagesForProduct(product);

            foreach (var image in allImages.Skip(1))
            {
                string imagePath = $"catalog/{product.ManufacturerFtpPath}/products/" + image.CreateMD5() + Path.GetExtension(image);
                sb.AppendLine($"{id}\t{imagePath}\t{sort_order++}");
            }

            id++;
        }

        var result = sb.ToString();
        return result;
    }

    private List<string> GetAllImagesForProduct(Product product)
    {
        List<string> allImages = new();
        if (product.Images != null)
        {
            allImages.AddRange(product.Images);
        }
        if (product?.ParentProduct?.Images != null)
        {
            allImages.AddRange(product?.ParentProduct?.Images);
        }
        allImages = allImages.Distinct().ToList();
        return allImages;
    }

    internal string GetDescriptionSql()
    {
        var sb = new StringBuilder();

        string pidArray = string.Join(",", Enumerable.Range(startId, ModelRangeProducts.Count));
        sb
            .AppendLine("UPDATE oc_product_description")
            .AppendLine("JOIN oc_product ON oc_product.product_id = oc_product_description.product_id")
            .AppendLine("SET oc_product_description.description = case oc_product.sku");

        string skuArray = string.Join(",", products.Select(p => $"'KV-{p.Sku}'"));
        foreach (var product in products)
        {
            string desc = BuildDescriptionForProduct(product);
            string encodedDesk = HttpUtility.HtmlEncode(desc);
            sb.AppendLine($"WHEN 'KV-{product.Sku}' THEN '{encodedDesk}'");
        }
        sb.AppendLine("END");
        sb.AppendLine($"WHERE oc_product.sku IN ({skuArray})");

        var result = sb.ToString().TrimHtml() + ";";
        return result;
    }

    internal string GetDescriptionSingleSql()
    {
        var sb = new StringBuilder();

        sb.AppendLine(@"UPDATE oc_product_description 
                        JOIN oc_product ON(oc_product.product_id = oc_product_description.product_id)
                        SET oc_product_description.description = case oc_product.sku");

        int id = startId;
        string skuArray = string.Join(",", products.Select(p => $"'KV-{p.Sku}'"));

        foreach (var product in products)
        {
            string desc = BuildDescriptionForProduct(product);
            string encodedDesc = HttpUtility.HtmlEncode(desc);
            sb.AppendLine($"WHEN 'KV-{product.Sku}' THEN '{encodedDesc}'");
        }
        sb.AppendLine("END");
        sb.AppendLine($"WHERE oc_product.manufacturer_id = 79 AND oc_product.sku IN ({skuArray})");

        var result = sb.ToString().TrimHtml() + ";";
        return result;
    }

    private string GetMainImage(Product product)
    {
        var firstImage = product.Images.FirstOrDefault();
        if (firstImage == null && product.ParentProduct != null)
        {
            firstImage = product.ParentProduct.Images.FirstOrDefault();
        }

        if (firstImage != null)
        {
            return $"catalog/{product.ManufacturerFtpPath}/products/" + firstImage.CreateMD5() + Path.GetExtension(firstImage);
        }
        else
        {
            return "catalog/placeholder.jpg";
        }
    }

    internal string BuildDescriptionForProduct(Product product)
    {
        var sb = new StringBuilder();

        if (product.ShortDescriptionMarkup != null)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(product.ShortDescriptionMarkup);
            doc.DocumentNode.SelectSingleNode("//div[@class='product-meta']")?.Remove();
            doc.DocumentNode.SelectSingleNode("//h1")?.Remove();
            doc.DocumentNode.SelectSingleNode("//h2[@class='product-shortname']")?.Remove();
            if (doc.DocumentNode.SelectSingleNode("//img") != null)
                doc.DocumentNode.SelectNodes("//img").ToList().ForEach(img => img.Remove());
            string html = Regex.Replace(doc.DocumentNode.InnerHtml.TrimHtml(), "<a(.*?)>(.*?)</a>", "<strong>$2</strong>");
            sb.AppendLine(html);
        }

        if (!string.IsNullOrWhiteSpace(product.ProductPurposeText))
        {
            sb.AppendLine($"<p>Назначение, применение: {product.ProductPurposeText}</p>");
        }

        if (product.Features.Any())
        {
            sb.AppendLine("<h2>Особенности</h2>");
            sb.AppendLine("<ul>");
            product.Features.ForEach(f => sb.AppendLine($"<li>{f}</li>"));
            sb.AppendLine("</ul>");
        }

        if (product.CharacteristicsTableMarkup != null)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(product.CharacteristicsTableMarkup);
            if (doc.DocumentNode.SelectSingleNode("//img") != null)
                doc.DocumentNode.SelectNodes("//img").ToList().ForEach(img => img.Remove());
            string html = Regex.Replace(doc.DocumentNode.InnerHtml.TrimHtml(), "<a(.*?)>(.*?)</a>", "<strong>$2</strong>");

            sb.AppendLine("<h2>Модельный ряд</h2>");
            sb.AppendLine(html);
        }

        if (product.TechDescriptionTableMarkup != null)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(product.TechDescriptionTableMarkup);
            if (doc.DocumentNode.SelectSingleNode("//img") != null)
                doc.DocumentNode.SelectNodes("//img").ToList().ForEach(img => img.Remove());
            string html = Regex.Replace(doc.DocumentNode.InnerHtml.TrimHtml(), "<a(.*?)>(.*?)</a>", "<strong>$2</strong>");

            sb.AppendLine("<h2>Технические характеристики</h2>");
            sb.AppendLine(html);
        }

        var result = sb.ToString();

        if (result.Contains("<a") || result.Contains("<img") || result.Contains("<iframe"))
        {
            throw new FormatException();
        }

        if (result.Contains("РРЦ"))
        {
            throw new FormatException("Нужно удалить строку с РРЦ ценой товара");
        }

        return result;
    }

    internal string GetImagesSql()
    {
        var sb = new StringBuilder();

        foreach (var product in products)
        {
            if (product.Images.Count == 0)
            {
                continue;
            }

            string imagePath = ImageUriToEtkPath(product.Images.First());
            sb.AppendLine($"UPDATE oc_product SET image = '{imagePath}' WHERE product_id = {GetProductIdSqlCondition(product)};");

            if (product.Images.Count > 1)
            {
                int sort_order = 0;
                foreach (var image in product.Images.Skip(1))
                {
                    string path = ImageUriToEtkPath(image);
                    sb.Append("INSERT INTO oc_product_image (product_id, image, sort_order) VALUES ");
                    sb.AppendLine($"({GetProductIdSqlCondition(product)}, '{path}', {sort_order++});");
                }
            }
        }

        return sb.ToString().TrimHtml();
    }

    private string GetProductIdSqlCondition(Product p)
    {
        if (string.IsNullOrWhiteSpace(p.Sku) || !Regex.IsMatch(p.Sku, @"^\d+$"))
        {
            throw new FormatException(nameof(p.Sku));

        }
        return $"(SELECT product_id FROM oc_product WHERE sku = 'KV-{p.Sku}')";
    }

    private string ImageUriToEtkPath(string image)
    {
        string imageExt = Path.GetExtension(image);
        if (imageExt != ".jpg")
        {
            throw new NotSupportedException("Недопустимый формат изображения: {imageExt}");
        }
        var path = "catalog/kvt/products/" + image.CreateMD5() + Path.GetExtension(image);
        return path;
    }

    internal string GetDimensionsSql()
    {
        var sb = new StringBuilder();
        foreach (var product in products)
        {
            if (product.Dimensions.IsEmpty) { continue; }

            sb.Append("UPDATE oc_product SET ");
            sb.Append($"length = {product.Dimensions.Length.ToString("F0").Replace(",", ".")}, ");
            sb.Append($"width = {product.Dimensions.Width.ToString("F0".Replace(",", "."))}, ");
            sb.Append($"height = {product.Dimensions.Height.ToString("F0").Replace(",", ".")}, ");
            sb.Append($"weight = {product.Dimensions.Weight.ToString("F4").Replace(",", ".")} ");
            sb.AppendLine($"WHERE product_id >= {startId} AND (sku = 'KV-{product.Sku}' OR model = '{product.Sku}');");
        }

        return sb.ToString();
    }
}
