using HtmlAgilityPack;
using KvtSu.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace KvtSu;
public class Parser
{
    private readonly HttpClient client;
    private readonly string sitemap_uri = "https://kvt.su/sitemap.xml";
    private readonly string HOST = "https://kvt.su";
    private readonly string resourceFolder;
    private readonly Dictionary<string, Product> parentProductBuffer;
    private readonly bool firstRun = true;
    private readonly string activeRelocationHeader = null;

    public Parser(string resourceFolder)
    {
        var cookieContainer = new CookieContainer();

        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = true,
            CookieContainer = cookieContainer
        };

        cookieContainer.Add(new Cookie()
        {
            Name = "beget",
            Value = "begetok",
            Domain = "kvt.su",
            Path = "/",
            Expires = DateTime.Now.AddDays(7)
        });

        client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br, zstd");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        client.DefaultRequestHeaders.Add("Pragma", "no-cache");
        client.DefaultRequestHeaders.Add("Priority", "u=0, i");
        client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        //client.DefaultRequestHeaders.Add("", "");

        parentProductBuffer = new Dictionary<string, Product>();

        //Нужно обновлять при новом парсинге из реального браузера (application/cookies)
        var cookie = new Cookie("PHPSESSID", "28e60448a880fe30bff1a43cb494ca68")
        {
            Domain = "kvt.su",
            Path = "/",
            HttpOnly = true,
            Secure = false,
            Expires = DateTime.UtcNow.AddDays(14)
        };
        cookieContainer.Add(cookie);
        this.resourceFolder = resourceFolder;
    }

    public async Task<List<Product>> ParseBrand(IEnumerable<string> skuToParse, IProgress<double> progress)
    {
        parentProductBuffer.Clear();

        var dic = await GetSkuToUriDictionary();

        var list = new List<Product>();

        int total = skuToParse.Count();
        int current = 0;

        //Сначала выводим файлы для которых была информация, а далее пустые для ручного заполнения или из прайс-листа

        var notFound = skuToParse.Except(dic.Keys).ToArray();
        if (notFound.Length > 0)
        {
            string missedSku = string.Join(", ", notFound);
            Debug.WriteLine($"Следующие sku ({notFound.Length}) не найдены на сайте {HOST}: {missedSku}");
        }

        foreach (var sku in skuToParse.OrderBy(sku => dic.ContainsKey(sku) ? "0" : "1"))
        {
            var product = new Product()
            {
                Sku = sku
            };

            if (dic.TryGetValue(sku, out var url))
            {
                product.Uri = url;
                await ParseProductDetails(product, isSingleProduct: true);
            }

            list.Add(product);

            progress?.Report((double)++current / total);
        }


        return list;
    }

    public async Task<List<Product>> ParseModelRanges(IProgress<double> progress)
    {
        parentProductBuffer.Clear();

        var urls = await GetModelRangeUrlList();
        var list = new List<Product>();

        int total = urls.Count();
        int current = 0;

        foreach (var url in urls)
        {
            var product = new Product()
            {
                Uri = url
            };

            await ParseProductDetails(product, isSingleProduct: false);

            if (product.Model != null)
            {
                list.Add(product);
            }



            progress?.Report((double)++current / total);

            await RandomDelay();
        }


        return list;
    }

    private async Task PrepareCookies()
    {
        await client.GetStringAsync(HOST);
        await client.GetStringAsync($"{HOST}/kvt_production/history_kvt/");
    }

    private async Task RandomDelay()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(750, 4500)));
    }

    public async Task ParseProductDetails(Product product, bool isSingleProduct, HtmlDocument existedDocument = null)
    {
        var doc = existedDocument ?? await GetDocument(product.Uri);
        if (firstRun)
        {
            await PrepareCookies();
        }

        if (doc == null) { return; }

        if (isSingleProduct)
        {
            product.Name = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.TrimHtml();
            product.ShortDescriptionMarkup = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'product product-single')]/div[2]/table")?.OuterHtml.TrimHtml();
            product.TechDescriptionTableMarkup = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'tab-content')]/div[contains(@class, 'active')]/table")?.OuterHtml.TrimHtml();
            product.ProductPurposeText = doc.DocumentNode.SelectSingleNode("//td[text()='Назначение, применение']/../td[2]")?.InnerText.TrimHtml();


            if (doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'product-gallery')]/div[@class='product-thumbs-wrap']//img") != null)
            {

                var images = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-gallery')]/div[@class='product-thumbs-wrap']//img")
                    .Select(img => HOST + img.GetAttributeValue("src", null))
                    .Distinct()
                    .ToArray();

                product.Images.AddRange(images);
            }

            HtmlNode? lastBcLi = null;
            try
            {
                lastBcLi = doc.DocumentNode
                    .SelectNodes("//ul[contains(@class, 'breadcrumb')]/li/a")
                    .Last();
            }
            catch
            {
                throw new Exception($"Ошибка парсинга Breadcrums для товара с sku: {product.Sku}");
            }

            var parentProductUri = HOST + lastBcLi.GetAttributeValue("href", null);

            if (!parentProductBuffer.ContainsKey(parentProductUri))
            {
                var parentRoot = new Product()
                {
                    Uri = parentProductUri
                };
                await ParseProductDetails(parentRoot, isSingleProduct: false);

                parentProductBuffer[parentProductUri] = parentRoot;
            }

            product.ParentProduct = parentProductBuffer[parentProductUri];
        }
        else
        {
            var isProductPage = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'product product-single')]");
            if (isProductPage == null)
            {
                //Значит это не страница с товаром
                return;
            }

            product.Model = doc.DocumentNode.SelectSingleNode("//ul[@class='breadcrumb']/li[last()]")?.InnerText.TrimHtml();

            if (doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'product-thumb')]/img") != null)
            {
                var parentImages = doc.DocumentNode
                    .SelectNodes("//div[contains(@class, 'product-thumb')]/img")
                    .Select(img => HOST + img.GetAttributeValue("src", String.Empty))
                    .Where(img => img != HOST && !string.IsNullOrWhiteSpace(img))
                    .Distinct()
                    .ToArray();

                product.Images.AddRange(parentImages);
            }

            if (doc.DocumentNode.SelectSingleNode("//div[@id='collapse1-montage-info']//a[contains(@href, '.pdf')]") != null)
            {
                var pdfs = doc.DocumentNode.SelectNodes("//div[@id='collapse1-montage-info']//a[contains(@href, '.pdf')]")
                    .Select(a => new PdfInstruction()
                    {
                        Name = a.InnerText.TrimHtml(),
                        Uri = HOST + a.GetAttributeValue("href", null)
                    }).ToArray();

                product.Instructions.AddRange(pdfs);
            }

            if (doc.DocumentNode.SelectSingleNode("//ul[@class='product-icons']/li/div") != null)
            {
                var features = doc.DocumentNode.SelectNodes("//ul[@class='product-icons']/li/div")
                    .Select(div => div.InnerText.TrimHtml())
                    .ToArray();

                product.Features.AddRange(features);

            }

            if (doc.DocumentNode.SelectSingleNode("//td[@data-id='sku']/label") != null)
            {
                var skus = doc.DocumentNode.SelectNodes("//td[@data-id='sku']/label")
                    .Select(label => label.InnerText.TrimHtml())
                    .Distinct()
                    .ToArray();

                product.ChildProductsSkuList.AddRange(skus);
            }

            product.CharacteristicsTableMarkup = doc.DocumentNode.SelectSingleNode("//div[@id='tech-table']/div/table")?.OuterHtml.TrimHtml();

            doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'accordion accordion-simple')]")?.Remove();
            doc.DocumentNode.SelectSingleNode("//ul[@class='product-icons']")?.Remove();
            product.ShortDescriptionMarkup = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'product product-single')]//div[contains(@class, 'product-details')]").InnerHtml.TrimHtml();
        }

        string lengthString = doc.DocumentNode.SelectSingleNode("//td[text()='Длина потребительской упаковки, см']/following-sibling::td").InnerText.TrimHtml();
        string widthString = doc.DocumentNode.SelectSingleNode("//td[text()='Ширина потребительской упаковки, см']/following-sibling::td").InnerText.TrimHtml();
        string heightString = doc.DocumentNode.SelectSingleNode("//td[text()='Высота потребительской упаковки, см']/following-sibling::td").InnerText.TrimHtml();
        string weightString = doc.DocumentNode.SelectSingleNode("//td[text()='Вес брутто потребительской упаковки, кг']/following-sibling::td").InnerText.TrimHtml();

        if (decimal.TryParse(lengthString.Replace(".", ","), out var lengthCm))
        {
            product.Dimensions.Length = lengthCm * 10;
        }
        if (decimal.TryParse(widthString.Replace(".", ","), out var widthCm))
        {
            product.Dimensions.Width = widthCm * 10;
        }
        if (decimal.TryParse(heightString.Replace(".", ","), out var heightCm))
        {
            product.Dimensions.Height = heightCm * 10;
        }
        if (decimal.TryParse(weightString.Replace(".", ","), out var weightKg))
        {
            product.Dimensions.Weight = weightKg;
        }

    }

    private async Task<Dictionary<string, string>> GetSkuToUriDictionary()
    {

        if (!File.Exists(Path.Combine(resourceFolder, "sitemap.xml")))
        {
            throw new ArgumentException("Необходимо скачать sitemap.xml в папку Downloads");
        }

        using var fs = File.OpenRead(Path.Combine(resourceFolder, "sitemap.xml"));

        var dic = new Dictionary<string, string>();

        XmlDocument doc = new();
        doc.Load(fs);

        XmlElement? xRoot = doc.DocumentElement;

        foreach (XmlNode node in xRoot.GetElementsByTagName("url"))
        {
            string url = node.FirstChild.InnerText;

            var m = Regex.Match(url, @"sku_(\d+)/");

            if (m.Success)
            {
                if (!dic.ContainsKey(m.Groups[1].Value))
                {
                    dic[m.Groups[1].Value] = url;
                }
            }
        }
        return dic;
    }

    private async Task<string[]> GetModelRangeUrlList()
    {
        var dic = await GetSkuToUriDictionary();

        var list = new HashSet<string>();

        foreach (var kvp in dic)
        {
            var m = Regex.Match(kvp.Value, @"https://kvt.su/prod/(.*?)/sku_(\d+)/");

            if (m.Success)
            {
                string modelRangeName = $"https://kvt.su/prod/{m.Groups[1]}/";

                if (!list.Contains(modelRangeName))
                {
                    list.Add(modelRangeName);
                }
            }
        }

        return list.ToArray();
    }

    private async Task<HtmlDocument> GetDocument(string uri)
    {
        try
        {
            var doc = new HtmlDocument();

            //var str = await client.GetStringAsync(uri);

            var req = await client.GetAsync(uri);

            string htmlString = await req.Content.ReadAsStringAsync();

            if (req.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            /*if (req.Headers.TryGetValues("Location", out var reloc))
            {
                activeRelocationHeader = reloc.FirstOrDefault();
            }*/

            doc.LoadHtml(htmlString);
            return doc;
        }
        catch
        {
            return null;
        }

    }

    public async Task<Product> ParseProductDetailsUsinExistHtml(string html, bool isSingleProduct)
    {
        var product = new Product();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        await ParseProductDetails(product, isSingleProduct: true, doc);

        return product;
    }

    internal async Task DownloadSitemap()
    {
        await client.GetAsync(HOST); // заполняем cookie
        var stream = await client.GetStreamAsync(sitemap_uri);
        using var fs = File.Create(Path.Combine(resourceFolder, "sitemap.xml"));
        await stream.CopyToAsync(fs);
    }
}
