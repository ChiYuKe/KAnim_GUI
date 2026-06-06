using KAnimGui.Core;
using KAnimGui.Models;
using KAnimGui.Utils;
using MaterialDesignThemes.Wpf;
using System.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace KAnimGui.Windows
{
    public partial class OniResourceBridgeWindow : Window
    {
        internal const string KAnimExportLayoutGrouped = "Grouped";
        internal const string KAnimExportLayoutSplit = "Split";
        private readonly OniResourceBridgeClient client = new();
        private readonly List<OniBridgeResourceViewModel> resources = new();
        private readonly List<TaskQueueEntry> taskQueue = new();
        private readonly List<RecentActionEntry> recentActions = new();
        private readonly ConcurrentDictionary<string, byte> thumbnailLoads = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> favoriteResourceIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> resourceTags = new(StringComparer.OrdinalIgnoreCase);
        private List<OniBridgeResourceViewModel> filteredResources = new();
        private CancellationTokenSource? batchExportCancellation;
        private CancellationTokenSource? thumbnailLoadCancellation;
        private OniBridgeSnapshot? snapshot;
        private bool hasShownConnectionFailureDialog;

        public OniResourceBridgeWindow()
        {
            InitializeComponent();
            LoadFavorites();
            LoadTags();
            LoadRecentActions();
            UpdateActionButtonLabels();
            Loaded += async (_, _) => await RefreshAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ResourceTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            UpdateActionButtonLabels();
            ApplyFilter();
        }

        private void AnimListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItems = AnimListView.SelectedItems.Cast<OniBridgeResourceViewModel>().ToList();
            if (selectedItems.Count == 1)
            {
                var selected = selectedItems[0];
                UpdateDetailPanel(selected);
                _ = LoadThumbnailAsync(selected, CancellationToken.None, force: true);
            }
            else if (selectedItems.Count > 1)
            {
                UpdateMultiSelectionDetailPanel(selectedItems);
            }
            else
            {
                UpdateDetailPanel(null);
            }

            UpdateImportButtonState();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (snapshot == null || AnimListView.SelectedItem is not OniBridgeResourceViewModel selected)
            {
                return;
            }

            await ImportAsync(selected);
        }

        private async void ExportFilteredButton_Click(object sender, RoutedEventArgs e)
        {
            if (snapshot == null)
            {
                return;
            }

            var candidates = filteredResources.Where(item => item.CanImport).ToList();
            if (candidates.Count == 0)
            {
                ShowMessage("当前筛选结果里没有可导出的资源。", "批量导出", PackIconKind.AlertCircle);
                return;
            }

            await ExportItemsAsync(candidates, "批量导出");
        }

        private void OpenExportFolderButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            string folderPath = ResourceTypeComboBox.SelectedIndex == 1
                ? GetSpriteExportDirectory()
                : GetKAnimExportOpenFolder();
            Directory.CreateDirectory(folderPath);
            OpenFolder(folderPath);
        }

        private void CancelBatchButton_Click(object sender, RoutedEventArgs e)
        {
            batchExportCancellation?.Cancel();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new OniResourceBridgeSettingsWindow();
            TrySetOwner(settingsWindow);

            if (settingsWindow.ShowDialog() == true)
            {
                if (ResourceTypeComboBox.SelectedIndex == 0 && AnimListView.SelectedItem is OniBridgeResourceViewModel selected)
                {
                    UpdateDetailPanel(selected);
                }

                StatusTextBlock.Text = GetKAnimExportLayout() == KAnimExportLayoutSplit
                    ? "KAnim 导出布局已切换为 PNG 和 bytes 分开放"
                    : "KAnim 导出布局已切换为每个资源一个文件夹";
            }
        }

        private async void DetailExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (AnimListView.SelectedItem is OniBridgeResourceViewModel selected)
            {
                await ImportAsync(selected);
            }
        }

        private void DetailOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (AnimListView.SelectedItem is not OniBridgeResourceViewModel selected)
            {
                return;
            }

            string folderPath = selected is OniBridgeSpriteViewModel
                ? GetSpriteExportDirectory()
                : GetKAnimExportLayout() == KAnimExportLayoutSplit
                    ? GetKAnimPngExportDirectory()
                    : GetItemExportDirectory(selected);
            Directory.CreateDirectory(folderPath);
            OpenFolder(folderPath);
        }

        private async Task RefreshAsync()
        {
            SetBusy(true, "正在连接游戏资源桥...");

            try
            {
                snapshot = await client.GetSnapshotAsync(CancellationToken.None);
                resources.Clear();
                thumbnailLoads.Clear();
                CancelThumbnailLoads();
                resources.AddRange(snapshot.Anims
                    .Concat(snapshot.OfflineAnims)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new OniBridgeAnimViewModel(
                        item,
                        favoriteResourceIds.Contains(item.Id),
                        resourceTags.TryGetValue(item.Id, out var tags) ? tags : new List<string>())));
                resources.AddRange(snapshot.Sprites
                    .Concat(snapshot.OfflineSprites)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new OniBridgeSpriteViewModel(
                        item,
                        favoriteResourceIds.Contains(item.Id),
                        resourceTags.TryGetValue(item.Id, out var tags) ? tags : new List<string>())));
                UpdateDetailPanel(null);
                Title = $"ONI 资源桥  端口 {snapshot.Status.Port}";
                ConnectionText.Text = $"已加载资源 {snapshot.Status.AnimCount} / 游戏资源包 {snapshot.Status.ResourcePackageCount}";
                StatusTextBlock.Text = snapshot.Status.AssetsReady
                    ? $"已读取 {snapshot.Anims.Count} 个已加载资源，扫描到 {snapshot.OfflineAnims.Count} 个离线资源"
                    : $"资源桥已连接，但游戏资源可能还在加载：已加载 {snapshot.Anims.Count}，离线 {snapshot.OfflineAnims.Count}";
                ApplyFilter();
            }
            catch (Exception ex)
            {
                snapshot = null;
                resources.Clear();
                AnimListView.ItemsSource = null;
                UpdateDetailPanel(null);
                Title = "ONI 资源桥";
                ConnectionText.Text = "未连接";
                StatusTextBlock.Text = ex.Message;
                if (!hasShownConnectionFailureDialog || IsKeyboardFocusWithin)
                {
                    hasShownConnectionFailureDialog = true;
                    ShowMessage(ex.Message, "ONI 资源桥连接诊断", PackIconKind.AccessPointNetworkOff);
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ImportAsync(OniBridgeResourceViewModel selected)
        {
            if (snapshot == null)
            {
                return;
            }

            if (!selected.CanImport)
            {
                ShowMessage(selected.ImportStatus, "无法导入", PackIconKind.AlertCircle);
                return;
            }

            var taskEntry = BeginTask("导入资源", 1);
            SetBusy(true, $"正在导入 {selected.Name}...");

            try
            {
                switch (selected)
                {
                    case OniBridgeAnimViewModel anim:
                    {
                        var package = anim.IsOffline
                            ? await client.GetOfflineKAnimPackageAsync(snapshot.BaseUrl, anim.Id, CancellationToken.None)
                            : await client.GetKAnimPackageAsync(snapshot.BaseUrl, anim.Name, CancellationToken.None);
                        var fileSet = SavePackage(package);

                        if (Owner is MainWindow mainWindow)
                        {
                            mainWindow.ImportKanimFileSet(fileSet.PngPath, fileSet.AnimPath, fileSet.BuildPath);
                        }

                        AddRecentAction("导入资源", anim.Name, Path.GetDirectoryName(fileSet.PngPath), "import", anim.Id);
                        break;
                    }
                    case OniBridgeSpriteViewModel sprite:
                    {
                        string exportedPath = await ExportSpriteResourceAsync(sprite, CancellationToken.None);
                        AddRecentAction("导出 Sprite", sprite.Name, Path.GetDirectoryName(exportedPath), "import", sprite.Id);
                        break;
                    }
                    default:
                        throw new InvalidOperationException("不支持的资源类型。");
                }

                StatusTextBlock.Text = $"已导入 {selected.Name}";
                CompleteTask(taskEntry, "已完成");
                if (!AppSettings.NoSuccessPopup)
                {
                    ShowMessage($"已导入：{selected.Name}", "ONI 资源桥", PackIconKind.AccessPointNetwork);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"导入失败：{ex.Message}";
                CompleteTask(taskEntry, "失败");
                AddRecentAction("导入失败", $"{selected.Name}: {ex.Message}", null, "import", selected.Id);
                ShowMessage(ex.Message, "导入失败", PackIconKind.AlertCircle);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ExportItemsAsync(IReadOnlyList<OniBridgeResourceViewModel> items, string actionTitle)
        {
            if (snapshot == null)
            {
                return;
            }

            var taskEntry = BeginTask(actionTitle, items.Count);
            batchExportCancellation = new CancellationTokenSource();
            SetBusy(true, $"正在准备{actionTitle} {items.Count} 个资源...");
            int successCount = 0;
            List<string> failures = new();

            try
            {
                for (int index = 0; index < items.Count; index++)
                {
                    batchExportCancellation.Token.ThrowIfCancellationRequested();
                    var item = items[index];

                    try
                    {
                        StatusTextBlock.Text = $"正在导出 {index + 1} / {items.Count}: {item.Name}";
                        UpdateTask(taskEntry, index + 1, items.Count, $"正在处理 {item.Name}");
                        switch (item)
                        {
                            case OniBridgeAnimViewModel anim:
                            {
                                var package = anim.IsOffline
                                    ? await client.GetOfflineKAnimPackageAsync(snapshot.BaseUrl, anim.Id, batchExportCancellation.Token)
                                    : await client.GetKAnimPackageAsync(snapshot.BaseUrl, anim.Name, batchExportCancellation.Token);
                                SavePackage(package);
                                break;
                            }
                            case OniBridgeSpriteViewModel sprite:
                                await ExportSpriteResourceAsync(sprite, batchExportCancellation.Token);
                                break;
                            default:
                                throw new InvalidOperationException("不支持的资源类型。");
                        }
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        if (ex is OperationCanceledException)
                        {
                            throw;
                        }

                        failures.Add($"{item.Name}: {ex.Message}");
                    }
                }

                string? reportPath = failures.Count > 0 ? WriteBatchFailureReport(failures) : null;
                AddRecentAction(
                    failures.Count == 0 ? actionTitle + "完成" : actionTitle + "完成（含失败）",
                    failures.Count == 0
                        ? $"成功导出 {successCount} 个资源。"
                        : $"成功 {successCount}，失败 {failures.Count}。{(string.IsNullOrWhiteSpace(reportPath) ? string.Empty : " 已写出失败报告。")}",
                    !string.IsNullOrWhiteSpace(reportPath) ? reportPath : GetExportRootDirectory(),
                    !string.IsNullOrWhiteSpace(reportPath) ? "retry_failures" : "batch_export",
                    null);

                string message = failures.Count == 0
                    ? $"已导出 {successCount} 个资源到文档目录的 KSE_Output\\ONI_Bridge。"
                    : $"已导出 {successCount} 个资源，失败 {failures.Count} 个。\n\n" + string.Join("\n", failures.Take(12));
                if (!string.IsNullOrWhiteSpace(reportPath))
                {
                    message += $"\n\n失败报告：{reportPath}";
                }

                StatusTextBlock.Text = failures.Count == 0
                    ? $"已导出 {successCount} 个资源"
                    : $"导出完成：成功 {successCount}，失败 {failures.Count}";
                CompleteTask(taskEntry, failures.Count == 0 ? "已完成" : $"完成，失败 {failures.Count} 个");
                ShowMessage(message, actionTitle, failures.Count == 0 ? PackIconKind.FolderDownload : PackIconKind.AlertCircle);
            }
            catch (OperationCanceledException)
            {
                StatusTextBlock.Text = $"{actionTitle}已取消";
                CompleteTask(taskEntry, "已取消");
                AddRecentAction(actionTitle + "已取消", $"已成功导出 {successCount} 个资源。", GetExportRootDirectory(), "batch_export", null);
                ShowMessage($"{actionTitle}已取消。\n已成功导出 {successCount} 个资源。", actionTitle, PackIconKind.Cancel);
            }
            finally
            {
                batchExportCancellation?.Dispose();
                batchExportCancellation = null;
                SetBusy(false);
            }
        }

        private ImportedKanimFileSet SavePackage(OniBridgeKAnimPackage package)
        {
            string safeName = MakeSafeFileName(package.Name);
            var exportPaths = GetKAnimExportPaths(safeName);
            Directory.CreateDirectory(Path.GetDirectoryName(exportPaths.AnimPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(exportPaths.BuildPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(exportPaths.PngPath)!);

            var firstTexture = package.Textures?.OrderBy(texture => texture.Index).FirstOrDefault(texture => !string.IsNullOrWhiteSpace(texture.PngBytes));
            if (firstTexture == null)
            {
                throw new InvalidOperationException("这个资源没有独立 PNG 贴图，暂时不能作为完整 KAnim 包导入。");
            }

            byte[] animBytes = DecodeBase64(package.AnimBytes, "anim bytes");
            byte[] buildBytes = DecodeBase64(package.BuildBytes, "build bytes");
            byte[] pngBytes = DecodeBase64(firstTexture.PngBytes, "png texture");

            File.WriteAllBytes(exportPaths.AnimPath, animBytes);
            File.WriteAllBytes(exportPaths.BuildPath, buildBytes);
            File.WriteAllBytes(exportPaths.PngPath, pngBytes);
            return new ImportedKanimFileSet(exportPaths.PngPath, exportPaths.AnimPath, exportPaths.BuildPath);
        }

        private async Task<string> ExportSpriteResourceAsync(OniBridgeSpriteViewModel sprite, CancellationToken cancellationToken)
        {
            var package = sprite.IsOffline
                ? await client.GetOfflineSpritePackageAsync(snapshot!.BaseUrl, sprite.Id, cancellationToken)
                : await client.GetSpritePackageAsync(snapshot!.BaseUrl, sprite.Id, cancellationToken);

            string dir = GetSpriteExportDirectory();
            Directory.CreateDirectory(dir);
            string pngPath = Path.Combine(dir, MakeUniqueSpriteFileName(sprite));
            File.WriteAllBytes(pngPath, DecodeBase64(package.PngBytes, "sprite png"));
            return pngPath;
        }

        private void ApplyFilter()
        {
            string filter = FilterTextBox.Text?.Trim() ?? string.Empty;
            IEnumerable<OniBridgeResourceViewModel> query = resources;

            switch (ResourceTypeComboBox.SelectedIndex)
            {
                case 0:
                    query = query.Where(item => item is OniBridgeAnimViewModel);
                    break;
                case 1:
                    query = query.Where(item => item is OniBridgeSpriteViewModel);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(item => item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            filteredResources = query
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AnimListView.ItemsSource = filteredResources;
            StatusTextBlock.Text = snapshot == null
                ? StatusTextBlock.Text
                : BuildFilterStatusText();

            StartThumbnailLoads(filteredResources.Take(96).ToList());
        }

        private string BuildFilterStatusText()
        {
            int loadedCount = filteredResources.Count(item => !item.IsOffline);
            return $"显示 {filteredResources.Count} / {resources.Count} 个资源（已加载 {loadedCount}）";
        }

        private void SetBusy(bool isBusy, string? status = null)
        {
            BusyProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            RefreshButton.IsEnabled = !isBusy;
            ExportFilteredButton.IsEnabled = !isBusy;
            CancelBatchButton.Visibility = batchExportCancellation != null ? Visibility.Visible : Visibility.Collapsed;
            CancelBatchButton.IsEnabled = batchExportCancellation != null;
            DetailExportButton.IsEnabled = !isBusy && AnimListView.SelectedItem is OniBridgeResourceViewModel { CanImport: true };
            DetailOpenFolderButton.IsEnabled = AnimListView.SelectedItem is OniBridgeResourceViewModel;
            UpdateImportButtonState(isBusy);
            if (!string.IsNullOrWhiteSpace(status))
            {
                StatusTextBlock.Text = status;
            }
        }

        private void UpdateMultiSelectionDetailPanel(IReadOnlyList<OniBridgeResourceViewModel> items)
        {
            DetailNameTextBlock.Text = "多选模式";
            DetailTypeTextBlock.Text = $"{items.Count} 项";
            DetailSourceTextBlock.Text = "批量";
            DetailSummaryTextBlock.Text = $"已选择 {items.Count} 个资源";
            DetailStatusTextBlock.Text = "可以使用顶部的批量导出处理当前筛选结果。";
            DetailPreviewImage.Source = null;
            DetailPreviewEmptyText.Visibility = Visibility.Visible;
            DetailExportButton.IsEnabled = false;
            DetailOpenFolderButton.IsEnabled = false;
        }

        private void UpdateDetailPanel(OniBridgeResourceViewModel? item)
        {
            if (item == null)
            {
                DetailNameTextBlock.Text = "-";
                DetailTypeTextBlock.Text = "-";
                DetailSourceTextBlock.Text = "-";
                DetailSummaryTextBlock.Text = "选择一个资源后查看详情";
                DetailStatusTextBlock.Text = string.Empty;
                DetailPreviewImage.Source = null;
                DetailPreviewEmptyText.Visibility = Visibility.Visible;
                DetailExportButton.IsEnabled = false;
                DetailOpenFolderButton.IsEnabled = false;
                return;
            }

            DetailNameTextBlock.Text = item.Name;
            DetailTypeTextBlock.Text = item.ResourceType;
            DetailSourceTextBlock.Text = item.SourceLabel;
            DetailSummaryTextBlock.Text = item.SummaryText;
            DetailStatusTextBlock.Text = item.ImportStatus;
            DetailPreviewImage.Source = item.Thumbnail;
            DetailPreviewEmptyText.Visibility = item.Thumbnail == null ? Visibility.Visible : Visibility.Collapsed;
            DetailExportButton.IsEnabled = item.CanImport;
            DetailOpenFolderButton.IsEnabled = true;
        }

        private void UpdateImportButtonState(bool isBusy = false)
        {
            ImportButton.IsEnabled = !isBusy && AnimListView.SelectedItem is OniBridgeResourceViewModel { CanImport: true } && snapshot != null;
        }

        private void UpdateActionButtonLabels()
        {
            bool spriteMode = ResourceTypeComboBox.SelectedIndex == 1;
            MaterialDesignThemes.Wpf.HintAssist.SetHint(FilterTextBox, spriteMode ? "搜索 Sprite" : "搜索 KAnim");
            ImportButton.Content = spriteMode ? "导出选中" : "导入选中";
            DetailExportButton.Content = spriteMode ? "导出当前 Sprite" : "仅导出当前";
        }

        private void StartThumbnailLoads(IReadOnlyList<OniBridgeResourceViewModel> items)
        {
            CancelThumbnailLoads();
            thumbnailLoadCancellation = new CancellationTokenSource();
            var token = thumbnailLoadCancellation.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Parallel.ForEachAsync(
                        items,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = 8,
                            CancellationToken = token
                        },
                        async (item, cancellationToken) => await LoadThumbnailAsync(item, cancellationToken));
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private void CancelThumbnailLoads()
        {
            thumbnailLoadCancellation?.Cancel();
            thumbnailLoadCancellation?.Dispose();
            thumbnailLoadCancellation = null;
        }

        private async Task LoadThumbnailAsync(OniBridgeResourceViewModel item, CancellationToken cancellationToken, bool force = false)
        {
            var currentSnapshot = snapshot;
            if (currentSnapshot == null)
            {
                return;
            }

            string loadKey = GetThumbnailLoadKey(item);
            if (!force && !thumbnailLoads.TryAdd(loadKey, 0))
            {
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? cachedPath = GetCachedThumbnailPath(item);
                string? path = cachedPath;

                if (path == null)
                {
                    var preview = item.IsOffline
                        ? await client.GetOfflinePreviewAsync(currentSnapshot.BaseUrl, item.Id, cancellationToken)
                        : await client.GetPreviewAsync(currentSnapshot.BaseUrl, item.Name, cancellationToken);
                    path = SaveThumbnail(item, preview.PngBytes);
                }

                var bitmap = LoadBitmap(path);
                await Dispatcher.InvokeAsync(() => ApplyThumbnail(item, bitmap));
            }
            catch (OperationCanceledException)
            {
                thumbnailLoads.TryRemove(loadKey, out _);
            }
            catch
            {
                thumbnailLoads.TryRemove(loadKey, out _);
                await Dispatcher.InvokeAsync(() => ApplyThumbnail(item, null));
            }
        }

        private void ApplyThumbnail(OniBridgeResourceViewModel item, BitmapImage? thumbnail)
        {
            item.Thumbnail = thumbnail;
            item.HasPreviewLoaded = true;

            if (ReferenceEquals(AnimListView.SelectedItem, item))
            {
                UpdateDetailPanel(item);
                UpdateImportButtonState();
            }
        }

        private static string GetThumbnailLoadKey(OniBridgeResourceViewModel item)
        {
            string sourceFolder = item.IsOffline ? "offline" : "loaded";
            string key = string.IsNullOrWhiteSpace(item.Id) ? item.Name : item.Id;
            return sourceFolder + ":" + key;
        }

        private static string? GetCachedThumbnailPath(OniBridgeResourceViewModel item)
        {
            string path = GetThumbnailPath(item);
            return File.Exists(path) ? path : null;
        }

        private static string SaveThumbnail(OniBridgeResourceViewModel item, string pngBytes)
        {
            string path = GetThumbnailPath(item);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, DecodeBase64(pngBytes, "preview png"));
            return path;
        }

        private static string GetThumbnailPath(OniBridgeResourceViewModel item)
        {
            string sourceFolder = item.IsOffline ? "offline" : "loaded";
            string key = string.IsNullOrWhiteSpace(item.Id) ? item.Name : item.Id;
            string safeName = MakeSafeFileName(key);
            return Path.Combine(
                GetExportRootDirectory(),
                "_thumbs",
                sourceFolder,
                safeName + ".png");
        }

        private static string GetExportRootDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "KSE_Output",
                "ONI_Bridge");
        }

        private static string GetItemExportDirectory(OniBridgeResourceViewModel item)
        {
            return Path.Combine(GetExportRootDirectory(), MakeSafeFileName(item.Name));
        }

        private static string GetSpriteExportDirectory()
        {
            return Path.Combine(GetExportRootDirectory(), "Sprites");
        }

        private static string GetKAnimBytesExportDirectory()
        {
            return Path.Combine(GetExportRootDirectory(), "KAnim_Bytes");
        }

        private static string GetKAnimPngExportDirectory()
        {
            return Path.Combine(GetExportRootDirectory(), "KAnim_Png");
        }

        private static string? WriteBatchFailureReport(IReadOnlyList<string> failures)
        {
            try
            {
                Directory.CreateDirectory(GetExportRootDirectory());
                string path = Path.Combine(
                    GetExportRootDirectory(),
                    "batch_export_failures_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                File.WriteAllLines(path, failures);
                return path;
            }
            catch
            {
                return null;
            }
        }

        private List<OniBridgeResourceViewModel> ResolveFailedItemsFromReport(string reportPath)
        {
            if (!File.Exists(reportPath))
            {
                return new List<OniBridgeResourceViewModel>();
            }

            var names = File.ReadAllLines(reportPath)
                .Select(line => line.Split(new[] { ':' }, 2)[0].Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return resources
                .Where(item => names.Contains(item.Name, StringComparer.OrdinalIgnoreCase) && item.CanImport)
                .ToList();
        }

        private void OpenFolder(string folderPath)
        {
            string fullPath = Path.GetFullPath(folderPath);
            StatusTextBlock.Text = $"打开导出目录：{fullPath}";

            Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
        }

        private void ToggleFavorite(OniBridgeResourceViewModel item)
        {
            if (item.IsFavorite)
            {
                favoriteResourceIds.Remove(item.Id);
                item.SetFavorite(false);
                StatusTextBlock.Text = $"已取消收藏：{item.Name}";
            }
            else
            {
                favoriteResourceIds.Add(item.Id);
                item.SetFavorite(true);
                StatusTextBlock.Text = $"已收藏：{item.Name}";
            }

            SaveFavorites();
            UpdateDetailPanel(item);
            ApplyFilter();
        }

        private void LoadFavorites()
        {
            favoriteResourceIds.Clear();
            string raw = KAnimGui.Properties.Default.FavoriteOniResources ?? string.Empty;
            foreach (string id in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                favoriteResourceIds.Add(id.Trim());
            }
        }

        private void SaveFavorites()
        {
            KAnimGui.Properties.Default.FavoriteOniResources = string.Join("\n", favoriteResourceIds.OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
            KAnimGui.Properties.Default.Save();
        }

        private void LoadTags()
        {
            resourceTags.Clear();
            string raw = KAnimGui.Properties.Default.OniResourceTags ?? string.Empty;
            foreach (string line in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 2)
                {
                    continue;
                }

                string id = parts[0].Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var tags = parts[1]
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(tag => tag.Trim())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                resourceTags[id] = tags;
            }
        }

        private void SaveTags()
        {
            KAnimGui.Properties.Default.OniResourceTags = string.Join(
                "\n",
                resourceTags
                    .Where(pair => pair.Value.Count > 0)
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => pair.Key + "\t" + string.Join(",", pair.Value)));
            KAnimGui.Properties.Default.Save();
        }

        private void AddRecentAction(string title, string detail, string? path, string actionKind, string? actionArg)
        {
            recentActions.Insert(0, new RecentActionEntry(DateTime.Now, title, detail, path, actionKind, actionArg));
            if (recentActions.Count > 12)
            {
                recentActions.RemoveRange(12, recentActions.Count - 12);
            }

            SaveRecentActions();
            RefreshRecentActionsView();
        }

        private void LoadRecentActions()
        {
            recentActions.Clear();
            string raw = KAnimGui.Properties.Default.OniRecentActions ?? string.Empty;
            foreach (string line in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 4)
                {
                    continue;
                }

                if (!long.TryParse(parts[0], out long ticks))
                {
                    continue;
                }

                string actionKind = parts.Length >= 5 ? parts[4] : string.Empty;
                string? actionArg = parts.Length >= 6 && !string.IsNullOrWhiteSpace(parts[5]) ? parts[5] : null;
                recentActions.Add(new RecentActionEntry(new DateTime(ticks), parts[1], parts[2], string.IsNullOrWhiteSpace(parts[3]) ? null : parts[3], actionKind, actionArg));
            }

            RefreshRecentActionsView();
        }

        private void SaveRecentActions()
        {
            KAnimGui.Properties.Default.OniRecentActions = string.Join(
                "\n",
                recentActions.Select(item => string.Join("\t", new[]
                {
                    item.Timestamp.Ticks.ToString(),
                    item.Title.Replace("\t", " "),
                    item.Detail.Replace("\t", " "),
                    item.Path ?? string.Empty,
                    item.ActionKind ?? string.Empty,
                    item.ActionArg ?? string.Empty
                })));
            KAnimGui.Properties.Default.Save();
        }

        private void RefreshRecentActionsView()
        {
            // Recent actions are still persisted for internal bookkeeping, but no longer shown in the UI.
        }

        private TaskQueueEntry BeginTask(string title, int totalCount)
        {
            var entry = new TaskQueueEntry(title, totalCount);
            taskQueue.Insert(0, entry);
            if (taskQueue.Count > 8)
            {
                taskQueue.RemoveRange(8, taskQueue.Count - 8);
            }

            RefreshTaskQueueView();
            return entry;
        }

        private void UpdateTask(TaskQueueEntry entry, int completed, int total, string detail)
        {
            entry.CompletedCount = completed;
            entry.TotalCount = total;
            entry.Detail = detail;
            entry.State = "进行中";
            RefreshTaskQueueView();
        }

        private void CompleteTask(TaskQueueEntry entry, string state)
        {
            entry.State = state;
            if (string.IsNullOrWhiteSpace(entry.Detail))
            {
                entry.Detail = state;
            }
            entry.CompletedAt = DateTime.Now;
            RefreshTaskQueueView();
        }

        private void RefreshTaskQueueView()
        {
            // Task queue is no longer rendered in the detail panel.
        }

        private static BitmapImage LoadBitmap(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 96;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void ShowMessage(string message, string title, PackIconKind iconKind)
        {
            var msgBox = new CustomMessageBox(message, title, iconKind);
            TrySetOwner(msgBox);
            msgBox.ShowDialog();
        }

        private bool TrySetOwner(Window child)
        {
            if (!IsLoaded || !IsVisible)
            {
                return false;
            }

            try
            {
                child.Owner = this;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static byte[] DecodeBase64(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"资源桥返回的 {label} 为空。");
            }

            return Convert.FromBase64String(value);
        }

        private static string MakeSafeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
            string safe = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(safe) ? "oni_kanim" : safe;
        }

        private string MakeUniqueSpriteFileName(OniBridgeSpriteViewModel sprite)
        {
            string safeName = MakeSafeFileName(sprite.Name);
            bool hasDuplicateName = resources
                .OfType<OniBridgeSpriteViewModel>()
                .Count(item => string.Equals(item.Name, sprite.Name, StringComparison.OrdinalIgnoreCase)) > 1;

            if (!hasDuplicateName)
            {
                return safeName + ".png";
            }

            string suffix = GetSpriteFileNameSuffix(sprite.Id);
            string fileName = string.IsNullOrWhiteSpace(suffix) ? safeName : $"{safeName}__{suffix}";
            return fileName + ".png";
        }

        private static string GetSpriteFileNameSuffix(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            int separatorIndex = id.LastIndexOf('|');
            string tail = separatorIndex >= 0 ? id[(separatorIndex + 1)..] : id;
            string safeTail = MakeSafeFileName(tail).Replace(' ', '_').Trim('_');
            return string.IsNullOrWhiteSpace(safeTail) ? string.Empty : safeTail;
        }

        internal static string GetKAnimExportLayout()
        {
            string? layout = KAnimGui.Properties.Default.OniKAnimExportLayout;
            return string.Equals(layout, KAnimExportLayoutSplit, StringComparison.OrdinalIgnoreCase)
                ? KAnimExportLayoutSplit
                : KAnimExportLayoutGrouped;
        }

        private static KAnimExportPaths GetKAnimExportPaths(string safeName)
        {
            if (GetKAnimExportLayout() == KAnimExportLayoutSplit)
            {
                return new KAnimExportPaths(
                    Path.Combine(GetKAnimPngExportDirectory(), safeName + "_0.png"),
                    Path.Combine(GetKAnimBytesExportDirectory(), safeName + "_anim.bytes"),
                    Path.Combine(GetKAnimBytesExportDirectory(), safeName + "_build.bytes"));
            }

            string dir = Path.Combine(GetExportRootDirectory(), safeName);
            return new KAnimExportPaths(
                Path.Combine(dir, safeName + "_0.png"),
                Path.Combine(dir, safeName + "_anim.bytes"),
                Path.Combine(dir, safeName + "_build.bytes"));
        }

        private static string GetKAnimDetailPath(OniBridgeResourceViewModel item)
        {
            string safeName = MakeSafeFileName(item.Name);
            if (GetKAnimExportLayout() == KAnimExportLayoutSplit)
            {
                return $"{GetKAnimPngExportDirectory()} | {GetKAnimBytesExportDirectory()}";
            }

            return Path.Combine(GetExportRootDirectory(), safeName);
        }

        private static string GetKAnimExportOpenFolder()
        {
            return GetExportRootDirectory();
        }

        private static int ParseInt(string value)
        {
            int.TryParse(value, out int result);
            return result;
        }

        private sealed record ImportedKanimFileSet(string PngPath, string AnimPath, string BuildPath);
        private sealed record KAnimExportPaths(string PngPath, string AnimPath, string BuildPath);

        private abstract class OniBridgeResourceViewModel : INotifyPropertyChanged
        {
            protected OniBridgeResourceViewModel(string id, string name, string source, string? bundle, bool isFavorite, List<string> tags)
            {
                Id = id;
                Name = name;
                Source = source;
                Bundle = bundle;
                this.isFavorite = isFavorite;
                this.tags = tags;
            }

            private bool isFavorite;
            private List<string> tags;
            private BitmapImage? thumbnail;
            private bool hasPreviewLoaded;

            public string Id { get; }
            public string Name { get; }
            public string Source { get; }
            public string? Bundle { get; }
            public bool IsOffline => string.Equals(Source, "offline", StringComparison.OrdinalIgnoreCase);
            public string SourceLabel => IsOffline ? "离线" : "已加载";
            public bool IsFavorite => isFavorite;
            public string FavoriteGlyph => isFavorite ? "★" : "";
            public IReadOnlyList<string> Tags => tags;
            public bool HasTags => tags.Count > 0;
            public string TagText => string.Join(", ", tags);
            public BitmapImage? Thumbnail
            {
                get => thumbnail;
                set
                {
                    if (ReferenceEquals(thumbnail, value))
                    {
                        return;
                    }

                    thumbnail = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasThumbnail));
                    OnPropertyChanged(nameof(CanImport));
                    OnPropertyChanged(nameof(ImportStatus));
                }
            }

            public bool HasPreviewLoaded
            {
                get => hasPreviewLoaded;
                set
                {
                    if (hasPreviewLoaded == value)
                    {
                        return;
                    }

                    hasPreviewLoaded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanImport));
                    OnPropertyChanged(nameof(ImportStatus));
                }
            }

            public bool HasThumbnail => Thumbnail != null;
            public abstract string ResourceType { get; }
            public abstract bool CanImport { get; }
            public abstract string ImportStatus { get; }
            public abstract string SummaryText { get; }
            public abstract int AnimCount { get; }
            public abstract int FrameCount { get; }
            public abstract int ElementCount { get; }

            public event PropertyChangedEventHandler? PropertyChanged;

            public void SetFavorite(bool value)
            {
                if (isFavorite == value)
                {
                    return;
                }

                isFavorite = value;
                OnPropertyChanged(nameof(IsFavorite));
                OnPropertyChanged(nameof(FavoriteGlyph));
            }

            public void SetTags(List<string> value)
            {
                tags = value;
                OnPropertyChanged(nameof(Tags));
                OnPropertyChanged(nameof(HasTags));
                OnPropertyChanged(nameof(TagText));
            }

            protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private sealed class OniBridgeAnimViewModel : OniBridgeResourceViewModel
        {
            public OniBridgeAnimViewModel(OniBridgeAnimInfo info, bool isFavorite, List<string> tags)
                : base(info.Id, info.Name, info.Source, info.Bundle, isFavorite, tags)
            {
                AnimCount = info.AnimCount;
                FrameCount = info.FrameCount;
                ElementCount = info.ElementCount;
            }

            public override string ResourceType => "KAnim";
            public bool HasAnimation => AnimCount > 0 && FrameCount > 0;
            public override bool CanImport => HasAnimation;
            public override string ImportStatus
            {
                get
                {
                    if (!HasAnimation)
                    {
                        return "无动画，不能导入";
                    }

                    if (IsOffline)
                    {
                        return HasPreviewLoaded
                            ? string.IsNullOrWhiteSpace(Bundle) ? "离线资源" : $"离线资源 / {Bundle}"
                            : "离线资源，缩略图加载中";
                    }

                    return HasPreviewLoaded ? "无缩略图预览" : "缩略图加载中";
                }
            }
            public override int AnimCount { get; }
            public override int FrameCount { get; }
            public override int ElementCount { get; }
            public override string SummaryText => $"{AnimCount} 动画 / {FrameCount} 帧 / {ElementCount} 元素";
        }

        private sealed class OniBridgeSpriteViewModel : OniBridgeResourceViewModel
        {
            public OniBridgeSpriteViewModel(OniBridgeSpriteInfo info, bool isFavorite, List<string> tags)
                : base(info.Id, info.Name, info.Source, info.Bundle, isFavorite, tags)
            {
                Width = info.Width;
                Height = info.Height;
            }

            public int Width { get; }
            public int Height { get; }
            public override string ResourceType => "Sprite";
            public override bool CanImport => true;
            public override string ImportStatus => IsOffline
                ? HasPreviewLoaded ? $"离线 Sprite / {Bundle}" : "离线 Sprite，缩略图加载中"
                : HasPreviewLoaded ? "已加载 Sprite" : "Sprite 缩略图加载中";
            public override string SummaryText => $"{Width} x {Height}";
            public override int AnimCount => 0;
            public override int FrameCount => Width;
            public override int ElementCount => Height;
        }

        private sealed class RecentActionEntry
        {
            public RecentActionEntry(DateTime timestamp, string title, string detail, string? path, string? actionKind, string? actionArg)
            {
                Timestamp = timestamp;
                Title = title;
                Detail = detail;
                Path = path;
                ActionKind = actionKind ?? string.Empty;
                ActionArg = actionArg;
            }

            public DateTime Timestamp { get; }
            public string Title { get; }
            public string Detail { get; }
            public string? Path { get; }
            public string ActionKind { get; }
            public string? ActionArg { get; }
            public bool CanReplay => ActionKind is "import" or "copy_name";
            public string Summary
            {
                get
                {
                    string prefix = Title.Contains("失败", StringComparison.OrdinalIgnoreCase)
                        ? "[失败] "
                        : Title.Contains("取消", StringComparison.OrdinalIgnoreCase)
                            ? "[取消] "
                            : Title.Contains("完成", StringComparison.OrdinalIgnoreCase)
                                ? "[完成] "
                                : string.Empty;
                    return string.IsNullOrWhiteSpace(Detail) ? prefix + Title : prefix + Title + ": " + Detail;
                }
            }
            public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private sealed class TaskQueueEntry
        {
            public TaskQueueEntry(string title, int totalCount)
            {
                Title = title;
                TotalCount = totalCount;
                State = "排队中";
                CompletedAt = DateTime.Now;
            }

            public string Title { get; }
            public int TotalCount { get; set; }
            public int CompletedCount { get; set; }
            public string State { get; set; }
            public string Detail { get; set; } = string.Empty;
            public DateTime CompletedAt { get; set; }
            public string Summary
            {
                get
                {
                    string prefix = State.Contains("失败", StringComparison.OrdinalIgnoreCase)
                        ? "[失败] "
                        : State.Contains("取消", StringComparison.OrdinalIgnoreCase)
                            ? "[取消] "
                            : State.Contains("完成", StringComparison.OrdinalIgnoreCase) || State.Contains("已完成", StringComparison.OrdinalIgnoreCase)
                                ? "[完成] "
                                : string.Empty;
                    return string.IsNullOrWhiteSpace(Detail) ? prefix + Title : prefix + Title + ": " + Detail;
                }
            }
            public string StatusText => TotalCount > 1
                ? $"{State} · {CompletedCount}/{TotalCount} · {CompletedAt:HH:mm:ss}"
                : $"{State} · {CompletedAt:HH:mm:ss}";
        }
    }
}
