using KvtSu.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace KvtSu;
public partial class Manager
{
    public class ResourceDownloader
    {
        public async Task DownloadResource(IEnumerable<Product> products, string folder, IProgress<double> indicator)
        {
            int total = products.Count();
            int current = 0;

            foreach (var product in products)
            {

                try
                {
                    await DownloadProductResources(folder, product);
                }
                catch
                {

                }

                indicator?.Report((double)++current / total);
            }
        }

        private async Task DownloadProductResources(string folder, Product product)
        {
            var images = product.Images.Concat(product?.ParentProduct?.Images ?? new List<string>()).Distinct().ToArray();

            foreach (var image in images)
            {
                string localPath = Path.Combine(folder, "products", image.CreateMD5() + Path.GetExtension(image));

                if (!File.Exists(localPath))
                {
                    var dir = Path.GetDirectoryName(localPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    try
                    {
                        await (new WebClient().DownloadFileTaskAsync(image, localPath));
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
            /*
            var instructions = product.Instructions.Concat(product?.ParentProduct.Instructions ?? new List<PdfInstruction>()).Distinct().ToArray();

            foreach (var pdf in instructions)
            {
                string localPath = Path.Combine(folder, "pdf", pdf.Uri.CreateMD5() + Path.GetExtension(pdf.Uri));

                if (!File.Exists(localPath))
                {
                    var dir = Path.GetDirectoryName(localPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    try
                    {
                        await (new WebClient().DownloadFileTaskAsync(pdf.Uri, localPath));
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
        */
        }
    }
}
