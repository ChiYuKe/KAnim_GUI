using System.Configuration;
using System.Data;
using System.Windows;

using KAnimGui.Application.ResourceBridge;
using KAnimGui.Application.Platform;
using KAnimGui.Application.Preview;
using KAnimGui.Application.Conversion;
using KAnimGui.Infrastructure.Conversion;
using KAnimGui.Infrastructure.ResourceBridge;
using KAnimGui.Infrastructure.Platform;
using KAnimGui.Infrastructure.Preview;
using KAnimGui.Presentation.ResourceBridge;
using KAnimGui.Presentation.Conversion;
using KAnimGui.Presentation.Preview;
using KAnimGui.Presentation.Settings;
using KAnimGui.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace KAnimGui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            services.AddSingleton<IApplicationPathProvider, LocalApplicationPathProvider>();
            services.AddSingleton<IFileSystemGateway, LocalFileSystemGateway>();
            services.AddSingleton<IExternalLauncher, ShellExternalLauncher>();
            services.AddSingleton<IKanimPackageLoader, FileKanimPackageLoader>();
            services.AddSingleton<IPngTextureCodec, PngTextureCodec>();
            services.AddSingleton(_ => new System.Net.Http.HttpClient());
            services.AddSingleton<IResourceBridgeClient, OniResourceBridgeHttpClient>();
            services.AddSingleton<IResourceBridgeStateStore, JsonResourceBridgeStateStore>();
            services.AddSingleton<IResourceBridgeExportService, FileResourceBridgeExportService>();
            services.AddSingleton<IThumbnailCache, FileThumbnailCache>();
            services.AddSingleton<IProcessRunner, CliProcessRunnerService>();
            services.AddSingleton<IOperationLogSink, FileOperationLogSink>();
            services.AddSingleton<IKsePathSettings, PropertiesKsePathSettings>();
            services.AddSingleton<IKseExecutableLocator, KseExecutableLocator>();
            services.AddSingleton<IInputFilePreparer, TxtToBytesFilePreparer>();
            services.AddSingleton<IConversionService, LegacyConversionServiceAdapter>();
            services.AddSingleton<ConversionWorkspaceViewModel>();
            services.AddTransient<OniResourceBridgeViewModel>();
            services.AddTransient<OniResourceBridgeWorkspaceWindow>();
            services.AddTransient<KAnimPreviewLoadService>();
            services.AddTransient<KAnimPreviewRenderService>();
            services.AddTransient<KAnimPreviewImageService>();
            services.AddTransient<KAnimPreviewExportService>();
            services.AddTransient<KAnimRenderWindow>();
            services.AddSingleton<MainWindow>();

            serviceProvider = services.BuildServiceProvider();
            MainWindow mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        public IServiceProvider Services => serviceProvider ??
            throw new InvalidOperationException("应用服务尚未初始化。");

        protected override void OnExit(ExitEventArgs e)
        {
            serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }

}
