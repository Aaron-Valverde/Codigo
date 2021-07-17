﻿using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;

namespace livelywpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static MainWindow _appWindow;
        public static MainWindow AppWindow
        {
            get => _appWindow ??= new MainWindow();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                //create directories if not exist, eg: C:\Users\<User>\AppData\Local
                Directory.CreateDirectory(Program.AppDataDir);
                Directory.CreateDirectory(Path.Combine(Program.AppDataDir, "logs"));
                Directory.CreateDirectory(Path.Combine(Program.AppDataDir, "temp"));
                Directory.CreateDirectory(Path.Combine(Program.AppDataDir, "Cef"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "AppData Directory Initialize Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Program.ExitApplication();
            }

            //Setting up logging.
            NLogger.SetupNLog();
            SetupUnhandledExceptionLogging();
            NLogger.LogHardwareInfo();

            //clear temp files if any.
            FileOperations.EmptyDirectory(Path.Combine(Program.AppDataDir, "temp"));

            //Initialize before viewmodel and main window.
            ScreenHelper.Initialize();

            #region vm init

            Program.SettingsVM = new SettingsViewModel();
            Program.WallpaperDir = Program.SettingsVM.Settings.WallpaperDir;
            try
            {
                CreateWallpaperDir();
            }
            catch (Exception ex)
            {
                Logger.Error("Wallpaper Directory creation fail, falling back to default directory:" + ex.ToString());
                Program.SettingsVM.Settings.WallpaperDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lively Wallpaper", "Library");
                Program.SettingsVM.UpdateConfigFile();
                try
                {
                    CreateWallpaperDir();
                }
                catch (Exception ie)
                {
                    Logger.Error("Wallpaper Directory creation failed, Exiting:" + ie.ToString());
                    MessageBox.Show(ie.Message, "Error: Failed to create wallpaper folder", MessageBoxButton.OK, MessageBoxImage.Error);
                    Program.ExitApplication();
                }
            }

            //previous installed appversion is different from current instance..
            if (!Program.SettingsVM.Settings.AppVersion.Equals(Assembly.GetExecutingAssembly().GetName().Version.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                //todo: show changelog window here..
                Program.SettingsVM.Settings.WallpaperBundleVersion = ExtractWallpaperBundle(Program.SettingsVM.Settings.WallpaperBundleVersion);
                Program.SettingsVM.Settings.AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Program.SettingsVM.UpdateConfigFile();
            }

            Program.AppRulesVM = new ApplicationRulesViewModel();
            Program.LibraryVM = new LibraryViewModel();

            #endregion //vm init

            Application.Current.MainWindow = AppWindow;
            //Creates an empty xaml island control as a temp fix for closing issue; also receives window msg..
            //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/3482
            //Steps to reproduce: Start gif wallpaper using uwp control -> restart lively -> close restored gif wallpaper -> library gridview stops.
            WndProcMsgWindow wndproc = new WndProcMsgWindow();
            wndproc.Show();
            //Package app otherwise bugging out when initialized in settings vm.
            SetupDesktop.SetupInputHooks();
            if (Program.SettingsVM.Settings.IsRestart)
            {
                Program.SettingsVM.Settings.IsRestart = false;
                Program.SettingsVM.UpdateConfigFile();
                AppWindow?.Show();
            }
            base.OnStartup(e);
        }

        /// <summary>
        /// Extract default wallpapers and incremental if any.
        /// </summary>
        public static int ExtractWallpaperBundle(int currentBundleVer)
        {
            //Lively stores the last extracted bundle filename, extraction proceeds from next file onwards.
            int maxExtracted = currentBundleVer;
            try
            {
                //wallpaper bundles filenames are 0.zip, 1.zip ...
                var sortedBundles = Directory.GetFiles(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bundle"))
                    .OrderBy(x => x);

                foreach (var item in sortedBundles)
                {
                    if(int.TryParse(Path.GetFileNameWithoutExtension(item), out int val))
                    {
                        if (val > maxExtracted)
                        {
                            //Sharpzip library will overwrite files if exists during extraction.
                            ZipExtract.ZipExtractFile(item, Path.Combine(Program.WallpaperDir, "wallpapers"), false);
                            maxExtracted = val;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Base Wallpaper Extract Fail:" + e.ToString());
            }
            return maxExtracted;
        }

        private void CreateWallpaperDir()
        {
            Directory.CreateDirectory(Path.Combine(Program.WallpaperDir, "wallpapers"));
            Directory.CreateDirectory(Path.Combine(Program.WallpaperDir, "SaveData", "wptmp"));
            Directory.CreateDirectory(Path.Combine(Program.WallpaperDir, "SaveData", "wpdata"));
        }

        private void SetupUnhandledExceptionLogging()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            Dispatcher.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            string message = $"Unhandled exception ({source})";
            try
            {
                System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                message = string.Format("Unhandled exception in {0} v{1}", assemblyName.Name, assemblyName.Version);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception in LogUnhandledException");
            }
            finally
            {
                Logger.Error("{0}\n{1}", message, exception.ToString());
            }
        }
    }
}
