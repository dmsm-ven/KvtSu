using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KvtSu;
public partial class Manager
{
    private readonly Parser parser;
    private readonly ResourceDownloader resourceDownloader;
    private readonly JsonSerializerOptions settings;
    private readonly string storageFile;
    private List<Product> products;
    public string SkuFile { get; }

    public Manager(string storageFile, string skuFile, string resourceFolder)
    {
        settings = new JsonSerializerOptions() { WriteIndented = true };


        parser = new Parser(resourceFolder);
        resourceDownloader = new ResourceDownloader();

        this.storageFile = storageFile;
        SkuFile = skuFile;
        Load();
    }

    public async Task ParseBrand(IProgress<double> progress)
    {
        var urls = File.ReadAllLines(SkuFile);
        var list = await parser.ParseBrand(urls, progress);
        //var list = await parser.ParseModelRanges(progress);
        Save(list);
        products = list;
    }

    public string GetDescriptionSql(int pid)
    {
        var formatter = new Formatter(products, pid);
        return formatter.GetDescriptionSingleSql();
    }

    public string GetAdditionalImagesExport(int pid)
    {
        var formatter = new Formatter(products, pid);
        return formatter.GetAdditionalImagesExport();
    }

    public string GetGeneralExport(int pid)
    {
        var formatter = new Formatter(products, pid);
        return formatter.GetGeneralExport();
    }

    private void Save(List<Product> productsToSave)
    {
        if (productsToSave != null)
        {
            var str = JsonSerializer.Serialize(productsToSave, settings);
            File.WriteAllText(storageFile, str);
        }
    }

    internal string GetAdjacentIds(int pid)
    {
        var formatter = new Formatter(products, pid);
        return formatter.GetAdjacent();
    }

    internal string GetDescriptionSingleSql()
    {
        //var str = File.ReadAllText("missing.json");
        //var missing = JsonConvert.DeserializeObject<List<Product>>(str, settings);
        var skuSet = File.ReadAllLines(SkuFile).ToHashSet();
        var missing = this.products.Where(p => skuSet.Contains(p.Sku.Replace("KV-", ""))).ToList();

        var formatter = new Formatter(missing, -1);
        return formatter.GetDescriptionSql();
    }

    private void Load()
    {
        if (File.Exists(storageFile))
        {
            var str = File.ReadAllText(storageFile);
            products = JsonSerializer.Deserialize<List<Product>>(str, settings);
        }
    }

    internal async Task DownloadResources(string resourceFolder, IProgress<double> progress)
    {
        await resourceDownloader.DownloadResource(products, resourceFolder, progress);
    }

    internal string GetImagesSql()
    {
        var skuSet = File.ReadAllLines(SkuFile).ToHashSet();
        var missing = this.products.Where(p => skuSet.Contains(p.Sku.Replace("KV-", ""))).ToList();

        var formatter = new Formatter(missing, -1);
        return formatter.GetImagesSql();
    }

    public async Task<string> GetDescriptionFromSource(string clipboardText)
    {
        var product = await parser.ParseProductDetailsUsinExistHtml(clipboardText, isSingleProduct: true);
        var formatter = new Formatter(Enumerable.Empty<Product>(), -1);
        var desc = formatter.BuildDescriptionForProduct(product);
        return desc;
    }

    internal async Task DownloadSitemap()
    {
        await parser.DownloadSitemap();
    }

    internal string GetDimensionsUpdateSql(int startId)
    {
        var formatter = new Formatter(products, startId);
        return formatter.GetDimensionsSql();
    }
}
