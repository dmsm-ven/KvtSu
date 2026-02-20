using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace KvtSu;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly string resourceFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    private readonly Manager manager;

    private int PID
    {
        get
        {
            if (int.TryParse(txtProductId.Text, out var pid))
            {
                if (pid >= 0)
                {
                    return pid;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(PID));
        }
    }

    public MainWindow()
    {
        InitializeComponent();

        manager = new("products.json", "sku_list.txt", resourceFolder);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        string sitemapLocalPath = Path.Combine(resourceFolder, "sitemap.xml");
        if (!File.Exists(sitemapLocalPath))
        {
            var res = MessageBox.Show("Файл sitemap.xml не найден. Скачать ?", "Информация", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                await manager.DownloadSitemap();
            }
        }
        if (File.Exists(manager.SkuFile))
        {
            int linesCount = (await File.ReadAllLinesAsync(manager.SkuFile)).Count();
            btnParseFromFile.Content = $"Парсинг артикулов из файла sku__list.txt [{linesCount}] артикулов";
        }
    }

    private async void btnParseBrand_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            await manager.ParseBrand(new Progress<double>(v => pbIndicator.Value = v));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void btnGeneralExport_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetGeneralExport(PID);
        Clipboard.SetText(data);
    }

    private void btnAdditionalImages_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetAdditionalImagesExport(PID);
        Clipboard.SetText(data);
    }

    private void btnFillDescriptions_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetDescriptionSql(PID);
        Clipboard.SetText(data);
    }

    private void btnFillAdjacent_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetAdjacentIds(PID);
        Clipboard.SetText(data);
    }

    private void btnFillDescriptionsSingle_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetDescriptionSingleSql();
        Clipboard.SetText(data);
    }

    private async void btnDownloadResources_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            await manager.DownloadResources(resourceFolder, new Progress<double>(v => pbIndicator.Value = v));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void btnImagesSql_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetImagesSql();
        Clipboard.SetText(data);
    }

    private async void btnRawHtmlToDescription_Click(object sender, RoutedEventArgs e)
    {
        btnRawHtmlToDescription.IsEnabled = false;
        try
        {
            string data = await manager.GetDescriptionFromSource(Clipboard.GetText());
            Clipboard.SetText(data);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}");
        }
        finally
        {
            btnRawHtmlToDescription.IsEnabled = true;
        }
    }

    private void btnFillDimensions_Click(object sender, RoutedEventArgs e)
    {
        btnFillDimensions.IsEnabled = false;
        try
        {
            string data = manager.GetDimensionsUpdateSql(PID);
            Clipboard.SetText(data);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}");
        }
        finally
        {
            btnFillDimensions.IsEnabled = true;
        }
    }
}
